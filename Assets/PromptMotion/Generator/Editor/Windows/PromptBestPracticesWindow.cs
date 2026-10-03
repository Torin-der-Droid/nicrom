using PromptMotion.Shared.Utilities;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Windows
{
    public class PromptBestPracticesWindow : EditorWindow
    {
        private const string ContactEmail = "ragendom@gmail.com";

        private Vector2 _scroll;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _bulletStyle;
        private GUIStyle _emailStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialized;

        [MenuItem("Window/PromptMotion/Prompt Best Practices", priority = 200)]
        public static void Open()
        {
            var w = GetWindow<PromptBestPracticesWindow>(
                utility: false,
                title: "PromptMotion Prompting Tips");
            w.minSize = new Vector2(560, 600);
            w.maxSize = new Vector2(800, 1200);
            w.Show();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            _subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _subtitleStyle.normal.textColor = Theme.MutedText;

            _headingStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                wordWrap = true
            };
            _headingStyle.margin.top = 4;
            _headingStyle.margin.bottom = 4;

            _bodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true
            };

            _mutedStyle = new GUIStyle(_bodyStyle);
            _mutedStyle.normal.textColor = Theme.MutedText;

            _bulletStyle = new GUIStyle(_bodyStyle);
            _bulletStyle.margin.left = 16;

            _emailStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _emailStyle.normal.textColor = new Color(0.3f, 0.55f, 1f);
            _emailStyle.focused.textColor = new Color(0.3f, 0.55f, 1f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 32
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Space(16);
            GUILayout.Label("PromptMotion Prompting Tips", _titleStyle);
            GUILayout.Space(4);
            GUILayout.Label("Write better prompts, get better animations.", _subtitleStyle);
            GUILayout.Space(12);

            DrawSeparator();
            DrawSectionHowPromptsWork();
            DrawSeparator();
            DrawSectionGoodVsBad();
            DrawSeparator();
            DrawSectionSequencing();
            DrawSeparator();
            DrawSectionHandlesWell();
            DrawSeparator();
            DrawSectionLimitations();
            DrawSeparator();
            DrawSectionTips();
            DrawSeparator();
            DrawFooter();

            GUILayout.Space(16);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSeparator()
        {
            GUILayout.Space(10);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, Theme.Divider);
            GUILayout.Space(10);
        }

        // === Section 1 ===

        private void DrawSectionHowPromptsWork()
        {
            GUILayout.Label("How prompts work", _headingStyle);
            GUILayout.Space(4);

            GUILayout.Label(
                "PromptMotion uses NVIDIA's Kimodo motion model, which expects prompts " +
                "that describe what a person is doing.",
                _bodyStyle);
            GUILayout.Space(6);

            GUILayout.Label(
                "You don't need to start your prompts with \"a person\" — PromptMotion " +
                "automatically adds this prefix for you when it isn't already there. " +
                "Just describe the motion.",
                _bodyStyle);
            GUILayout.Space(6);

            GUILayout.Label("Good results come from prompts that:", _bodyStyle);
            GUILayout.Space(2);
            GUILayout.Label("•  describe a clear physical action", _bulletStyle);
            GUILayout.Label(
                "•  specify direction or which limb when it matters (right hand, left foot)",
                _bulletStyle);
            GUILayout.Label(
                "•  include how the motion ends when relevant (lands on both feet, returns to standing)",
                _bulletStyle);
            GUILayout.Label("•  use natural sentences, not keyword lists", _bulletStyle);
        }

        // === Section 2 ===

        private static readonly string[][] Examples =
        {
            new[] { "backflip", "performs a clean standing backflip, lands on both feet" },
            new[] { "wave", "waves with the right hand at shoulder height" },
            new[] { "walk and wave", "walks forward, then stops and waves" },
            new[] { "kick", "performs a roundhouse kick with the right leg" },
            new[] { "jump spin", "jumps high and spins 360 degrees in the air" },
            new[] { "dance", "dances energetically with arm flourishes and footwork" },
            new[] { "tired walk", "walks slowly with shoulders slumped, looking tired" },
            new[] { "zombie", "shambles forward like a zombie, arms outstretched" },
            new[] { "throw", "throws a ball overhand with the right arm" },
        };

        private void DrawSectionGoodVsBad()
        {
            GUILayout.Label("Good vs bad prompt examples", _headingStyle);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.38f));
            GUILayout.Label("LESS EFFECTIVE", EditorStyles.miniBoldLabel);
            GUILayout.Space(4);
            foreach (var pair in Examples)
                GUILayout.Label(pair[0], _mutedStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(16);

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));
            GUILayout.Label("MORE EFFECTIVE", EditorStyles.miniBoldLabel);
            GUILayout.Space(4);
            foreach (var pair in Examples)
                GUILayout.Label(pair[1], _bodyStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        // === Section 3 ===

        private void DrawSectionSequencing()
        {
            GUILayout.Label("Sequencing multiple actions", _headingStyle);
            GUILayout.Space(4);

            GUILayout.Label(
                "Kimodo can handle prompts that combine actions either in sequence or " +
                "simultaneously. Use these connector words:",
                _bodyStyle);
            GUILayout.Space(6);

            GUILayout.Label(
                "\"then\"  —  for sequential actions:\n" +
                "    \"walks forward, then stops and waves\"",
                _bulletStyle);
            GUILayout.Space(4);
            GUILayout.Label(
                "\"while\"  —  for simultaneous actions:\n" +
                "    \"walks forward while waving\"",
                _bulletStyle);
            GUILayout.Space(4);
            GUILayout.Label(
                "\", and\"  —  also works for sequential:\n" +
                "    \"crouches down, picks up an object, and stands up\"",
                _bulletStyle);
            GUILayout.Space(6);

            GUILayout.Label(
                "For complex multi-step motions, longer durations (3–4 seconds) give " +
                "better results.",
                _bodyStyle);
        }

        // === Section 4 ===

        private void DrawSectionHandlesWell()
        {
            GUILayout.Label("What Kimodo handles well", _headingStyle);
            GUILayout.Space(4);

            DrawCategory("Locomotion",
                "walks, runs, sprints, jumps, climbs, crawls, sneaks, marches");
            DrawCategory("Idles & poses",
                "standing, sitting, leaning, crouching, sleeping, kneeling");
            DrawCategory("Combat",
                "punches, kicks, sword swings, dodges, blocks, parries");
            DrawCategory("Gestures",
                "waves, bows, points, applauds, shrugs, salutes");
            DrawCategory("Reactions",
                "surprised, hurt, knocked back, falling, dodging");
            DrawCategory("Sports & athletics",
                "backflips, cartwheels, dancing, yoga, stretching");
            DrawCategory("Daily activities",
                "sitting down, picking up objects, drinking, throwing, opening doors");
            DrawCategory("Style & emotion",
                "tired, excited, angry, sad, drunk, sneaky, proud");
        }

        private void DrawCategory(string title, string items)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(title, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(items, _bodyStyle);
            EditorGUILayout.EndHorizontal();
        }

        // === Section 5 ===

        private void DrawSectionLimitations()
        {
            GUILayout.Label("Limitations to know", _headingStyle);
            GUILayout.Space(4);

            GUILayout.Label(
                "PromptMotion produces full-body motion only. The following are NOT generated:",
                _bodyStyle);
            GUILayout.Space(4);

            GUILayout.Label(
                "•  Finger animation — hands stay in their default rest pose",
                _bulletStyle);
            GUILayout.Label(
                "•  Facial expressions — no face or blendshape data is produced",
                _bulletStyle);
            GUILayout.Label(
                "•  Lip sync or speech-driven animation",
                _bulletStyle);
            GUILayout.Label(
                "•  Object interactions where the object's geometry matters (e.g. opening a " +
                "specific drawer requires manual placement of the hand on that drawer)",
                _bulletStyle);
            GUILayout.Space(6);

            GUILayout.Label(
                "For projects needing detailed hand or face animation, layer those in " +
                "separately using Animation Rigging, Final IK, or a face-tracking solution.",
                _bodyStyle);
        }

        // === Section 6 ===

        private void DrawSectionTips()
        {
            GUILayout.Label("Tips for repeatable results", _headingStyle);
            GUILayout.Space(4);

            GUILayout.Label(
                "•  The same prompt can produce slightly different motions each time. To " +
                "get the same result twice, use the Seed field (set it to any integer instead " +
                "of leaving it empty).",
                _bulletStyle);
            GUILayout.Space(4);

            GUILayout.Label(
                "•  If a generation looks wrong, try regenerating with the same prompt — " +
                "the diffusion process is sampled, and a second attempt may give better results.",
                _bulletStyle);
            GUILayout.Space(4);

            GUILayout.Label(
                "•  If a prompt consistently produces poor output, try rewording it with " +
                "more specific body part references (left hand, right foot, both arms) instead " +
                "of generic verbs.",
                _bulletStyle);
            GUILayout.Space(4);

            GUILayout.Label(
                "•  Identical prompts return cached results without using a generation " +
                "credit. If you want a different motion for the same prompt, change a word or " +
                "set a different seed.",
                _bulletStyle);
        }

        // === Footer ===

        private void DrawFooter()
        {
            GUILayout.Space(4);

            DrawCenteredButton("Open Documentation", 260, () =>
            {
                var doc = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/PromptMotion/Documentation/Documentation.html");
                if (doc != null)
                {
                    EditorGUIUtility.PingObject(doc);
                    Selection.activeObject = doc;
                }
            });

            GUILayout.Space(12);

            var contactStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            contactStyle.normal.textColor = Theme.MutedText;
            GUILayout.Label("Need help? Reach out:", contactStyle);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.SelectableLabel(
                ContactEmail, _emailStyle,
                GUILayout.Width(260), GUILayout.Height(28));
            if (GUILayout.Button("Copy", GUILayout.Width(50), GUILayout.Height(28)))
                EditorGUIUtility.systemCopyBuffer = ContactEmail;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawCenteredButton(string label, float width, System.Action onClick)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(label, _buttonStyle, GUILayout.Width(width)))
                onClick?.Invoke();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
