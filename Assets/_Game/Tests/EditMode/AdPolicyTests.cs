using NUnit.Framework;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Tests
{
    /// <summary>PRD 15.1 ad rules (F10).</summary>
    public class AdPolicyTests
    {
        private static readonly AdScene Idle = new AdScene(false, false, false);

        private static AdPolicy Ready(float sessionStart = 0f)
        {
            var p = new AdPolicy(sessionStart) { InterstitialsEnabled = true };
            for (int i = 0; i < AdPolicy.MinIgnitionsBetween; i++)
            {
                p.RecordIgnition();
            }

            return p;
        }

        [Test]
        public void Interstitial_AfterSixIgnitionsAndFourMinutes()
        {
            AdPolicy p = Ready();
            Assert.AreEqual(AdBlock.None, p.Check(AdKind.Interstitial, 240f, Idle));
        }

        [Test]
        public void Interstitial_NeverAtLaunch()
        {
            AdPolicy p = Ready(sessionStart: 100f);
            Assert.AreEqual(AdBlock.TooSoonAfterAd, p.Check(AdKind.Interstitial, 100f, Idle));
            Assert.AreEqual(AdBlock.TooSoonAfterAd, p.Check(AdKind.Interstitial, 339f, Idle));
            Assert.AreEqual(AdBlock.None, p.Check(AdKind.Interstitial, 340f, Idle));
        }

        [Test]
        public void Interstitial_NeedsSixIgnitionsSinceTheLastAd()
        {
            AdPolicy p = Ready();
            p.RecordAdShown(240f);
            Assert.AreEqual(0, p.IgnitionsSinceAd);
            for (int i = 0; i < 5; i++)
            {
                p.RecordIgnition();
            }

            Assert.AreEqual(AdBlock.TooFewIgnitions, p.Check(AdKind.Interstitial, 600f, Idle));
            p.RecordIgnition();
            Assert.AreEqual(AdBlock.None, p.Check(AdKind.Interstitial, 600f, Idle));
        }

        [Test]
        public void Interstitial_NotWithinTenSecondsOfAPayoff()
        {
            AdPolicy p = Ready();
            p.RecordPayoff(300f);
            Assert.AreEqual(AdBlock.TooSoonAfterPayoff, p.Check(AdKind.Interstitial, 309.9f, Idle));
            Assert.AreEqual(AdBlock.None, p.Check(AdKind.Interstitial, 310f, Idle));
            Assert.AreEqual(10f, p.SecondsSincePayoff(310f), 1e-4f);
        }

        [TestCase(true, false, false, AdBlock.CrackerActive)]
        [TestCase(false, true, false, AdBlock.Placing)]
        [TestCase(false, false, true, AdBlock.FinaleRunning)]
        public void NoAdOfAnyKind_DuringFusePayoffPlacementOrFinale(bool active, bool placing, bool finale, AdBlock expected)
        {
            AdPolicy p = Ready();
            var scene = new AdScene(active, placing, finale);
            Assert.AreEqual(expected, p.Check(AdKind.Interstitial, 1000f, scene));
            Assert.AreEqual(expected, p.Check(AdKind.Rewarded, 1000f, scene));
        }

        [Test]
        public void Interstitial_NeverRightAfterATap()
        {
            AdPolicy p = Ready();
            Assert.AreEqual(AdBlock.RecentTouch, p.Check(AdKind.Interstitial, 1000f, new AdScene(false, false, false, 2.9f)));
            Assert.AreEqual(AdBlock.None, p.Check(AdKind.Interstitial, 1000f, new AdScene(false, false, false, 3f)));
            Assert.AreEqual(AdBlock.None, p.Check(AdKind.Rewarded, 1000f, new AdScene(false, false, false, 0f)), "rewarded follows the player's own tap");
        }

        [Test]
        public void Rewarded_IgnoresCapsBecauseThePlayerAskedForIt()
        {
            var p = new AdPolicy(0f);
            p.RecordPayoff(0f);
            Assert.AreEqual(AdBlock.None, p.Check(AdKind.Rewarded, 1f, Idle));
        }

        [Test]
        public void FlagsPurchaseAndConsent()
        {
            AdPolicy p = Ready();
            p.InterstitialsEnabled = false;
            Assert.AreEqual(AdBlock.FeatureOff, p.Check(AdKind.Interstitial, 1000f, Idle));
            Assert.AreEqual(AdBlock.None, p.Check(AdKind.Rewarded, 1000f, Idle), "the interstitial flag leaves rewarded alone");
            p.ConsentAllowsAds = false;
            Assert.AreEqual(AdBlock.NoConsent, p.Check(AdKind.Rewarded, 1000f, Idle));
            p.AdsRemoved = true;
            Assert.AreEqual(AdBlock.AdsRemoved, p.Check(AdKind.Rewarded, 1000f, Idle));
            p.AdsEnabled = false;
            Assert.AreEqual(AdBlock.FeatureOff, p.Check(AdKind.Rewarded, 1000f, Idle));
        }

        [Test]
        public void SecondsSincePayoff_IsMinusOneBeforeAnyPayoff()
        {
            Assert.AreEqual(-1f, new AdPolicy(0f).SecondsSincePayoff(50f));
        }

        [Test]
        public void Messages_ExplainBlockedRewardedAds()
        {
            StringAssert.Contains("fireworks", AdPolicy.Message(AdBlock.CrackerActive));
            StringAssert.Contains("Place", AdPolicy.Message(AdBlock.Placing));
            Assert.IsEmpty(AdPolicy.Message(AdBlock.None));
        }
    }
}
