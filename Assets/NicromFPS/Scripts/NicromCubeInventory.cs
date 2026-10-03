using System.Collections;
using Akila.FPSFramework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NicromFPS
{
    [DefaultExecutionOrder(-500)]
    public sealed class NicromCubeInventory : MonoBehaviour
    {
        [SerializeField] Material cubeMaterial = null;
        [SerializeField] Material buildPreviewMaterial = null;
        [SerializeField, Min(1)] int cubeCount = 24;
        [SerializeField, Min(1)] float spawnRadius = 25;
        [SerializeField, Min(0.1f)] float pickupRange = 3;

        private readonly ResourceInventory inventory = new ResourceInventory();
        private CharacterInput playerInput;
        private Camera playerCamera;
        private Transform player;
        private InputAction toggleInventory;
        private GameObject panel;
        private TMP_Text summary, prompt, notification;
        private readonly TMP_Text[] slots = new TMP_Text[8];
        private float notificationUntil, nextPlayerSearch;
        private bool inventoryOpen;
        private CubeBuilder builder;
        private const float CubeSize = 0.55f;
        // Include world geometry, but exclude the FPS body and first-person weapon models.
        private const int WorldMask = Physics.DefaultRaycastLayers & ~(1 << 8) & ~(1 << 12);

        private void Awake()
        {
            toggleInventory = new InputAction("Resource inventory", InputActionType.Button, "<Keyboard>/i");
            BuildHud();
            inventory.Changed += RefreshHud;
            RefreshHud();
            builder = new CubeBuilder(transform, inventory, cubeMaterial, buildPreviewMaterial);
        }

        private void OnEnable() => toggleInventory?.Enable();
        private void OnDisable()
        {
            toggleInventory?.Disable();
            builder?.Cancel();
        }
        private void OnDestroy()
        {
            inventory.Changed -= RefreshHud;
            toggleInventory?.Dispose();
            builder?.Dispose();
        }

        private IEnumerator Start()
        {
            while (!FindPlayer()) yield return new WaitForSeconds(0.25f);
            // The player's terrain placement happens in Awake, before this coroutine runs.
            SpawnCubes();
        }

        private bool FindPlayer()
        {
            foreach (NicromTerrainSpawn spawn in FindObjectsByType<NicromTerrainSpawn>())
            {
                if (spawn.gameObject.scene != gameObject.scene) continue;
                foreach (Camera camera in spawn.GetComponentsInChildren<Camera>())
                {
                    if (!camera.CompareTag("MainCamera")) continue;
                    player = spawn.transform;
                    playerCamera = camera;
                    playerInput = spawn.GetComponent<CharacterInput>();
                    return playerInput != null;
                }
            }
            return false;
        }

        private void Update()
        {
            if ((!player || !playerCamera || !playerInput) && Time.unscaledTime >= nextPlayerSearch)
            {
                FindPlayer();
                nextPlayerSearch = Time.unscaledTime + 0.5f;
            }
            bool canInteract = player && playerCamera && playerInput && Time.timeScale > 0
                && FPSFrameworkCore.IsActive && FPSFrameworkCore.IsInputActive && !FPSFrameworkCore.IsPaused;
            prompt.text = "";
            notification.gameObject.SetActive(Time.unscaledTime < notificationUntil);
            panel.SetActive(inventoryOpen && canInteract);
            string buildPrompt = builder.Tick(player, playerCamera, canInteract);
            if (!canInteract) return;
            if (buildPrompt != null)
            {
                panel.SetActive(false);
                prompt.text = buildPrompt;
                return;
            }

            if (toggleInventory.WasPressedThisFrame())
            {
                inventoryOpen = !inventoryOpen;
                panel.SetActive(inventoryOpen);
            }

            // Raycast against ALL world geometry: cubes behind walls cannot be collected.
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit = default;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit candidate in Physics.RaycastAll(ray, pickupRange, WorldMask, QueryTriggerInteraction.Collide))
            {
                if (candidate.collider.isTrigger && !candidate.collider.GetComponentInParent<WallBlueprint>()) continue;
                if (candidate.distance >= nearest) continue;
                nearest = candidate.distance;
                hit = candidate;
            }
            if (!hit.collider) return;
            WallBlueprint wall = hit.collider.GetComponentInParent<WallBlueprint>();
            if (wall)
            {
                prompt.text = wall.GetPrompt(inventory, player);
                if (playerInput.Controls.Player.Interact.WasPressedThisFrame() && wall.TrySupply(inventory, player))
                {
                    notification.text = "Würfel verbaut · -1 im Inventar";
                    notificationUntil = Time.unscaledTime + 2;
                    notification.gameObject.SetActive(true);
                    prompt.text = wall.GetPrompt(inventory, player);
                }
                return;
            }
            CubePickup cube = hit.collider.GetComponent<CubePickup>();
            if (!cube) return;
            bool hasSpace = inventory.CanAdd(CubePickup.ItemId, 1);
            prompt.text = hasSpace ? "[F] Grauen Würfel einsammeln" : "Inventar voll";
            if (hasSpace && playerInput.Controls.Player.Interact.WasPressedThisFrame() && cube.TryCollect(inventory))
            {
                notification.text = "+1 Grauer Würfel";
                notificationUntil = Time.unscaledTime + 2;
                notification.gameObject.SetActive(true);
                prompt.text = "";
            }
        }

        private void SpawnCubes()
        {
            if (!cubeMaterial)
            {
                Debug.LogError("Nicrom cubes: Assign the gray cube material.", this);
                return;
            }
            Physics.SyncTransforms();
            var random = new System.Random(1701);
            int spawned = 0;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            for (int attempt = 0; attempt < cubeCount * 60 && spawned < cubeCount; attempt++)
            {
                // Put the first few cubes in front of the player, then scatter the rest nearby.
                float angle = attempt < 6 ? (attempt - 2) * 15 : (float)random.NextDouble() * 360;
                float distance = attempt < 6 ? 2 + attempt * 0.6f : Mathf.Lerp(4, spawnRadius, (float)random.NextDouble());
                Vector3 candidate = player.position + Quaternion.Euler(0, angle, 0) * forward * distance;
                if (!TryFindGround(candidate, out Vector3 center, out Quaternion rotation)) continue;
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(cube, gameObject.scene);
                cube.name = "Grauer Würfel " + (spawned + 1);
                cube.transform.SetParent(transform, false);
                cube.transform.SetPositionAndRotation(center, rotation);
                cube.transform.localScale = Vector3.one * CubeSize;
                cube.GetComponent<Renderer>().sharedMaterial = cubeMaterial;
                cube.AddComponent<CubePickup>();
                // Default layer keeps resource pickups out of the framework's weapon pickup scan.
                cube.layer = 0;
                Physics.SyncTransforms();
                spawned++;
            }
            if (spawned < cubeCount)
                Debug.LogWarning($"Nicrom cubes: Found space for {spawned}/{cubeCount} cubes near the player.", this);
        }

        private bool TryFindGround(Vector3 candidate, out Vector3 center, out Quaternion rotation)
        {
            center = default;
            rotation = Quaternion.identity;
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain.gameObject.scene != gameObject.scene) continue;
                Vector3 local = candidate - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x < 1 || local.z < 1 || local.x > size.x - 1 || local.z > size.z - 1) continue;
                var ground = terrain.GetComponent<TerrainCollider>();
                if (!ground || !ground.enabled) continue;
                Ray ray = new Ray(new Vector3(candidate.x, terrain.transform.position.y + size.y + 10, candidate.z), Vector3.down);
                if (!ground.Raycast(ray, out RaycastHit hit, size.y + 20) || Vector3.Angle(hit.normal, Vector3.up) > 25) continue;
                if (Physics.Raycast(ray, out RaycastHit water, size.y + 20, 1 << 4, QueryTriggerInteraction.Collide)
                    && water.point.y > hit.point.y) continue;
                rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                center = hit.point + hit.normal * (CubeSize * 0.5f + 0.03f);
                if (Physics.CheckBox(center, Vector3.one * (CubeSize * 0.5f + 0.01f), rotation,
                    WorldMask, QueryTriggerInteraction.Ignore)) continue;
                return true;
            }
            return false;
        }

        private void BuildHud()
        {
            var canvasObject = new GameObject("Würfel-Inventar HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform badge = Box(canvasObject.transform, "Inventar", new Vector2(1, 1), new Vector2(-28, -28), new Vector2(310, 82));
            summary = Label(badge, "Anzahl", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(285, 72), 23);
            prompt = Label(canvasObject.transform, "Aufheben", new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(900, 46), 26);
            prompt.alignment = TextAlignmentOptions.Center;
            notification = Label(canvasObject.transform, "Gesammelt", new Vector2(0.5f, 0.5f), new Vector2(0, -128), new Vector2(580, 40), 23);
            notification.alignment = TextAlignmentOptions.Center;
            notification.color = new Color(0.65f, 0.92f, 0.72f);

            RectTransform inventoryPanel = Box(canvasObject.transform, "Inventarübersicht", new Vector2(1, 1), new Vector2(-28, -126), new Vector2(590, 345));
            panel = inventoryPanel.gameObject;
            Label(panel.transform, "Titel", new Vector2(0, 1), new Vector2(22, -18), new Vector2(540, 45), 28).text = "INVENTAR";
            Label(panel.transform, "Hilfe", new Vector2(0, 1), new Vector2(22, -66), new Vector2(540, 32), 18).text = "20 Würfel je Stapel  ·  I zum Schließen";
            for (int i = 0; i < slots.Length; i++)
            {
                RectTransform slot = Box(panel.transform, "Platz " + (i + 1), new Vector2(0, 1),
                    new Vector2(22 + (i % 4) * 139, -115 - (i / 4) * 103), new Vector2(128, 92));
                slot.GetComponent<Image>().color = new Color(0.2f, 0.23f, 0.27f, 1);
                slots[i] = Label(slot, "Inhalt", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 84), 20);
                slots[i].alignment = TextAlignmentOptions.Center;
            }
            panel.SetActive(false);
        }

        private void RefreshHud()
        {
            summary.text = $"Graue Würfel: {inventory.Count(CubePickup.ItemId)}\n<size=17>[I] Inventar  ·  [T] Bauen</size>";
            var stacks = inventory.Stacks;
            for (int i = 0; i < slots.Length; i++)
                slots[i].text = i < stacks.Count ? $"Grauer Würfel\n<size=27>{stacks[i].Count}</size> / {inventory.StackLimit}" : "<color=#89929B>Leer</color>";
        }

        private static RectTransform Rect(GameObject obj, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.GetComponent<Image>().color = new Color(0.055f, 0.07f, 0.09f, 0.94f);
            obj.GetComponent<Image>().raycastTarget = false;
            return Rect(obj, parent, anchor, position, size);
        }

        private static TMP_Text Label(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            Rect(obj, parent, anchor, position, size);
            var label = obj.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.text = "";
            return label;
        }
    }
}
