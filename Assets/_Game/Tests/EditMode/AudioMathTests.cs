using System;
using NUnit.Framework;
using UtsavNights.Core.Audio;

namespace UtsavNights.Tests
{
    public class VoiceAllocatorTests
    {
        [Test]
        public void UsesFreeVoicesFirst()
        {
            var a = new VoiceAllocator(3);
            Assert.AreEqual(0, a.Allocate(10));
            Assert.AreEqual(1, a.Allocate(10));
            Assert.AreEqual(2, a.Allocate(10));
            Assert.AreEqual(3, a.ActiveCount);
            Assert.AreEqual(0, a.Stolen);
        }

        [Test]
        public void WhenFull_StealsLowestPriority()
        {
            var a = new VoiceAllocator(3);
            a.Allocate(50);
            int low = a.Allocate(10);
            a.Allocate(50);
            Assert.AreEqual(low, a.Allocate(100));
            Assert.AreEqual(1, a.Stolen);
            Assert.AreEqual(100, a.PriorityOf(low));
        }

        [Test]
        public void EqualPriorities_StealOldest()
        {
            var a = new VoiceAllocator(3);
            int oldest = a.Allocate(10);
            a.Allocate(10);
            a.Allocate(10);
            Assert.AreEqual(oldest, a.Allocate(10));
            int nextOldest = 1;
            Assert.AreEqual(nextOldest, a.Allocate(10));
        }

        [Test]
        public void NeverStealsMoreImportantSound()
        {
            var a = new VoiceAllocator(2);
            a.Allocate(128);
            a.Allocate(128);
            Assert.AreEqual(-1, a.Allocate(32), "a fuse hiss cannot cut a bang");
            Assert.AreEqual(1, a.Rejected);
            Assert.AreEqual(2, a.ActiveCount);
        }

        [Test]
        public void Release_FreesVoice()
        {
            var a = new VoiceAllocator(1);
            int v = a.Allocate(10);
            a.Release(v);
            Assert.IsFalse(a.IsActive(v));
            Assert.AreEqual(v, a.Allocate(5));
            Assert.AreEqual(0, a.Stolen);
            a.ReleaseAll();
            Assert.AreEqual(0, a.ActiveCount);
            a.Release(-1);
            a.Release(99);
        }

        [Test]
        public void ZeroVoices_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceAllocator(0));
        }
    }

    public class DuckingEnvelopeTests
    {
        [Test]
        public void Duck_HoldThenLinearRelease()
        {
            var d = new DuckingEnvelope();
            Assert.AreEqual(1f, d.Gain);
            d.Duck(0.6f, 0.2f, 0.4f);
            Assert.AreEqual(0.4f, d.Gain, 1e-5f);
            d.Tick(0.2f);
            Assert.AreEqual(0.4f, d.Gain, 1e-5f, "still holding");
            d.Tick(0.2f);
            Assert.AreEqual(0.7f, d.Gain, 1e-5f, "half released");
            d.Tick(0.2f);
            Assert.AreEqual(1f, d.Gain, 1e-5f);
        }

        [Test]
        public void HoldAndReleaseInOneTick()
        {
            var d = new DuckingEnvelope();
            d.Duck(0.5f, 0.1f, 0.2f);
            d.Tick(0.2f);
            Assert.AreEqual(0.75f, d.Gain, 1e-5f);
        }

        [Test]
        public void DeeperDuck_Wins_ShallowerDoesNotRaiseGain()
        {
            var d = new DuckingEnvelope();
            d.Duck(0.3f, 0.5f, 0.5f);
            d.Duck(0.8f, 0.5f, 0.5f);
            Assert.AreEqual(0.2f, d.Gain, 1e-5f);
            d.Duck(0.1f, 0.5f, 0.5f);
            Assert.AreEqual(0.2f, d.Gain, 1e-5f);
        }

        [Test]
        public void NewDuckDuringRelease_RestartsFromCurrentDepth()
        {
            var d = new DuckingEnvelope();
            d.Duck(0.6f, 0f, 1f);
            d.Tick(0.5f);
            Assert.AreEqual(0.7f, d.Gain, 1e-5f);
            d.Duck(0.2f, 0f, 1f);
            Assert.AreEqual(0.7f, d.Gain, 1e-5f, "keeps the deeper current duck");
        }

        [Test]
        public void ZeroDepth_IsIgnored_AndValuesAreClamped()
        {
            var d = new DuckingEnvelope();
            d.Duck(0f, 1f, 1f);
            Assert.AreEqual(1f, d.Gain);
            d.Duck(2f, 0f, 1f);
            Assert.AreEqual(0f, d.Gain, 1e-5f);
            d.Reset();
            Assert.AreEqual(1f, d.Gain);
        }
    }
}
