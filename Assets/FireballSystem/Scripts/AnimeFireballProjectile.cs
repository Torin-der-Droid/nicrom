using UnityEngine;
using UnityEngine.VFX;
using NicromFPS;

namespace FireballSystem
{
    public sealed class AnimeFireballProjectile : MonoBehaviour
    {
        [SerializeField, Min(.1f)] float speed = 16f;
        [SerializeField, Min(.1f)] float maximumLifetime = 5f;
        [SerializeField] GameObject impactPrefab = null;
        [SerializeField] Transform visualRoot = null;
        [SerializeField, Min(0f)] float visualSpin = 240f;

        Vector3 direction = Vector3.forward;
        float age;
        bool launched;
        bool exploding;
        Transform ignoredRoot;
        VisualEffect[] visualEffects;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(.1f, value);
        }

        public void Launch(Vector3 flightDirection)
        {
            direction = flightDirection.sqrMagnitude > .001f ? flightDirection.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            age = 0f;
            launched = true;
        }

        public void IgnoreRoot(Transform root) => ignoredRoot = root;

        void OnEnable()
        {
            age = 0f;
            exploding = false;
            visualEffects = GetComponentsInChildren<VisualEffect>(true);
            foreach (var effect in visualEffects)
            {
                if (!effect || !effect.visualEffectAsset) continue;
                effect.pause = false;
                effect.Reinit();
                effect.Play();
            }
        }

        void Update()
        {
            if (!launched || exploding) return;

            float step = speed * Time.deltaTime;
            transform.position += direction * step;
            if (visualRoot) visualRoot.Rotate(0f, 0f, visualSpin * Time.deltaTime, Space.Self);

            age += Time.deltaTime;
            if (age >= maximumLifetime) Explode();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!launched || exploding || other.transform.IsChildOf(transform)) return;
            if (ignoredRoot && other.transform.IsChildOf(ignoredRoot)) return;
            GoblinEnemy goblin = other.GetComponentInParent<GoblinEnemy>();
            if (goblin && !goblin.IsDead) goblin.Damage(300f, gameObject);
            Explode();
        }

        public void Explode()
        {
            if (exploding) return;
            exploding = true;
            if (impactPrefab) Instantiate(impactPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
