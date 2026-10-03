using PromptMotion.Windows;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Bootstrap
{
    /// <summary>
    /// First-run trigger for <see cref="WelcomePopup"/>. Schedules a one-shot
    /// check on the next Editor tick after a fresh domain reload. Not menu-
    /// accessible by design — once the customer dismisses with "Don't show
    /// again", the popup never appears unless they manually clear the
    /// EditorPref key.
    /// </summary>
    internal static class WelcomeBootstrap
    {
        // Lite-namespaced so dismissing the Lite welcome doesn't also suppress
        // the Standard/Pro welcome (or vice-versa) on the same machine.
        internal const string DismissedKey = "PromptMotion.Lite.Welcome.Dismissed";
        private const float MinSecondsSinceStartup = 2.0f;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += TryShowWelcome;
        }

        private static void TryShowWelcome()
        {
            if (!ShouldShow()) return;
            WelcomePopup.Show();
        }

        internal static bool ShouldShow()
        {
            if (Application.isBatchMode) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (EditorApplication.isCompiling) return false;
            if (EditorApplication.isUpdating) return false;
            if (Time.realtimeSinceStartup < MinSecondsSinceStartup) return false;
            if (EditorPrefs.GetBool(DismissedKey, false)) return false;
            return true;
        }

        internal static void ResetDismissalForTest()
        {
            EditorPrefs.DeleteKey(DismissedKey);
        }
    }
}
