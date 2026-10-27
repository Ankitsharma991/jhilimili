using NUnit.Framework;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Tests
{
    /// <summary>progress.json: purchase cache and rewarded unlocks (F10, F11).</summary>
    public class PlayerProgressTests
    {
        private const long Now = 1_800_000_000;
        private const long Day = PlayerProgress.RewardedUnlockSeconds;

        [Test]
        public void RewardedUnlock_LastsTwentyFourHours()
        {
            var p = new PlayerProgress();
            Assert.IsFalse(p.IsUnlocked("peacock_fountain", Now));
            p.GrantRewardedUnlock("peacock_fountain", Now);
            Assert.IsTrue(p.IsUnlocked("peacock_fountain", Now + Day - 1));
            Assert.IsFalse(p.IsUnlocked("peacock_fountain", Now + Day));
            Assert.AreEqual(Day - 60, p.SecondsLeft("peacock_fountain", Now + 60));
            Assert.IsFalse(p.IsUnlocked("other", Now), "only the chosen cracker");
        }

        [Test]
        public void WatchingAgain_RestartsTheTimerWithoutStacking()
        {
            var p = new PlayerProgress();
            p.GrantRewardedUnlock("peacock_fountain", Now);
            p.GrantRewardedUnlock("peacock_fountain", Now + 3600);
            Assert.AreEqual(1, p.unlocks.Count);
            Assert.AreEqual(Day, p.SecondsLeft("peacock_fountain", Now + 3600));
        }

        [Test]
        public void Purchase_UnlocksEverythingUntilCleared()
        {
            var p = new PlayerProgress();
            p.SetPurchased("remove_ads");
            Assert.IsTrue(p.adsRemoved);
            Assert.IsTrue(p.IsUnlocked("anything", Now));
            Assert.AreEqual("remove_ads", p.purchasedSku);
            p.ClearPurchase();
            Assert.IsFalse(p.IsUnlocked("anything", Now));
            Assert.IsEmpty(p.purchasedSku);
        }

        [Test]
        public void Sanitize_DropsExpiredEmptyAndDuplicates_AndCapsFutureClocks()
        {
            var p = new PlayerProgress();
            p.unlocks.Add(new PlayerProgress.TimedUnlock { id = "old", expiresUnix = Now - 1 });
            p.unlocks.Add(new PlayerProgress.TimedUnlock { id = "", expiresUnix = Now + 10 });
            p.unlocks.Add(new PlayerProgress.TimedUnlock { id = "a", expiresUnix = Now + 10 });
            p.unlocks.Add(new PlayerProgress.TimedUnlock { id = "a", expiresUnix = Now + 20 });
            p.unlocks.Add(new PlayerProgress.TimedUnlock { id = "edited", expiresUnix = Now + 365 * Day });
            Assert.IsTrue(p.Sanitize(Now));
            Assert.AreEqual(2, p.unlocks.Count);
            Assert.AreEqual(Day, p.SecondsLeft("edited", Now), "capped to 24 h from now");
            Assert.IsFalse(p.Sanitize(Now), "already clean");
        }

        [Test]
        public void Sanitize_RepairsNullsFromAnOldFile()
        {
            var p = new PlayerProgress { unlocks = null, purchasedSku = null, version = 0 };
            Assert.IsTrue(p.Migrate());
            Assert.IsTrue(p.Sanitize(Now));
            Assert.IsNotNull(p.unlocks);
            Assert.AreEqual(PlayerProgress.CurrentVersion, p.version);
        }
    }
}
