#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CubeDungeon
{
    // Runs only with the explicit smoke-test command-line flag, in automated
    // Play Mode checks and development player builds. No effect in normal play.
    public sealed class CubeDungeonRuntimeCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Environment.GetCommandLineArgs().Contains("--cube-dungeon-smoke-test"))
                new GameObject("Cube Dungeon Runtime Check").AddComponent<CubeDungeonRuntimeCheck>();
        }

        IEnumerator Start()
        {
            yield return null;
            yield return null;
            var generator = FindAnyObjectByType<CubeDungeonGenerator>();
            string failure = null;
            int initialCount = 0;
            try
            {
                Require(generator != null && generator.GeneratedRoot != null, "Start generated a dungeon");
                Require(generator.BlockCount > 100 && generator.RampCount > 0, "Cubes and ramps exist in play");
                Require(generator.FailedConnections == 0, "All connections found in play");
                initialCount = generator.BlockCount;
                generator.SetCeilingsVisible(false);
                Require(generator.GeneratedRoot.GetComponentsInChildren<CubeBlock>().Where(b => b.kind == BlockKind.Ceiling)
                    .All(b => !b.GetComponent<Renderer>().enabled && b.GetComponent<Collider>().enabled), "Ceiling toggle in play");
                generator.Generate();
                Require(generator.BlockCount == initialCount, "Runtime regeneration deterministic");
            }
            catch (Exception error) { failure = error.ToString(); }
            yield return null; // Unity's deferred Destroy must have completed.
            try
            {
                if (failure == null)
                {
                    Require(generator.transform.Cast<Transform>().Count(t => t.name == "Cube Dungeon") == 1, "Old runtime output destroyed");
                    Require(generator.GeneratedRoot.GetComponentsInChildren<CubeBlock>(true).Length == initialCount, "Duplicate cubes destroyed in play");
                    generator.Clear();
                }
            }
            catch (Exception error) { failure = error.ToString(); }
            yield return null;
            try
            {
                if (failure == null)
                {
                    Require(generator.GeneratedRoot == null, "Runtime clear");
                    generator.Generate();
                    Require(generator.BlockCount == initialCount, "Generate after runtime clear");
                }
            }
            catch (Exception error) { failure = error.ToString(); }

            string report = failure == null ? "PASS: Start, regeneration, deterministic block count, ceiling toggle, deferred destruction, clear and regenerate." : "FAIL: " + failure;
            var args = Environment.GetCommandLineArgs();
            int reportIndex = Array.IndexOf(args, "--cube-report");
            string path = reportIndex >= 0 && reportIndex + 1 < args.Length ? args[reportIndex + 1] : "Logs/CubeDungeonRuntime.txt";
            File.WriteAllText(path, report);
            Debug.Log(report);
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(failure == null ? 0 : 1);
#else
            Application.Quit(failure == null ? 0 : 1);
#endif
        }

        static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
#endif
