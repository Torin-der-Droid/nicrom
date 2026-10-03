using System.Threading.Tasks;
using NUnit.Framework;
using PromptMotion.Auth;
using PromptMotion.Http;
using PromptMotion.Models;
using PromptMotion.Windows;
using UnityEngine;

namespace PromptMotion.Tests
{
    public class DoubleFireGuardTests
    {
        private MockHttpTransport _transport;
        private PromptMotionWindow _window;

        [SetUp]
        public void SetUp()
        {
            _transport = new MockHttpTransport();
            _window = PromptMotionWindow.OpenForTest(_transport);
            _window.StateForTest.Auth = AuthState.Ready;
            // RestoreFromState during CreateGUI may fire a verify request if
            // EditorPrefs holds a real invoice from a prior session. Clear the
            // transport so only test-initiated requests are counted.
            _transport.Sent.Clear();
            _transport.SentGets.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) Object.DestroyImmediate(_window);
        }

        [Test]
        public async Task DoGenerate_CalledTwice_OnlyOneRequestFires()
        {
            EnqueueHappyPath("job_001");
            EnqueueHappyPath("job_002");

            var gate = new TaskCompletionSource<bool>();
            _transport.PostGate = gate;

            var t1 = _window.DoGenerateForTest("INV", "walk", 3, null);

            var t2 = _window.DoGenerateForTest("INV", "run", 3, null);
            await t2;

            Assert.AreEqual(1, _transport.Sent.Count,
                "Second DoGenerate should have been rejected by the _isGenerating guard.");

            gate.TrySetResult(true);
            await t1;
        }

        [Test]
        public async Task DoGenerate_SecondCallWhileFirstInFlight_IsGeneratingStaysConsistent()
        {
            EnqueueHappyPath("job_001");
            EnqueueHappyPath("job_002");

            var gate = new TaskCompletionSource<bool>();
            _transport.PostGate = gate;

            var t1 = _window.DoGenerateForTest("INV", "walk", 3, null);

            Assert.IsTrue(_window.IsGeneratingForTest,
                "_isGenerating should be true after first DoGenerate starts.");

            var t2 = _window.DoGenerateForTest("INV", "run", 3, null);
            await t2;

            gate.TrySetResult(true);
            await t1;

            Assert.IsFalse(_window.IsGeneratingForTest,
                "_isGenerating should be false after generation completes.");
        }

        private void EnqueueHappyPath(string jobId)
        {
            _transport.EnqueueOk("/prod/generate", new GenerateResult
            {
                JobId = jobId,
                FbxUrl = $"https://signed.test/{jobId}.fbx",
                DurationSeconds = 3,
                GenerationsRemaining = 49,
                Cached = false,
            });
            _transport.EnqueueBytesOk($"/{jobId}.fbx", new byte[] { 1, 2, 3 });
        }
    }
}
