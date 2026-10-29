using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Tests
{
    /// <summary>Phase 8 step 2: chunk detail by distance and tier, hysteresis, low memory, and the settlement's triangle budget.</summary>
    public class ChunkPlannerTests
    {
        /// <summary>Triangles the whole settlement may draw per tier (houses, modules, decorations), excluding fireworks.</summary>
        public static readonly int[] SettlementTriangles = { 150000, 250000, 400000 };

        [Test]
        public void AtHome_DetailFallsAwayFromThePlayer()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            layout.ChunkRange(out int first, out int last);
            var planner = new ChunkPlanner(first, last, 0);
            planner.Update(0f);
            Assert.AreEqual(ChunkDetail.Full, planner.DetailOf(0));
            Assert.AreEqual(ChunkDetail.Simple, planner.DetailOf(1));
            Assert.AreEqual(ChunkDetail.Simple, planner.DetailOf(-1));
            Assert.AreEqual(1, planner.Current.Values.Count(d => d == ChunkDetail.Full), "low tier: one full chunk");

            var high = new ChunkPlanner(first, last, 2);
            high.Update(0f);
            Assert.AreEqual(3, high.Current.Values.Count(d => d == ChunkDetail.Full), "high tier: the player's chunk and both neighbours");
        }

        [Test]
        public void WalkingAlongABoundary_DoesNotRebuildBackAndForth()
        {
            var planner = new ChunkPlanner(-3, 3, 0);
            planner.Update(0f);
            float boundary = 0.5f * SettlementLayout.ChunkSize;
            var up = new List<int>(planner.Update(boundary + 0.1f));
            CollectionAssert.Contains(up, 1, "the next chunk comes up as soon as it is near");
            Assert.AreEqual(ChunkDetail.Full, planner.DetailOf(1));
            Assert.AreEqual(ChunkDetail.Full, planner.DetailOf(0), "the one behind holds for a couple of metres");
            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.IsEmpty(planner.Update(boundary + (i % 2 == 0 ? -0.3f : 0.3f)), "no thrash at the boundary");
            }

            planner.Update(boundary + ChunkPlanner.Hysteresis + 0.5f);
            Assert.AreEqual(ChunkDetail.Simple, planner.DetailOf(0), "and steps down once well past it");
        }

        [Test]
        public void FarChunks_Unload_AndLowMemoryDropsToImpostors()
        {
            var planner = new ChunkPlanner(-6, 6, 0);
            planner.Update(0f);
            Assert.AreEqual(ChunkDetail.Unloaded, planner.DetailOf(6), "beyond the impostor radius nothing is in memory");
            Assert.LessOrEqual(planner.Current.Count, 2 * ChunkPlanner.ImpostorRadius[0] + 1, "memory bounded");
            planner.Update(0f, lowMemory: true);
            Assert.AreEqual(ChunkDetail.Full, planner.DetailOf(0));
            Assert.AreEqual(ChunkDetail.Impostor, planner.DetailOf(1));
        }

        [Test]
        public void Impostor_IsCheapAndStaysInsideTheHouse()
        {
            foreach (string roof in new[] { "roof_flat_parapet", "roof_tiled_slope" })
            {
                HouseDesign d = HouseDesign.Default();
                d.ModuleAt("roof").module = roof;
                HouseMesh m = ImpostorBuilder.Build(d);
                HouseGeneratorTests.AssertWellFormed(m, roof);
                Assert.LessOrEqual(m.TriangleCount, 160, roof);
                Box3 b = m.Bounds();
                Assert.LessOrEqual(b.Max.Y, 8.6f);
                Assert.GreaterOrEqual(b.Min.Z, -HouseGeometry.Depth - 0.31f);
                Assert.LessOrEqual(b.Max.Z, HouseGeometry.BackWallZ + 0.2f);
                Assert.Greater(m.Triangles[(int)HousePaint.Glass].Count, 0, "lit windows");
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void WholeSettlement_StaysWithinTheTiersTriangles(int tier)
        {
            foreach (EnvironmentInfo env in Environments.All)
            {
                SettlementLayout layout = SettlementLibrary.For(env.Id);
                layout.ChunkRange(out int first, out int last);
                foreach (SpotDef standAt in layout.spots)
                {
                    var planner = new ChunkPlanner(first, last, tier);
                    planner.Update(standAt.x);
                    long triangles = 0;
                    foreach (LotDef lot in layout.lots)
                    {
                        HouseDesign design = SettlementLibrary.DesignFor(layout, lot, null);
                        if (design != null)
                        {
                            triangles += LotTriangles(design, planner.DetailOf(SettlementLayout.ChunkOf(lot.x)));
                        }
                    }

                    Assert.LessOrEqual(triangles, SettlementTriangles[tier], $"{env.Id}, standing at {standAt.id}, tier {tier}");
                }
            }
        }

        /// <summary>The triangles a lot draws at a detail level (the view builds exactly these pieces).</summary>
        public static long LotTriangles(HouseDesign design, ChunkDetail detail)
        {
            if (detail == ChunkDetail.Unloaded)
            {
                return 0;
            }

            if (detail == ChunkDetail.Impostor)
            {
                return ImpostorBuilder.Build(design).TriangleCount;
            }

            bool empty = string.IsNullOrEmpty(design.ModuleAt("balcony")?.module);
            long n = HouseGenerator.Shell(empty).TriangleCount;
            foreach (ModuleChoice m in design.modules)
            {
                HouseMesh mesh = string.IsNullOrEmpty(m.module) ? null : HouseGenerator.Module(m.point, m.module);
                n += mesh?.TriangleCount ?? 0;
            }

            foreach (PlacedDecor d in design.decorations)
            {
                DecorInfo info = HomeCatalog.Decoration(d.item);
                if (detail == ChunkDetail.Simple && !ChunkPlanner.KeptAtSimple(info))
                {
                    continue;
                }

                HouseMesh mesh = info.Mode == PlacementMode.Path
                    ? DecorGenerator.Path(d.item, new Core.Avatar.V3(d.x, d.y, d.z), new Core.Avatar.V3(d.ex, d.ey, d.ez), d.sag)
                    : DecorGenerator.Item(d.item);
                n += mesh?.TriangleCount ?? 0;
            }

            return n;
        }
    }
}
