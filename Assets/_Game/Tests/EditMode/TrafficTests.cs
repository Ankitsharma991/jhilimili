using System;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.District;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Home;

namespace UtsavNights.Tests
{
    /// <summary>Phase 12 step 5: traffic keeps left, keeps its distance, never touches anyone, and the scooter rides like one.</summary>
    public class TrafficTests
    {
        private static readonly DistrictPlan Plan = DistrictGenerator.Bazaar;
        private const float Far = -1000f;

        [Test]
        public void Arms_OnBothMainRoads_KeepLeft_AndTurnRoundSmoothly()
        {
            var arms = TrafficSim.BuildArms(Plan);
            Assert.AreEqual(4, arms.Count, "the two main roads, each split at the chowk");
            foreach (TrafficArm arm in arms)
            {
                var last = arm.At(0f);
                for (float s = 0.25f; s < arm.Length; s += 0.25f)
                {
                    var at = arm.At(s);
                    float step = (float)Math.Sqrt((at.x - last.x) * (at.x - last.x) + (at.z - last.z) * (at.z - last.z));
                    Assert.Less(step, 0.27f, $"{arm.Road} s={s}: no jumps");
                    Assert.Less(Math.Abs(Locomotion.DeltaAngle(last.heading, at.heading)), 10f, $"{arm.Road} s={s}: no sudden turns");
                    Assert.IsTrue(Plan.IsWalkable(at.x, at.z, 0.4f), $"{arm.Road} s={s} ({at.x:0.0}, {at.z:0.0}): clear of buildings and stalls");
                    Assert.IsFalse(Plan.Square.Contains(at.x, at.z), "the chowk is for people");
                    Assert.Greater(at.x, Plan.Join.X1, "not into the player's lane");
                    if (!at.turning)
                    {
                        // Keeping left: heading east on the north side, west on the south, north on the west side.
                        float h = (at.heading + 360f) % 360f;
                        if (Math.Abs(h - 90f) < 1f)
                        {
                            Assert.AreEqual(arm.Centre + TrafficArm.Offset, at.z, 0.01f, "eastbound keeps to the north half");
                        }
                        else if (Math.Abs(h - 270f) < 1f)
                        {
                            Assert.AreEqual(arm.Centre - TrafficArm.Offset, at.z, 0.01f);
                        }
                        else if (h < 1f || h > 359f)
                        {
                            Assert.AreEqual(arm.Centre - TrafficArm.Offset, at.x, 0.01f, "northbound keeps to the west half");
                        }
                    }

                    last = at;
                }
            }
        }

        [Test]
        public void Vehicles_GetAbout_AndKeepTheirDistance()
        {
            var sim = new TrafficSim(Plan, 5);
            Assert.GreaterOrEqual(sim.Vehicles.Length, 12);
            Assert.IsTrue(sim.Vehicles.Any(v => v.Kind == VehicleKind.Scooter) && sim.Vehicles.Any(v => v.Kind == VehicleKind.Auto)
                && sim.Vehicles.Any(v => v.Kind == VehicleKind.Cycle), "scooters, autos and cyclists");
            var travelled = new float[sim.Vehicles.Length];
            for (int f = 0; f < 30 * 180; f++)
            {
                sim.Tick(1f / 30f, Far, Far, 0.3f, null);
                for (int i = 0; i < sim.Vehicles.Length; i++)
                {
                    travelled[i] += sim.Vehicles[i].Speed / 30f;
                    for (int k = i + 1; k < sim.Vehicles.Length; k++)
                    {
                        if (sim.Vehicles[k].Arm != sim.Vehicles[i].Arm)
                        {
                            continue;
                        }

                        float dx = sim.Vehicles[k].X - sim.Vehicles[i].X, dz = sim.Vehicles[k].Z - sim.Vehicles[i].Z;
                        Assert.Greater(dx * dx + dz * dz, 1.6f * 1.6f, $"frame {f}: {i} and {k} never touch");
                    }
                }
            }

            Assert.Greater(travelled.Min(), 150f, "everyone gets about in three minutes");
            Assert.AreEqual(0, sim.TotalHonks, "no honking on a clear road");
        }

