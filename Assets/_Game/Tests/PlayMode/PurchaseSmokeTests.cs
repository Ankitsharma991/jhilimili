using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 4 step 5 (F11): buy, persists, restore on a fresh install, refund, offline, pending, cancel.</summary>
    public class PurchaseSmokeTests
    {
        private const int Premium = 8;

        [UnityTest]
        public IEnumerator Buy_Restore_Refund_FromSettings()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out PurchaseService purchases));
            Assert.IsTrue(Game.Services.TryGet(out ProgressService progress));
            Assert.IsTrue(Game.Services.TryGet(out AdService ads));
            var store = (DevPurchaseBackend)purchases.Backend;
            SettingsSheet sheet = Object.FindFirstObjectByType<SettingsSheet>();
            TrayView tray = Object.FindFirstObjectByType<TrayView>();
            bool wasOwned = store.Owns(Entitlements.RemoveAdsSku);
            store.Refund();
            progress.ClearPurchase();
            progress.ClearUnlocks();
            try
            {
                sheet.Open();
                Assert.IsTrue(sheet.BuyVisible);
                StringAssert.Contains("INR 99", sheet.RemoveAdsNote, "price from the store");

                sheet.BuyRemoveAds();
                Assert.IsTrue(store.DialogVisible, "store dialog");
                Assert.IsTrue(purchases.Busy);
                store.Choose(PurchaseResult.Cancelled);
                Assert.IsFalse(purchases.Owned, "cancel buys nothing");

                sheet.BuyRemoveAds();
                store.Choose(PurchaseResult.Pending);
                Assert.IsFalse(purchases.Owned, "pending is not granted yet");
                StringAssert.Contains("pending", sheet.RemoveAdsNote);

                sheet.BuyRemoveAds();
                store.Choose(PurchaseResult.Purchased);
                Assert.IsTrue(purchases.Owned);
                Assert.IsTrue(ads.Policy.AdsRemoved, "no ads after buying");
                Assert.IsFalse(tray.ItemLocked(Premium), "every cracker unlocked");
                Assert.IsFalse(sheet.BuyVisible, "Buy hides once owned");
                StringAssert.Contains("Thank you", sheet.RemoveAdsNote);

                // A fresh install (data cleared): the cache is empty until Restore asks the store.
                progress.ClearPurchase();
                Assert.IsTrue(tray.ItemLocked(Premium));
                sheet.RestorePurchases();
                Assert.IsTrue(purchases.Owned, "restore brings it back");
                StringAssert.Contains("restored", sheet.RestoreNote);

                store.FailNextQuery = true;
                sheet.RestorePurchases();
                Assert.IsTrue(purchases.Owned, "offline never removes a purchase");
                StringAssert.Contains("connection", sheet.RestoreNote);

                store.Refund();
                sheet.RestorePurchases();
                Assert.IsFalse(purchases.Owned, "a refund takes it away");
                Assert.IsFalse(ads.Policy.AdsRemoved);
                StringAssert.Contains("No purchases", sheet.RestoreNote);
                sheet.Close();
            }
            finally
            {
                if (wasOwned)
                {
                    PlayerPrefs.SetString(DevPurchaseBackend.OwnedKey, Entitlements.RemoveAdsSku);
                    progress.SetPurchased(Entitlements.RemoveAdsSku);
                }
                else
                {
                    store.Refund();
                    progress.ClearPurchase();
                }
            }
        }

        [UnityTest]
        public IEnumerator UnlockCard_OffersThePurchase()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out PurchaseService purchases));
            Assert.IsTrue(Game.Services.TryGet(out ProgressService progress));
            Assert.IsTrue(Game.Services.TryGet(out PlacementController placement));
            var store = (DevPurchaseBackend)purchases.Backend;
            bool wasOwned = store.Owns(Entitlements.RemoveAdsSku);
            store.Refund();
            progress.ClearPurchase();
            progress.ClearUnlocks();
            TrayView tray = Object.FindFirstObjectByType<TrayView>();
            UnlockSheet card = Object.FindFirstObjectByType<UnlockSheet>();
            try
            {
                tray.Select(Premium);
                Assert.IsTrue(card.IsOpen);
                card.BuyRemoveAds();
                store.Choose(PurchaseResult.Purchased);
                StringAssert.Contains("Thank you", card.Status);
                yield return new WaitForSecondsRealtime(1.4f);
                Assert.IsFalse(card.IsOpen);
                Assert.AreEqual("peacock_fountain", placement.Selected != null ? placement.Selected.Id : null, "picked up after buying");
                placement.Cancel();
            }
            finally
            {
                if (!wasOwned)
                {
                    store.Refund();
                    progress.ClearPurchase();
                }
            }
        }
    }
}
