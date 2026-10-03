using System;
using System.Collections.Generic;
using System.IO;
using CubeDungeon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
namespace NicromFPS.Editor
{
    public static class DungeonGoblinValidation
    {
        [MenuItem("Tools/Nicrom FPS/Dungeon-Goblin-Navigation prüfen")]
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
                foreach (int seed in new[] {42,7,1337})
                {
                    var dungeon = new GameObject("Goblin navigation validation").AddComponent<CubeDungeonGenerator>();
                    dungeon.generateOnStart = false; dungeon.showCeilings = true;
                    dungeon.transform.position = new Vector3(0,-1000,0);
                    dungeon.gridSize = new Vector3Int(20,4,20); dungeon.roomCount = 8; dungeon.seed = seed;
                    dungeon.Generate(); Physics.SyncTransforms();
                    Require(dungeon.TryGetPlayerSpawnPosition(out var arrival), "Entry exists");
                    var player = new GameObject("Capsule").AddComponent<CharacterController>();
                    player.height = 1.8f; player.radius = .3f; player.center = Vector3.up*.9f;
                    player.transform.position = new Vector3(500, -1000, 500);
                    Require(DungeonPlacement.TryFind(arrival, player, false, scene, out arrival), "Safe player arrival");
                    Require(DungeonPlacement.TryFindGate(arrival, player, scene, out var gate, out _), "Gate exists");
                    UnityEngine.Object.DestroyImmediate(player.gameObject);
                    Physics.SyncTransforms();
                    using (var nav = new DungeonGoblinNavigation())
                    {
                        Require(nav.Build(dungeon,arrival,gate), "Build navigation: " + seed);
                        var path = new NavMeshPath();
                        Require(NavMesh.SamplePosition(arrival,out var entry,1,nav.Filter),"Entry on mesh");
                        foreach (var point in nav.Candidates)
                        {
                            Require(DungeonGoblinNavigation.Clear(point),"Spawn capsule free");
                            Require(Vector3.Distance(point,gate)>=4,"Gate clearance");
                            Require(NavMesh.CalculatePath(point,entry.position,nav.Filter,path)
                                && path.status == NavMeshPathStatus.PathComplete,"Return path exists");
                            Require(NavMesh.CalculatePath(entry.position,point,nav.Filter,path)
                                && path.status == NavMeshPathStatus.PathComplete,"Outbound path exists");
                        }
                        var layout = dungeon.GetComponentInChildren<DungeonGenerator>();
                        int connected = 0;
                        foreach (var room in layout.GetRooms())
                        {
                            bool found = false;
                            foreach (var cell in room.cells)
                            {
                                Vector3 floor = dungeon.transform.TransformPoint(cell.center + Vector3.down*2.5f);
                                foreach (var point in nav.Candidates) if (Vector3.Distance(point,floor)<3) { found=true; break; }
                                if(found)break;
                            }
                            if(found)connected++;
                        }
                        Require(connected == layout.GetRooms().Count, "Every room reachable over ramps: " + connected + "/" + layout.GetRooms().Count);
                        // Physical occupancy must invalidate a previously safe spawn immediately.
                        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        blocker.transform.position = nav.Candidates[0] + Vector3.up*.7f;
                        Physics.SyncTransforms();
                        Require(!DungeonGoblinNavigation.Clear(nav.Candidates[0]), "Built obstacle blocks spawn");
                        UnityEngine.Object.DestroyImmediate(blocker);
                        results.Add($"PASS seed {seed}: {nav.Candidates.Count} free spawns, {connected} connected rooms, bidirectional paths and blocked spawn rejection.");
                    }
                    UnityEngine.Object.DestroyImmediate(dungeon.gameObject);
                }
            }
            catch(Exception ex) { results.Add("FAIL: " + ex); throw; }
            finally
            {
                if(original.IsValid())SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene,true);
                Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/NicromDungeonGoblins.txt",results);
            }
        }
        static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
    }
}
