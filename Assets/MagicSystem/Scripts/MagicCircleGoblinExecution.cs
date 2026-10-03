using System.Collections;
using System.Collections.Generic;
using Akila.FPSFramework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicSystem
{
    /// <summary>Places the circle at the aimed point and executes every damageable inside its expansion.</summary>
    public sealed class MagicCircleGoblinExecution : MonoBehaviour
    {
        const string ResourceName = "MagicCircleExecution";
        const float MaximumRange = 80f;
        const float FallbackDistance = 25f;
        const float KillRadius = 3f;
        const float KillDelay = .55f;
        const float EffectLifetime = 2.1f;
        bool casting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<MagicCircleGoblinExecution>()) return;
            var host = new GameObject("Magic Circle Goblin Execution");
            DontDestroyOnLoad(host);
            host.AddComponent<MagicCircleGoblinExecution>();
        }

        void Update()
        {
            bool controllerPressed = Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame;
            bool keyboardPressed = Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame;
            if (casting || (!controllerPressed && !keyboardPressed) || !FPSFrameworkCore.IsActive
                || !FPSFrameworkCore.IsInputActive || FPSFrameworkCore.IsPaused) return;
            Camera view = Camera.main;
            if (!view) return;
            if (!FindPlacement(view, out Vector3 position)) return;
            StartCoroutine(Execute(position, view.transform.root));
        }

        static bool FindPlacement(Camera view, out Vector3 position)
        {
            RaycastHit[] hits = Physics.RaycastAll(view.transform.position, view.transform.forward, MaximumRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            Transform playerRoot = view.transform.root;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(playerRoot)) continue;
                return MagicCircleTerrainConformer.TryFindGround(hit.point, playerRoot, out position);
            }
            Vector3 fallback = view.transform.position + view.transform.forward * FallbackDistance;
            return MagicCircleTerrainConformer.TryFindGround(fallback, playerRoot, out position);
        }

        IEnumerator Execute(Vector3 position, Transform playerRoot)
        {
            casting = true;
            GameObject prefab = Resources.Load<GameObject>(ResourceName);
            if (!prefab)
            {
                Debug.LogError("MagicCircleExecution fehlt im Resources-Ordner.", this);
                casting = false;
                yield break;
            }

            GameObject effect = Instantiate(prefab, position, Quaternion.identity);
            effect.name = "Area Instant Kill – Magic Circle";
            effect.AddComponent<MagicCircleTerrainConformer>().Conform(playerRoot);
            MagicCircleController circle = effect.GetComponent<MagicCircleController>();
            if (circle) circle.ActivateCircle();

            float elapsed = 0f;
            while (elapsed < KillDelay)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            KillEverythingInRadius(position, playerRoot);

            elapsed = KillDelay;
            while (elapsed < EffectLifetime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (circle) circle.DeactivateCircle();
            if (effect) Destroy(effect, .45f);
            casting = false;
        }

        void KillEverythingInRadius(Vector3 center, Transform playerRoot)
        {
            var killed = new HashSet<IDamageable>();
            foreach (Collider hit in Physics.OverlapSphere(center, KillRadius, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide))
            {
                foreach (MonoBehaviour component in hit.GetComponentsInParent<MonoBehaviour>(true))
                {
                    if (!(component is IDamageable damageable) || !killed.Add(damageable)) continue;
                    if (playerRoot && (component.transform == playerRoot || component.transform.IsChildOf(playerRoot))) continue;
                    if (damageable.Health > 0) damageable.Damage(float.MaxValue, gameObject);
                }
            }
        }
    }
}
