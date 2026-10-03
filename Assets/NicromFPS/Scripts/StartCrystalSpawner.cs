using UnityEngine;

namespace NicromFPS
{
    /// <summary>Places the blue crystal model on the ground beside the initial player spawn.</summary>
    public sealed class StartCrystalSpawner : MonoBehaviour
    {
        [SerializeField] Object crystalModel;
        [SerializeField] Texture2D baseColor;
        [SerializeField] Texture2D metallic;
        [SerializeField] Texture2D normal;
        [SerializeField] Texture2D roughness;
        [SerializeField] Texture2D emission;
        [SerializeField] Vector3 localOffset = new Vector3(2.5f, 0, 4.5f);
        [SerializeField, Min(.25f)] float targetHeight = 2.2f;

        void Start()
        {
            if (!crystalModel || GameObject.Find("Blauer Startkristall")) return;

            Vector3 candidate = transform.TransformPoint(localOffset);
            Vector3 ground = candidate;
            int mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player", "FPS Object", "Water");
            if (Physics.Raycast(candidate + Vector3.up * 25f, Vector3.down, out RaycastHit hit,
                    60f, mask, QueryTriggerInteraction.Ignore))
                ground = hit.point;

            Object spawnedAsset = Instantiate(crystalModel);
            GameObject crystal = spawnedAsset as GameObject;
            if (!crystal)
            {
                if (spawnedAsset) Destroy(spawnedAsset);
                Debug.LogError("Der Startkristall-Verweis zeigt nicht auf das FBX-Hauptobjekt.", this);
                return;
            }
            crystal.name = "Blauer Startkristall";
            crystal.transform.SetPositionAndRotation(ground, Quaternion.Euler(0, transform.eulerAngles.y + 25f, 0));

            // The purchased FBX contains every colour variant in one hierarchy.
            // Keep only the requested blue mesh before navigation collects scene geometry.
            foreach (Renderer item in crystal.GetComponentsInChildren<Renderer>(true))
                if (item.name != "BlueCrystal") item.gameObject.SetActive(false);

            Renderer[] renderers = crystal.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > .001f)
                crystal.transform.localScale *= targetHeight / bounds.size.y;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            crystal.transform.position += Vector3.up * (ground.y - bounds.min.y);

            Material material = CreateBlueMaterial();
            foreach (Renderer item in renderers) item.sharedMaterial = material;

            // A box gives the crystal collision without feeding an unreadable FBX mesh
            // into the runtime NavMesh builder used by the goblin encounter.
            Renderer collisionRenderer = renderers[0];
            var collider = collisionRenderer.gameObject.AddComponent<BoxCollider>();
            collider.center = collisionRenderer.localBounds.center;
            collider.size = collisionRenderer.localBounds.size;

            var lightObject = new GameObject("Blaues Kristalllicht", typeof(Light));
            lightObject.transform.SetParent(crystal.transform, false);
            lightObject.transform.localPosition = Vector3.up * .7f;
            Light glow = lightObject.GetComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(.08f, .45f, 1f);
            glow.intensity = 2.2f;
            glow.range = 5f;
            glow.shadows = LightShadows.None;
        }

        Material CreateBlueMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Blauer Kristall (Runtime)" };
            Color tint = new Color(.12f, .48f, 1f, 1f);
            material.color = tint;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (baseColor)
            {
                material.mainTexture = baseColor;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
            }
            if (metallic && material.HasProperty("_MetallicGlossMap"))
                material.SetTexture("_MetallicGlossMap", metallic);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .72f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .68f);
            if (normal && material.HasProperty("_BumpMap"))
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normal);
            }
            // Keep the supplied roughness referenced for shaders that expose it directly.
            if (roughness && material.HasProperty("_RoughnessMap")) material.SetTexture("_RoughnessMap", roughness);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", new Color(.08f, .7f, 3.5f, 1f));
            }
            return material;
        }
    }
}
