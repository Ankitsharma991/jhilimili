using System;
using NUnit.Framework;
using UtsavNights.Core;

namespace UtsavNights.Tests
{
    public class IgnitionHoldTrackerTests
    {
        [Test]
        public void FiresOnce_AfterRequiredHold()
        {
            var hold = new IgnitionHoldTracker(0.5f);
            Assert.IsFalse(hold.Tick(true, 0.2f));
            Assert.IsFalse(hold.Tick(true, 0.2f));
            Assert.IsTrue(hold.Tick(true, 0.2f));
            Assert.IsFalse(hold.Tick(true, 0.2f), "must not fire again during the same hold");
        }

        [Test]
        public void LeavingRange_ResetsProgress()
        {
            var hold = new IgnitionHoldTracker(0.5f);
            hold.Tick(true, 0.4f);
            Assert.IsFalse(hold.Tick(false, 0.016f));
            Assert.AreEqual(0f, hold.Progress);
            Assert.IsFalse(hold.Tick(true, 0.4f));
            Assert.IsTrue(hold.Tick(true, 0.1f));
        }

        [Test]
        public void ReArms_AfterLeavingAndReturning()
        {
            var hold = new IgnitionHoldTracker(0.5f);
            Assert.IsTrue(hold.Tick(true, 0.6f));
            hold.Tick(false, 0.016f);
            Assert.IsTrue(hold.Tick(true, 0.6f));
        }

        [Test]
        public void Progress_IsClampedToOne()
        {
            var hold = new IgnitionHoldTracker(0.5f);
            hold.Tick(true, 0.25f);
            Assert.AreEqual(0.5f, hold.Progress, 1e-4f);
            hold.Tick(true, 5f);
            Assert.AreEqual(1f, hold.Progress);
        }

        [Test]
        public void NegativeDelta_IsIgnored()
        {
            var hold = new IgnitionHoldTracker(0.5f);
            hold.Tick(true, -1f);
            Assert.AreEqual(0f, hold.HeldSeconds);
        }

        [Test]
        public void NonPositiveRequirement_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IgnitionHoldTracker(0f));
        }
    }
}
