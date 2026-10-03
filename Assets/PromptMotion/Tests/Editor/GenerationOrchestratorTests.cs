using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PromptMotion.Auth;
using PromptMotion.Http;
using PromptMotion.Models;
using PromptMotion.Settings;
using PromptMotion.Windows;
using UnityEngine;

namespace PromptMotion.Tests
{
    public class GenerationOrchestratorTests
    {
        private PromptMotionSettings _settings;
        private MockHttpTransport _transport;
        private AssetStoreApiClient _client;
        private RecordingFbxSink _sink;
        private GenerationOrchestrator _orchestrator;

        private sealed class RecordingFbxSink : GenerationOrchestrator.IFbxSink
        {
            public List<(string jobId, byte[] bytes)> Saves = new List<(string, byte[])>();
            public string Save(string jobId, byte[] bytes)
            {
                Saves.Add((jobId, bytes));
                return $"Assets/PromptMotion/Generated/{jobId}.fbx";
            }
        }

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PromptMotionSettings>();
            _settings.BaseUrl = "https://api.test";
            _settings.GenerateTimeoutSeconds = 180;
            _transport = new MockHttpTransport();
            _client = new AssetStoreApiClient(_settings, _transport);
            _sink = new RecordingFbxSink();
            _orchestrator = new GenerationOrchestrator(_client, _sink);
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        [Test]
        public async Task RunAsync_HappyPath_FreshGeneration_SavesAndReturnsPath()
        {
            _transport.EnqueueOk("/prod/generate", new GenerateResult
            {
                JobId = "job_fresh_001",
                FbxUrl = "https://signed.test/job_fresh_001.fbx?token=abc",
                DurationSeconds = 3,
                GenerationsCharged = 1,
                GenerationsRemaining = 49,
                Cached = false,
            });
            var fakeBytes = System.Text.Encoding.UTF8.GetBytes("FBX_BINARY_PAYLOAD");
            _transport.EnqueueBytesOk("/job_fresh_001.fbx", fakeBytes);

            var outcome = await _orchestrator.RunAsync(
                "IN_TEST_001", "walking forward", 3, seed: null, ct: CancellationToken.None);

            Assert.IsTrue(outcome.Ok, outcome.Error?.Message);
            Assert.IsFalse(outcome.Cached);
            Assert.AreEqual(49, outcome.GenerationsRemaining);
            Assert.AreEqual("job_fresh_001", outcome.JobId);
            StringAssert.Contains("job_fresh_001.fbx", outcome.FbxPath);
            Assert.AreEqual(1, _sink.Saves.Count);
            CollectionAssert.AreEqual(fakeBytes, _sink.Saves[0].bytes);
        }

        [Test]
        public async Task RunAsync_HappyPath_CacheHit_PreservesFlag()
        {
            _transport.EnqueueOk("/prod/generate", new GenerateResult
            {
                JobId = "job_cached_002",
                FbxUrl = "https://signed.test/job_cached_002.fbx",
                DurationSeconds = 3,
                GenerationsCharged = 0,
                GenerationsRemaining = 49,
                Cached = true,
            });
            _transport.EnqueueBytesOk("/job_cached_002.fbx", new byte[] { 1, 2, 3 });

            var outcome = await _orchestrator.RunAsync(
                "IN_X", "walking", 3, seed: null, ct: CancellationToken.None);

            Assert.IsTrue(outcome.Ok);
            Assert.IsTrue(outcome.Cached);
            Assert.AreEqual(49, outcome.GenerationsRemaining);
        }

        [Test]
        public async Task RunAsync_QuotaExhausted_ReturnsErrorWithoutDownloading()
        {
            _transport.EnqueueError("/prod/generate", new ApiError
            {
                CodeRaw = "QUOTA_EXHAUSTED",
                Message = "Monthly quota reached.",
                Details = new Dictionary<string, object> { ["monthly_quota"] = 50 },
            });

            var outcome = await _orchestrator.RunAsync(
                "IN_X", "walking", 3, seed: null, ct: CancellationToken.None);

            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(ApiErrorCode.QuotaExhausted, outcome.Error.Code);
            Assert.AreEqual(0, _sink.Saves.Count);
            Assert.AreEqual(0, _transport.SentGets.Count);
        }

        [Test]
        public async Task RunAsync_ServerError_ReturnsErrorWithoutDownloading()
        {
            _transport.EnqueueError("/prod/generate", new ApiError
            {
                CodeRaw = "SERVER_ERROR",
                Message = "boom",
            });

            var outcome = await _orchestrator.RunAsync(
                "IN_X", "walking", 3, seed: null, ct: CancellationToken.None);

            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(ApiErrorCode.ServerError, outcome.Error.Code);
            Assert.AreEqual(0, _sink.Saves.Count);
        }

        [Test]
        public async Task RunAsync_EmptyPrompt_FailsWithoutHittingNetwork()
        {
            var outcome = await _orchestrator.RunAsync(
                "IN_X", "   ", 3, seed: null, ct: CancellationToken.None);

            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(ApiErrorCode.BadRequest, outcome.Error.Code);
            Assert.AreEqual(0, _transport.Sent.Count);
            Assert.AreEqual(0, _sink.Saves.Count);
        }
    }
}
