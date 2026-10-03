using UnityEngine;

namespace EnergyEffects
{
    public sealed class KamehamehaEffect : MonoBehaviour
    {
        [Header("Timing")]
        [Min(0)] public float chargeDuration = 0f;
        [Min(0.1f)] public float beamDuration = 2.3f;
        public bool autoRepeat;
        [Min(0)] public float repeatDelay = 1.5f;
        [Header("Beam")]
        [Min(0.1f)] public float beamLength = 18;
        [Min(0.05f)] public float beamWidth = 0.55f;
        public LayerMask hitLayers = ~0;
        public Transform target;
        [Header("Particle Systems")]
        public ParticleSystem core;
        public ParticleSystem aura;
        public ParticleSystem charge;
        public ParticleSystem streaks;
        public ParticleSystem impact;
        public ParticleSystem shockwave;

        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[512];
        float elapsed;
        float nextRepeat;
        float sparkAccumulator;
        bool active;
        bool hitBefore;
        bool sustained;
        public bool IsPlaying => active;

        void OnEnable() { nextRepeat = Time.time + 0.5f; }
        void OnDisable() { StopEffect(); }

        [ContextMenu("Fire")]
        public void Fire() => Fire(false);

        public void Fire(bool keepAlive)
        {
            if (!core || !aura || !charge || !streaks || !impact || !shockwave) return;
            StopEffect();
            sustained = keepAlive;
            elapsed = 0;
            active = true;
        }

        public void StopEffect()
        {
            active = false;
            sustained = false;
            hitBefore = false;
            sparkAccumulator = 0;
            foreach (var ps in new[] { core, aura, charge, streaks, impact, shockwave })
                if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            nextRepeat = Time.time + repeatDelay;
        }

        void Update()
        {
            if (!active)
            {
                if (autoRepeat && Time.time >= nextRepeat) Fire();
                return;
            }
            elapsed += Time.deltaTime;
            Vector3 origin = transform.position;
            Vector3 direction = target ? (target.position - origin).normalized : transform.forward;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            Quaternion rotation = Quaternion.LookRotation(direction);
            if (elapsed < chargeDuration)
            {
                // Only inward spirals: deliberately no charging sphere.
                int count = 44;
                for (int i = 0; i < count; i++)
                {
                    float p = Mathf.Repeat(elapsed * 1.5f + i / (float)count, 1);
                    float angle = i * 2.39996f + elapsed * 5;
                    float radius = Mathf.Lerp(1.7f, 0.08f, p);
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.5f * (1 - p));
                    SetParticle(i, origin + rotation * offset, 0.035f + p * 0.05f,
                        new Color(0.08f, 0.65f, 1, Mathf.Sin(p * Mathf.PI)));
                }
                charge.SetParticles(particles, count);
                return;
            }
            charge.Clear();
            float age = elapsed - chargeDuration;
            if (!sustained && age > beamDuration + 0.35f) { StopEffect(); return; }
            float endStrength = sustained ? 1 : Mathf.Clamp01((beamDuration + 0.35f - age) / 0.35f);
            float strength = Mathf.Clamp01(age / 0.10f) * endStrength;
            float maxLength = target ? Mathf.Min(beamLength, Vector3.Distance(origin, target.position)) : beamLength;
            bool hit = Physics.Raycast(origin, direction, out RaycastHit info, maxLength, hitLayers, QueryTriggerInteraction.Ignore);
            float distance = hit ? info.distance : maxLength;
            float visibleLength = Mathf.Min(distance, age * 90);
            int samples = Mathf.Clamp(Mathf.CeilToInt(visibleLength / (beamWidth * 0.22f)), 2, particles.Length);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                float taper = Mathf.Lerp(0.65f, 1, Mathf.Sin(t * Mathf.PI));
                SetParticle(i, origin + direction * (t * visibleLength), beamWidth * taper * strength,
                    new Color(0.65f, 0.9f, 1, 0.8f * strength));
            }
            core.SetParticles(particles, samples);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                float wave = Mathf.Sin(t * 42 - age * 22);
                Vector3 offset = rotation * new Vector3(Mathf.Cos(t * 32 + age * 9), wave, 0) * beamWidth * 0.12f;
                SetParticle(i, origin + direction * (t * visibleLength) + offset,
                    beamWidth * (1.9f + wave * 0.15f) * strength, new Color(0.015f, 0.25f, 1, 0.14f * strength));
            }
            aura.SetParticles(particles, samples);
            for (int i = 0; i < 28; i++)
            {
                float t = Mathf.Repeat(i / 28f + age * 2, 1);
                float angle = i * 2.39996f + age * 3;
                Vector3 offset = rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * beamWidth * 0.7f;
                SetParticle(i, origin + direction * (t * visibleLength) + offset, beamWidth * 0.15f * strength,
                    new Color(0.15f, 0.75f, 1, strength));
            }
            streaks.SetParticles(particles, 28);
            bool reached = hit && visibleLength >= distance - 0.01f;
            if (reached)
            {
                Vector3 point = info.point + info.normal * 0.08f;
                if (!hitBefore)
                {
                    shockwave.transform.SetPositionAndRotation(point, Quaternion.LookRotation(info.normal));
                    shockwave.Play();
                    shockwave.Emit(1);
                    impact.Play();
                    for (int i = 0; i < 45; i++) EmitSpark(point, info.normal, strength);
                }
                sparkAccumulator += Time.deltaTime * 95 * strength;
                while (sparkAccumulator >= 1) { EmitSpark(point, info.normal, strength); sparkAccumulator--; }
            }
            hitBefore = reached;
        }

        void EmitSpark(Vector3 position, Vector3 normal, float strength)
        {
            Vector3 velocity = Random.onUnitSphere;
            if (Vector3.Dot(velocity, normal) < 0) velocity = -velocity;
            impact.Emit(new ParticleSystem.EmitParams
            {
                position = position, velocity = (velocity * Random.Range(2f, 7f) + normal) * strength,
                startSize = Random.Range(0.035f, 0.14f), startLifetime = Random.Range(0.2f, 0.65f),
                startColor = new Color(0.15f, 0.7f, 1, 1)
            }, 1);
        }

        void SetParticle(int index, Vector3 position, float size, Color color)
        {
            particles[index] = new ParticleSystem.Particle
            {
                position = position, startSize = size, startColor = color,
                startLifetime = 1, remainingLifetime = 1
            };
        }
    }
}
