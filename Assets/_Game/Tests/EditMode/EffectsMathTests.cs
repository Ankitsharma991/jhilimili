using System;
using NUnit.Framework;
using UtsavNights.Core.Effects;

namespace UtsavNights.Tests
{
    public class FlashRateLimiterTests
    {
        [Test]
        public void AllowsThreePerSecond_ThenBlocks()
        {
            var limiter = new FlashRateLimiter(3, 1.0);
            Assert.IsTrue(limiter.TryAcquire(0.0));
            Assert.IsTrue(limiter.TryAcquire(0.1));
            Assert.IsTrue(limiter.TryAcquire(0.2));
            Assert.IsFalse(limiter.TryAcquire(0.3));
            Assert.AreEqual(3, limiter.CountInWindow(0.3));
        }

        [Test]
        public void Window_Rolls()
        {
            var limiter = new FlashRateLimiter(3, 1.0);
            limiter.TryAcquire(0.0);
            limiter.TryAcquire(0.5);
            limiter.TryAcquire(0.9);
            Assert.IsFalse(limiter.TryAcquire(0.99));
            Assert.IsTrue(limiter.TryAcquire(1.0), "the flash at 0.0 has left the window");
            Assert.IsFalse(limiter.TryAcquire(1.2));
            Assert.IsTrue(limiter.TryAcquire(1.5));
        }

        [Test]
        public void NeverMoreThanCapInAnyWindow_UnderSpam()
        {
            var limiter = new FlashRateLimiter(3, 1.0);
            var granted = new System.Collections.Generic.List<double>();
            for (int i = 0; i < 600; i++)
            {
                double t = i / 60.0;
                if (limiter.TryAcquire(t))
                {
                    granted.Add(t);
                }
            }

            for (int i = 3; i < granted.Count; i++)
            {
                Assert.GreaterOrEqual(granted[i] - granted[i - 3], 1.0 - 1e-9);
            }
        }

        [Test]
        public void Reset_ClearsHistory()
        {
            var limiter = new FlashRateLimiter(1, 1.0);
            limiter.TryAcquire(0.0);
            limiter.Reset();
            Assert.IsTrue(limiter.TryAcquire(0.1));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlashRateLimiter(0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlashRateLimiter(3, 0.0));
        }
    }

    public class ParticleBudgetTests
    {
        [Test]
        public void ReservesUntilCap_ThenShrinks_ThenSkips()
        {
            var budget = new ParticleBudget(1000);
            Assert.AreEqual(600, budget.Reserve(600, 16));
            Assert.AreEqual(400, budget.Reserve(600, 16), "gets what is left");
            Assert.AreEqual(0, budget.Reserve(600, 16), "nothing left");
            Assert.AreEqual(1000, budget.Reserved);
            Assert.AreEqual(0, budget.Remaining);
        }

        [Test]
        public void BelowMinimum_IsSkipped()
        {
            var budget = new ParticleBudget(100);
            budget.Reserve(90, 16);
            Assert.AreEqual(0, budget.Reserve(50, 16));
            Assert.AreEqual(90, budget.Reserved, "a skipped request reserves nothing");
        }

        [Test]
        public void SmallRequests_UseTheirOwnSizeAsMinimum()
        {
            var budget = new ParticleBudget(100);
            Assert.AreEqual(8, budget.Reserve(8, 16));
        }

        [Test]
        public void Release_ReturnsBudget_AndNeverGoesNegative()
        {
            var budget = new ParticleBudget(500);
            int a = budget.Reserve(300, 16);
            budget.Release(a);
            Assert.AreEqual(0, budget.Reserved);
            budget.Release(100);
            Assert.AreEqual(0, budget.Reserved);
        }

        [Test]
        public void LoweringCap_LeavesRunningEffects_AndLimitsNewOnes()
        {
            var budget = new ParticleBudget(3000);
            budget.Reserve(1000, 16);
            budget.Cap = 800;
            Assert.AreEqual(1000, budget.Reserved);
            Assert.AreEqual(0, budget.Remaining);
            Assert.AreEqual(0, budget.Reserve(100, 16));
        }

        [Test]
        public void NegativeCap_IsClampedToZero_AndZeroDesiredIsNoop()
        {
            var budget = new ParticleBudget(-5);
            Assert.AreEqual(0, budget.Cap);
            Assert.AreEqual(0, new ParticleBudget(100).Reserve(0, 16));
        }
    }
}
