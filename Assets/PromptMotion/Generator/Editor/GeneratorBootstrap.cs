using PromptMotion.Settings;
using PromptMotion.Shared.Utilities;
using UnityEditor;

namespace PromptMotion
{
    /// <summary>
    /// One-shot domain-reload hook that wires Generator-specific dependencies
    /// into the Shared assembly. Today this only binds <see cref="Log.VerboseProvider"/>
    /// to <see cref="PromptMotionSettings.VerboseLogging"/> so moved code (now in
    /// <c>PromptMotion.Shared.Utilities</c>) keeps honoring the existing settings
    /// toggle without Shared taking a dependency on Settings.
    /// </summary>
    internal static class GeneratorBootstrap
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Log.VerboseProvider = IsVerboseLoggingEnabled;
        }

        private static bool IsVerboseLoggingEnabled()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PromptMotionSettings>(
                PromptMotionSettings.AssetPath);
            return settings != null && settings.VerboseLogging;
        }
    }
}
