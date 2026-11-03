using System;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Explore;

namespace UtsavNights.Tests
{
    /// <summary>Phase 12 step 1: how the character moves (the feel, in numbers) and how the follow camera behaves.</summary>
    public class LocomotionTests
    {
        private const float Dt = 1f / 60f;

        private static LocomotionState Run(LocomotionState s, LocomotionInput input, float seconds, bool grounded = true)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                s.Grounded = grounded;
                s = Locomotion.Step(s, input, Dt);
            }

            return s;
        }

        private static LocomotionState Standing => new LocomotionState { Grounded = true };

        [Test]
        public void Speeds_FollowTheStick_AndSprint()
        {
            Assert.AreEqual(0f, Locomotion.TargetSpeed(0.05f, false), "inside the dead zone");
            Assert.Less(Locomotion.TargetSpeed(0.3f, false), Locomotion.WalkSpeed + 1e-3f);
            Assert.AreEqual(Locomotion.JogSpeed, Locomotion.TargetSpeed(0.9f, false));
            Assert.AreEqual(Locomotion.SprintSpeed, Locomotion.TargetSpeed(0.9f, true));
            Assert.AreEqual(Gait.Jog, Locomotion.GaitOf(Run(Standing, new LocomotionInput(0f, 1f, false, false), 1f)));
            Assert.AreEqual(Gait.Sprint, Locomotion.GaitOf(Run(Standing, new LocomotionInput(0f, 1f, true, false), 1.5f)));
            Assert.AreEqual(Gait.Walk, Locomotion.GaitOf(Run(Standing, new LocomotionInput(0f, 0.4f, false, false), 1f)));
        }

        [Test]
        public void StartsQuickly_StopsQuicker()
        {
            LocomotionState s = Run(Standing, new LocomotionInput(0f, 1f, false, false), 0.25f);
            Assert.Greater(s.GroundSpeed, Locomotion.JogSpeed * 0.65f, "most of a jog within a quarter second");
            s = Run(s, new LocomotionInput(0f, 1f, false, false), 1f);
            float jog = s.GroundSpeed;
            s = Run(s, new LocomotionInput(0f, 0f, false, false), 0.25f);
            Assert.AreEqual(0f, s.GroundSpeed, 1e-3f, "stopped within a quarter second of letting go");
            Assert.Greater(Locomotion.Deceleration, Locomotion.Acceleration);
            Assert.AreEqual(Locomotion.JogSpeed, jog, 1e-3f);
        }

        [Test]
        public void Turning_IsFast_ButSlowerAtASprint()
        {
            LocomotionState s = Standing;
            s = Locomotion.Step(s, new LocomotionInput(1f, 0f, false, false), 0.1f);
            Assert.AreEqual(72f, s.Facing, 1e-3f, "720 degrees a second");
            LocomotionState sprinting = Standing;
            sprinting = Locomotion.Step(sprinting, new LocomotionInput(1f, 0f, true, false), 0.1f);
            Assert.Less(sprinting.Facing, s.Facing);
            Assert.AreEqual(-90f, Locomotion.DeltaAngle(0f, 270f));
            Assert.AreEqual(170f, Locomotion.MoveTowardsAngle(-170f, 170f, 30f), "the short way round (20 degrees, not 340)");
        }

        [Test]
        public void Jump_ReachesItsHeight_AndComesDown()
        {
            LocomotionState s = Standing;
            s = Locomotion.Step(s, new LocomotionInput(0f, 0f, false, true), Dt);
            Assert.IsTrue(s.Jumped);
            float y = 0f, top = 0f;
            for (int i = 0; i < 120 && (i == 0 || y > 0f); i++)
            {
                s.Grounded = false;
                y += s.Velocity.Y * Dt;
                top = Math.Max(top, y);
                s = Locomotion.Step(s, new LocomotionInput(0f, 0f, false, false), Dt);
            }

            Assert.AreEqual(Locomotion.JumpHeight, top, 0.06f);
            Assert.Less(y, 0.01f, "back down within two seconds");
        }

        [Test]
        public void CoyoteTime_AndJumpBuffer_Forgive()
        {
            // Walked off an edge 0.1 s ago: still a jump.
            var s = new LocomotionState { Grounded = false, Airborne = 0.08f };
            s = Locomotion.Step(s, new LocomotionInput(0f, 0f, false, true), Dt);
            Assert.IsTrue(s.Jumped, "inside coyote time");

            // 0.25 s off the edge: no jump.
            s = new LocomotionState { Grounded = false, Airborne = 0.25f, Velocity = new V3(0f, -3f, 0f) };
            s = Locomotion.Step(s, new LocomotionInput(0f, 0f, false, true), Dt);
            Assert.IsFalse(s.Jumped);

            // Pressed 0.1 s before landing: jumps on landing.
            s = new LocomotionState { Grounded = false, Airborne = 0.5f, Velocity = new V3(0f, -3f, 0f) };
            s = Locomotion.Step(s, new LocomotionInput(0f, 0f, false, true), Dt);
            s = Run(s, new LocomotionInput(0f, 0f, false, false), 0.08f, grounded: false);
            s = Locomotion.Land(s, 3f);
            s = Locomotion.Step(s, new LocomotionInput(0f, 0f, false, false), Dt);
            Assert.IsTrue(s.Jumped, "the buffered press");

            // No second jump in mid-air.
            s.Grounded = false;
            s = Locomotion.Step(s, new LocomotionInput(0f, 0f, false, true), Dt);
            Assert.IsFalse(s.Jumped);
        }

        [Test]
        public void InTheAir_LittleControl_AndHardLandingsDip()
        {
            LocomotionState ground = Run(Standing, new LocomotionInput(1f, 0f, false, false), 0.2f);
            LocomotionState air = Run(new LocomotionState { Grounded = false, Airborne = 1f }, new LocomotionInput(1f, 0f, false, false), 0.2f, grounded: false);
            Assert.Less(air.GroundSpeed, ground.GroundSpeed * 0.5f);
            LocomotionState landed = Locomotion.Land(new LocomotionState(), 9f);
            Assert.Greater(landed.Landing, 0f);
            Assert.AreEqual(0f, Locomotion.Land(new LocomotionState(), 3f).Landing, "a hop doesn't dip");
            LocomotionState slowed = Run(landed, new LocomotionInput(0f, 1f, false, false), Locomotion.LandingSeconds * 0.9f);
            Assert.LessOrEqual(slowed.GroundSpeed, Locomotion.JogSpeed * 0.5f + 1e-3f, "half speed while recovering from the landing");
            Assert.AreEqual(Gait.Air, Locomotion.GaitOf(air));
            Assert.AreNotEqual(Gait.Air, Locomotion.GaitOf(slowed));
        }

        [Test]
        public void Camera_LimitsRecentresAndPullsInAtWalls()
        {
            var cam = new OrbitCamera();
            cam.Look(0f, -500f);
            Assert.AreEqual(OrbitCamera.MaxPitch, cam.Pitch, "can't flip over the top");
            cam.Look(0f, 500f);
            Assert.AreEqual(OrbitCamera.MinPitch, cam.Pitch);
            cam.Zoom(100f);
            Assert.AreEqual(OrbitCamera.MaxDistance, cam.Distance);

            cam = new OrbitCamera { Yaw = 0f };
            cam.Look(40f, 0f);
            cam.Follow(120f, moving: true, 0.5f);
            Assert.AreEqual(40f, cam.Yaw, 1e-3f, "no recentring right after the player turned it");
            for (int i = 0; i < 200; i++)
            {
                cam.Follow(120f, moving: true, 0.02f);
            }

            Assert.AreEqual(120f, cam.Yaw, 1e-3f, "eases behind the moving character");
            float yawBefore = cam.Yaw;
            cam.Follow(10f, moving: false, 2f);
            Assert.AreEqual(yawBefore, cam.Yaw, "standing still, the camera stays where the player left it");

            Assert.AreEqual(cam.Distance, cam.Clear(null));
            Assert.AreEqual(1.0f - OrbitCamera.WallPadding, cam.Clear(1.0f), 1e-4f);
            Assert.AreEqual(OrbitCamera.NearestPull, cam.Clear(0.1f), "never inside the head");

            cam = new OrbitCamera { Yaw = 0f, Pitch = 0f, Distance = 3f };
            V3 p = cam.Position(new V3(0f, 0f, 0f), 3f);
            Assert.AreEqual(-3f, p.Z, 1e-3f, "behind the character");
            Assert.AreEqual(OrbitCamera.Shoulder, p.X, 1e-3f, "over the right shoulder");
            Assert.AreEqual(OrbitCamera.PivotHeight, p.Y, 1e-3f);

            float v = 0f, x = 0f;
            for (int i = 0; i < 120; i++)
            {
                x = OrbitCamera.Smooth(x, 1f, ref v, 0.3f, Dt);
                Assert.LessOrEqual(x, 1f, "no overshoot");
            }

            Assert.AreEqual(1f, x, 0.01f);
        }

        [Test]
        public void Footsteps_AreShort_AndVary()
        {
            float[] a = Footsteps.Generate(44100, 0), b = Footsteps.Generate(44100, 1);
            Assert.AreEqual((int)(44100 * Footsteps.Seconds), a.Length);
            Assert.AreNotEqual(a[200], b[200]);
            float peak = 0f;
            foreach (float s in a)
            {
                peak = Math.Max(peak, Math.Abs(s));
            }

            Assert.LessOrEqual(peak, 0.71f);
        }
    }
}
