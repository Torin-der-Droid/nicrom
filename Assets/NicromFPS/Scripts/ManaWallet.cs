using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NicromFPS
{
    /// <summary>Unlimited player mana storage and its HUD display.</summary>
    public sealed class ManaWallet : MonoBehaviour
    {
        public static ManaWallet Active { get; private set; }
        public long Mana { get; private set; }
        public Transform CollectionPoint { get; private set; }

        TMP_Text manaText;
        float flashUntil;

        void Awake()
        {
            // Mana is collected at the player root near the feet, not at the
            // first-person camera where pickups would fly into the player's face.
            CollectionPoint = transform;
            BuildHud();
            Refresh();
        }

        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        void Update()
        {
            if (manaText)
                manaText.color = Time.unscaledTime < flashUntil
                    ? new Color(.72f, .96f, 1f) : new Color(.22f, .75f, 1f);
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Mana = Mana > long.MaxValue - amount ? long.MaxValue : Mana + amount;
            flashUntil = Time.unscaledTime + .25f;
            Refresh();
        }

        public int Spend(int amount)
        {
            if (amount <= 0 || Mana <= 0) return 0;
            int spent = (int)Mathf.Min(amount, Mana);
            Mana -= spent;
            Refresh();
            return spent;
        }

        void Refresh()
        {
            if (manaText) manaText.text = $"<size=18>MANA</size>  •  {Mana:N0}";
        }

        void BuildHud()
        {
            var root = new GameObject("Mana HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var panel = new GameObject("Mana Anzeige", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(28, -28);
            rect.sizeDelta = new Vector2(310, 70);
            var image = panel.GetComponent<Image>();
            image.color = new Color(.025f, .09f, .15f, .92f);
            image.raycastTarget = false;

            var label = new GameObject("Mana", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(panel.transform, false);
            var textRect = label.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20, 6);
            textRect.offsetMax = new Vector2(-20, -6);
            manaText = label.GetComponent<TextMeshProUGUI>();
            manaText.fontSize = 28;
            manaText.fontStyle = FontStyles.Bold;
            manaText.alignment = TextAlignmentOptions.MidlineLeft;
            manaText.raycastTarget = false;
        }
    }
}
