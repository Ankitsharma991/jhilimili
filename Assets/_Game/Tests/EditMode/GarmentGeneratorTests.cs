using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Tests
{
    /// <summary>Phase 6 step 3: every catalogue garment is generated, fits every body style and stays light.</summary>
    public class GarmentGeneratorTests
    {
        private static readonly string[] HeadItems = { "dhaka_topi", "turban", "hijab", "ghutra", "cap", "earrings" };

        [Test]
        public void EveryCatalogueGarment_HasAGenerator()
        {
            foreach (GarmentInfo g in WardrobeCatalog.Default.All)
            {
                GarmentMesh mesh = GarmentGenerator.Build(g.Id);
                Assert.IsTrue(mesh.Skinned != null || mesh.HeadRigid != null, g.Id);
                Assert.AreEqual(HeadItems.Contains(g.Id), mesh.HeadRigid != null, $"{g.Id}: head items sit on the Head bone");
            }
        }

        [Test]
        public void SkinnedGarments_CarryTheBodyFitMorphs_AndStayLight()
        {
            string[] bodyShapes = BodyGenerator.Build().BlendShapes.Select(b => b.Key).ToArray();
            foreach (GarmentInfo g in WardrobeCatalog.Default.All)
            {
                MeshData m = GarmentGenerator.Build(g.Id).Skinned;
                if (m == null)
                {
                    continue;
                }

                CollectionAssert.AreEquivalent(bodyShapes, m.BlendShapes.Select(b => b.Key), $"{g.Id} fits every body style");
                Assert.Less(m.VertexCount, 3000, $"{g.Id} vertex budget");
                Assert.AreEqual(2, m.Submeshes.Count, $"{g.Id} main and trim");
                Assert.IsTrue(m.BoneA.All(b => b >= 0 && b < AvatarSkeleton.Count), g.Id);
            }
        }

        [Test]
        public void BroadBodies_GetWiderGarments()
        {
            foreach (string id in new[] { "kurta", "jeans", "lehenga", "abaya" })
            {
                MeshData m = GarmentGenerator.Build(id).Skinned;
                float widen = m.BlendShapes.Single(b => b.Key == BodyGenerator.BlendBroad).Value.Max(d => System.Math.Abs(d.X));
                Assert.Greater(widen, 0.02f, $"{id} widens for a broad body");
            }
        }

        [Test]
        public void SkirtsAndHems_ClearTheLegsOfEveryBodyStyle()
        {
            foreach (float style in new[] { 0f, 1f, 2f })
            {
                var shape = new BodyShape { Style = style, Hips = 1f };
                GarmentGenerator.LegExtent(shape, out float side, out float depth);
                foreach (Ring thigh in new[] { BodyGenerator.Parts(shape, 0f)[BodyGenerator.PartLeftLeg][0], BodyGenerator.Parts(shape, 0f)[BodyGenerator.PartRightLeg][0] })
                {
                    Assert.Greater(side, System.Math.Abs(thigh.Center.X) + thigh.Side, $"style {style}: hem wider than the thighs");
                    Assert.Greater(depth, thigh.Depth, $"style {style}: hem deeper than the thighs");
                }
            }
        }

        [Test]
        public void Colours_ExistForEveryVariant()
        {
            foreach (GarmentInfo g in WardrobeCatalog.Default.All)
            {
                for (int v = 0; v < g.Variants; v++)
                {
                    var c = GarmentLooks.Main(g.Id, v);
                    Assert.That(c.R + c.G + c.B, Is.InRange(0f, 3f));
                }

                var a = GarmentLooks.Main(g.Id, 0);
                var b = GarmentLooks.Main(g.Id, 1);
                Assert.IsTrue(a.R != b.R || a.G != b.G || a.B != b.B, $"{g.Id} variants differ");
            }
        }
    }
}
