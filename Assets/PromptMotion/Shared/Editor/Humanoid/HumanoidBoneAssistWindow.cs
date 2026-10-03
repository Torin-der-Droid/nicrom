using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Shared.Humanoid
{
    /// <summary>
    /// Fallback UI shown when <see cref="HumanoidAvatarAutoConfigurator.TryAutoConfigure"/>
    /// fails on an exotic rig. Two ways to assign each Humanoid slot:
    ///
    ///   * <b>Drag-and-drop</b> the Transform from the Hierarchy onto the
    ///     per-slot <see cref="EditorGUILayout.ObjectField"/>. Easiest when the
    ///     user has the rig open in the scene.
    ///   * <b>Dropdown</b> with a flat list of every Transform in the rig,
    ///     showing each bone's full hierarchy path. Easiest when the rig is in
    ///     a prefab or otherwise not unfolded in the scene view.
    ///
    /// Both controls write to the same backing store; updating one updates the
    /// other. Required slots that aren't yet assigned are surfaced in a red
    /// box at the top of the window so the user can find them at a glance.
    /// </summary>
    public sealed class HumanoidBoneAssistWindow : EditorWindow
    {
        private GameObject _character;
        private List<Transform> _allTransforms;
        private string[] _transformLabels;       // "<none>" + each transform's hierarchy path
        private Dictionary<string, Transform> _assignment; // slot -> assigned Transform (single source of truth)
        private HashSet<string> _requiredSlots;
        private string _statusMessage;
        private MessageType _statusType = MessageType.Info;
        private Vector2 _scroll;

        // Callback signature: (ok, builtAvatar). On user cancel: (false, null).
        // On successful Build Avatar click: (true, <avatar>). On window close
        // without a button click: (false, null) — the popup treats unloads as
        // cancellation so the caller doesn't hang on a stale handle.
        private System.Action<bool, Avatar> _onComplete;
        private bool _completed;

        /// <summary>
        /// Open the popup as a NON-modal utility window so the user can drag
        /// transforms from the Hierarchy into the per-slot object fields.
        /// <paramref name="onComplete"/> is invoked exactly once — either when
        /// the user clicks Build (success or fail), Cancel, or closes the
        /// window via the title-bar X.
        /// </summary>
        public static void ShowFor(GameObject character, System.Action<bool, Avatar> onComplete)
        {
            var win = CreateInstance<HumanoidBoneAssistWindow>();
            win.titleContent = new GUIContent("PromptMotion — Help map bones");
            win.minSize = new Vector2(620, 520);
            win._onComplete = onComplete;
            win.Initialize(character);
            // ShowUtility (vs ShowModalUtility) keeps the rest of the Editor
            // interactive, which is the whole point — the user needs to drag
            // from the Hierarchy or click into a Prefab to grab a Transform.
            win.ShowUtility();
        }

        private void Initialize(GameObject character)
        {
            _character = character;
            _allTransforms = new List<Transform>(
                character.transform.GetComponentsInChildren<Transform>(includeInactive: true));
            _transformLabels = new string[_allTransforms.Count + 1];
            _transformLabels[0] = "<none>";
            for (int i = 0; i < _allTransforms.Count; i++)
            {
                _transformLabels[i + 1] = BuildHierarchyLabel(_allTransforms[i], character.transform);
            }

            var (autoMapped, _, _) = HumanoidAvatarAutoConfigurator.AnalyzeRig(character);
            _assignment = new Dictionary<string, Transform>();
            foreach (var slot in HumanoidAvatarAutoConfigurator.AllHumanBoneSlots)
            {
                _assignment[slot] = autoMapped.TryGetValue(slot, out var t) ? t : null;
            }
            _requiredSlots = new HashSet<string>(HumanoidAvatarAutoConfigurator.RequiredHumanBoneSlots);
        }

        // Build a "Hips/Spine/Spine1/Shoulder.L" style label so users can tell
        // apart same-name bones in different chains (e.g. finger bones).
        private static string BuildHierarchyLabel(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null && cur != root.parent; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private int IndexOfTransform(Transform t)
        {
            if (t == null) return 0;
            for (int i = 0; i < _allTransforms.Count; i++)
                if (_allTransforms[i] == t) return i + 1;
            return 0;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            EditorGUILayout.LabelField($"Help us map '{_character?.name}' to a Humanoid avatar", title);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "PromptMotion couldn't recognise every bone in this rig automatically. " +
                "Two ways to assign each slot:\n" +
                "  • Drag-and-drop the bone from the Hierarchy onto the object field.\n" +
                "  • Or pick it from the dropdown (full hierarchy path is shown so " +
                "duplicate-named bones in different chains stay distinguishable).\n\n" +
                "Required slots are marked •. Optional slots can stay empty.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // --- Red box: required slots that still need assignment -----------
            var missingRequired = _requiredSlots
                .Where(s => _assignment[s] == null)
                .ToList();

            if (missingRequired.Count > 0)
            {
                DrawSectionHeader(
                    $"{missingRequired.Count} required slot{(missingRequired.Count == 1 ? "" : "s")} not yet assigned",
                    new Color(0.9f, 0.30f, 0.30f));

                // Tint the built-in helpBox background red. Doing it this way
                // (instead of drawing a rect over the layout) keeps the widgets
                // on top of the tint, which is what IMGUI's draw order needs.
                var savedBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1.0f, 0.35f, 0.35f, 1f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = savedBg;
                foreach (var slot in missingRequired)
                {
                    DrawSlotRow(slot, isMissingRequired: true);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(8);
            }

            // --- Full slot list: required + optional, including already-assigned ---
            DrawSectionHeader("All Humanoid slots", new Color(0.55f, 0.55f, 0.55f));
            foreach (var slot in HumanoidAvatarAutoConfigurator.AllHumanBoneSlots)
            {
                // Skip slots we already drew in the red box, to avoid duplicate edits
                // for the same backing field.
                if (_assignment[slot] == null && _requiredSlots.Contains(slot)) continue;
                DrawSlotRow(slot, isMissingRequired: false);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(6);

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
                EditorGUILayout.Space(4);
            }

            using (new EditorGUI.DisabledScope(missingRequired.Count > 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Build Avatar", GUILayout.Height(28)))
                    {
                        TryBuild();
                    }
                    if (GUILayout.Button("Cancel", GUILayout.Height(28), GUILayout.Width(120)))
                    {
                        Complete(success: false, avatar: null);
                        Close();
                    }
                }
            }
            if (missingRequired.Count > 0)
            {
                EditorGUILayout.LabelField(
                    $"Required slot(s) still missing: {string.Join(", ", missingRequired)}",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(6);
        }

        private void DrawSlotRow(string slot, bool isMissingRequired)
        {
            var required = _requiredSlots.Contains(slot);
            var label = (required ? "• " : "   ") + slot;

            using (new EditorGUILayout.HorizontalScope())
            {
                // Slot label
                var labelStyle = new GUIStyle(EditorStyles.label);
                if (isMissingRequired)
                {
                    labelStyle.normal.textColor = new Color(1f, 0.5f, 0.5f);
                    labelStyle.fontStyle = FontStyle.Bold;
                }
                EditorGUILayout.LabelField(label, labelStyle, GUILayout.Width(170));

                // Drag-and-drop object field. Native Unity widget — accepts any
                // GameObject dragged from the Hierarchy and we resolve to its
                // Transform. Restricting type to Transform also works, but
                // GameObject is more forgiving when the user drags the rig root.
                var current = _assignment[slot];
                var dropped = (Transform)EditorGUILayout.ObjectField(
                    current,
                    typeof(Transform),
                    allowSceneObjects: true,
                    GUILayout.MinWidth(160));

                // Only accept transforms that actually belong to this character —
                // otherwise the assignment is nonsense and BuildHumanAvatar will
                // reject it anyway.
                if (dropped != current)
                {
                    if (dropped == null || _allTransforms.Contains(dropped))
                    {
                        _assignment[slot] = dropped;
                    }
                    else
                    {
                        _statusType = MessageType.Warning;
                        _statusMessage = $"'{dropped.name}' isn't part of '{_character.name}'. " +
                            "Drag a Transform from inside this character's hierarchy.";
                    }
                }

                // Dropdown — alternative to drag-and-drop. Synced with the
                // ObjectField via _assignment.
                var currentIdx = IndexOfTransform(_assignment[slot]);
                var newIdx = EditorGUILayout.Popup(currentIdx, _transformLabels, GUILayout.MinWidth(160));
                if (newIdx != currentIdx)
                {
                    _assignment[slot] = newIdx <= 0 ? null : _allTransforms[newIdx - 1];
                }
            }
        }

        private static void DrawSectionHeader(string text, Color underlineColor)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField(text, style);
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, underlineColor);
            EditorGUILayout.Space(4);
        }

        private void TryBuild()
        {
            var mapping = new Dictionary<string, Transform>();
            foreach (var kv in _assignment)
            {
                if (kv.Value == null) continue;
                mapping[kv.Key] = kv.Value;
            }

            if (!HumanoidAvatarAutoConfigurator.TryBuildFromExplicitMapping(
                    _character, mapping, out var avatar, out var log))
            {
                _statusType = MessageType.Error;
                _statusMessage = "Build failed. " + log;
                Repaint();
                return;
            }

            Complete(success: true, avatar: avatar);
            Close();
        }

        private void Complete(bool success, Avatar avatar)
        {
            // Guard against double-invoke: e.g. Build → Close → OnDestroy.
            if (_completed) return;
            _completed = true;
            try { _onComplete?.Invoke(success, avatar); }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnDestroy()
        {
            // User closed the window via the title-bar X without a button.
            // Treat as cancellation so the caller doesn't hang waiting for
            // a callback that will never come.
            Complete(success: false, avatar: null);
        }
    }
}
