using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PromptMotion.Shared.Humanoid;
using PromptMotion.Shared.Utilities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PromptMotion.Shared.Clips
{
    /// <summary>
    /// Wires a generated <see cref="AnimationClip"/> into an assigned character's
    /// <see cref="Animator"/> + <see cref="AnimatorController"/> without manual
    /// drag-drop. Handles three character-source cases identically from the
    /// caller's perspective: prefab asset (Project view), prefab instance in the
    /// active scene, and plain scene GameObject.
    /// </summary>
    public static class ClipApplier
    {
        /// <summary>Folder under which generated AnimatorControllers are written.</summary>
        public const string ControllerFolder = "Assets/PromptMotion/Generated/Controllers";

        private const int MaxSlugLength = 40;
        private const string EmptyFallback = "clip";

        /// <summary>
        /// Slugifies a free-form string into a filename- and state-name-safe form:
        /// lowercase, non-alphanumerics replaced with '_', repeated underscores
        /// collapsed, leading/trailing underscores stripped, truncated to
        /// <see cref="MaxSlugLength"/>. Empty / whitespace-only input returns
        /// <c>"clip"</c>.
        /// </summary>
        public static string Slugify(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return EmptyFallback;
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else sb.Append('_');
            }
            var s = Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
            if (s.Length == 0) return EmptyFallback;
            if (s.Length > MaxSlugLength) s = s.Substring(0, MaxSlugLength).TrimEnd('_');
            if (s.Length == 0) return EmptyFallback;
            return s;
        }

        /// <summary>
        /// Returns <paramref name="desired"/> if it doesn't appear in <paramref name="existing"/>,
        /// else appends <c>_2</c>, <c>_3</c>, ... until unique.
        /// </summary>
        public static string MakeUniqueName(IEnumerable<string> existing, string desired)
        {
            if (desired == null) desired = EmptyFallback;
            var set = new HashSet<string>(existing ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (!set.Contains(desired)) return desired;
            for (int n = 2; n < int.MaxValue; n++)
            {
                var candidate = $"{desired}_{n}";
                if (!set.Contains(candidate)) return candidate;
            }
            // Practically unreachable.
            return desired + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        /// <summary>
        /// Path of the AnimatorController for a given character name, anchored at
        /// the default <see cref="ControllerFolder"/>. Slugified so arbitrary
        /// GameObject names produce a stable, filesystem-safe path.
        /// </summary>
        public static string ResolveControllerPath(string characterName)
        {
            return ResolveControllerPath(characterName, ControllerFolder);
        }

        /// <summary>
        /// Path of the AnimatorController for a given character name, anchored at
        /// <paramref name="folder"/>. Used by the Library window to write
        /// controllers under <c>Assets/PromptMotion/Library/Generated/Controllers</c>
        /// instead of the Generator's default location.
        /// </summary>
        public static string ResolveControllerPath(string characterName, string folder)
        {
            var trimmed = string.IsNullOrEmpty(folder) ? ControllerFolder : folder.TrimEnd('/');
            return $"{trimmed}/{Slugify(characterName)}_Controller.controller";
        }

        /// <summary>
        /// Recursively creates the folder chain under "Assets/" so
        /// <see cref="AssetDatabase.CreateFolder"/> can be called on the leaf.
        /// No-op if the folder already exists.
        /// </summary>
        public static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            var parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        /// <summary>
        /// Result of <see cref="Apply"/>. Carries enough information for the
        /// caller to surface success/failure and ping the produced controller
        /// in the Project view.
        /// </summary>
        public sealed class ApplyResult
        {
            public bool Success;
            public string Message;
            public AnimatorController Controller;
            public string FinalClipName;
            public string FinalStateName;

            public static ApplyResult Ok(AnimatorController controller, string clipName, string stateName, string message)
                => new ApplyResult { Success = true, Controller = controller, FinalClipName = clipName, FinalStateName = stateName, Message = message };

            public static ApplyResult Fail(string message)
                => new ApplyResult { Success = false, Message = message };
        }

        /// <summary>
        /// Applies <paramref name="clip"/> to <paramref name="character"/>'s
        /// Animator under a per-character AnimatorController. Handles prefab-asset,
        /// prefab-instance, and plain scene GameObject sources. Renames the clip
        /// (best-effort) and the state to <paramref name="desiredClipName"/>
        /// (slugified, made unique against siblings).
        /// </summary>
        /// <param name="character">Target character (prefab asset, prefab instance, or scene GameObject).</param>
        /// <param name="clip">Clip to wire in.</param>
        /// <param name="desiredClipName">Display name; will be slugified and made unique.</param>
        /// <param name="controllerFolder">
        /// Folder the per-character AnimatorController lives in when one has to
        /// be created. Defaults to <see cref="ControllerFolder"/> (the Generator's
        /// path); the Library window passes its own folder so the two products
        /// don't share controllers by accident.
        /// </param>
        public static ApplyResult Apply(GameObject character, AnimationClip clip, string desiredClipName, string controllerFolder = ControllerFolder)
        {
            if (character == null) return ApplyResult.Fail("Character is null.");
            if (clip == null) return ApplyResult.Fail("Clip is null.");

            var slug = Slugify(string.IsNullOrWhiteSpace(desiredClipName) ? clip.name : desiredClipName);
            var folder = string.IsNullOrEmpty(controllerFolder) ? ControllerFolder : controllerFolder;

            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(character);
            var isPrefabInstance = !isPrefabAsset && PrefabUtility.IsPartOfPrefabInstance(character);

            if (isPrefabAsset)
            {
                return ApplyToPrefabAsset(character, clip, slug, folder);
            }

            // Scene GameObject (possibly a prefab instance).
            var inner = ApplyToObjectGraph(character, clip, slug, isPrefabInstance, folder);
            if (inner.Success && isPrefabInstance && inner.Controller != null)
            {
                // Recorded inside ApplyToObjectGraph already; nothing further.
            }
            return inner;
        }

        private static ApplyResult ApplyToPrefabAsset(GameObject prefabRoot, AnimationClip clip, string slug, string controllerFolder)
        {
            var prefabPath = AssetDatabase.GetAssetPath(prefabRoot);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return ApplyResult.Fail("Prefab asset has no asset path; cannot save.");
            }

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var inner = ApplyToObjectGraph(contents, clip, slug, recordPrefabInstance: false, controllerFolder);
                if (!inner.Success) return inner;

                try
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                catch (Exception ex)
                {
                    return ApplyResult.Fail(string.Format(SharedStrings.Apply.ErrorPrefabSaveFormat, ex.Message));
                }

                return ApplyResult.Ok(
                    inner.Controller,
                    inner.FinalClipName,
                    inner.FinalStateName,
                    string.Format(SharedStrings.Apply.SuccessFormat, inner.FinalClipName, prefabRoot.name, inner.Controller.name));
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static ApplyResult ApplyToObjectGraph(
            GameObject root,
            AnimationClip clip,
            string slug,
            bool recordPrefabInstance,
            string controllerFolder)
        {
            // 1. Animator
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                try
                {
                    animator = recordPrefabInstance
                        ? Undo.AddComponent<Animator>(root)
                        : root.AddComponent<Animator>();
                }
                catch (Exception ex)
                {
                    Log.Error($"AddComponent<Animator> failed on {root.name}: {ex}");
                    return ApplyResult.Fail(string.Format(SharedStrings.Apply.ErrorAddAnimatorFormat, root.name));
                }
                if (animator == null)
                {
                    return ApplyResult.Fail(string.Format(SharedStrings.Apply.ErrorAddAnimatorFormat, root.name));
                }
            }

            // 2. Avatar (best-effort)
            if (animator.avatar == null)
            {
                var avatar = TryFindAvatarFromCharacter(root);
                if (avatar != null)
                {
                    if (recordPrefabInstance) Undo.RecordObject(animator, "Apply PromptMotion clip");
                    animator.avatar = avatar;
                }
                else
                {
                    Log.Warn(
                        $"Could not auto-detect Humanoid Avatar for {root.name}; the Animator's Avatar field is empty. " +
                        "If retargeting looks wrong, assign the Avatar manually in the Inspector.");
                }
            }

            // 3. Controller (existing wins; otherwise load-or-create per-character)
            AnimatorController controller;
            if (animator.runtimeAnimatorController is AnimatorController existing)
            {
                controller = existing;
            }
            else
            {
                EnsureFolder(controllerFolder);
                var controllerPath = ResolveControllerPath(root.name, controllerFolder);
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                }
                if (controller == null)
                {
                    return ApplyResult.Fail(string.Format(SharedStrings.Apply.ErrorCreateControllerFormat, controllerPath));
                }
            }

            // 4. Rename the clip sub-asset (best-effort; may not persist across reimport
            // unless ModelImporter.clipAnimations is used — out of scope per spec).
            var clipPath = AssetDatabase.GetAssetPath(clip);
            var siblings = string.IsNullOrEmpty(clipPath)
                ? new List<string>()
                : ImportedAnimationLocator.GetAllClips(clipPath)
                    .Where(c => c != clip)
                    .Select(c => c.name)
                    .ToList();
            var finalClipName = MakeUniqueName(siblings, slug);
            if (clip.name != finalClipName)
            {
                try
                {
                    clip.name = finalClipName;
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssets();
                }
                catch (Exception ex)
                {
                    Log.Warn($"Clip rename failed: {ex.Message}; proceeding with name '{clip.name}'.");
                }
            }

            // 5. Add as new state to Base Layer; first-state-becomes-default.
            if (controller.layers == null || controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }
            var baseLayer = controller.layers[0];
            var stateMachine = baseLayer.stateMachine;

            var existingStateNames = stateMachine.states.Select(c => c.state.name).ToList();
            var finalStateName = MakeUniqueName(existingStateNames, finalClipName);

            var wasEmpty = stateMachine.states.Length == 0;
            var newState = stateMachine.AddState(finalStateName);
            newState.motion = clip;
            if (wasEmpty)
            {
                stateMachine.defaultState = newState;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // 6. Assign controller to Animator if not already.
            if (animator.runtimeAnimatorController != controller)
            {
                if (recordPrefabInstance) Undo.RecordObject(animator, "Apply PromptMotion clip");
                animator.runtimeAnimatorController = controller;
                if (recordPrefabInstance) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }

            return ApplyResult.Ok(
                controller,
                finalClipName,
                finalStateName,
                string.Format(SharedStrings.Apply.SuccessFormat, finalClipName, root.name, controller.name));
        }

        /// <summary>
        /// Walks the character's hierarchy looking for a SkinnedMeshRenderer whose
        /// shared mesh comes from an FBX; returns the first Avatar sub-asset on
        /// that FBX. Heuristic — works for typical Humanoid imports (Mixamo,
        /// PromptMotion-generated FBXes) but may miss compound rigs that load the
        /// mesh and avatar from different files.
        /// </summary>
        private static Avatar TryFindAvatarFromCharacter(GameObject root)
        {
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) return null;
            var meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
            if (string.IsNullOrEmpty(meshPath)) return null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(meshPath))
            {
                if (asset is Avatar avatar) return avatar;
            }
            return null;
        }
    }
}
