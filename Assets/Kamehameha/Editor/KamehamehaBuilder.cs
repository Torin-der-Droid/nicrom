using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EnergyEffects.Editor
{
    [InitializeOnLoad]
    public static class KamehamehaBuilder
    {
        const string Root = "Assets/Kamehameha";
        const string PrefabPath = Root + "/Kamehameha.prefab";
        const string ScenePath = Root + "/KamehamehaDemo.unity";
        static KamehamehaBuilder() { EditorApplication.delayCall += EnsureAssets; }

        static void EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!File.Exists(PrefabPath) || !File.Exists(ScenePath)) Build();
        }

        [MenuItem("Tools/Kamehameha/Open Demo")]
        public static void OpenDemo()
        {
            EnsureAssets();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Kamehameha/Create Missing Assets")]
        public static void Build()
        {
            var shader = Shader.Find("Kamehameha/EnergyParticle");
            if (!shader) { Debug.LogError("Kamehameha shader could not be imported."); return; }
            var glow = MaterialAsset("EnergyGlow", shader, 0);
            var ring = MaterialAsset("EnergyRing", shader, 1);
            if (!File.Exists(PrefabPath))
            {
                var root = new GameObject("Kamehameha");
                var fx = root.AddComponent<KamehamehaEffect>();
                fx.core = System(root, "01 White Core", glow);
                fx.aura = System(root, "02 Cyan Aura", glow);
                fx.charge = System(root, "03 Inward Charge Sparks", glow);
                fx.streaks = System(root, "04 Energy Flow", glow);
                fx.impact = System(root, "05 Impact Sparks", glow);
                var fade = fx.impact.colorOverLifetime;
                fade.enabled = true;
                fade.color = FadeGradient();
                fx.shockwave = System(root, "06 Impact Shockwave", ring);
                var main = fx.shockwave.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = 0.5f;
                main.startSize = 4;
                main.startColor = new Color(0.05f, 0.55f, 1, 0.8f);
                var size = fx.shockwave.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.08f, 1, 1));
                var color = fx.shockwave.colorOverLifetime;
                color.enabled = true;
                color.color = FadeGradient();
                fx.shockwave.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
            }
            if (!File.Exists(ScenePath)) CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Kamehameha ready: Assets/Kamehameha/Kamehameha.prefab and KamehamehaDemo.unity. Tools > Kamehameha > Open Demo.");
        }

        static Material MaterialAsset(string name, Shader shader, float ring)
        {
            string path = Root + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(shader) { name = name };
            material.SetFloat("_Ring", ring);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Gradient FadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            return gradient;
        }

        static ParticleSystem System(GameObject parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 5;
            main.startSpeed = 0;
            main.startLifetime = 1;
            main.maxParticles = 1024;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 2;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static void CreateScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.13f, 0.18f, 0.25f);
                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(12, 7, -10);
                camera.transform.LookAt(new Vector3(0, 1.7f, 8));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.008f, 0.014f, 0.03f);
                camera.fieldOfView = 49;
                camera.allowHDR = true;
                camera.gameObject.AddComponent<AudioListener>();
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var light = new GameObject("Key Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.color = new Color(0.6f, 0.75f, 1);
                light.transform.rotation = Quaternion.Euler(45, -25, 0);
                var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Arena.mat");
                if (!groundMaterial)
                {
                    groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    groundMaterial.SetColor("_BaseColor", new Color(0.055f, 0.075f, 0.11f));
                    AssetDatabase.CreateAsset(groundMaterial, Root + "/Arena.mat");
                }
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Arena Floor";
                floor.transform.position = new Vector3(0, -0.3f, 8);
                floor.transform.localScale = new Vector3(22, 0.5f, 30);
                floor.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Raycast Impact Target";
                wall.transform.position = new Vector3(0, 2, 16);
                wall.transform.localScale = new Vector3(6, 4, 0.7f);
                wall.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                instance.transform.position = new Vector3(0, 1.8f, 0);
                var effect = instance.GetComponent<KamehamehaEffect>();
                effect.autoRepeat = true;
                new GameObject("Demo Controls").AddComponent<KamehamehaDemo>().effect = effect;
                var volume = new GameObject("Bloom").AddComponent<Volume>();
                volume.isGlobal = true;
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/Bloom.asset");
                if (!profile)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, Root + "/Bloom.asset");
                    var bloom = profile.Add<Bloom>();
                    bloom.intensity.Override(0.7f);
                    bloom.threshold.Override(1);
                    bloom.scatter.Override(0.65f);
                    AssetDatabase.AddObjectToAsset(bloom, profile);
                    EditorUtility.SetDirty(profile);
                }
                volume.sharedProfile = profile;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
