using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicSystem
{
    public sealed class MagicCircleDemoController : MonoBehaviour
    {
        public MagicCircleController circle;
        float sliderEnergy;

        void Start() => sliderEnergy = circle ? circle.TargetEnergy : 0f;

        void Update()
        {
            if (!circle || Keyboard.current == null) return;
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (circle.TargetEnergy > .5f) circle.DeactivateCircle();
                else circle.ActivateCircle();
                sliderEnergy = circle.TargetEnergy;
            }
        }

        void OnGUI()
        {
            if (!circle) return;
            const float panelWidth = 420f;
            GUI.Box(new Rect(20, 20, panelWidth, 225), "GOLDENER MAGIEKREIS");
            GUI.Label(new Rect(40, 50, 370, 24), "Leertaste: aktivieren / deaktivieren");

            if (GUI.Button(new Rect(40, 78, 110, 30), "Aktivieren"))
            {
                circle.ActivateCircle();
                sliderEnergy = 1f;
            }
            if (GUI.Button(new Rect(160, 78, 110, 30), "Deaktivieren"))
            {
                circle.DeactivateCircle();
                sliderEnergy = 0f;
            }
            if (GUI.Button(new Rect(280, 78, 120, 30), "Runen löschen")) circle.ResetRunes();

            GUI.Label(new Rect(40, 116, 70, 24), "Energie");
            float changedEnergy = GUI.HorizontalSlider(new Rect(105, 121, 255, 20), sliderEnergy, 0f, 1f);
            GUI.Label(new Rect(370, 116, 45, 24), changedEnergy.ToString("0.00"));
            if (!Mathf.Approximately(changedEnergy, sliderEnergy))
            {
                sliderEnergy = changedEnergy;
                circle.SetEnergy(sliderEnergy);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = 40 + (i % 6) * 60;
                float y = 150 + (i / 6) * 29;
                if (GUI.Button(new Rect(x, y, 52, 25), $"Rune {i + 1}")) circle.ActivateRune(i);
            }

            if (GUI.Button(new Rect(40, 209, 360, 28), "Zentralstrahl auslösen"))
                circle.TriggerCenterBurst();
        }
    }
}
