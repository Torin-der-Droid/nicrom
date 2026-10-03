using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PromptMotion.Shared.Clips;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PromptMotion.Tests.Apply
{
    /// <summary>
    /// Covers <see cref="ClipApplier"/>'s pure helpers (Slugify, MakeUniqueName,
    /// ResolveControllerPath) and the create-or-load AnimatorController logic
    /// against real on-disk assets under a temp folder. Prefab-vs-scene
    /// branching is intentionally not exhaustively tested — too much Unity-
    /// internal state to set up reliably; the helpers + controller flow are
    /// what regress when ClipApplier changes.
    /// </summary>
    public class ClipApplierTests
    {
        private const string TempFolder = "Assets/Temp_PromptMotionTests";

        [SetUp]
        public void SetUp()
        {
            // All clip assets created in tests live under TempFolder so TearDown
            // can drop them with one DeleteAsset call.
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Temp_PromptMotionTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
            // Best-effort cleanup of any controllers tests created. Walks the
            // folder and deletes only files we recognise as test artefacts so
            // user-authored assets in that path aren't touched.
            if (AssetDatabase.IsValidFolder(ClipApplier.ControllerFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { ClipApplier.ControllerFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (name.StartsWith("unittestpawn_") ||
                        name.StartsWith("repeatpawn_") ||
                        name.StartsWith("dupepawn_"))
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }
            }
        }

        // --- Slugify ---

        [Test]
        public void Slugify_SimpleSpaces_BecomesUnderscores()
        {
            Assert.AreEqual("a_person_waves", ClipApplier.Slugify("a person waves"));
        }

        [Test]
        public void Slugify_PunctuationStrippedToUnderscore_TrailingUnderscoreRemoved()
        {
            Assert.AreEqual("a_person_picks_up_an_object",
                ClipApplier.Slugify("a person picks up an object!"));
        }

        [Test]
        public void Slugify_RepeatedAndLeadingTrailingSpaces_CollapsedAndStripped()
        {
            Assert.AreEqual("multiple_spaces", ClipApplier.Slugify("  multiple   spaces  "));
        }

        [Test]
        public void Slugify_Empty_ReturnsClipFallback()
        {
            Assert.AreEqual("clip", ClipApplier.Slugify(""));
        }

        [Test]
        public void Slugify_WhitespaceOnly_ReturnsClipFallback()
        {
            Assert.AreEqual("clip", ClipApplier.Slugify("   \t  \n "));
        }

        [Test]
        public void Slugify_Null_ReturnsClipFallback()
        {
            Assert.AreEqual("clip", ClipApplier.Slugify(null));
        }

        [Test]
        public void Slugify_OnlyPunctuation_ReturnsClipFallback()
        {
            Assert.AreEqual("clip", ClipApplier.Slugify("!!! @@@ ???"));
        }

        [Test]
        public void Slugify_LongString_TruncatesToFortyOrFewerChars()
        {
            var input = new string('a', 200);
            var result = ClipApplier.Slugify(input);
            Assert.LessOrEqual(result.Length, 40, $"truncated length must be <= 40, got {result.Length}");
            Assert.AreEqual(40, result.Length);
        }

        [Test]
        public void Slugify_TruncationDoesNotLeaveTrailingUnderscore()
        {
            // 39 letters + "_xyz" — slugifying truncates at 40 which lands on the "_"
            // boundary. Result must not end with underscore.
            var input = new string('a', 39) + " bcdef";
            var result = ClipApplier.Slugify(input);
            Assert.LessOrEqual(result.Length, 40);
            Assert.IsFalse(result.EndsWith("_"), $"slug should not end with '_': '{result}'");
        }

        [Test]
        public void Slugify_MixedCaseAndDigits_LowercasedDigitsKept()
        {
            Assert.AreEqual("walk_v2_take3", ClipApplier.Slugify("Walk V2 take3"));
        }

        // --- MakeUniqueName ---

        [Test]
        public void MakeUniqueName_NoConflict_ReturnsDesired()
        {
            Assert.AreEqual("walk", ClipApplier.MakeUniqueName(new[] { "run", "jump" }, "walk"));
        }

        [Test]
        public void MakeUniqueName_SingleConflict_AppendsTwo()
        {
            Assert.AreEqual("walk_2", ClipApplier.MakeUniqueName(new[] { "walk" }, "walk"));
        }

        [Test]
        public void MakeUniqueName_ChainedConflict_FindsNextFreeIndex()
        {
            Assert.AreEqual("walk_4",
                ClipApplier.MakeUniqueName(new[] { "walk", "walk_2", "walk_3" }, "walk"));
        }

        [Test]
        public void MakeUniqueName_EmptyExisting_ReturnsDesired()
        {
            Assert.AreEqual("walk", ClipApplier.MakeUniqueName(new List<string>(), "walk"));
        }

        [Test]
        public void MakeUniqueName_NullExisting_ReturnsDesired()
        {
            Assert.AreEqual("walk", ClipApplier.MakeUniqueName(null, "walk"));
        }

        // --- ResolveControllerPath ---

        [Test]
        public void ResolveControllerPath_SimpleName_ProducesExpectedPath()
        {
            Assert.AreEqual(
                ClipApplier.ControllerFolder + "/x_bot_Controller.controller",
                ClipApplier.ResolveControllerPath("X Bot"));
        }

        [Test]
        public void ResolveControllerPath_NameWithPunctuation_Slugifies()
        {
            Assert.AreEqual(
                ClipApplier.ControllerFolder + "/my_character_v1_Controller.controller",
                ClipApplier.ResolveControllerPath("My Character (v1)"));
        }

        // --- EnsureFolder ---

        [Test]
        public void EnsureFolder_CreatesNestedChainUnderAssets()
        {
            var path = TempFolder + "/Sub/Deep";
            Assert.IsFalse(AssetDatabase.IsValidFolder(path), "test precondition: folder must not exist");
            ClipApplier.EnsureFolder(path);
            Assert.IsTrue(AssetDatabase.IsValidFolder(path), $"folder was not created: {path}");
            Assert.IsTrue(AssetDatabase.IsValidFolder(TempFolder), "parent folder should also exist");
        }

        [Test]
        public void EnsureFolder_AlreadyExists_NoOp()
        {
            ClipApplier.EnsureFolder(TempFolder);
            ClipApplier.EnsureFolder(TempFolder); // second call: must not throw or duplicate
            Assert.IsTrue(AssetDatabase.IsValidFolder(TempFolder));
        }

        // --- AnimatorController create-or-load + state uniqueness ---

        [Test]
        public void Apply_CreatesControllerAtExpectedPath_AndAddsFirstStateAsDefault()
        {
            // Build a minimal scene character with no Animator component.
            var character = new GameObject("UnitTestPawn");
            try
            {
                var clip = new AnimationClip { name = "tmp" };
                AssetDatabase.CreateAsset(clip, TempFolder + "/clip.anim");

                var result = ClipApplier.Apply(character, clip, "walk forward");
                Assert.IsTrue(result.Success, $"Apply failed: {result.Message}");
                Assert.IsNotNull(result.Controller);

                var animator = character.GetComponent<Animator>();
                Assert.IsNotNull(animator, "Animator should have been added");
                Assert.AreEqual(result.Controller, animator.runtimeAnimatorController as AnimatorController);

                var sm = result.Controller.layers[0].stateMachine;
                Assert.AreEqual(1, sm.states.Length);
                Assert.AreEqual("walk_forward", sm.states[0].state.name);
                Assert.AreEqual(sm.states[0].state, sm.defaultState,
                    "first state added to an empty layer must become the default");
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void Apply_TwiceWithDifferentClipNames_AppendsSecondStateToSameController()
        {
            var character = new GameObject("RepeatPawn");
            try
            {
                var clip1 = new AnimationClip { name = "c1" };
                AssetDatabase.CreateAsset(clip1, TempFolder + "/clip1.anim");
                var clip2 = new AnimationClip { name = "c2" };
                AssetDatabase.CreateAsset(clip2, TempFolder + "/clip2.anim");

                var r1 = ClipApplier.Apply(character, clip1, "walk");
                Assert.IsTrue(r1.Success);
                var r2 = ClipApplier.Apply(character, clip2, "run");
                Assert.IsTrue(r2.Success);

                Assert.AreEqual(r1.Controller, r2.Controller, "second apply should re-use the existing controller");
                var states = r2.Controller.layers[0].stateMachine.states;
                Assert.AreEqual(2, states.Length);
                CollectionAssert.AreEquivalent(new[] { "walk", "run" },
                    states.Select(s => s.state.name).ToArray());
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void Apply_TwiceWithSameClipName_SecondStateIsSuffixed()
        {
            var character = new GameObject("DupePawn");
            try
            {
                var clip1 = new AnimationClip { name = "c1" };
                AssetDatabase.CreateAsset(clip1, TempFolder + "/a.anim");
                var clip2 = new AnimationClip { name = "c2" };
                AssetDatabase.CreateAsset(clip2, TempFolder + "/b.anim");

                var r1 = ClipApplier.Apply(character, clip1, "wave");
                Assert.IsTrue(r1.Success);
                var r2 = ClipApplier.Apply(character, clip2, "wave"); // same name -> _2 suffix
                Assert.IsTrue(r2.Success);

                var states = r2.Controller.layers[0].stateMachine.states;
                CollectionAssert.AreEquivalent(new[] { "wave", "wave_2" },
                    states.Select(s => s.state.name).ToArray());
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        // --- Defensive ---

        [Test]
        public void Apply_NullCharacter_ReturnsFailureNotException()
        {
            var clip = new AnimationClip { name = "x" };
            var r = ClipApplier.Apply(null, clip, "x");
            Assert.IsFalse(r.Success);
            Assert.IsNotNull(r.Message);
        }

        [Test]
        public void Apply_NullClip_ReturnsFailureNotException()
        {
            var go = new GameObject("NullClipPawn");
            try
            {
                var r = ClipApplier.Apply(go, null, "x");
                Assert.IsFalse(r.Success);
                Assert.IsNotNull(r.Message);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
