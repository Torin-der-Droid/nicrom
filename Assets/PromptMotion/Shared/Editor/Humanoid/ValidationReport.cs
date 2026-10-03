using System;
using System.Collections.Generic;
using System.Linq;

namespace PromptMotion.Shared.Humanoid
{
    /// <summary>
    /// Result of running a validator. Wraps a read-only list of issues sorted from
    /// highest to lowest severity and exposes an overall <see cref="IsUsable"/> flag.
    /// </summary>
    public sealed class ValidationReport
    {
        /// <summary>All issues emitted by the validator, sorted highest-severity first.</summary>
        public IReadOnlyList<ValidationIssue> Issues { get; }

        /// <summary>Creates a report from a set of issues. <c>null</c> produces an empty report.</summary>
        public ValidationReport(IEnumerable<ValidationIssue> issues)
        {
            if (issues == null)
            {
                Issues = Array.Empty<ValidationIssue>();
                return;
            }
            Issues = issues
                .OrderByDescending(i => (int)i.Severity)
                .ToList();
        }

        /// <summary>
        /// True when there are no <see cref="ValidationSeverity.Error"/>-level issues.
        /// Warnings and informational messages do not block usability.
        /// </summary>
        public bool IsUsable => !Issues.Any(i => i.Severity == ValidationSeverity.Error);

        /// <summary>True when at least one issue is present (of any severity).</summary>
        public bool HasIssues => Issues.Count > 0;

        /// <summary>A shared empty, usable report.</summary>
        public static ValidationReport Empty { get; } = new ValidationReport(null);
    }
}
