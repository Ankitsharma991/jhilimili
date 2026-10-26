using NUnit.Framework;
using UtsavNights.Content;
using UtsavNights.Haptics;

namespace UtsavNights.Tests
{
    public class HapticsServiceTests
    {
        private NullHapticsBackend _backend;
        private HapticsService _service;

        [SetUp]
        public void SetUp()
        {
            _backend = new NullHapticsBackend();
            _service = new HapticsService(_backend);
        }

        [Test]
        public void Play_SendsPatternToBackend()
        {
            Assert.IsTrue(_service.Play(HapticPattern.Thud));
            Assert.AreEqual(1, _backend.PlayCount);
            Assert.AreEqual(204, _backend.LastPlayed.PeakAmplitude, 1, "default intensity 0.8 of 255");
        }

        [Test]
        public void Disabled_OrZeroIntensity_OrNone_DoNothing()
        {
            _service.Enabled = false;
            Assert.IsFalse(_service.Play(HapticPattern.Tick));
            _service.Enabled = true;
            _service.Intensity = 0f;
            Assert.IsFalse(_service.Play(HapticPattern.Tick));
            _service.Intensity = 1f;
            Assert.IsFalse(_service.Play(HapticPattern.None));
            Assert.AreEqual(0, _backend.PlayCount);
        }

        [Test]
        public void SamePattern_IsRateLimited_OthersAreNot()
        {
            Assert.IsTrue(_service.Play(HapticPattern.Thud));
            Assert.IsFalse(_service.Play(HapticPattern.Thud), "within the minimum gap");
            Assert.IsTrue(_service.Play(HapticPattern.Tick), "a different pattern is independent");
            _service.Tick(HapticsService.MinGapSeconds(HapticPattern.Thud));
            Assert.IsTrue(_service.Play(HapticPattern.Thud));
        }

        [Test]
        public void Pause_BlocksPlayback()
        {
            _service.SetPaused(true);
            Assert.IsFalse(_service.Play(HapticPattern.Rumble));
            _service.SetPaused(false);
            Assert.IsTrue(_service.Play(HapticPattern.Rumble));
        }

        [Test]
        public void IntensityChange_RebuildsWaveforms()
        {
            _service.Intensity = 1f;
            _service.Play(HapticPattern.Thud);
            Assert.AreEqual(255, _backend.LastPlayed.PeakAmplitude);
            Assert.AreEqual(1f, _service.Intensity);
            _service.Intensity = 3f;
            Assert.AreEqual(1f, _service.Intensity, "clamped");
        }

        [Test]
        public void NullBackend_FallsBackSafely()
        {
            var service = new HapticsService(null);
            Assert.DoesNotThrow(() => service.Play(HapticPattern.Tick));
            Assert.IsFalse(service.Backend.HasVibrator);
        }
    }
}
