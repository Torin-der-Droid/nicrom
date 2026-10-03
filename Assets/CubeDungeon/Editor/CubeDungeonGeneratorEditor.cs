using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeDungeon.Editor
{
    [CustomEditor(typeof(CubeDungeonGenerator))]
    public sealed class CubeDungeonGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var generator = (CubeDungeonGenerator)target;
            serializedObject.Update();
            EditorGUILayout.LabelField("Cube Dungeon", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Einzelne 1-m-Cubes · 5-m-Zellen · Daedalus-Layout\nManuelle Blockänderungen werden beim Generieren ersetzt.", MessageType.Info);
            Field("kit", "Bauteile");
            Field("seed", "Seed");
            Field("gridSize", "Raster (X / Ebenen / Z)");
            Field("roomCount", "Raumanzahl");
            Field("minimumRoomSize", "Raumgröße mindestens (X / Z)");
            Field("maximumRoomSize", "Raumgröße höchstens (X / Z)");
            Field("extraConnections", "Zusätzliche Verbindungen");
            Field("generateOnStart", "Beim Spielstart erzeugen");
            bool wasVisible = generator.showCeilings;
            Field("showCeilings", "Decken anzeigen");
            serializedObject.ApplyModifiedProperties();
            if (wasVisible != generator.showCeilings)
            {
                generator.SetCeilingsVisible(generator.showCeilings);
                MarkChanged(generator);
            }
            EditorGUILayout.Space();
            if (!generator.ValidateSettings(out var error))
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            using (new EditorGUI.DisabledScope(generator.IsGenerating))
            {
                if (GUILayout.Button("Generieren")) { generator.Generate(); MarkChanged(generator); }
                if (GUILayout.Button("Neuer Seed + Generieren")) { generator.GenerateNewSeed(); MarkChanged(generator); }
                if (GUILayout.Button("Dungeon löschen")) { generator.Clear(); MarkChanged(generator); }
            }
            if (!string.IsNullOrEmpty(generator.LastMessage))
                EditorGUILayout.HelpBox(generator.LastMessage,
                    generator.FailedConnections > 0 ? MessageType.Warning : MessageType.None);
        }

        void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label));

        static void MarkChanged(CubeDungeonGenerator generator)
        {
            EditorUtility.SetDirty(generator);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            SceneView.RepaintAll();
        }

        [MenuItem("GameObject/Cube Dungeon/Generator", false, 10)]
        public static void CreateGenerator()
        {
            CubeDungeonAssets.EnsureAssets();
            var obj = new GameObject("Cube Dungeon Generator");
            Undo.RegisterCreatedObjectUndo(obj, "Cube Dungeon Generator");
            var generator = obj.AddComponent<CubeDungeonGenerator>();
            generator.kit = Resources.Load<CubeDungeonKit>("CubeDungeonKit");
            Selection.activeGameObject = obj;
        }
    }
}
