using System;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Tests
{
    /// <summary>Phase 6 step 7: the poser matches Unity's skinning, and the full clipping matrix has no blockers (F16, exit gate).</summary>
    public class ClippingMatrixTests
    {
        [Test]
        public void Poser_AtRest_ReturnsTheRestMesh()
        {
            var poser = new AvatarPoser();
            var pose = new AvatarPose();
            poser.SetPose(pose);
            MeshData body = BodyGenerator.Build();
            V3[] posed = poser.Skin(body, BodyGenerator.Weights(new AvatarProfile()));
            for (int i = 0; i < body.VertexCount; i += 7)
            {
                Assert.Less((posed[i] - body.Positions[i]).Length, 1e-4f);
            }
        }

        [Test]
        public void Poser_UsesUnitysRotationOrder()
        {
            // Quaternion.Euler(0, 0, 90) * right = up; Euler(90, 0, 0) * up = forward; Euler(0, 90, 0) * forward = right.
            Assert.Less((Mat3.Euler(0f, 0f, 90f) * new V3(1f, 0f, 0f) - new V3(0f, 1f, 0f)).Length, 1e-5f);
            Assert.Less((Mat3.Euler(90f, 0f, 0f) * new V3(0f, 1f, 0f) - new V3(0f, 0f, 1f)).Length, 1e-5f);
            Assert.Less((Mat3.Euler(0f, 90f, 0f) * new V3(0f, 0f, 1f) - new V3(1f, 0f, 0f)).Length, 1e-5f);
        }

        [Test]
        public void Poser_RaisedArm_MovesTheHandUp()
        {
            var poser = new AvatarPoser();
            var pose = new AvatarPose();
            pose.Z[AvatarSkeleton.RightUpperArm] = 150f;
            poser.SetPose(pose);
            Assert.Greater(poser.BonePosition(AvatarSkeleton.RightHand).Y, AvatarSkeleton.Rest[AvatarSkeleton.RightUpperArm].Y, "a hand above the shoulder");
        }

        [Test]
        public void FullMatrix_HasNoClippingBlockers()
        {
            var results = ClippingCheck.RunMatrix();
            Assert.AreEqual(WardrobeCatalog.Default.All.Count * ClippingCheck.BodyStyles.Length * ClippingCheck.Poses.Length, results.Count);
            var blockers = results.Where(r => r.Blocker).Select(r => $"{r.Garment} {r.BodyStyle} {r.Pose}: {r.Worst}, stretch {r.Stretch:0.00}").ToList();
            Assert.IsEmpty(blockers, string.Join("\n", blockers));
        }

        [Test]
        public void Check_CatchesALegThroughAHem()
        {
            // A deep squat in a long skirt is exactly what two-bone skinning can't drape: the check must see it.
            var outfit = ClippingCheck.OutfitFor("lehenga", WardrobeCatalog.Default);
            var pose = new AvatarPose();
            AvatarMotion.Evaluate(AvatarClip.Ignite, 0.9f, AnimationStyle.Calm, pose, longHem: false);
            var poser = new AvatarPoser();
            poser.SetPose(pose);
            Assert.IsTrue(ClippingCheck.Check(outfit, WardrobeCatalog.Default, poser).Blocker, "a full squat in a lehenga is flagged");
        }

        [Test]
        public void Report_ListsEveryGarment()
        {
            var results = ClippingCheck.RunMatrix();
            string report = ClippingCheck.Report(results, "test");
            foreach (GarmentInfo g in WardrobeCatalog.Default.All)
            {
                StringAssert.Contains($"| {g.Id} |", report);
            }

            StringAssert.Contains("**Blockers: 0.**", report);
        }
    }
}
