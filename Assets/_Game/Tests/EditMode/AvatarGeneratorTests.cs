using System;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Tests
{
    /// <summary>Phase 6 step 2: the procedural skeleton, body and head are well formed and fit a phone's budget.</summary>
    public class AvatarGeneratorTests
    {
        private static readonly MeshData Body = BodyGenerator.Build();

        [Test]
        public void Skeleton_UsesHumanoidNamesAndAValidHierarchy()
        {
            Assert.AreEqual(AvatarSkeleton.Count, AvatarSkeleton.Names.Length);
            Assert.AreEqual(AvatarSkeleton.Count, AvatarSkeleton.Parents.Length);
            Assert.AreEqual(AvatarSkeleton.Count, AvatarSkeleton.Rest.Length);
            CollectionAssert.IsSubsetOf(new[] { "Hips", "Spine", "Chest", "Neck", "Head", "LeftUpperArm", "RightLowerLeg", "LeftFoot" }, AvatarSkeleton.Names);
            for (int i = 1; i < AvatarSkeleton.Count; i++)
            {
                Assert.Less(AvatarSkeleton.Parents[i], i, "parents come before children");
            }

            Assert.AreEqual(-1, AvatarSkeleton.Parents[AvatarSkeleton.Hips]);
        }

        [Test]
        public void Body_IsValidAndWithinBudget()
        {
            Assert.Less(Body.VertexCount, 4000, "body vertices for low-tier phones");
            Assert.Less(Body.TriangleCount, 6000);
            for (int i = 0; i < Body.VertexCount; i++)
            {
                V3 p = Body.Positions[i];
                Assert.IsFalse(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z), $"vertex {i}");
                Assert.That(Body.BoneA[i], Is.InRange(0, AvatarSkeleton.Count - 1));
                Assert.That(Body.BoneB[i], Is.InRange(0, AvatarSkeleton.Count - 1));
                Assert.That(Body.WeightB[i], Is.InRange(0f, 1f));
            }

            foreach (int index in Body.Submeshes.SelectMany(s => s))
            {
                Assert.That(index, Is.InRange(0, Body.VertexCount - 1));
            }
        }

        [Test]
        public void Body_StandsOnTheFloorAndIsSymmetric()
        {
            float lowest = Body.Positions.Min(p => p.Y);
            Assert.That(lowest, Is.InRange(-0.01f, 0.02f), "soles at the floor");
            float highest = Body.Positions.Max(p => p.Y);
            Assert.That(highest, Is.InRange(1.5f, 1.6f), "the body reaches the neck; the head sits on top");
            Assert.AreEqual(Body.Positions.Min(p => p.X), -Body.Positions.Max(p => p.X), 0.002f, "left and right match");
        }

        [Test]
        public void BlendShapes_CoverEveryBodyAndWidthSlider()
        {
            string[] names = Body.BlendShapes.Select(b => b.Key).ToArray();
            CollectionAssert.AreEquivalent(new[]
            {
                BodyGenerator.BlendSlim, BodyGenerator.BlendBroad, BodyGenerator.BlendShouldersWide, BodyGenerator.BlendShouldersNarrow,
                BodyGenerator.BlendHipsWide, BodyGenerator.BlendHipsNarrow,
            }, names);
            foreach (var shape in Body.BlendShapes)
            {
                Assert.AreEqual(Body.VertexCount, shape.Value.Length);
                Assert.Greater(shape.Value.Max(d => d.Length), 0.005f, $"{shape.Key} moves the surface");
            }

            float broadest = Body.BlendShapes.Single(b => b.Key == BodyGenerator.BlendBroad).Value.Max(d => Math.Abs(d.X));
            float slimmest = Body.BlendShapes.Single(b => b.Key == BodyGenerator.BlendSlim).Value.Max(d => Math.Abs(d.X));
            Assert.Greater(broadest, slimmest, "broad differs from average more than slim does");
        }

        [Test]
        public void Weights_PickOneSideOfEachSlider()
        {
            var p = new AvatarProfile { bodyStyle = 0.25f, shoulders = 0.5f, hips = -1f };
            var w = BodyGenerator.Weights(p).ToDictionary(k => k.Key, k => k.Value);
            Assert.AreEqual(75f, w[BodyGenerator.BlendSlim], 1e-3f);
            Assert.AreEqual(0f, w[BodyGenerator.BlendBroad]);
            Assert.AreEqual(50f, w[BodyGenerator.BlendShouldersWide], 1e-3f);
            Assert.AreEqual(0f, w[BodyGenerator.BlendShouldersNarrow]);
            Assert.AreEqual(100f, w[BodyGenerator.BlendHipsNarrow], 1e-3f);
        }

        [Test]
        public void SkinMask_HidesOnlyCoveredRegions_AndNeverTheHands()
        {
            int all = Body.TrianglesExcept(BodyGenerator.SkinSubmesh, BodyRegion.None).Count;
            int dressed = Body.TrianglesExcept(BodyGenerator.SkinSubmesh, BodyRegion.Torso | BodyRegion.Arms | BodyRegion.Hips | BodyRegion.Legs | BodyRegion.Feet).Count;
            Assert.Less(dressed, all / 2, "a full outfit hides most of the body");
            Assert.Greater(dressed, 0, "hands, neck and head stay");
            Assert.IsTrue(Body.TriangleRegions[0].Contains(BodyRegion.None), "hands are never masked");
        }

        [Test]
        public void Head_BuildsEveryOptionWithinBudget()
        {
            for (int face = 0; face < AvatarOptions.FaceShapes; face++)
            {
                for (int hair = 0; hair < AvatarOptions.HairStyles; hair++)
                {
                    var p = new AvatarProfile { faceShape = face, hairStyle = hair, facialHair = hair % AvatarOptions.FacialHair, glasses = hair % AvatarOptions.Glasses };
                    MeshData head = HeadGenerator.Build(p, hideHair: false);
                    Assert.AreEqual(3, head.Submeshes.Count);
                    Assert.Less(head.VertexCount, 9000, $"face {face} hair {hair}");
                    Assert.Greater(head.Submeshes[HeadGenerator.Skin].Count, 0);
                    Assert.Greater(head.Submeshes[HeadGenerator.Features].Count, 0, "eyes and mouth");
                    Assert.Greater(head.Submeshes[HeadGenerator.Hair].Count, 0, "brows are always there, even with a shaved head");
                }
            }
        }

        [Test]
        public void Headwear_ThatHidesHair_RemovesTheHairstyle()
        {
            var p = new AvatarProfile { hairStyle = 5, facialHair = 0 };
            int withHair = HeadGenerator.Build(p, hideHair: false).Submeshes[HeadGenerator.Hair].Count;
            int hidden = HeadGenerator.Build(p, hideHair: true).Submeshes[HeadGenerator.Hair].Count;
            Assert.Greater(withHair, hidden * 4, "only the brows remain");
        }

        [Test]
        public void Glasses_AddFrames()
        {
            int bare = HeadGenerator.Build(new AvatarProfile { glasses = 0 }, false).Submeshes[HeadGenerator.Features].Count;
            for (int g = 1; g < AvatarOptions.Glasses; g++)
            {
                Assert.Greater(HeadGenerator.Build(new AvatarProfile { glasses = g }, false).Submeshes[HeadGenerator.Features].Count, bare);
            }
        }
    }
}
