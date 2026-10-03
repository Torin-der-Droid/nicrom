namespace PromptMotion.Shared.Utilities
{
    /// <summary>
    /// User-visible strings consumed by Shared editor code (clip-apply flow,
    /// validation status labels). Generator-only UI copy lives in the
    /// Generator-side <c>PromptMotion.Utilities.Strings</c> class.
    /// </summary>
    /// <remarks>
    /// Format strings use composite-format placeholders (<c>{0}</c>, <c>{1}</c>)
    /// so translators do not have to preserve C# interpolation syntax.
    /// </remarks>
    public static class SharedStrings
    {
        /// <summary>Status messages shown after the Apply-to-character flow runs.</summary>
        public static class Apply
        {
            /// <summary>Composite: {0}=clip name, {1}=character name, {2}=controller name.</summary>
            public const string SuccessFormat = "Applied '{0}' to {1} (controller: {2})";
            /// <summary>Composite: {0}=character name.</summary>
            public const string ErrorAddAnimatorFormat = "Failed to add Animator to {0}.";
            /// <summary>Composite: {0}=controller path.</summary>
            public const string ErrorCreateControllerFormat = "Failed to create AnimatorController at {0}.";
            /// <summary>Composite: {0}=exception message.</summary>
            public const string ErrorPrefabSaveFormat = "Failed to save prefab: {0}";
            /// <summary>Composite: {0}=exception message.</summary>
            public const string ErrorGenericFormat = "Apply failed: {0}";
        }

        /// <summary>Validation status labels.</summary>
        public static class Validation
        {
            public const string NotUsable = "Not usable — fix errors below";
            public const string ReadyWithWarnings = "Ready with warnings";
            public const string Ready = "Ready to generate";
        }
    }
}
