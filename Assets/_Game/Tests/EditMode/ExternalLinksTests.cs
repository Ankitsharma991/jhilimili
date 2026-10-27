using NUnit.Framework;
using UnityEngine;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests
{
    /// <summary>PRD 16: the privacy policy link opens only https pages, and the row needs a URL.</summary>
    public class ExternalLinksTests
    {
        [SetUp]
        public void Suppress() => ExternalLinks.SuppressForTests = true;

        [TearDown]
        public void Restore() => ExternalLinks.SuppressForTests = false;

        [Test]
        public void OnlyHttpsLinksOpen()
        {
            ExternalLinks.Open("https://example.org/privacy");
            Assert.AreEqual("https://example.org/privacy", ExternalLinks.LastOpened);
            ExternalLinks.Open("http://example.org/insecure");
            ExternalLinks.Open("intent://something");
            Assert.AreEqual("https://example.org/privacy", ExternalLinks.LastOpened, "anything else is ignored");
        }

        [Test]
        public void PrivacyRowNeedsAnHttpsUrl()
        {
            var links = ScriptableObject.CreateInstance<AppLinks>();
            Assert.IsFalse(links.HasPrivacyPolicy);
            links.privacyPolicyUrl = "http://example.org";
            Assert.IsFalse(links.HasPrivacyPolicy);
            links.privacyPolicyUrl = "https://example.org/privacy";
            Assert.IsTrue(links.HasPrivacyPolicy);
            Object.DestroyImmediate(links);
        }
    }
}
