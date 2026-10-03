using System.Reflection;
using NUnit.Framework;
using PromptMotion.Windows;
using UnityEditor;

namespace PromptMotion.Tests
{
    public class PromptBestPracticesWindowTests
    {
        [Test]
        public void Open_DoesNotThrow()
        {
            PromptBestPracticesWindow.Open();
            Assert.IsTrue(EditorWindow.HasOpenInstances<PromptBestPracticesWindow>());

            var window = EditorWindow.GetWindow<PromptBestPracticesWindow>();
            window.Close();
        }

        [Test]
        public void MenuItem_IsRegistered()
        {
            var method = typeof(PromptBestPracticesWindow).GetMethod(
                "Open",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method, "Open method not found");

            var attr = method.GetCustomAttribute<MenuItem>();
            Assert.IsNotNull(attr, "MenuItem attribute missing");
            Assert.AreEqual("Window/PromptMotion/Prompt Best Practices", attr.menuItem);
        }
    }
}
