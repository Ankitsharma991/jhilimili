using System;
using NUnit.Framework;
using UtsavNights.Core.Haptics;

namespace UtsavNights.Tests
{
    public class HapticWaveformTests
    {
        [Test]
        public void Patterns_HaveDistinctShapes()
        {
            HapticWaveform tick = HapticWaveforms.Tick(1f);
            HapticWaveform thud = HapticWaveforms.Thud(1f);
            HapticWaveform rumble = HapticWaveforms.Rumble(1f);
            Assert.Less(tick.TotalMs, 40, "tick is a short pulse");
            Assert.AreEqual(255, thud.PeakAmplitude, "thud is the strongest");
            Assert.Greater(rumble.TotalMs, 1000, "rumble is long");
            Assert.Less(rumble.PeakAmplitude, thud.PeakAmplitude);
        }

        [Test]
        public void Intensity_ScalesAmplitude_ButKeepsPulsesAudible()
        {
            Assert.AreEqual(128, HapticWaveforms.Thud(0.5f).PeakAmplitude, 1);
            HapticWaveform faint = HapticWaveforms.Tick(0.001f);
            Assert.AreEqual(1, faint.PeakAmplitude, "non-zero intensity never rounds a pulse to off");
            Assert.AreEqual(0, HapticWaveforms.Rumble(0f).PeakAmplitude);
            Assert.AreEqual(255, HapticWaveforms.Thud(5f).PeakAmplitude, "clamped");
        }

        [Test]
        public void OffSegments_StayOff()
        {
            HapticWaveform thud = HapticWaveforms.Thud(1f);
            Assert.AreEqual(0, thud.Amplitudes[1]);
        }

        [Test]
        public void OnOffPattern_StartsWithOffAndMergesRuns()
        {
            // Thud: on 45, off 25, on 70 -> off 0, on 45, off 25, on 70
            CollectionAssert.AreEqual(new long[] { 0, 45, 25, 70 }, HapticWaveforms.Thud(1f).OnOffTimingsMs);

            // Rumble is all on: one merged on segment.
            CollectionAssert.AreEqual(new long[] { 0, 1200 }, HapticWaveforms.Rumble(1f).OnOffTimingsMs);
        }

        [Test]
        public void MismatchedArrays_Throw()
        {
            Assert.Throws<ArgumentException>(() => new HapticWaveform(new long[] { 10 }, new[] { 1, 2 }));
            Assert.Throws<ArgumentException>(() => new HapticWaveform(new long[0], new int[0]));
        }
    }
}
