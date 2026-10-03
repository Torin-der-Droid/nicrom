using UnityEngine;

namespace FireballSystem
{
    public sealed class AnimeFireballImpact : MonoBehaviour
    {
        [SerializeField, Min(.1f)] float lifetime = 2.2f;
        [SerializeField] Light impactLight = null;
        [SerializeField, Min(.01f)] float lightFadeDuration = .28f;

        float age;
        float initialLightIntensity;

        void Awake()
        {
            if (impactLight) initialLightIntensity = impactLight.intensity;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (impactLight)
            {
                float t = Mathf.Clamp01(age / lightFadeDuration);
                impactLight.intensity = initialLightIntensity * (1f - t * t);
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
