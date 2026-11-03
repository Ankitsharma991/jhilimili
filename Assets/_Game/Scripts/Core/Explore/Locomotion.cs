using System;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Core.Explore
{
    public enum Gait
    {
        Idle,
        Walk,
        Jog,
        Sprint,
        Air,
    }

    /// <summary>One frame's input: the stick already turned into a world direction (x right, z forward), its push 0 to 1, and the buttons.</summary>
    public readonly struct LocomotionInput
    {
        public LocomotionInput(float x, float z, bool sprint, bool jump)
        {
            X = x;
            Z = z;
            Sprint = sprint;
            Jump = jump;
        }

        public float X { get; }

        public float Z { get; }

        public bool Sprint { get; }

        /// <summary>Jump was pressed this frame.</summary>
        public bool Jump { get; }

        public float Push => Math.Min(1f, (float)Math.Sqrt(X * X + Z * Z));
    }

    /// <summary>The character's motion between frames. The caller moves the body by <see cref="Velocity"/> and reports <see cref="Grounded"/> back.</summary>
    public struct LocomotionState
    {
        public V3 Velocity;

        /// <summary>Degrees about the vertical; 0 faces +z.</summary>
        public float Facing;

        public bool Grounded;

        /// <summary>Seconds since the feet last touched the ground (coyote time).</summary>
        public float Airborne;

        /// <summary>Seconds left of a jump pressed just before landing (jump buffer).</summary>
        public float JumpBuffered;

        /// <summary>Seconds left of the dip after a hard landing.</summary>
        public float Landing;

        public bool Jumped;

        public float GroundSpeed => (float)Math.Sqrt(Velocity.X * Velocity.X + Velocity.Z * Velocity.Z);
    }

    /// <summary>
    /// How the player's character moves in Explore mode (Phase 12 step 1): speeds by how far the stick is pushed (walk,
    /// jog) and a sprint, quick acceleration and quicker stops, turning that slows when sprinting, a jump with coyote time
    /// and a jump buffer so it forgives a late or early press, little control in the air, and a short dip after a hard
    /// landing. Pure maths, the same on every phone; the scene applies it with a CharacterController.
    /// </summary>
    public static class Locomotion
    {
        public const float WalkSpeed = 1.7f;
        public const float JogSpeed = 3.8f;
        public const float SprintSpeed = 6.2f;
        public const float Deadzone = 0.12f;

        /// <summary>Below this push the character walks; at or above it, jogs.</summary>
        public const float JogPush = 0.6f;

        public const float Acceleration = 11f;
        public const float SprintAcceleration = 7f;
        public const float Deceleration = 20f;
        public const float AirControl = 0.3f;
        public const float TurnRate = 720f;
        public const float SprintTurnRate = 330f;
        public const float Gravity = 22f;
        public const float JumpHeight = 0.6f;
        public const float CoyoteTime = 0.12f;
        public const float JumpBufferTime = 0.15f;
        public const float HardLanding = 7f;
        public const float LandingSeconds = 0.18f;

        public static float JumpSpeed => (float)Math.Sqrt(2f * Gravity * JumpHeight);

        /// <summary>The speed the stick asks for.</summary>
        public static float TargetSpeed(float push, bool sprint)
        {
            if (push < Deadzone)
            {
                return 0f;
            }

            if (sprint)
            {
                return SprintSpeed;
            }

            float t = (push - Deadzone) / (1f - Deadzone);
            return push < JogPush ? WalkSpeed * Math.Max(0.5f, t / ((JogPush - Deadzone) / (1f - Deadzone))) : JogSpeed;
        }

        public static Gait GaitOf(LocomotionState s)
        {
            if (!s.Grounded && s.Airborne > CoyoteTime)
            {
                return Gait.Air;
            }

            float v = s.GroundSpeed;
            return v < 0.15f ? Gait.Idle : v < (WalkSpeed + JogSpeed) / 2f ? Gait.Walk : v < (JogSpeed + SprintSpeed) / 2f ? Gait.Jog : Gait.Sprint;
        }

        public static LocomotionState Step(LocomotionState s, LocomotionInput input, float dt)
        {
            s.Jumped = false;
            s.Airborne = s.Grounded ? 0f : s.Airborne + dt;
            s.JumpBuffered = input.Jump ? JumpBufferTime : Math.Max(0f, s.JumpBuffered - dt);
            s.Landing = Math.Max(0f, s.Landing - dt);

            // Horizontal: towards the asked-for velocity, quicker to stop than to start, little say in the air.
            float push = input.Push;
            float target = TargetSpeed(push, input.Sprint) * (s.Landing > 0f ? 0.5f : 1f);
            float dirX = push > 1e-4f ? input.X / Math.Max(push, 1e-4f) : 0f;
            float dirZ = push > 1e-4f ? input.Z / Math.Max(push, 1e-4f) : 0f;
            float wantX = dirX * target, wantZ = dirZ * target;
            bool slowing = target < s.GroundSpeed;
            float rate = slowing ? Deceleration : input.Sprint ? SprintAcceleration : Acceleration;
            if (!s.Grounded)
            {
                rate *= AirControl;
            }

            float dx = wantX - s.Velocity.X, dz = wantZ - s.Velocity.Z;
            float gap = (float)Math.Sqrt(dx * dx + dz * dz);
            float stepV = rate * dt;
            float k = gap <= stepV || gap < 1e-5f ? 1f : stepV / gap;
            float vx = s.Velocity.X + dx * k, vz = s.Velocity.Z + dz * k;

            // Facing: turn towards the stick, slower when sprinting; on the ground only.
            if (push >= Deadzone && s.Grounded)
            {
                float want = (float)(Math.Atan2(dirX, dirZ) * 180.0 / Math.PI);
                float turn = (input.Sprint ? SprintTurnRate : TurnRate) * dt;
                s.Facing = MoveTowardsAngle(s.Facing, want, turn);
            }

            // Vertical: gravity, the jump (coyote time and buffer), the landing.
            float vy = s.Velocity.Y;
            bool canJump = s.Grounded || s.Airborne <= CoyoteTime;
            if (s.JumpBuffered > 0f && canJump && vy <= 0.01f)
            {
                vy = JumpSpeed;
                s.JumpBuffered = 0f;
                s.Grounded = false;
                s.Airborne = CoyoteTime + 1e-3f; // no second jump from the same ground
                s.Jumped = true;
            }
            else if (s.Grounded)
            {
                vy = -2f; // kept pressed to the ground, down slopes and steps
            }
            else
            {
                vy -= Gravity * dt;
            }

            s.Velocity = new V3(vx, vy, vz);
            return s;
        }

        /// <summary>Called when the body touches down: a hard landing makes a short dip.</summary>
        public static LocomotionState Land(LocomotionState s, float fallSpeed)
        {
            s.Grounded = true;
            s.Airborne = 0f;
            if (fallSpeed >= HardLanding)
            {
                s.Landing = LandingSeconds;
            }

            return s;
        }

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = DeltaAngle(current, target);
            if (Math.Abs(delta) <= maxDelta)
            {
                return target;
            }

            return current + Math.Sign(delta) * maxDelta;
        }

        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f)
            {
                d -= 360f;
            }
            else if (d < -180f)
            {
                d += 360f;
            }

            return d;
        }
    }
}
