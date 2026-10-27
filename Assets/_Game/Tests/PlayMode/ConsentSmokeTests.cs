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
    /// <summary>Phase 4 step 4: consent with debug geography (F10), Privacy options and usage statistics (PRD 16).</summary>
    public class ConsentSmokeTests
    {
        [UnityTest]
        public IEnumerator Eea_AsksBeforeAds_PrivacyOptionsCanChangeIt()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out ConsentService consent));
            Assert.IsTrue(Game.Services.TryGet(out AdService ads));
            var dev = (DevConsentBackend)consent.Backend;
            Assert.IsTrue(consent.Gathered, "gathered at launch");
            Object.FindFirstObjectByType<CrackerSpawner>().ClearAll();
            bool wasEea = dev.InEea;
            try
            {
                dev.InEea = true;
                dev.Reset();
                consent.Gather();
                Assert.IsTrue(dev.FormVisible, "EEA: the consent form shows");
                Assert.AreEqual(ConsentStatus.Required, consent.Status);
                Assert.AreEqual(AdBlock.NoConsent, ads.Check(AdKind.Rewarded), "no ads before a choice");
                Assert.IsFalse(consent.AnalyticsAllowed, "no analytics before a choice");

                dev.Answer(false);
                Assert.AreEqual(ConsentStatus.Obtained, consent.Status);
                Assert.AreEqual(AdBlock.None, ads.Check(AdKind.Rewarded), "declining still allows non-personalised ads");
                Assert.IsTrue(consent.AnalyticsAllowed);

                SettingsSheet sheet = Object.FindFirstObjectByType<SettingsSheet>();
                sheet.Open();
                StringAssert.Contains("Change", sheet.PrivacyNote);
                sheet.OpenPrivacyOptions();
                Assert.IsTrue(dev.FormVisible, "Privacy options reopens the form");
                dev.Answer(true);
                Assert.AreEqual("agree", dev.Choice);

                sheet.SetUsageStats(false);
                Assert.IsFalse(consent.AnalyticsAllowed, "the switch turns analytics off");
                sheet.SetUsageStats(true);
                Assert.IsTrue(consent.AnalyticsAllowed);
                sheet.Close();
            }
            finally
            {
                dev.InEea = wasEea;
                dev.Reset();
                consent.Gather();
            }
        }

        [UnityTest]
        public IEnumerator OutsideEea_NoFormAndAdsAllowed()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out ConsentService consent));
            var dev = (DevConsentBackend)consent.Backend;
            if (dev.InEea)
            {
                Assert.Ignore("this editor is set to simulate the EEA");
            }

            Assert.IsFalse(dev.FormVisible);
            Assert.AreEqual(ConsentStatus.NotRequired, consent.Status);
            Assert.IsTrue(consent.Backend.CanRequestAds);
        }
    }
}
