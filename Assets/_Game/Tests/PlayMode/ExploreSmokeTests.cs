using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 12 step 1: the character in Explore mode: runs, sprints, stops, jumps, keeps out of walls, interacts, exits.</summary>
    public class ExploreSmokeTests
    {
        [UnityTest]
        public IEnumerator RunSprintStopJump_StayOnTheGround_ThenExit()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            var tray = Object.FindFirstObjectByType<TrayView>();
            Assert.IsTrue(mode.Launcher.gameObject.activeInHierarchy, "the Explore button is on screen");
            Assert.IsTrue(mode.Open());
            yield return null;
            ExploreController c = mode.Controller;
            Assert.IsTrue(c.IsOpen);
            Assert.IsFalse(tray.gameObject.activeInHierarchy, "the HUD steps aside for the controls");

            // Out in the open square (the courtyard is only a few steps across), facing along the street.
            SpotDef square = Game.Services.Get<SettlementView>().Layout.Spot("open");
            c.Teleport(new Vector3(square.x, 0f, square.z), 90f);
            yield return null;
            Vector3 start = c.Player.position;

            // Jog, then sprint (the gaits it reaches on the way; the square's edge may stop it after).
            bool jogged = false, sprinted = false;
            mode.Stick.Set(new Vector2(0f, 1f));
            for (float end = Time.realtimeSinceStartup + 0.45f; Time.realtimeSinceStartup < end;)
            {
                yield return null;
                jogged |= c.Gait == Gait.Jog;
            }

            mode.ToggleSprint();
            for (float end = Time.realtimeSinceStartup + 0.6f; Time.realtimeSinceStartup < end;)
            {
                yield return null;
                sprinted |= c.Gait == Gait.Sprint;
            }

            Assert.IsTrue(jogged, "a full push jogs");
            Assert.IsTrue(sprinted, "and Sprint sprints");
            float moved = Vector3.Distance(start, c.Player.position);
            Assert.Greater(moved, 1.5f, "it covers ground");

            // Let go: even a sprint stops in about a third of a second.
            mode.Stick.Set(Vector2.zero);
            mode.ToggleSprint();
            yield return Seconds(0.45f);
            Assert.Less(c.Speed, 0.2f);

            // Jump: leaves the ground and lands.
            c.Jump();
            yield return Seconds(0.15f);
            Assert.Greater(c.Player.position.y, 0.15f, "in the air");
            yield return Seconds(0.8f);
            Assert.Less(c.Player.position.y, 0.1f, "back on the ground");
            Assert.IsTrue(c.State.Grounded);

            // Always on walkable ground, never through a wall: run in every direction for a while.
            var nav = new WalkNavigator(Game.Services.Get<SettlementView>().Layout, Game.Services.Get<HomeService>().Design);
            foreach (Vector2 dir in new[] { Vector2.left, Vector2.right, Vector2.down, new Vector2(1f, 1f).normalized })
            {
                mode.Stick.Set(dir);
                yield return Seconds(0.8f);
                Vector3 p = c.Player.position;
                Assert.IsTrue(nav.IsWalkable(new Core.Avatar.V3(p.x, 0f, p.z)), $"on walkable ground at {p}");
            }

            mode.Stick.Set(Vector2.zero);
            mode.Exit();
            yield return null;
            Assert.IsFalse(c.IsOpen);
            Assert.IsTrue(tray.gameObject.activeInHierarchy, "the HUD is back");
            Assert.IsNull(GameObject.Find("ExplorePlayer"));
        }

        [UnityTest]
        public IEnumerator WalkingUpToASpot_OffersCelebrate()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            Assert.IsTrue(mode.Open());
            yield return null;
            ExploreController c = mode.Controller;
            SettlementLayout layout = Game.Services.Get<SettlementView>().Layout;
            SpotDef open = layout.Spot("open");
            Assert.IsNotNull(open);
            c.Teleport(new Vector3(open.x, 0f, open.z), 0f);
            yield return null;
            yield return null;
            StringAssert.StartsWith("Celebrate", c.Prompt);
            Assert.AreEqual(c.Prompt, mode.InteractLabel, "the Interact button shows it");
            Assert.IsTrue(c.Interact());
            Assert.IsFalse(c.IsOpen, "celebrating ends Explore at the spot");
            Assert.AreEqual("open", Game.Services.Get<CelebrationService>().Current.id);
            Game.Services.Get<CelebrationService>().Choose(CelebrationService.Courtyard);
        }

        [UnityTest]
        public IEnumerator OutOfTheGate_AndBackIn_EvenNotLinedUp()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            Assert.IsTrue(mode.Open());
            yield return null;
            ExploreController c = mode.Controller;
            var nav = new WalkNavigator(Game.Services.Get<SettlementView>().Layout, Game.Services.Get<HomeService>().Design);
            float wall = Core.Home.HouseGeometry.WorldFrontZ + Core.Home.HouseGeometry.BackWallZ;
            try
            {
                // From where Explore starts, and from either side of the courtyard, pushing straight at the street.
                Vector3 courtyard = c.Player.position;
                foreach (float x in new[] { courtyard.x, -0.8f, 2.8f })
                {
                    c.Teleport(new Vector3(x, 0f, 1.5f), 0f);
                    yield return null;
                    mode.Stick.Set(new Vector2(0f, 1f));
                    for (float end = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < end && c.Player.position.z < wall + 1.2f;)
                    {
                        yield return null;
                        Vector3 p = c.Player.position;
                        Assert.IsTrue(nav.IsWalkable(new Core.Avatar.V3(p.x, 0f, p.z)), $"on walkable ground at {p}");
                    }

                    mode.Stick.Set(Vector2.zero);
                    Assert.Greater(c.Player.position.z, wall + 1.2f, $"out of the gate into the lane, starting at x {x}; at {c.Player.position}, touching {Touching(c.Player.position)}");
                }

                // And back in from the street, a metre to the side of the gate.
                c.Teleport(new Vector3(0.4f, 0f, wall + 1.6f), 180f);
                yield return null;
                mode.Stick.Set(new Vector2(0f, 1f));
                for (float end = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < end && c.Player.position.z > wall - 1f;)
                {
                    yield return null;
                }

                Assert.Less(c.Player.position.z, wall - 1f, "back into the courtyard");
            }
            finally
            {
                mode.Stick.Set(Vector2.zero);
                mode.Exit();
            }
        }

        private static string Touching(Vector3 at)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (Collider col in Physics.OverlapSphere(at + Vector3.up * 0.9f, 0.8f))
            {
                if (col.gameObject.name != "ExplorePlayer")
                {
                    names.Add($"{col.transform.parent?.name}/{col.gameObject.name} {col.bounds.center:F2} {col.bounds.size:F2}");
                }
            }

            return string.Join("; ", names);
        }

        private static IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }
    }
}
