using System.Linq;
using NUnit.Framework;
using PromptMotion.Shared.Humanoid;
using UnityEngine;

namespace PromptMotion.Tests.Validation
{
    public class HumanoidValidatorTests
    {
        private GameObject _created;

        [TearDown]
        public void TearDown()
        {
            if (_created != null)
            {
                Object.DestroyImmediate(_created);
                _created = null;
            }
        }

        [Test]
        public void Validate_NullGameObject_ReturnsInfoHint()
        {
            var report = HumanoidValidator.Validate((GameObject)null);
            Assert.IsTrue(report.IsUsable, "Info-only report has no errors, so IsUsable is true.");
            Assert.IsTrue(report.HasIssues);
            var issue = report.Issues[0];
            Assert.AreEqual(ValidationSeverity.Info, issue.Severity);
            Assert.IsTrue(issue.Message.Contains("Drag a Humanoid character"));
        }

        [Test]
        public void Validate_NullAnimator_IsNotUsable()
        {
            var report = HumanoidValidator.Validate((Animator)null);
            Assert.IsFalse(report.IsUsable);
            Assert.IsTrue(report.HasIssues);
        }

        [Test]
        public void Validate_NoAnimator_CanonicalMessage()
        {
            _created = new GameObject("TestChar");
            var report = HumanoidValidator.Validate(_created);
            Assert.IsFalse(report.IsUsable);
            var issue = report.Issues.First(i => i.Severity == ValidationSeverity.Error);
            Assert.IsTrue(issue.Message.Contains("has no Animator component"));
            Assert.IsTrue(issue.Message.Contains("TestChar"));
            Assert.IsTrue(issue.HasFix);
            Assert.AreEqual("Add Animator", issue.FixLabel);
        }

        [Test]
        public void Validate_NoAvatar_CanonicalMessage()
        {
            _created = new GameObject("TestChar");
            _created.AddComponent<Animator>();
            var report = HumanoidValidator.Validate(_created);
            Assert.IsFalse(report.IsUsable);
            var issue = report.Issues.First(i => i.Severity == ValidationSeverity.Error);
            Assert.IsTrue(issue.Message.Contains("has no Avatar assigned"));
            Assert.IsTrue(issue.Message.Contains("Auto-configure"));
            Assert.IsTrue(issue.HasFix);
            Assert.AreEqual("Auto-Configure Humanoid", issue.FixLabel);
        }

        [Test]
        public void Validate_ZeroScale_ReturnsErrorWithResetFix()
        {
            _created = new GameObject("TestChar");
            _created.transform.localScale = Vector3.zero;
            _created.AddComponent<Animator>();
            var report = HumanoidValidator.Validate(_created);
            Assert.IsFalse(report.IsUsable);
            var issue = report.Issues.First(i =>
                i.Severity == ValidationSeverity.Error &&
                i.Message.ToLower().Contains("scale"));
            Assert.IsTrue(issue.HasFix);
            Assert.AreEqual("Reset Scale", issue.FixLabel);
        }

        [Test]
        public void Validate_NonUniformScale_CanonicalMessage()
        {
            _created = new GameObject("TestChar");
            _created.transform.localScale = new Vector3(1f, 2f, 1f);
            _created.AddComponent<Animator>();
            var report = HumanoidValidator.Validate(_created);
            var issue = report.Issues.First(i => i.Severity == ValidationSeverity.Warning);
            Assert.IsTrue(issue.Message.Contains("non-uniform scale"));
            Assert.IsTrue(issue.Message.Contains("TestChar"));
            Assert.IsTrue(issue.HasFix);
            Assert.AreEqual("Reset Scale", issue.FixLabel);
        }

        [Test]
        public void Validate_UniformScale_DoesNotReportScaleIssue()
        {
            _created = new GameObject("TestChar");
            _created.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            _created.AddComponent<Animator>();
            var report = HumanoidValidator.Validate(_created);
            Assert.IsFalse(report.Issues.Any(
                i => i.Message.ToLower().Contains("scale")));
        }

        [Test]
        public void Validate_RunsUnder100ms_OnEmptyGameObject()
        {
            _created = new GameObject("TestChar");
            _created.AddComponent<Animator>();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var _ = HumanoidValidator.Validate(_created);
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 100);
        }

        [Test]
        public void ValidationReport_SortsErrorsFirst()
        {
            var issues = new[]
            {
                new ValidationIssue(ValidationSeverity.Info, "info"),
                new ValidationIssue(ValidationSeverity.Error, "error"),
                new ValidationIssue(ValidationSeverity.Warning, "warn")
            };
            var report = new ValidationReport(issues);
            Assert.AreEqual(ValidationSeverity.Error, report.Issues[0].Severity);
            Assert.AreEqual(ValidationSeverity.Warning, report.Issues[1].Severity);
            Assert.AreEqual(ValidationSeverity.Info, report.Issues[2].Severity);
        }

        [Test]
        public void ValidationReport_Empty_IsUsable()
        {
            var report = ValidationReport.Empty;
            Assert.IsTrue(report.IsUsable);
            Assert.IsFalse(report.HasIssues);
        }

        [Test]
        public void ValidationReport_NullConstructor_IsUsable()
        {
            var report = new ValidationReport(null);
            Assert.IsTrue(report.IsUsable);
            Assert.IsFalse(report.HasIssues);
        }

        [Test]
        public void ValidationReport_OnlyWarnings_IsStillUsable()
        {
            var report = new ValidationReport(new[]
            {
                new ValidationIssue(ValidationSeverity.Warning, "w"),
                new ValidationIssue(ValidationSeverity.Info, "i")
            });
            Assert.IsTrue(report.IsUsable);
            Assert.IsTrue(report.HasIssues);
        }

        [Test]
        public void ValidationIssue_FixNull_HasFixFalse()
        {
            var issue = new ValidationIssue(ValidationSeverity.Warning, "msg");
            Assert.IsFalse(issue.HasFix);
        }

        [Test]
        public void ValidationIssue_FixProvided_HasFixTrue()
        {
            var issue = new ValidationIssue(ValidationSeverity.Warning, "msg", () => { });
            Assert.IsTrue(issue.HasFix);
        }
    }
}
