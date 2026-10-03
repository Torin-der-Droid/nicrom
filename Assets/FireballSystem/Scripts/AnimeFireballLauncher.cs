using UnityEngine;

namespace FireballSystem
{
    public sealed class AnimeFireballLauncher : MonoBehaviour
    {
        [SerializeField] AnimeFireballProjectile fireballPrefab = null;
        [SerializeField] Transform muzzle = null;
        [SerializeField, Min(.1f)] float projectileSpeed = 16f;
        [SerializeField, Min(0f)] float cooldown = .2f;

        float nextFireTime;

        public AnimeFireballProjectile Fire()
        {
            Transform source = muzzle ? muzzle : transform;
            return Fire(source.forward);
        }

        public AnimeFireballProjectile Fire(Vector3 direction)
        {
            if (!fireballPrefab)
            {
                Debug.LogWarning("AnimeFireballLauncher: Das Feuerball-Prefab fehlt.", this);
                return null;
            }
            if (Time.time < nextFireTime) return null;
            Transform source = muzzle ? muzzle : transform;
            var fireball = Instantiate(fireballPrefab, source.position, Quaternion.LookRotation(direction));
            fireball.Speed = projectileSpeed;
            fireball.Launch(direction);
            nextFireTime = Time.time + cooldown;
            return fireball;
        }
    }
}
