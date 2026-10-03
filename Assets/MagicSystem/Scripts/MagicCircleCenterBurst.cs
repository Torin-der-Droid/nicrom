using UnityEngine;

namespace MagicSystem
{
    public sealed class MagicCircleCenterBurst : MonoBehaviour
    {
        static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionStrength");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int TearProgressId = Shader.PropertyToID("_TearProgress");
        static readonly int FlickerId = Shader.PropertyToID("_Flicker");

        enum BurstPhase { Hidden, Focused, Expanding, Holding, Tearing }

        [SerializeField] Renderer beamRenderer = null;
        [SerializeField] Color tintColor = new Color(1f, .62f, .08f, 1f);
        [SerializeField, Min(.01f)] float thinDiameter = .09f;
        [SerializeField, Min(.1f)] float maximumDiameter = 6f;
        [SerializeField, Min(.01f)] float focusedDuration = .45f;
        [SerializeField, Min(.01f)] float expansionDuration = .18f;
        [SerializeField, Min(.01f)] float holdDuration = .16f;
        [SerializeField, Min(.01f)] float tearDuration = .28f;
        [SerializeField, Min(1f)] float focusedEmission = 14f;
        [SerializeField, Min(1f)] float expandedEmission = 9f;

        MaterialPropertyBlock propertyBlock;
        BurstPhase phase = BurstPhase.Hidden;
        float phaseTime;
        float beamHeightScale;

        public bool IsPlaying => phase != BurstPhase.Hidden;
        public Renderer BeamRenderer => beamRenderer;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            beamHeightScale = transform.localScale.y;
            HideImmediately();
        }

        void OnEnable()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (phase == BurstPhase.Hidden && beamRenderer) beamRenderer.enabled = false;
        }

        void Update()
        {
            if (phase == BurstPhase.Hidden || !beamRenderer) return;

            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case BurstPhase.Focused:
                    UpdateFocused();
                    if (phaseTime >= focusedDuration) ChangePhase(BurstPhase.Expanding);
                    break;
                case BurstPhase.Expanding:
                    UpdateExpanding();
                    if (phaseTime >= expansionDuration) ChangePhase(BurstPhase.Holding);
                    break;
                case BurstPhase.Holding:
                    ApplyBeam(maximumDiameter, expandedEmission, 1f, 0f, .04f);
                    if (phaseTime >= holdDuration) ChangePhase(BurstPhase.Tearing);
                    break;
                case BurstPhase.Tearing:
                    UpdateTearing();
                    if (phaseTime >= tearDuration) HideImmediately();
                    break;
            }
        }

        public void TriggerBurst()
        {
            if (!beamRenderer) return;
            phase = BurstPhase.Focused;
            phaseTime = 0f;
            beamRenderer.enabled = true;
            ApplyBeam(thinDiameter, focusedEmission, 1f, 0f, 0f);
        }

        public void TearOff()
        {
            if (phase == BurstPhase.Hidden || !beamRenderer) return;
            ChangePhase(BurstPhase.Tearing);
        }

        void UpdateFocused()
        {
            float t = Mathf.Clamp01(phaseTime / focusedDuration);
            float ignition = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 5f));
            float tightPulse = 1f + Mathf.Sin(t * Mathf.PI * 12f) * .035f;
            ApplyBeam(thinDiameter * tightPulse, focusedEmission, ignition, 0f, .02f);
        }

        void UpdateExpanding()
        {
            float t = Mathf.Clamp01(phaseTime / expansionDuration);
            float explosive = 1f - Mathf.Pow(1f - t, 5f);
            float diameter = Mathf.Lerp(thinDiameter, maximumDiameter, explosive);
            float flash = Mathf.Lerp(focusedEmission * 1.45f, expandedEmission, explosive);
            ApplyBeam(diameter, flash, 1f, 0f, .03f);
        }

        void UpdateTearing()
        {
            float t = Mathf.Clamp01(phaseTime / tearDuration);
            float steppedTear = Mathf.Floor(Mathf.Pow(t, .72f) * 9f) / 8f;
            float shudder = 1f + Mathf.Sin(phaseTime * 115f) * .025f * (1f - t);
            float flicker = Mathf.Clamp01(.18f + Mathf.Sin(phaseTime * 173f) * .12f + t * .45f);
            float alpha = 1f - Mathf.SmoothStep(.72f, 1f, t);
            ApplyBeam(maximumDiameter * shudder, expandedEmission * Mathf.Lerp(1.2f, .45f, t),
                alpha, steppedTear, flicker);
        }

        void ApplyBeam(float diameter, float emission, float alpha, float tearProgress, float flicker)
        {
            transform.localScale = new Vector3(diameter, beamHeightScale, diameter);
            beamRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintColorId, tintColor);
            propertyBlock.SetFloat(EmissionId, emission);
            propertyBlock.SetFloat(AlphaId, alpha);
            propertyBlock.SetFloat(TearProgressId, tearProgress);
            propertyBlock.SetFloat(FlickerId, flicker);
            beamRenderer.SetPropertyBlock(propertyBlock);
        }

        void ChangePhase(BurstPhase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        void HideImmediately()
        {
            phase = BurstPhase.Hidden;
            phaseTime = 0f;
            if (beamRenderer) beamRenderer.enabled = false;
        }

        void OnDisable() => HideImmediately();
    }
}
