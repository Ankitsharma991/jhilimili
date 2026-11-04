using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.District;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 12 step 5: get on the scooter, ride, brake, honk, park and walk away; traffic stops for you and never touches you.</summary>
    public class ScooterSmokeTests
    {
        [UnityTest]
        public IEnumerator GetOn_Ride_Brake_Honk_Park_AndWalkOff()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(2);
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out DistrictView district));
                Assert.IsTrue(mode.Open());
                yield return null;
                ExploreController c = mode.Controller;
                c.Teleport(ExploreController.ParkingSpot + new Vector3(-1.6f, 0f, 0f), 90f);
                district.Refresh();
                yield return null;
                yield return null;
                Assert.IsNotNull(c.Scooter, "the scooter is parked at the bazaar's end of the lane");
                Assert.AreEqual("Ride the scooter", c.Prompt);
                ClearTheWestArm();

                Assert.IsTrue(c.Interact());
                Assert.IsTrue(c.Riding);
                yield return null;
                Assert.AreEqual(AvatarClip.Ride, c.Player.GetComponent<AvatarAnimator>().Clip, "seated");
                Assert.AreEqual("Horn", JumpLabel(mode));

                // Full throttle into the bazaar.
                float startX = c.Player.position.x;
                mode.Stick.Set(new Vector2(0f, 1f));
                yield return Seconds(2.5f);
                Assert.Greater(c.Speed, 6f, "up to speed");
                Assert.Greater(c.Player.position.x, startX + 10f, "down the bazaar road");
                Assert.IsTrue(district.IsWalkable(c.Player.position));
                Assert.Less(Vector3.Distance(c.Scooter.position, c.Player.position), 0.6f, "the scooter under the rider");
                Assert.IsTrue(string.IsNullOrEmpty(c.Prompt), "too fast to get off");

                // Brake, then honk.
                mode.Stick.Set(new Vector2(0f, -1f));
                yield return Seconds(1.6f);
                mode.Stick.Set(Vector2.zero);
                yield return null;
                Assert.Less(c.Speed, 0.5f, "stopped");
                Assert.AreEqual("Get off", c.Prompt);
                c.Jump();
                Assert.AreEqual(1, c.Horns, "Jump is the horn on the scooter");
                Assert.IsTrue(c.Riding);

                // Park and walk off.
                Vector3 seat = c.Player.position;
                Assert.IsTrue(c.Interact());
                Assert.IsFalse(c.Riding);
                Assert.Less(Vector3.Distance(c.Parked.at, seat), 1f, "parked where it stopped");
                Assert.Less(Vector3.Distance(c.Scooter.position, c.Parked.at), 0.01f);
                yield return null;
                Assert.AreNotEqual(AvatarClip.Ride, c.Player.GetComponent<AvatarAnimator>().Clip, "standing again");
                Assert.AreEqual("Jump", JumpLabel(mode));
                mode.Stick.Set(new Vector2(-1f, 0f));
                yield return Seconds(0.6f);
                Assert.AreNotEqual(Gait.Idle, c.Gait, "walking");
                mode.Stick.Set(Vector2.zero);

                // The scooter stays where it was left, the next time too.
                Vector3 parked = c.Parked.at;
                mode.Exit();
                yield return null;
                Assert.IsNull(c.Scooter, "put away with the bazaar");
                Assert.IsTrue(mode.Open());
                yield return null;
                c.Teleport(parked + new Vector3(0f, 0f, -1.5f), 0f);
                district.Refresh();
                yield return null;
                yield return null;
                Assert.Less(Vector3.Distance(c.Scooter.position, parked), 0.01f, "still where it was parked");
                Assert.AreEqual("Ride the scooter", c.Prompt);
            }
            finally
            {
                mode.Stick.Set(Vector2.zero);
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        [UnityTest]
        public IEnumerator Traffic_Flows_StopsForThePlayer_AndNeverTouches()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(1);
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out DistrictView district));
                Assert.IsTrue(Game.Services.TryGet(out TrafficView traffic));
                Assert.IsTrue(mode.Open());
                yield return null;
                ExploreController c = mode.Controller;

                // Standing in the eastbound lane of the bazaar road.
                var stand = new Vector3(95f, 0f, 7f + TrafficArm.Offset);
                c.Teleport(stand, 0f);
                district.Refresh();
                for (int f = 0; f < 10 && traffic.Sim == null; f++)
                {
                    yield return null;
                }

                Assert.IsNotNull(traffic.Sim, "traffic with the district (mid tier too)");

                // Someone coming along the lane 25 m behind.
                for (int k = 0; k < traffic.Sim.Vehicles.Length; k++)
                {
                    if (traffic.Sim.Vehicles[k].Arm == 0)
                    {
                        traffic.Sim.Vehicles[k].S = stand.x - traffic.Sim.Arms[0].From - 25f;
                        break;
                    }
                }

                float closest = float.MaxValue, end = Time.realtimeSinceStartup + 12f;
                while (Time.realtimeSinceStartup < end)
                {
                    foreach (Vehicle v in traffic.Sim.Vehicles)
                    {
                        closest = Mathf.Min(closest, new Vector2(v.X - c.Player.position.x, v.Z - c.Player.position.z).magnitude);
                    }

                    yield return null;
                }

                Assert.Greater(traffic.Drawn, 2, "vehicles drawn round the player");
                Assert.Greater(closest, 1.6f, "nothing ever touches the player");
                bool waiting = false;
                foreach (Vehicle v in traffic.Sim.Vehicles)
                {
                    waiting |= v.Speed < 0.1f && v.Arm == 0 && v.X < c.Player.position.x && Mathf.Abs(v.Z - stand.z) < 0.5f;
                }

                Assert.IsTrue(waiting, "someone waits behind the player");
                Assert.GreaterOrEqual(traffic.Sim.TotalHonks, 1, "and honks");
                int i = 0;
                for (; i < traffic.Sim.Vehicles.Length && !traffic.VehicleObject(i).activeSelf; i++)
                {
                }

                Vehicle shown = traffic.Sim.Vehicles[i];
                Assert.Less(Vector3.Distance(traffic.VehicleObject(i).transform.position, new Vector3(shown.X, 0f, shown.Z)), 0.5f, "drawn where it is");

                mode.Exit();
                yield return null;
                district.Refresh();
                yield return null;
                Assert.IsNull(traffic.Sim, "put away with the bazaar");
            }
            finally
            {
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        /// <summary>Sends the west arm's vehicles to its far end, so the ride from the lane is clear.</summary>
        private static void ClearTheWestArm()
        {
            Assert.IsTrue(Game.Services.TryGet(out TrafficView traffic));
            Assert.IsNotNull(traffic.Sim);
            TrafficArm arm = traffic.Sim.Arms[0];
            int k = 0;
            for (int i = 0; i < traffic.Sim.Vehicles.Length; i++)
            {
                if (traffic.Sim.Vehicles[i].Arm == 0)
                {
                    traffic.Sim.Vehicles[i].S = arm.Straight - 4f - 7f * k++;
                }
            }
        }

        private static string JumpLabel(ExploreMode mode) => mode.JumpText;

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
