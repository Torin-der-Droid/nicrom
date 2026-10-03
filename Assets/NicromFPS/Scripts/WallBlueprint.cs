using UnityEngine;
using UnityEngine.Rendering;

namespace NicromFPS
{
    public sealed class WallBlueprint : MonoBehaviour
    {
        public const float BlockSize = 0.55f;
        private readonly WallBuildProgress progress = new WallBuildProgress();
        private readonly GameObject[] row = new GameObject[WallBuildProgress.Width];
        private Material solidMaterial, ghostMaterial;
        private const int CollisionMask = Physics.DefaultRaycastLayers & ~(1 << 12);

        public void Initialize(Material solid, Material ghost)
        {
            solidMaterial = solid;
            ghostMaterial = ghost;
            CreateNextRow();
        }

        public string GetPrompt(ResourceInventory inventory, Transform player)
        {
            string status = $"Schicht {progress.Layer + 1}: {progress.Column}/5";
            if (Vector3.Distance(player.position, row[progress.Column].transform.position) > 3.5f)
                return status + " · Näher an den nächsten Würfel gehen";
            if (inventory.Count(CubePickup.ItemId) == 0) return status + " · Würfel sammeln";
            if (!NextSpaceIsClear()) return status + " · Nächster Bauplatz blockiert";
            return "[F] Würfel hinzufügen · " + status;
        }

        public bool TrySupply(ResourceInventory inventory, Transform player)
        {
            GameObject next = row[progress.Column];
            if (Vector3.Distance(player.position, next.transform.position) > 3.5f) return false;
            Physics.SyncTransforms();
            if (!progress.TrySupply(inventory, NextSpaceIsClear())) return false;
            next.name = "Fertiger Mauerwürfel";
            var renderer = next.GetComponent<Renderer>();
            renderer.sharedMaterial = solidMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            next.GetComponent<BoxCollider>().isTrigger = false;
            next.AddComponent<PlacedCube>();
            if (progress.Column == 0) CreateNextRow();
            Physics.SyncTransforms();
            return true;
        }

        private bool NextSpaceIsClear()
        {
            var next = row[progress.Column];
            Collider self = next.GetComponent<Collider>();
            foreach (Collider obstacle in Physics.OverlapBox(next.transform.position,
                Vector3.one * (BlockSize * 0.5f - 0.006f), transform.rotation,
                CollisionMask, QueryTriggerInteraction.Collide))
                if (obstacle != self && (!obstacle.isTrigger || obstacle.GetComponentInParent<WallBlueprint>())) return false;
            return true;
        }

        private void CreateNextRow()
        {
            for (int i = 0; i < row.Length; i++)
            {
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Bauplan Schicht {progress.Layer + 1} / Würfel {i + 1}";
                block.transform.SetParent(transform, false);
                block.transform.localPosition = new Vector3((i - 2) * BlockSize, progress.Layer * BlockSize, 0);
                block.transform.localScale = Vector3.one * BlockSize;
                // Raycasts can select the plan, but the player can walk through unfinished blocks.
                block.GetComponent<BoxCollider>().isTrigger = true;
                var renderer = block.GetComponent<Renderer>();
                renderer.sharedMaterial = ghostMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                row[i] = block;
            }
        }
    }
}
