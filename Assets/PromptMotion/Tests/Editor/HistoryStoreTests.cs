using NUnit.Framework;
using PromptMotion.Services;
using UnityEditor;

namespace PromptMotion.Tests
{
    public class HistoryStoreTests
    {
        // Isolated prefs key so tests never touch the real
        // "PromptMotion.History.v1" the user's Generator window writes to.
        private const string TestPrefsKey = "PromptMotion.History.v1.test";

        [SetUp]
        public void SetUp()
        {
            if (EditorPrefs.HasKey(TestPrefsKey)) EditorPrefs.DeleteKey(TestPrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (EditorPrefs.HasKey(TestPrefsKey)) EditorPrefs.DeleteKey(TestPrefsKey);
        }

        private static HistoryEntry MakeEntry(string prompt, string invoice = "IN_TEST")
        {
            return new HistoryEntry
            {
                TimestampUnix = 1700000000,
                JobId = "j_" + prompt,
                Prompt = prompt,
                DurationSeconds = 4,
                Seed = -1,
                FbxAssetPath = $"Assets/PromptMotion/Generated/j_{prompt}.fbx",
                InvoiceAtCreation = invoice,
            };
        }

        [Test]
        public void Add_AppendsNewestFirst()
        {
            var store = new HistoryStore(TestPrefsKey);
            store.Load();

            store.Add(MakeEntry("A"));
            store.Add(MakeEntry("B"));
            store.Add(MakeEntry("C"));

            Assert.AreEqual(3, store.Entries.Count);
            Assert.AreEqual("C", store.Entries[0].Prompt);
            Assert.AreEqual("B", store.Entries[1].Prompt);
            Assert.AreEqual("A", store.Entries[2].Prompt);
        }

        [Test]
        public void Add_CapsAtTen()
        {
            var store = new HistoryStore(TestPrefsKey);
            store.Load();

            for (var i = 0; i < 12; i++)
                store.Add(MakeEntry("p" + i));

            Assert.AreEqual(HistoryStore.MaxEntries, store.Entries.Count);
            // Newest (p11) at index 0; oldest survivor is p2 (p0 and p1 dropped).
            Assert.AreEqual("p11", store.Entries[0].Prompt);
            Assert.AreEqual("p2", store.Entries[HistoryStore.MaxEntries - 1].Prompt);
        }

        [Test]
        public void IsStale_DifferentInvoice_ReturnsTrue()
        {
            var store = new HistoryStore(TestPrefsKey);
            var entry = MakeEntry("walking", invoice: "IN_001");

            Assert.IsTrue(store.IsStale(entry, currentInvoice: "IN_002"));
            Assert.IsFalse(store.IsStale(entry, currentInvoice: "IN_001"));
        }

        [Test]
        public void IsStale_NoCurrentInvoice_ReturnsTrue()
        {
            var store = new HistoryStore(TestPrefsKey);
            var entry = MakeEntry("walking", invoice: "IN_001");

            Assert.IsTrue(store.IsStale(entry, currentInvoice: null));
            Assert.IsTrue(store.IsStale(entry, currentInvoice: ""));
        }

        [Test]
        public void LoadAfterSave_RoundTrip()
        {
            var first = new HistoryStore(TestPrefsKey);
            first.Load();
            first.Add(MakeEntry("A"));
            first.Add(MakeEntry("B"));
            first.Add(MakeEntry("C"));

            var second = new HistoryStore(TestPrefsKey);
            second.Load();

            Assert.AreEqual(3, second.Entries.Count);
            Assert.AreEqual("C", second.Entries[0].Prompt);
            Assert.AreEqual("B", second.Entries[1].Prompt);
            Assert.AreEqual("A", second.Entries[2].Prompt);
            Assert.AreEqual("j_A", second.Entries[2].JobId);
            Assert.AreEqual("Assets/PromptMotion/Generated/j_A.fbx", second.Entries[2].FbxAssetPath);
        }
    }
}