        [Test]
        public void StopsShortOfThePlayer_Honks_ThenGoesOn()
        {
            var sim = new TrafficSim(Plan, 5);
            int i = Lone(sim, out TrafficArm arm);
            var ahead = arm.At(sim.Vehicles[i].S + 14f);
            for (int f = 0; f < 30 * 6; f++)
            {
                sim.Tick(1f / 30f, ahead.x, ahead.z, 0.3f, null);
                Assert.Greater(Distance(sim.Vehicles[i], ahead.x, ahead.z), 2.2f, $"frame {f}: stops short");
            }

            Assert.Less(sim.Vehicles[i].Speed, 0.05f, "waiting");
            Assert.GreaterOrEqual(sim.TotalHonks, 1, "a honk for the player in the way");
            float s = sim.Vehicles[i].S;
            for (int f = 0; f < 30 * 3; f++)
            {
                sim.Tick(1f / 30f, Far, Far, 0.3f, null);
            }

            Assert.Greater((sim.Vehicles[i].S - s + arm.Length) % arm.Length, 5f, "and goes on once the way is clear");
        }

        [Test]
        public void StopsForSomeoneCrossing_NotForTheFootpath()
        {
            var sim = new TrafficSim(Plan, 5);
            int i = Lone(sim, out TrafficArm arm);
            var crossing = arm.At(sim.Vehicles[i].S + 12f);
            float footX = arm.AlongX ? crossing.x : arm.Centre + 2.8f, footZ = arm.AlongX ? arm.Centre + 2.8f : crossing.z;
            var people = new[] { new Person { X = crossing.x, Z = crossing.z }, new Person { X = footX, Z = footZ } };
            for (int f = 0; f < 30 * 5; f++)
            {
                sim.Tick(1f / 30f, Far, Far, 0.3f, people);
                Assert.Greater(Distance(sim.Vehicles[i], crossing.x, crossing.z), 2.2f, "never reaches the person crossing");
            }

            Assert.Less(sim.Vehicles[i].Speed, 0.05f);
            Assert.AreEqual(0, sim.TotalHonks, "no honking at people");

            people[0].X = Far;
            float s = sim.Vehicles[i].S;
            for (int f = 0; f < 30 * 3; f++)
            {
                sim.Tick(1f / 30f, Far, Far, 0.3f, people);
            }

            Assert.Greater((sim.Vehicles[i].S - s + arm.Length) % arm.Length, 5f, "past the person on the footpath without stopping");
        }

        [Test]
        public void TheSameEveryRun()
        {
            var a = new TrafficSim(Plan, 9);
            var b = new TrafficSim(Plan, 9);
            for (int f = 0; f < 600; f++)
            {
                a.Tick(1f / 30f, 60f, 6f, 0.3f, null);
                b.Tick(1f / 30f, 60f, 6f, 0.3f, null);
            }

            for (int i = 0; i < a.Vehicles.Length; i++)
            {
                Assert.AreEqual(a.Vehicles[i].X, b.Vehicles[i].X);
                Assert.AreEqual(a.Vehicles[i].Z, b.Vehicles[i].Z);
            }
        }

        [Test]
        public void TheHorn_MovesPeopleOutOfTheWay_AndWalkersStepAsideForTraffic()
        {
            var crowd = new CrowdSim(Plan, 0, 3);
            int keepers = crowd.People.Length;
            var shop = crowd.People[0];
            Assert.IsTrue(shop.Shopkeeper);

            // A resident standing on the bazaar road ahead of the scooter.
            var withOne = new CrowdSim(Plan, 1, 3);
            ref Person p = ref withOne.People[0];
            p.X = 60f;
            p.Z = 6.2f;
            p.State = PersonState.Standing;
            p.Timer = 100f;
            int moved = withOne.Horn(55f, 6f, 90f);
            Assert.AreEqual(1, moved);
            Assert.Greater(Math.Abs(withOne.People[0].Z - 6f), 1f, "stepped clear of the scooter's line");
            Assert.Less(Math.Abs(withOne.People[0].X - 60f), 0.01f, "sideways, not along the road");
            Assert.AreEqual(0, withOne.Horn(55f, 6f, 270f), "nobody behind is bothered");
            Assert.AreEqual(keepers + 1, withOne.People.Length);
        }

