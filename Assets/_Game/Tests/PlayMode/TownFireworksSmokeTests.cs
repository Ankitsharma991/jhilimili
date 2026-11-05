using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Content;
using UtsavNights.Core.District;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 12 step 7: light a cracker in the square, not on the road; the district's own show; the cracker strip.</summary>
    public class TownFireworksSmokeTests
    {
        [UnityTest]
        public IEnumerator LightOneInTheSquare_NotOnTheRoad()
        {
            yield return Begin();
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            int payoffs = 0;
            void OnPayoff(PayoffStartedEvent e) => payoffs++;
            Game.Events.Subscribe<PayoffStartedEvent>(OnPayoff);
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out TownFireworks town));
                Assert.IsTrue(Game.Services.TryGet(out DistrictView district));
                ExploreController c = mode.Controller;
                CrackerDefinition fountain = town.Available.FirstOrDefault(d => d.Category == CrackerCategory.GroundFountain);
                Assert.IsNotNull(fountain, "a fountain to light");

                // The bazaar road: no.
                c.Teleport(new Vector3(100f, 0f, 5.2f), 0f);
                district.Refresh();
                yield return null;
                Assert.AreEqual(TownVerdict.OnTheRoad, town.Place(fountain));
                yield return null;
                StringAssert.Contains("Not on the road", mode.ToastText);

                // An open corner of the square: yes.
                Plan(out DistrictPlan plan);
                var stand = new Vector3(plan.Square.X0 + 10f, 0f, plan.Square.Z0 + 8f);
                c.Teleport(stand, 0f);
                district.Refresh();
                yield return null;
                ClearAround(town.SpotAhead(), 16f);
                Assert.AreEqual(TownVerdict.Ok, town.Place(fountain));
                Assert.AreEqual(1, town.Placed.Count);
                CrackerRuntime placed = town.Placed[0];
                Assert.Less(Vector3.Distance(placed.transform.position, stand + new Vector3(0f, 0f, TownFireworks.PlaceAhead)), 0.1f, "in front of the player");

                // Walk up, light it, step back.
                c.Teleport(placed.transform.position + new Vector3(0f, 0f, -1f), 0f);
                yield return null;
                yield return null;
                Assert.AreEqual("Light the fuse", c.Prompt);
                Assert.IsTrue(c.Interact());
                Assert.IsTrue(placed.IsBurning || placed.StateMachine.State >= Core.Crackers.CrackerState.Lit, "lit");
                Assert.AreEqual(0, town.Placed.Count);
                StringAssert.Contains("Step back", mode.ToastText);
                c.Teleport(placed.transform.position + new Vector3(0f, 0f, -7f), 0f);
                float end = Time.realtimeSinceStartup + 10f;
                while (payoffs == 0 && Time.realtimeSinceStartup < end)
                {
                    yield return null;
                }

                Assert.Greater(payoffs, 0, "it goes off");
            }
            finally
            {
                Game.Events.Unsubscribe<PayoffStartedEvent>(OnPayoff);
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        [UnityTest]
        public IEnumerator TheDistrictsShow_AtTheGhat_PutAwayOnLeaving()
        {
            yield return Begin();
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out TownFireworks town));
                Assert.IsTrue(Game.Services.TryGet(out CrackerSpawner spawner));
                Assert.IsTrue(Game.Services.TryGet(out CrowdView crowd));
                Plan(out DistrictPlan plan);
                TownShowPlace ghat = TownShows.Places(plan)[0];
                mode.Controller.Teleport(new Vector3(ghat.WatchX, 0f, ghat.WatchZ - 6f), 0f);
                Object.FindFirstObjectByType<DistrictView>().Refresh();
                yield return null;
                Assert.IsTrue(town.StartShow(0));
                Assert.IsTrue(town.ShowOn);
                Assert.IsFalse(town.StartShow(1), "one at a time");
                yield return new WaitForSeconds(4f);
                Assert.Greater(town.CuesFired, 0, "the program runs");
                Assert.IsTrue(spawner.Active.Any(r => Mathf.Abs(r.transform.position.z - ghat.Pads[0].z) < 1f), "crackers on the ghat's pads");
                int coming = crowd.Sim.People.Count(p => p.State == Core.District.PersonState.Walking || p.State == Core.District.PersonState.Watching);
                Assert.Greater(coming, 5, "people come to watch");

                mode.Exit();
                yield return null;
                yield return null;
                Assert.IsFalse(town.ShowOn, "it stops when the player leaves");
            }
            finally
            {
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        [UnityTest]
        public IEnumerator TheCrackerStrip_ListsWhatYouHave()
        {
            yield return Begin();
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out TownFireworks town));
                mode.OpenCrackers();
                Assert.IsTrue(mode.PickerOpen);
                yield return null;
                Assert.Greater(town.Available.Count, 3);
                Assert.IsTrue(town.Available.All(d => d.Category != CrackerCategory.Sparkler), "no sparklers: they're held");
                Assert.IsNotNull(GameObject.Find("Cracker_" + town.Available[0].Id));
                mode.CloseCrackers();
                Assert.IsFalse(mode.PickerOpen);
            }
            finally
            {
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        private static IEnumerator Begin()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Object.FindFirstObjectByType<Gameplay.QualityManager>().SetTierManually(2);
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            Assert.IsTrue(mode.Open());
            yield return null;
            mode.Controller.Teleport(new Vector3(60f, 0f, 9.8f), 90f);
            Object.FindFirstObjectByType<DistrictView>().Refresh();
            for (int f = 0; f < 15; f++)
            {
                yield return null;
            }
        }

        private static void Plan(out DistrictPlan plan) => plan = DistrictGenerator.Bazaar;

        /// <summary>Sends anyone near a point off to the far end of the bazaar road, so the spot is clear.</summary>
        private static void ClearAround(Vector3 at, float radius)
        {
            Assert.IsTrue(Game.Services.TryGet(out CrowdView crowd));
            Core.District.Person[] people = crowd.Sim.People;
            for (int i = 0; i < people.Length; i++)
            {
                if (new Vector2(people[i].X - at.x, people[i].Z - at.z).magnitude < radius)
                {
                    people[i].X = 300f;
                    people[i].Z = 9.8f;
                    people[i].State = Core.District.PersonState.Standing;
                    people[i].Timer = 120f;
                    people[i].PathCount = 0;
                }
            }
        }
    }
}
