using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.District;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Tests
{
    /// <summary>Phase 12 step 7: where crackers may go in the town, people keeping back, and the district's own shows.</summary>
    public class TownFireworksTests
    {
        private static readonly DistrictPlan Plan = DistrictGenerator.Bazaar;
        private static readonly List<TrafficArm> Arms = TrafficSim.BuildArms(Plan);

        private static TownVerdict Check(float x, float z, bool aerial = false, int intensity = 1, Person[] people = null, float y = 0f, int placed = 0) =>
            TownSafety.Check(Plan, Arms, x, y, z, intensity, aerial, people, placed);

        /// <summary>An open corner of the square, clear of the tower and the roads.</summary>
        private static (float x, float z) SquareSpot => (Plan.Square.X0 + 10f, Plan.Square.Z0 + 10f);

        [Test]
        public void TheSquareAndTheGhat_AreFine_ForAnything()
        {
            (float x, float z) = SquareSpot;
            Assert.AreEqual(TownVerdict.Ok, Check(x, z));
            Assert.AreEqual(TownVerdict.Ok, Check(x, z, aerial: true, intensity: 2));
            foreach (TownShowPlace place in TownShows.Places(Plan))
            {
                foreach ((float px, float pz) in place.Pads)
                {
                    Assert.AreEqual(TownVerdict.Ok, Check(px, pz, aerial: true, intensity: 2), $"{place.Name} pad ({px:0}, {pz:0})");
                }

                Assert.IsTrue(Plan.Bounds.Contains(place.WatchX, place.WatchZ), $"people gather round {place.Name}");
            }
        }

        [Test]
        public void NotOnTheRoad_NorByDoorsStallsOrWires()
        {
            Assert.AreEqual(TownVerdict.OnTheRoad, Check(100f, 7f), "the bazaar road's carriageway");
            Assert.AreEqual(TownVerdict.OnTheRoad, Check(175f, -80f), "the chowk road's");
            Assert.AreEqual(TownVerdict.NotOpenGround, Check(200f, 140f), "the river");

            DistrictProp stall = Plan.Props.First(p => p.Kind == DistrictProp.Stall);
            Assert.AreEqual(TownVerdict.NotOpenGround, Check(stall.X, stall.Z), "on the stall itself");
            Assert.AreEqual(TownVerdict.NearStall, Check(stall.X + 1.8f, stall.Z + (stall.Front == Facing.North ? 0.7f : -0.7f)), "by its awning");

            DistrictProp lamp = Plan.Props.First(p => p.Kind == DistrictProp.Lamp && Plan.IsWalkable(p.X + 1f, p.Z, 0.4f) && !TownSafety.OnCarriageway(Arms, p.X + 1f, p.Z)
                                                       && Plan.Props.All(s => s.Kind != DistrictProp.Stall || Math.Abs(s.X - p.X) > 5f));
            Assert.AreEqual(TownVerdict.NearWires, Check(lamp.X + 1f, lamp.Z), "under the lamp's wires");

            DistrictBuilding house = Plan.Buildings.First(b => b.Front == Facing.South && b.Footprint.Z0 > 60f && b.Footprint.Z0 < 70f);
            (float dx, float dz) = TownSafety.DoorPoint(house);
            Assert.AreEqual(TownVerdict.NearDoor, Check(dx, dz - 1.5f), "on a doorstep");

            // The lane: by a gate, no; in the middle of the street between gates, yes.
            Assert.AreEqual(TownVerdict.NearDoor, Check(SettlementLibrary.LotSpacing, SettlementLibrary.StreetZ0 + 1f));
            Assert.AreEqual(TownVerdict.Ok, Check(SettlementLibrary.LotSpacing * 1.5f, SettlementLibrary.StreetLine + 1.5f));
        }

        [Test]
        public void PeopleKeptBack_ByHowLoud_AndAerialsNeedOpenSky()
        {
            (float x, float z) = SquareSpot;
            var near = new[] { new Person { X = x + 4f, Z = z } };
            Assert.AreEqual(TownVerdict.Ok, Check(x, z, intensity: 0, people: near), "a gentle one, 4 m away is fine");
            Assert.AreEqual(TownVerdict.NearPeople, Check(x, z, intensity: 1, people: near), "a medium one needs 5 m");
            var further = new[] { new Person { X = x + 6f, Z = z } };
            Assert.AreEqual(TownVerdict.Ok, Check(x, z, intensity: 1, people: further));
            Assert.AreEqual(TownVerdict.NearPeople, Check(x, z, intensity: 2, people: further), "a loud one, 8 m");
            Assert.AreEqual(8f, TownSafety.PeopleClear(2, false));
            Assert.AreEqual(6f, TownSafety.PeopleClear(0, true), "anything that flies, 6 m at least");

            // Down a narrow lane, or by the clock tower: a fountain yes, a rocket no.
            DistrictRoad lane = Plan.Roads.First(r => r.Kind == RoadKind.Lane && r.Area.Width < r.Area.Depth);
            float lx = lane.Area.CentreX, lz = -120f;
            Assert.IsTrue(Plan.IsWalkable(lx, lz, 0.4f));
            Assert.AreEqual(TownVerdict.Ok, Check(lx, lz));
            Assert.AreEqual(TownVerdict.NoOpenSky, Check(lx, lz, aerial: true));
            Assert.AreEqual(TownVerdict.NoOpenSky, Check(Plan.Square.CentreX + 5f, Plan.Square.CentreZ, aerial: true), "too near the tower");

            // Up on a roof: fine (the rooftop show).
            DistrictBuilding roof = JobCatalog.RooftopHouse(Plan);
            Assert.AreEqual(TownVerdict.Ok, Check(roof.Footprint.CentreX, roof.Footprint.CentreZ, aerial: true, y: roof.Height));
            Assert.AreEqual(TownVerdict.TooMany, Check(x, z, placed: TownSafety.MaxPlaced));
            foreach (TownVerdict v in Enum.GetValues(typeof(TownVerdict)))
            {
                Assert.AreEqual(v == TownVerdict.Ok, string.IsNullOrEmpty(TownSafety.Explain(v)), $"{v} explained");
            }
        }

        [Test]
        public void TheCrowd_GathersInARing_KeepingBack()
        {
            var crowd = new CrowdSim(Plan, 140, 7);
            TownShowPlace ghat = TownShows.Places(Plan)[0];
            (float px, float pz) = ghat.Pads[2];
            int coming = crowd.GatherAround(px, pz, 90f, 60f, 10f);
            Assert.Greater(coming, 10, "people come to watch");
            float closest = float.MaxValue;
            for (int f = 0; f < 30 * 90; f++)
            {
                crowd.Tick(1f / 30f, -1000f, -1000f, false);
            }

            int watching = 0;
            foreach (Person p in crowd.People)
            {
                if (p.State == PersonState.Watching)
                {
                    watching++;
                    closest = Math.Min(closest, (float)Math.Sqrt((p.X - px) * (p.X - px) + (p.Z - pz) * (p.Z - pz)));
                }
            }

            Assert.Greater(watching, 8, "watching");
            Assert.Greater(closest, 8f, "and keeping back");
        }

        [Test]
        public void Shows_OnATimetable_InTurn_BuildingToAFinale()
        {
            Assert.AreEqual((0, TownShows.FirstAt), TownShows.Next(0f));
            Assert.AreEqual((0, TownShows.FirstAt), TownShows.Next(TownShows.FirstAt + 10f), "still the first while it's on");
            (int n, float at) = TownShows.Next(TownShows.FirstAt + TownShows.Length + 1f);
            Assert.AreEqual(1, n);
            Assert.AreEqual(TownShows.FirstAt + TownShows.Every, at);
            Assert.AreEqual(0, TownShows.PlaceOf(0, 2));
            Assert.AreEqual(1, TownShows.PlaceOf(1, 2), "the ghat, then the chowk");

            List<TownCue> program = TownShows.Program(5, 3);
            Assert.Greater(program.Count, 15);
            Assert.IsTrue(program.Zip(program.Skip(1), (a, b) => b.At >= a.At).All(x => x), "in time order");
            Assert.IsTrue(program.All(c => c.Pad >= 0 && c.Pad < 5 && c.At < TownShows.Length));
            Assert.AreEqual(10, program.Count(c => c.Size == 2), "a finale of big ones from every pad");
            CollectionAssert.AreEqual(program.Select(c => c.At), TownShows.Program(5, 3).Select(c => c.At), "the same every time");
        }
    }
}
