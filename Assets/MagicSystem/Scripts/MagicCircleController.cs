using UnityEngine;
using UnityEngine.VFX;

namespace MagicSystem
{
    public sealed class MagicCircleController : MonoBehaviour
    {
        static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionStrength");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int UseRuneMaskId = Shader.PropertyToID("_UseRuneMask");
        static readonly int RuneIndexId = Shader.PropertyToID("_RuneIndex");
        static readonly int VfxEnergyId = Shader.PropertyToID("Energy");
        static readonly int VfxRadiusId = Shader.PropertyToID("Radius");
        static readonly int VfxTintId = Shader.PropertyToID("TintColor");

        [Header("Five animated layers")]
        public Transform outerRing;
        public Transform runeSlots;
        public Transform innerCircles;
        public Transform pentagram;
        public Transform core;

        [Header("Layer renderers")]
        [SerializeField] Renderer outerRingRenderer = null;
        [SerializeField] Renderer runeSlotsRenderer = null;
        [SerializeField] Renderer innerCirclesRenderer = null;
        [SerializeField] Renderer pentagramRenderer = null;
        [SerializeField] Renderer coreRenderer = null;

        [Header("Rotation in degrees per second")]
        public float outerRingSpeed = 15f;
        public float runeSlotSpeed = -24f;
        public float innerCircleSpeed = 36f;
        public float pentagramSpeed = -6f;

        [Header("Energy")]
        [SerializeField, Range(0f, 1f)] float energy;
        [SerializeField, Min(.05f)] float energyChangeSpeed = .75f;
        [SerializeField] Color tintColor = new Color(1f, .55f, .08f, 1f);
        [SerializeField, Min(0f)] float baseEmission = 4f;
        [SerializeField] bool startActive = false;

        [Header("Runes")]
        public Transform[] runePositions = new Transform[12];
        [SerializeField] Renderer[] runeHighlights = new Renderer[12];
        [SerializeField, Min(.05f)] float runeFlashDuration = .4f;
        [SerializeField, Min(1f)] float runeActiveBrightness = 2.2f;
        [SerializeField, Min(1f)] float runeFlashBrightness = 7f;

        [Header("Visual Effect Graph")]
        [SerializeField] VisualEffect goldenDust = null;
        [SerializeField, Min(.1f)] float vfxRadius = 3f;

        [Header("Center beam")]
        [SerializeField] MagicCircleCenterBurst centerBurst = null;

        readonly bool[] activeRunes = new bool[12];
        readonly float[] runeFlashRemaining = new float[12];
        MaterialPropertyBlock propertyBlock;
        Renderer[] allRenderers;
        float targetEnergy;
        bool visualsEnabled;

        public float Energy => energy;
        public float TargetEnergy => targetEnergy;
        public bool IsActive => targetEnergy > .001f || energy > .001f;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            allRenderers = GetComponentsInChildren<Renderer>(true);
            targetEnergy = startActive ? 1f : 0f;
            energy = startActive ? 1f : 0f;
            SetVisualsEnabled(startActive);
            ApplyVisuals();
            UpdateVFX(true);
        }

        void Update()
        {
            float previousEnergy = energy;
            energy = Mathf.MoveTowards(energy, targetEnergy, energyChangeSpeed * Time.deltaTime);
            if (targetEnergy > 0f && !visualsEnabled) SetVisualsEnabled(true);

            if (energy > .0001f)
            {
                outerRing?.Rotate(0f, outerRingSpeed * energy * Time.deltaTime, 0f, Space.Self);
                runeSlots?.Rotate(0f, runeSlotSpeed * energy * Time.deltaTime, 0f, Space.Self);
                innerCircles?.Rotate(0f, innerCircleSpeed * energy * Time.deltaTime, 0f, Space.Self);
                pentagram?.Rotate(0f, pentagramSpeed * energy * Time.deltaTime, 0f, Space.Self);
            }

            UpdateRunes();
            ApplyVisuals();
            UpdateVFX(false);

            if (previousEnergy > 0f && energy <= .0001f && targetEnergy <= 0f)
                SetVisualsEnabled(false);
        }

        public void SetEnergy(float value)
        {
            targetEnergy = Mathf.Clamp01(value);
            if (targetEnergy > 0f) SetVisualsEnabled(true);
        }

