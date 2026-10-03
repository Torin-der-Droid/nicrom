using System;

namespace PromptMotion.Shared.Humanoid
{
    /// <summary>
    /// A single finding emitted by a validator: a severity, a human-readable message,
    /// and an optional auto-fix callback that the UI may surface as a one-click Fix button.
    /// </summary>
    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public string Message { get; }
        public Action Fix { get; }
        public string FixLabel { get; }

        public ValidationIssue(ValidationSeverity severity, string message, Action fix = null, string fixLabel = null)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Fix = fix;
            FixLabel = fixLabel;
        }

        public bool HasFix => Fix != null;
    }
}
