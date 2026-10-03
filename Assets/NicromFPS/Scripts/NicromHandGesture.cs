using System;
using Akila.FPSFramework;
using EnergyEffects;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NicromFPS
{
    [DefaultExecutionOrder(10000)]
    public sealed class NicromHandGesture : MonoBehaviour
    {
        [Header("Kamehameha")]
        [SerializeField] KamehamehaEffect kamehamehaPrefab;
        [SerializeField, Min(.1f)] float kamehamehaDuration = .35f;
        [SerializeField, Min(0)] float palmForwardOffset = .08f;
        [SerializeField, Min(0)] float beamDamagePerSecond = 2500f;
        [SerializeField, Min(.02f)] float damageInterval = .1f;
        [SerializeField] AudioClip beamSound;
        [SerializeField, Range(0, 1)] float beamVolume = 1f;
        [SerializeField, Range(1, 4)] int beamAudioLayers = 3;
        [SerializeField, Min(0)] float manaCostPerSecond = 200f;

        readonly HandGestureTiming timing = new HandGestureTiming();
        InputAction gesture;
        Inventory inventory;
        IDamageable health;
        Transform view;
        Firearm weapon;
        LeftHandGesturePose pose;
        KamehamehaEffect kamehameha;
        AudioSource[] beamAudio;
        bool gestureHeld;
        bool manaDepleted;
        float nextDamageTime;
        float manaCostAccumulator;
        public Transform MagicOrigin => pose?.Palm;
        public bool IsGesturing => timing.Playing;
        // Future VFX can subscribe here. The palm's +Z axis points outwards.
        public event Action<Transform> PalmExtended;

        void Awake()
        {
            inventory = GetComponentInChildren<Inventory>(true);
            health = GetComponent<IDamageable>();
            foreach (var camera in GetComponentsInChildren<Camera>(true))
                if (camera.CompareTag("MainCamera")) view = camera.transform;
            gesture = new InputAction("Left hand thrust", InputActionType.Button, "<Gamepad>/leftShoulder");
        }
        void OnEnable() => gesture?.Enable();
        void Update()
        {
            // Remove last frame's correction before Animator evaluates this frame's weapon pose.
            pose?.Restore();
            var current = inventory && inventory.currentItem ? inventory.currentItem.firearm : null;
            if (current != weapon)
            {
                CancelGesture(); pose?.Dispose(); pose = null; weapon = current;
                if (weapon && !LeftHandGesturePose.TryCreate(weapon.transform, out pose))
                    Debug.LogWarning("L1-Handgeste: Dieses Waffenmodell hat kein unterstütztes linkes Hand-Rig.", weapon);
            }
            if (!CanGesture()) { CancelGesture(); return; }
            bool paused = FPSFrameworkCore.IsPaused;
            gestureHeld = gesture.IsPressed();
            if (paused)
            {
                timing.Tick(0, true);
                StopKamehameha();
                return;
            }
            if (gesture.WasPressedThisFrame())
            {
                manaDepleted = false;
                manaCostAccumulator = 0;
                timing.Trigger();
            }
            if (gesture.WasReleasedThisFrame())
            {
                manaDepleted = false;
                manaCostAccumulator = 0;
                StopKamehameha();
            }

            // Extend to the peak, hold there for as long as L1 is held, then
            // continue the existing return animation after release.
            float step = Time.deltaTime;
            if (gestureHeld && timing.Playing)
                step = Mathf.Min(step, Mathf.Max(0, HandGestureTiming.PeakTime - timing.Elapsed));
            timing.Tick(step, false);
        }
        bool CanGesture() => pose != null && weapon && weapon.isActiveAndEnabled && view
            && health != null && health.Health > 0 && FPSFrameworkCore.IsActive
            && (FPSFrameworkCore.IsInputActive || FPSFrameworkCore.IsPaused)
            && !weapon.isReloading && !weapon.isTryingToDrop && !weapon.isPlayingHidingAnimation
            && !(DungeonTravelManager.Instance && DungeonTravelManager.Instance.IsTransitioning);
        void LateUpdate()
        {
            if (pose == null) return;
            if (!CanGesture()) { pose.Restore(); CancelGesture(); return; }
            pose.Apply(view, timing.Weight);
            FollowPalm();
            bool handExtended = timing.Playing && timing.Elapsed >= HandGestureTiming.PeakTime;
            if (gestureHeld && !manaDepleted && handExtended && !FPSFrameworkCore.IsPaused
                && (!kamehameha || !kamehameha.IsPlaying))
            {
                FireKamehameha();
                PalmExtended?.Invoke(pose.Palm);
            }
            if (gestureHeld && handExtended && kamehameha && kamehameha.IsPlaying)
            {
                ConsumeMana();
                ApplyBeamDamage();
            }
        }

        void FireKamehameha()
        {
            if (!kamehamehaPrefab || pose?.Palm == null)
            {
                if (!kamehamehaPrefab)
                    Debug.LogWarning("L1-Kamehameha: Im Spieler-Prefab ist kein Kamehameha-Effekt zugewiesen.", this);
                return;
            }

            if (!kamehameha)
            {
                kamehameha = Instantiate(kamehamehaPrefab);
                kamehameha.name = "Player Kamehameha";
                kamehameha.autoRepeat = false;
                kamehameha.chargeDuration = 0;
                kamehameha.beamDuration = kamehamehaDuration;
                kamehameha.hitLayers = Physics.DefaultRaycastLayers
                    & ~LayerMask.GetMask("Player", "FPS Object", "Water");
                beamAudio = new AudioSource[beamAudioLayers];
                for (int i = 0; i < beamAudio.Length; i++)
                {
                    beamAudio[i] = kamehameha.gameObject.AddComponent<AudioSource>();
                    beamAudio[i].playOnAwake = false;
                    beamAudio[i].loop = true;
                    beamAudio[i].spatialBlend = 0;
                    beamAudio[i].volume = beamVolume;
                    beamAudio[i].clip = beamSound;
                }
            }

            FollowPalm(true);
            nextDamageTime = Time.time;
            kamehameha.Fire(true);
            if (beamAudio != null)
                foreach (AudioSource layer in beamAudio)
                    if (layer && layer.clip && !layer.isPlaying) layer.Play();
        }

        void FollowPalm(bool force = false)
        {
            if (!kamehameha || (!force && !kamehameha.IsPlaying) || pose?.Palm == null) return;
            Transform palm = pose.Palm;
            kamehameha.transform.SetPositionAndRotation(
                palm.position + palm.forward * palmForwardOffset,
                palm.rotation);
        }

        void ApplyBeamDamage()
        {
            if (Time.time < nextDamageTime || !kamehameha) return;
            nextDamageTime = Time.time + damageInterval;

            Vector3 origin = kamehameha.transform.position;
            Vector3 direction = kamehameha.transform.forward;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, kamehameha.beamLength,
                kamehameha.hitLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                IDamageable damageable = FindDamageable(hit.transform);
                if (damageable != null && damageable.Health > 0 && !damageable.isDamagableDisabled)
                    damageable.Damage(beamDamagePerSecond * damageInterval, gameObject);
                // The first non-player collider blocks the beam, damageable or not.
                break;
            }
        }

        void ConsumeMana()
        {
            ManaWallet wallet = ManaWallet.Active;
            if (!wallet || wallet.Mana <= 0)
            {
                manaDepleted = true;
                StopKamehameha();
                return;
            }

            manaCostAccumulator += manaCostPerSecond * Time.deltaTime;
            int due = Mathf.FloorToInt(manaCostAccumulator);
            if (due <= 0) return;
            int spent = wallet.Spend(due);
            manaCostAccumulator -= spent;
            if (spent < due || wallet.Mana <= 0)
            {
                manaCostAccumulator = 0;
                manaDepleted = true;
                StopKamehameha();
            }
        }

        static IDamageable FindDamageable(Transform hit)
        {
            for (Transform current = hit; current; current = current.parent)
                foreach (MonoBehaviour component in current.GetComponents<MonoBehaviour>())
                    if (component is IDamageable damageable) return damageable;
            return null;
        }

        void CancelGesture()
        {
            gestureHeld = false;
            manaDepleted = false;
            manaCostAccumulator = 0;
            timing.Cancel();
            StopKamehameha();
        }

        void StopKamehameha()
        {
            if (kamehameha && kamehameha.IsPlaying) kamehameha.StopEffect();
            if (beamAudio != null)
                foreach (AudioSource layer in beamAudio)
                    if (layer && layer.isPlaying) layer.Stop();
        }
        void OnDisable()
        {
            gesture?.Disable(); CancelGesture(); pose?.Dispose(); pose = null; weapon = null;
        }
        void OnDestroy()
        {
            gesture?.Dispose();
            if (kamehameha) Destroy(kamehameha.gameObject);
        }
    }
}
