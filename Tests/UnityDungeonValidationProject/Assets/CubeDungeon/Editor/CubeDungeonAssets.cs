using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CubeDungeon.Editor
{
    public static class CubeDungeonAssets
    {
        public const string Root = "Assets/CubeDungeon";
        public const string DemoScene = Root + "/Scenes/CubeDungeonDemo.unity";
        public const string KitPath = Root + "/Resources/CubeDungeonKit.asset";
        public const string PlayerMaterialPath = Root + "/Materials/Player.mat";

        [MenuItem("Tools/Cube Dungeon/Bauteile erstellen")]
        public static void EnsureAssets()
        {
            var kitExists = AssetDatabase.LoadAssetAtPath<CubeDungeonKit>(KitPath) != null;
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Resources");
            Directory.CreateDirectory(Root + "/Scenes");
            if (!kitExists) AssetDatabase.Refresh();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("URP/Lit fehlt.");
            EnsurePlayerMaterial(shader);
            if (kitExists) return;
            var kit = ScriptableObject.CreateInstance<CubeDungeonKit>();
            kit.stonePalette = new Material[5];
            for (int i = 0; i < kit.stonePalette.Length; i++)
            {
                float shade = .34f + i * .035f;
                var material = new Material(shader) { name = "Stone " + (i + 1), enableInstancing = true };
                material.SetColor("_BaseColor", new Color(shade * .96f, shade, shade * 1.04f));
                material.SetFloat("_Smoothness", .08f);
                material.SetFloat("_Metallic", 0);
                AssetDatabase.CreateAsset(material, Root + $"/Materials/Stone{i + 1}.mat");
                kit.stonePalette[i] = material;
            }

            kit.room = SavePiece("Room", kit, root => { Slab(root, -3, BlockKind.Floor); Slab(root, 2, BlockKind.Ceiling); });
            kit.hall = SavePiece("Hall", kit, root => { Slab(root, -3, BlockKind.Floor); Slab(root, 2, BlockKind.Ceiling); });
            kit.stairSpace = SavePiece("StairSpace", kit, root => Slab(root, 2, BlockKind.Ceiling));
            kit.wall = SavePiece("Wall", kit, root =>
            {
                for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 1; y++) Block(root, new Vector3(x, y, 2), BlockKind.Wall);
            });
            kit.doorway = SavePiece("Doorway", kit, root =>
            {
                for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 1; y++)
                        if (Mathf.Abs(x) == 2 || y == 1) Block(root, new Vector3(x, y, 2), BlockKind.Wall);
            });
            kit.pillar = SavePiece("Pillar", kit, root =>
            {
                for (int y = -2; y <= 1; y++) Block(root, new Vector3(2, y, 2), BlockKind.Wall);
            });
            // Daedalus requests arches inside large rooms too. Only a lintel is
            // needed; the existing corner columns provide its supports.
            kit.arch = SavePiece("Arch", kit, root =>
            {
                for (int x = -2; x <= 2; x++) Block(root, new Vector3(x, 1, 2), BlockKind.Wall);
            });
            kit.ramp = SavePiece("Ramp", kit, root =>
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "Plane Ramp";
                plane.transform.SetParent(root, false);
                plane.transform.localRotation = Quaternion.Euler(-45, 0, 0);
                // Unity's plane is 10 x 10. After the cell's 5x scale the
                // endpoints are exactly (-2.5,-2.5) and (+2.5,+2.5) in Y/Z.
                plane.transform.localScale = new Vector3(.1f, .1f, Mathf.Sqrt(2) * .1f);
            });
            AssetDatabase.CreateAsset(kit, KitPath);
            AssetDatabase.SaveAssets();
        }

        static void Slab(Transform root, int y, BlockKind kind)
        {
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++) Block(root, new Vector3(x, y, z), kind);
        }

        static void Block(Transform root, Vector3 position, BlockKind kind)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = kind.ToString();
            obj.transform.SetParent(root, false);
            obj.transform.localPosition = position / 5;
            obj.transform.localScale = Vector3.one / 5;
            obj.AddComponent<CubeBlock>().kind = kind;
        }

        static GameObject SavePiece(string name, CubeDungeonKit kit, System.Action<Transform> fill)
        {
            var obj = new GameObject(name);
            try
            {
                fill(obj.transform);
                foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = kit.stonePalette[2];
                return PrefabUtility.SaveAsPrefabAsset(obj, Root + "/Prefabs/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [MenuItem("Tools/Cube Dungeon/Beispielszene öffnen")]
        public static void OpenDemo()
        {
            EnsureAssets();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(DemoScene)) CreateDemo();
            else UpdateDemoPlayer();
            EditorSceneManager.OpenScene(DemoScene);
        }

        static void UpdateDemoPlayer()
        {
            var activeSceneBeforeUpdate = SceneManager.GetActiveScene();
            var existingScene = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(existingScene);
            try
            {
                ConfigureDemoPlayer(existingScene);
                EditorSceneManager.MarkSceneDirty(existingScene);
                EditorSceneManager.SaveScene(existingScene);
            }
            finally
            {
                if (activeSceneBeforeUpdate.IsValid()) SceneManager.SetActiveScene(activeSceneBeforeUpdate);
                EditorSceneManager.CloseScene(existingScene, true);
            }
        }
        public static void CreateDemo()
        {
            EnsureAssets();
            if (File.Exists(DemoScene))
            {
                var activeSceneBeforeUpdate = SceneManager.GetActiveScene();
                var existingScene = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(existingScene);
                try
                {
                    foreach (var rootObject in existingScene.GetRootGameObjects())
                    {
                        var existingGenerator = rootObject.GetComponentInChildren<CubeDungeonGenerator>();
                        if (existingGenerator == null) continue;
                        existingGenerator.Clear();
                        existingGenerator.generateOnStart = true;
                        EditorUtility.SetDirty(existingGenerator.gameObject);
                        break;
                    }
                    ConfigureDemoPlayer(existingScene);
                    EditorSceneManager.MarkSceneDirty(existingScene);
                    EditorSceneManager.SaveScene(existingScene);
                }
                finally
                {
                    if (activeSceneBeforeUpdate.IsValid()) SceneManager.SetActiveScene(activeSceneBeforeUpdate);
                    EditorSceneManager.CloseScene(existingScene, true);
                }
                return;
            }
            var previousScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(previousScene.path))
                throw new System.InvalidOperationException("Save the active scene before creating the demo scene.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.62f, .66f, .73f);
                var root = new GameObject("Cube Dungeon Generator");
                var generator = root.AddComponent<CubeDungeonGenerator>();
                generator.kit = AssetDatabase.LoadAssetAtPath<CubeDungeonKit>(KitPath);
                generator.showCeilings = false;
                generator.generateOnStart = true;
                ConfigureDemoPlayer(scene);
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(48, -35, 0);
                EditorSceneManager.SaveScene(scene, DemoScene);
            }
            finally
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void EnsurePlayerMaterial(Shader shader)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath) != null) return;
            var material = new Material(shader) { name = "Player" };
            material.SetColor("_BaseColor", new Color(.12f, .72f, .88f));
            material.SetFloat("_Smoothness", .22f);
            material.SetFloat("_Metallic", .05f);
            AssetDatabase.CreateAsset(material, PlayerMaterialPath);
            AssetDatabase.SaveAssets();
        }

        static void ConfigureDemoPlayer(Scene scene)
        {
            CubeDungeonGenerator generator = null;
            Camera camera = null;
            GameObject player = null;
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (generator == null) generator = rootObject.GetComponentInChildren<CubeDungeonGenerator>();
                if (camera == null) camera = rootObject.GetComponentInChildren<Camera>();
                if (player == null && rootObject.name == "Dungeon Player") player = rootObject;
            }

            if (generator == null)
                throw new System.InvalidOperationException("Die Demo-Szene enthält keinen Cube Dungeon Generator.");

            if (camera == null)
                camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.gameObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .06f, .09f);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 70f;
            camera.orthographic = false;
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            camera.transform.rotation = Quaternion.identity;
            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            if (player == null) player = new GameObject("Dungeon Player");
            var characterController = player.GetComponent<CharacterController>();
            if (characterController == null) characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = .35f;
            characterController.center = new Vector3(0f, .9f, 0f);
            characterController.stepOffset = .3f;
            characterController.slopeLimit = 50f;
            characterController.skinWidth = .03f;

            var controller = player.GetComponent<CubeDungeonPlayerController>();
            if (controller == null) controller = player.AddComponent<CubeDungeonPlayerController>();
            controller.dungeonGenerator = generator;
            controller.viewCamera = camera;

            var visual = player.transform.Find("Capsule Visual");
            if (visual == null)
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Capsule Visual";
                capsule.transform.SetParent(player.transform, false);
                capsule.transform.localPosition = new Vector3(0f, .9f, 0f);
                capsule.transform.localScale = new Vector3(.7f, .9f, .7f);
                Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
                visual = capsule.transform;
            }

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(camera.gameObject);
        }
    }
}