        [Test]
        public void Scooter_SpeedsUp_ToTownSpeed_AndNoFaster()
        {
            var s = new ScooterState { Heading = 90f };
            var forward = new ScooterInput(1f, 0f);
            float t = 0f;
            while (s.Speed < 10.5f && t < 10f)
            {
                s = ScooterModel.Step(s, forward, 1f / 60f);
                t += 1f / 60f;
                Assert.LessOrEqual(s.Speed, ScooterModel.MaxSpeed + 1e-3f);
            }

            Assert.Less(t, 4f, "to 38 km/h in under four seconds");
            Assert.Greater(t, 2f, "but not instantly");
            Assert.AreEqual(40, ScooterModel.Kmh(ScooterModel.MaxSpeed));
            for (int f = 0; f < 120; f++)
            {
                s = ScooterModel.Step(s, new ScooterInput(0.5f, 0f), 1f / 60f);
            }

            Assert.AreEqual(ScooterModel.MaxSpeed * 0.5f, s.Speed, 0.6f, "half a push, half the speed");
        }

        [Test]
        public void Scooter_Brakes_Coasts_AndRollsBack()
        {
            var s = new ScooterState { Heading = 0f, Speed = ScooterModel.MaxSpeed };
            for (int f = 0; f < 120; f++)
            {
                s = ScooterModel.Step(s, new ScooterInput(0f, 0f), 1f / 60f);
            }

            Assert.AreEqual(ScooterModel.MaxSpeed - 2f * ScooterModel.CoastDeceleration, s.Speed, 0.05f, "let go: it coasts down");

            s.Speed = ScooterModel.MaxSpeed;
            float travelled = 0f;
            var back = new ScooterInput(0f, -1f);
            while (s.Speed > 0.25f)
            {
                s = ScooterModel.Step(s, back, 1f / 60f);
                travelled += s.Speed / 60f;
                Assert.IsTrue(s.Braking);
            }

            Assert.Less(travelled, ScooterModel.StoppingDistance(ScooterModel.MaxSpeed) + 0.5f, "stops in under 7 m from 40 km/h");
            Assert.AreEqual(0f, s.Heading, 0.01f, "braking doesn't turn it");
            for (int f = 0; f < 240; f++)
            {
                s = ScooterModel.Step(s, back, 1f / 60f);
            }

            Assert.AreEqual(-ScooterModel.ReverseSpeed, s.Speed, 0.01f, "held back when stopped: it rolls back, slowly");
        }

        [Test]
        public void Scooter_Turns_NoTighterThanItCan_AndLeansIn()
        {
            var s = new ScooterState { Heading = 0f, Speed = 8f };
            var right = new ScooterInput(1f, 0f);
            for (int f = 0; f < 30; f++)
            {
                s = ScooterModel.Step(s, right, 1f / 60f);
                Assert.LessOrEqual(Math.Abs(s.YawRate), ScooterModel.MaxYawRate + 1e-3f);
                float radius = s.Speed / (Math.Abs(s.YawRate) * (float)Math.PI / 180f);
                Assert.GreaterOrEqual(radius, ScooterModel.MinTurnRadius - 0.01f, "no tighter than a scooter turns");
            }

            Assert.Greater(s.Heading, 20f, "turning right");
            Assert.Greater(s.Lean, 8f, "leaning right into it");
            Assert.LessOrEqual(s.Lean, ScooterModel.MaxLean);
            for (int f = 0; f < 240; f++)
            {
                s = ScooterModel.Step(s, right, 1f / 60f);
            }

            Assert.AreEqual(90f, s.Heading, 0.5f, "settles on where the stick points");
            Assert.AreEqual(0f, s.Lean, 1f, "and straightens up");

            // From a standstill, the stick to one side: it moves off and turns.
            var still = new ScooterState { Heading = 0f };
            for (int f = 0; f < 90; f++)
            {
                still = ScooterModel.Step(still, new ScooterInput(-1f, 0.2f), 1f / 60f);
            }

            Assert.Greater(still.Speed, 1f);
            Assert.Greater(Math.Abs(Locomotion.DeltaAngle(0f, still.Heading)), 10f, "turning left");
            Assert.Less(Locomotion.DeltaAngle(0f, still.Heading), 0f);
        }

