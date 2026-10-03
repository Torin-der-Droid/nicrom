using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace PromptMotion.Shared.Setup
{
    /// <summary>
    /// Popup shown on editor load when <c>com.unity.nuget.newtonsoft-json</c> is
    /// missing. Offers a one-click install via <see cref="Client.Add(string)"/>,
    /// a fallback to open the Package Manager window, and a "don't ask again"
    /// toggle persisted to <see cref="EditorPrefs"/>.
    /// </summary>
    internal sealed class NewtonsoftMissingWindow : EditorWindow
    {
        private AddRequest _addRequest;
        private string _statusMessage;
        private MessageType _statusType = MessageType.Info;
        private bool _dontAskAgain;

        public static void ShowWindow()
        {
            // GetWindow(utility:true, ...) already calls ShowUtility internally
            // and focuses the window; calling ShowUtility again on the returned
            // instance logs a "window already shown" warning, so don't.
            var w = GetWindow<NewtonsoftMissingWindow>(
                utility: true,
                title: "PromptMotion — Setup Required",
                focus: true);
            w.minSize = new Vector2(480, 260);
            w.maxSize = new Vector2(640, 380);
            w.Focus();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(12);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("Newtonsoft.Json is required", titleStyle);

            EditorGUILayout.Space(6);

            EditorGUILayout.HelpBox(
                "PromptMotion uses Newtonsoft.Json to talk to the generation backend and " +
                "to cache the local clip library.\n\n" +
                "The Unity package com.unity.nuget.newtonsoft-json isn't installed in this " +
                "project yet — install it to enable PromptMotion.",
                MessageType.Warning);

            EditorGUILayout.Space(8);

            var installing = _addRequest != null && !_addRequest.IsCompleted;

            using (new EditorGUI.DisabledScope(installing))
            {
                if (GUILayout.Button(
                        installing ? "Installing…" : "Install Newtonsoft.Json… (recommended)",
                        GUILayout.Height(30)))
                {
                    if (ConfirmInstall()) BeginInstall();
                }

                if (GUILayout.Button("Open Package Manager", GUILayout.Height(22)))
                {
                    UnityEditor.PackageManager.UI.Window.Open(NewtonsoftDependencyChecker.PackageId);
                }

                if (GUILayout.Button("I already installed it — re-check", GUILayout.Height(22)))
                {
                    Recheck();
                }
            }

            EditorGUILayout.Space(8);

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                _dontAskAgain = EditorGUILayout.ToggleLeft(
                    "Don't show again for this project",
                    _dontAskAgain,
                    GUILayout.Width(260));

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(installing))
                {
                    if (GUILayout.Button("Close", GUILayout.Width(80)))
                    {
                        NewtonsoftDependencyChecker.SetSkipPreference(_dontAskAgain);
                        Close();
                    }
                }
            }

            EditorGUILayout.Space(6);
        }

        // Explicit pre-install confirmation. Unity Asset Store policy requires
        // that any operation which fetches an external package or modifies the
        // project manifest be confirmed by the user with full disclosure of
        // what's being added and where it comes from — a one-click "Install"
        // button isn't enough.
        private bool ConfirmInstall()
        {
            return EditorUtility.DisplayDialog(
                "Install Newtonsoft.Json?",
                "PromptMotion will add the following package to this project via Unity Package Manager:\n\n" +
                "  • Package ID: " + NewtonsoftDependencyChecker.PackageId + "\n" +
                "  • Source: Unity registry (registry.packages.unity.com)\n" +
                "  • Maintainer: Unity Technologies\n\n" +
                "This will:\n" +
                "  • Add an entry to Packages/manifest.json\n" +
                "  • Download the package to your project's Library cache\n" +
                "  • Trigger a Unity recompile when the download finishes\n\n" +
                "No files outside this project are modified, and PromptMotion never " +
                "sends data anywhere during this step. You can also install the same " +
                "package manually from Window → Package Manager.",
                "Install",
                "Cancel");
        }

        private void BeginInstall()
        {
            _statusType = MessageType.Info;
            _statusMessage = "Adding com.unity.nuget.newtonsoft-json — Unity will recompile when it finishes.";
            _addRequest = Client.Add(NewtonsoftDependencyChecker.PackageId);
            EditorApplication.update += MonitorInstall;
            Repaint();
        }

        private void MonitorInstall()
        {
            if (_addRequest == null)
            {
                EditorApplication.update -= MonitorInstall;
                return;
            }
            if (!_addRequest.IsCompleted) return;

            EditorApplication.update -= MonitorInstall;

            if (_addRequest.Status == StatusCode.Success)
            {
                var pkg = _addRequest.Result;
                _statusType = MessageType.Info;
                _statusMessage = $"Installed {pkg.displayName} {pkg.version}. " +
                                 "Unity will finish recompiling, then PromptMotion is ready to use.";
                NewtonsoftDependencyChecker.SetSkipPreference(false);
                // Auto-close after a beat so the user sees the success message.
                EditorApplication.delayCall += () => { if (this != null) Close(); };
            }
            else
            {
                var err = _addRequest.Error != null ? _addRequest.Error.message : "Unknown error.";
                _statusType = MessageType.Error;
                _statusMessage = "Install failed: " + err +
                                 "\nYou can install it manually from Window → Package Manager " +
                                 "(search for \"Newtonsoft Json\").";
            }

            _addRequest = null;
            Repaint();
        }

        private void Recheck()
        {
            if (NewtonsoftDependencyChecker.IsNewtonsoftAvailable())
            {
                _statusType = MessageType.Info;
                _statusMessage = "Newtonsoft.Json detected — closing.";
                NewtonsoftDependencyChecker.SetSkipPreference(false);
                EditorApplication.delayCall += () => { if (this != null) Close(); };
            }
            else
            {
                _statusType = MessageType.Warning;
                _statusMessage = "Still not detected. If you just installed it, wait for Unity " +
                                 "to finish recompiling and try again.";
            }
            Repaint();
        }

        private void OnDestroy()
        {
            EditorApplication.update -= MonitorInstall;
        }
    }
}
