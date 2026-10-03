using System.Collections.Generic;
using Akila.FPSFramework;
using CubeDungeon;
using UnityEngine;
using UnityEngine.AI;

namespace NicromFPS
{
    public sealed class DungeonGoblinEncounter : MonoBehaviour
    {
        sealed class Resident
        {
            public GoblinEnemy Enemy;
            public int Id;
            public Vector3 LastPosition;
            public float Stalled;
            public float RecoveryWait;
            public float LastHealth;
            public bool Defeated;
        }
        readonly DungeonWaveProgress progress = new DungeonWaveProgress();
        DungeonGoblinNavigation navigation;
        IReadOnlyList<Vector3> candidates => navigation.Candidates;
        readonly List<Resident> residents = new List<Resident>();
        readonly List<GoblinEnemy> allEnemies = new List<GoblinEnemy>();
        NavMeshPath path;
        readonly Plane[] viewPlanes = new Plane[6];
        int nextId, lastPoint = -1;
        Transform player;
        Camera view;
        GoblinEncounter assets;
        Vector3 gate;
        bool prepared;
        int dungeonLevel = 1;
        float healthMultiplier = 1;
        float damageMultiplier = 1;
        public event System.Action<int> WaveCompleted;
        NavMeshQueryFilter Filter => navigation.Filter;
        public string Status => !prepared ? $"Dungeon {dungeonLevel} – Wegfindung wird vorbereitet"
            : progress.Intermission ? $"Dungeon {dungeonLevel} – Welle {progress.Wave + 1}\nStart in {Mathf.CeilToInt(progress.Countdown)} s"
            : $"Dungeon {dungeonLevel} – Welle {progress.Wave}\nNoch {progress.Alive + progress.Pending} Goblins";

        void Awake() => InitializeNavigation();

        void InitializeNavigation()
        {
            // Native Unity objects must be created on the main thread, after construction.
            if (navigation == null) navigation = new DungeonGoblinNavigation();
            if (path == null) path = new NavMeshPath();
        }

        public bool Prepare(CubeDungeonGenerator dungeon, Vector3 arrival, Vector3 returnGate, GoblinEncounter source,
            int level = 1, int countMultiplier = 1, float lifeMultiplier = 1, float attackMultiplier = 1)
        {
            InitializeNavigation();
            if (!source || !source.AssetsReady) return false;
            assets = source; gate = returnGate; dungeonLevel = Mathf.Max(1, level);
            progress.CountMultiplier = Mathf.Max(1, countMultiplier);
            healthMultiplier = Mathf.Max(1, lifeMultiplier);
            damageMultiplier = Mathf.Max(1, attackMultiplier);
            prepared = navigation.Build(dungeon, arrival, returnGate);
            return prepared;
        }

        public void SetPlayer(Transform target)
        {
            player = target; view = null;
            if (target) foreach (var camera in target.GetComponentsInChildren<Camera>())
                if (camera.CompareTag("MainCamera")) view = camera;
            foreach (var resident in residents) if (resident.Enemy) resident.Enemy.SetTarget(target);
        }

        bool Active => prepared && player && player.GetComponent<IDamageable>()?.Health > 0
            && DungeonTravelManager.Instance && DungeonTravelManager.Instance.IsDungeonActive(dungeonLevel)
            && !DungeonTravelManager.Instance.IsTransitioning && FPSFrameworkCore.IsActive && !FPSFrameworkCore.IsPaused;

