using UnityEngine;
using UnityEngine.SceneManagement;

namespace NicromFPS
{
    public static class DungeonPlacement
    {
        public static bool TryFindGate(Vector3 entry, CharacterController body, Scene scene,
            out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            for (int ring = 0; ring < 3; ring++)
            for (int direction = 0; direction < 8; direction++)
            {
                Vector3 forward = Quaternion.Euler(0, direction * 45, 0) * Vector3.forward;
                if (!TryFind(entry + forward * (2.5f + ring), body, false, scene, out var gate)) continue;
                Vector3 delta = gate - entry;
                if (delta.magnitude < 2.2f || Mathf.Abs(delta.y) > 0.2f) continue;
                var facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(delta, Vector3.up));
                // Actual frame: 2.8m wide, 3.15m tall and 0.3m deep, plus small clearance.
                if (Physics.CheckBox(gate + Vector3.up * 1.65f, new Vector3(1.42f, 1.57f, 0.17f),
                    facing, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                float radius = body.radius * Mathf.Max(body.transform.lossyScale.x, body.transform.lossyScale.z);
                float height = Mathf.Max(body.height * body.transform.lossyScale.y, radius * 2);
                Vector3 foot = entry + body.transform.TransformVector(body.center) - Vector3.up * (height * 0.5f);
                bool blocked = false;
                foreach (var hit in Physics.CapsuleCastAll(foot + Vector3.up * radius,
                    foot + Vector3.up * (height - radius), radius, delta.normalized, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (!hit.collider.transform.IsChildOf(body.transform)) { blocked = true; break; }
                if (blocked) continue;
                if (!TryFind(entry + delta * 0.5f, body, false, scene, out var middle)
                    || Mathf.Abs(middle.y - entry.y) > 0.2f) continue;
                position = gate;
                rotation = facing;
                return true;
            }
            return false;
        }

        public static bool TryFind(Vector3 near, CharacterController body, bool terrainOnly, Scene interiorScene, out Vector3 result)
        {
            result = default;
            if (!body) return false;
            for (int i = 0; i < 25; i++)
            {
                Vector3 p = near + new Vector3(i == 0 ? 0 : ((i - 1) % 5 - 2) * 0.6f, 0,
                    i == 0 ? 0 : ((i - 1) / 5 - 2) * 0.6f);
                float above = terrainOnly ? 80 : 1;
                if (!Physics.Raycast(p + Vector3.up * above, Vector3.down, out var hit, above + 2,
                    Physics.DefaultRaycastLayers & ~(1 << 8) & ~(1 << 12), QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.transform.IsChildOf(body.transform) || hit.normal.y < 0.8f) continue;
                if (terrainOnly && !(hit.collider is TerrainCollider)) continue;
                if (!terrainOnly && hit.collider.gameObject.scene != interiorScene) continue;
                Vector3 foot = hit.point + Vector3.up * 0.06f;
                float radius = body.radius * Mathf.Max(body.transform.lossyScale.x, body.transform.lossyScale.z);
                float height = Mathf.Max(body.height * body.transform.lossyScale.y, radius * 2);
                bool blocked = false;
                foreach (var obstacle in Physics.OverlapCapsule(foot + Vector3.up * radius,
                    foot + Vector3.up * (height - radius), radius + 0.02f,
                    Physics.DefaultRaycastLayers & ~(1 << 12), QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(body.transform)) { blocked = true; break; }
                if (blocked) continue;
                result = foot - body.transform.TransformVector(body.center) + Vector3.up * (height * 0.5f);
                return true;
            }
            return false;
        }

    }
}
