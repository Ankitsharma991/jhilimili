using System;
using NUnit.Framework;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Tests
{
    /// <summary>Phase 6 step 4: the procedural clips stay within human joint limits, loop cleanly and keep the feet down.</summary>
    public class AvatarMotionTests
    {
        // Allowed range (degrees) per bone for X (forward or backward bend), Y (twist) and Z (sideways).
        private static readonly (int bone, float minX, float maxX, float maxY, float maxZ)[] Limits =
        {
            (AvatarSkeleton.Spine, -15f, 45f, 20f, 20f),
            (AvatarSkeleton.Chest, -20f, 30f, 25f, 20f),
            (AvatarSkeleton.Head, -45f, 45f, 70f, 30f),
            (AvatarSkeleton.LeftUpperArm, -170f, 60f, 60f, 170f),
            (AvatarSkeleton.RightUpperArm, -170f, 60f, 60f, 170f),
            (AvatarSkeleton.LeftLowerArm, -150f, 5f, 10f, 10f),
            (AvatarSkeleton.RightLowerArm, -150f, 5f, 10f, 10f),
            (AvatarSkeleton.LeftUpperLeg, -120f, 45f, 20f, 30f),
            (AvatarSkeleton.RightUpperLeg, -120f, 45f, 20f, 30f),
            (AvatarSkeleton.LeftLowerLeg, 0f, 150f, 5f, 5f),
            (AvatarSkeleton.RightLowerLeg, 0f, 150f, 5f, 5f),
            (AvatarSkeleton.LeftFoot, -50f, 50f, 10f, 10f),
            (AvatarSkeleton.RightFoot, -50f, 50f, 10f, 10f),
        };

        private static readonly AvatarClip[] Clips = { AvatarClip.Idle, AvatarClip.Walk, AvatarClip.Ignite, AvatarClip.Celebrate };
        private static readonly AnimationStyle[] Styles = { AnimationStyle.Calm, AnimationStyle.Lively };

        [Test]
        public void EveryClip_StaysWithinJointLimits()
        {
            var pose = new AvatarPose();
            foreach (AvatarClip clip in Clips)
            {
                foreach (AnimationStyle style in Styles)
                {
                    for (float t = 0f; t <= AvatarMotion.Length(clip); t += 0.02f)
                    {
                        AvatarMotion.Evaluate(clip, t, style, pose);
                        foreach (var l in Limits)
                        {
                            Assert.That(pose.X[l.bone], Is.InRange(l.minX, l.maxX), $"{clip} {style} t={t:0.00} {AvatarSkeleton.Names[l.bone]} X");
                            Assert.LessOrEqual(Math.Abs(pose.Y[l.bone]), l.maxY, $"{clip} {style} t={t:0.00} {AvatarSkeleton.Names[l.bone]} Y");
                            Assert.LessOrEqual(Math.Abs(pose.Z[l.bone]), l.maxZ, $"{clip} {style} t={t:0.00} {AvatarSkeleton.Names[l.bone]} Z");
                        }

                        Assert.Less(pose.HipsOffset.Length, 0.35f, $"{clip} hips stay near the body");
                    }
                }
            }
        }

        [TestCase(AvatarClip.Idle)]
        [TestCase(AvatarClip.Walk)]
        public void Loops_HaveNoSeam(AvatarClip clip)
        {
            var start = new AvatarPose();
            var end = new AvatarPose();
            AvatarMotion.Evaluate(clip, 0f, AnimationStyle.Lively, start);
            AvatarMotion.Evaluate(clip, AvatarMotion.Length(clip), AnimationStyle.Lively, end);
            for (int i = 0; i < AvatarSkeleton.Count; i++)
            {
                Assert.AreEqual(start.X[i], end.X[i], 0.05f, AvatarSkeleton.Names[i]);
                Assert.AreEqual(start.Z[i], end.Z[i], 0.05f, AvatarSkeleton.Names[i]);
            }
        }

        [TestCase(AvatarClip.Ignite)]
        [TestCase(AvatarClip.Celebrate)]
        public void OneShots_StartAndEndAtRest(AvatarClip clip)
        {
            var pose = new AvatarPose();
            foreach (float t in new[] { 0f, AvatarMotion.Length(clip) })
            {
                AvatarMotion.Evaluate(clip, t, AnimationStyle.Calm, pose);
                for (int i = 0; i < AvatarSkeleton.Count; i++)
                {
                    Assert.AreEqual(0f, pose.X[i], 1f, $"{clip} t={t} {AvatarSkeleton.Names[i]}");
                    Assert.AreEqual(0f, pose.Z[i], 1f, $"{clip} t={t} {AvatarSkeleton.Names[i]}");
                }
            }
        }

        [Test]
        public void Ignite_CrouchesWithFeetFlatAndOnTheFloor()
        {
            var pose = new AvatarPose();
            AvatarMotion.Evaluate(AvatarClip.Ignite, 0.9f, AnimationStyle.Calm, pose);
            float hip = pose.X[AvatarSkeleton.RightUpperLeg], knee = pose.X[AvatarSkeleton.RightLowerLeg];
            Assert.Less(hip, -40f, "thighs forward");
            Assert.Greater(knee, 70f, "knees bent");
            Assert.AreEqual(0f, hip + knee + pose.X[AvatarSkeleton.RightFoot], 0.01f, "foot flat");
            Assert.AreEqual(-AvatarMotion.CrouchDrop(hip, knee), pose.HipsOffset.Y, 1e-4f, "hips drop by the leg's shortening");
            Assert.Less(pose.X[AvatarSkeleton.RightUpperArm], -45f, "right hand reaches towards the fuse");
        }

        [Test]
        public void CrouchDrop_MatchesTheLegGeometry()
        {
            Assert.AreEqual(0f, AvatarMotion.CrouchDrop(0f, 0f), 1e-5f);
            Assert.Greater(AvatarMotion.CrouchDrop(-60f, 110f), AvatarMotion.CrouchDrop(-30f, 50f));
        }

        [Test]
        public void Lively_MovesMoreThanCalm()
        {
            var calm = new AvatarPose();
            var lively = new AvatarPose();
            AvatarMotion.Evaluate(AvatarClip.Celebrate, 0.6f, AnimationStyle.Calm, calm);
            AvatarMotion.Evaluate(AvatarClip.Celebrate, 0.6f, AnimationStyle.Lively, lively);
            Assert.Greater(Math.Abs(lively.Z[AvatarSkeleton.LeftUpperArm]), Math.Abs(calm.Z[AvatarSkeleton.LeftUpperArm]));
        }
    }
}
