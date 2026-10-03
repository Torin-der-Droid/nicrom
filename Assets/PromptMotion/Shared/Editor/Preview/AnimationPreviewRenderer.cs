using System;
using PromptMotion.Shared.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PromptMotion.Shared.Preview
{
    /// <summary>
    /// Renders a Humanoid character with a sampled <see cref="AnimationClip"/> into
    /// a texture for display in an editor window. Uses a hand-managed preview scene
    /// (<see cref="EditorSceneManager.NewPreviewScene"/>) plus an off-screen
    /// <see cref="Camera"/> rendering to a <see cref="RenderTexture"/> — this is the
    /// fallback path the class-level remarks previously described as the backup for
    /// <see cref="PreviewRenderUtility"/>, promoted to the default because
    /// <c>PreviewRenderUtility</c> renders black on Unity 6 + URP. Not thread-safe;
    /// must be called from the main editor thread.
    /// </summary>
    /// <remarks>
    /// Design choices for URP compatibility:
    /// <list type="bullet">
    /// <item>The camera is flagged <c>CameraType.Preview</c> so URP skips
    /// post-processing and expensive features that need a full pipeline setup.</item>
    /// <item><see cref="Camera.scene"/> is bound to the preview scene so only
    /// objects we instantiate into that scene are rendered; the user's project
    /// content is untouched.</item>
    /// <item><c>UniversalAdditionalCameraData</c> and
    /// <c>UniversalAdditionalLightData</c> are attached by reflection when the URP
    /// runtime assembly is present; on built-in projects the reflection lookup
    /// fails silently and the camera/lights work without them.</item>
    /// <item>Rendering is on-demand: <see cref="Camera.enabled"/> is false,
    /// <see cref="Render"/> drives each frame via <see cref="Camera.Render"/> into a
    /// sized-to-request <see cref="RenderTexture"/>.</item>
    /// </list>
    /// Public shape (SetCharacter / SetClip / Play / Pause / TogglePlay / Tick /
    /// Render / Dispose / IsPlaying / Loop / Speed / CurrentTime / ClipLength /
    /// HasCharacter / HasClip) is the contract <see cref="Windows.PromptMotionWindow"/>
    /// depends on — do not change it without touching the window.
    /// </remarks>
    public sealed class AnimationPreviewRenderer : IDisposable
    {
        private const string UrpAssembly = "Unity.RenderPipelines.Universal.Runtime";

        private Scene _previewScene;
        private GameObject _cameraGO;
        private Camera _camera;
        private GameObject _keyLightGO;
        private GameObject _fillLightGO;
        private RenderTexture _renderTexture;
        private GameObject _characterInstance;
        private AnimationClip _clip;
        private float _time;

        // --- Orbit / zoom state (transient — not serialised across domain reload). ---
        private Vector3 _pivot;
        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _defaultYaw;
        private float _defaultPitch;
        private float _defaultDistance;

        /// <summary>True while <see cref="Tick"/> is advancing <see cref="CurrentTime"/>.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>When true, playback wraps to the start on reaching the end of the clip.</summary>
        public bool Loop { get; set; } = true;

        /// <summary>Playback-speed multiplier applied to <see cref="Tick"/>'s delta-time.</summary>
        public float Speed { get; set; } = 1f;

        /// <summary>When true, the camera pivot tracks the character's position each frame.</summary>
        public bool FollowCharacter { get; set; }

        /// <summary>Sample time within the current clip, clamped to [0, <see cref="ClipLength"/>].</summary>
        public float CurrentTime
        {
            get => _time;
            set
            {
                _time = Mathf.Clamp(value, 0f, ClipLength);
                SamplePose();
            }
        }

        /// <summary>Duration of the assigned clip in seconds, or 0 if no clip is set.</summary>
        public float ClipLength => _clip != null ? _clip.length : 0f;

        /// <summary>True once <see cref="SetCharacter"/> has successfully instantiated a character.</summary>
        public bool HasCharacter => _characterInstance != null;

        /// <summary>True once <see cref="SetClip"/> has been called with a non-null clip.</summary>
        public bool HasClip => _clip != null;

        /// <summary>Creates the preview scene, camera, and default lighting.</summary>
        public AnimationPreviewRenderer()
        {
            _previewScene = EditorSceneManager.NewPreviewScene();
            BuildCamera();
            BuildLights();
        }

        /// <summary>
        /// Replaces the preview character with an instance of <paramref name="sourcePrefab"/>.
        /// Pass <c>null</c> to clear the preview. The instance lives in the preview scene
        /// (flagged <see cref="HideFlags.HideAndDontSave"/>) and is destroyed on the next
        /// call or on <see cref="Dispose"/>.
        /// </summary>
        public void SetCharacter(GameObject sourcePrefab)
        {
            DestroyInstance();
            if (sourcePrefab == null) return;

            try
            {
                // Instantiate spawns into the active scene by default; immediately
                // re-parent to the preview scene so the object never appears in the
                // user's hierarchy. HideAndDontSave keeps it out of serialization
                // even during that brief interval.
                _characterInstance = UnityEngine.Object.Instantiate(sourcePrefab);
                _characterInstance.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(_characterInstance, _previewScene);
            }
            catch (Exception ex)
            {
                Log.Error($"Preview instantiation failed for '{sourcePrefab.name}': {ex.Message}");
                DestroyInstance();
                return;
            }

            // Pose first, frame second. If a clip is already assigned, SamplePose
            // applies it at t=0, changing the character's silhouette from the bind
            // pose — so FrameCamera must read bounds after the pose is applied,
            // otherwise the camera distance reflects arms-wide-T-pose bounds that
            // no longer match what's on screen.
            _time = 0f;
            SamplePose();
            FrameCamera();
        }

        /// <summary>Assigns the clip to sample. Resets <see cref="CurrentTime"/> to 0 and reframes the camera.</summary>
        public void SetClip(AnimationClip clip)
        {
            _clip = clip;
            _time = 0f;
            SamplePose();
            // Reframe: the clip's t=0 pose generally differs from the bind pose
            // that SetCharacter framed against, so a clip that tucks the arms in
            // would leave the figure tiny relative to the pre-clip framing.
            FrameCamera();
        }

        /// <summary>
        /// Starts advancing time in <see cref="Tick"/>. No-op without both a clip
        /// and a character. If a non-looping clip has reached its end, Play resets
        /// the play head to zero first so the user can re-play without having to
        /// scrub back manually.
        /// </summary>
        public void Play()
        {
            if (_clip == null || _characterInstance == null) return;

            if (!Loop && _time >= _clip.length - 0.0001f)
            {
                _time = 0f;
                SamplePose();
            }
            IsPlaying = true;
        }

        /// <summary>Stops advancing time. The pose at <see cref="CurrentTime"/> stays visible.</summary>
        public void Pause() => IsPlaying = false;

        /// <summary>Toggles between <see cref="Play"/> and <see cref="Pause"/>.</summary>
        public void TogglePlay()
        {
            if (IsPlaying) Pause();
            else Play();
        }

        /// <summary>Advances <see cref="CurrentTime"/> by <paramref name="deltaTime"/> seconds scaled by <see cref="Speed"/>.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsPlaying) return;
            if (_clip == null || _characterInstance == null) return;

            _time += deltaTime * Speed;
            if (Loop && _clip.length > 0f)
            {
                _time %= _clip.length;
                if (_time < 0f) _time += _clip.length;
            }
            else if (_time >= _clip.length)
            {
                _time = _clip.length;
                IsPlaying = false;
            }
            else if (_time < 0f)
            {
                _time = 0f;
            }
            SamplePose();
        }

        /// <summary>
        /// Renders the current frame into a <see cref="RenderTexture"/> sized to
        /// <paramref name="rect"/>. Returns the texture, or <c>null</c> when the
        /// preview has no character or the rect is degenerate.
        /// </summary>
        public Texture Render(Rect rect)
        {
            if (_camera == null || _characterInstance == null) return null;
            if (rect.width < 1f || rect.height < 1f) return null;
            if (FollowCharacter) UpdateFollowPivot();

            var width = Mathf.Max(1, Mathf.CeilToInt(rect.width));
            var height = Mathf.Max(1, Mathf.CeilToInt(rect.height));

            EnsureRenderTexture(width, height);

            _camera.aspect = width / (float)height;
            _camera.targetTexture = _renderTexture;
            _camera.Render();
            _camera.targetTexture = null;

            return _renderTexture;
        }

        /// <summary>Destroys the preview scene's camera/lights/character and releases the render texture.</summary>
        public void Dispose()
        {
            DestroyInstance();

            if (_cameraGO != null)
            {
                UnityEngine.Object.DestroyImmediate(_cameraGO);
                _cameraGO = null;
                _camera = null;
            }
            if (_keyLightGO != null)
            {
                UnityEngine.Object.DestroyImmediate(_keyLightGO);
                _keyLightGO = null;
            }
            if (_fillLightGO != null)
            {
                UnityEngine.Object.DestroyImmediate(_fillLightGO);
                _fillLightGO = null;
            }
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_renderTexture);
                _renderTexture = null;
            }
            if (_previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(_previewScene);
            }
            _clip = null;
        }

        // --- Internals ---

        private void BuildCamera()
        {
            _cameraGO = new GameObject("PromptMotion_PreviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            SceneManager.MoveGameObjectToScene(_cameraGO, _previewScene);

            _camera = _cameraGO.AddComponent<Camera>();
            _camera.enabled = false;                        // we render on-demand
            _camera.scene = _previewScene;                  // only this scene's objects
            _camera.cameraType = CameraType.Preview;        // URP skips post-fx for this type
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Theme.PreviewBackground;
            _camera.fieldOfView = 40f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.transform.position = new Vector3(0f, 1f, 3f);
            _camera.transform.rotation = Quaternion.Euler(10f, 180f, 0f);

            AddUrpComponent(_cameraGO, "UniversalAdditionalCameraData");
        }

        private void BuildLights()
        {
            _keyLightGO = CreateLight("KeyLight", Color.white, 1.2f, new Vector3(50f, 50f, 0f));
            _fillLightGO = CreateLight("FillLight", new Color(0.7f, 0.8f, 1f), 0.6f, new Vector3(-30f, -30f, 0f));
        }

        private GameObject CreateLight(string name, Color color, float intensity, Vector3 eulerAngles)
        {
            var go = new GameObject("PromptMotion_" + name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            SceneManager.MoveGameObjectToScene(go, _previewScene);
            go.transform.rotation = Quaternion.Euler(eulerAngles);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            // Shadows off — PreviewRenderUtility-style lookdev, not a full scene render.
            light.shadows = LightShadows.None;

            AddUrpComponent(go, "UniversalAdditionalLightData");
            return go;
        }

        /// <summary>
        /// Adds URP's per-camera or per-light additional-data component via reflection.
        /// No-ops when URP isn't loaded (the type resolve fails silently) so the
        /// same code path works on built-in-pipeline projects.
        /// </summary>
        private static void AddUrpComponent(GameObject go, string simpleName)
        {
            var typeName = $"UnityEngine.Rendering.Universal.{simpleName}, {UrpAssembly}";
            var type = Type.GetType(typeName);
            if (type == null) return;
            if (go.GetComponent(type) != null) return;
            go.AddComponent(type);
        }

        private void EnsureRenderTexture(int width, int height)
        {
            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height)
            {
                return;
            }
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_renderTexture);
            }
            _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                name = "PromptMotion_PreviewRT"
            };
            _renderTexture.Create();
        }

        private void DestroyInstance()
        {
            if (_characterInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(_characterInstance);
                _characterInstance = null;
            }
        }

        private void SamplePose()
        {
            if (_clip == null || _characterInstance == null) return;
            _clip.SampleAnimation(_characterInstance, _time);
            if (FollowCharacter) UpdateFollowPivot();
        }

        /// <summary>
        /// Orbits the camera around the current pivot. Horizontal motion is yaw
        /// (around world up), vertical motion is pitch (clamped to ±85° so the
        /// camera never flips upside-down). Degrees per call — the window
        /// converts mouse deltas to a reasonable sensitivity.
        /// </summary>
        public void OrbitBy(float deltaYawDegrees, float deltaPitchDegrees)
        {
            if (_characterInstance == null) return;
            _yaw = Mathf.Repeat(_yaw + deltaYawDegrees, 360f);
            _pitch = Mathf.Clamp(_pitch + deltaPitchDegrees, -85f, 85f);
            UpdateCameraTransform();
        }

        /// <summary>
        /// Dollies the camera along its current view direction. Positive
        /// <paramref name="scrollAmount"/> zooms out (matches Unity's scroll-wheel
        /// delta convention). Distance is clamped to a range derived from the
        /// framed default so the user can't zoom inside the mesh or off into
        /// infinity.
        /// </summary>
        public void ZoomBy(float scrollAmount)
        {
            if (_characterInstance == null) return;
            var factor = Mathf.Pow(1.1f, scrollAmount);
            var target = _distance * factor;
            var minDist = Mathf.Max(0.25f, _defaultDistance * 0.1f);
            var maxDist = Mathf.Max(_defaultDistance * 5f, minDist + 1f);
            _distance = Mathf.Clamp(target, minDist, maxDist);
            UpdateCameraTransform();
        }

        /// <summary>
        /// Restores yaw/pitch/distance to the values computed by the last
        /// <see cref="FrameCamera"/> call. Used by the "Reset view" button and
        /// the double-click gesture on the preview viewport.
        /// </summary>
        public void ResetView()
        {
            _yaw = _defaultYaw;
            _pitch = _defaultPitch;
            _distance = _defaultDistance;
            // Pan-aware reset: re-frame the camera so any pan offset is undone.
            // FrameCamera resets _pivot to the character's current bounds centre.
            FrameCamera();
        }

        /// <summary>
        /// Translates the orbit pivot by a screen-aligned delta. Positive
        /// <paramref name="deltaScreenX"/> moves the pivot to the right relative
        /// to the camera; positive <paramref name="deltaScreenY"/> moves it up.
        /// Speed scales with current distance so pan feels consistent across
        /// zoom levels. Used by the Library window for left-mouse-drag pan.
        /// </summary>
        public void PanBy(float deltaScreenX, float deltaScreenY)
        {
            if (_characterInstance == null || _camera == null) return;
            var camTransform = _camera.transform;
            var right = camTransform.right;
            var up = camTransform.up;
            // Distance-scaled pan speed: at default distance, ~1 pixel = ~1 unit / 1000.
            var panSpeed = _distance * 0.0015f;
            _pivot += -right * deltaScreenX * panSpeed + up * deltaScreenY * panSpeed;
            UpdateCameraTransform();
        }

        private void FrameCamera()
        {
            if (_characterInstance == null || _camera == null) return;

            // SkinnedMeshRenderer.bounds is only recomputed when the renderer
            // passes a cull check. In a freshly-created preview scene that never
            // happens, so bounds can be stale (or zero) and the camera ends up
            // framing empty space — pulling the character back to a tiny dot in
            // the middle of the viewport. Force per-frame bounds updates.
            foreach (var smr in _characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true;
            }

            var renderers = _characterInstance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                _pivot = new Vector3(0f, 1f, 0f);
                _defaultDistance = 3f;
            }
            else
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                // Frame off the larger of width vs height — using extents.magnitude
                // mixed depth in, which made T-pose characters look tiny because the
                // arm span inflated the diagonal. 1.05x margin aims for ~70%
                // viewport fill.
                var frameExtent = Mathf.Max(bounds.extents.x, bounds.extents.y);
                frameExtent = Mathf.Max(frameExtent, 0.1f); // guard against zero bounds
                var halfFovRad = Mathf.Deg2Rad * _camera.fieldOfView * 0.5f;
                var distance = frameExtent / Mathf.Max(0.01f, Mathf.Sin(halfFovRad));
                distance = Mathf.Max(1.5f, distance * 1.05f);

                _pivot = bounds.center;
                _defaultDistance = distance;
            }

            // Front-on view: yaw 0 (camera on +Z facing character), pitch 0 (level).
            _defaultYaw = 0f;
            _defaultPitch = 0f;
            _yaw = _defaultYaw;
            _pitch = _defaultPitch;
            _distance = _defaultDistance;

            UpdateCameraTransform();
        }

        private void UpdateFollowPivot()
        {
            if (_characterInstance == null) return;
            var renderers = _characterInstance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                _pivot = _characterInstance.transform.position + Vector3.up;
            }
            else
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                _pivot = bounds.center;
            }
            UpdateCameraTransform();
        }

        private void UpdateCameraTransform()
        {
            if (_camera == null) return;
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var offset = rot * Vector3.forward * _distance;
            _camera.transform.position = _pivot + offset;
            _camera.transform.LookAt(_pivot, Vector3.up);
        }
    }
}
