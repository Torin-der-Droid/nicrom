using UnityEditor;
using UnityEngine;

namespace PromptMotion.Shared.Setup
{
    /// <summary>
    /// Detects whether Newtonsoft.Json is present in the project and prompts the
    /// user to install it when it's missing. The Generator/Library/Tests asmdefs
    /// gate their compilation on the <c>PROMPTMOTION_HAS_NEWTONSOFT</c> symbol
    /// (defined by their <c>versionDefines</c> when <c>com.unity.nuget.newtonsoft-json</c>
    /// is installed), so this checker lives in the Shared assembly — the one
    /// place that compiles regardless of the dependency state.
    /// </summary>
    [InitializeOnLoad]
    internal static class NewtonsoftDependencyChecker
    {
        internal const string PackageId = "com.unity.nuget.newtonsoft-json";

        // EditorPrefs is per-machine, so namespace the key with the project
        // path to keep the "don't ask again" choice project-scoped.
        private static string SkipPrefKey =>
            "PromptMotion.Newtonsoft.DontAskAgain." + Application.dataPath.GetHashCode();

        private const string SessionPromptedKey = "PromptMotion.Newtonsoft.PromptedThisSession";

        static NewtonsoftDependencyChecker()
        {
            // Defer to the next idle tick — Unity is still loading scripts at
            // static-ctor time and EditorWindow.ShowUtility silently no-ops
            // while the editor is mid-compile or mid-asset-import.
            EditorApplication.update += WaitForEditorReady;
        }

        private static void WaitForEditorReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitForEditorReady;
            Check();
        }

        private static void Check()
        {
            if (IsNewtonsoftAvailable())
            {
                Debug.Log("[PromptMotion] Newtonsoft.Json detected — no setup prompt needed.");
                return;
            }
            if (EditorPrefs.GetBool(SkipPrefKey, false))
            {
                Debug.Log("[PromptMotion] Newtonsoft.Json is missing, but the setup prompt was dismissed for this project. Run Window → PromptMotion → Check Newtonsoft.Json Dependency to re-open it.");
                return;
            }
            if (SessionState.GetBool(SessionPromptedKey, false))
            {
                Debug.Log("[PromptMotion] Newtonsoft.Json setup prompt already shown this session.");
                return;
            }

            Debug.Log("[PromptMotion] Newtonsoft.Json is missing — opening setup window.");
            NewtonsoftMissingWindow.ShowWindow();
            SessionState.SetBool(SessionPromptedKey, true);
        }

        /// <summary>True when the com.unity.nuget.newtonsoft-json package is installed.</summary>
        /// <remarks>
        /// We deliberately do NOT probe for the JsonConvert type via reflection: several
        /// Unity packages (Collaborate / Plastic, some render pipeline tooling, the editor
        /// itself in recent versions) ship internalised copies of Newtonsoft that satisfy
        /// the reflection check without satisfying the asmdef reference. The popup needs
        /// to fire on exactly the same condition that gates Generator/Library compilation
        /// — i.e. whether the version-define symbol is set — so we use it directly.
        /// </remarks>
        internal static bool IsNewtonsoftAvailable()
        {
#if PROMPTMOTION_HAS_NEWTONSOFT
            return true;
#else
            return false;
#endif
        }

        internal static void SetSkipPreference(bool skip)
        {
            if (skip) EditorPrefs.SetBool(SkipPrefKey, true);
            else EditorPrefs.DeleteKey(SkipPrefKey);
        }

        /// <summary>Manual entry point — exposed under the PromptMotion menu so
        /// users who skipped the popup can re-open it.</summary>
        [MenuItem("Window/PromptMotion/Check Newtonsoft.Json Dependency")]
        private static void ManualCheck()
        {
            if (IsNewtonsoftAvailable())
            {
                EditorUtility.DisplayDialog(
                    "PromptMotion",
                    "Newtonsoft.Json is installed — PromptMotion is good to go.",
                    "OK");
                return;
            }
            SetSkipPreference(false);
            NewtonsoftMissingWindow.ShowWindow();
        }
    }
}
