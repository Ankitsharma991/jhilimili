using NUnit.Framework;
using UtsavNights.Core.Diagnostics;

namespace UtsavNights.Tests
{
    public class PlaytestSessionTests
    {
        [Test]
        public void TimeToFirstIgnition_IsMeasuredFromSceneReady()
        {
            var s = new PlaytestSession(startTime: 1f);
            s.MarkSceneReady(3f);
            s.MarkSceneReady(9f);
            Assert.AreEqual(3f, s.SceneReadyTime, "first ready time wins");
            Assert.AreEqual(-1f, s.TimeToFirstIgnition);
            Assert.IsTrue(s.RecordIgnition(10.5f));
            Assert.IsFalse(s.RecordIgnition(20f));
            Assert.AreEqual(7.5f, s.TimeToFirstIgnition, 1e-4f);
            Assert.AreEqual(2, s.Ignitions);
        }

        [Test]
        public void IgnitionBeforeReady_UsesStartTime()
        {
            var s = new PlaytestSession(startTime: 2f);
            s.RecordIgnition(5f);
            Assert.AreEqual(3f, s.TimeToFirstIgnition, 1e-4f);
        }

        [Test]
        public void Counters()
        {
            var s = new PlaytestSession(0f);
            s.RecordPayoff();
            s.RecordFlameOut();
            s.RecordFlameOut();
            s.RecordQualityChange();
            Assert.AreEqual(1, s.Payoffs);
            Assert.AreEqual(2, s.FlameOuts);
            Assert.AreEqual(1, s.QualityChanges);
            Assert.AreEqual(12f, s.Duration(12f));
        }

        [Test]
        public void PerfSample_DueEveryInterval_WithWindowStats()
        {
            var s = new PlaytestSession(0f, sampleIntervalSeconds: 1f);
            bool due = false;
            for (int i = 0; i < 59; i++)
            {
                due |= s.AddFrame(1000f / 60f, 1f / 60f);
            }

            Assert.IsFalse(due);
            Assert.IsTrue(s.AddFrame(1000f / 60f, 1f / 60f + 0.001f));
            Assert.AreEqual(60f, s.WindowAverageFps, 0.1f);
            Assert.AreEqual(60, s.WindowFrames);
            s.ClearWindow();
            Assert.AreEqual(0, s.WindowFrames);
        }

        [Test]
        public void HugeFrames_AreNotCountedInPerfWindow()
        {
            var s = new PlaytestSession(0f, 10f);
            s.AddFrame(5000f, 0.1f);
            s.AddFrame(16f, 0.1f);
            Assert.AreEqual(1, s.WindowFrames);
        }
    }
}
