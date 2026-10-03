using Akila.FPSFramework;
using UnityEngine;
using UnityEngine.UI;

namespace NicromFPS
{
    public sealed class GoblinMinimapMarker : MonoBehaviour
    {
        private Minimap map;
        private RectTransform icon;
        private GoblinEnemy enemy;

        private void Awake() => enemy = GetComponent<GoblinEnemy>();

        private void LateUpdate()
        {
            if (enemy && enemy.EncounterPaused) { if (icon) icon.gameObject.SetActive(false); return; }
            if (!enemy || enemy.IsDead) { RemoveIcon(); return; }
            var current = Minimap.Instance;
            if (!current || !current.iconsContainer || !current.mapRect) { RemoveIcon(); return; }
            if (map != current || !icon)
            {
                RemoveIcon();
                map = current;
                // The prefab's Shadow is a later sibling and covers this container.
                // Keep icons above it while retaining the circular parent's mask.
                map.iconsContainer.SetAsLastSibling();
                var marker = new GameObject("Goblin", typeof(RectTransform), typeof(RawImage));
                marker.layer = map.iconsContainer.gameObject.layer;
                icon = marker.GetComponent<RectTransform>();
                icon.SetParent(map.iconsContainer, false);
                icon.anchorMin = icon.anchorMax = icon.pivot = Vector2.one * 0.5f;
                icon.sizeDelta = Vector2.one * 10;
                var image = marker.GetComponent<RawImage>();
                image.texture = Texture2D.whiteTexture;
                image.color = new Color(1, 0.12f, 0.08f);
                image.raycastTarget = false;
            }
            icon.gameObject.SetActive(map.player && map.Visible);
            if (!map.player) return;
            Vector3 delta = transform.position - map.player.position;
            Vector2 halfSize = map.mapRect.rect.size * 0.5f;
            Vector2 position = new Vector2(delta.x * halfSize.x, delta.z * halfSize.y) * (map.zoom / 50f);
            // Keep distant enemies visible at the edge of the circular minimap.
            icon.anchoredPosition = Vector2.ClampMagnitude(position, Mathf.Max(0, Mathf.Min(halfSize.x, halfSize.y) - 6));
        }

        private void RemoveIcon() { if (icon) Destroy(icon.gameObject); icon = null; }
        private void OnDisable() => RemoveIcon();
    }
}