        void Update()
        {
            bool active = Active;
            progress.Tick(Time.deltaTime, active);
            if (!active) return;
            if (view) GeometryUtility.CalculateFrustumPlanes(view, viewPlanes);
            for (int i = residents.Count - 1; i >= 0; i--)
            {
                var resident = residents[i];
                resident.RecoveryWait = Mathf.Max(0, resident.RecoveryWait - Time.deltaTime);
                if (resident.Defeated || (resident.Enemy && resident.Enemy.IsDead))
                {
                    bool wasIntermission = progress.Intermission;
                    int completedWave = progress.Wave;
                    progress.Defeat(resident.Id); residents.RemoveAt(i);
                    if (!wasIntermission && progress.Intermission) WaveCompleted?.Invoke(completedWave);
                    continue;
                }
                // Unexpected destruction is replaced, never credited as a kill.
                if (!resident.Enemy)
                {
                    if (resident.RecoveryWait > 0) continue;
                    resident.RecoveryWait = .5f;
                    if (TrySpawnPoint(out var replacement))
                    {
                        resident.Enemy = Spawn(replacement, resident.Id, DungeonWaveProgress.HealthFor(progress.Wave) * healthMultiplier);
                        resident.Enemy.Health = resident.LastHealth;
                        TrackDeath(resident);
                        resident.LastPosition = replacement; resident.Stalled = 0;
                    }
                    continue;
                }
                resident.LastHealth = resident.Enemy.Health;
                Vector3 position = resident.Enemy.transform.position;
                if (Vector3.Distance(position, resident.LastPosition) > .5f || Vector3.Distance(position, player.position) < 2)
                { resident.LastPosition = position; resident.Stalled = 0; }
                else resident.Stalled += Time.deltaTime;
                if (resident.Stalled >= 30 && resident.RecoveryWait <= 0 && !Visible(position))
                {
                    resident.RecoveryWait = .5f;
                    if (!TrySpawnPoint(out var recovery)) continue;
                    var agent = resident.Enemy.GetComponent<NavMeshAgent>();
                    if (agent.Warp(recovery)) { resident.LastPosition = recovery; resident.Stalled = 0; }
                }
            }
            if (!progress.CanSpawn) return;
            if (!TrySpawnPoint(out var spot)) { progress.Retry(); return; }
            int id = ++nextId;
            var enemy = Spawn(spot, id, DungeonWaveProgress.HealthFor(progress.Wave) * healthMultiplier);
            if (!progress.Register(id)) { Destroy(enemy.gameObject); return; }
            var added = new Resident { Enemy = enemy, Id = id, LastPosition = spot, LastHealth = enemy.Health };
            TrackDeath(added);
            residents.Add(added);
        }

        void TrackDeath(Resident resident)
        {
            // Remember kills even if the player exits before the next encounter Update.
            resident.Enemy.Defeated += () => resident.Defeated = true;
        }
        GoblinEnemy Spawn(Vector3 position, int id, float health)
        {
            var enemy = assets.CreateEnemy(transform, position, id, health, player, true, navigation.AgentType, damageMultiplier);
            enemy.DungeonLevel = dungeonLevel;
            allEnemies.RemoveAll(e => !e);
            allEnemies.Add(enemy);
            Physics.SyncTransforms();
            return enemy;
        }

        bool TrySpawnPoint(out Vector3 result)
        {
            result = default;
            if (!player || !view || !NavMesh.SamplePosition(player.position, out var destination, 1.5f, Filter)) return false;
            int start = Random.Range(0, candidates.Count), fallback = -1;
            for (int n = 0; n < candidates.Count; n++)
            {
                int i = (start + n) % candidates.Count;
                Vector3 p = candidates[i];
                if (i == lastPoint || Vector3.Distance(p, player.position) < 8 || Vector3.Distance(p, gate) < 4
                    || Visible(p) || !Clear(p)) continue;
                if (!NavMesh.SamplePosition(p, out var sample, .2f, Filter)
                    || !NavMesh.CalculatePath(sample.position, destination.position, Filter, path)
                    || path.status != NavMeshPathStatus.PathComplete) continue;
                float distance = 0;
                var corners = path.corners;
                for (int c = 1; c < corners.Length; c++) distance += Vector3.Distance(corners[c-1], corners[c]);
                if (distance <= 30) { result = sample.position; lastPoint = i; return true; }
                if (fallback < 0) fallback = i;
            }
            if (fallback >= 0) { result = candidates[fallback]; lastPoint = fallback; return true; }
            // Permit reuse when this is the only safe point on the next retry.
            lastPoint = -1;
            return false;
        }

        static bool Clear(Vector3 p) => DungeonGoblinNavigation.Clear(p);

        bool Visible(Vector3 p)
        {
            if (!view) return true;
            var bounds = new Bounds(p + Vector3.up * .8f, new Vector3(1, 1.6f, 1));
            if (!GeometryUtility.TestPlanesAABB(viewPlanes, bounds)) return false;
            // Require all samples to be occluded, so a visible head does not pop into view.
            for (int sample = 0; sample < 4; sample++)
            {
                Vector3 offset = sample == 0 ? Vector3.up * .4f : sample == 1 ? Vector3.up * 1.4f
                    : Vector3.up * .9f + view.transform.right * (sample == 2 ? .4f : -.4f);
                Vector3 origin = view.transform.position, delta = p + offset - origin;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                    Physics.DefaultRaycastLayers & ~(1 << 8) & ~(1 << 12), QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(player) || hit.collider.GetComponentInParent<GoblinEnemy>()) continue;
                    blocked = true; break;
                }
                if (!blocked) return true;
            }
            return false;
        }

        public void ResetAfterDeath()
        {
            foreach (var enemy in allEnemies) if (enemy) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            residents.Clear(); allEnemies.Clear(); progress.Reset(); nextId = 0; lastPoint = -1;
            player = null; view = null;
        }
        void OnDestroy() => navigation?.Dispose();
    }
}
