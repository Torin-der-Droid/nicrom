using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CubeDungeon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Cube Dungeon/Spielersteuerung")]
    public sealed class CubeDungeonPlayerController : MonoBehaviour
    {
        public CubeDungeonGenerator dungeonGenerator;
        public Camera viewCamera;
        [Min(0.1f)] public float movementSpeed = 5f;
        [Min(0.1f)] public float jumpHeight = 1.2f;
        [Min(0.1f)] public float gravity = 20f;
        [Min(0.01f)] public float mouseSensitivity = .12f;
        [Min(0.1f)] public float eyeHeight = 1.6f;

        CharacterController characterController;
        float yaw;
        float pitch;
        float verticalSpeed;
        bool ready;

        void Awake()
        {
            characterController = GetComponent<CharacterController>();
            characterController.enabled = false;
        }

        IEnumerator Start()
        {
            if (dungeonGenerator == null)
                dungeonGenerator = FindAnyObjectByType<CubeDungeonGenerator>();
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera != null)
            {
                viewCamera.orthographic = false;
                viewCamera.nearClipPlane = .05f;
            }

            // Keep the capsule as the collider and shadow, without seeing the
            // inside of its mesh from the camera at eye level.
            var visual = transform.Find("Capsule Visual");
            if (visual != null)
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            while (dungeonGenerator == null)
            {
                dungeonGenerator = FindAnyObjectByType<CubeDungeonGenerator>();
                yield return null;
            }

            // The generator also builds its dungeon in Start(). Wait one frame
            // so this coroutine cannot inspect an empty/stale serialized root
            // before the generator has replaced it.
            yield return null;

            Vector3 spawnPosition = default;
            var hasSpawn = false;
            var waitTime = 0f;
            const float spawnWaitLimit = 10f;
            while (!hasSpawn && waitTime < spawnWaitLimit)
            {
                if (dungeonGenerator == null)
                    dungeonGenerator = FindAnyObjectByType<CubeDungeonGenerator>();

                if (dungeonGenerator != null && !dungeonGenerator.IsGenerating)
                    hasSpawn = dungeonGenerator.TryGetPlayerSpawnPosition(out spawnPosition);

                if (!hasSpawn)
                {
                    // A scene can contain more than one generator. Prefer the
                    // assigned one, then use whichever has a generated floor.
                    foreach (var candidate in FindObjectsByType<CubeDungeonGenerator>())
                    {
                        if (candidate == null || candidate.IsGenerating) continue;
                        if (!candidate.TryGetPlayerSpawnPosition(out spawnPosition)) continue;
                        dungeonGenerator = candidate;
                        hasSpawn = true;
                        break;
                    }
                }

                if (!hasSpawn)
                {
                    yield return null;
                    waitTime += Time.unscaledDeltaTime;
                }
            }

            if (!hasSpawn)
            {
                var rootState = dungeonGenerator == null || dungeonGenerator.GeneratedRoot == null
                    ? "kein erzeugter Dungeon"
                    : $"{dungeonGenerator.BlockCount} Cubes, {dungeonGenerator.GeneratedRoomCount} Räume";
                Debug.LogError($"Der Spieler konnte keinen begehbaren Startpunkt finden. Nach {spawnWaitLimit:0} s: {rootState}.", this);
                yield break;
            }

            transform.position = spawnPosition;
            yaw = transform.eulerAngles.y;
            characterController.enabled = true;
            ready = true;
            SetCursorLocked(true);
            UpdateCamera();
        }

        void Update()
        {
            if (!ready) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (keyboard != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);

            var input = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked && keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;

                if (mouse != null)
                {
                    var look = mouse.delta.ReadValue() * mouseSensitivity;
                    yaw += look.x;
                    pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                }
            }

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            if (characterController.isGrounded)
            {
                verticalSpeed = -2f;
                if (Cursor.lockState == CursorLockMode.Locked && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                    verticalSpeed = Mathf.Sqrt(jumpHeight * 2f * gravity);
            }
            else
            {
                verticalSpeed -= gravity * Time.deltaTime;
            }

            var velocity = direction * movementSpeed + Vector3.up * verticalSpeed;
            characterController.Move(velocity * Time.deltaTime);
        }

        void LateUpdate()
        {
            if (ready) UpdateCamera();
        }

        void OnDisable()
        {
            if (ready) SetCursorLocked(false);
        }

        void UpdateCamera()
        {
            if (viewCamera == null) return;
            viewCamera.transform.SetPositionAndRotation(
                transform.TransformPoint(Vector3.up * eyeHeight), Quaternion.Euler(pitch, yaw, 0f));
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
