using System;
using System.Collections.Generic;
using System.IO;
using CubeDungeon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NicromFPS.Editor
{
    public static class DungeonPlacementValidation
    {
        [MenuItem("Tools/Nicrom FPS/Dungeon-Ankunft prüfen")]
        public static void Run()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var results = new List<string>();
            try
            {
                var player = new GameObject("Placement test player");
                var body = player.AddComponent<CharacterController>();
                body.height = 2;
                body.radius = 0.4f;
                body.center = Vector3.up;
                player.transform.position = new Vector3(500, -1000, 500);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = new Vector3(0, -1000.5f, 0);
                floor.transform.localScale = new Vector3(20, 1, 20);
                Physics.SyncTransforms();
                Check(DungeonPlacement.TryFind(new Vector3(0, -1000, 0), body, false, scene, out var safe), "Free floor is accepted");
                Check(Mathf.Abs(safe.y + 999.94f) < 0.01f, "Feet land above floor, not inside it");
                Check(!DungeonPlacement.TryFind(new Vector3(100, -1000, 0), body, false, scene, out _), "Missing ground is rejected");
                Check(!DungeonPlacement.TryFind(new Vector3(0, -1000, 0), body, true, scene, out _), "Exterior arrival requires terrain");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0, -999, 0);
                wall.transform.localScale = new Vector3(8, 2, 8);
                Physics.SyncTransforms();
                Check(!DungeonPlacement.TryFind(new Vector3(0, -1000, 0), body, false, scene, out _), "Blocked arrival is rejected");
                UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(floor);
                results.Add("PASS: free, blocked, missing and exterior-only ground checks");

                foreach (int seed in new[] { 42, 7, 1337 })
                {
                    var generator = new GameObject("Dungeon entry validation").AddComponent<CubeDungeonGenerator>();
                    generator.generateOnStart = false;
                    generator.transform.position = new Vector3(0, -1000, 0);
                    generator.gridSize = new Vector3Int(20, 4, 20);
                    generator.roomCount = 8;
                    generator.seed = seed;
                    generator.Generate();
                    Physics.SyncTransforms();
                    Check(generator.FailedConnections == 0 && generator.GeneratedRoomCount >= 2, "Connected dungeon: " + seed);
                    Check(generator.TryGetPlayerSpawnPosition(out var spawn), "Entry exists: " + seed);
                    Check(DungeonPlacement.TryFind(spawn, body, false, scene, out safe), "FPS capsule fits entry: " + seed);
                    Check(DungeonPlacement.TryFindGate(safe, body, scene, out var gate, out _),
                        "Return gate fits and is reachable from arrival: " + seed);
                    Check(Vector3.Distance(gate, safe) >= 2.2f, "Arrival stays outside gate trigger");
                    results.Add("PASS: dungeon at -1000m, FPS entry and return gate, seed " + seed);
                    UnityEngine.Object.DestroyImmediate(generator.gameObject);
                }
            }
            catch (Exception ex) { results.Add("FAIL: " + ex); throw; }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/NicromDungeonPlacement.txt", results);
            }
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
        }
    }
}
