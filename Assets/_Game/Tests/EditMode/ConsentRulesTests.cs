using NUnit.Framework;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Tests
{
    /// <summary>PRD 15.2 and 16: consent gates analytics; the Settings line explains the state.</summary>
    public class ConsentRulesTests
    {
        [TestCase(true, ConsentStatus.NotRequired, true)]
        [TestCase(true, ConsentStatus.Obtained, true)]
        [TestCase(true, ConsentStatus.Required, false)]
        [TestCase(true, ConsentStatus.Unknown, false)]
        [TestCase(false, ConsentStatus.NotRequired, false)]
        [TestCase(false, ConsentStatus.Obtained, false)]
        public void Analytics_NeedsTheSettingAndAnAnsweredConsent(bool share, ConsentStatus status, bool expected)
        {
            Assert.AreEqual(expected, ConsentRules.AnalyticsAllowed(share, status));
        }

        [Test]
        public void Describe_GivesAPlainLinePerState()
        {
            StringAssert.Contains("Change", ConsentRules.Describe(ConsentStatus.Obtained, true));
            StringAssert.Contains("region", ConsentRules.Describe(ConsentStatus.NotRequired, false));
            StringAssert.Contains("Choose", ConsentRules.Describe(ConsentStatus.Required, true));
            StringAssert.Contains("Checking", ConsentRules.Describe(ConsentStatus.Unknown, false));
        }
    }
}
