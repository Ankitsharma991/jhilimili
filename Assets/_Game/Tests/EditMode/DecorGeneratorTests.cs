using System;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Tests
{
    /// <summary>Phase 7 step 3: decoration items. Catalogue lint: footprints, budgets, variants (PRD 9.1).</summary>
    public class DecorGeneratorTests
    {
        private static readonly object[][] Placed =
            HomeCatalog.Decorations.Where(d => d.Mode != PlacementMode.Path).Select(d => new object[] { d.Id }).ToArray();

        private static readonly object[][] Paths =
            HomeCatalog.Decorations.Where(d => d.Mode == PlacementMode.Path).Select(d => new object[] { d.Id }).ToArray();

        [TestCaseSource(nameof(Placed))]
        public void Item_FillsItsCatalogueFootprint(string id)
        {
            DecorInfo info = HomeCatalog.Decoration(id);
            HouseMesh mesh = DecorGenerator.Item(id);
            Assert.IsNotNull(mesh, id);
            HouseGeneratorTests.AssertWellFormed(mesh, id);
            Box3 b = mesh.Bounds();
            const float slack = 1.02f;
            Assert.LessOrEqual(Math.Max(-b.Min.X, b.Max.X) * 2f, info.Width * slack, $"{id} width");
            Assert.LessOrEqual(Math.Max(-b.Min.Z, b.Max.Z) * 2f, info.Depth * slack, $"{id} depth");
            Assert.GreaterOrEqual(b.Min.Y, -1e-4f, $"{id} stands on its base");
            Assert.GreaterOrEqual(b.Max.X - b.Min.X, info.Width * 0.6f, $"{id} is much narrower than its footprint");
            Assert.GreaterOrEqual(b.Max.Z - b.Min.Z, info.Depth * 0.6f, $"{id} is much shallower than its footprint");
            if (info.Mode == PlacementMode.Decal)
            {
                Assert.LessOrEqual(b.Max.Y, 0.02f, $"{id}: rangoli lies flat");
                Assert.IsTrue(mesh.Normals.All(n => n.Y > 0.99f), $"{id}: rangoli faces up");
            }
            else
            {
                Assert.LessOrEqual(b.Max.Y, info.Height * slack, $"{id} height");
                Assert.GreaterOrEqual(b.Max.Y, info.Height * 0.85f, $"{id} is much lower than its height");
            }

            Assert.LessOrEqual(mesh.TriangleCount, info.Cost * DecorBudget.TrianglesPerUnit, $"{id}: {mesh.TriangleCount} triangles for {info.Cost} units");
        }

        [TestCaseSource(nameof(Paths))]
        public void Path_HangsBetweenItsEndsWithinBudget(string id)
        {
            DecorInfo info = HomeCatalog.Decoration(id);
            var a = new V3(-1.5f, 2.5f, 0.1f);
            var b = new V3(1.5f, 2.5f, 0.1f);
            HouseMesh mesh = DecorGenerator.Path(id, a, b, 0.3f);
            Assert.IsNotNull(mesh, id);
            HouseGeneratorTests.AssertWellFormed(mesh, id);
            Box3 bounds = mesh.Bounds();
            Assert.AreEqual(a.X, bounds.Min.X, 0.04f, $"{id} starts at its first point");
            Assert.AreEqual(b.X, bounds.Max.X, 0.04f, $"{id} ends at its second point");
            Assert.LessOrEqual(bounds.Max.Y, a.Y + 0.04f, $"{id} hangs from its points (flowers sit around the cord)");
            Assert.GreaterOrEqual(bounds.Min.Y, a.Y - 3f * 0.25f * 0.3f - info.Height - 0.1f, $"{id} hangs no lower than its sag and drop");
            Assert.LessOrEqual(mesh.TriangleCount, DecorBudget.CostOf(info, 3f) * DecorBudget.TrianglesPerUnit,
                $"{id}: {mesh.TriangleCount} triangles over 3 m");

            // The longest allowed span stays within its cost too.
            HouseMesh longest = DecorGenerator.Path(id, new V3(0, 3, 0), new V3(DecorBudget.MaxPathLength, 3, 0), 0.2f);
            Assert.LessOrEqual(longest.TriangleCount, DecorBudget.CostOf(info, DecorBudget.MaxPathLength) * DecorBudget.TrianglesPerUnit, id);
        }

        [Test]
        public void PathPoint_SagsInTheMiddleOnly()
        {
            var a = new V3(0, 2, 0);
            var b = new V3(4, 2, 0);
            Assert.AreEqual(2f, DecorGenerator.PathPoint(a, b, 0.5f, 0f).Y, 1e-5f);
            Assert.AreEqual(2f, DecorGenerator.PathPoint(a, b, 0.5f, 1f).Y, 1e-5f);
            Assert.AreEqual(2f - 0.5f * 4f * 0.25f, DecorGenerator.PathPoint(a, b, 0.5f, 0.5f).Y, 1e-5f);
            Assert.AreEqual(2f, DecorGenerator.PathPoint(a, b, 0f, 0.5f).Y, 1e-5f);
            Assert.IsNull(DecorGenerator.Path("garland_marigold", a, a, 0.3f), "no span");
            Assert.IsNull(DecorGenerator.Path("diya_clay", a, b, 0.3f), "not a path item");
            Assert.IsNull(DecorGenerator.Item("garland_marigold"), "a path item has no single mesh");
        }

        [Test]
        public void Budgets_ATierFullOfTheHeaviestItemStaysWithinItsTriangles()
        {
            for (int tier = 0; tier < DecorBudget.Units.Length; tier++)
            {
                Assert.LessOrEqual(DecorBudget.Units[tier] * DecorBudget.TrianglesPerUnit, DecorBudget.Triangles[tier], $"tier {tier}");
            }

            Assert.AreEqual(4, DecorBudget.CostOf(HomeCatalog.Decoration("string_lights_warm"), 2.9f));
            Assert.AreEqual(8, DecorBudget.CostOf(HomeCatalog.Decoration("string_lights_warm"), 3.1f));
            Assert.AreEqual(8, DecorBudget.CostOf(HomeCatalog.Decoration("string_lights_warm"), 6f));
            Assert.AreEqual(1, DecorBudget.CostOf(HomeCatalog.Decoration("diya_clay"), 99f));
        }

        [Test]
        public void EveryVariant_HasColoursAndSomethingGlows()
        {
            foreach (DecorInfo info in HomeCatalog.Decorations)
            {
                HouseMesh mesh = info.Mode == PlacementMode.Path
                    ? DecorGenerator.Path(info.Id, new V3(0, 2, 0), new V3(2, 2, 0), 0.3f)
                    : DecorGenerator.Item(info.Id);
                var used = Enumerable.Range(0, HouseMesh.PaintCount).Where(p => mesh.Triangles[p].Count > 0).Select(p => (HousePaint)p).ToList();
                var looks = new System.Collections.Generic.HashSet<string>();
                for (int v = 0; v < info.Variants; v++)
                {
                    looks.Add(string.Join(";", used.Select(p => HousePalette.Decor(p, info.Id, v)).Select(c => $"{c.R:0.00},{c.G:0.00},{c.B:0.00}")));
                }

                Assert.AreEqual(info.Variants, looks.Count, $"{info.Id}: every colour variant looks different");
                if (info.Category == DecorCategory.Lamps || info.Category == DecorCategory.Lights)
                {
                    Assert.IsTrue(used.Any(HousePalette.IsEmissive), $"{info.Id} gives light");
                }
            }
        }

        [Test]
        public void Items_BuildTheSameEveryTime()
        {
            foreach (DecorInfo info in HomeCatalog.Decorations.Where(d => d.Mode != PlacementMode.Path))
            {
                Assert.AreEqual(DecorGenerator.Item(info.Id).Bounds().ToString(), DecorGenerator.Item(info.Id).Bounds().ToString());
                Assert.AreEqual(DecorGenerator.Item(info.Id).TriangleCount, DecorGenerator.Item(info.Id).TriangleCount);
            }
        }
    }
}
