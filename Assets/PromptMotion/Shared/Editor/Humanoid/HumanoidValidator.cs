using System.Collections.Generic;
using PromptMotion.Shared.Utilities;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Shared.Humanoid
{
    public static class HumanoidValidator
    {
        private const float ScaleAxisMinAbs = 0.001f;
        private const float ScaleUniformityTolerance = 0.001f;
        private const float TPoseArmAngleToleranceDegrees = 30f;

        public static ValidationReport Validate(GameObject candidate)
        {
            var issues = new List<ValidationIssue>();

            if (candidate == null)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Info,
                    "Drag a Humanoid character into the Character slot to validate."));
                return new ValidationReport(issues);
            }

            ValidateScale(candidate, issues);

            var animator = candidate.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"'{candidate.name}' has no Animator component. Add an Animator to make this character usable.",
                    fix: () => ConfirmAndRun(
                        "Auto-Fix: Add Animator",
                        $"This will add an Animator component to '{candidate.name}'.\n\n" +
                        "Safe operation — adding a component does not modify your model files. " +
                        "If you change your mind, you can remove the component or use Edit > Undo.",
                        "Add Animator",
                        () => HumanoidAvatarFixer.AddAnimator(candidate)),
                    fixLabel: "Add Animator"));
                return new ValidationReport(issues);
            }

            ValidateAvatar(animator, candidate, issues);
            return new ValidationReport(issues);
        }

        public static ValidationReport Validate(Animator animator)
        {
            var issues = new List<ValidationIssue>();

            if (animator == null)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "No Animator supplied."));
                return new ValidationReport(issues);
            }

            ValidateScale(animator.gameObject, issues);
            ValidateAvatar(animator, animator.gameObject, issues);
            return new ValidationReport(issues);
        }

        private static void ValidateScale(GameObject go, List<ValidationIssue> issues)
        {
            var t = go.transform;
            var scale = t.lossyScale;
            var absX = Mathf.Abs(scale.x);
            var absY = Mathf.Abs(scale.y);
            var absZ = Mathf.Abs(scale.z);

            if (absX < ScaleAxisMinAbs || absY < ScaleAxisMinAbs || absZ < ScaleAxisMinAbs)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"'{go.name}' has zero or near-zero scale on at least one axis ({scale}). Reset the transform to a non-zero scale.",
                    fix: () => ConfirmAndRun(
                        "Auto-Fix: Reset Scale",
                        $"This will reset '{go.name}''s Transform scale to (1, 1, 1).\n\n" +
                        "The character will visibly resize in the scene. You may need to adjust " +
                        "your scene's camera or other objects to compensate.\n\n" +
                        "This change can be reverted via Edit > Undo or by manually setting the scale back.",
                        "Reset Scale",
                        () => HumanoidAvatarFixer.ResetScale(go)),
                    fixLabel: "Reset Scale"));
                return;
            }

            var maxAxis = Mathf.Max(absX, Mathf.Max(absY, absZ));
            var minAxis = Mathf.Min(absX, Mathf.Min(absY, absZ));
            if (maxAxis - minAxis > ScaleUniformityTolerance)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    $"'{go.name}' has non-uniform scale ({scale}). Humanoid retargeting assumes uniform scale; results may look distorted.",
                    fix: () => ConfirmAndRun(
                        "Auto-Fix: Reset Scale",
                        $"This will reset '{go.name}''s Transform scale to (1, 1, 1).\n\n" +
                        "The character will visibly resize in the scene. You may need to adjust " +
                        "your scene's camera or other objects to compensate.\n\n" +
                        "This change can be reverted via Edit > Undo or by manually setting the scale back.",
                        "Reset Scale",
                        () => HumanoidAvatarFixer.ResetScale(go)),
                    fixLabel: "Reset Scale"));
            }
        }

        private static void ValidateAvatar(Animator animator, GameObject go, List<ValidationIssue> issues)
        {
            var avatar = animator.avatar;
            if (avatar == null)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"Animator on '{go.name}' has no Avatar assigned. Auto-configure can build a Humanoid Avatar from the rig's bone names.",
                    fix: () => ConfirmAndRun(
                        "Auto-Fix: Configure Humanoid Avatar",
                        $"This will analyze '{go.name}''s skeleton and try to build a Humanoid Avatar " +
                        "from its bones, then assign it to the Animator.\n\n" +
                        "Safe operation — your character's source FBX is not modified. If the result " +
                        "looks wrong, you can clear the Avatar field on the Animator manually.\n\n" +
                        "This works best when bone names follow a recognizable convention (Mixamo, RPM, " +
                        "generic Unity naming). Custom rigs may fail and require manual configuration.",
                        "Auto-Configure Humanoid",
                        () => HumanoidAvatarFixer.AutoConfigureAvatar(go)),
                    fixLabel: "Auto-Configure Humanoid"));
                return;
            }

            if (!avatar.isValid)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"'{go.name}''s Avatar is Humanoid but bone mapping is invalid. Open the FBX → Rig → Configure to fix any red/yellow bones, then click Apply."));
                return;
            }

            if (!avatar.isHuman)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"'{go.name}''s Avatar is configured as Generic, not Humanoid. PromptMotion clips are Humanoid and require Humanoid avatars. Convert by setting Animation Type = Humanoid in the FBX's Rig tab and clicking Apply."));
                return;
            }

            ValidateTPose(animator, issues);
        }

        private static void ValidateTPose(Animator animator, List<ValidationIssue> issues)
        {
            var leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (leftArm == null || rightArm == null || leftHand == null || rightHand == null)
                return;

            var up = animator.transform.up;
            var leftDir = (leftHand.position - leftArm.position).normalized;
            var rightDir = (rightHand.position - rightArm.position).normalized;

            var leftVerticalAngle = Vector3.Angle(leftDir, up);
            var rightVerticalAngle = Vector3.Angle(rightDir, up);

            if (Mathf.Abs(leftVerticalAngle - 90f) > TPoseArmAngleToleranceDegrees ||
                Mathf.Abs(rightVerticalAngle - 90f) > TPoseArmAngleToleranceDegrees)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "Character does not appear to be in a T-pose on frame 0. Humanoid retargeting expects a T-pose; an A-pose may produce slight shoulder distortion."));
            }
        }

        private static void ConfirmAndRun(string title, string message, string okButton, System.Func<FixResult> fixAction)
        {
            if (!EditorUtility.DisplayDialog(title, message, okButton, "Cancel"))
                return;

            var result = fixAction();
            if (result.Success)
            {
                Log.Info($"[PromptMotion] Auto-fix: {result.LogMessage}");
            }
            else
            {
                Log.Warn($"[PromptMotion] Auto-fix failed: {result.FailureReason}");
                EditorUtility.DisplayDialog(
                    title + " — Failed",
                    result.FailureReason,
                    "OK");
            }
        }
    }
}
