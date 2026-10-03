using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Services
{
    /// <summary>
    /// One row in the Generator window's "Recent Generations" panel. Persisted
    /// per-user-per-machine via EditorPrefs JSON. Stale-detection compares the
    /// invoice captured at creation time so entries from a previous session
    /// render in muted form.
    /// </summary>
    [Serializable]
    internal class HistoryEntry
    {
        public long TimestampUnix;
        public string JobId;
        public string Prompt;
        public int DurationSeconds;
        public int Seed = -1;
        public string FbxAssetPath;
        public string InvoiceAtCreation;
    }

    [Serializable]
    internal class HistoryEntryList
    {
        public List<HistoryEntry> Entries = new List<HistoryEntry>();
    }

    /// <summary>
    /// EditorPrefs-backed list of the customer's recent generations (newest
    /// first, capped at <see cref="MaxEntries"/>). The store is per-user-per-
    /// machine by virtue of EditorPrefs — sync across machines is intentionally
    /// out of scope. Serialisation goes through JsonUtility, hence the wrapper
    /// list type (JsonUtility doesn't serialise bare List&lt;T&gt;).
    /// </summary>
    internal class HistoryStore
    {
        // Lite-namespaced so a Lite install's history never bleeds into a
        // Standard/Pro install on the same machine (EditorPrefs is machine-wide).
        public const string DefaultPrefsKey = "PromptMotion.Lite.History.v1";
        public const int MaxEntries = 10;

        private readonly string _prefsKey;
        private List<HistoryEntry> _entries = new List<HistoryEntry>();

        public HistoryStore() : this(DefaultPrefsKey) { }

        public HistoryStore(string prefsKey)
        {
            _prefsKey = string.IsNullOrEmpty(prefsKey) ? DefaultPrefsKey : prefsKey;
        }

        public IReadOnlyList<HistoryEntry> Entries => _entries;

        public void Load()
        {
            _entries = new List<HistoryEntry>();
            if (!EditorPrefs.HasKey(_prefsKey)) return;
            var json = EditorPrefs.GetString(_prefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var wrapper = JsonUtility.FromJson<HistoryEntryList>(json);
                if (wrapper?.Entries != null) _entries = wrapper.Entries;
            }
            catch
            {
                // Corrupt prefs blob — start fresh rather than throw.
                _entries = new List<HistoryEntry>();
            }
        }

        public void Save()
        {
            try
            {
                var wrapper = new HistoryEntryList { Entries = _entries };
                EditorPrefs.SetString(_prefsKey, JsonUtility.ToJson(wrapper));
            }
            catch
            {
                // Best-effort persistence; never throw out of a UI event handler.
            }
        }

        public void Add(HistoryEntry entry)
        {
            if (entry == null) return;
            _entries.Insert(0, entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            Save();
        }

        public void Clear()
        {
            _entries.Clear();
            Save();
        }

        /// <summary>
        /// True when the entry's invoice no longer matches the current session
        /// — i.e. it was generated under a different invoice. Treats a missing
        /// current invoice as stale (everything is suspect when nothing's
        /// activated).
        /// </summary>
        public bool IsStale(HistoryEntry entry, string currentInvoice)
        {
            if (entry == null) return true;
            if (string.IsNullOrEmpty(currentInvoice)) return true;
            return entry.InvoiceAtCreation != currentInvoice;
        }
    }
}
