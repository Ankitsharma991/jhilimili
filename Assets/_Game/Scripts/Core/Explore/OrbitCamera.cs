using System;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Core.Explore
{
    /// <summary>
    /// The follow camera's maths (Phase 12 step 1): an orbit behind and over the right shoulder of the character, turned by
    /// a drag, zoomed by a pinch, that eases back behind the character once the player stops turning it and starts moving,
    /// pulls in when a wall is in the way, and lags a little so the character leads.
    /// </summary>
    public sealed class OrbitCamera
    {
        public const float MinPitch = -35f;
        public const float MaxPitch = 65f;
        public const float MinDistance = 1.8f;
        public const float MaxDistance = 6f;
        public const float DefaultDistance = 3.4f;
        public const float PivotHeight = 1.55f;
        public const float Shoulder = 0.38f;
        public const float RecentreDelay = 1.4f;
        public const float RecentreRate = 110f;
        public const float WallPadding = 0.22f;
        public const float NearestPull = 0.55f;

        public float Yaw;
        public float Pitch = 12f;
        public float Distance = DefaultDistance;

        /// <summary>Seconds since the player last turned the camera.</summary>
        public float SinceLook = RecentreDelay;

        /// <summary>Turns the camera by a drag in degrees (already scaled by sensitivity).</summary>
        public void Look(float yawDegrees, float pitchDegrees, bool invertY = false)
        {
            Yaw = Wrap(Yaw + yawDegrees);
            Pitch = Math.Max(MinPitch, Math.Min(MaxPitch, Pitch + (invertY ? pitchDegrees : -pitchDegrees)));
            SinceLook = 0f;
        }

        public void Zoom(float metres) => Distance = Math.Max(MinDistance, Math.Min(MaxDistance, Distance + metres));

        /// <summary>Eases the yaw back behind a moving character once the player hasn't turned the camera for a moment.</summary>
        public void Follow(float characterFacing, bool moving, float dt, bool autoRecentre = true)
        {
            SinceLook += dt;
            if (!autoRecentre || !moving || SinceLook < RecentreDelay)
            {
                return;
            }

            Yaw = Wrap(Locomotion.MoveTowardsAngle(Yaw, characterFacing, RecentreRate * dt));
        }

        /// <summary>The point the camera looks at: above the character, over the right shoulder.</summary>
        public V3 Pivot(V3 character)
        {
            double y = Yaw * Math.PI / 180.0;
            return new V3(character.X + Shoulder * (float)Math.Cos(y), character.Y + PivotHeight, character.Z - Shoulder * (float)Math.Sin(y));
        }

        /// <summary>The way from the pivot back to the camera (unit length).</summary>
        public V3 Back()
        {
            double y = Yaw * Math.PI / 180.0, p = Pitch * Math.PI / 180.0;
            return new V3((float)(-Math.Sin(y) * Math.Cos(p)), (float)Math.Sin(p), (float)(-Math.Cos(y) * Math.Cos(p)));
        }

        /// <summary>How far back the camera can sit when a wall is <paramref name="hitDistance"/> away (or none: null).</summary>
        public float Clear(float? hitDistance) =>
            hitDistance.HasValue ? Math.Max(NearestPull, Math.Min(Distance, hitDistance.Value - WallPadding)) : Distance;

        /// <summary>The camera position for a character and a clear distance.</summary>
        public V3 Position(V3 character, float distance)
        {
            V3 pivot = Pivot(character);
            V3 back = Back();
            return new V3(pivot.X + back.X * distance, pivot.Y + back.Y * distance, pivot.Z + back.Z * distance);
        }

        /// <summary>A critically damped approach of <paramref name="current"/> to <paramref name="target"/> (no overshoot).</summary>
        public static float Smooth(float current, float target, ref float velocity, float smoothTime, float dt)
        {
            smoothTime = Math.Max(1e-4f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            float result = target + (change + temp) * exp;
            if ((target - current > 0f) == (result > target))
            {
                result = target;
                velocity = 0f;
            }

            return result;
        }

        private static float Wrap(float a)
        {
            a %= 360f;
            return a < 0f ? a + 360f : a;
        }
    }
}
