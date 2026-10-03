using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PromptMotion.Auth;
using PromptMotion.Http;
using PromptMotion.Models;
using PromptMotion.Settings;
using UnityEngine;

namespace PromptMotion.Tests
{
    public class TrialActivatorTests
    {
        private const string Nonce = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private PromptMotionSettings _settings;
        private MockHttpTransport _transport;
        private TrialActivator _activator;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PromptMotionSettings>();
            _settings.BaseUrl = "https://api.test";
            _transport = new MockHttpTransport();
            _activator = new TrialActivator(new AssetStoreApiClient(_settings, _transport));
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        [Test]
        public async Task RequestCodeAsync_Ok_ReturnsExpiry()
        {
            _transport.EnqueueOk("/prod/trial-request", new TrialRequestResult
            {
                Message = "Check your email for a 6-digit verification code.",
                ExpiresInSeconds = 900,
            });
            var outcome = await _activator.RequestCodeAsync("user@example.com", Nonce, CancellationToken.None);
            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(900, outcome.ExpiresInSeconds);
        }

        [Test]
        public async Task RequestCodeAsync_DisposableEmail_IsFailed()
        {
            _transport.EnqueueError("/prod/trial-request", new ApiError
            {
                CodeRaw = "DISPOSABLE_EMAIL",
                Message = "Disposable provider.",
            });
            var outcome = await _activator.RequestCodeAsync("x@mailinator.com", Nonce, CancellationToken.None);
            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(ApiErrorCode.DisposableEmail, outcome.Error.Code);
        }

        [Test]
        public async Task VerifyCodeAsync_Ok_IsVerifiedWithTrialTier()
        {
            _transport.EnqueueOk("/prod/trial-verify", new VerifyResult
            {
                Invoice = "user@example.com",
                Tier = "trial",
                MonthlyQuota = 5,
                UsedThisMonth = 0,
            });
            var result = await _activator.VerifyCodeAsync("user@example.com", "123456", Nonce, CancellationToken.None);
            Assert.AreEqual(VerifierOutcome.Verified, result.Outcome);
            Assert.AreEqual("trial", result.Data.Tier);
            Assert.AreEqual(5, result.Data.MonthlyQuota);
            Assert.AreEqual("user@example.com", result.Data.Invoice);
        }

        [Test]
        public async Task VerifyCodeAsync_BadCode_IsFailed()
        {
            _transport.EnqueueError("/prod/trial-verify", new ApiError
            {
                CodeRaw = "TRIAL_CODE_INVALID",
                Message = "That code isn't valid.",
            });
            var result = await _activator.VerifyCodeAsync("user@example.com", "000000", Nonce, CancellationToken.None);
            Assert.AreEqual(VerifierOutcome.Failed, result.Outcome);
            Assert.AreEqual(ApiErrorCode.TrialCodeInvalid, result.Error.Code);
        }
    }
}
