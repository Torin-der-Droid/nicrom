using UnityEngine;

namespace NicromFPS
{
    // Runs for the initial player and for every player created by the FPS respawn manager.
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class NicromTerrainSpawn : MonoBehaviour
    {
        private void Awake()
        {
            if (!PlaceOnGround())
                Debug.LogError("Nicrom FPS: No clear terrain spawn found near the spawn point.", this);
        }

        public bool PlaceOnGround()
        {
            var controller = GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            Physics.SyncTransforms();
            Vector3 origin = transform.position;
            float radius = controller.radius + controller.skinWidth;
            float halfHeight = Mathf.Max(controller.height * 0.5f, radius);
            int mask = Physics.DefaultRaycastLayers & ~(1 << 4) & ~(1 << 8) & ~(1 << 12);

            // Search outwards if a tree, rock, steep slope or water blocks the initial spot.
            for (int ring = 0; ring <= 12; ring++)
            {
                int count = ring == 0 ? 1 : ring * 8;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2 / count;
                    Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring * 2;
                    foreach (Terrain terrain in Terrain.activeTerrains)
                    {
                        if (terrain.gameObject.scene != gameObject.scene) continue;
                        Vector3 local = candidate - terrain.transform.position;
                        Vector3 size = terrain.terrainData.size;
                        if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
                        var ground = terrain.GetComponent<TerrainCollider>();
                        if (!ground || !ground.enabled) continue;
                        var ray = new Ray(new Vector3(candidate.x, terrain.transform.position.y + size.y + 10, candidate.z), Vector3.down);
                        if (!ground.Raycast(ray, out RaycastHit hit, size.y + 20)) continue;
                        if (Vector3.Angle(hit.normal, Vector3.up) > 35) continue;
                        if (Physics.Raycast(ray, out RaycastHit water, size.y + 20, 1 << 4, QueryTriggerInteraction.Collide)
                            && water.point.y > hit.point.y) continue;
                        Vector3 center = hit.point + Vector3.up * (halfHeight + 0.15f);
                        Vector3 offset = Vector3.up * (halfHeight - radius);
                        if (Physics.CheckCapsule(center - offset, center + offset, radius, mask, QueryTriggerInteraction.Ignore)) continue;
                        transform.position = center - controller.center;
                        controller.enabled = wasEnabled;
                        return true;
                    }
                }
            }
            controller.enabled = wasEnabled;
            return false;
        }
    }
}
