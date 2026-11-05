using System;

namespace UtsavNights.Core.Explore
{
    /// <summary>
    /// Helps the character through a narrow gate in a wall (the home's courtyard gate, 0.78 m wide). With a thumbstick
    /// nobody walks exactly at a gap that size, so a walk aimed at the wall near the gate is turned towards a point just
    /// through it: more the closer to the wall, never against what the player pushes (only a push that heads at the wall
    /// is helped), and nothing beyond <see cref="Reach"/> of the gate or <see cref="Depth"/> from the wall.
    /// </summary>
    public static class GateAssist
    {
        /// <summary>How far either side of the gate's middle the help works, along the wall.</summary>
        public const float Reach = 1.8f;

        /// <summary>Inside the home's courtyard the whole wall leads to the gate: pushing at it has no other use.</summary>
        public const float CourtyardReach = 4.8f;

        /// <summary>How far from the wall it starts.</summary>
        public const float Depth = 1.6f;

        /// <summary>The point it aims for, this far through the gate.</summary>
        public const float Through = 0.8f;

        /// <summary>A push needs at least this much of itself towards the wall to be helped.</summary>
        public const float MinToward = 0.3f;

        /// <summary>
        /// The walk (<paramref name="wx"/>, <paramref name="wz"/>: a world direction, any length up to 1) for a character at
        /// (<paramref name="px"/>, <paramref name="pz"/>) near a wall that runs along x at <paramref name="wallZ"/> with a gate
        /// at <paramref name="gateX"/>. The length is kept: the player's push sets the speed.
        /// </summary>
        public static (float wx, float wz) Steer(float px, float pz, float wx, float wz, float gateX, float wallZ, float reach = Reach)
        {
            float length = (float)Math.Sqrt(wx * wx + wz * wz);
            float toWall = wallZ - pz;
            float side = toWall >= 0f ? 1f : -1f;
            float toward = wz * side;
            if (length < 1e-3f || Math.Abs(toWall) > Depth || Math.Abs(px - gateX) > reach || toward < MinToward * length)
            {
                return (wx, wz);
            }

            // Where to: just through the gate. How much: from a little at Depth to nearly all of it at the wall.
            float ax = gateX - px, az = toWall + side * Through;
            float aimLength = (float)Math.Sqrt(ax * ax + az * az);
            ax /= aimLength;
            az /= aimLength;
            float weight = 0.25f + 0.65f * (1f - Math.Abs(toWall) / Depth);
            float x = wx / length * (1f - weight) + ax * weight;
            float z = wz / length * (1f - weight) + az * weight;
            float n = (float)Math.Sqrt(x * x + z * z);
            return n < 1e-4f ? (wx, wz) : (x / n * length, z / n * length);
        }
    }
}
