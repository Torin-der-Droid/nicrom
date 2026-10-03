using NUnit.Framework;
using PromptMotion.Shared.Humanoid;
using UnityEngine;

namespace PromptMotion.Tests
{
    public class HumanoidAvatarFixerTests
    {
        [Test]
        public void AddAnimator_WhenMissing_AddsComponent()
        {
            var go = new GameObject("FixerTest_AddAnimator");
            try
            {
                Assert.IsNull(go.GetComponent<Animator>());
                var result = HumanoidAvatarFixer.AddAnimator(go);
                Assert.IsTrue(result.Success);
                Assert.IsNotNull(go.GetComponent<Animator>());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResetScale_NonUniform_ResetsToIdentity()
        {
            var go = new GameObject("FixerTest_ResetScale");
            try
            {
                go.transform.localScale = new Vector3(2f, 1f, 0.5f);
                var result = HumanoidAvatarFixer.ResetScale(go);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(Vector3.one, go.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ConvertFbxToHumanoid_NoFbxSource_ReturnsFailure()
        {
            var go = new GameObject("FixerTest_NoFbx");
            go.AddComponent<Animator>();
            try
            {
                var result = HumanoidAvatarFixer.ConvertFbxToHumanoid(go);
                Assert.IsFalse(result.Success);
                Assert.IsTrue(result.FailureReason.Contains("source FBX"),
                    $"Expected failure reason to mention 'source FBX', got: {result.FailureReason}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
