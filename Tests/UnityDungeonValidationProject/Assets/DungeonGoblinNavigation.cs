using System.Collections.Generic;
using CubeDungeon;
using UnityEngine;
using UnityEngine.AI;
namespace NicromFPS
{
    public sealed class DungeonGoblinNavigation : System.IDisposable
    {
        readonly List<Vector3> candidates = new List<Vector3>();
        readonly NavMeshPath path = new NavMeshPath();
        NavMeshData data;
        NavMeshDataInstance instance;
        int agentType = -1;
        Vector3 gate;
        bool prepared;
        public IReadOnlyList<Vector3> Candidates => candidates;
        public int AgentType => agentType;
        public NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        public static bool Clear(Vector3 p) => !Physics.CheckCapsule(p + Vector3.up * .37f,
            p + Vector3.up * 1.13f, .32f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        public bool Build(CubeDungeonGenerator dungeon, Vector3 arrival, Vector3 returnGate)
        {
            if (prepared) return true;
            Dispose();
            candidates.Clear();
            gate = returnGate;
            var settings = NavMesh.CreateSettings();
            agentType = settings.agentTypeID;
            settings.agentRadius = .32f; settings.agentHeight = 1.4f;
            settings.agentClimb = .3f; settings.agentSlope = 50;
            settings.overrideVoxelSize = true; settings.voxelSize = .1f;
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(dungeon.transform, Physics.DefaultRaycastLayers,
                NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var bounds = new Bounds(arrival, Vector3.one);
            foreach (var collider in dungeon.GetComponentsInChildren<Collider>()) bounds.Encapsulate(collider.bounds);
            bounds.Expand(4);
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (!data) { Dispose(); return false; }
            instance = NavMesh.AddNavMeshData(data);
            if (!NavMesh.SamplePosition(arrival, out var entry, 1, Filter)) { Dispose(); return false; }
            var layout = dungeon.GetComponentInChildren<DungeonGenerator>();
            if (layout) foreach (var room in layout.GetRooms()) foreach (var cell in room.cells)
            {
                // Room cell floor tops are 2.5 metres below the cell centre.
                Vector3 floor = dungeon.transform.TransformPoint(cell.center + Vector3.down * 2.5f);
                for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                {
                    Vector3 proposed = floor + dungeon.transform.TransformVector(new Vector3(x * 1.5f, 0, z * 1.5f));
                    if (!NavMesh.SamplePosition(proposed, out var hit, .45f, Filter)
                        || Mathf.Abs(hit.position.y - proposed.y) > .2f || !Clear(hit.position)
                        || Vector3.Distance(hit.position, gate) < 4) continue;
                    if (!NavMesh.CalculatePath(hit.position, entry.position, Filter, path)
                        || path.status != NavMeshPathStatus.PathComplete) continue;
                    if (!candidates.Exists(p => (p - hit.position).sqrMagnitude < 1)) candidates.Add(hit.position);
                }
            }
            prepared = candidates.Exists(p => Vector3.Distance(p, arrival) >= 8);
            if (!prepared) Dispose();
            return prepared;
        }

        public void Dispose()
        {
            prepared = false;
            if (instance.valid) instance.Remove();
            if (data) { if (Application.isPlaying) Object.Destroy(data); else Object.DestroyImmediate(data); }
            data = null;
            if (agentType != -1) NavMesh.RemoveSettings(agentType);
            agentType = -1;
        }
    }
}
