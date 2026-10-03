using System;
using System.Collections.Generic;
using System.IO;
using CubeDungeon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NicromFPS.Editor
{
    public static class DungeonTraversalValidation
    {
        [MenuItem("Tools/Nicrom FPS/Dungeon-Rampen begehen prüfen")]
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
                var kit = AssetDatabase.LoadAssetAtPath<CubeDungeonKit>("Assets/CubeDungeon/Resources/CubeDungeonKit.asset");
                Require(kit && kit.ramp && kit.hall, "Dungeon ramp and hall assets are required.");
                foreach (int yaw in new[] { 0, 90, 180, 270 })
                {
                    var fixture = CreateFixture(kit, yaw, out var low, out var high);
                    try
                    {
                        foreach (int rate in new[] { 30, 60, 144 })
                            foreach (bool ascending in new[] { true, false })
                            {
                                Traverse(low, high, ascending, rate);
                                results.Add($"PASS: {(ascending ? "up" : "down")}, yaw {yaw}, {rate} steps/s; both landings crossed.");
                            }
                    }
                    finally { Object.DestroyImmediate(fixture); }
                }
                results.Add("PASS: 24 traversals with the FPS capsule, 50-degree slope limit, gravity and movement smoothing.");
                Debug.Log(results[results.Count - 1]);
            }
            catch (Exception ex) { results.Add("FAIL: " + ex); throw; }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/NicromDungeonTraversal.txt", results);
            }
        }

        private static GameObject CreateFixture(CubeDungeonKit kit, int yaw, out Vector3 low, out Vector3 high)
        {
            var fixture = new GameObject("Dungeon traversal fixture");
            fixture.transform.SetPositionAndRotation(new Vector3(2000, -1000, 2000), Quaternion.Euler(0, yaw, 0));
            var ramp = Object.Instantiate(kit.ramp, fixture.transform);
            ramp.transform.localPosition = Vector3.zero;
            ramp.transform.localRotation = Quaternion.identity;
            ramp.transform.localScale = Vector3.one * 5;
            var lower = Object.Instantiate(kit.hall, fixture.transform);
            lower.transform.localPosition = new Vector3(0, 0, -5);
            lower.transform.localRotation = Quaternion.identity;
            lower.transform.localScale = Vector3.one * 5;
            var upper = Object.Instantiate(kit.hall, fixture.transform);
            upper.transform.localPosition = new Vector3(0, 5, 5);
            upper.transform.localRotation = Quaternion.identity;
            upper.transform.localScale = Vector3.one * 5;
            var surface = ramp.GetComponentInChildren<MeshCollider>();
            Require(surface, "Ramp needs its real mesh collider.");
            low = surface.transform.TransformPoint(new Vector3(0, 0, -5));
            high = surface.transform.TransformPoint(new Vector3(0, 0, 5));
            Physics.SyncTransforms();
            Require(Mathf.Abs(Vector3.Angle(high - low, Vector3.up) - 45) < .01f,
                "Fixture must exercise the existing 45-degree ramp.");
            return fixture;
        }

        private static void Traverse(Vector3 low, Vector3 high, bool ascending, int rate)
        {
            var player = new GameObject("FPS traversal capsule");
            try
            {
                var body = player.AddComponent<CharacterController>();
                body.height = 1.8f;
                body.radius = .3f;
                body.center = Vector3.up * .9f;
                body.skinWidth = .03f;
                body.slopeLimit = 50;
                body.stepOffset = .3f;
                body.minMoveDistance = 0;
                body.enableOverlapRecovery = true;
                var uphill = Vector3.ProjectOnPlane(high - low, Vector3.up).normalized;
                var direction = ascending ? uphill : -uphill;
                var start = ascending ? low - uphill * 1.5f : high + uphill * 1.5f;
                var goal = ascending ? high + uphill * 1.5f : low - uphill * 1.5f;
                player.transform.position = start + Vector3.up * .06f;
                Physics.SyncTransforms();

                // Same dimensions and movement settings as Nicrom FPS Player. The
                // editor test drives the native CharacterController.Move collision
                // path directly, so it also works in the isolated validation project
                // without loading the weapon, input and HUD dependencies.
                float dt = 1f / rate;
                float gravity = 0;
                var velocity = Vector3.zero;
                var desired = Vector3.zero;
                var smoothRef = Vector3.zero;
                var target = Vector3.zero;
                float speed = 5;
                float bestProgress = 0;
                int lastProgressFrame = 0;
                float distance = Vector3.Dot(goal - start, direction);
                for (int frame = 0; frame < rate * 12; frame++)
                {
                    Vector3 feet = player.transform.position;
                    float slope = Physics.Raycast(feet, Vector3.down, out var ground)
                        ? Vector3.Angle(Vector3.up, ground.normal) : 0;
                    Vector3 forward = direction;
                    if (Physics.Raycast(feet + body.center, Vector3.down, out var surface, body.height * .5f + .1f)
                        && slope < body.slopeLimit)
                        forward = Vector3.ProjectOnPlane(direction, surface.normal);
                    var raw = forward.normalized * speed;
                    if (body.isGrounded) target = raw;
                    desired = Vector3.SmoothDamp(desired, body.isGrounded ? target : velocity * .9f,
                        ref smoothRef, .13f, Mathf.Infinity, dt);
                    // Dungeon mode keeps small step recovery on walkable slopes;
                    // otherwise tiny collider joins can trap the capsule at the top.
                    body.stepOffset = !body.isGrounded || slope >= body.slopeLimit ? 0 : .3f;
                    speed = slope < body.slopeLimit ? 5 : 0;
                    velocity.x = desired.x;
                    velocity.z = desired.z;
                    gravity = body.isGrounded ? Physics.gravity.y * .5f : gravity + Physics.gravity.y * 2 * dt;
                    velocity.y = gravity;
                    body.Move(velocity * dt);
                    Physics.SyncTransforms();

                    float progress = Vector3.Dot(player.transform.position - start, direction);
                    Require(player.transform.position.y >= low.y - .15f, "Capsule fell through the ramp or landing.");
                    if (progress > bestProgress + .02f) { bestProgress = progress; lastProgressFrame = frame; }
                    Require(frame - lastProgressFrame < rate * 2,
                        $"Capsule stuck {(ascending ? "ascending" : "descending")} at {rate} steps/s, progress {progress:F2}/{distance:F2} m.");
                    if (progress >= distance)
                    {
                        Require(Mathf.Abs(player.transform.position.y - goal.y) < .18f,
                            "Capsule crossed horizontally without arriving on the destination landing.");
                        return;
                    }
                }
                throw new InvalidOperationException($"Traversal timed out {(ascending ? "ascending" : "descending")} at {rate} steps/s.");
            }
            finally { Object.DestroyImmediate(player); }
        }

        private static void Require(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
        }
    }
}
