namespace PromptMotion.Shared.Humanoid
{
    /// <summary>
    /// Severity level of a validation issue. Higher values are more severe. Only
    /// <see cref="Error"/> marks a target as not usable.
    /// </summary>
    public enum ValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }
}
