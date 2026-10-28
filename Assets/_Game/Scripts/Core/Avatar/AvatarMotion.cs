using System;

namespace UtsavNights.Core.Avatar
{
    public enum AvatarClip
    {
        Idle,
        Walk,

        /// <summary>Crouch, lean and reach the lighter to a fuse with the right hand, then stand (one-shot).</summary>
        Ignite,

        /// <summary>Arms up with a hop, after a burst (one-shot).</summary>
        Celebrate,
    }

    /// <summary>
    /// A pose on the shared skeleton: local Euler angles in degrees per bone (applied by Unity as Z, then X, then Y) and the hips'
    /// offset from rest. Sign conventions on a down-pointing limb: negative X swings it forward (hip flexion, arm raise forward),
    /// positive X on a lower limb bends the knee back; negative Z raises the left arm sideways, positive Z the right.
    /// </summary>
    public sealed class AvatarPose
    {
        public readonly float[] X = new float[AvatarSkeleton.Count];
        public readonly float[] Y = new float[AvatarSkeleton.Count];
        public readonly float[] Z = new float[AvatarSkeleton.Count];
        public V3 HipsOffset;

        public void Clear()
        {
            Array.Clear(X, 0, X.Length);
            Array.Clear(Y, 0, Y.Length);
            Array.Clear(Z, 0, Z.Length);
            HipsOffset = V3.Zero;
        }

        /// <summary>This pose moved towards <paramref name="other"/> by <paramref name="t"/> (crossfades).</summary>
        public void BlendTowards(AvatarPose other, float t)
        {
            for (int i = 0; i < AvatarSkeleton.Count; i++)
            {
                X[i] += (other.X[i] - X[i]) * t;
                Y[i] += (other.Y[i] - Y[i]) * t;
                Z[i] += (other.Z[i] - Z[i]) * t;
            }

            HipsOffset = V3.Lerp(HipsOffset, other.HipsOffset, t);
        }

        public void CopyFrom(AvatarPose other)
        {
            Array.Copy(other.X, X, X.Length);
            Array.Copy(other.Y, Y, Y.Length);
            Array.Copy(other.Z, Z, Z.Length);
            HipsOffset = other.HipsOffset;
        }
    }

    /// <summary>
    /// Procedural clips (Phase 6 step 4): idle, walk, ignite gesture and celebrate on the shared skeleton, in calm or lively
    /// style. Pure functions of time, so they need no animation assets and the tests can sample them. A commissioned rig's
    /// Mecanim clips could replace them later; the bone names already match.
    /// </summary>
    public static class AvatarMotion
    {
        public const float WalkCycle = 1.05f;
        public const float IgniteLength = 1.9f;
        public const float CelebrateLength = 1.7f;
        public const float IdleCycle = 8f;

        private const float ThighLength = 0.41f, ShinLength = 0.41f;

        public static bool Loops(AvatarClip clip) => clip == AvatarClip.Idle || clip == AvatarClip.Walk;

        public static float Length(AvatarClip clip) => clip switch
        {
            AvatarClip.Walk => WalkCycle,
            AvatarClip.Ignite => IgniteLength,
            AvatarClip.Celebrate => CelebrateLength,
            _ => IdleCycle,
        };

