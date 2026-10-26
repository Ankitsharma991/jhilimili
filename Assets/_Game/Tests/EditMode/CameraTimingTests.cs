using NUnit.Framework;
using UtsavNights.Core.CameraRig;

namespace UtsavNights.Tests
{
    public class CameraTimingTests
    {
        private static void Run(System.Action<float> tick, float seconds, float step = 1f / 60f)
        {
            for (float t = 0f; t < seconds - 1e-6f; t += step)
            {
                tick(step);
            }
        }

        [Test]
        public void Easing_EndpointsAndMidpoint()
        {
            Assert.AreEqual(0f, Easing.InOutCubic(0f));
            Assert.AreEqual(1f, Easing.InOutCubic(1f));
            Assert.AreEqual(0.5f, Easing.InOutCubic(0.5f), 1e-5f);
            Assert.AreEqual(1f, Easing.InOutCubic(5f), "clamped");
            Assert.AreEqual(0.75f, Easing.OutQuad(0.5f), 1e-5f);
        }

        [Test]
        public void Aerial_RisesOverTwoSeconds_HoldsThenReturns()
        {
            var aerial = new AerialMove();
            aerial.Start(2f, 1f, 1f);
            Run(aerial.Tick, 1f);
            Assert.AreEqual(0.5f, aerial.Weight, 0.03f, "half way up at 1 s");
            Run(aerial.Tick, 1.5f);
            Assert.AreEqual(1f, aerial.Weight, 1e-5f, "holding");
            Run(aerial.Tick, 1f);
            Assert.IsTrue(aerial.IsReturning);
            Run(aerial.Tick, 1f);
            Assert.AreEqual(0f, aerial.Weight);
            Assert.IsFalse(aerial.IsActive);
        }

        [Test]
        public void Aerial_CancelReturnsFromCurrentWeight()
        {
            var aerial = new AerialMove();
            aerial.Start(2f, 1f, 1f);
            Run(aerial.Tick, 1f);
            float before = aerial.Weight;
            aerial.Cancel();
            Assert.IsTrue(aerial.IsReturning);
            aerial.Tick(1f / 60f);
            Assert.LessOrEqual(aerial.Weight, before, "no jump upward on cancel");
            Run(aerial.Tick, 1.1f);
            Assert.AreEqual(0f, aerial.Weight);
        }

        [Test]
        public void Aerial_OneLargeTick_SettlesCorrectly()
        {
            var aerial = new AerialMove();
            aerial.Start(2f, 1f, 1f);
            aerial.Tick(10f);
            Assert.AreEqual(0f, aerial.Weight);
            Assert.IsFalse(aerial.IsActive);
        }

        [Test]
        public void PushIn_RisesAndSettles()
        {
            var push = new PushIn();
            push.Start(1f);
            Run(push.Tick, 0.3f);
            Assert.Greater(push.Weight, 0.9f);
            Run(push.Tick, 0.75f);
            Assert.AreEqual(0f, push.Weight);
            Assert.IsFalse(push.IsActive);
        }

        [Test]
        public void Shake_IsShortAndDecays()
        {
            var shake = new ShakeEnvelope();
            Assert.IsTrue(shake.Start(1f, 0.13f));
            Assert.AreEqual(1f, shake.Strength);
            shake.Tick(0.065f);
            Assert.Less(shake.Strength, 0.5f);
            shake.Tick(0.1f);
            Assert.AreEqual(0f, shake.Strength);
        }

        [Test]
        public void Shake_DurationClampedTo100To150Ms()
        {
            var shake = new ShakeEnvelope();
            shake.Start(1f, 5f);
            shake.Tick(ShakeEnvelope.MaxDuration + 0.001f);
            Assert.AreEqual(0f, shake.Strength, "long requests are clamped to 150 ms");
            shake.Start(1f, 0.01f);
            shake.Tick(0.05f);
            Assert.Greater(shake.Strength, 0f, "short requests last at least 100 ms");
        }

        [Test]
        public void Shake_DisallowedByAccessibility()
        {
            var shake = new ShakeEnvelope { Allowed = false };
            Assert.IsFalse(shake.Start(1f, 0.13f));
            Assert.AreEqual(0f, shake.Strength);
            shake.Allowed = true;
            shake.Start(1f, 0.13f);
            shake.Allowed = false;
            shake.Tick(0.01f);
            Assert.AreEqual(0f, shake.Strength, "turning it off mid-shake stops it");
        }
    }
}
