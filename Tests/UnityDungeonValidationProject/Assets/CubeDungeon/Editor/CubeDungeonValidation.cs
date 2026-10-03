using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CubeDungeon.Editor
{
    public static class CubeDungeonValidation
    {
        static readonly List<string> results = new List<string>();

        [MenuItem("Tools/Cube Dungeon/Generator prüfen")]
        public static void Run()
        {
            results.Clear();
            CubeDungeonAssets.EnsureAssets();
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var original = SceneManager.GetActiveScene();
            CubeDungeonAssets.CreateDemo();
            Check(File.Exists(CubeDungeonAssets.DemoScene), "Example scene saved");
            var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(testScene);
            try
            {
                var generator = Create("Test A");
                var randomBefore = Random.state;
                generator.Generate();
                Check(generator.GeneratedRoot != null && generator.BlockCount > 100, "Default dungeon generated");
                Check(generator.GeneratedRoomCount == 10, "Default room count");
                Check(generator.FailedConnections == 0, "All default connections found");
                Check(generator.RampCount > 0, "Default has height changes");
                Check(JsonUtility.ToJson(randomBefore) == JsonUtility.ToJson(Random.state), "Global random state restored");
                string first = Signature(generator);
                CheckGeometry(generator);
                generator.Generate();
                Check(first == Signature(generator), "Same seed reproduces geometry and materials");
                Check(generator.transform.Cast<Transform>().Count(t => t.name == "Cube Dungeon") == 1, "Regeneration replaces previous output");

                var second = Create("Test B");
                second.transform.SetPositionAndRotation(new Vector3(300, 20, -200), Quaternion.Euler(0, 90, 0));
                second.Generate();
                Check(first == Signature(second), "Translated/rotated generator preserves local layout");
                var secondOutput = second.GeneratedRoot;
                generator.Clear();
                Check(second.GeneratedRoot == secondOutput && second.BlockCount > 100, "Independent ownership when clearing");
                Check(generator.GeneratedRoot == null, "Clear removes output");
                Object.DestroyImmediate(generator.gameObject);
                Check(second.GeneratedRoot == secondOutput, "Destroying one generator preserves the other");
                Object.DestroyImmediate(second.gameObject);

                var planar = Create("Planar");
                planar.gridSize = new Vector3Int(12, 1, 12);
                planar.roomCount = 4;
                planar.extraConnections = 0;
                planar.Generate();
                Check(planar.GeneratedRoomCount >= 2 && planar.FailedConnections == 0 && planar.RampCount == 0,
                    "Single-floor fallback uses connected MST without ramps");
                Object.DestroyImmediate(planar.gameObject);

                var invalid = Create("Invalid");
                invalid.gridSize = Vector3Int.zero;
                Check(!invalid.ValidateSettings(out _), "Invalid settings rejected");
                Object.DestroyImmediate(invalid.gameObject);
                CheckRampOrientations();

                results.Add("PASS: Editor validation completed");
            }
            catch (Exception error)
            {
                results.Add("FAIL: " + error);
                throw;
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(testScene, true);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/CubeDungeonValidation.txt", results);
                AssetDatabase.SaveAssets();
            }
        }

        static CubeDungeonGenerator Create(string name)
        {
            var result = new GameObject(name).AddComponent<CubeDungeonGenerator>();
            result.kit = AssetDatabase.LoadAssetAtPath<CubeDungeonKit>(CubeDungeonAssets.KitPath);
            result.generateOnStart = false;
            return result;
        }

        static string Signature(CubeDungeonGenerator generator)
        {
            return string.Join(";", generator.GeneratedRoot.GetComponentsInChildren<CubeBlock>()
                .Select(b => b.name + ":" + b.GetComponent<Renderer>().sharedMaterial.name + ":" +
                    generator.transform.InverseTransformPoint(b.transform.position).ToString("F2"))
                .OrderBy(s => s));
        }

        static void CheckGeometry(CubeDungeonGenerator generator)
        {
            var blocks = generator.GeneratedRoot.GetComponentsInChildren<CubeBlock>();
            var coordinates = new HashSet<Vector3Int>();
            foreach (var block in blocks)
            {
                var position = Vector3Int.RoundToInt(block.transform.position);
                Require(coordinates.Add(position), "Duplicate cube at " + position);
                Require((block.transform.lossyScale - Vector3.one).sqrMagnitude < .00001f, "Cube size differs from one meter");
                Require(block.GetComponent<BoxCollider>()?.enabled == true, "Cube collider missing");
                Require(block.GetComponent<Renderer>().sharedMaterial.shader.name == "Universal Render Pipeline/Lit", "Incorrect material shader");
            }
            Check(true, "Unique one-meter cubes, colliders and URP materials");
            var ceilings = blocks.Where(b => b.kind == BlockKind.Ceiling).ToArray();
            Check(ceilings.Length > 0, "Ceiling cubes present");
            generator.SetCeilingsVisible(false);
            Check(ceilings.All(b => !b.GetComponent<Renderer>().enabled && b.GetComponent<BoxCollider>().enabled), "Ceiling hiding preserves collision");
            generator.SetCeilingsVisible(true);
            Check(ceilings.All(b => b.GetComponent<Renderer>().enabled), "Ceilings can be shown again");
            Physics.SyncTransforms();
            foreach (var ramp in generator.GeneratedRoot.GetComponentsInChildren<MeshCollider>())
            {
                var low = ramp.transform.TransformPoint(new Vector3(0, 0, -5));
                var high = ramp.transform.TransformPoint(new Vector3(0, 0, 5));
                Require(Mathf.Abs(high.y - low.y - 5) < .001f, "Incorrect ramp height");
                var along = Vector3.ProjectOnPlane(high - low, Vector3.up).normalized;
                // The floor just outside each endpoint must be level with the ramp.
                foreach (var endpoint in new[] { low - along * .1f, high + along * .1f })
                {
                    var ray = new Ray(endpoint + Vector3.up * .1f, Vector3.down);
                    Require(blocks.Any(b => b.kind == BlockKind.Floor &&
                        b.GetComponent<BoxCollider>().Raycast(ray, out var hit, .11f)), "Ramp does not meet a landing at " + endpoint);
                }
                for (int step = 0; step <= 10; step++)
                {
                    var feet = Vector3.Lerp(low, high, step / 10f);
                    var body = new Bounds(feet + Vector3.up * 1.1f, new Vector3(.5f, 1.8f, .5f));
                    foreach (var block in blocks)
                        Require(!body.Intersects(block.GetComponent<BoxCollider>().bounds), "Blocked ramp clearance at " + feet + " by " + block.name);
                }
            }
            Check(true, "Generated ramps meet landings and have player headroom");
        }

        static void CheckRampOrientations()
        {
            var kit = AssetDatabase.LoadAssetAtPath<CubeDungeonKit>(CubeDungeonAssets.KitPath);
            for (int yaw = 0; yaw < 360; yaw += 90)
            {
                var root = Object.Instantiate(kit.ramp, Vector3.zero, Quaternion.Euler(0, yaw, 0));
                root.transform.localScale = Vector3.one * 5;
                try
                {
                    var collider = root.GetComponentInChildren<MeshCollider>();
                    var low = collider.transform.TransformPoint(new Vector3(0, 0, -5));
                    var high = collider.transform.TransformPoint(new Vector3(0, 0, 5));
                    Physics.SyncTransforms();
                    Require(Mathf.Abs(high.y - low.y - 5) < .001f, "Rotated ramp rise");
                    Require(collider.Raycast(new Ray(Vector3.up * 2, Vector3.down), out var hit, 4), "Rotated ramp collider");
                    Require(Mathf.Abs(hit.point.y) < .001f, "Rotated ramp surface");
                }
                finally { Object.DestroyImmediate(root); }
            }
            Check(true, "Ramp collider and endpoints in all four directions");
        }

        static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }

        static void Check(bool condition, string description)
        {
            Require(condition, description);
            results.Add("PASS: " + description);
            Debug.Log("Cube Dungeon test: " + description);
        }

        public static void BuildPlayer()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            CubeDungeonAssets.CreateDemo();
            Directory.CreateDirectory("Builds/CubeDungeon");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { CubeDungeonAssets.DemoScene },
                locationPathName = "Builds/CubeDungeon/CubeDungeon.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("Logs/CubeDungeonBuild.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Cube Dungeon player build failed");
        }

        public static void RenderPreview()
        {
            var scene = EditorSceneManager.OpenScene(CubeDungeonAssets.DemoScene);
            var camera = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>()).First();
            var texture = new RenderTexture(1280, 900, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(1280, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/CubeDungeonPreview.png", image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
            }
        }

        public static void PlayModeCheck()
        {
            EditorSceneManager.OpenScene(CubeDungeonAssets.DemoScene);
            EditorApplication.EnterPlaymode();
        }

        public static void ProbeSeeds()
        {
            CubeDungeonAssets.EnsureAssets();
            var rows = new List<string>();
            var generator = Create("Probe");
            try
            {
                foreach (var seed in new[] { 42, 123, 20260929 })
                {
                    generator.seed = seed;
                    generator.Generate();
                    rows.Add($"Seed {seed}: rooms {generator.GeneratedRoomCount}, failures {generator.FailedConnections}, ramps {generator.RampCount}");
                    try { CheckGeometry(generator); rows.Add("Geometry PASS"); }
                    catch (Exception error) { rows.Add(error.Message); }
                }
            }
            finally
            {
                Object.DestroyImmediate(generator.gameObject);
                File.WriteAllLines("Logs/CubeDungeonSeeds.txt", rows);
            }
        }
    }
}