        /// <summary>
        /// True when the outfit has a long hem (sari, lehenga, robe, kurta, sherwani). The clips then move the way people do in
        /// them: shorter strides, a bow from the waist instead of a squat, a hop without deep knee bends. Two-bone skinning can't
        /// drape cloth over bent knees, and the clipping check (Phase 6 step 7) found legs pushing through hems in a full squat.
        /// </summary>
        public static bool HasLongHem(AvatarProfile profile)
        {
            foreach (EquippedItem item in profile.outfit)
            {
                if (GarmentGenerator.FollowsLegs(item.id))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Evaluate(AvatarClip clip, float time, AnimationStyle style, AvatarPose pose, bool longHem = false)
        {
            pose.Clear();
            float energy = style == AnimationStyle.Lively ? 1.6f : 1f;
            switch (clip)
            {
                case AvatarClip.Walk:
                    Walk(time, energy, pose, longHem);
                    break;
                case AvatarClip.Ignite:
                    Ignite(time, energy, pose, longHem);
                    break;
                case AvatarClip.Celebrate:
                    Celebrate(time, energy, style, pose, longHem);
                    break;
                default:
                    Idle(time, energy, pose);
                    break;
            }
        }

        /// <summary>Breathing, a slow weight shift and a look around; arms hang relaxed.</summary>
        private static void Idle(float t, float energy, AvatarPose p)
        {
            float breath = Sin(t, 0.25f);
            float shift = Sin(t, 1f / IdleCycle);
            p.X[AvatarSkeleton.Chest] = -1.2f * breath * energy;
            p.Z[AvatarSkeleton.Spine] = 1.5f * shift * energy;
            p.Z[AvatarSkeleton.Hips] = -1f * shift * energy;
            p.HipsOffset = new V3(0.012f * shift * energy, -0.003f * Math.Abs(shift), 0f);
            p.Y[AvatarSkeleton.Head] = 9f * Sin(t, 2f / IdleCycle) * energy;
            p.X[AvatarSkeleton.Head] = 2f * Sin(t, 3f / IdleCycle);
            Arms(p, side: 6f, forward: 2f * breath, elbow: -8f);
        }

        /// <summary>A walking gait in place (moving the avatar through the world is Phase 8).</summary>
        private static void Walk(float t, float energy, AvatarPose p, bool longHem)
        {
            float phase = t / WalkCycle * 2f * (float)Math.PI;
            float swing = (longHem ? 14f : 24f) * Math.Min(1.25f, energy);
            float s = (float)Math.Sin(phase);
            float left = s, right = -s;

            p.X[AvatarSkeleton.LeftUpperLeg] = -swing * left;
            p.X[AvatarSkeleton.RightUpperLeg] = -swing * right;

            // The knee bends while the leg swings forward through the air.
            float kneeLift = longHem ? 22f : 40f;
            p.X[AvatarSkeleton.LeftLowerLeg] = kneeLift * Math.Max(0f, (float)Math.Sin(phase + 1.2f));
            p.X[AvatarSkeleton.RightLowerLeg] = kneeLift * Math.Max(0f, (float)Math.Sin(phase + 1.2f + Math.PI));
            p.X[AvatarSkeleton.LeftFoot] = -0.3f * (p.X[AvatarSkeleton.LeftUpperLeg] + p.X[AvatarSkeleton.LeftLowerLeg]);
            p.X[AvatarSkeleton.RightFoot] = -0.3f * (p.X[AvatarSkeleton.RightUpperLeg] + p.X[AvatarSkeleton.RightLowerLeg]);

            // Arms swing against the legs; the chest counter-turns a little.
            p.X[AvatarSkeleton.LeftUpperArm] = 18f * left * energy * 0.8f;
            p.X[AvatarSkeleton.RightUpperArm] = 18f * right * energy * 0.8f;
            p.Z[AvatarSkeleton.LeftUpperArm] = -5f;
            p.Z[AvatarSkeleton.RightUpperArm] = 5f;
            p.X[AvatarSkeleton.LeftLowerArm] = -12f - 8f * Math.Max(0f, -left);
            p.X[AvatarSkeleton.RightLowerArm] = -12f - 8f * Math.Max(0f, -right);
            p.Y[AvatarSkeleton.Chest] = 5f * s;
            p.Y[AvatarSkeleton.Hips] = -4f * s;
            p.X[AvatarSkeleton.Spine] = 3f;
            p.HipsOffset = new V3(0f, -0.018f + 0.016f * Math.Abs((float)Math.Cos(phase)), 0f);
        }

        /// <summary>
        /// Lower and lean in (0 to 0.55 s), hold the lighter at the fuse (to 1.35 s), stand back up (to 1.9 s). The hips drop exactly
        /// as much as the bent legs shorten, so the feet stay on the floor.
        /// </summary>
        private static void Ignite(float t, float energy, AvatarPose p, bool longHem)
        {
            float down = Ease(Clamp01(t / 0.55f)) * (1f - Ease(Clamp01((t - 1.35f) / 0.55f)));

            // A long hem bows from the waist with soft knees; otherwise a full squat.
            float hip = (longHem ? -12f : -55f) * down, knee = (longHem ? 24f : 95f) * down;
            foreach (int leg in new[] { AvatarSkeleton.LeftUpperLeg, AvatarSkeleton.RightUpperLeg })
            {
                p.X[leg] = hip;
            }

            p.X[AvatarSkeleton.LeftLowerLeg] = knee;
            p.X[AvatarSkeleton.RightLowerLeg] = knee;
            p.X[AvatarSkeleton.LeftFoot] = -(hip + knee);
            p.X[AvatarSkeleton.RightFoot] = -(hip + knee);
            p.HipsOffset = new V3(0f, -CrouchDrop(hip, knee), -0.08f * down);

            p.X[AvatarSkeleton.Spine] = (longHem ? 36f : 22f) * down;
            p.X[AvatarSkeleton.Chest] = (longHem ? 22f : 10f) * down;
            p.X[AvatarSkeleton.Head] = 12f * down;

            // Right arm reaches forward and down to the fuse; a small flick while holding, as the flame catches.
            float flick = t > 0.55f && t < 1.35f ? 3f * Sin(t - 0.55f, 2.5f) * energy : 0f;
            p.X[AvatarSkeleton.RightUpperArm] = (longHem ? -48f : -62f) * down + flick;
            p.Z[AvatarSkeleton.RightUpperArm] = 8f * down;
            p.X[AvatarSkeleton.RightLowerArm] = -18f * down;
            p.X[AvatarSkeleton.LeftUpperArm] = -10f * down;
            p.Z[AvatarSkeleton.LeftUpperArm] = -12f * down;
            p.X[AvatarSkeleton.LeftLowerArm] = -35f * down;
        }

        /// <summary>Arms up and a hop at the burst; lively avatars hop twice and raise their arms higher.</summary>
        private static void Celebrate(float t, float energy, AnimationStyle style, AvatarPose p, bool longHem)
        {
            float up = Ease(Clamp01(t / 0.35f)) * (1f - Ease(Clamp01((t - 1.25f) / 0.45f)));
            float raise = style == AnimationStyle.Lively ? 150f : 115f;
            p.Z[AvatarSkeleton.LeftUpperArm] = -raise * up;
            p.Z[AvatarSkeleton.RightUpperArm] = raise * up;
            p.X[AvatarSkeleton.LeftUpperArm] = -15f * up;
            p.X[AvatarSkeleton.RightUpperArm] = -15f * up;
            p.X[AvatarSkeleton.LeftLowerArm] = -25f * up;
            p.X[AvatarSkeleton.RightLowerArm] = -25f * up;
            p.X[AvatarSkeleton.Head] = -10f * up;
            p.X[AvatarSkeleton.Chest] = -6f * up;

            // Hops: a quick dip then up, once (calm) or twice (lively).
            int hops = style == AnimationStyle.Lively ? 2 : 1;
            float hop = 0f;
            for (int i = 0; i < hops; i++)
            {
                float start = 0.25f + i * 0.45f;
                float h = Clamp01((t - start) / 0.4f);
                hop = Math.Max(hop, (float)Math.Sin(h * Math.PI));
            }

            float height = 0.07f * Math.Min(1.3f, energy) * hop;
            float bend = (longHem ? 8f : 25f) * (1f - hop) * up;
            p.X[AvatarSkeleton.LeftUpperLeg] = -bend;
            p.X[AvatarSkeleton.RightUpperLeg] = -bend;
            p.X[AvatarSkeleton.LeftLowerLeg] = bend * 1.8f;
            p.X[AvatarSkeleton.RightLowerLeg] = bend * 1.8f;
            p.X[AvatarSkeleton.LeftFoot] = -bend * 0.8f;
            p.X[AvatarSkeleton.RightFoot] = -bend * 0.8f;
            p.HipsOffset = new V3(0f, height - CrouchDrop(-bend, bend * 1.8f), 0f);
        }

        /// <summary>How much the hips must drop so feet stay on the floor with the thigh at <paramref name="hip"/> and the knee bent by <paramref name="knee"/> degrees.</summary>
        public static float CrouchDrop(float hip, float knee)
        {
            double thigh = Math.Abs(hip) * Math.PI / 180.0;
            double shin = (knee - Math.Abs(hip)) * Math.PI / 180.0;
            double height = ThighLength * Math.Cos(thigh) + ShinLength * Math.Cos(shin);
            return (float)(ThighLength + ShinLength - height);
        }

        private static void Arms(AvatarPose p, float side, float forward, float elbow)
        {
            p.Z[AvatarSkeleton.LeftUpperArm] = -side;
            p.Z[AvatarSkeleton.RightUpperArm] = side;
            p.X[AvatarSkeleton.LeftUpperArm] = -forward;
            p.X[AvatarSkeleton.RightUpperArm] = -forward;
            p.X[AvatarSkeleton.LeftLowerArm] = elbow;
            p.X[AvatarSkeleton.RightLowerArm] = elbow;
        }

        private static float Sin(float t, float hz) => (float)Math.Sin(2 * Math.PI * hz * t);

        private static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));

        private static float Ease(float t) => t * t * (3f - 2f * t);
    }
}
