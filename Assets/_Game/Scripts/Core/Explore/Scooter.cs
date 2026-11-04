using System;

namespace UtsavNights.Core.Explore
{
    /// <summary>What the rider asks for: the stick turned into the world (where to go and how hard), already camera-relative.</summary>
    public readonly struct ScooterInput
    {
        public ScooterInput(float worldX, float worldZ)
        {
            WorldX = worldX;
            WorldZ = worldZ;
        }

        public float WorldX { get; }

        public float WorldZ { get; }
    }

    public struct ScooterState
    {
        /// <summary>Degrees, Unity's yaw (0 faces +z, 90 faces +x).</summary>
        public float Heading;

        /// <summary>Metres a second along the heading; negative when rolling back.</summary>
        public float Speed;

        /// <summary>Degrees of roll into the turn (positive leans right).</summary>
        public float Lean;

        /// <summary>Degrees a second of turn last step.</summary>
        public float YawRate;

        public float Throttle;
        public bool Braking;
    }

    /// <summary>
    /// The player's scooter (Phase 12 step 5): an arcade ride for a thumb. Point the stick where to go: the scooter speeds
    /// up by how far it is pushed and steers towards it, turning tighter at low speed and wider at speed (a smallest circle
    /// of 2.4 m, no more than 100°/s); pull the stick back against the way it faces to brake, and hold it there when
    /// stopped to roll back slowly. It leans into turns by the speed and the turn, as a real two-wheeler must. Town speeds:
    /// 40 km/h at most. Pure maths, so tests can drive it.
    /// </summary>
    public static class ScooterModel
    {
        public const float MaxSpeed = 11f;
        public const float ReverseSpeed = 1.6f;
        public const float Acceleration = 3.6f;
        public const float BrakeDeceleration = 9f;
        public const float CoastDeceleration = 1.4f;
        public const float MinTurnRadius = 2.4f;
        public const float MaxYawRate = 100f;
        public const float MaxLean = 30f;
        public const float LeanRate = 6f;

        /// <summary>Beyond this angle between the stick and the scooter's heading, the stick brakes instead of steering.</summary>
        public const float BrakeAngle = 115f;

        public const float Deadzone = 0.12f;

        public static ScooterState Step(ScooterState s, ScooterInput input, float dt)
        {
            float mag = Math.Min(1f, (float)Math.Sqrt(input.WorldX * input.WorldX + input.WorldZ * input.WorldZ));
            float target = 0f, rate = CoastDeceleration, diff = 0f;
            s.Braking = false;
            s.Throttle = 0f;
            if (mag > Deadzone)
            {
                float wanted = (float)(Math.Atan2(input.WorldX, input.WorldZ) * 180.0 / Math.PI);
                diff = Locomotion.DeltaAngle(s.Heading, wanted);
                if (Math.Abs(diff) <= BrakeAngle)
                {
                    // Ease off in a sharp turn, as a rider does.
                    float turnEase = 1f - 0.45f * Math.Min(1f, Math.Abs(diff) / 90f);
                    target = MaxSpeed * mag * turnEase;
                    rate = s.Speed < target ? Acceleration * (s.Speed < 0f ? 3f : 1f) : CoastDeceleration * 2f;
                    s.Throttle = mag;
                }
                else if (s.Speed > 0.25f)
                {
                    target = 0f;
                    rate = BrakeDeceleration * mag;
                    s.Braking = true;
                    diff = 0f;
                }
                else
                {
                    // Stopped and pulling back: roll back, straight.
                    target = -ReverseSpeed * mag;
                    rate = Acceleration * 0.5f;
                    diff = 0f;
                }
            }

            s.Speed = MoveTowards(s.Speed, target, rate * dt);

            // Steering: the tightest turn the speed allows.
            float speed = Math.Abs(s.Speed);
            float maxYaw = Math.Min(MaxYawRate, speed / MinTurnRadius * 180f / (float)Math.PI);
            float turn = Math.Max(-maxYaw * dt, Math.Min(maxYaw * dt, diff));
            if (s.Speed < 0f)
            {
                turn = 0f;
            }

            s.Heading = Wrap(s.Heading + turn);
            s.YawRate = dt > 0f ? turn / dt : 0f;

            // Lean: tan(lean) = v ω / g.
            float want = (float)(Math.Atan(Math.Max(0f, s.Speed) * s.YawRate * Math.PI / 180.0 / 9.81) * 180.0 / Math.PI);
            want = Math.Max(-MaxLean, Math.Min(MaxLean, want));
            s.Lean += (want - s.Lean) * Math.Min(1f, LeanRate * dt);
            return s;
        }

