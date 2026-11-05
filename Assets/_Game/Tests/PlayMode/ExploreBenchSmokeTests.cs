using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Diagnostics;
using UtsavNights.Core.District;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 12 step 8: the Explore bench tours the town and writes its results; the open world's analytics.</summary>
    public class ExploreBenchSmokeTests
    {
        [UnityTest]
        public IEnumerator TheBench_ToursEveryLeg_AndWritesResults()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var bench = Object.FindFirstObjectByType<ExploreBench>();
            Assert.IsNotNull(bench, "the bench is in the scene");
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            QualityManager quality = Object.FindFirstObjectByType<QualityManager>();
            quality.SetTierManually(2);
            try
            {
                File.Delete(ExploreBench.FilePath);
                bench.Scale = 0.2f;
                Assert.IsTrue(bench.Run());
                Assert.IsFalse(bench.Run(), "one at a time");
                float end = Time.realtimeSinceStartup + 90f;
                while (bench.Running && Time.realtimeSinceStartup < end)
                {
                    yield return null;
                }

                Assert.IsFalse(bench.Running, "the tour finished");
                CollectionAssert.AreEqual(ExploreBench.Legs, bench.Results.Select(l => l.Name).ToArray(), "every leg, in order");
                foreach (BenchLeg leg in bench.Results)
                {
                    Assert.Greater(leg.Frames, 5, leg.Name);
                    Assert.Greater(leg.AvgMs, 0f, leg.Name);
                }

                BenchLeg walk = bench.Results[1], show = bench.Results[4];
                Assert.Greater(walk.People, 100, "the crowd is simulated on the bazaar road");
                Assert.Greater(bench.Results[3].Vehicles, 0, "traffic while riding");
                Assert.Greater(show.Crackers, 0, "the show's crackers on the scene");
                Assert.IsFalse(mode.IsOpen, "Explore closed at the end");

                string[] lines = File.ReadAllLines(ExploreBench.FilePath);
                Assert.AreEqual(ExploreBench.Legs.Length + 1, lines.Length, "a line a leg, and the summary");
                StringAssert.Contains("\"leg\":\"chowk_show\"", lines[4]);
                StringAssert.Contains("\"done\":true", lines[^1]);
            }
            finally
            {
                bench.Scale = 1f;
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        [UnityTest]
        public IEnumerator OpenWorldEvents_ReachAnalytics()
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
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                dev.InEea = false;
                dev.Reset();
                consent.Gather();
                settings.Change(s => s.shareUsageStats = true);
                int rejected = analytics.Rejected;

                Assert.IsTrue(mode.Open());
                Assert.AreEqual("explore_started", log.LastEvent);
                yield return null;
                mode.Phone.OpenApp("jobs");
                Assert.AreEqual("phone_app", log.LastEvent);
                StringAssert.Contains("\"app\":\"jobs\"", Last());
                mode.Phone.Close();

                Assert.IsTrue(Game.Services.TryGet(out TownFireworks town));
                mode.Controller.Teleport(new Vector3(100f, 0f, 5.2f), 0f);
                Object.FindFirstObjectByType<DistrictView>().Refresh();
                yield return null;
                town.Place(town.Available[0]);
                Assert.AreEqual("town_cracker_placed", log.LastEvent);
                StringAssert.Contains("\"verdict\":\"on_the_road\"", Last());

                Assert.IsTrue(town.StartShow(0));
                Assert.AreEqual("town_show", log.LastEvent);
                StringAssert.Contains("\"place\":\"ghat\"", Last());

                Game.Events.Publish(new JobEvent(Core.Explore.JobCatalog.Lane, Core.Explore.JobStatus.Active, 0));
                Assert.AreEqual("job_started", log.LastEvent);
                Game.Events.Publish(new JobEvent(Core.Explore.JobCatalog.Lane, Core.Explore.JobStatus.Completed, 95));
                Assert.AreEqual("job_ended", log.LastEvent);
                StringAssert.Contains("\"outcome\":\"completed\"", Last());
                Game.Events.Publish(new PlayerRewardEvent(10, 50, "job", 3));
                Assert.AreEqual("level_up", log.LastEvent);

                mode.Exit();
                Assert.AreEqual("explore_ended", log.LastEvent);
                string ended = Last();
                StringAssert.Contains("\"ride_s\":", ended);
                StringAssert.Contains("\"district_s\":", ended);
                StringAssert.DoesNotContain("\"x\"", ended);
                Assert.AreEqual(rejected, analytics.Rejected, "every event passed the schema");
            }
            finally
            {
                mode.Exit();
                dev.InEea = wasEea;
                settings.Change(s => s.shareUsageStats = wasSharing);
            }

            static string Last() => File.ReadAllLines(DevAnalyticsBackend.FilePath)[^1];
        }
    }
}
