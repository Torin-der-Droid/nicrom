using System.Collections.Generic;
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
    public class AssetStoreApiClientGenerateTests
    {
        private PromptMotionSettings _settings;
        private MockHttpTransport _transport;
        private AssetStoreApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PromptMotionSettings>();
            _settings.BaseUrl = "https://api.test";
            _settings.ClientVersion = "9.9.9";
            _settings.GenerateTimeoutSeconds = 180;
            _transport = new MockHttpTransport();
            _client = new AssetStoreApiClient(_settings, _transport);
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        [Test]
        public async Task GenerateAsync_HappyPath_ParsesEnvelope()
        {
            _transport.EnqueueOk("/prod/generate", new GenerateResult
            {
                JobId = "job_abc123",
                FbxUrl = "https://signed.test/job_abc123.fbx?token=xyz",
                DurationSeconds = 3,
                GenerationsCharged = 1,
                GenerationsRemaining = 49,
                Cached = false,
                CreatedAt = "2026-04-27T12:00:00Z",
            });

            var resp = await _client.GenerateAsync("IN_TEST_001", "walking forward", 3);

            Assert.IsTrue(resp.Ok);
            Assert.AreEqual("job_abc123", resp.Data.JobId);
            Assert.IsFalse(resp.Data.Cached);
            Assert.AreEqual(1, resp.Data.GenerationsCharged);
            Assert.AreEqual(49, resp.Data.GenerationsRemaining);

            var sent = _transport.Sent[0];
            Assert.AreEqual("https://api.test/prod/generate", sent.Url);
            Assert.AreEqual(180, sent.TimeoutSeconds);
            StringAssert.Contains("\"invoice\":\"IN_TEST_001\"", sent.BodyJson);
            StringAssert.Contains("\"prompt\":\"walking forward\"", sent.BodyJson);
            StringAssert.Contains("\"duration_seconds\":3", sent.BodyJson);
            StringAssert.Contains("\"skeleton\":\"humanoid\"", sent.BodyJson);
            StringAssert.Contains("\"format\":\"fbx\"", sent.BodyJson);

            Assert.IsFalse(sent.Headers.ContainsKey("X-Machine-Hash"));
            Assert.AreEqual("9.9.9", sent.Headers["X-Client-Version"]);
        }

        [Test]
        public async Task GenerateAsync_CacheHit_PreservesCachedFlag()
        {
            _transport.EnqueueOk("/prod/generate", new GenerateResult
            {
                JobId = "job_cached",
                FbxUrl = "https://signed.test/job_cached.fbx",
                DurationSeconds = 3,
                GenerationsCharged = 0,
                GenerationsRemaining = 49,
                Cached = true,
            });

            var resp = await _client.GenerateAsync("IN_X", "walking", 3);

            Assert.IsTrue(resp.Ok);
            Assert.IsTrue(resp.Data.Cached);
            Assert.AreEqual(0, resp.Data.GenerationsCharged);
            Assert.AreEqual(49, resp.Data.GenerationsRemaining);
        }

        [Test]
        public async Task GenerateAsync_ServerError_ParsesErrorEnvelope()
        {
            _transport.EnqueueError("/prod/generate", new ApiError
            {
                CodeRaw = "SERVER_ERROR",
                Message = "Unhandled exception in generator.",
            });

            var resp = await _client.GenerateAsync("IN_X", "walking", 3);

            Assert.IsFalse(resp.Ok);
            Assert.IsNotNull(resp.Error);
            Assert.AreEqual(ApiErrorCode.ServerError, resp.Error.Code);
        }

        [Test]
        public async Task GenerateAsync_QuotaExhausted_ParsesDetails()
        {
            _transport.EnqueueError("/prod/generate", new ApiError
            {
                CodeRaw = "QUOTA_EXHAUSTED",
                Message = "Monthly quota reached.",
                Details = new Dictionary<string, object>
                {
                    ["resets_at"] = "2026-05-15T00:00:00Z",
                    ["monthly_quota"] = 50,
                },
            });

            var resp = await _client.GenerateAsync("IN_X", "walking", 3);

            Assert.IsFalse(resp.Ok);
            Assert.AreEqual(ApiErrorCode.QuotaExhausted, resp.Error.Code);
            Assert.AreEqual("2026-05-15T00:00:00Z", resp.Error.Details["resets_at"]);
        }

        [Test]
        public async Task GenerateAsync_ServiceUnavailable_PopulatesRetryAfter()
        {
            _transport.EnqueueError("/prod/generate", new ApiError
            {
                CodeRaw = "SERVICE_UNAVAILABLE",
                Message = "Backend warming up.",
                RetryAfterSeconds = 60,
            });

            var resp = await _client.GenerateAsync("IN_X", "walking", 3);

            Assert.IsFalse(resp.Ok);
            Assert.AreEqual(ApiErrorCode.ServiceUnavailable, resp.Error.Code);
            Assert.AreEqual(60, resp.Error.RetryAfterSeconds);
        }

        [Test]
        public void GenerateAsync_TransportFailure_SurfacesException()
        {
            // The mock transport doesn't have a "throw on POST" path right now;
            // network-level failures are what UnityWebRequestHttpTransport
            // converts into NetworkError/Timeout envelopes. Here we exercise
            // the exception surface: with no canned response queued, the mock
            // throws — the orchestrator (one layer up) catches and folds these
            // into an ApiError, but at the client layer the exception propagates.
            Assert.ThrowsAsync<System.InvalidOperationException>(
                () => _client.GenerateAsync("IN_X", "walking", 3));
        }
    }
}
