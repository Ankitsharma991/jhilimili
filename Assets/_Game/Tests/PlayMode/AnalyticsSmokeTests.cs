using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Analytics;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 4 step 6 (F12): events flow with consent, stop with the switch off, and hold during a consent form.</summary>
    public class AnalyticsSmokeTests
    {
        [UnityTest]
        public IEnumerator DecorationEvents_ReachAnalytics()
        {
            new HouseStore().Delete();
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
            try
            {
                dev.InEea = false;
                dev.Reset();
                consent.Gather();
                settings.Change(s => s.shareUsageStats = true);
                var mode = Object.FindFirstObjectByType<UI.DecorationMode>();
                mode.Open();
                Assert.AreEqual("decor_mode_opened", log.LastEvent);

                Assert.IsTrue(Game.Services.TryGet(out HomeService home));
                PlacedDecor pose = new PlacedDecor { uid = home.Design.nextUid, item = "rangoli_lotus", x = 0f, z = 2.5f, scale = 1f };
                Assert.IsTrue(home.Do(new PlaceDecor(pose)));
                Assert.AreEqual("decoration_placed", log.LastEvent);
                string line = System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1];
                StringAssert.Contains("\"id\":\"rangoli_lotus\"", line);
                StringAssert.Contains("\"category\":\"rangoli\"", line);

                mode.SaveLayout(0);
                Assert.AreEqual("layout_saved", log.LastEvent);
                StringAssert.DoesNotContain("Layout 1", System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1], "layout names stay on the phone");

                mode.Done();
                Assert.AreEqual("decor_mode_closed", log.LastEvent);
                analytics.EndSession();
                StringAssert.Contains("\"decorations\":1", System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1]);
            }
            finally
            {
                dev.InEea = wasEea;
                settings.Change(s => s.shareUsageStats = wasSharing);
                new HouseStore().Delete();
            }
        }

        [UnityTest]
        public IEnumerator NeighbourhoodEvents_ReachAnalytics()
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
            try
            {
                dev.InEea = false;
                dev.Reset();
                consent.Gather();
                settings.Change(s => s.shareUsageStats = true);
                var walk = Object.FindFirstObjectByType<UI.WalkMode>();
                walk.Open();
                Assert.AreEqual("walk_started", log.LastEvent);
                Assert.IsTrue(walk.Controller.FastTravel("open"));
                Assert.AreEqual("fast_travel", log.LastEvent);
                StringAssert.Contains("\"spot\":\"open\"", System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1]);
                Assert.IsTrue(walk.Celebrate("open"));
                string[] lines = System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath);
                StringAssert.Contains("spot_chosen", lines[^2]);
                StringAssert.Contains("\"kind\":\"square\"", lines[^2]);
                Assert.AreEqual("walk_ended", log.LastEvent);
                StringAssert.DoesNotContain("\"x\"", lines[^1], "no positions");

                analytics.EndSession();
                string end = System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1];
                StringAssert.Contains("\"environment\":\"town_lane\"", end);
                StringAssert.Contains("walk_s", end);
                Assert.IsTrue(Game.Services.TryGet(out CelebrationService celebration));
                celebration.Choose("courtyard");
            }
            finally
            {
                dev.InEea = wasEea;
                settings.Change(s => s.shareUsageStats = wasSharing);
            }
        }

        [UnityTest]
        public IEnumerator PackShowAndClipEvents_ReachAnalytics()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out AnalyticsService analytics));
            Assert.IsTrue(Game.Services.TryGet(out SettingsService settings));
            Assert.IsTrue(Game.Services.TryGet(out ConsentService consent));
            Assert.IsTrue(Game.Services.TryGet(out ShowPlayer player));
            var log = (DevAnalyticsBackend)analytics.Backend;
            var dev = (DevConsentBackend)consent.Backend;
            bool wasEea = dev.InEea;
            bool wasSharing = settings.Current.shareUsageStats;
            string shows = System.IO.Path.Combine(Application.temporaryCachePath, "shows_analytics_test.json");
            string clip = System.IO.Path.Combine(Application.temporaryCachePath, "utsav_analytics_test.mp4");
            try
            {
                dev.InEea = false;
                dev.Reset();
                consent.Gather();
                settings.Change(s => s.shareUsageStats = true);
                int rejected = analytics.Rejected;

                Game.Events.Publish(new PackInstalledEvent(Core.Packs.SamplePacks.BlueCityPack, "1.0.0", "environment"));
                Assert.AreEqual("pack_installed", log.LastEvent);
                StringAssert.Contains("\"version\":\"1.0.0\"", Last());
                Game.Events.Publish(new PackFailedEvent(Core.Packs.SamplePacks.BlueCityPack, "network"));
                Assert.AreEqual("pack_failed", log.LastEvent);
                Game.Events.Publish(new PackDeletedEvent(Core.Packs.SamplePacks.BlueCityPack));
                Assert.AreEqual("pack_deleted", log.LastEvent);

                player.UseStore(new ShowStore(shows));
                Core.Shows.ShowDef show = Core.Shows.ShowTemplates.All[0].Clone();
                show.id = player.Library.NewId();
                show.name = "Dadi show";
                Assert.IsTrue(player.Save(show));
                Assert.AreEqual("show_saved", log.LastEvent);
                StringAssert.Contains("\"pads\":4", Last());
                StringAssert.DoesNotContain("Dadi", Last(), "the show's name stays on the phone");
                // Playing sets the first cracker at once, so cracker_placed may follow show_played.
                Assert.IsTrue(player.Play(show));
                StringAssert.Contains("\"replay\":0", LastOf("show_played"));
                player.Stop();
                Assert.IsTrue(player.Replay());
                StringAssert.Contains("\"replay\":1", LastOf("show_played"));
                player.Stop();

                Game.Events.Publish(new ClipRecordedEvent(true, 14, "show", 410, string.Empty));
                Assert.AreEqual("clip_recorded", log.LastEvent);
                Game.Events.Publish(new ClipRecordedEvent(false, 0, "free", 0, "encoder"));
                Assert.AreEqual("clip_failed", log.LastEvent);
                System.IO.File.WriteAllBytes(clip, new byte[16]);
                Assert.AreEqual(string.Empty, ClipSharer.Share(new ClipResult(true, clip, 12.4f, "free", string.Empty), Core.Clips.ShareTarget.WhatsApp));
                Assert.AreEqual("clip_shared", log.LastEvent);
                StringAssert.Contains("\"target\":\"whatsapp\"", Last());
                StringAssert.DoesNotContain(clip, Last(), "no file paths");
                Assert.AreEqual(rejected, analytics.Rejected, "every event passed the schema");
            }
            finally
            {
                player.Stop();
                player.UseStore(new ShowStore());
                foreach (string f in new[] { shows, clip })
                {
                    if (System.IO.File.Exists(f))
                    {
                        System.IO.File.Delete(f);
                    }
                }

                dev.InEea = wasEea;
                settings.Change(s => s.shareUsageStats = wasSharing);
            }

            static string Last() => System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1];
            static string LastOf(string name) => System.Linq.Enumerable.Last(System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath), l => l.Contains("\"" + name + "\""));
        }

        [UnityTest]
        public IEnumerator AvatarEvents_ReachAnalytics_WithoutTheName()
        {
            new ProfileStore().Delete();
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
            try
            {
                dev.InEea = false;
                dev.Reset();
                consent.Gather();
                settings.Change(s => s.shareUsageStats = true);
                var creator = Object.FindFirstObjectByType<UI.AvatarCreator>();

                creator.Open();
                Assert.AreEqual("avatar_creator_opened", log.LastEvent);
                creator.Skip();
                Assert.AreEqual("avatar_skipped", log.LastEvent);

                creator.Open();
                creator.ApplyPreset(5); // kurta, dhoti, shawl, chappals, turban
                creator.EditName("Ravi");
                creator.Save();
                Assert.AreEqual("avatar_saved", log.LastEvent);
                string line = System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1];
                StringAssert.Contains("\"from_preset\":1", line);
                StringAssert.Contains("\"top\":\"kurta\"", line);
                StringAssert.Contains("\"headwear\":\"turban\"", line);
                StringAssert.DoesNotContain("Ravi", line, "the name never leaves the phone");
                yield return null;

                analytics.EndSession();
                StringAssert.Contains("\"has_avatar\":1", System.IO.File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1]);
            }
            finally
            {
                dev.InEea = wasEea;
                settings.Change(s => s.shareUsageStats = wasSharing);
                new ProfileStore().Delete();
            }
        }

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
