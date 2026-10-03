using UnityEditor;
using UnityEngine;

namespace PromptMotion.Shared.Utilities
{
    /// <summary>
    /// Skin-aware color palette for the PromptMotion editor UI. Build brief §Phase 8
    /// prohibits hardcoded colors — use this or UI Toolkit USS variables. Each
    /// accessor branches on <see cref="EditorGUIUtility.isProSkin"/> so the window
    /// looks correct in both the dark (Professional) and light (Personal) editor
    /// skins.
    /// </summary>
    /// <remarks>
    /// Colors are re-evaluated on each access — if you cache a <see cref="Color"/>
    /// into an element's style at CreateGUI time and the user later toggles skins,
    /// the cached value won't refresh. That's acceptable for Phase 8 (live skin
    /// switch isn't in scope); users see correct colors on the next window open.
    /// </remarks>
    public static class Theme
    {
        private static bool IsPro => EditorGUIUtility.isProSkin;

        /// <summary>Thin separator line between window sections.</summary>
        public static Color Divider => IsPro
            ? new Color(0.4f, 0.4f, 0.4f, 0.5f)
            : new Color(0.6f, 0.6f, 0.6f, 0.5f);

        /// <summary>Background fill behind the preview viewport.</summary>
        public static Color PreviewBackground => IsPro
            ? new Color(0.12f, 0.12f, 0.12f, 1f)
            : new Color(0.75f, 0.75f, 0.75f, 1f);

        /// <summary>Placeholder / helper text shown in italics.</summary>
        public static Color MutedText => IsPro
            ? new Color(0.70f, 0.70f, 0.70f)
            : new Color(0.40f, 0.40f, 0.40f);

        /// <summary>Hint text inside dialogs (slightly brighter than MutedText on dark).</summary>
        public static Color HintText => IsPro
            ? new Color(0.72f, 0.72f, 0.72f)
            : new Color(0.35f, 0.35f, 0.35f);

        // --- Status label colors shown under the Generate button. ---

        /// <summary>Green text used for success messages and "ready" states.</summary>
        public static Color StatusSuccess => IsPro
            ? new Color(0.35f, 0.75f, 0.35f)
            : new Color(0.20f, 0.55f, 0.20f);

        /// <summary>Amber text used for non-blocking warnings.</summary>
        public static Color StatusWarning => IsPro
            ? new Color(0.90f, 0.70f, 0.20f)
            : new Color(0.70f, 0.50f, 0.10f);

        /// <summary>Red text used for errors and blocking validation failures.</summary>
        public static Color StatusError => IsPro
            ? new Color(0.85f, 0.30f, 0.30f)
            : new Color(0.70f, 0.20f, 0.20f);

        /// <summary>Neutral text color for informational status lines.</summary>
        public static Color StatusNeutral => IsPro
            ? new Color(0.80f, 0.80f, 0.80f)
            : new Color(0.30f, 0.30f, 0.30f);

        // --- Account header. ---

        /// <summary>Background fill for the account header strip.</summary>
        public static Color HeaderBackground => IsPro
            ? new Color(0.18f, 0.18f, 0.18f, 1f)
            : new Color(0.88f, 0.88f, 0.88f, 1f);

        /// <summary>Text color for account-error messages (muted orange).</summary>
        public static Color AccountErrorText => IsPro
            ? new Color(0.85f, 0.55f, 0.30f)
            : new Color(0.70f, 0.40f, 0.10f);

        /// <summary>Blue plan pill. Readable against both skin backgrounds.</summary>
        public static Color PlanPillBackground => new Color(0.25f, 0.35f, 0.55f, 1f);

        /// <summary>Green badge background (used when a count is non-zero / healthy).</summary>
        public static Color BadgeOkBackground => new Color(0.35f, 0.55f, 0.35f, 1f);

        /// <summary>Red badge background (used when a count is zero / unhealthy).</summary>
        public static Color BadgeErrorBackground => new Color(0.55f, 0.35f, 0.35f, 1f);

        /// <summary>Text color sitting on top of pill backgrounds.</summary>
        public static Color PillText => Color.white;

        // --- Progress bar. ---

        /// <summary>Background track of the generation progress bar.</summary>
        public static Color ProgressBarTrack => IsPro
            ? new Color(0.15f, 0.15f, 0.15f, 1f)
            : new Color(0.78f, 0.78f, 0.78f, 1f);

        /// <summary>Green fill of the generation progress bar.</summary>
        public static Color ProgressBarFill => IsPro
            ? new Color(0.22f, 0.58f, 0.22f, 1f)
            : new Color(0.30f, 0.65f, 0.30f, 1f);

        // --- Onboarding banner (muted amber). ---

        /// <summary>Onboarding banner background (muted amber in both skins).</summary>
        public static Color BannerBackground => IsPro
            ? new Color(0.30f, 0.24f, 0.10f, 1f)
            : new Color(0.95f, 0.85f, 0.55f, 1f);

        /// <summary>Onboarding banner text color, tuned for readability on <see cref="BannerBackground"/>.</summary>
        public static Color BannerText => IsPro
            ? new Color(0.95f, 0.90f, 0.75f)
            : new Color(0.30f, 0.25f, 0.10f);
    }
}
