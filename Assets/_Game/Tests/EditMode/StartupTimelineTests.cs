using NUnit.Framework;
using UtsavNights.Core.Diagnostics;

namespace UtsavNights.Tests
{
    /// <summary>Phase 5 step 3: cold start is measured from process start, and frame spikes against the budget.</summary>
    public class StartupTimelineTests
    {
        [Test]
        public void Interactive_CountsTheTimeBeforeTheEngineToo()
        {
            var timeline = new StartupTimeline { PreEngineSeconds = 0.4f };
            Assert.AreEqual(-1f, timeline.InteractiveSeconds);
            timeline.Mark("first_scene", 0.05f);
            timeline.Mark("main_loaded", 0.8f);
            Assert.IsFalse(timeline.IsInteractive);
            timeline.Mark(StartupTimeline.Interactive, 0.85f);
            Assert.AreEqual(1.25f, timeline.InteractiveSeconds, 0.0001f);
        }

        [Test]
        public void Marks_KeepTheFirstTimeOnly()
        {
            var timeline = new StartupTimeline();
            Assert.IsTrue(timeline.Mark("consent", 1f));
            Assert.IsFalse(timeline.Mark("consent", 5f));
            Assert.IsTrue(timeline.TryGet("consent", out float t));
            Assert.AreEqual(1f, t);
            Assert.AreEqual(1, timeline.Marks.Count);
        }

        [Test]
        public void Describe_IsOneLineOfMillisecondsSinceProcessStart()
        {
            var timeline = new StartupTimeline { PreEngineSeconds = 0.42f };
            timeline.Mark("first_scene", 0.035f);
            timeline.Mark(StartupTimeline.Interactive, 0.85f);
            timeline.Mark("ads_sdk", 1.9f);
            Assert.AreEqual("pre_engine_ms=420 first_scene_ms=455 scene_ready_ms=1270 ads_sdk_ms=2320 interactive_ms=1270", timeline.Describe());
        }

        [TestCase(0, 6f)]
        [TestCase(1, 4f)]
        [TestCase(2, 3f)]
        public void Targets_FollowPrd14(int tier, float seconds)
        {
            Assert.AreEqual(seconds, StartupTimeline.TargetSeconds(tier));
        }

        [Test]
        public void SpikeShare_CountsFramesOverTwiceTheBudget()
        {
            var session = new PlaytestSession(0f);
            for (int i = 0; i < 95; i++)
            {
                session.AddFrame(16f, 0.016f);
            }

            for (int i = 0; i < 5; i++)
            {
                session.AddFrame(40f, 0.04f);
            }

            Assert.AreEqual(0.05f, session.WindowSpikeShare(1000f / 60f), 0.0001f, "40 ms is over 2 x 16.7 ms");
            Assert.AreEqual(0f, session.WindowSpikeShare(1000f / 30f), "but not over 2 x 33.3 ms");
            Assert.AreEqual(0f, new PlaytestSession(0f).WindowSpikeShare(16.7f));
        }
    }
}