        [Test]
        public void Scooter_Bumps_AndSounds()
        {
            var s = new ScooterState { Speed = 10f };
            Assert.AreEqual(10f, ScooterModel.Bump(s, 0.9f).Speed, "a graze keeps going");
            Assert.Less(ScooterModel.Bump(s, 0.1f).Speed, 1f, "a knock nearly stops it");

            float[] engine = VehicleSounds.Engine(44100);
            Assert.AreEqual((int)(44100 * VehicleSounds.EngineSeconds), engine.Length);
            Assert.Less(engine.Max(Math.Abs), 1f);
            Assert.Less(Math.Abs(engine[0] - engine[engine.Length - 1]), 0.15f, "loops without a click");
            float[] horn = VehicleSounds.Horn(44100);
            Assert.Less(horn.Max(Math.Abs), 1f);
            Assert.Greater(horn.Max(Math.Abs), 0.2f);
        }

        [Test]
        public void VehicleKit_EveryKindAndPaint_IsSmall_WithAHeadlamp()
        {
            foreach (VehicleKind kind in Enum.GetValues(typeof(VehicleKind)))
            {
                for (int v = 0; v < VehicleKit.Variants; v++)
                {
                    HouseMesh m = VehicleKit.Build(kind, v);
                    Assert.Less(m.TriangleCount, 1500, $"{kind} {v}");
                    Assert.Greater(m.TriangleCount, 50);
                    Box3 b = m.Bounds();
                    Assert.LessOrEqual(b.Max.X, TrafficSim.HalfWidth(kind) + 0.05f, $"{kind} within its width");
                    Assert.LessOrEqual(b.Max.Z, TrafficSim.HalfLength(kind) + 0.05f, $"{kind} within its length");
                    Assert.GreaterOrEqual(b.Min.Y, -0.01f, "on the ground");
                }
            }

            Assert.Less(VehicleKit.Build(VehicleKind.Scooter, 0, rider: false).TriangleCount, VehicleKit.Build(VehicleKind.Scooter, 0).TriangleCount,
                "the player's scooter has no rider");
        }

        /// <summary>A vehicle on a straight with the road ahead of it cleared: the others on its arm moved round the loop.</summary>
        private static int Lone(TrafficSim sim, out TrafficArm arm)
        {
            const int i = 0;
            arm = sim.Arms[sim.Vehicles[i].Arm];
            sim.Vehicles[i].S = 2f;
            int k = 0;
            for (int j = 1; j < sim.Vehicles.Length; j++)
            {
                if (sim.Vehicles[j].Arm == sim.Vehicles[i].Arm)
                {
                    sim.Vehicles[j].S = arm.Straight + arm.Turn + 8f + 8f * k++;
                    var at = arm.At(sim.Vehicles[j].S);
                    sim.Vehicles[j].X = at.x;
                    sim.Vehicles[j].Z = at.z;
                    sim.Vehicles[j].Speed = 0f;
                }
            }

            var me = arm.At(2f);
            sim.Vehicles[i].X = me.x;
            sim.Vehicles[i].Z = me.z;
            sim.Vehicles[i].Heading = me.heading;
            sim.Vehicles[i].Speed = sim.Vehicles[i].Cruise;
            return i;
        }

        private static float Distance(Vehicle v, float x, float z) => (float)Math.Sqrt((v.X - x) * (v.X - x) + (v.Z - z) * (v.Z - z));
    }
}
