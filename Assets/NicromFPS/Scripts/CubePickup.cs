using UnityEngine;

namespace NicromFPS
{
    public sealed class CubePickup : MonoBehaviour
    {
        public const string ItemId = "gray_cube";
        private bool collected;

        public bool TryCollect(ResourceInventory inventory)
        {
            if (collected || inventory == null || !inventory.TryAdd(ItemId, 1)) return false;
            collected = true;
            // Disable immediately, so a second interaction in the same frame cannot collect twice.
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
