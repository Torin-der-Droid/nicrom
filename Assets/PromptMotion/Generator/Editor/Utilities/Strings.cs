namespace PromptMotion.Utilities
{
    public static class Strings
    {
        public static class Window
        {
            public const string SectionPrompt = "Prompt";
            public const string SectionPreview = "Preview";
            public const string FieldCharacter = "Character";
            public const string FieldDuration = "Duration (s)";
            public const string FieldSeed = "Seed";
            public const string PromptPlaceholder = "a person does a jumping jack";
            public const string TooltipCharacter = "Drag a Humanoid character prefab or scene object here.";
            public const string TooltipSeed = "-1 for a random seed.";
            public const string TooltipPlayPause = "Play / pause";
            public const string HintAssignCharacter = "Drag a Humanoid character into the Character slot to validate.";
            public const string HintPreviewNoCharacter = "Assign a Humanoid character to see the preview.";
            public const string HintPreviewNoClip = "Generate a clip to animate.";
            public const string ButtonGenerate = "Generate";
            public const string ButtonCancel = "Cancel";
            public const string ButtonFix = "Fix";
            public const string FieldClipName = "Clip name (optional)";
            public const string TooltipClipName =
                "Custom name for the generated clip. If empty, the prompt will be used.";
            public const string ButtonApplyToCharacter = "Apply to character";
            public const string TooltipApplyToCharacter =
                "Wire the generated clip into the assigned character's Animator.";
        }

        public static class Status
        {
            public const string Generating = "Generating...";
            public const string Cancelled = "Cancelled.";
            public const string ErrorPromptEmpty = "Prompt is empty.";
            public const string DoneNoClipFormat = "Done — {0} (no clip extracted)";
        }
    }
}