        public void ActivateCircle()
        {
            SetEnergy(1f);
            centerBurst?.TriggerBurst();
        }

        public void DeactivateCircle()
        {
            SetEnergy(0f);
            centerBurst?.TearOff();
        }

        public void TriggerCenterBurst() => centerBurst?.TriggerBurst();

        public void ActivateRune(int index)
        {
            if (index < 0 || index >= activeRunes.Length)
            {
                Debug.LogWarning($"MagicCircle: Rune index {index} is invalid. Use a value from 0 to 11.", this);
                return;
            }
            if (targetEnergy <= 0f) ActivateCircle();
            activeRunes[index] = true;
            runeFlashRemaining[index] = runeFlashDuration;
        }

        public void ResetRunes()
        {
            for (int i = 0; i < activeRunes.Length; i++)
            {
                activeRunes[i] = false;
                runeFlashRemaining[i] = 0f;
            }
        }

        void UpdateRunes()
        {
            for (int i = 0; i < runeFlashRemaining.Length; i++)
                runeFlashRemaining[i] = Mathf.Max(0f, runeFlashRemaining[i] - Time.deltaTime);
        }

        void ApplyVisuals()
        {
            float alpha = Mathf.SmoothStep(0f, 1f, energy);
            ApplyLayer(outerRingRenderer, alpha, baseEmission * .8f * energy, false, 0);
            ApplyLayer(runeSlotsRenderer, alpha, baseEmission * .95f * energy, false, 0);
            ApplyLayer(innerCirclesRenderer, alpha, baseEmission * 1.05f * energy, false, 0);
            ApplyLayer(pentagramRenderer, alpha, baseEmission * 1.3f * energy, false, 0);
            ApplyLayer(coreRenderer, alpha, baseEmission * 1.65f * energy, false, 0);

            for (int i = 0; i < runeHighlights.Length; i++)
            {
                float brightness = activeRunes[i] ? runeActiveBrightness : 0f;
                if (runeFlashRemaining[i] > 0f)
                {
                    float flash = runeFlashRemaining[i] / runeFlashDuration;
                    brightness = Mathf.Lerp(runeActiveBrightness, runeFlashBrightness, flash * flash);
                }
                ApplyLayer(runeHighlights[i], alpha * Mathf.Clamp01(brightness), baseEmission * brightness * energy, true, i);
            }
        }

        void ApplyLayer(Renderer target, float alpha, float emission, bool runeMask, int runeIndex)
        {
            if (!target) return;
            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintColorId, tintColor);
            propertyBlock.SetFloat(EmissionId, emission);
            propertyBlock.SetFloat(AlphaId, alpha);
            propertyBlock.SetFloat(UseRuneMaskId, runeMask ? 1f : 0f);
            propertyBlock.SetFloat(RuneIndexId, runeIndex);
            target.SetPropertyBlock(propertyBlock);
        }

        void UpdateVFX(bool force)
        {
            UpdateVFXSystem(goldenDust, energy, force, 1f);
        }

        void UpdateVFXSystem(VisualEffect effect, float value, bool force, float speedMultiplier)
        {
            if (!effect) return;
            if (effect.HasFloat(VfxEnergyId)) effect.SetFloat(VfxEnergyId, value);
            if (effect.HasFloat(VfxRadiusId)) effect.SetFloat(VfxRadiusId, vfxRadius);
            if (effect.HasVector4(VfxTintId)) effect.SetVector4(VfxTintId, tintColor);
            effect.playRate = Mathf.Lerp(.05f, 1.35f * speedMultiplier, value);
            bool shouldPlay = value > .001f;
            if (shouldPlay && (force || effect.pause))
            {
                effect.pause = false;
                effect.Reinit();
                effect.Play();
            }
            else if (!shouldPlay)
            {
                effect.Stop();
                effect.pause = true;
            }
        }

        void SetVisualsEnabled(bool enabledState)
        {
            visualsEnabled = enabledState;
            if (allRenderers != null)
                foreach (var target in allRenderers)
                    if (target && (!centerBurst || target != centerBurst.BeamRenderer))
                        target.enabled = enabledState;
        }

        void OnDisable()
        {
            if (goldenDust) goldenDust.pause = true;
        }
    }
}
