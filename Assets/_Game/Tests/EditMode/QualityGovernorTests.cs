using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Quality;

namespace UtsavNights.Tests
{
    public class TierDetectionTests
    {
        [TestCase(3700, 8, TierDetection.Low)]
        [TestCase(4400, 8, TierDetection.Low)]
        [TestCase(5600, 8, TierDetection.Mid)]
        [TestCase(7400, 8, TierDetection.High)]
        [TestCase(7400, 6, TierDetection.Mid)]
        [TestCase(11000, 8, TierDetection.High)]
        [TestCase(0, 8, TierDetection.Mid)]
        public void FromHardware(int ramMb, int cores, int expected)
        {
            Assert.AreEqual(expected, TierDetection.FromHardware(ramMb, cores));
        }

        [Test]
        public void Benchmark_OnlyEverLowersTier()
        {
            float high = TierDetection.TargetFrameMs(TierDetection.High);
            Assert.AreEqual(TierDetection.High, TierDetection.AdjustForBenchmark(TierDetection.High, high));
            Assert.AreEqual(TierDetection.Mid, TierDetection.AdjustForBenchmark(TierDetection.High, high * 1.3f));
            Assert.AreEqual(TierDetection.Low, TierDetection.AdjustForBenchmark(TierDetection.High, high * 2f));
            Assert.AreEqual(TierDetection.Low, TierDetection.AdjustForBenchmark(TierDetection.Low, 500f));
            Assert.AreEqual(TierDetection.Mid, TierDetection.AdjustForBenchmark(TierDetection.Mid, 1f), "fast never raises");
            Assert.AreEqual(TierDetection.Mid, TierDetection.AdjustForBenchmark(TierDetection.Mid, 0f), "no data keeps tier");
        }

        [Test]
        public void Targets_Are30_45_60Fps()
        {
            Assert.AreEqual(33.33f, TierDetection.TargetFrameMs(TierDetection.Low), 0.01f);
            Assert.AreEqual(22.22f, TierDetection.TargetFrameMs(TierDetection.Mid), 0.01f);
            Assert.AreEqual(16.67f, TierDetection.TargetFrameMs(TierDetection.High), 0.01f);
        }
    }

    public class QualityGovernorTests
    {
        private const float Dt = 1f / 30f;

        private static void Feed(QualityGovernor g, float frameMs, float seconds)
        {
            for (float t = 0f; t < seconds - 1e-6f; t += Dt)
            {
                g.Sample(frameMs, Dt);
            }
        }

        [Test]
        public void SustainedOverBudget_StepsDownInPrdOrder()
        {
            var g = new QualityGovernor(16.7f);
            var steps = new List<DegradeStep>();
            g.SteppedDown += s => steps.Add(s);
            Feed(g, 40f, 2.6f);
            CollectionAssert.AreEqual(new[] { DegradeStep.ReduceParticles }, steps);
            Feed(g, 40f, 2.1f);
            Feed(g, 40f, 2.1f);
            CollectionAssert.AreEqual(new[] { DegradeStep.ReduceParticles, DegradeStep.DisableBloom, DegradeStep.ReduceResolution }, steps);
            Feed(g, 40f, 10f);
            Assert.AreEqual(3, steps.Count, "nothing beyond resolution");
        }

        [Test]
        public void ShortSpikes_DoNotStepDown()
        {
            var g = new QualityGovernor(16.7f);
            int downs = 0;
            g.SteppedDown += _ => downs++;
            for (int i = 0; i < 20; i++)
            {
                Feed(g, 40f, 0.5f);
                Feed(g, 14f, 1.5f);
            }

            Assert.AreEqual(0, downs);
        }

        [Test]
        public void Hitches_AreIgnored()
        {
            var g = new QualityGovernor(16.7f);
            int downs = 0;
            g.SteppedDown += _ => downs++;
            Feed(g, 900f, 5f);
            Assert.AreEqual(0, downs);
        }

        [Test]
        public void StepsUpAtMostOncePerSession()
        {
            var g = new QualityGovernor(16.7f);
            var ups = new List<DegradeStep>();
            g.SteppedUp += s => ups.Add(s);
            Feed(g, 40f, 2.6f);
            Feed(g, 40f, 2.1f);
            Assert.AreEqual(2, g.StepsApplied);
            Feed(g, 8f, QualityGovernor.StepUpAfterSeconds + 3f); // about 1 s for the smoothed time to drop below headroom
            CollectionAssert.AreEqual(new[] { DegradeStep.DisableBloom }, ups);
            Assert.AreEqual(1, g.StepsApplied);
            Feed(g, 8f, 60f);
            Assert.AreEqual(1, ups.Count, "never a second step up");
            Assert.IsTrue(g.SteppedUpThisSession);
        }

        [Test]
        public void NoStepUpWithoutClearHeadroom()
        {
            var g = new QualityGovernor(16.7f);
            int ups = 0;
            g.SteppedUp += _ => ups++;
            Feed(g, 40f, 2.6f);
            Feed(g, 15f, 60f);
            Assert.AreEqual(0, ups, "15 ms is under budget but not 30 % under");
        }

        [Test]
        public void Disabled_DoesNothing()
        {
            var g = new QualityGovernor(16.7f) { Enabled = false };
            Feed(g, 40f, 10f);
            Assert.AreEqual(0, g.StepsApplied);
        }

        [Test]
        public void InvalidTarget_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new QualityGovernor(0f));
        }
    }
}
