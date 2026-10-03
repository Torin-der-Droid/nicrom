using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace FireballSystem.Editor
{
    [InitializeOnLoad]
    public static class AnimeFireballBuilder
    {
        const string Root = "Assets/FireballSystem";
        const string FireballPrefabPath = Root + "/Prefabs/AnimeFireball.prefab";
        const string ImpactPrefabPath = Root + "/Prefabs/AnimeFireballImpact.prefab";
        const string DemoScenePath = Root + "/Scenes/AnimeFireballDemo.unity";
        const string ParticleTexturePath = Root + "/Textures/SoftParticle.png";

        static AnimeFireballBuilder() => EditorApplication.delayCall += EnsureAssets;

        static void EnsureAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(FireballPrefabPath) || !File.Exists(ImpactPrefabPath) || !File.Exists(DemoScenePath))
                Build();
        }

        [MenuItem("Tools/Anime Fireball/Open Demo")]
        public static void OpenDemo()
        {
            EnsureAssets();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(DemoScenePath);
        }

        [MenuItem("Tools/Anime Fireball/Rebuild Assets")]
        public static void Build()
        {
            CreateFolders();
            CreateSoftParticleTexture();

            Shader coreShader = Shader.Find("FireballSystem/Anime Fireball Core URP");
            Shader particleShader = Shader.Find("FireballSystem/Anime Fireball Particle URP");
            Texture2D particleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexturePath);
            var flameGraph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(Root + "/VFX/AnimeFireballFlames.vfx");
            var wakeGraph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(Root + "/VFX/AnimeFireballWake.vfx");
            var impactGraph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(Root + "/VFX/AnimeFireballImpact.vfx");
            if (!coreShader || !particleShader || !particleTexture || !flameGraph || !wakeGraph || !impactGraph)
            {
                Debug.Log("Anime Fireball wartet auf den Import von Shader und Partikeltextur.");
                EditorApplication.delayCall += EnsureAssets;
                return;
            }

            Material core = CreateMaterial("FireballCore", coreShader, null,
                new Color(1f, .88f, .28f, 1f), 9f, true);
            Material flame = CreateMaterial("FireballFlame", particleShader, particleTexture,
                new Color(1f, .24f, .015f, 1f), 6.5f, true);
            Material spark = CreateMaterial("FireballSpark", particleShader, particleTexture,
                new Color(1f, .62f, .05f, 1f), 8f, true);
            Material smoke = CreateMaterial("FireballSmoke", particleShader, particleTexture,
                new Color(.16f, .12f, .1f, .48f), 1.1f, false);

            GameObject impactPrefab = CreateImpactPrefab(flame, spark, smoke, impactGraph);
            CreateFireballPrefab(core, flame, spark, impactPrefab, flameGraph, wakeGraph);
            CreateDemoScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Anime-Feuerball fertig. Öffnen: Tools > Anime Fireball > Open Demo");
        }

        static void CreateFolders()
        {
            string[] folders = { "Materials", "Prefabs", "Scenes", "Scripts", "Shaders", "Textures", "VFX", "Editor" };
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "FireballSystem");
            foreach (string folder in folders)
            {
                string path = Root + "/" + folder;
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Root, folder);
            }
        }

        static void CreateSoftParticleTexture()
        {
            if (File.Exists(ParticleTexturePath)) return;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + .5f) / size, (y + .5f) / size) * 2f - Vector2.one;
                float radius = p.magnitude;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.7f);
                float hotCore = Mathf.Pow(Mathf.Clamp01(1f - radius * .7f), 2f);
                pixels[y * size + x] = new Color(1f, Mathf.Lerp(.35f, 1f, hotCore), .08f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(ParticleTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(ParticleTexturePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(ParticleTexturePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static Material CreateMaterial(string name, Shader shader, Texture texture, Color tint,
            float emission, bool additive)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (texture) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", tint);
            if (material.HasProperty("_InnerColor")) material.SetColor("_InnerColor", new Color(1f, 1f, .72f, 1f));
            if (material.HasProperty("_OuterColor")) material.SetColor("_OuterColor", new Color(1f, .08f, .005f, 1f));
            material.SetFloat("_EmissionStrength", emission);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend",
                additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = additive ? 3090 : 3070;
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject CreateImpactPrefab(Material flame, Material spark, Material smoke,
            VisualEffectAsset impactGraph)
        {
            if (File.Exists(ImpactPrefabPath)) AssetDatabase.DeleteAsset(ImpactPrefabPath);
            var root = new GameObject("AnimeFireballImpact");
            try
            {
                var impact = root.AddComponent<AnimeFireballImpact>();
                CreateVFX(root.transform, "VFXGraph_ImpactBurst", impactGraph,
                    new Vector3(0f, .05f, 0f), Vector3.one * 1.65f, 1.35f);
                ParticleSystem flash = CreateParticles(root.transform, "ImpactFlash", flame,
                    false, .22f, 0f, .8f, .8f, 1, 0f);
                SetSizeCurve(flash, new AnimationCurve(new Keyframe(0f, .2f), new Keyframe(.18f, 4.8f), new Keyframe(1f, 6f)));

                ParticleSystem explosion = CreateParticles(root.transform, "FireExplosion", flame,
                    false, .55f, 4.8f, .35f, .95f, 44, .08f);
                SetColor(explosion, FireGradient(1f));
                SetSizeCurve(explosion, new AnimationCurve(new Keyframe(0f, .35f), new Keyframe(.25f, 1f), new Keyframe(1f, .05f)));

                ParticleSystem sparks = CreateParticles(root.transform, "ImpactSparks", spark,
                    false, .72f, 8.5f, .04f, .11f, 34, .05f);
                SetColor(sparks, SparkGradient());

                ParticleSystem smokeSystem = CreateParticles(root.transform, "ImpactSmoke", smoke,
                    false, 1.7f, 1.2f, .7f, 1.35f, 16, .22f);
                SetColor(smokeSystem, SmokeGradient());
                var smokeVelocity = smokeSystem.velocityOverLifetime;
                smokeVelocity.enabled = true;
                smokeVelocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                smokeVelocity.y = new ParticleSystem.MinMaxCurve(1.1f, 2.1f);
                smokeVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                var lightObject = new GameObject("ImpactLight");
                lightObject.transform.SetParent(root.transform, false);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, .34f, .035f);
                light.range = 9f;
                light.intensity = 8f;

                var serialized = new SerializedObject(impact);
                serialized.FindProperty("impactLight").objectReferenceValue = light;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, ImpactPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void CreateFireballPrefab(Material coreMaterial, Material flame, Material spark,
            GameObject impactPrefab, VisualEffectAsset flameGraph, VisualEffectAsset wakeGraph)
        {
            if (File.Exists(FireballPrefabPath)) AssetDatabase.DeleteAsset(FireballPrefabPath);
            var root = new GameObject("AnimeFireball");
            try
            {
                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var collider = root.AddComponent<SphereCollider>();
                collider.radius = .5f;
                collider.isTrigger = true;

                var visualRoot = new GameObject("Visuals").transform;
                visualRoot.SetParent(root.transform, false);

                CreateVFX(visualRoot, "VFXGraph_FlameCrown", flameGraph,
                    Vector3.zero, new Vector3(.9f, .9f, 1.05f), 1.75f);
                CreateVFX(visualRoot, "VFXGraph_EnergyWake", wakeGraph,
                    new Vector3(0f, 0f, -.48f), new Vector3(.72f, .72f, 1.55f), 1.45f);

                var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                core.name = "WhiteHotCore";
                core.transform.SetParent(visualRoot, false);
                core.transform.localScale = new Vector3(.88f, .88f, 1.16f);
                Object.DestroyImmediate(core.GetComponent<Collider>());
                var coreRenderer = core.GetComponent<MeshRenderer>();
                coreRenderer.sharedMaterial = coreMaterial;
                coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
                coreRenderer.receiveShadows = false;

                ParticleSystem shell = CreateParticles(visualRoot, "FlameShell", flame,
                    true, .28f, .35f, .35f, .8f, 0, .34f);
                var shellEmission = shell.emission;
                shellEmission.rateOverTime = 48f;
                SetColor(shell, FireGradient(1f));
                SetSizeCurve(shell, new AnimationCurve(new Keyframe(0f, .25f), new Keyframe(.25f, 1f), new Keyframe(1f, .05f)));

                ParticleSystem trail = CreateParticles(visualRoot, "FireTrail", flame,
                    true, .5f, 0f, .22f, .52f, 0, .16f);
                var trailEmission = trail.emission;
                trailEmission.rateOverTime = 62f;
                SetColor(trail, FireGradient(.85f));
                SetSizeCurve(trail, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.45f, .65f), new Keyframe(1f, 0f)));

                ParticleSystem embers = CreateParticles(visualRoot, "Embers", spark,
                    true, .65f, .45f, .035f, .09f, 0, .25f);
                var emberEmission = embers.emission;
                emberEmission.rateOverTime = 24f;
                SetColor(embers, SparkGradient());
                var emberVelocity = embers.velocityOverLifetime;
                emberVelocity.enabled = true;
                emberVelocity.x = new ParticleSystem.MinMaxCurve(-.7f, .7f);
                emberVelocity.y = new ParticleSystem.MinMaxCurve(-.35f, .7f);
                emberVelocity.z = new ParticleSystem.MinMaxCurve(-.35f, .35f);

                var lightObject = new GameObject("FireLight");
                lightObject.transform.SetParent(root.transform, false);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, .32f, .025f);
                light.range = 6f;
                light.intensity = 4.5f;

                var projectile = root.AddComponent<AnimeFireballProjectile>();
                var serialized = new SerializedObject(projectile);
                serialized.FindProperty("impactPrefab").objectReferenceValue = impactPrefab;
                serialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, FireballPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static ParticleSystem CreateParticles(Transform parent, string name, Material material,
            bool loop, float lifetime, float speed, float minSize, float maxSize, int burstCount, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = loop;
            main.playOnAwake = true;
            main.duration = loop ? 1f : Mathf.Max(.1f, lifetime);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .72f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .7f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 512;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            if (burstCount > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 1f;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingOrder = 90;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        static VisualEffect CreateVFX(Transform parent, string name, VisualEffectAsset asset,
            Vector3 localPosition, Vector3 localScale, float playRate)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var effect = go.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            effect.playRate = playRate;
            effect.pause = false;
            return effect;
        }

        static void SetColor(ParticleSystem particles, Gradient gradient)
        {
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }

        static void SetSizeCurve(ParticleSystem particles, AnimationCurve curve)
        {
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        static Gradient FireGradient(float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, .7f), 0f),
                    new GradientColorKey(new Color(1f, .34f, .015f), .32f),
                    new GradientColorKey(new Color(.75f, .015f, 0f), 1f)
                },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * .8f, .55f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        static Gradient SparkGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, .4f, .01f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .55f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        static Gradient SmokeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.28f, .19f, .13f), 0f), new GradientColorKey(new Color(.055f, .045f, .045f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.55f, .18f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        static void CreateDemoScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            bool additive = !Application.isBatchMode;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.018f, .022f, .035f);

                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(12f, 7f, -10f);
                camera.transform.LookAt(new Vector3(0f, 1.7f, 8f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.003f, .005f, .012f);
                camera.fieldOfView = 49f;
                camera.allowHDR = true;
                camera.gameObject.AddComponent<AudioListener>();
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

                var moon = new GameObject("Cool Fill Light").AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.color = new Color(.22f, .32f, .58f);
                moon.intensity = .5f;
                moon.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

                Material floorMaterial = CreateLitMaterial("DemoGround", new Color(.016f, .022f, .035f));
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Dark Ground";
                floor.transform.localScale = new Vector3(2.2f, 1f, 3.2f);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

                var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "Impact Target";
                target.transform.position = new Vector3(0f, 2.2f, 16f);
                target.transform.localScale = new Vector3(5f, 4.4f, .35f);
                target.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial("DemoTarget", new Color(.035f, .04f, .055f));

                var launcherObject = new GameObject("Fireball Launcher");
                launcherObject.transform.position = new Vector3(0f, 1.8f, 0f);
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(launcherObject.transform, false);
                var launcher = launcherObject.AddComponent<AnimeFireballLauncher>();
                var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FireballPrefabPath)
                    .GetComponent<AnimeFireballProjectile>();
                var launcherSerialized = new SerializedObject(launcher);
                launcherSerialized.FindProperty("fireballPrefab").objectReferenceValue = projectilePrefab;
                launcherSerialized.FindProperty("muzzle").objectReferenceValue = muzzle;
                launcherSerialized.FindProperty("projectileSpeed").floatValue = 12f;
                launcherSerialized.ApplyModifiedPropertiesWithoutUndo();

                var demo = new GameObject("Anime Fireball Demo Controls").AddComponent<AnimeFireballDemoController>();
                demo.launcher = launcher;

                string profilePath = Root + "/Materials/FireballBloom.asset";
                if (File.Exists(profilePath)) AssetDatabase.DeleteAsset(profilePath);
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                var bloom = profile.Add<Bloom>();
                bloom.intensity.Override(1.15f);
                bloom.threshold.Override(.45f);
                bloom.scatter.Override(.75f);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                var volume = new GameObject("Fireball Bloom").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;

                EditorSceneManager.SaveScene(scene, DemoScenePath);
            }
            finally
            {
                if (additive)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                }
            }
        }

        static Material CreateLitMaterial(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .22f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
