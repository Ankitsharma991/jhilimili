using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Release;

namespace UtsavNights.Tests
{
    /// <summary>Phase 5 step 1: the store build preflight stops uploads Play would reject or players would be harmed by.</summary>
    public class ReleaseChecksTests
    {
        private static ReleaseInputs Ready(ReleaseTrack track) => new ReleaseInputs
        {
            Track = track,
            VersionName = "0.1.0",
            VersionCode = 2,
            LastStoreVersionCode = 1,
            KeystoreFound = true,
            KeystorePasswordsSet = true,
            AdsSdkInstalled = true,
            PurchasingSdkInstalled = true,
            AnalyticsSdkInstalled = true,
            AdsUseTestIds = false,
            DebugGeographyEea = false,
            HasPrivacyPolicy = true,
            TargetSdk = 36,
        };

        private static string[] Ids(ReleaseInputs input) => ReleaseChecks.Evaluate(input).Select(c => c.Id).ToArray();

        private static CheckLevel LevelOf(ReleaseInputs input, string id) =>
            ReleaseChecks.Evaluate(input).Single(c => c.Id == id).Level;

        [TestCase(ReleaseTrack.Internal)]
        [TestCase(ReleaseTrack.Closed)]
        [TestCase(ReleaseTrack.Production)]
        public void ReadyProject_HasNoChecks(ReleaseTrack track)
        {
            CollectionAssert.IsEmpty(ReleaseChecks.Evaluate(Ready(track)));
        }

        [Test]
        public void VersionCode_MustRiseAboveTheLastStoreBuild()
        {
            ReleaseInputs input = Ready(ReleaseTrack.Internal);
            input.VersionCode = 1;
            Assert.AreEqual(CheckLevel.Blocker, LevelOf(input, "version_code"));
            input.VersionCode = 0;
            Assert.AreEqual(CheckLevel.Blocker, LevelOf(input, "version_code"));
        }

        [TestCase("0.1.0", true)]
        [TestCase("1.12.3", true)]
        [TestCase("1.0", false)]
        [TestCase("1.0.0-beta", false)]
        [TestCase("01.0.0", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void VersionName_IsThreeNumbers(string name, bool valid)
        {
            Assert.AreEqual(valid, ReleaseChecks.IsVersionName(name));
        }

        [Test]
        public void MissingKeyOrPasswords_Block()
        {
            ReleaseInputs input = Ready(ReleaseTrack.Internal);
            input.KeystorePasswordsSet = false;
            Assert.AreEqual(CheckLevel.Blocker, LevelOf(input, "upload_key"));
            input.KeystoreFound = false;
            StringAssert.Contains("make_upload_key", ReleaseChecks.Evaluate(input).Single(c => c.Id == "upload_key").Message);
        }

        [Test]
        public void DevelopmentBackends_NeverReachTheStore()
        {
            ReleaseInputs input = Ready(ReleaseTrack.Internal);
            input.AdsSdkInstalled = false;
            input.PurchasingSdkInstalled = false;
            Assert.AreEqual(CheckLevel.Blocker, LevelOf(input, "ads_sdk"));
            Assert.AreEqual(CheckLevel.Blocker, LevelOf(input, "purchasing_sdk"));
        }

        [Test]
        public void DebugGeography_AlwaysBlocks()
        {
            ReleaseInputs input = Ready(ReleaseTrack.Internal);
            input.DebugGeographyEea = true;
            Assert.AreEqual(CheckLevel.Blocker, LevelOf(input, "debug_geography"));
        }

        [TestCase(ReleaseTrack.Internal, CheckLevel.Warning)]
        [TestCase(ReleaseTrack.Closed, CheckLevel.Blocker)]
        [TestCase(ReleaseTrack.Production, CheckLevel.Blocker)]
        public void TestAdIdsAndMissingPolicy_AreFineOnlyOnTheInternalTrack(ReleaseTrack track, CheckLevel expected)
        {
            ReleaseInputs input = Ready(track);
            input.AdsUseTestIds = true;
            input.HasPrivacyPolicy = false;
            Assert.AreEqual(expected, LevelOf(input, "test_ad_ids"));
            Assert.AreEqual(expected, LevelOf(input, "privacy_policy"));
        }

        [Test]
        public void NoFirebase_IsOnlyAWarning()
        {
            ReleaseInputs input = Ready(ReleaseTrack.Production);
            input.AnalyticsSdkInstalled = false;
            Assert.AreEqual(CheckLevel.Warning, LevelOf(input, "analytics_sdk"));
            Assert.IsFalse(ReleaseChecks.HasBlocker(ReleaseChecks.Evaluate(input)));
        }

        [Test]
        public void OldTargetSdk_Blocks()
        {
            ReleaseInputs input = Ready(ReleaseTrack.Internal);
            input.TargetSdk = ReleaseChecks.MinimumTargetSdk - 1;
            CollectionAssert.Contains(Ids(input), "target_sdk");
            Assert.IsTrue(ReleaseChecks.HasBlocker(ReleaseChecks.Evaluate(input)));
        }

        [Test]
        public void TodaysProject_CanGoToInternalTestingButNotTheClosedTest()
        {
            // Where the project stands at the start of Phase 5: test ad IDs, no policy URL, no Firebase.
            ReleaseInputs input = Ready(ReleaseTrack.Internal);
            input.AdsUseTestIds = true;
            input.HasPrivacyPolicy = false;
            input.AnalyticsSdkInstalled = false;
            Assert.IsFalse(ReleaseChecks.HasBlocker(ReleaseChecks.Evaluate(input)));
            input.Track = ReleaseTrack.Closed;
            Assert.IsTrue(ReleaseChecks.HasBlocker(ReleaseChecks.Evaluate(input)));
        }
    }
}
