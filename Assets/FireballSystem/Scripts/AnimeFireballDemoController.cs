using UnityEngine;
using UnityEngine.InputSystem;

namespace FireballSystem
{
    public sealed class AnimeFireballDemoController : MonoBehaviour
    {
        public AnimeFireballLauncher launcher;
        public bool autoRepeat = true;
        [SerializeField, Min(.3f)] float repeatInterval = 1.4f;

        float repeatTimer = .35f;

        void Update()
        {
            if (!launcher) return;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                launcher.Fire();
                repeatTimer = repeatInterval;
            }
            if (!autoRepeat) return;
            repeatTimer -= Time.deltaTime;
            if (repeatTimer <= 0f)
            {
                launcher.Fire();
                repeatTimer = repeatInterval;
            }
        }

        void OnGUI()
        {
            if (!launcher) return;
            GUI.Box(new Rect(20, 20, 360, 140), "ANIME-FEUERBALL");
            GUI.Label(new Rect(40, 50, 280, 24), "Leertaste oder Button: Feuerball abfeuern");
            if (GUI.Button(new Rect(40, 80, 290, 32), "FEUERBALL")) launcher.Fire();
            autoRepeat = GUI.Toggle(new Rect(40, 118, 220, 24), autoRepeat, "Automatisch wiederholen");
        }
    }
}
