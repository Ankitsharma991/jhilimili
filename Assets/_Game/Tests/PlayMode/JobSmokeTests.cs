using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 12 step 6: take a job on the phone, follow the route, do it, get paid; the rooftop show; the phone's apps.</summary>
    public class JobSmokeTests
    {
        [UnityTest]
        public IEnumerator LightTheLane_FromThePhone_PaidAndLit()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out JobService jobs));
                Assert.IsTrue(Game.Services.TryGet(out PlayerService player));
                Assert.IsTrue(mode.Open());
                yield return null;
                yield return null;
                ExploreController c = mode.Controller;
                Assert.Greater(jobs.Messages.Count, 0, "offers on the phone");
                Assert.Greater(jobs.Unread, 0);

                // The phone: Messages, then Jobs, then take the lane.
                mode.OpenPhone();
                Assert.IsTrue(mode.Phone.IsOpen);
                mode.Phone.OpenApp("messages");
                yield return null;
                Assert.AreEqual(0, jobs.Unread, "read");
                mode.Phone.OpenApp("jobs");
                yield return null;
                Assert.IsTrue(jobs.Accept(JobCatalog.Lane));
                mode.Phone.Close();
                yield return null;
                yield return null;
                StringAssert.Contains("Light the diyas", mode.ObjectiveText);
                yield return new WaitForSeconds(0.7f);
                Assert.IsTrue(mode.Minimap.HasTarget, "the minimap points the way");
                Assert.Greater(mode.Minimap.DotsShown, 0, "with a route");
                Assert.IsNotNull(GameObject.Find("Beacon0"), "a beacon at a diya");

                long coins = player.Current.coins;
                int times = player.Current.TimesDone(JobCatalog.Lane);
                (long pay, int rep) = JobPay.For(jobs.Find(JobCatalog.Lane), times);
                for (int i = 0; i < 8; i++)
                {
                    JobStep s = jobs.Find(JobCatalog.Lane).Steps[i];
                    c.Teleport(new Vector3(s.X + 0.6f, 0f, s.Z), 0f);
                    yield return null;
                    yield return null;
                    Assert.AreEqual("Light the diya", c.Prompt, $"at diya {i}");
                    Assert.IsTrue(c.Interact());
                    yield return null;
                }

                Assert.IsNull(jobs.Active, "done");
                Assert.AreEqual(coins + pay, player.Current.coins, "paid");
                Assert.AreEqual(times + 1, player.Current.TimesDone(JobCatalog.Lane));
                Assert.AreEqual("LaneDiyaLit", GameObject.Find("LaneDiya_diya0").GetComponent<MeshFilter>().sharedMesh.name, "and the lane glows");
                StringAssert.Contains($"+{pay:N0} coins", mode.ToastText);
                Assert.AreEqual(rep > 0, mode.ToastText.Contains("reputation"));
            }
            finally
            {
                mode.Phone?.Close();
                mode.Exit();
            }
        }

        [UnityTest]
        public IEnumerator RooftopShow_UpOnTheRoof_RacksAndTheShow_ThenDown()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(2);
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out JobService jobs));
                Assert.IsTrue(Game.Services.TryGet(out PlayerService player));
                Assert.IsTrue(Game.Services.TryGet(out DistrictView district));
                if (player.Level < 2)
                {
                    player.Reward(0, 100, "test");
                }

                Assert.IsTrue(mode.Open());
                yield return null;
                ExploreController c = mode.Controller;
                Assert.IsTrue(jobs.Accept(JobCatalog.Rooftop));
                JobDef job = jobs.Active.Def;

                // The crate, then up the stairs.
                yield return At(c, district, job.Steps[0], 0f);
                Assert.AreEqual("Collect the crate", c.Prompt);
                Assert.IsTrue(c.Interact());
                yield return At(c, district, job.Steps[1], 0f);
                Assert.AreEqual("Take it up to the roof", c.Prompt);
                Assert.IsTrue(c.Interact());
                yield return null;
                yield return null;
                float roof = job.Steps[1].ToY;
                Assert.AreEqual(roof, c.Player.position.y, 0.3f, "up on the roof");

                // Walking about up there: it holds you, inside the parapet.
                mode.Stick.Set(new Vector2(0f, 1f));
                yield return new WaitForSeconds(0.8f);
                mode.Stick.Set(Vector2.zero);
                Assert.AreEqual(roof, c.Player.position.y, 0.3f, "still on the roof");
                Assert.IsTrue(district.IsWalkable(c.Player.position));

                for (int i = 2; i <= 4; i++)
                {
                    yield return At(c, district, job.Steps[i], roof);
                    Assert.AreEqual("Set up a rack", c.Prompt, job.Steps[i].Id);
                    Assert.IsTrue(c.Interact());
                }

                int bursts = jobs.ShowBursts;
                Assert.IsTrue(Game.Services.TryGet(out TownFireworks town));
                int cues = town.CuesFired;
                yield return At(c, district, job.Steps[5], roof);
                Assert.AreEqual("Light the show", c.Prompt);
                Assert.IsTrue(c.Interact());
                Assert.IsTrue(jobs.ShowPlaying, "the show goes up");
                Assert.IsNull(jobs.Active, "and the job's done");
                yield return new WaitForSeconds(2.5f);
                Assert.Greater(jobs.ShowBursts, bursts, "bursts over the chowk");
                Assert.Greater(town.CuesFired, cues, "real rockets from the racks");

                Assert.AreEqual("Go down to the street", c.Prompt);
                Assert.IsTrue(c.Interact());
                yield return null;
                Assert.Less(c.Player.position.y, 0.5f, "back on the street");
            }
            finally
            {
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        [UnityTest]
        public IEnumerator Phone_Map_Waypoint_Camera_AndHome()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out JobService jobs));
                Assert.IsTrue(mode.Open());
                yield return null;
                ExploreController c = mode.Controller;
                mode.OpenPhone();
                mode.Phone.OpenApp("map");
                yield return null;
                Assert.AreEqual(PhoneScreen.Page.Map, mode.Phone.Current);
                Assert.IsNotNull(GameObject.Find("You"), "the player on the map");

                // A waypoint at the chowk: the minimap routes there.
                (float u, float v) = MapTexture.Frame.ToUV(Core.District.DistrictGenerator.Bazaar.Square.CentreX, Core.District.DistrictGenerator.Bazaar.Square.CentreZ);
                mode.Phone.SetWaypointUV(u, v);
                Assert.IsTrue(jobs.Waypoint.HasValue);
                Assert.IsNotNull(GameObject.Find("Waypoint"));
                mode.Phone.Close();
                yield return new WaitForSeconds(0.7f);
                Assert.IsTrue(mode.Minimap.HasTarget);
                Assert.Greater(mode.Minimap.DotsShown, 5, "a long way, a long route");

                // The stick does nothing while the phone is up.
                mode.OpenPhone();
                mode.Stick.Set(new Vector2(0f, 1f));
                Vector3 at = c.Player.position;
                yield return new WaitForSeconds(0.4f);
                Assert.Less(Vector3.Distance(at, c.Player.position), 0.05f);
                mode.Stick.Set(Vector2.zero);

                // A photo, when there's a screen to take it from.
                mode.Phone.OpenApp("camera");
                Assert.IsFalse(mode.Phone.IsOpen);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Assert.IsTrue(System.IO.File.Exists(mode.Phone.LastPhoto), "saved");
                    Assert.AreEqual("Photo saved", mode.ToastText);
                    System.IO.File.Delete(mode.Phone.LastPhoto);
                }

                // Home: back to the gate.
                c.Teleport(new Vector3(-20f, 0f, 6.8f), 90f);
                mode.OpenPhone();
                mode.Phone.OpenApp("home");
                yield return null;
                Assert.Less(Vector2.Distance(new Vector2(c.Player.position.x, c.Player.position.z), new Vector2(0f, 4.6f)), 0.5f);
            }
            finally
            {
                mode.Phone?.Close();
                mode.Exit();
            }
        }

        private static IEnumerator At(ExploreController c, DistrictView district, JobStep s, float y)
        {
            c.Teleport(new Vector3(s.X, y + 0.02f, s.Z), 0f);
            district.Refresh();
            yield return null;
            yield return null;
            yield return null;
        }
    }
}
