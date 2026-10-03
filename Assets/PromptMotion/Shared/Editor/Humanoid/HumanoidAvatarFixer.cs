using System;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Shared.Humanoid
{
    public sealed class FixResult
    {
        public bool Success;
        public string LogMessage;
        public string FailureReason;

        public static FixResult Ok(string log) => new FixResult { Success = true, LogMessage = log };
        public static FixResult Fail(string reason) => new FixResult { Success = false, FailureReason = reason };
    }

    public static class HumanoidAvatarFixer
    {
        /// <summary>
        /// Fired after the bone-assist popup completes and the user-mapped
        /// avatar has been assigned to the animator. The synchronous path of
        /// <see cref="AutoConfigureAvatar"/> doesn't fire this — its result is
        /// already observed by the validator's normal post-fix refresh. The
        /// async path bypasses that refresh because <see cref="AutoConfigureAvatar"/>
        /// returns BEFORE the popup completes; subscribers (typically the
        /// Generator window) use this event to re-run validation when the
        /// avatar finally lands.
        /// </summary>
        public static event Action<GameObject> AvatarAssignedAsync;

        public static FixResult AddAnimator(GameObject character)
        {
            if (character == null) return FixResult.Fail("No character supplied.");
            if (character.GetComponentInChildren<Animator>(true) != null)
                return FixResult.Ok("Animator already present.");

            Undo.AddComponent<Animator>(character);
            return FixResult.Ok($"Added Animator component to '{character.name}'.");
        }

        public static FixResult AutoConfigureAvatar(GameObject character)
        {
            if (character == null) return FixResult.Fail("No character supplied.");

            var animator = character.GetComponentInChildren<Animator>(true);
            if (animator == null)
                return FixResult.Fail("No Animator component found. Add an Animator first.");

            if (HumanoidAvatarAutoConfigurator.TryAutoConfigure(character, out var avatar, out var configLog))
            {
                Undo.RecordObject(animator, "Assign Auto-Built Humanoid Avatar");
                animator.avatar = avatar;
                EditorUtility.SetDirty(animator);
                return FixResult.Ok($"Built and assigned Humanoid Avatar for '{character.name}'.");
            }

            // Bone names didn't match any known convention. Open the assist
            // popup so the user can drag-and-drop the missing slots. The
            // popup is non-modal (the user has to drag from the Hierarchy,
            // which requires Editor input to keep flowing) so this method
            // can't wait for its result synchronously — return immediately
            // with a "popup opened" success message, and let the popup
            // callback finish the assignment once the user is done.
            HumanoidBoneAssistWindow.ShowFor(character, (ok, userBuilt) =>
            {
                if (!ok || userBuilt == null)
                {
                    // Window cancelled or closed. The validator already logged
                    // that we were attempting an auto-fix; a quieter info log
                    // here is enough — re-running validation re-opens the
                    // popup if the user wants another go.
                    Debug.Log($"[PromptMotion] Bone-mapping assistant for '{character.name}' was cancelled. " +
                              "Re-run validation to reopen.");
                    return;
                }
                if (animator == null)
                {
                    // The character (or its Animator) was destroyed while the
                    // popup was open. Discard the result rather than crashing.
                    Debug.LogWarning($"[PromptMotion] Bone-mapping assistant finished, but the Animator on " +
                                     $"'{character?.name ?? "(destroyed)"}' is no longer available. Avatar not assigned.");
                    return;
                }
                Undo.RecordObject(animator, "Assign Manually-Mapped Humanoid Avatar");
                animator.avatar = userBuilt;
                EditorUtility.SetDirty(animator);
                Debug.Log($"[PromptMotion] Built and assigned Humanoid Avatar for '{character.name}' from manual mapping.");
                // Notify subscribers (the Generator window) so they can re-run
                // validation and clear the stale "no avatar" issue without
                // requiring the user to click anything.
                try { AvatarAssignedAsync?.Invoke(character); }
                catch (Exception ex) { Debug.LogException(ex); }
            });

            return FixResult.Ok(
                "Bone-mapping assistant opened — the auto-mapper couldn't recognise every bone " +
                "in this rig. Drag the missing bones from the Hierarchy onto the slots in the " +
                "popup window (or pick them from the dropdowns), then click Build Avatar.\n\n" +
                "Details:\n" + configLog);
        }

        public static FixResult ConvertFbxToHumanoid(GameObject character)
        {
            if (character == null) return FixResult.Fail("No character supplied.");

            var importer = FindModelImporter(character);
            if (importer == null)
                return FixResult.Fail(
                    "Could not find the source FBX for this character. The Avatar may have been " +
                    "created at runtime or the source asset is missing.\n\n" +
                    "Manual fix:\n" +
                    "1. Find your character's FBX in the Project window\n" +
                    "2. In the Inspector, go to the Rig tab\n" +
                    "3. Set Animation Type to \"Humanoid\"\n" +
                    "4. Click Apply");

            Undo.RegisterCompleteObjectUndo(importer, "Convert FBX to Humanoid");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return FixResult.Ok($"Converted '{importer.assetPath}' to Humanoid animation type.");
        }

        public static FixResult ReconfigureBoneMapping(GameObject character)
        {
            if (character == null) return FixResult.Fail("No character supplied.");

            var importer = FindModelImporter(character);
            if (importer == null)
                return FixResult.Fail(
                    "Could not find the source FBX for this character.\n\n" +
                    "Manual fix:\n" +
                    "1. Find your character's FBX in the Project window\n" +
                    "2. In the Inspector, go to the Rig tab\n" +
                    "3. Click Configure, fix any red/yellow bone indicators\n" +
                    "4. Click Apply");

            Undo.RegisterCompleteObjectUndo(importer, "Reconfigure Humanoid Bone Mapping");
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return FixResult.Ok($"Reconfigured bone mapping on '{importer.assetPath}'.");
        }

        public static FixResult ResetScale(GameObject character)
        {
            if (character == null) return FixResult.Fail("No character supplied.");

            Undo.RecordObject(character.transform, "Reset Scale");
            character.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(character.transform);
            return FixResult.Ok($"Reset scale to (1, 1, 1) on '{character.name}'.");
        }

        private static ModelImporter FindModelImporter(GameObject character)
        {
            var animator = character.GetComponentInChildren<Animator>(true);

            if (animator != null && animator.avatar != null)
            {
                var avatarPath = AssetDatabase.GetAssetPath(animator.avatar);
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    var imp = AssetImporter.GetAtPath(avatarPath) as ModelImporter;
                    if (imp != null) return imp;
                }
            }

            var smr = character.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null)
            {
                var meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (!string.IsNullOrEmpty(meshPath))
                {
                    var imp = AssetImporter.GetAtPath(meshPath) as ModelImporter;
                    if (imp != null) return imp;
                }
            }

            var charPath = AssetDatabase.GetAssetPath(character);
            if (!string.IsNullOrEmpty(charPath))
            {
                var imp = AssetImporter.GetAtPath(charPath) as ModelImporter;
                if (imp != null) return imp;
            }

            return null;
        }
    }
}
