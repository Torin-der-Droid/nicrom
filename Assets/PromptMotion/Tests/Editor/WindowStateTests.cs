using NUnit.Framework;
using PromptMotion.Http;
using PromptMotion.Models;
using PromptMotion.Windows;

namespace PromptMotion.Tests
{
    public class WindowStateTests
    {
        [SetUp]
        public void SetUp() => WindowState.ClearPersisted();

        [TearDown]
        public void TearDown() => WindowState.ClearPersisted();

        [Test]
        public void LoadOrDefault_NoPersisted_ReturnsDefault()
        {
            var s = WindowState.LoadOrDefault();
            Assert.AreEqual(AuthState.NeedEmail, s.Auth);
        }

        [Test]
        public void Save_RoundTripsThroughEditorPrefs()
        {
            var s = new WindowState
            {
                Auth = AuthState.Ready,
                PendingEmail = "user@example.com",
                Tier = "trial",
                UsedThisMonth = 2,
                MonthlyQuota = 5,
                WindowResetsAt = null,
            };
            s.Save();
            var loaded = WindowState.LoadOrDefault();
            Assert.AreEqual(AuthState.Ready, loaded.Auth);
            Assert.AreEqual("user@example.com", loaded.PendingEmail);
            Assert.AreEqual("trial", loaded.Tier);
            Assert.AreEqual(2, loaded.UsedThisMonth);
            Assert.AreEqual(5, loaded.MonthlyQuota);
        }

        [Test]
        public void ApplyVerifyResult_PullsFieldsFromData()
        {
            var s = new WindowState();
            s.ApplyVerifyResult(new VerifyResult
            {
                Tier = "trial",
                MonthlyQuota = 5,
                UsedThisMonth = 1,
            });
            Assert.AreEqual("trial", s.Tier);
            Assert.AreEqual(5, s.MonthlyQuota);
            Assert.AreEqual(1, s.UsedThisMonth);
        }

        [Test]
        public void SetError_AndClearError_BehaveSymmetrically()
        {
            var s = new WindowState();
            s.SetError(new ApiError { CodeRaw = "TRIAL_CODE_INVALID", Message = "x" });
            Assert.AreEqual("TRIAL_CODE_INVALID", s.LastErrorCode);
            s.ClearError();
            Assert.IsNull(s.LastErrorCode);
            Assert.IsNull(s.LastErrorMessage);
        }
    }
}
