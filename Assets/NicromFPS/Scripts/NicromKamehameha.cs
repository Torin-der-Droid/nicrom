using Akila.FPSFramework;
using EnergyEffects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NicromFPS
{
    [DefaultExecutionOrder(-20)]
    public sealed class NicromKamehameha : MonoBehaviour
    {
        [SerializeField] KamehamehaEffect effectPrefab;
        [SerializeField] Vector3 muzzleOffset = new Vector3(-0.3f, -0.22f, 0.65f);
        [SerializeField, Min(1)] float range = 35;
        InputAction fire;
        Camera view;
        IDamageable health;
        KamehamehaEffect effect;
        Transform aim;

        void Awake()
        {
            health = GetComponent<IDamageable>();
            foreach (var camera in GetComponentsInChildren<Camera>(true))
                if (camera.CompareTag("MainCamera")) { view = camera; break; }
            fire = new InputAction("Kamehameha", InputActionType.Button, "<Keyboard>/k");
            if (!effectPrefab || !view)
            {
                Debug.LogError("Kamehameha: Effekt-Prefab oder Spielerkamera fehlt.", this);
                enabled = false;
                return;
            }
            effect = Instantiate(effectPrefab, transform);
            effect.name = "Player Kamehameha";
            effect.autoRepeat = false;
            effect.chargeDuration = 0;
            effect.beamLength = range;
            effect.hitLayers = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player", "FPS Object", "Water");
            aim = new GameObject("Kamehameha Aim").transform;
            aim.SetParent(transform, false);
            effect.target = aim;
        }

        void OnEnable() => fire?.Enable();
        void Update()
        {
            if (!effect || !view) return;
            bool canFire = view.isActiveAndEnabled && health != null && health.Health > 0
                && FPSFrameworkCore.IsActive && FPSFrameworkCore.IsInputActive && !FPSFrameworkCore.IsPaused
                && !(DungeonTravelManager.Instance && DungeonTravelManager.Instance.IsTransitioning);
            if (!canFire)
            {
                if (effect.IsPlaying) effect.StopEffect();
                return;
            }
            var ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            bool hit = Physics.Raycast(ray, out RaycastHit info, range, effect.hitLayers, QueryTriggerInteraction.Ignore);
            Vector3 point = hit ? info.point : ray.GetPoint(range);
            Vector3 origin = view.transform.TransformPoint(muzzleOffset);
            // Keep the muzzle on this side of nearby walls.
            Vector3 toMuzzle = origin - ray.origin;
            if (Physics.Raycast(ray.origin, toMuzzle.normalized, out RaycastHit obstruction,
                    toMuzzle.magnitude, effect.hitLayers, QueryTriggerInteraction.Ignore))
                origin = obstruction.point - toMuzzle.normalized * 0.04f;
            Vector3 direction = point - origin;
            if (direction.sqrMagnitude < 0.001f) direction = ray.direction;
            // Aim slightly beyond the surface so the beam's own raycast detects the impact.
            aim.position = point + direction.normalized * 0.1f;
            effect.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            if (fire.WasPressedThisFrame()) effect.Fire();
        }
        void OnDisable()
        {
            fire?.Disable();
            if (effect) effect.StopEffect();
        }
        void OnDestroy() => fire?.Dispose();
    }
}
