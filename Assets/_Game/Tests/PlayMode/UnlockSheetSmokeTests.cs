using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 4 step 3: premium tray item, rewarded unlock for 24 h, then placing it (F10, PRD 15.1).</summary>
    public class UnlockSheetSmokeTests
    {
        private const int Premium = 8;

        [UnityTest]
        public IEnumerator LockedItem_WatchAd_UnlocksAndPicksItUp()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            TrayView tray = Object.FindFirstObjectByType<TrayView>();
            UnlockSheet sheet = Object.FindFirstObjectByType<UnlockSheet>();
            Assert.IsTrue(Game.Services.TryGet(out ProgressService progress));
            Assert.IsTrue(Game.Services.TryGet(out AdService ads));
            Assert.IsTrue(Game.Services.TryGet(out PlacementController placement));
            bool wasRemoved = progress.AdsRemoved;
            progress.ClearPurchase();
            progress.ClearUnlocks();
            Object.FindFirstObjectByType<CrackerSpawner>().ClearAll();
            yield return null;
            try
            {
                Assert.IsTrue(tray.ItemLocked(Premium), "premium starts locked");
                tray.Select(Premium);
                Assert.IsFalse(placement.IsPlacing, "a locked item is never picked up");
                Assert.IsTrue(sheet.IsOpen, "the unlock sheet opens");
                Assert.AreEqual("Peacock fountain", tray.ItemLabel(Premium));

                var card = (DevAdBackend)ads.Backend;
                sheet.WatchAd();
                Assert.IsTrue(card.Visible, "rewarded test ad");
                card.Finish(false);
                StringAssert.Contains("end", sheet.Status, "skipping gives no reward");
                Assert.IsTrue(tray.ItemLocked(Premium));

                sheet.WatchAd();
                card.Finish(true);
                StringAssert.Contains("24 hours", sheet.Status);
                Assert.IsFalse(tray.ItemLocked(Premium), "unlocked now");
                Assert.Greater(progress.SecondsLeft("peacock_fountain"), 23 * 3600);

                yield return new WaitForSecondsRealtime(1.4f);
                Assert.IsFalse(sheet.IsOpen, "closes by itself");
                Assert.IsTrue(placement.IsPlacing, "the cracker is picked up, ready to place");
                Assert.AreEqual("peacock_fountain", placement.Selected.Id);
                placement.Cancel();
            }
            finally
            {
                progress.ClearUnlocks();
                if (wasRemoved)
                {
                    progress.SetPurchased("remove_ads");
                }
            }
        }

        [UnityTest]
        public IEnumerator Purchase_UnlocksPremiumWithoutAnAd()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            TrayView tray = Object.FindFirstObjectByType<TrayView>();
            Assert.IsTrue(Game.Services.TryGet(out ProgressService progress));
            Assert.IsTrue(Game.Services.TryGet(out AdService ads));
            bool wasRemoved = progress.AdsRemoved;
            progress.ClearUnlocks();
            try
            {
                progress.SetPurchased("remove_ads");
                Assert.IsFalse(tray.ItemLocked(Premium));
                Assert.IsTrue(ads.Policy.AdsRemoved, "ads switch off with the purchase");
            }
            finally
            {
                if (!wasRemoved)
                {
                    progress.ClearPurchase();
                }
            }
        }
    }
}
