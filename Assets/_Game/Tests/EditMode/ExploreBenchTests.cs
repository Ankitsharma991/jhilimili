using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Diagnostics;

namespace UtsavNights.Tests
{
    /// <summary>Phase 12 step 8: the Explore bench's frame figures, its budgets and the results line.</summary>
    public class ExploreBenchTests
    {
        [Test]
        public void FrameFigures_FromFrameTimes()
        {
            // 100 frames: 94 at 25 ms, 5 at 40 ms and one 80 ms hitch.
            float[] ms = Enumerable.Repeat(25f, 94).Concat(Enumerable.Repeat(40f, 5)).Concat(new[] { 80f }).Reverse().ToArray();
            var leg = new BenchLeg { Name = "bazaar_walk" };
            ExploreBudget.FillFrames(leg, ms, ms.Length);
            Assert.AreEqual(100, leg.Frames);
            Assert.AreEqual(25f, leg.P50Ms);
            Assert.AreEqual(40f, leg.P95Ms);
            Assert.AreEqual(40f, leg.P99Ms);
            Assert.AreEqual(80f, leg.MaxMs);
            Assert.AreEqual((94 * 25f + 5 * 40f + 80f) / 100f, leg.AvgMs, 1e-3f);
            Assert.AreEqual(1f, leg.SpikePct, 1e-4f, "one frame over 66.7 ms");
            Assert.AreEqual(1000f / leg.AvgMs, leg.AvgFps, 1e-3f);
        }

        [Test]
        public void Budgets_ThirtyFps_FewSpikes_LittleGarbage_TierMemory()
        {
            var good = new BenchLeg { Name = "chowk_show", Frames = 900, AvgMs = 28f, SpikePct = 1f, GcKbAvg = 1.1f, MemoryMb = 850f };
            CollectionAssert.IsEmpty(ExploreBudget.Misses(good, 1));
            Assert.AreEqual(1, ExploreBudget.Misses(good, 0).Count, "850 MB is over the low tier's 700");
            Assert.AreEqual(66.67f, ExploreBudget.SpikeMs, 0.01f);

            var slow = new BenchLeg { Frames = 600, AvgMs = 40f, SpikePct = 6f, GcKbAvg = 5f, MemoryMb = 1300f };
            Assert.AreEqual(4, ExploreBudget.Misses(slow, 2).Count, "fps, spikes, garbage and memory");
            StringAssert.Contains("25.0 fps", ExploreBudget.Misses(slow, 2)[0]);

            var unknown = new BenchLeg { Frames = 600, AvgMs = 20f, SpikePct = 0f };
            CollectionAssert.IsEmpty(ExploreBudget.Misses(unknown, 2), "garbage and memory the player couldn't measure aren't misses");
            Assert.IsNotEmpty(ExploreBudget.Misses(new BenchLeg(), 2), "no frames is a miss");
            Assert.AreEqual(700f, ExploreBudget.MemoryMb(0));
            Assert.AreEqual(900f, ExploreBudget.MemoryMb(1));
            Assert.AreEqual(1200f, ExploreBudget.MemoryMb(2));
        }

        [Test]
        public void TheResultsLine_IsOneJsonObject()
        {
            var leg = new BenchLeg { Name = "bazaar_ride", Seconds = 12f, Frames = 400, AvgMs = 40f, P95Ms = 55.5f, SpikePct = 0f, Crackers = 3, People = 140, PeopleDrawn = 12, Vehicles = 5 };
            string line = ExploreBudget.ToJson(leg, "20261105-101010", "Xiaomi \"Pad\"", 1);
            StringAssert.StartsWith("{\"run\":\"20261105-101010\",\"device\":\"Xiaomi Pad\",\"tier\":1,\"leg\":\"bazaar_ride\"", line);
            StringAssert.Contains("\"avg_fps\":25", line);
            StringAssert.Contains("\"p95_ms\":55.5", line);
            StringAssert.Contains("\"people_drawn\":12", line);
            StringAssert.Contains("\"ok\":false", line);
            StringAssert.Contains("\"misses\":[\"25.0 fps, under 30\"]", line);
            StringAssert.EndsWith("]}", line);
            Assert.AreEqual(line.Count(c => c == '{'), line.Count(c => c == '}'));
        }
    }
}
