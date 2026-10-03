using Akila.FPSFramework;
using UnityEngine;

namespace NicromFPS
{
    /// <summary>A glowing mana pickup that is attracted to the active player.</summary>
    public sealed class ManaOrb : MonoBehaviour
    {
        const float MagnetRadius = 2.5f;
        const float CollectDistance = .35f;
        const float DropTime = .65f;
        const float OrbDiameter = .1f;
        const float HoverCenterHeight = .1f;
        static Material glowMaterial;
        static int activeLights;

        int value;
        float age;
        float phase;
        Vector3 start;
        Vector3 rest;
        bool attracted;
        bool ownsLight;

        public static void Drop(Vector3 position, float maximumHealth)
        {
            int count = Mathf.Max(1, Mathf.FloorToInt(maximumHealth / 20f));
            for (int i = 0; i < count; i++)
                Spawn(position + Vector3.up * .65f, 10, i, count);
        }

        static void Spawn(Vector3 position, int amount, int index, int count)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = $"Mana-Kugel +{amount}";
            orb.layer = LayerMask.NameToLayer("Ignore Raycast");
            orb.transform.position = position;
            orb.transform.localScale = Vector3.one * OrbDiameter;
            if (orb.TryGetComponent(out Collider collider)) collider.enabled = false;
            orb.GetComponent<Renderer>().sharedMaterial = GlowMaterial();
            float angle = (index / (float)Mathf.Max(1, count)) * Mathf.PI * 2f + Random.Range(-.25f, .25f);
            Vector3 spread = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Random.Range(.18f, .45f);
            var pickup = orb.AddComponent<ManaOrb>();
            pickup.value = amount;
            pickup.rest = FindGround(position + spread);
            pickup.start = pickup.rest;
            orb.transform.position = pickup.rest;
            pickup.phase = Random.value * Mathf.PI * 2f;
            if (activeLights < 24)
            {
                var light = orb.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(.05f, .55f, 1f);
                light.intensity = 1.35f;
                light.range = 1.6f;
                light.shadows = LightShadows.None;
                pickup.ownsLight = true;
                activeLights++;
            }
        }

        static Vector3 FindGround(Vector3 position)
        {
            int mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player", "FPS Object", "Water");
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 1.5f, Vector3.down,
                8f, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                // Nearby living goblins are obstacles, not a resting surface.
                if (hit.collider.GetComponentInParent<GoblinEnemy>()) continue;
                // A 10 cm sphere with its centre 10 cm above the surface leaves
                // roughly 5 cm of air between its underside and the ground.
                return hit.point + Vector3.up * HoverCenterHeight;
            }
            return position;
        }

        void Update()
        {
            if (FPSFrameworkCore.IsPaused) return;
            age += Time.deltaTime;
            ManaWallet wallet = ManaWallet.Active;
            if (!wallet || !wallet.CollectionPoint) { AnimateDrop(); return; }

            Vector3 target = wallet.CollectionPoint.position + Vector3.up * .25f;
            float distance = Vector3.Distance(transform.position, target);
            if (!attracted && age >= .25f && distance <= MagnetRadius) attracted = true;
            if (!attracted) { AnimateDrop(); return; }

            float speed = 10f + Mathf.Max(0, MagnetRadius - distance) * 1.8f;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(.055f, OrbDiameter, Mathf.Clamp01(distance / 1.5f));
            if (distance <= CollectDistance)
            {
                wallet.Add(value);
                Destroy(gameObject);
            }
        }

        void AnimateDrop()
        {
            if (age < DropTime)
            {
                float t = Mathf.Clamp01(age / DropTime);
                transform.position = Vector3.Lerp(start, rest, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .75f);
            }
            else
            {
                transform.position = rest + Vector3.up * (Mathf.Sin(Time.time * 3f + phase) * .015f);
                transform.Rotate(0, 100f * Time.deltaTime, 0, Space.World);
            }
        }

        static Material GlowMaterial()
        {
            if (glowMaterial) return glowMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            glowMaterial = new Material(shader) { name = "Mana Glow (Runtime)" };
            Color blue = new Color(.02f, .35f, 1f, 1f);
            glowMaterial.color = blue;
            if (glowMaterial.HasProperty("_BaseColor")) glowMaterial.SetColor("_BaseColor", blue);
            glowMaterial.EnableKeyword("_EMISSION");
            glowMaterial.SetColor("_EmissionColor", new Color(.05f, 1.2f, 4f, 1f));
            return glowMaterial;
        }

        void OnDestroy()
        {
            if (ownsLight) activeLights = Mathf.Max(0, activeLights - 1);
        }
    }
}
