using System.IO;
using UnityEditor;
using UnityEngine;

namespace NicromFPS.Editor
{
    [InitializeOnLoad]
    static class SkullGateImporter
    {
        const string ModelPath = "Assets/models/models/gate/source/Skull Gate/Skull Gate/Skull Gate.obj";
        const string BasePath = "Assets/models/models/gate/textures/Skull Gate_BaseColor.png";
        const string NormalPath = "Assets/models/models/gate/textures/Skull Gate_Normal.png";
        const string RoughnessPath = "Assets/models/models/gate/textures/Skull Gate_Roughness.jpeg";
        const string AoPath = "Assets/models/models/gate/textures/Skull Gate_ao.jpeg";
        const string MetallicPath = "Assets/models/models/gate/source/Skull Gate/Skull Gate/Skull Gate_Metalness.jpg";
        const string MaterialPath = "Assets/NicromFPS/Materials/Skull Gate.mat";
        const string PrefabPath = "Assets/Resources/NicromGate.prefab";

        static SkullGateImporter() => EditorApplication.delayCall += Import;

        static void Import()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!source) return;
            Directory.CreateDirectory("Assets/NicromFPS/Materials");
            Directory.CreateDirectory("Assets/Resources");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "Skull Gate" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            ConfigureMaterial(material);

            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (!instance) return;
            try
            {
                instance.name = "NicromGate";
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = material;
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            AssetDatabase.SaveAssets();
        }

        static void ConfigureMaterial(Material material)
        {
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(AoPath);
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseMap);
            material.mainTexture = baseMap;
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
            if (normal) material.EnableKeyword("_NORMALMAP");
            if (material.HasProperty("_OcclusionMap")) material.SetTexture("_OcclusionMap", ao);
            if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", metallic);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .45f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .32f);
            // Keep the supplied map visible to shaders which support roughness directly.
            Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(RoughnessPath);
            if (material.HasProperty("_RoughnessMap")) material.SetTexture("_RoughnessMap", roughness);
            EditorUtility.SetDirty(material);
        }
    }
}
