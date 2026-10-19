using System;
using NUnit.Framework;
using UtsavNights.Core;

namespace UtsavNights.Tests
{
    public class RollingStatsTests
    {
        [Test]
        public void Empty_ReturnsZeros()
        {
            var stats = new RollingStats(4);
            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0f, stats.Average);
            Assert.AreEqual(0f, stats.Min);
            Assert.AreEqual(0f, stats.Max);
            Assert.AreEqual(0f, stats.Percentile(99f));
            Assert.AreEqual(0f, stats.PercentAbove(1f));
        }

        [Test]
        public void Average_MinMax_BeforeWrap()
        {
            var stats = new RollingStats(4);
            stats.Add(10f);
            stats.Add(20f);
            stats.Add(30f);
            Assert.AreEqual(3, stats.Count);
            Assert.AreEqual(20f, stats.Average, 1e-4f);
            Assert.AreEqual(10f, stats.Min);
            Assert.AreEqual(30f, stats.Max);
        }

        [Test]
        public void WrapAround_DropsOldestSamples()
        {
            var stats = new RollingStats(3);
            stats.Add(100f);
            stats.Add(1f);
            stats.Add(2f);
            stats.Add(3f);
            Assert.AreEqual(3, stats.Count);
            Assert.AreEqual(2f, stats.Average, 1e-4f);
            Assert.AreEqual(3f, stats.Max);
        }

        [Test]
        public void Percentile_UsesNearestRank()
        {
            var stats = new RollingStats(100);
            for (int i = 1; i <= 100; i++)
            {
                stats.Add(i);
            }

            Assert.AreEqual(50f, stats.Percentile(50f));
            Assert.AreEqual(95f, stats.Percentile(95f));
            Assert.AreEqual(99f, stats.Percentile(99f));
            Assert.AreEqual(100f, stats.Percentile(100f));
            Assert.AreEqual(1f, stats.Percentile(0f));
        }

        [Test]
        public void Percentile_DoesNotReorderSamples()
        {
            var stats = new RollingStats(3);
            stats.Add(3f);
            stats.Add(1f);
            stats.Add(2f);
            stats.Percentile(50f);
            stats.Add(10f);
            // Oldest sample (3) must be the one evicted, proving the ring order survived the sort.
            Assert.AreEqual((1f + 2f + 10f) / 3f, stats.Average, 1e-4f);
        }

        [Test]
        public void PercentAbove_CountsStrictlyGreater()
        {
            var stats = new RollingStats(4);
            stats.Add(10f);
            stats.Add(66.7f);
            stats.Add(70f);
            stats.Add(20f);
            Assert.AreEqual(2, stats.CountAbove(66.6f));
            Assert.AreEqual(50f, stats.PercentAbove(66.6f), 1e-4f);
        }

        [Test]
        public void Clear_ResetsState()
        {
            var stats = new RollingStats(2);
            stats.Add(5f);
            stats.Clear();
            Assert.AreEqual(0, stats.Count);
            stats.Add(7f);
            Assert.AreEqual(7f, stats.Average);
        }

        [Test]
        public void InvalidCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RollingStats(0));
        }
    }
}
