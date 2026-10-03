using System;
using PromptMotion.Bootstrap;
using PromptMotion.Shared.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PromptMotion.Windows
{
    /// <summary>
    /// First-run welcome popup. Opened once per machine by
    /// <see cref="WelcomeBootstrap"/> and never reachable from a menu — by
    /// design. Customers see it once, dismiss it, and move on.
    /// </summary>
    internal class WelcomePopup : EditorWindow
    {
        private const string DocumentationAssetPath =
            "Assets/PromptMotion/Documentation/Documentation.html";
        private const string ReviewUrl = "https://u3d.as/409N"; // PromptMotion Lite listing
        private const string SupportEmail = "ragendom@gmail.com";

        private Toggle _dontShowAgainToggle;

        public new static void Show()
        {
            var window = CreateInstance<WelcomePopup>();
            window.titleContent = new GUIContent("Welcome to PromptMotion Lite");
            var size = new Vector2(480, 540);
            window.minSize = size;
            window.maxSize = size;
            window.ShowUtility();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;

            BuildHeader(root);
            BuildIntro(root);

            BuildCard(
                root,
                heading: "Documentation",
                body: "Read the full guide — quickstart, FAQ, troubleshooting, and more.",
                buttonLabel: "Open Documentation",
                onClick: OnDocumentationClicked,
                background: Theme.HeaderBackground);

            BuildCard(
                root,
                heading: "Enjoying PromptMotion?",
                body: "Reviews help indie publishers grow. If you find this useful, a quick rating means a lot.",
                buttonLabel: "Leave a Review",
                onClick: OnReviewClicked,
                background: Theme.BannerBackground);

            BuildSupportCard(root);

            BuildFooter(root);
        }

        private static void BuildHeader(VisualElement root)
        {
            var heading = new Label("Welcome to PromptMotion Lite");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 18;
            heading.style.marginBottom = 4;
            root.Add(heading);
        }

        private static void BuildIntro(VisualElement root)
        {
            var intro = new Label(
                "AI-generated 3D character animations for Unity — free to try. Activate with your email to unlock 5 lifetime generations, then type a prompt, point at a humanoid character, and get a ready-to-use AnimationClip in about a minute.");
            intro.style.whiteSpace = WhiteSpace.Normal;
            intro.style.color = Theme.MutedText;
            intro.style.marginBottom = 14;
            root.Add(intro);
        }

        private static void BuildCard(
            VisualElement root,
            string heading,
            string body,
            string buttonLabel,
            Action onClick,
            Color background)
        {
            var card = new VisualElement();
            card.style.backgroundColor = background;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = Theme.Divider;
            card.style.borderBottomColor = Theme.Divider;
            card.style.borderLeftColor = Theme.Divider;
            card.style.borderRightColor = Theme.Divider;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.marginBottom = 10;
            root.Add(card);

            var title = new Label(heading);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13;
            title.style.marginBottom = 2;
            card.Add(title);

            var bodyLabel = new Label(body);
            bodyLabel.style.whiteSpace = WhiteSpace.Normal;
            bodyLabel.style.fontSize = 11;
            bodyLabel.style.marginBottom = 8;
            card.Add(bodyLabel);

            var button = new Button(() => onClick?.Invoke()) { text = buttonLabel };
            button.style.alignSelf = Align.FlexEnd;
            card.Add(button);
        }

        private static void BuildSupportCard(VisualElement root)
        {
            var card = new VisualElement();
            card.style.backgroundColor = Theme.HeaderBackground;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = Theme.Divider;
            card.style.borderBottomColor = Theme.Divider;
            card.style.borderLeftColor = Theme.Divider;
            card.style.borderRightColor = Theme.Divider;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.marginBottom = 10;
            root.Add(card);

            var title = new Label("Need help?");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13;
            title.style.marginBottom = 2;
            card.Add(title);

            var body = new Label(
                "Email us with your activation email, diagnostic_id, Unity version, and OS. " +
                "Subject prefix: [PromptMotion Lite]. Response within 24-48h on weekdays.");
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.fontSize = 11;
            body.style.marginBottom = 6;
            card.Add(body);

            // Read-only TextField gives the customer a selectable, Ctrl+C-able
            // email — no button needed. Centered + bold so it reads as the
            // important thing in the card.
            var emailField = new TextField { value = SupportEmail, isReadOnly = true };
            emailField.style.alignSelf = Align.Center;
            emailField.style.minWidth = 240;
            emailField.style.fontSize = 13;
            emailField.style.unityFontStyleAndWeight = FontStyle.Bold;
            emailField.style.marginTop = 2;
            card.Add(emailField);
        }

        private void BuildFooter(VisualElement root)
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            root.Add(spacer);

            _dontShowAgainToggle = new Toggle("Don't show this again") { value = true };
            _dontShowAgainToggle.style.marginBottom = 8;
            root.Add(_dontShowAgainToggle);

            var getStarted = new Button(OnGetStartedClicked) { text = "Get Started" };
            getStarted.style.alignSelf = Align.FlexEnd;
            getStarted.style.height = 28;
            getStarted.style.minWidth = 140;
            getStarted.style.backgroundColor = Theme.StatusSuccess;
            getStarted.style.color = Theme.PillText;
            getStarted.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(getStarted);
        }

        private void OnDocumentationClicked()
        {
            var fullPath = System.IO.Path.GetFullPath(DocumentationAssetPath);
            if (!System.IO.File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog(
                    "PromptMotion",
                    $"Could not find the documentation at:\n{fullPath}",
                    "OK");
                return;
            }
            Application.OpenURL("file://" + fullPath);
        }

        private void OnReviewClicked()
        {
            Application.OpenURL(ReviewUrl);
        }

        private void OnGetStartedClicked()
        {
            PersistDismissalIfChecked();
            Close();
            PromptMotionWindow.OpenGenerator();
        }

        private void OnDestroy()
        {
            PersistDismissalIfChecked();
        }

        private void PersistDismissalIfChecked()
        {
            if (_dontShowAgainToggle == null) return;
            WriteDismissalFlag(_dontShowAgainToggle.value);
        }

        /// <summary>
        /// Test-visible seam: persistence is exercised by unit tests without
        /// instantiating an EditorWindow (UI tests are flaky). Writes the
        /// dismissed pref when <paramref name="dontShowAgain"/> is true; clears
        /// it otherwise so a subsequent unchecked dismissal cannot leave a
        /// stale flag behind.
        /// </summary>
        internal static void WriteDismissalFlag(bool dontShowAgain)
        {
            if (dontShowAgain)
                EditorPrefs.SetBool(WelcomeBootstrap.DismissedKey, true);
            else
                EditorPrefs.DeleteKey(WelcomeBootstrap.DismissedKey);
        }
    }
}
