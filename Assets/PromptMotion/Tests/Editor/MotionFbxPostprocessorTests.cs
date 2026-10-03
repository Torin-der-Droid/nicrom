using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PromptMotion.Importers;
using PromptMotion.Shared.Humanoid;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace PromptMotion.Tests.Importers
{
    /// <summary>
    /// Covers <see cref="MotionFbxPostprocessor"/>'s path guard with direct unit
    /// tests against <see cref="MotionFbxPostprocessor.IsPathUnderOutputFolder"/>
    /// (the inside / outside / prefix-colliding-sibling cases from build brief
    /// Phase 4), and one integration test that drops a minimally-valid FBX under
    /// the output folder and verifies <see cref="ModelImporter.animationType"/>
    /// comes up as Humanoid — the real behaviour under test. The
    /// <see cref="ImportedAnimationLocator"/> edge cases live here too.
    ///
    /// The integration test uses an FBX binary signature (magic "Kaydara FBX
    /// Binary  " + version) rather than pure zero bytes so Unity's asset pipeline
    /// classifies the file as a model and the preprocessor callback fires. The
    /// body is still garbage so the FBX parser logs an error, which we suppress
    /// via <see cref="LogAssert.ignoreFailingMessages"/>.
    /// </summary>
    public class MotionFbxPostprocessorTests
    {
        // "Kaydara FBX Binary  " (20 bytes, note the two trailing spaces),
        // then magic separator 0x00 0x1A 0x00, then FBX version 7400 as
        // little-endian uint32 (0xE8 0x1C 0x00 0x00). 27 bytes total — enough
        // for Unity to recognise the file as an FBX binary and engage the
        // model-import pipeline (the body is padded with zeros so the parser
        // fails harmlessly).
        private static readonly byte[] FbxBinarySignature =
        {
            0x4B, 0x61, 0x79, 0x64, 0x61, 0x72, 0x61, 0x20,
            0x46, 0x42, 0x58, 0x20,
            0x42, 0x69, 0x6E, 0x61, 0x72, 0x79, 0x20, 0x20,
            0x00, 0x1A, 0x00,
            0xE8, 0x1C, 0x00, 0x00,
        };

        private const string OutputFolder = "Assets/PromptMotion/Generated";

        private readonly List<string> _createdAssetPaths = new List<string>();

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            EnsureFolder(OutputFolder);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _createdAssetPaths)
            {
                if (File.Exists(path) || AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
            _createdAssetPaths.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        // --- Path guard (pure unit tests, no file I/O) ---

        [Test]
        public void IsPathUnderOutputFolder_FileInsideOutputFolder_ReturnsTrue()
        {
            Assert.IsTrue(MotionFbxPostprocessor.IsPathUnderOutputFolder(
                OutputFolder + "/test_inside.fbx", OutputFolder));
        }

        [Test]
        public void IsPathUnderOutputFolder_FileOutsideOutputFolder_ReturnsFalse()
        {
            // Sanity check demanded by build brief Phase 4: files NOT under the
            // output folder must not be touched by the postprocessor.
            Assert.IsFalse(MotionFbxPostprocessor.IsPathUnderOutputFolder(
                "Assets/test_outside.fbx", OutputFolder));
        }

        [Test]
        public void IsPathUnderOutputFolder_FileInPrefixCollidingSibling_ReturnsFalse()
        {
            // "Generated2" shares a prefix with "Generated". A naive StartsWith
            // check would match it. The guard must not.
            Assert.IsFalse(MotionFbxPostprocessor.IsPathUnderOutputFolder(
                "Assets/PromptMotion/Generated2/test_collide.fbx", OutputFolder));
        }

        [Test]
        public void IsPathUnderOutputFolder_BackslashPath_Normalized()
        {
            // Windows path separators must not sneak past the guard.
            Assert.IsTrue(MotionFbxPostprocessor.IsPathUnderOutputFolder(
                @"Assets\PromptMotion\Generated\win_path.fbx", OutputFolder));
        }

        [Test]
        public void IsPathUnderOutputFolder_NullOrEmptyInputs_ReturnFalse()
        {
            Assert.IsFalse(MotionFbxPostprocessor.IsPathUnderOutputFolder(null, OutputFolder));
            Assert.IsFalse(MotionFbxPostprocessor.IsPathUnderOutputFolder(string.Empty, OutputFolder));
            Assert.IsFalse(MotionFbxPostprocessor.IsPathUnderOutputFolder(
                OutputFolder + "/x.fbx", null));
            Assert.IsFalse(MotionFbxPostprocessor.IsPathUnderOutputFolder(
                OutputFolder + "/x.fbx", string.Empty));
        }

        [Test]
        public void IsPathUnderOutputFolder_TrailingSlashOnFolder_StillMatches()
        {
            // Caller passes the folder with a trailing slash — still matches.
            Assert.IsTrue(MotionFbxPostprocessor.IsPathUnderOutputFolder(
                OutputFolder + "/x.fbx", OutputFolder + "/"));
        }

        // --- Integration: postprocessor actually flips animationType to Human ---

        [Test]
        public void Postprocessor_OnFbxInGeneratedFolder_SetsAnimationTypeHuman()
        {
            var path = OutputFolder + "/test_postproc_inside.fbx";
            _createdAssetPaths.Add(path);

            // Unity's FBX importer logs native Error-level messages when fed our
            // minimal-signature-only bytes ("ImportFBX Errors: File is corrupted",
            // and a Humanoid rig error because no Hips bone exists). Neither of
            // these is suppressed by LogAssert.ignoreFailingMessages in Unity 6 /
            // Test Framework 1.6 — they surface as "Unhandled log message" test
            // failures. Explicitly expect them so the framework treats them as
            // handled; they are orthogonal to what this test verifies (that the
            // preprocessor flipped animationType to Humanoid before the native
            // parse failed).
            LogAssert.Expect(LogType.Error, new Regex("ImportFBX Errors"));
            LogAssert.Expect(LogType.Error, new Regex("Rig Error"));

            WriteAndImport(path);

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.NotNull(importer, "Expected a ModelImporter for a .fbx path.");
            Assert.AreEqual(
                ModelImporterAnimationType.Human, importer.animationType,
                "Postprocessor should set animationType=Human on files inside the output folder.");
            Assert.IsFalse(importer.optimizeGameObjects,
                "Postprocessor should keep optimizeGameObjects=false so GetBoneTransform works.");
            Assert.IsTrue(importer.importAnimation,
                "Postprocessor should leave importAnimation=true.");
            Assert.IsFalse(importer.importVisibility,
                "Postprocessor should disable importVisibility on skeleton-only output.");
            Assert.IsFalse(importer.importCameras,
                "Postprocessor should disable importCameras on skeleton-only output.");
            Assert.IsFalse(importer.importLights,
                "Postprocessor should disable importLights on skeleton-only output.");
        }

        // --- ImportedAnimationLocator edge cases ---

        [Test]
        public void ImportedAnimationLocator_NullPath_ReturnsEmptyList()
        {
            var clips = ImportedAnimationLocator.GetAllClips(null);
            Assert.NotNull(clips);
            Assert.AreEqual(0, clips.Count);
        }

        [Test]
        public void ImportedAnimationLocator_EmptyPath_ReturnsEmptyList()
        {
            var clips = ImportedAnimationLocator.GetAllClips(string.Empty);
            Assert.NotNull(clips);
            Assert.AreEqual(0, clips.Count);
        }

        [Test]
        public void ImportedAnimationLocator_NonExistentPath_ReturnsEmptyList()
        {
            var clips = ImportedAnimationLocator.GetAllClips(
                OutputFolder + "/nonexistent_xyz.fbx");
            Assert.NotNull(clips);
            Assert.AreEqual(0, clips.Count);
        }

        [Test]
        public void ImportedAnimationLocator_GetClip_ReturnsNullForNullPath()
        {
            Assert.IsNull(ImportedAnimationLocator.GetClip(null));
        }

        [Test]
        public void ImportedAnimationLocator_GetClip_ReturnsNullForMissingFile()
        {
            Assert.IsNull(ImportedAnimationLocator.GetClip(
                OutputFolder + "/nonexistent_xyz.fbx"));
        }

        private static void WriteAndImport(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var bytes = new byte[256];
            Array.Copy(FbxBinarySignature, bytes, FbxBinarySignature.Length);
            File.WriteAllBytes(path, bytes);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            var parts = assetFolderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
