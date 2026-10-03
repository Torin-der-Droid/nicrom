using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Shared.Humanoid
{
    /// <summary>
    /// Extracts <see cref="AnimationClip"/> sub-assets from an imported FBX. Unity
    /// auto-generates a <c>__preview__</c> clip on humanoid imports that must be
    /// filtered out — callers want the real animation, not the preview stub.
    /// </summary>
    public static class ImportedAnimationLocator
    {
        private const string PreviewClipPrefix = "__preview__";

        /// <summary>
        /// Returns the first usable animation clip at the given path, or null if
        /// none was extracted.
        /// </summary>
        public static AnimationClip GetClip(string assetPath)
        {
            var clips = GetAllClips(assetPath);
            return clips.Count > 0 ? clips[0] : null;
        }

        /// <summary>
        /// Returns all usable animation clips at the given path, excluding Unity's
        /// auto-generated preview clip. Never returns null — an empty list means
        /// nothing was extracted.
        /// </summary>
        public static List<AnimationClip> GetAllClips(string assetPath)
        {
            var result = new List<AnimationClip>();
            if (string.IsNullOrEmpty(assetPath)) return result;

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets == null) return result;

            foreach (var asset in assets)
            {
                if (!(asset is AnimationClip clip)) continue;
                if (IsPreviewClip(clip)) continue;
                result.Add(clip);
            }
            return result;
        }

        private static bool IsPreviewClip(AnimationClip clip)
        {
            if (clip == null) return true;
            return clip.name != null &&
                   clip.name.StartsWith(PreviewClipPrefix, StringComparison.Ordinal);
        }
    }
}
