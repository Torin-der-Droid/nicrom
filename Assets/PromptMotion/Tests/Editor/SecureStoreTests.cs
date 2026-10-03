using System.Text.RegularExpressions;
using NUnit.Framework;
using PromptMotion.Auth;
using UnityEditor;

namespace PromptMotion.Tests
{
    public class SecureStoreTests
    {
        private const string NonceKey = "PromptMotion.Lite.ActivationNonce";

        private string _preserved;
        private bool _had;

        [SetUp]
        public void SetUp()
        {
            _had = SecureStore.HasEmail();
            _preserved = _had ? SecureStore.Load() : null;
            SecureStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_had) SecureStore.Save(_preserved);
            else SecureStore.Clear();
        }

        [Test]
        public void Save_ThenLoad_RoundTripsValue()
        {
            SecureStore.Save("user@example.com");
            Assert.AreEqual("user@example.com", SecureStore.Load());
            Assert.IsTrue(SecureStore.HasEmail());
        }

        [Test]
        public void Clear_RemovesValue()
        {
            SecureStore.Save("gone@example.com");
            SecureStore.Clear();
            Assert.IsNull(SecureStore.Load());
            Assert.IsFalse(SecureStore.HasEmail());
        }

        [Test]
        public void Load_WithNoSavedValue_ReturnsNull()
        {
            Assert.IsNull(SecureStore.Load());
        }

        [Test]
        public void Save_WithEmpty_ClearsValue()
        {
            SecureStore.Save("temp@example.com");
            SecureStore.Save(string.Empty);
            Assert.IsFalse(SecureStore.HasEmail());
        }

        [Test]
        public void Save_WithNull_ClearsValue()
        {
            SecureStore.Save("temp@example.com");
            SecureStore.Save(null);
            Assert.IsFalse(SecureStore.HasEmail());
        }

        [Test]
        public void GetOrCreateActivationNonce_IsStable_AndHexFormatted()
        {
            EditorPrefs.DeleteKey(NonceKey);
            try
            {
                var a = SecureStore.GetOrCreateActivationNonce();
                var b = SecureStore.GetOrCreateActivationNonce();
                Assert.AreEqual(a, b, "Nonce must be stable across calls for one install.");
                // Backend requires 40–128 hex chars; we mint 64.
                Assert.IsTrue(Regex.IsMatch(a, "^[0-9a-f]{40,128}$"),
                    $"Nonce '{a}' is not 40–128 lowercase hex characters.");
            }
            finally
            {
                EditorPrefs.DeleteKey(NonceKey);
            }
        }
    }
}
