using System;
using PromptMotion.Http;
using PromptMotion.Models;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Windows
{
    /// <summary>
    /// Auth state machine driving the Lite trial banner. Survives close+reopen
    /// via EditorPrefs so an activated user doesn't have to re-enter their email
    /// each session.
    ///
    /// Flow: <see cref="NeedEmail"/> → (send code) → <see cref="CodeSent"/> →
    /// (verify code) → <see cref="Ready"/>. <see cref="Busy"/> is the transient
    /// state shown while a request/verify round-trip is in flight.
    /// </summary>
    public enum AuthState
    {
        NeedEmail,
        CodeSent,
        Busy,
        Ready,
    }

    /// <summary>
    /// Serialisable state held by <see cref="PromptMotionWindow"/>.
    /// Round-trips through JsonUtility — keep field types primitive/serialisable.
    /// </summary>
    [Serializable]
    public class WindowState
    {
        public AuthState Auth = AuthState.NeedEmail;

        /// <summary>The email the user is activating (entered, awaiting code, or active).</summary>
        public string PendingEmail;

        public string Tier;
        public int MonthlyQuota;
        public int UsedThisMonth;

        // Populated by /prod/status. ISO-8601. For the trial tier the quota is a
        // lifetime counter with no reset, so this stays null — kept for envelope
        // parity with the paid status response.
        public string WindowResetsAt;

        // Last error surfaced to the banner. Cleared on successful state transitions.
        public string LastErrorCode;
        public string LastErrorMessage;

        // EditorPrefs is machine-wide, so a bare key would leak state between two
        // Unity projects on the same machine (e.g. a developer with both the Lite
        // and paid SKUs installed in separate projects). Namespace by the
        // project's dataPath hash so each project gets its own slot, under a
        // Lite-specific prefix so it never aliases the paid window's state.
        private static string PrefsKey =>
            "PromptMotion.Lite.WindowState." + Application.dataPath.GetHashCode();

        public static WindowState LoadOrDefault()
        {
            var key = PrefsKey;
            if (!EditorPrefs.HasKey(key)) return new WindowState();
            var json = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json)) return new WindowState();
            try
            {
                var state = JsonUtility.FromJson<WindowState>(json);
                return state ?? new WindowState();
            }
            catch
            {
                return new WindowState();
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this);
                EditorPrefs.SetString(PrefsKey, json);
            }
            catch
            {
                // Best-effort persistence; never throw out of a UI event handler.
            }
        }

        public static void ClearPersisted()
        {
            if (EditorPrefs.HasKey(PrefsKey)) EditorPrefs.DeleteKey(PrefsKey);
        }

        public void ApplyVerifyResult(VerifyResult data)
        {
            if (data == null) return;
            Tier = data.Tier;
            MonthlyQuota = data.MonthlyQuota;
            UsedThisMonth = data.UsedThisMonth;
        }

        public void ApplyStatusResult(StatusResult data)
        {
            if (data == null) return;
            Tier = data.Tier;
            MonthlyQuota = data.MonthlyQuota;
            UsedThisMonth = data.UsedThisMonth;
            WindowResetsAt = data.WindowResetsAt;
        }

        public void SetError(ApiError error)
        {
            if (error == null)
            {
                LastErrorCode = null;
                LastErrorMessage = null;
                return;
            }
            LastErrorCode = error.CodeRaw;
            LastErrorMessage = error.Message;
        }

        public void ClearError()
        {
            LastErrorCode = null;
            LastErrorMessage = null;
        }
    }
}
