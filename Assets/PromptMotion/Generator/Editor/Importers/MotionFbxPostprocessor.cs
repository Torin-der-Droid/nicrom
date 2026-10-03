using System;
using PromptMotion.Settings;
using PromptMotion.Shared.Utilities;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Importers
{
    /// <summary>
    /// Auto-configures FBX files landing under PromptMotion's output folder as
    /// Humanoid animations with a <c>CreateFromThisModel</c> avatar. Path guard
    /// is strict — only files whose <c>assetPath</c> sits beneath
    /// <see cref="PromptMotionSettings.OutputFolder"/> are touched; any other
    /// FBX anywhere in the project is left completely alone.
    /// </summary>
    /// <remarks>
    /// Per build brief §11: <see cref="ModelImporter.animationType"/> must be set
    /// during <c>OnPreprocessModel</c>, not after — otherwise Unity re-imports in
    /// a loop. This class also deliberately avoids calling
    /// <see cref="AssetDatabase.Refresh"/> from any callback, and reads the
    /// settings asset via a direct <see cref="AssetDatabase.LoadAssetAtPath"/>
    /// rather than <see cref="PromptMotionSettings.Instance"/> (whose lazy-create
    /// path calls <see cref="AssetDatabase.CreateAsset"/>, which is unsafe inside
    /// a preprocess callback).
    /// </remarks>
    public class MotionFbxPostprocessor : AssetPostprocessor
    {
        private const string DefaultOutputFolder = "Assets/PromptMotion/Generated";

        private void OnPreprocessModel()
        {
            if (!IsPromptMotionOutput(assetPath)) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.motionNodeName = string.Empty;   // let Unity pick the root motion node
            importer.optimizeGameObjects = false;     // preserve bone hierarchy for GetBoneTransform

            // Skeleton-only FBXes: skip mesh-side imports we don't use.
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;

            Log.Info($"Configuring '{assetPath}' as Humanoid (PromptMotion output).");
        }

        // NOTE: An earlier iteration had an OnPostprocessModel(GameObject) override
        // that called ImportedAnimationLocator.GetAllClips(assetPath) and warned when
        // the list was empty. That check runs BEFORE Unity materialises the
        // AnimationClip sub-assets, so AssetDatabase.LoadAllAssetsAtPath returned an
        // empty array every time and the warning fired even on perfectly-imported
        // clips — confirmed during the Phase 6 end-to-end verification. Removed
        // entirely: the window's HandleResponse logs the extracted clip name after
        // import completes, which is the reliable source of truth.

        private static bool IsPromptMotionOutput(string path)
        {
            return IsPathUnderOutputFolder(path, GetOutputFolder());
        }

        /// <summary>
        /// Returns true when <paramref name="path"/> sits strictly under
        /// <paramref name="outputFolder"/>. The match requires a trailing-slash
        /// boundary so a sibling whose name shares a prefix (e.g. "Generated2"
        /// vs "Generated") cannot slip through a naive prefix check. Exposed as
        /// <c>internal</c> (via <c>InternalsVisibleTo</c>) so the path-guard
        /// logic can be unit-tested directly without round-tripping through the
        /// asset import pipeline.
        /// </summary>
        internal static bool IsPathUnderOutputFolder(string path, string outputFolder)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (string.IsNullOrEmpty(outputFolder)) return false;
            var normalizedPath = path.Replace('\\', '/');
            var normalizedFolder = outputFolder.Replace('\\', '/').TrimEnd('/');
            if (normalizedFolder.Length == 0) return false;
            return normalizedPath.StartsWith(normalizedFolder + "/", StringComparison.Ordinal);
        }

        private static string GetOutputFolder()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PromptMotionSettings>(
                PromptMotionSettings.AssetPath);
            if (settings == null) return DefaultOutputFolder;
            var folder = settings.OutputFolder?.TrimEnd('/');
            return string.IsNullOrEmpty(folder) ? DefaultOutputFolder : folder;
        }
    }
}
