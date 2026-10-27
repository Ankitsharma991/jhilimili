using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Analytics;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 4 step 6 (F12): events flow with consent, stop with the switch off, and hold during a consent form.</summary>
    public class AnalyticsSmokeTests
    {
        [UnityTest]
        public IEnumerator Events_FollowConsentAndTheUsageSwitch()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out AnalyticsService analytics));
            Assert.IsTrue(Game.Services.TryGet(out SettingsService settings));
            Assert.IsTrue(Game.Services.TryGet(out ConsentService consent));
            var log = (DevAnalyticsBackend)analytics.Backend;
            var dev = (DevConsentBackend)consent.Backend;
            bool wasEea = dev.InEea;
            bool wasSharing = settings.Current.shareUsageStats;
            bool wasQuiet = settings.Current.quietMode;
            try
            {
                dev.InEea = false;
                dev.Reset();
                consent.Gather();
                settings.Change(s => s.shareUsageStats = true);
                Assert.AreEqual(AnalyticsGate.State.Allowed, analytics.ConsentState);

                CrackerRuntime pot = SceneCrackers.Find("flowerpot_gold");
                int before = log.Sent;
                pot.Ignite();
                Assert.AreEqual(before + 1, log.Sent, "cracker_ignited sent");
                Assert.AreEqual("cracker_ignited", log.LastEvent);

                settings.Change(s => s.quietMode = !wasQuiet);
                yield return new WaitForSecondsRealtime(AnalyticsService.SettingsSettleSeconds + 0.3f);
                Assert.AreEqual("settings_changed", log.LastEvent, "reported once the change settles");

                settings.Change(s => s.shareUsageStats = false);
                Assert.AreEqual(AnalyticsGate.State.Denied, analytics.ConsentState);
                Assert.IsFalse(log.CollectionEnabled);
                before = log.Sent;
                Object.FindFirstObjectByType<CrackerSpawner>().ClearAll();
                SceneCrackers.Spawn("bomb_single", new Vector3(1.1f, 0f, 0.3f)).Ignite();
                Assert.AreEqual(before, log.Sent, "nothing sent with the switch off");

                settings.Change(s => s.shareUsageStats = true);
                dev.InEea = true;
                dev.Reset();
                consent.Gather();
                Assert.AreEqual(AnalyticsGate.State.Undecided, analytics.ConsentState, "held while the form is up");
                analytics.EndSession();
                Assert.AreEqual(1, analytics.HeldEvents);
                before = log.Sent;
                dev.Answer(true);
                Assert.AreEqual(before + 1, log.Sent, "held event sent after agreeing");
                Assert.AreEqual("session_end", log.LastEvent);
                Assert.AreEqual(0, analytics.Rejected, "every event matched the schema");
            }
            finally
            {
                dev.InEea = wasEea;
                dev.Reset();
                consent.Gather();
                settings.Change(s =>
                {
                    s.shareUsageStats = wasSharing;
                    s.quietMode = wasQuiet;
                });
            }
        }
    }
}
