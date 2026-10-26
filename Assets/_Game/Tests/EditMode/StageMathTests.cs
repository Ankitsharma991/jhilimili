using NUnit.Framework;
using UtsavNights.Core.Crackers;

namespace UtsavNights.Tests
{
    public class StageMathTests
    {
        [Test]
        public void LinePattern_StepsOneWay()
        {
            Assert.AreEqual(0f, StageMath.OffsetMultiplier(0, RepeatPattern.Line));
            Assert.AreEqual(1f, StageMath.OffsetMultiplier(1, RepeatPattern.Line));
            Assert.AreEqual(4f, StageMath.OffsetMultiplier(4, RepeatPattern.Line));
        }

        [Test]
        public void FanPattern_Alternates()
        {
            float[] expected = { 0f, 1f, -1f, 2f, -2f, 3f };
            for (int k = 0; k < expected.Length; k++)
            {
                Assert.AreEqual(expected[k], StageMath.OffsetMultiplier(k, RepeatPattern.Fan), $"repeat {k}");
            }
        }

        [Test]
        public void RepeatTime_AddsIntervals()
        {
            Assert.AreEqual(2f, StageMath.RepeatTime(2f, 0, 0.5f));
            Assert.AreEqual(3.5f, StageMath.RepeatTime(2f, 3, 0.5f), 1e-5f);
            Assert.AreEqual(2f, StageMath.RepeatTime(2f, 3, -1f), "negative interval treated as 0");
        }

        [Test]
        public void LaunchHeight_EasesOutToApex()
        {
            Assert.AreEqual(0f, StageMath.LaunchHeight(0f, 1f, 10f));
            Assert.AreEqual(7.5f, StageMath.LaunchHeight(0.5f, 1f, 10f), 1e-4f, "fast off the pad");
            Assert.AreEqual(10f, StageMath.LaunchHeight(1f, 1f, 10f));
            Assert.AreEqual(10f, StageMath.LaunchHeight(5f, 1f, 10f), "clamped at the apex");
            Assert.AreEqual(10f, StageMath.LaunchHeight(0f, 0f, 10f), "instant launch");
            Assert.IsFalse(StageMath.LaunchFinished(0.9f, 1f));
            Assert.IsTrue(StageMath.LaunchFinished(1f, 1f));
        }

        [Test]
        public void TimelineCue_CarriesRepeat()
        {
            var cue = new TimelineCue(1f, CueKind.Normal, 2, 5);
            Assert.AreEqual(5, cue.Repeat);
            Assert.AreEqual(0, new TimelineCue(1f, CueKind.Normal, 2).Repeat);
        }

        [Test]
        public void StateMachine_RaisesCueWithRepeat()
        {
            var timeline = new CrackerTimeline(1f, 0f, new[]
            {
                new TimelineCue(0f, CueKind.Normal, 0, 0),
                new TimelineCue(1f, CueKind.Normal, 1, 0),
                new TimelineCue(1.5f, CueKind.Normal, 1, 1),
                new TimelineCue(3f, CueKind.Stop, 2, 0),
            });
            var sm = new CrackerStateMachine(timeline);
            var repeats = new System.Collections.Generic.List<int>();
            sm.Cue += c =>
            {
                if (c.StageIndex == 1)
                {
                    repeats.Add(c.Repeat);
                }
            };
            sm.Place();
            sm.Ignite();
            sm.Tick(5f);
            CollectionAssert.AreEqual(new[] { 0, 1 }, repeats);
        }
    }
}
