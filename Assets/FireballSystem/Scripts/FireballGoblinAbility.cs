using Akila.FPSFramework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireballSystem
{
    /// <summary>Fires the imported anime fireball from the active FPS camera.</summary>
    public sealed class FireballGoblinAbility : MonoBehaviour
    {
        const string ResourceName = "AnimeFireballAbility";
        const float Cooldown = .7f;
        const float ProjectileSpeed = 20f;
        float nextShot;
        AnimeFireballProjectile prefab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<FireballGoblinAbility>()) return;
            var host = new GameObject("Fireball Goblin Ability");
            DontDestroyOnLoad(host);
            host.AddComponent<FireballGoblinAbility>();
        }

        void Update()
        {
            bool controllerPressed = Gamepad.current != null && Gamepad.current.dpad.left.wasPressedThisFrame;
            bool keyboardPressed = Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame;
            if ((!controllerPressed && !keyboardPressed) || Time.time < nextShot || !FPSFrameworkCore.IsActive
                || !FPSFrameworkCore.IsInputActive || FPSFrameworkCore.IsPaused) return;

            Camera view = Camera.main;
            if (!view) return;
            if (!prefab)
            {
                GameObject resource = Resources.Load<GameObject>(ResourceName);
                prefab = resource ? resource.GetComponent<AnimeFireballProjectile>() : null;
            }
            if (!prefab)
            {
                Debug.LogError("AnimeFireballAbility fehlt im Resources-Ordner.", this);
                return;
            }

            Vector3 direction = view.transform.forward;
            Vector3 position = view.transform.position + direction * 1.25f - view.transform.up * .12f;
            AnimeFireballProjectile shot = Instantiate(prefab, position, Quaternion.LookRotation(direction));
            shot.IgnoreRoot(view.transform.root);
            shot.Speed = ProjectileSpeed;
            shot.Launch(direction);
            nextShot = Time.time + Cooldown;
        }
    }
}
