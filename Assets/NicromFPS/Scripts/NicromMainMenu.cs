using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NicromFPS
{
    /// <summary>Build-start menu for the Nicrom FPS game.</summary>
    public sealed class NicromMainMenu : MonoBehaviour
    {
        [SerializeField] string gameplayScene = "Nicrom FPS";

        TMP_Text statusText;
        Button playButton;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsureEventSystem();
            BuildMenu();
            if (EventSystem.current != null && playButton != null)
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
                StartGame();
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            var input = eventSystem.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
        }

        void BuildMenu()
        {
            var canvasObject = new GameObject("Main Menu UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Stretch the backdrop across the whole canvas. A 1x1 anchored panel
            // makes every child appear to share almost the same screen position.
            var backdrop = CreatePanel(canvasObject.transform, "Backdrop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.06f, 1f);

            var accent = CreatePanel(backdrop.transform, "Accent", new Vector2(0.12f, 0f), new Vector2(0.125f, 1f), Vector2.zero, Vector2.zero);
            accent.GetComponent<Image>().color = new Color(0.9f, 0.43f, 0.12f, 1f);

            CreateText(backdrop.transform, "Title", "NICROM FPS", 62, FontStyle.Bold,
                new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), new Vector2(0f, 0f), new Vector2(900f, 110f), TextAlignmentOptions.Center,
                new Color(0.96f, 0.97f, 0.98f, 1f));
            CreateText(backdrop.transform, "Subtitle", "ÜBERLEBE DIE WELLEN", 22, FontStyle.Normal,
                new Vector2(0.5f, 0.59f), new Vector2(0.5f, 0.59f), Vector2.zero, new Vector2(700f, 48f), TextAlignmentOptions.Center,
                new Color(0.65f, 0.72f, 0.77f, 1f));

            playButton = CreateButton(backdrop.transform, "Play Button", "SPIELEN", new Vector2(0.5f, 0.43f), StartGame, true);
            CreateButton(backdrop.transform, "Quit Button", "BEENDEN", new Vector2(0.5f, 0.33f), QuitGame, false);
            statusText = CreateText(backdrop.transform, "Status", "P drücken oder SPIELEN wählen", 17, FontStyle.Normal,
                new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(900f, 42f), TextAlignmentOptions.Center,
                new Color(0.57f, 0.64f, 0.68f, 1f));
        }

        static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        TMP_Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta,
            TextAlignmentOptions alignment, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.fontSize = size;
            text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        Button CreateButton(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction action, bool primary)
        {
            var rect = CreatePanel(parent, name, anchor, anchor, Vector2.zero, new Vector2(360f, 68f));
            var image = rect.GetComponent<Image>();
            image.color = primary ? new Color(0.83f, 0.31f, 0.08f, 1f) : new Color(0.1f, 0.14f, 0.17f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var colors = button.colors;
            colors.highlightedColor = primary ? new Color(1f, 0.43f, 0.12f, 1f) : new Color(0.18f, 0.25f, 0.29f, 1f);
            colors.pressedColor = new Color(0.55f, 0.2f, 0.07f, 1f);
            button.colors = colors;
            button.onClick.AddListener(action);
            CreateText(rect, "Label", label, 23, FontStyle.Bold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center, Color.white);
            return button;
        }

        void StartGame()
        {
            if (playButton == null || !playButton.interactable)
                return;
            playButton.interactable = false;
            statusText.text = "Welt wird geladen …";
            StartCoroutine(LoadGame());
        }

        IEnumerator LoadGame()
        {
            var load = SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
            if (load == null)
            {
                statusText.text = $"Szene „{gameplayScene}“ konnte nicht geladen werden.";
                playButton.interactable = true;
                yield break;
            }
            while (!load.isDone)
            {
                statusText.text = $"Welt wird geladen … {Mathf.Clamp01(load.progress / 0.9f):P0}";
                yield return null;
            }
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
