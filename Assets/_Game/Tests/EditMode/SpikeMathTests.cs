using NUnit.Framework;
using UtsavNights.Core;

namespace UtsavNights.Tests
{
    public class SpikeMathTests
    {
        [Test]
        public void DpToPx_UsesDpi()
        {
            Assert.AreEqual(64f, SpikeMath.DpToPx(64f, 160f), 1e-4f);
            Assert.AreEqual(128f, SpikeMath.DpToPx(64f, 320f), 1e-4f);
        }

        [Test]
        public void DpToPx_FallsBackWhenDpiUnknown()
        {
            Assert.AreEqual(64f * SpikeMath.FallbackDpi / 160f, SpikeMath.DpToPx(64f, 0f), 1e-3f);
        }

        [TestCase(256, 4, 48000, 21.333f)]
        [TestCase(512, 4, 48000, 42.667f)]
        [TestCase(1024, 2, 44100, 46.440f)]
        public void BufferLatency_IsLengthTimesCountOverRate(int length, int count, int rate, float expectedMs)
        {
            Assert.AreEqual(expectedMs, SpikeMath.BufferLatencyMs(length, count, rate), 0.01f);
        }

        [Test]
        public void BufferLatency_InvalidInputsReturnZero()
        {
            Assert.AreEqual(0f, SpikeMath.BufferLatencyMs(256, 4, 0));
            Assert.AreEqual(0f, SpikeMath.BufferLatencyMs(0, 4, 48000));
        }

        [Test]
        public void Fps_FromFrameTime()
        {
            Assert.AreEqual(60f, SpikeMath.FpsFromFrameMs(1000f / 60f), 1e-3f);
            Assert.AreEqual(0f, SpikeMath.FpsFromFrameMs(0f));
        }
    }
}
