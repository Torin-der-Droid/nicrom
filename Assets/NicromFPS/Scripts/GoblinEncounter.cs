using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using Akila.FPSFramework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace NicromFPS
{
    public sealed class GoblinEncounter : MonoBehaviour
    {
        [SerializeField] GameObject modelPrefab = null;
        [SerializeField] RuntimeAnimatorController animationController = null;
        [SerializeField] Material goblinMaterial = null;
        [SerializeField] AudioClip goblinCackle = null;
        [SerializeField, Min(5)] float spawnRadiusMin = 15;
        [SerializeField, Min(6)] float spawnRadiusMax = 30;
        [SerializeField, Min(0)] float waveDelay = 5;
        [SerializeField, Min(1)] float health = 100;
        [SerializeField, Min(1)] float attackDamage = 12;
        [SerializeField, Min(1)] float detectionRange = 30;
        [SerializeField, Min(1)] float moveSpeed = 4f;
        private NavMeshData navigationData;
        private NavMeshDataInstance navigationInstance;

        private readonly WaveProgress progress = new WaveProgress();
        private readonly List<GoblinEnemy> enemies = new List<GoblinEnemy>();
        private readonly List<int> enemyIds = new List<int>();
        private NavMeshSurface surface;
        private Transform player;
        private IDamageable playerHealth;
        private Vector3 navigationCenter;
        private TMP_Text waveText;
        private string status = "Wegfindung wird vorbereitet";
        private float countdown;
        private Minimap configuredMap;

        private bool Ready => player && playerHealth != null && playerHealth.Health > 0
            && FPSFrameworkCore.IsActive && !FPSFrameworkCore.IsPaused && !DungeonTravelManager.OutsidePaused;

        private IEnumerator Start()
        {
            CreateHUD();
            if (!modelPrefab || !animationController || !goblinMaterial)
            {
                status = "Goblin-Assets fehlen";
                Debug.LogError("Goblin encounter: Model, animations or material missing.", this);
                yield break;
            }
            spawnRadiusMin = Mathf.Max(5, spawnRadiusMin);
            spawnRadiusMax = Mathf.Max(spawnRadiusMin + 1, spawnRadiusMax);
            var navigation = new GameObject("Wellen Navigation");
            navigation.transform.SetParent(transform, false);
            navigation.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.layerMask = Physics.DefaultRaycastLayers & ~(1 << 4) & ~(1 << 8) & ~(1 << 12) & ~(1 << 5);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.15f;
            surface.buildHeightMesh = true;
            navigationData = new NavMeshData(surface.agentTypeID);
            var path = new NavMeshPath();
            while (true)
            {
                while (!Ready) yield return null;
                yield return RefreshNavigation();
                while (!Ready) yield return null;
                progress.Next();
                status = "";
                while (!progress.Complete)
                {
                    if (!Ready) { yield return null; continue; }
                    if (Vector3.Distance(player.position, navigationCenter) > 25)
                        yield return RefreshNavigation();
                    int added = 0;
                    for (int attempt = 0; attempt < 24 && progress.Pending > 0 && Ready; attempt++)
                    {
                        Vector3 origin = player.position;
                        if (!NavMesh.SamplePosition(origin, out NavMeshHit playerGround, 4, NavMesh.AllAreas)) break;
                        float angle = Random.value * Mathf.PI * 2;
                        float radius = Mathf.Sqrt(Mathf.Lerp(spawnRadiusMin * spawnRadiusMin,
                            spawnRadiusMax * spawnRadiusMax, Random.value));
                        Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                        if (!TryTerrain(candidate, out Vector3 ground)) continue;
                        if (!NavMesh.SamplePosition(ground, out NavMeshHit spot, 2, NavMesh.AllAreas)) continue;
                        float distance = Vector3.ProjectOnPlane(spot.position - origin, Vector3.up).magnitude;
                        if (distance < spawnRadiusMin || distance > spawnRadiusMax) continue;
                        if (!NavMesh.CalculatePath(spot.position, playerGround.position, NavMesh.AllAreas, path)
                            || path.status != NavMeshPathStatus.PathComplete) continue;
                        if (Physics.CheckCapsule(spot.position + Vector3.up * 0.37f,
                            spot.position + Vector3.up * 1.2f, 0.32f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                        Spawn(spot.position, progress.Spawned + 1);
                        Physics.SyncTransforms();
                        if (++added >= 2) break;
                    }
                    status = progress.Pending > 0 ? $"Verstärkung: {progress.Pending} ausstehend" : "Alle Goblins besiegen";
                    // Retry blocked spawn locations instead of counting missing enemies as defeated.
                    if (progress.Pending > 0 && added == 0) yield return new WaitForSeconds(0.25f);
                    else yield return null;
                }
                countdown = waveDelay;
                while (countdown > 0)
                {
                    status = $"Nächste Welle in {Mathf.CeilToInt(countdown)} s";
                    if (Ready) countdown -= Time.deltaTime;
                    yield return null;
                }
            }
        }

        private IEnumerator RefreshNavigation()
        {
            status = "Wegfindung wird vorbereitet";
            navigationCenter = player.position;
            var bounds = new Bounds(navigationCenter, new Vector3(2 * (spawnRadiusMax + 30), 80, 2 * (spawnRadiusMax + 30)));
            foreach (var enemy in enemies)
                if (enemy && !enemy.IsDead) bounds.Encapsulate(new Bounds(enemy.transform.position, Vector3.one * 12));
            surface.center = bounds.center;
            surface.size = bounds.size;
            Physics.SyncTransforms();
            yield return surface.UpdateNavMesh(navigationData);
            if (!navigationInstance.valid) navigationInstance = NavMesh.AddNavMeshData(navigationData);
        }

        private float nextPlayerSearch;
        private void Update()
        {
            if ((!player || playerHealth == null || playerHealth.Health <= 0) && Time.time >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.time + 0.5f;
                player = null;
                playerHealth = null;
                foreach (var candidate in FindObjectsByType<NicromTerrainSpawn>())
                {
                    if (candidate.gameObject.scene != gameObject.scene) continue;
                    var healthComponent = candidate.GetComponent<IDamageable>();
                    if (healthComponent == null || healthComponent.Health <= 0) continue;
                    player = candidate.transform;
                    playerHealth = healthComponent;
                    break;
                }
            }
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i] && !enemies[i].IsDead) continue;
                progress.Defeat(enemyIds[i]);
                enemies.RemoveAt(i);
                enemyIds.RemoveAt(i);
            }
            if (Minimap.Instance && configuredMap != Minimap.Instance)
            {
                configuredMap = Minimap.Instance;
                configuredMap.zoom = 1;
            }
            if (waveText && DungeonTravelManager.OutsidePaused)
                waveText.text = "Wellen pausiert – Dungeon";
            else if (waveText)
                waveText.text = progress.Wave == 0 ? status : $"WELLE {progress.Wave}  •  {progress.Target} GOBLINS\n"
                    + $"Noch {progress.Alive + progress.Pending}  |  {status}";
        }

        private void CreateHUD()
        {
            var root = new GameObject("Wellenanzeige", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var panel = new GameObject("Welle", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -24);
            rect.sizeDelta = new Vector2(560, 82);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 0.8f);
            panel.GetComponent<Image>().raycastTarget = false;
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(panel.transform, false);
            var textRect = label.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 8);
            textRect.offsetMax = new Vector2(-12, -8);
            waveText = label.GetComponent<TextMeshProUGUI>();
            waveText.fontSize = 23;
            waveText.alignment = TextAlignmentOptions.Center;
            waveText.raycastTarget = false;
        }

        private bool TryTerrain(Vector3 candidate, out Vector3 ground)
        {
            ground = default;
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain.gameObject.scene != gameObject.scene) continue;
                Vector3 local = candidate - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x < 1 || local.z < 1 || local.x > size.x - 1 || local.z > size.z - 1) continue;
                var collider = terrain.GetComponent<TerrainCollider>();
                Ray ray = new Ray(new Vector3(candidate.x, terrain.transform.position.y + size.y + 10, candidate.z), Vector3.down);
                if (!collider || !collider.Raycast(ray, out RaycastHit hit, size.y + 20) || Vector3.Angle(hit.normal, Vector3.up) > 30) continue;
                if (Physics.Raycast(ray, out RaycastHit water, size.y + 20, 1 << 4, QueryTriggerInteraction.Collide)
                    && water.point.y > hit.point.y) continue;
                ground = hit.point;
                return true;
            }
            return false;
        }

        public bool AssetsReady => modelPrefab && animationController && goblinMaterial;
        public GoblinEnemy CreateEnemy(Transform parent, Vector3 position, int number, float life,
            Transform target, bool dungeon, int agentType, float damageMultiplier = 1) => GoblinFactory.Spawn(parent, position, number,
                modelPrefab, animationController, goblinMaterial, goblinCackle, life, attackDamage * Mathf.Max(1, damageMultiplier),
                detectionRange, moveSpeed, target, dungeon, agentType);

        private void Spawn(Vector3 position, int number)
        {
            var enemy = CreateEnemy(transform, position, number, health, player, false, surface.agentTypeID);
            int id = number;
            progress.Register(id);
            enemies.Add(enemy);
            enemyIds.Add(id);
        }

        private void OnDestroy()
        {
            if (navigationData) NavMeshBuilder.Cancel(navigationData);
            if (navigationInstance.valid) navigationInstance.Remove();
            if (navigationData) Destroy(navigationData);
        }
    }
}
