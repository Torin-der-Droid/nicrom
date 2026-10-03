using System;
using UnityEngine;

namespace PromptMotion.Shared.Utilities
{
    /// <summary>
    /// Thin wrapper around <see cref="Debug"/> that prefixes every message with
    /// <c>[PromptMotion]</c> so tool output is easy to filter in the Unity Console.
    ///
    /// <para>
    /// <see cref="Info"/> is gated on <see cref="VerboseProvider"/>, a delegate
    /// each consuming product registers at startup. The Generator binds it to
    /// its <c>PromptMotionSettings.VerboseLogging</c> toggle; the Library will
    /// register its own provider when it ships. With no provider registered the
    /// gate defaults to quiet.
    /// </para>
    /// <para>
    /// The delegate indirection keeps this Shared assembly from depending on
    /// any product-specific settings asset.
    /// </para>
    /// </summary>
    public static class Log
    {
        private const string Prefix = "[PromptMotion] ";

        /// <summary>
        /// Gate for <see cref="Info"/>. Returning <c>true</c> emits info messages;
        /// returning <c>false</c> suppresses them. Defaults to a constant-false
        /// lambda so unconfigured callers stay quiet.
        /// </summary>
        public static Func<bool> VerboseProvider { get; set; } = () => false;

        /// <summary>Logs an informational message, gated on <see cref="VerboseProvider"/>.</summary>
        public static void Info(string message)
        {
            if (!IsVerbose()) return;
            Debug.Log(Prefix + message);
        }

        /// <summary>Logs a warning. Always emitted.</summary>
        public static void Warn(string message) => Debug.LogWarning(Prefix + message);

        /// <summary>Logs an error. Always emitted.</summary>
        public static void Error(string message) => Debug.LogError(Prefix + message);

        private static bool IsVerbose()
        {
            var provider = VerboseProvider;
            if (provider == null) return false;
            try
            {
                return provider();
            }
            catch
            {
                return false;
            }
        }
    }
}
