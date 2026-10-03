using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EnergyEffects
{
    public sealed class KamehamehaDemo : MonoBehaviour
    {
        public KamehamehaEffect effect;
        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) effect.Fire();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space)) effect.Fire();
#endif
        }
        void OnGUI()
        {
            GUI.Box(new Rect(20, 20, 330, 110), "KAMEHAMEHA / PARTICLE SYSTEM");
            GUI.Label(new Rect(35, 48, 310, 24), "Leertaste: abfeuern | Ohne Ladekugel");
            if (GUI.Button(new Rect(35, 80, 130, 32), "Abfeuern")) effect.Fire();
            effect.autoRepeat = GUI.Toggle(new Rect(185, 86, 150, 28), effect.autoRepeat, "Wiederholen");
        }
    }
}
