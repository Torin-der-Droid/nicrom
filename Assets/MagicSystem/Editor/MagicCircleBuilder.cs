using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace MagicSystem.Editor
{
    [InitializeOnLoad]
    public static class MagicCircleBuilder
    {
        const string Root = "Assets/MagicSystem";
        const string PrefabPath = Root + "/Prefabs/MagicCircle.prefab";
        const string ScenePath = Root + "/Scenes/MagicCircleDemo.unity";
        const string BeamMaterialPath = Root + "/Materials/CenterBeam.mat";
        const float Diameter = 6f;

        static readonly string[] TexturePaths =
        {
            Root + "/Textures/Layer_1_Aussenring.png",
            Root + "/Textures/Layer_2_RunenSlots.png",
            Root + "/Textures/Layer_3_InnereKreise.png",
            Root + "/Textures/Layer_4_Pentagramm.png",
            Root + "/Textures/Layer_5_Kern.png"
        };

        static MagicCircleBuilder() => EditorApplication.delayCall += EnsureAssets;

        static void EnsureAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(PrefabPath) || !File.Exists(ScenePath) || !File.Exists(BeamMaterialPath)) Build();
        }

        [MenuItem("Tools/Magic Circle/Open Demo")]
        public static void OpenDemo()
        {
            EnsureAssets();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Magic Circle/Rebuild Assets")]
        public static void Build()
        {
            ConfigureTextures();
            var shader = Shader.Find("MagicSystem/Golden Magic Circle URP");
            var beamShader = Shader.Find("MagicSystem/Golden Center Beam URP");
            var dustGraph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(Root + "/VFX/MagicCircleVFX.vfx");
            if (!shader || !beamShader || !dustGraph)
            {
                Debug.Log("Magic Circle wartet auf den Import von Shader und VFX Graph.");
                EditorApplication.delayCall += EnsureAssets;
                return;
            }

            var materials = new Material[5];
            float[] strengths = { 3.2f, 3.8f, 4.2f, 5.2f, 6.6f };
            for (int i = 0; i < materials.Length; i++)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePaths[i]);
                if (!texture)
                {
                    Debug.LogError("Magic Circle texture missing: " + TexturePaths[i]);
                    return;
                }
                materials[i] = CreateMagicMaterial($"Layer_{i + 1}", shader, texture, strengths[i], false);
            }
            var runeHighlightMaterial = CreateMagicMaterial("RuneHighlight", shader,
                AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePaths[1]), 7f, true);
            var beamMaterial = CreateBeamMaterial(beamShader);

            CreatePrefab(materials, runeHighlightMaterial, beamMaterial, dustGraph);
            CreateDemoScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Goldener Magiekreis fertig. Öffnen: Tools > Magic Circle > Open Demo");
        }

        static void ConfigureTextures()
        {
            foreach (string path in TexturePaths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static Material CreateMagicMaterial(string name, Shader shader, Texture2D texture, float strength, bool runeMask)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_TintColor", new Color(1f, .55f, .08f, 1f));
            material.SetFloat("_EmissionStrength", strength);
            material.SetFloat("_Alpha", 1f);
            material.SetFloat("_UseRuneMask", runeMask ? 1f : 0f);
            material.SetFloat("_RuneHalfWidth", 13f);
            material.renderQueue = 3100;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateBeamMaterial(Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
            if (!material)
            {
                material = new Material(shader) { name = "CenterBeam" };
                AssetDatabase.CreateAsset(material, BeamMaterialPath);
            }
            material.shader = shader;
            material.SetColor("_TintColor", new Color(1f, .62f, .08f, 1f));
            material.SetFloat("_EmissionStrength", 12f);
            material.SetFloat("_Alpha", 1f);
            material.SetFloat("_TearProgress", 0f);
            material.renderQueue = 3120;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreatePrefab(Material[] materials, Material runeMaterial, Material beamMaterial,
            VisualEffectAsset dustGraph)
        {
            if (File.Exists(PrefabPath)) AssetDatabase.DeleteAsset(PrefabPath);
            var root = new GameObject("MagicCircle");
            try
            {
                var controller = root.AddComponent<MagicCircleController>();
                Transform[] layers = new Transform[5];
                Renderer[] renderers = new Renderer[5];
                float[] heights = { .004f, .003f, .002f, .001f, .005f };
                string[] names =
                {
                    "Layer_1_Aussenring", "Layer_2_RunenSlots", "Layer_3_InnereKreise",
                    "Layer_4_Pentagramm", "Layer_5_Kern"
                };
                for (int i = 0; i < layers.Length; i++)
                    layers[i] = CreateLayer(root.transform, names[i], heights[i], materials[i], i, out renderers[i]);

                var runeRoot = new GameObject("RuneHighlights").transform;
                runeRoot.SetParent(root.transform, false);
                var runePositions = new Transform[12];
                var runeRenderers = new Renderer[12];
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * 30f * Mathf.Deg2Rad;
                    var marker = new GameObject($"RunePosition_{i:00}").transform;
                    marker.SetParent(root.transform, false);
                    marker.localPosition = new Vector3(Mathf.Sin(angle) * 2.38f, .012f, Mathf.Cos(angle) * 2.38f);
                    marker.localRotation = Quaternion.Euler(0f, i * 30f, 0f);
                    runePositions[i] = marker;

                    var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    overlay.name = $"RuneHighlight_{i:00}";
                    overlay.transform.SetParent(runeRoot, false);
                    overlay.transform.localPosition = new Vector3(0f, .006f + i * .00001f, 0f);
                    overlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    overlay.transform.localScale = Vector3.one * Diameter;
                    Object.DestroyImmediate(overlay.GetComponent<Collider>());
                    var renderer = overlay.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = runeMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sortingOrder = 10 + i;
                    var block = new MaterialPropertyBlock();
                    block.SetFloat("_UseRuneMask", 1f);
                    block.SetFloat("_RuneIndex", i);
                    block.SetFloat("_Alpha", 0f);
                    renderer.SetPropertyBlock(block);
                    runeRenderers[i] = renderer;
                }

                var vfxRoot = new GameObject("MagicCircleVFX").transform;
                vfxRoot.SetParent(root.transform, false);
                var dust = CreateVFX(vfxRoot, "GoldenDust", dustGraph, new Vector3(0f, .05f, 0f), new Vector3(1.55f, .2f, 1.55f));
                var centerBurst = CreateCenterBeam(root.transform, beamMaterial);

                controller.outerRing = layers[0];
                controller.runeSlots = layers[1];
                controller.innerCircles = layers[2];
                controller.pentagram = layers[3];
                controller.core = layers[4];
                controller.runePositions = runePositions;

                var serialized = new SerializedObject(controller);
                SetObject(serialized, "outerRingRenderer", renderers[0]);
                SetObject(serialized, "runeSlotsRenderer", renderers[1]);
                SetObject(serialized, "innerCirclesRenderer", renderers[2]);
                SetObject(serialized, "pentagramRenderer", renderers[3]);
                SetObject(serialized, "coreRenderer", renderers[4]);
                SetArray(serialized.FindProperty("runeHighlights"), runeRenderers);
                SetObject(serialized, "goldenDust", dust);
                SetObject(serialized, "centerBurst", centerBurst);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static MagicCircleCenterBurst CreateCenterBeam(Transform parent, Material material)
        {
            const float beamHeight = 9f;
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "CenterBurstBeam";
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = new Vector3(0f, beamHeight * .5f + .02f, 0f);
            beam.transform.localScale = new Vector3(.09f, beamHeight * .5f, .09f);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            var renderer = beam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 120;
            var burst = beam.AddComponent<MagicCircleCenterBurst>();
            var serialized = new SerializedObject(burst);
            SetObject(serialized, "beamRenderer", renderer);
            serialized.FindProperty("maximumDiameter").floatValue = Diameter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return burst;
        }

        static Transform CreateLayer(Transform parent, string name, float height, Material material,
            int sortingOrder, out Renderer renderer)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0f, height, 0f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Visual";
            quad.transform.SetParent(pivot, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * Diameter;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return pivot;
        }

        static VisualEffect CreateVFX(Transform parent, string name, VisualEffectAsset asset,
            Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var effect = go.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            effect.pause = true;
            return effect;
        }

        static void SetObject(SerializedObject serialized, string name, Object value)
            => serialized.FindProperty(name).objectReferenceValue = value;

        static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
                RenderSettings.ambientLight = new Color(.015f, .02f, .035f);

                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(7.8f, 7.2f, -7.8f);
                camera.transform.LookAt(new Vector3(0f, .25f, 0f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.002f, .004f, .009f);
                camera.fieldOfView = 48f;
                camera.allowHDR = true;
                camera.gameObject.AddComponent<AudioListener>();
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

                var light = new GameObject("Moon Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(.28f, .38f, .58f);
                light.intensity = .55f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                floor.name = "Dark Stone Floor";
                floor.transform.position = new Vector3(0f, -.11f, 0f);
                floor.transform.localScale = new Vector3(5.2f, .08f, 5.2f);
                floor.GetComponent<Renderer>().sharedMaterial = CreateFloorMaterial();

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var circleObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                circleObject.transform.position = Vector3.zero;
                var controller = circleObject.GetComponent<MagicCircleController>();
                var demo = new GameObject("Magic Circle Demo Controls").AddComponent<MagicCircleDemoController>();
                demo.circle = controller;

                string profilePath = Root + "/Materials/MagicCircleBloom.asset";
                if (File.Exists(profilePath)) AssetDatabase.DeleteAsset(profilePath);
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                var bloom = profile.Add<Bloom>();
                bloom.intensity.Override(1.25f);
                bloom.threshold.Override(.55f);
                bloom.scatter.Override(.8f);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                var volume = new GameObject("Golden Bloom").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;

                EditorSceneManager.SaveScene(scene, ScenePath);
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

        static Material CreateFloorMaterial()
        {
            string path = Root + "/Materials/DemoFloor.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!material)
            {
                material = new Material(shader) { name = "DemoFloor" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", new Color(.018f, .023f, .035f));
            material.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
