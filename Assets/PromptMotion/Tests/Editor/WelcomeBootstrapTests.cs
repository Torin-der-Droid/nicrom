using NUnit.Framework;
using PromptMotion.Bootstrap;
using PromptMotion.Windows;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Tests
{
    public class WelcomeBootstrapTests
    {
        private bool _hadKey;
        private bool _previousValue;

        [SetUp]
        public void SetUp()
        {
            _hadKey = EditorPrefs.HasKey(WelcomeBootstrap.DismissedKey);
            _previousValue = EditorPrefs.GetBool(WelcomeBootstrap.DismissedKey, false);
            EditorPrefs.DeleteKey(WelcomeBootstrap.DismissedKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadKey)
                EditorPrefs.SetBool(WelcomeBootstrap.DismissedKey, _previousValue);
            else
                EditorPrefs.DeleteKey(WelcomeBootstrap.DismissedKey);
        }

        [Test]
        public void DismissedFlagSet_DoesNotTriggerPopup()
        {
            EditorPrefs.SetBool(WelcomeBootstrap.DismissedKey, true);

            Assert.IsFalse(WelcomeBootstrap.ShouldShow());
        }

        [Test]
        public void NotDismissed_TriggersPopup()
        {
            // Other guards (batch mode, compile, play mode) are environmental;
            // skip this assertion when any of them are active so the test
            // doesn't false-fail in CI.
            Assume.That(!Application.isBatchMode, "Skipped in batch mode.");
            Assume.That(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Skipped in play mode.");
            Assume.That(!EditorApplication.isCompiling, "Skipped while compiling.");
            Assume.That(!EditorApplication.isUpdating, "Skipped while updating.");
            Assume.That(Time.realtimeSinceStartup >= 2f,
                "Skipped — editor just started.");

            EditorPrefs.DeleteKey(WelcomeBootstrap.DismissedKey);

            Assert.IsTrue(WelcomeBootstrap.ShouldShow());
        }

        [Test]
        public void DontShowAgainChecked_PersistsToEditorPrefs()
        {
            WelcomePopup.WriteDismissalFlag(true);

            Assert.IsTrue(EditorPrefs.GetBool(WelcomeBootstrap.DismissedKey, false));
        }

        [Test]
        public void DontShowAgainUnchecked_DoesNotPersist()
        {
            EditorPrefs.SetBool(WelcomeBootstrap.DismissedKey, true);

            WelcomePopup.WriteDismissalFlag(false);

            Assert.IsFalse(EditorPrefs.HasKey(WelcomeBootstrap.DismissedKey));
        }

        [Test]
        public void ResetDismissalForTest_ClearsFlag()
        {
            EditorPrefs.SetBool(WelcomeBootstrap.DismissedKey, true);

            WelcomeBootstrap.ResetDismissalForTest();

            Assert.IsFalse(EditorPrefs.HasKey(WelcomeBootstrap.DismissedKey));
        }

        [Test]
        public void DismissedKey_IsPromptMotionScoped()
        {
            StringAssert.StartsWith("PromptMotion.", WelcomeBootstrap.DismissedKey);
        }
    }
}
