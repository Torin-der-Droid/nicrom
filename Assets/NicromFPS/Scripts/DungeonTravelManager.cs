using System;
using System.Collections;
using Akila.FPSFramework;
using CubeDungeon;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NicromFPS
{
    [DefaultExecutionOrder(-500)]
    public sealed class DungeonTravelManager : MonoBehaviour
    {
        public static DungeonTravelManager Instance { get; private set; }
        public static bool OutsidePaused => Instance && (Instance.InDungeon || Instance.IsTransitioning);
        public bool InDungeon { get; private set; }
        public bool IsTransitioning { get; private set; }
        [SerializeField] string dungeonScene = "Nicrom Dungeon";
        [SerializeField] Vector3 dungeonOrigin = new Vector3(0, -1000, 0);
        private Scene outsideScene, interiorScene;
        public string DungeonStatus => CurrentEncounter ? CurrentEncounter.Status : "Dungeon wird vorbereitet";
        private DungeonGate entrance, advanceGate;
        private readonly CubeDungeonGenerator[] generators = new CubeDungeonGenerator[2];
        private readonly DungeonGoblinEncounter[] encounters = new DungeonGoblinEncounter[2];
        private readonly DungeonGate[] exits = new DungeonGate[2];
        private readonly Vector3[] entryPoints = new Vector3[2];
        private int currentDungeonLevel;
        private int deepestUnlocked = 1;
        private DungeonGoblinEncounter CurrentEncounter => currentDungeonLevel > 0 ? encounters[currentDungeonLevel - 1] : null;
        private GameObject traveler;
        private Vector3 returnPoint;
        private TMP_Text message;
        private Canvas overlay;
        private float hideMessageAt;
        private bool previousInput;
        private FirstPersonController lockedController;
        private bool controllerWasEnabled;
        private Material gateMaterial;
        private GameObject gateVisualPrefab;
        private Actor travelerActor;
        private CharacterController dungeonPlayerBody;
        private float outsideSlopeLimit;
        private FirstPersonController dungeonPlayerMovement;
        private bool outsideSlopeSteps;

        public bool IsDungeonActive(int level) => InDungeon && currentDungeonLevel == level;

        private void Awake()
        {
            Instance = this;
            outsideScene = gameObject.scene;
            CreateOverlay();
        }

        private IEnumerator Start()
        {
            while (!entrance)
            {
                var spawn = FindOutsidePlayer();
                if (spawn && TryPlaceEntrance(spawn.gameObject)) yield break;
                yield return new WaitForSeconds(1);
            }
        }

        private NicromTerrainSpawn FindOutsidePlayer()
        {
            foreach (var player in FindObjectsByType<NicromTerrainSpawn>())
                if (player.gameObject.scene == outsideScene && player.GetComponent<IDamageable>()?.Health > 0) return player;
            return null;
        }

        private bool TryPlaceEntrance(GameObject player)
        {
            var body = player.GetComponent<CharacterController>();
            for (int step = 0; step < 48; step++)
            {
                float angle = step * 137.5f;
                Vector3 desired = player.transform.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * (8 + step / 8);
                if (!TrySafeGround(desired, body, true, out Vector3 ground)) continue;
                bool blocked = false;
                foreach (var obstacle in Physics.OverlapBox(ground + Vector3.up * 1.65f, new Vector3(1.6f, 1.6f, 1.6f),
                    Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (!(obstacle is TerrainCollider)) { blocked = true; break; }
                // The foot position was already checked; slight terrain slopes may intersect the frame's base.
                if (blocked) continue;
                Vector3 forward = Vector3.ProjectOnPlane(ground - player.transform.position, Vector3.up).normalized;
                if (!TrySafeGround(ground - forward * 2.5f, body, true, out returnPoint)) continue;
                entrance = CreateGate("Dungeon betreten", ground, Quaternion.LookRotation(forward), false, outsideScene);
                Show("Dungeon-Gate in deiner Nähe: durch das blaue Tor gehen", 8);
                return true;
            }
            return false;
        }

        public void Travel(GameObject player, bool returning)
        {
            if (IsTransitioning || returning != InDungeon || !player || FPSFrameworkCore.IsPaused) return;
            var health = player.GetComponent<IDamageable>();
            if (health == null || health.Health <= 0) return;
            StartCoroutine(Transition(player, returning, returning ? 0 : deepestUnlocked));
        }

        public void AdvanceDungeon(GameObject player)
        {
            if (IsTransitioning || currentDungeonLevel != 1 || !advanceGate || !player || FPSFrameworkCore.IsPaused) return;
            var health = player.GetComponent<IDamageable>();
            if (health == null || health.Health <= 0) return;
            StartCoroutine(Transition(player, false, 2));
        }

        private IEnumerator Transition(GameObject player, bool returning, int destinationLevel)
        {
            IsTransitioning = true;
            traveler = player;
            if (travelerActor) travelerActor.OnDeathConfirmed.RemoveListener(OnTravelerDeath);
            travelerActor = player.GetComponent<Actor>();
            if (travelerActor) travelerActor.OnDeathConfirmed.AddListener(OnTravelerDeath);
            previousInput = FPSFrameworkCore.IsInputActive;
            FPSFrameworkCore.IsInputActive = false;
            lockedController = player.GetComponent<FirstPersonController>();
            controllerWasEnabled = lockedController && lockedController.enabled;
            if (lockedController) lockedController.enabled = false;
            Show(returning ? "Rückkehr zur Außenwelt …" : "Dungeon wird vorbereitet …", float.PositiveInfinity);
            // Allow UI and weapon input to settle before synchronous mesh generation.
            yield return null;
            yield return new WaitForEndOfFrame();
            try
            {
                if (!returning && !interiorScene.IsValid())
                {
                    if (!Application.CanStreamedLevelBeLoaded(dungeonScene))
                    {
                        Show("Dungeon-Szene fehlt in den Build Settings.", 6);
                        yield break;
                    }
                    var load = SceneManager.LoadSceneAsync(dungeonScene, LoadSceneMode.Additive);
                    if (load == null) { Show("Dungeon konnte nicht geladen werden.", 6); yield break; }
                    yield return load;
                    interiorScene = SceneManager.GetSceneByName(dungeonScene);
                }
                if (!player || player.GetComponent<IDamageable>()?.Health <= 0) yield break;
                var body = player.GetComponent<CharacterController>();
                int destinationIndex = destinationLevel - 1;
                if (!returning && !generators[destinationIndex] && !CreateDungeon(body, destinationLevel))
                {
                    Show("Dungeon konnte nicht sicher erzeugt werden. Bitte erneut durch das Gate gehen.", 7);
                    yield break;
                }
                if (!returning)
                {
                    CubeDungeonGenerator targetGenerator = generators[destinationIndex];
                    DungeonGoblinEncounter targetEncounter = encounters[destinationIndex];
                    if (!targetEncounter)
                    {
                        targetEncounter = targetGenerator.gameObject.AddComponent<DungeonGoblinEncounter>();
                        encounters[destinationIndex] = targetEncounter;
                        int capturedLevel = destinationLevel;
                        targetEncounter.WaveCompleted += wave => OnDungeonWaveCompleted(capturedLevel, wave);
                    }
                    GoblinEncounter source = null;
                    foreach (var encounter in FindObjectsByType<GoblinEncounter>())
                        if (encounter.gameObject.scene == outsideScene) { source = encounter; break; }
                    Physics.SyncTransforms();
                    int multiplier = destinationLevel == 2 ? 2 : 1;
                    if (!targetEncounter.Prepare(targetGenerator, entryPoints[destinationIndex],
                        exits[destinationIndex].transform.position, source, destinationLevel, multiplier, multiplier, multiplier))
                    {
                        Show("Dungeon-Wegfindung oder sichere Spawnpunkte fehlen. Zugang bitte erneut versuchen.", 7);
                        yield break;
                    }
                    targetEncounter.SetPlayer(player.transform);
                }
                Physics.SyncTransforms();
                Vector3 target = returning ? returnPoint : entryPoints[destinationIndex];
                if (!TrySafeGround(target, body, returning, out Vector3 safe))
                {
                    Show("Ankunft ist blockiert. Bitte Platz vor dem Ziel-Gate freihalten.", 6);
                    yield break;
                }
                DungeonGate arrivalGate = returning ? entrance : exits[destinationIndex];
                if (arrivalGate && Vector3.Distance(safe, arrivalGate.transform.position) < 1.5f)
                {
                    Show("Ankunft vor dem Gate ist blockiert. Bitte den Bereich freihalten.", 6);
                    yield break;
                }
                Teleport(player, safe);
                ConfigureDungeonMovement(player, !returning);
                InDungeon = !returning;
                currentDungeonLevel = returning ? 0 : destinationLevel;
                if (!returning) deepestUnlocked = Mathf.Max(deepestUnlocked, destinationLevel);
                SceneManager.SetActiveScene(returning ? outsideScene : interiorScene);
                if (entrance) entrance.ResetOccupancy();
                foreach (DungeonGate dungeonExit in exits) if (dungeonExit) dungeonExit.ResetOccupancy();
                if (advanceGate) advanceGate.ResetOccupancy();
                Show(returning ? "Zurück auf der Map – Wellen laufen weiter"
                    : destinationLevel == 2 ? "Dungeon 2 – Gegnerstärke verdoppelt" : "Dungeon 1 – das Skull Gate führt zurück", 5);
            }
            finally
            {
                Unlock();
            }
        }

        private bool CreateDungeon(CharacterController body, int level)
        {
            // A failed attempt is removed; a successful dungeon is retained for the session.
            var root = new GameObject("Cube Dungeon Generator");
            SceneManager.MoveGameObjectToScene(root, interiorScene);
            int index = level - 1;
            root.name += " " + level;
            root.transform.position = dungeonOrigin + Vector3.right * (level - 1) * 1500;
            var candidate = root.AddComponent<CubeDungeonGenerator>();
            candidate.generateOnStart = false;
            candidate.showCeilings = true;
            candidate.gridSize = new Vector3Int(20, 4, 20);
            candidate.roomCount = 8;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                candidate.Clear();
                candidate.GenerateNewSeed();
                Physics.SyncTransforms();
                if (!candidate.GeneratedRoot || candidate.GeneratedRoomCount < 2 || candidate.FailedConnections != 0) continue;
                if (!candidate.TryGetPlayerSpawnPosition(out var floor) || !TrySafeGround(floor, body, false, out entryPoints[index])) continue;
                if (!DungeonPlacement.TryFindGate(entryPoints[index], body, interiorScene, out var gatePoint, out var facing)) continue;
                exits[index] = CreateGate("Zurück zur Map", gatePoint, facing, true, interiorScene);
                generators[index] = candidate;
                var batches = candidate.GeneratedRoot.gameObject.AddComponent<CubeDungeonRenderBatches>();
                batches.Build(candidate);
                AddLights(candidate);
                return true;
            }
            root.SetActive(false);
            Destroy(root);
            return false;
        }

        private void AddLights(CubeDungeonGenerator dungeon)
        {
            var layout = dungeon.GetComponentInChildren<DungeonGenerator>();
            if (!layout) return;
            foreach (var room in layout.GetRooms())
            {
                var lamp = new GameObject("Dungeon Raumlicht");
                lamp.transform.SetParent(dungeon.transform, false);
                lamp.transform.localPosition = room.center + Vector3.up;
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1, 0.83f, 0.61f);
                light.range = 22;
                light.intensity = 2;
                light.shadows = LightShadows.None;
            }
        }

        private bool TrySafeGround(Vector3 near, CharacterController body, bool terrainOnly, out Vector3 result)
            => DungeonPlacement.TryFind(near, body, terrainOnly, interiorScene, out result);

        private void Teleport(GameObject player, Vector3 destination)
        {
            var body = player.GetComponent<CharacterController>();
            bool enabledBefore = body.enabled;
            body.enabled = false;
            player.transform.position = destination;
            player.GetComponent<FirstPersonController>()?.ResetMotionAfterTeleport();
            Physics.SyncTransforms();
            body.enabled = enabledBefore;
        }

        private void Update()
        {
            if (overlay && Time.unscaledTime > hideMessageAt) overlay.gameObject.SetActive(false);
            if (InDungeon && (!traveler || traveler.GetComponent<IDamageable>()?.Health <= 0))
            {
                OnTravelerDeath();
            }
        }

        private void OnDungeonWaveCompleted(int level, int wave)
        {
            if (level != 1 || wave < 10 || advanceGate) return;
            CharacterController body = traveler ? traveler.GetComponent<CharacterController>() : null;
            if (!body) return;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = attempt * 137.5f;
                Vector3 desired = entryPoints[0] + Quaternion.Euler(0, angle, 0) * Vector3.forward * (5 + attempt / 4f);
                if (!DungeonPlacement.TryFindGate(desired, body, interiorScene, out Vector3 point, out Quaternion facing)) continue;
                if (Vector3.Distance(point, exits[0].transform.position) < 4) continue;
                advanceGate = CreateAdvanceGate("Dungeon 2 betreten", point, facing, interiorScene);
                Show("Welle 10 geschafft – das blaue Gate zu Dungeon 2 ist offen!", 8);
                return;
            }
            Show("Welle 10 geschafft – für das zweite Gate wurde noch kein freier Platz gefunden.", 8);
        }

        private DungeonGate CreateAdvanceGate(string title, Vector3 position, Quaternion rotation, Scene scene)
        {
            var root = new GameObject(title);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(position, rotation);
            var gate = root.AddComponent<DungeonGate>();
            gate.InitializeAdvance(this);
            CreateFallbackGateFrame(root.transform);
            CreateGateLabel(root.transform, title);
            return gate;
        }

        private void Unlock()
        {
            if (!IsTransitioning) return;
            FPSFrameworkCore.IsInputActive = previousInput;
            if (lockedController) lockedController.enabled = controllerWasEnabled;
            lockedController = null;
            IsTransitioning = false;
        }

        private void OnTravelerDeath()
        {
            if (InDungeon && CurrentEncounter) CurrentEncounter.ResetAfterDeath();
            ConfigureDungeonMovement(null, false);
            InDungeon = false;
            currentDungeonLevel = 0;
            if (outsideScene.IsValid() && outsideScene.isLoaded) SceneManager.SetActiveScene(outsideScene);
            if (entrance) entrance.ResetOccupancy();
            Show("Respawn in der Außenwelt", 4);
        }

        private void ConfigureDungeonMovement(GameObject player, bool entering)
        {
            if (dungeonPlayerBody)
                dungeonPlayerBody.slopeLimit = outsideSlopeLimit;
            if (dungeonPlayerMovement)
                dungeonPlayerMovement.KeepStepOffsetOnWalkableSlopes = outsideSlopeSteps;
            dungeonPlayerMovement = null;
            dungeonPlayerBody = null;
            if (!entering || !player) return;
            dungeonPlayerBody = player.GetComponent<CharacterController>();
            if (!dungeonPlayerBody) return;
            outsideSlopeLimit = dungeonPlayerBody.slopeLimit;
            // CubeDungeon ramps are 45 degrees. Avoid the FPS controller's speed cutoff
            // and collision-normal rounding at exactly that angle.
            dungeonPlayerBody.slopeLimit = Mathf.Max(50, outsideSlopeLimit);
            dungeonPlayerMovement = player.GetComponent<FirstPersonController>();
            if (dungeonPlayerMovement)
            {
                outsideSlopeSteps = dungeonPlayerMovement.KeepStepOffsetOnWalkableSlopes;
                dungeonPlayerMovement.KeepStepOffsetOnWalkableSlopes = true;
            }
        }

        private DungeonGate CreateGate(string title, Vector3 position, Quaternion rotation, bool returning, Scene scene)
        {
            var root = new GameObject(title);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(position, rotation);
            var gate = root.AddComponent<DungeonGate>();
            gate.Initialize(this, returning);
            if (!gateVisualPrefab) gateVisualPrefab = Resources.Load<GameObject>("NicromGate");
            if (gateVisualPrefab)
            {
                var visual = Instantiate(gateVisualPrefab, root.transform);
                visual.name = "Skull Gate";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    if (bounds.size.y > .001f) visual.transform.localScale *= 4.2f / bounds.size.y;
                    bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    visual.transform.position += Vector3.up * (position.y - bounds.min.y);
                }
            }
            else
            {
                CreateFallbackGateFrame(root.transform);
            }
            CreateGateLabel(root.transform, title);
            return gate;
        }

        private void CreateFallbackGateFrame(Transform root)
        {
            if (!gateMaterial)
            {
                gateMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                gateMaterial.SetColor("_BaseColor", new Color(0.05f, 0.65f, 1));
                gateMaterial.EnableKeyword("_EMISSION");
                gateMaterial.SetColor("_EmissionColor", new Color(0.05f, 0.4f, 0.8f));
            }
            for (int part = 0; part < 3; part++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Gate-Rahmen";
                block.transform.SetParent(root.transform, false);
                block.transform.localPosition = part == 2 ? new Vector3(0, 3, 0) : new Vector3(part == 0 ? -1.25f : 1.25f, 1.5f, 0);
                block.transform.localScale = part == 2 ? new Vector3(2.8f, 0.3f, 0.3f) : new Vector3(0.3f, 3, 0.3f);
                block.GetComponent<Renderer>().sharedMaterial = gateMaterial;
            }
        }

        private static void CreateGateLabel(Transform root, string title)
        {
            var label = new GameObject("Gate-Beschriftung", typeof(TextMeshPro));
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(0, 3.6f, 0);
            var text = label.GetComponent<TextMeshPro>();
            text.text = title + "\nDurchgehen";
            text.fontSize = 3;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(5, 1);
        }

        private void CreateOverlay()
        {
            var root = new GameObject("Dungeon Ladehinweis", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            overlay = root.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var label = new GameObject("Hinweis", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.sizeDelta = new Vector2(1000, 100);
            message = label.GetComponent<TextMeshProUGUI>();
            message.fontSize = 28;
            message.alignment = TextAlignmentOptions.Center;
            message.raycastTarget = false;
            root.SetActive(false);
        }

        private void Show(string text, float seconds)
        {
            message.text = text;
            overlay.gameObject.SetActive(true);
            hideMessageAt = Time.unscaledTime + seconds;
        }

        private void OnDisable() { StopAllCoroutines(); Unlock(); }
        private void OnDestroy()
        {
            ConfigureDungeonMovement(null, false);
            if (Instance == this) Instance = null;
            if (travelerActor) travelerActor.OnDeathConfirmed.RemoveListener(OnTravelerDeath);
            if (gateMaterial) Destroy(gateMaterial);
            if (interiorScene.IsValid() && interiorScene.isLoaded) SceneManager.UnloadSceneAsync(interiorScene);
        }
    }
}
