using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.District;

namespace UtsavNights.Tests
{
    /// <summary>Phase 12 step 3: the bazaar district is joined, reachable, tidy, inside its budgets, and streams by tile.</summary>
    public class DistrictTests
    {
        private static DistrictPlan Bazaar => DistrictGenerator.Bazaar;

        [Test]
        public void TheBazaar_PassesItsLint()
        {
            CollectionAssert.IsEmpty(DistrictLint.Check(Bazaar));
            Assert.Greater(Bazaar.Buildings.Count, 150, "a town's worth of buildings");
            Assert.Less(Bazaar.Buildings.Count, 600);
            Assert.IsTrue(Bazaar.Buildings.Any(b => b.Shop) && Bazaar.Buildings.Any(b => !b.Shop));
            Assert.IsTrue(Bazaar.Buildings.Any(b => b.Floors >= 4) && Bazaar.Buildings.Any(b => b.Floors == 1));
            Assert.AreEqual(1, Bazaar.Props.Count(p => p.Kind == DistrictProp.Tower), "one clock tower");
            Assert.Greater(Bazaar.Props.Count(p => p.Kind == DistrictProp.Stall), 20);
        }

        [Test]
        public void SameEveryTime_FromItsSeed()
        {
            DistrictPlan a = DistrictGenerator.Make(2027), b = DistrictGenerator.Make(2027);
            Assert.AreEqual(a.Buildings.Count, b.Buildings.Count);
            for (int i = 0; i < a.Buildings.Count; i++)
            {
                Assert.AreEqual(a.Buildings[i].Footprint.X0, b.Buildings[i].Footprint.X0);
                Assert.AreEqual(a.Buildings[i].Floors, b.Buildings[i].Floors);
            }

            Assert.AreNotEqual(a.Buildings.Count + a.Buildings[3].Floors * 1000, DistrictGenerator.Make(7).Buildings.Count + DistrictGenerator.Make(7).Buildings[3].Floors * 1000,
                "another seed is another town");
        }

        [Test]
        public void Walkable_StreetsSquareAndGhat_NotBuildingsRiverOrStalls()
        {
            DistrictPlan p = Bazaar;
            Assert.IsTrue(p.IsWalkable(30f, 6.8f), "where the lane's street comes in");
            Assert.IsTrue(p.IsWalkable(160f, 20f), "the square");
            Assert.IsTrue(p.IsWalkable(200f, 120f), "the ghat steps");
            Assert.IsFalse(p.IsWalkable(200f, 140f), "the river");
            Assert.IsFalse(p.IsWalkable(p.Square.CentreX, p.Square.CentreZ), "the clock tower");
            DistrictBuilding b = p.Buildings[10];
            Assert.IsFalse(p.IsWalkable(b.Footprint.CentreX, b.Footprint.CentreZ), "inside a building");
            DistrictProp stall = p.Props.First(s => s.Kind == DistrictProp.Stall);
            Assert.IsFalse(p.IsWalkable(stall.X, stall.Z));
            Assert.IsFalse(p.IsWalkable(0f, 6.8f), "the lane itself isn't the district's");
            foreach (DistrictBuilding x in p.Buildings)
            {
                Assert.IsTrue(DistrictLint.Fronts(p, x), $"building {x.Id} faces a road");
            }
        }

        [Test]
        public void Tiles_FullNearSimpleFartherHiddenBeyond_ByTier()
        {
            DistrictPlan p = Bazaar;
            (int tx, int tz) = p.TileOf(175f, 7f);
            Assert.AreEqual(TileDetail.Full, TilePlanner.Detail(p, tx, tz, 175f, 7f, 2));
            Assert.AreEqual(TileDetail.Hidden, TilePlanner.Detail(p, tx, tz, 175f, 7f, 0), "the low tier keeps to the lane");
            Assert.AreEqual(TileDetail.Hidden, TilePlanner.Detail(p, p.TilesX - 1, p.TilesZ - 1, 30f, -130f, 2), "the far corner");
            int simple = 0;
            for (int x = 0; x < p.TilesX; x++)
            {
                for (int z = 0; z < p.TilesZ; z++)
                {
                    simple += TilePlanner.Detail(p, x, z, 175f, 7f, 1) == TileDetail.Simple ? 1 : 0;
                    int full = DistrictKit.Tile(p, x, z, simple: false).TriangleCount;
                    int lite = DistrictKit.Tile(p, x, z, simple: true).TriangleCount;
                    Assert.LessOrEqual(lite, full, "simple is never heavier");
                }
            }

            Assert.Greater(simple, 0);
            Assert.AreEqual(6, p.TilesX);
            Assert.AreEqual(6, p.TilesZ);
        }
    }
}
