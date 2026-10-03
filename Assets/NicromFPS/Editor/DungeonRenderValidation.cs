using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CubeDungeon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NicromFPS.Editor
{
    public static class DungeonRenderValidation
    {
        [MenuItem("Tools/Nicrom FPS/Dungeon-Darstellung prüfen")]
        public static void Run()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var originalScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var results = new List<string>();
            try
            {
                var generator = new GameObject("Dungeon render validation").AddComponent<CubeDungeonGenerator>();
                generator.generateOnStart = false;
                generator.transform.SetPositionAndRotation(new Vector3(300, -1000, -200), Quaternion.Euler(0, 90, 0));
                generator.gridSize = new Vector3Int(20, 4, 20);
                generator.roomCount = 8;
                generator.seed = 42;
                generator.showCeilings = true;
                generator.Generate();
                Require(generator.GeneratedRoot && generator.GeneratedRoomCount == 8 && generator.FailedConnections == 0,
                    "Seed 42 must produce a connected eight-room dungeon.");
                Physics.SyncTransforms();

                var root = generator.GeneratedRoot;
                var blocks = root.GetComponentsInChildren<CubeBlock>();
                var originals = blocks.Select(block => block.GetComponent<MeshRenderer>()).ToArray();
                var expectedAll = ReadGeometry(originals);
                var expectedWithoutCeilings = ReadGeometry(blocks.Where(block => block.kind != BlockKind.Ceiling)
                    .Select(block => block.GetComponent<MeshRenderer>()));
                var nonBlocks = root.GetComponentsInChildren<MeshRenderer>()
                    .Where(renderer => !renderer.GetComponent<CubeBlock>()).ToDictionary(renderer => renderer, renderer => renderer.enabled);
                var colliders = root.GetComponentsInChildren<Collider>().ToDictionary(collider => collider,
                    collider => (collider.enabled, collider.isTrigger, collider.bounds, collider.transform.localToWorldMatrix));

                var batches = root.gameObject.AddComponent<CubeDungeonRenderBatches>();
                batches.Build(generator);
                var combined = root.GetComponentsInChildren<MeshRenderer>()
                    .Where(renderer => !renderer.GetComponent<CubeBlock>() && !nonBlocks.ContainsKey(renderer)).ToArray();
                Require(combined.Length == batches.BatchCount && combined.Length > 0, "Each render group has one renderer.");
                Require(batches.SourceRendererCount == originals.Count(renderer => !renderer.enabled),
                    "Only the reported source renderers are disabled.");
                int activeCount = originals.Count(renderer => renderer.enabled) + combined.Count(renderer => renderer.enabled);
                Require(activeCount < originals.Length / 4, "Batching must reduce active cube renderers by at least 75 percent.");
                RequireGeometry(expectedAll, ReadGeometry(originals.Concat(combined)), "Full dungeon geometry");
                foreach (var renderer in combined) CheckGroupBounds(renderer);
                results.Add($"PASS: {originals.Length} cube renderers reduced to {activeCount}; {batches.BatchCount} spatial groups");
                results.Add("PASS: identical world vertices and materials at -1000m with 90-degree rotation; group bounds enclose vertices");

                var disabledSources = originals.Where(renderer => !renderer.enabled).ToArray();
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    generator.SetCeilingsVisible(false);
                    Require(disabledSources.All(renderer => !renderer.enabled), "Hiding ceilings must keep batched originals disabled.");
                    RequireGeometry(expectedWithoutCeilings, ReadGeometry(originals.Concat(combined)), "Ceilings hidden");
                    generator.SetCeilingsVisible(true);
                    Require(disabledSources.All(renderer => !renderer.enabled), "Showing ceilings must not render batched originals twice.");
                    RequireGeometry(expectedAll, ReadGeometry(originals.Concat(combined)), "Ceilings restored");
                }
                int batchCount = batches.BatchCount;
                batches.Build(generator);
                Require(batchCount == batches.BatchCount, "A repeated Build call must not duplicate groups.");
                RequireGeometry(expectedAll, ReadGeometry(originals.Concat(combined)), "Repeated Build");
                results.Add("PASS: repeated ceiling toggles and Build retain exact visible geometry without doubled originals");

                Physics.SyncTransforms();
                var afterColliders = root.GetComponentsInChildren<Collider>();
                Require(afterColliders.Length == colliders.Count && afterColliders.All(colliders.ContainsKey),
                    "Batching must retain every collider and add none.");
                foreach (var pair in colliders)
                {
                    Require(pair.Key.enabled == pair.Value.Item1 && pair.Key.isTrigger == pair.Value.Item2,
                        "Collider enabled/trigger state changed.");
                    Require(Near(pair.Key.bounds.center, pair.Value.Item3.center) && Near(pair.Key.bounds.size, pair.Value.Item3.size)
                        && pair.Key.transform.localToWorldMatrix == pair.Value.Item4, "Collider transform or bounds changed.");
                }
                Require(nonBlocks.All(pair => pair.Key && pair.Key.enabled == pair.Value), "Ramp renderer visibility changed.");
                Require(blocks.Length == root.GetComponentsInChildren<CubeBlock>().Length, "Cube metadata must remain intact.");
                results.Add("PASS: collider identity, state, transforms and bounds; ramp visibility and cube metadata preserved");
                var allocatedMeshes = combined.Select(renderer => renderer.GetComponent<MeshFilter>().sharedMesh).ToArray();
                Object.DestroyImmediate(generator.gameObject);
                Require(allocatedMeshes.All(mesh => !mesh), "Destroying the dungeon must release combined meshes.");
                results.Add("PASS: generated render meshes released with the dungeon");
            }
            catch (Exception error)
            {
                results.Add("FAIL: " + error);
                throw;
            }
            finally
            {
                if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
                EditorSceneManager.CloseScene(scene, true);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/NicromDungeonRender.txt", results);
            }
        }

        // Compare visible geometry independently of the batcher's spatial grouping.
        // A centimeter quantization tolerates matrix float error at the offset while
        // remaining much smaller than a cube or the character's collision skin.
        private static Dictionary<(Material, Vector3Int), int> ReadGeometry(IEnumerable<MeshRenderer> renderers)
        {
            var vertices = new Dictionary<(Material, Vector3Int), int>();
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                foreach (var vertex in mesh.vertices)
                {
                    var key = (renderer.sharedMaterial, Vector3Int.RoundToInt(renderer.transform.TransformPoint(vertex) * 100));
                    vertices.TryGetValue(key, out int count);
                    vertices[key] = count + 1;
                }
            }
            return vertices;
        }

        private static void RequireGeometry(Dictionary<(Material, Vector3Int), int> expected,
            Dictionary<(Material, Vector3Int), int> actual, string description)
        {
            Require(expected.Count == actual.Count && expected.All(pair => actual.TryGetValue(pair.Key, out int count) && count == pair.Value),
                description + ": visible world vertices/materials differ, are missing or duplicated.");
        }

        private static void CheckGroupBounds(MeshRenderer renderer)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            Require(vertices.Length > 0, "Empty render group.");
            var bounds = new Bounds(renderer.transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(renderer.transform.TransformPoint(vertex));
            Require(Near(bounds.center, renderer.bounds.center) && Near(bounds.size, renderer.bounds.size),
                "Render group bounds differ from transformed vertices.");
            Require(mesh.bounds.size.x <= 13.01f && mesh.bounds.size.y <= 7.01f && mesh.bounds.size.z <= 13.01f,
                "Render groups must remain spatially bounded for local culling.");
        }

        private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .0001f;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
