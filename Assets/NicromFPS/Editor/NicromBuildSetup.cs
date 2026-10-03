using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NicromFPS.Editor
{
    [InitializeOnLoad]
    public static class NicromBuildSetup
    {
        static NicromBuildSetup()
        {
            // The Build Profile scene list does not control which scene the
            // Editor enters when the Play button is pressed. Set the menu as
            // the Editor's Play Mode start scene so its P shortcut can be tested.
            EditorApplication.delayCall += SetMenuAsPlayModeStartScene;
        }

        static void SetMenuAsPlayModeStartScene()
        {
            const string menuPath = "Assets/Scenes/Main Menu.unity";
            var menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(menuPath);
            if (menuScene != null)
                EditorSceneManager.playModeStartScene = menuScene;
            else
                Debug.LogWarning($"Could not set Play Mode start scene; {menuPath} was not found.");
        }

        [MenuItem("Nicrom/Build Setup/Create Main Menu and Order Scenes")]
        public static void CreateMainMenuAndOrderScenes()
        {
            const string menuPath = "Assets/Scenes/Main Menu.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Main Menu");
            root.AddComponent<NicromMainMenu>();
            if (!EditorSceneManager.SaveScene(scene, menuPath))
                throw new System.IO.IOException($"Could not save menu scene to {menuPath}");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(menuPath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Nicrom FPS.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Nicrom Dungeon.unity", true)
            };

            PlayerSettings.productName = "Nicrom FPS";
            AssetDatabase.SaveAssets();
            Debug.Log("Nicrom build scenes ordered: Main Menu, Nicrom FPS, Nicrom Dungeon.");
        }
    }
}