        /// <summary>A knock against a wall, a stall or a person: most of the speed goes, nothing breaks.</summary>
        public static ScooterState Bump(ScooterState s, float movedFraction)
        {
            if (movedFraction < 0.6f)
            {
                s.Speed *= Math.Max(0.1f, movedFraction) * 0.5f;
            }

            return s;
        }

        /// <summary>How far it travels before stopping from a speed with the brake fully on (metres).</summary>
        public static float StoppingDistance(float speed) => speed * speed / (2f * BrakeDeceleration);

        /// <summary>The scooter's speed in km/h for the speedometer.</summary>
        public static int Kmh(float speed) => (int)Math.Round(Math.Abs(speed) * 3.6f);

        private static float MoveTowards(float v, float target, float step) =>
            v < target ? Math.Min(target, v + step) : Math.Max(target, v - step);

        private static float Wrap(float a)
        {
            a %= 360f;
            return a < 0f ? a + 360f : a;
        }
    }

    /// <summary>The scooter's engine (a looping putter whose pitch follows the speed) and a two-tone horn, synthesised.</summary>
    public static class VehicleSounds
    {
        public const float EngineSeconds = 0.5f;
        public const float HornSeconds = 0.45f;

        /// <summary>A small single-cylinder putter at idle; loops without a click (whole cycles of every partial).</summary>
        public static float[] Engine(int sampleRate)
        {
            int n = (int)(sampleRate * EngineSeconds), edge = sampleRate / 100;
            var g = new float[n + edge]; // g[j] is the sample at time (j - edge): a little run-up before the loop's start
            var random = new Random(911);
            float noise = 0f;
            const float firing = 24f; // fires 24 times a second at idle: 12 whole cycles in the half second
            for (int j = 0; j < g.Length; j++)
            {
                float t = (j - edge) / (float)sampleRate;
                float phase = (float)(((t * firing) % 1.0 + 1.0) % 1.0);
                float pulse = (float)Math.Exp(-phase * 7f);
                noise += 0.25f * ((float)(random.NextDouble() * 2.0 - 1.0) - noise);
                float body = (float)Math.Sin(2 * Math.PI * firing * 2 * t) * 0.5f + (float)Math.Sin(2 * Math.PI * firing * 4 * t) * 0.25f;
                g[j] = 0.55f * pulse * (body + 0.6f * noise);
            }

            var data = new float[n];
            Array.Copy(g, edge, data, 0, n);

            // The tail fades into the run-up, which leads straight into the first sample: no seam when it loops.
            for (int i = 0; i < edge; i++)
            {
                float w = (i + 1) / (float)edge;
                data[n - edge + i] = g[n + i] * (1f - w) + g[i] * w;
            }

            return data;
        }

        /// <summary>A friendly two-tone beep (a scooter's horn, not a lorry's).</summary>
        public static float[] Horn(int sampleRate)
        {
            int n = (int)(sampleRate * HornSeconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float env = Math.Min(1f, t / 0.015f) * Math.Min(1f, (HornSeconds - t) / 0.05f);
                double a = Math.Sin(2 * Math.PI * 420 * t), b = Math.Sin(2 * Math.PI * 520 * t);
                float square = (float)(Math.Sign(a) * 0.3 + Math.Sign(b) * 0.3 + a * 0.2 + b * 0.2);
                data[i] = 0.32f * env * square;
            }

            return data;
        }
    }
}
