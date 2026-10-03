using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDungeon.Editor
{
    public static class CubeDungeonWallValidation
    {
        [MenuItem("Tools/Cube Dungeon/Rampenwände prüfen")]
        public static void Run()
        {
            var report = new List<string>();
            var directions = new HashSet<int>();
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                int totalSeams = 0, totalOpenSides = 0;
                foreach (int seed in new[] { 42, 0, 1, 7, 1337, 8675309 })
                {
                    var generator = new GameObject("Wall regression").AddComponent<CubeDungeonGenerator>();
                    generator.kit = AssetDatabase.LoadAssetAtPath<CubeDungeonKit>(CubeDungeonAssets.KitPath);
                    generator.generateOnStart = false;
                    generator.showCeilings = false;
                    generator.seed = seed;
                    // Include a translated, rotated generator in the same checks.
                    if (seed == 1337)
                        generator.transform.SetPositionAndRotation(new Vector3(300, 20, -200), Quaternion.Euler(0, 90, 0));
                    try
                    {
                        generator.Generate();
                        Require(generator.GeneratedRoot != null && generator.GeneratedRoomCount > 1, "Dungeon failed to generate");
                        Physics.SyncTransforms();
                        var blocks = generator.GeneratedRoot.GetComponentsInChildren<CubeBlock>()
                            .ToDictionary(b => Vector3Int.RoundToInt(b.transform.position));
                        var ramps = generator.GeneratedRoot.GetComponentsInChildren<MeshCollider>();
                        var levels = new HashSet<int>(ramps.Select(r => Mathf.RoundToInt(r.transform.parent.position.y) + 2));
                        var holes = new List<Vector3Int>();
                        int seams = 0;
                        foreach (var pair in blocks)
                        {
                            var middle = pair.Key + Vector3Int.up;
                            if (pair.Value.kind != BlockKind.Wall || !levels.Contains(middle.y) ||
                                !blocks.TryGetValue(middle + Vector3Int.up, out var upper) || upper.kind != BlockKind.Wall)
                                continue;
                            if (!blocks.TryGetValue(middle, out var join))
                            {
                                holes.Add(middle);
                                continue;
                            }
                            Require(join.GetComponent<Renderer>().enabled, "Invisible wall join at " + middle);
                            var collider = join.GetComponent<BoxCollider>();
                            foreach (var direction in new[] { Vector3.right, Vector3.forward })
                            {
                                var ray = new Ray((Vector3)middle - direction * .75f, direction);
                                Require(collider != null && collider.Raycast(ray, out _, 1f), "Missing wall collision at " + middle);
                            }
                            seams++;
                        }
                        report.Add($"Seed {seed}: {ramps.Length} ramps; {seams} closed wall joins; {holes.Count} holes.");
                        if (holes.Count > 0)
                            throw new InvalidOperationException("Missing wall row at " + string.Join(", ", holes.Take(12)));

                        foreach (var ramp in ramps)
                        {
                            var frame = ramp.transform.parent;
                            directions.Add(Mathf.RoundToInt(frame.eulerAngles.y / 90f) % 4);
                            var low = ramp.transform.TransformPoint(new Vector3(0, 0, -5));
                            var high = ramp.transform.TransformPoint(new Vector3(0, 0, 5));
                            var along = Vector3.ProjectOnPlane(high - low, Vector3.up).normalized;
                            foreach (var endpoint in new[] { low - along * .1f, high + along * .1f })
                            {
                                var ray = new Ray(endpoint + Vector3.up * .1f, Vector3.down);
                                Require(blocks.Values.Any(b => b.kind == BlockKind.Floor &&
                                    b.GetComponent<BoxCollider>().Raycast(ray, out _, .12f)), "Ramp landing missing at " + endpoint);
                            }
                            for (int step = 0; step <= 10; step++)
                            {
                                var feet = Vector3.Lerp(low, high, step / 10f);
                                // Lift slightly above the 45-degree surface to account
                                // for the capsule's rounded bottom contacting the slope.
                                var hits = Physics.OverlapCapsule(feet + Vector3.up * .55f,
                                    feet + Vector3.up * 1.65f, .35f, ~0, QueryTriggerInteraction.Ignore);
                                foreach (var hit in hits)
                                    Require(!hit.transform.IsChildOf(generator.GeneratedRoot) || hit.GetComponent<CubeBlock>() == null,
                                        "Ramp passage blocked at " + feet + " by " + hit.name);
                            }

                            var side = frame.right;
                            var neighborPosition = frame.position + side * 5f;
                            if (ramps.Any(other => other != ramp &&
                                (other.transform.parent.position - neighborPosition).sqrMagnitude < .001f &&
                                Vector3.Dot(other.transform.parent.forward, frame.forward) > .999f))
                            {
                                var ray = new Ray(frame.position + Vector3.up * 2f, side);
                                Require(!Physics.RaycastAll(ray, 5f, ~0, QueryTriggerInteraction.Ignore).Any(hit =>
                                    hit.transform.IsChildOf(generator.GeneratedRoot) && hit.collider.GetComponent<CubeBlock>() != null),
                                    "Open side between parallel ramps was filled at " + frame.position);
                                totalOpenSides++;
                            }
                        }
                        totalSeams += seams;
                    }
                    finally { Object.DestroyImmediate(generator.gameObject); }
                }
                Require(totalSeams > 0 && directions.Count == 4, "Checks must cover wall joins and all four ramp orientations");
                report.Add($"PASS: {totalSeams} wall joins with collision; four orientations; all landings and capsule passages clear; {totalOpenSides} parallel ramp openings preserved.");
            }
            catch (Exception error)
            {
                report.Add("FAIL: " + error);
                throw;
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/CubeDungeonWalls.txt", report);
                foreach (string line in report) Debug.Log(line);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
