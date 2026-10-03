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
    public class AssetStoreApiClientTests
    {
        private const string Nonce = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private PromptMotionSettings _settings;
        private MockHttpTransport _transport;
        private AssetStoreApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PromptMotionSettings>();
            _settings.BaseUrl = "https://api.test";
            _settings.ClientVersion = "9.9.9";
            _settings.VerifyTimeoutSeconds = 7;
            _settings.StatusTimeoutSeconds = 8;
            _transport = new MockHttpTransport();
            _client = new AssetStoreApiClient(_settings, _transport);
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        [Test]
        public async Task TrialRequestAsync_BuildsCorrectUrl_AndSendsTrialHeaders()
        {
            _transport.EnqueueOk("/prod/trial-request", new TrialRequestResult
            {
                Message = "Check your email.",
                ExpiresInSeconds = 900,
            });

            var resp = await _client.TrialRequestAsync("user@example.com", Nonce, CancellationToken.None);

            Assert.IsTrue(resp.Ok);
            Assert.AreEqual(900, resp.Data.ExpiresInSeconds);

            var sent = _transport.Sent[0];
            Assert.AreEqual("https://api.test/prod/trial-request", sent.Url);
            Assert.AreEqual(7, sent.TimeoutSeconds);
            StringAssert.Contains("user@example.com", sent.BodyJson);
            StringAssert.Contains(Nonce, sent.BodyJson);

            Assert.IsFalse(sent.Headers.ContainsKey("X-Machine-Hash"));
            Assert.AreEqual("9.9.9", sent.Headers["X-Client-Version"]);
            Assert.AreEqual("trial", sent.Headers["X-PromptMotion-Auth-Type"]);
        }

        [Test]
        public async Task TrialVerifyAsync_BuildsCorrectUrl_AndPropagatesTrialState()
        {
            _transport.EnqueueOk("/prod/trial-verify", new VerifyResult
            {
                Invoice = "user@example.com",
                Tier = "trial",
                MonthlyQuota = 5,
                UsedThisMonth = 0,
            });

            var resp = await _client.TrialVerifyAsync("user@example.com", "123456", Nonce, CancellationToken.None);

            Assert.IsTrue(resp.Ok);
            Assert.AreEqual("trial", resp.Data.Tier);

            var sent = _transport.Sent[0];
            Assert.AreEqual("https://api.test/prod/trial-verify", sent.Url);
            StringAssert.Contains("123456", sent.BodyJson);
            Assert.AreEqual("trial", sent.Headers["X-PromptMotion-Auth-Type"]);
        }

        [Test]
        public async Task TrialVerifyAsync_PropagatesErrorEnvelope()
        {
            _transport.EnqueueError("/prod/trial-verify", new ApiError
            {
                CodeRaw = "TRIAL_CODE_INVALID",
                Message = "That code isn't valid.",
            });
            var resp = await _client.TrialVerifyAsync("user@example.com", "000000", Nonce, CancellationToken.None);
            Assert.IsFalse(resp.Ok);
            Assert.IsNotNull(resp.Error);
            Assert.AreEqual(ApiErrorCode.TrialCodeInvalid, resp.Error.Code);
        }

        [Test]
        public async Task StatusAsync_UsesStatusTimeout_AndCorrectPath()
        {
            _transport.EnqueueOk("/prod/status", new StatusResult
            {
                Invoice = "user@example.com",
                Tier = "trial",
                MonthlyQuota = 5,
                UsedThisMonth = 2,
                RemainingThisMonth = 3,
            });
            var resp = await _client.StatusAsync("user@example.com", CancellationToken.None);

            Assert.IsTrue(resp.Ok);
            Assert.AreEqual("trial", resp.Data.Tier);
            var sent = _transport.Sent[0];
            Assert.AreEqual("https://api.test/prod/status", sent.Url);
            Assert.AreEqual(8, sent.TimeoutSeconds);
            Assert.AreEqual("trial", sent.Headers["X-PromptMotion-Auth-Type"]);
        }

        [Test]
        public async Task TrialVerifyAsync_TrimsTrailingSlashOnBaseUrl()
        {
            _settings.BaseUrl = "https://api.test/";
            _transport.EnqueueOk("/prod/trial-verify", new VerifyResult
            {
                Invoice = "user@example.com",
                Tier = "trial",
                MonthlyQuota = 5,
            });
            await _client.TrialVerifyAsync("user@example.com", "123456", Nonce, CancellationToken.None);
            Assert.AreEqual("https://api.test/prod/trial-verify", _transport.Sent[0].Url);
        }
    }
}
