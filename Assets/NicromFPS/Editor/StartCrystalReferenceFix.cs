using UnityEditor;
using UnityEngine;

namespace NicromFPS.Editor
{
    /// <summary>Resolves the FBX root through AssetDatabase so Unity writes its generated local file ID.</summary>
    [InitializeOnLoad]
    static class StartCrystalReferenceFix
    {
        const string ModelPath = "Assets/models/models/crystals/source/Crystals.fbx";
        const string PlayerPrefabPath = "Assets/NicromFPS/Prefabs/Nicrom FPS Player.prefab";

        static StartCrystalReferenceFix()
        {
            EditorApplication.delayCall += FixReference;
        }

        static void FixReference()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                StartCrystalSpawner spawner = root.GetComponent<StartCrystalSpawner>();
                if (!spawner) return;

                var serialized = new SerializedObject(spawner);
                SerializedProperty property = serialized.FindProperty("crystalModel");
                if (property.objectReferenceValue == model) return;

                property.objectReferenceValue = model;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("Startkristall: FBX-Hauptobjekt wurde korrekt mit dem Spieler-Prefab verknüpft.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
