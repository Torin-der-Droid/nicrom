using System;
using System.Security.Cryptography;
using UnityEditor;

namespace PromptMotion.Auth
{
    /// <summary>
    /// Persists the Lite tier's activation identity in <see cref="EditorPrefs"/>:
    /// the customer's email (the "identifier" the backend resolves against
    /// <c>email_trials</c>) and a per-install activation nonce that ties the
    /// trial-request and trial-verify steps together.
    ///
    /// Neither value is a secret — the email is what the customer typed, and the
    /// nonce only de-dupes activation attempts — so both are stored in plaintext.
    /// Keys are namespaced under <c>PromptMotion.Lite.*</c> so a paid PromptMotion
    /// install on the same machine (which stores an invoice under its own key)
    /// can't collide with the free trial.
    /// </summary>
    public static class SecureStore
    {
        private const string EmailKey = "PromptMotion.Lite.Email";
        private const string NonceKey = "PromptMotion.Lite.ActivationNonce";

        /// <summary>Raised whenever the stored email changes (saved or cleared).</summary>
        public static event Action EmailChanged;

        public static void Save(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                Clear();
                return;
            }
            EditorPrefs.SetString(EmailKey, email);
            EmailChanged?.Invoke();
        }

        public static string Load()
        {
            if (!EditorPrefs.HasKey(EmailKey)) return null;
            var value = EditorPrefs.GetString(EmailKey, string.Empty);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public static bool HasEmail() => !string.IsNullOrEmpty(Load());

        /// <summary>
        /// Clears the activated email. The activation nonce is intentionally
        /// preserved: it identifies this install for the lifetime of the trial,
        /// so re-activating with the same nonce lets the backend recognise a
        /// repeat attempt rather than minting a second free trial.
        /// </summary>
        public static void Clear()
        {
            if (!EditorPrefs.HasKey(EmailKey)) return;
            EditorPrefs.DeleteKey(EmailKey);
            EmailChanged?.Invoke();
        }

        /// <summary>
        /// Returns this install's activation nonce, generating and persisting a
        /// fresh 64-hex-char value on first use. The format satisfies the
        /// backend's 40–128 hex-character requirement.
        /// </summary>
        public static string GetOrCreateActivationNonce()
        {
            if (EditorPrefs.HasKey(NonceKey))
            {
                var existing = EditorPrefs.GetString(NonceKey, string.Empty);
                if (!string.IsNullOrEmpty(existing)) return existing;
            }
            var nonce = NewNonce();
            EditorPrefs.SetString(NonceKey, nonce);
            return nonce;
        }

        private static string NewNonce()
        {
            var bytes = new byte[32]; // 32 bytes → 64 hex chars
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (var i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = hex[bytes[i] >> 4];
                chars[i * 2 + 1] = hex[bytes[i] & 0xF];
            }
            return new string(chars);
        }
    }
}
