using System.IO;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Settings
{
    /// <summary>
    /// Project-scoped settings for PromptMotion. Persisted as a ScriptableObject
    /// asset at <c>Assets/PromptMotion/Generator/Editor/Settings/Settings.asset</c>;
    /// auto-created on first access. Contains no secrets — safe to commit. The
    /// invoice number is stored separately in <see cref="Auth.SecureStore"/>.
    /// </summary>
    public class PromptMotionSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/PromptMotion/Generator/Editor/Settings/Settings.asset";

        // --- HTTP / production endpoint ---
        [SerializeField] private string _baseUrl = "https://api.promptmotion.dev";
        [SerializeField] private int _verifyTimeoutSeconds = 10;
        [SerializeField] private int _statusTimeoutSeconds = 10;
        // Backend's /prod/generate blocks until Kimodo finishes (~38s warm,
        // ~83s cold). 180s gives headroom for a cold start plus the FBX
        // download.
        [SerializeField] private int _generateTimeoutSeconds = 180;
        [SerializeField] private string _clientVersion = "1.0.0";

        // --- Generation defaults ---
        [SerializeField] private int _defaultDurationSeconds = 4;
        [SerializeField] private string _outputFolder = "Assets/PromptMotion/Generated";

        // --- Diagnostics ---
        [SerializeField] private bool _verboseLogging;

        public string BaseUrl
        {
            get => _baseUrl;
            set { if (_baseUrl == value) return; _baseUrl = value; Save(); }
        }

        public int VerifyTimeoutSeconds
        {
            get => _verifyTimeoutSeconds;
            set { if (_verifyTimeoutSeconds == value) return; _verifyTimeoutSeconds = value; Save(); }
        }

        public int StatusTimeoutSeconds
        {
            get => _statusTimeoutSeconds;
            set { if (_statusTimeoutSeconds == value) return; _statusTimeoutSeconds = value; Save(); }
        }

        public int GenerateTimeoutSeconds
        {
            get => _generateTimeoutSeconds;
            set { if (_generateTimeoutSeconds == value) return; _generateTimeoutSeconds = value; Save(); }
        }

        public string ClientVersion
        {
            get => string.IsNullOrEmpty(_clientVersion) ? "1.0.0" : _clientVersion;
            set { if (_clientVersion == value) return; _clientVersion = value; Save(); }
        }

        public int DefaultDurationSeconds
        {
            get => _defaultDurationSeconds;
            set { if (_defaultDurationSeconds == value) return; _defaultDurationSeconds = value; Save(); }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set { if (_outputFolder == value) return; _outputFolder = value; Save(); }
        }

        /// <summary>
        /// When true, <see cref="PromptMotion.Shared.Utilities.Log.Info"/> messages are
        /// written to the Unity Console. Warnings and errors always log regardless.
        /// </summary>
        public bool VerboseLogging
        {
            get => _verboseLogging;
            set { if (_verboseLogging == value) return; _verboseLogging = value; Save(); }
        }

        private static PromptMotionSettings _instance;

        /// <summary>Lazily-loaded singleton accessor. Creates the backing asset on first use.</summary>
        public static PromptMotionSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = AssetDatabase.LoadAssetAtPath<PromptMotionSettings>(AssetPath);
                if (_instance != null) return _instance;

                _instance = CreateInstance<PromptMotionSettings>();
                EnsureParentFolderExists(AssetPath);
                AssetDatabase.CreateAsset(_instance, AssetPath);
                AssetDatabase.SaveAssets();
                return _instance;
            }
        }

        private void Save()
        {
            // Guard against non-asset in-memory instances (e.g. test fixtures) so
            // setters don't attempt to persist to disk and pollute the project.
            if (!AssetDatabase.Contains(this)) return;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        private static void EnsureParentFolderExists(string assetPath)
        {
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (AssetDatabase.IsValidFolder(parent)) return;

            var parts = parent.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
