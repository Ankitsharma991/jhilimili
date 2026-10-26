using NUnit.Framework;
using UnityEngine;
using UtsavNights.Audio;

namespace UtsavNights.Tests
{
    public class AudioServiceTests
    {
        private GameObject _root;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("audio-root");
            _clip = AudioClip.Create("beep", 4800, 1, 48000, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_clip);
            AudioListener.pause = false;
        }

        [Test]
        public void CreatesOneSourcePerVoice()
        {
            var service = new AudioService(_root.transform, 16);
            Assert.AreEqual(16, service.VoiceCount);
            Assert.AreEqual(16, _root.GetComponentsInChildren<AudioSource>().Length);
        }

        [Test]
        public void Play_ReturnsHandles_AndRespectsPriority()
        {
            var service = new AudioService(_root.transform, 2);
            VoiceHandle fuse = service.Play(_clip, 1f, SoundPriority.Fuse);
            VoiceHandle bang = service.Play(_clip, 1f, SoundPriority.Bang);
            Assert.IsTrue(fuse.IsValid && bang.IsValid);

            VoiceHandle payoff = service.Play(_clip, 1f, SoundPriority.Payoff);
            Assert.IsTrue(payoff.IsValid, "steals the fuse voice");
            Assert.IsFalse(service.IsPlaying(fuse), "the stolen handle is now stale");
            Assert.IsTrue(service.IsPlaying(bang));

            VoiceHandle ambient = service.Play(_clip, 1f, SoundPriority.Ambient);
            Assert.IsFalse(ambient.IsValid, "cannot cut more important sounds");
        }

        [Test]
        public void Stop_ReleasesVoice_AndStaleHandlesAreIgnored()
        {
            var service = new AudioService(_root.transform, 2);
            VoiceHandle h = service.Play(_clip, 1f, SoundPriority.Effect);
            Assert.IsTrue(service.Stop(h));
            Assert.IsFalse(service.Stop(h));
            Assert.AreEqual(0, service.ActiveVoices);
            Assert.IsFalse(service.Play(null, 1f, SoundPriority.Effect).IsValid);
        }

        [Test]
        public void DuckingSound_LowersGain()
        {
            var service = new AudioService(_root.transform, 4);
            service.Play(_clip, 1f, SoundPriority.Bang, duckOthers: 0.5f);
            Assert.AreEqual(0.5f, service.DuckGain, 1e-5f);
            service.StopAll();
            Assert.AreEqual(1f, service.DuckGain);
            Assert.AreEqual(0, service.ActiveVoices);
        }

        [Test]
        public void ComputeVolume_AppliesEveryFactor()
        {
            Assert.AreEqual(0.4f, AudioService.ComputeVolume(0.8f, 1f, 0.5f, 1f, false, false, 1f), 1e-5f);
            Assert.AreEqual(0.4f, AudioService.ComputeVolume(0.8f, 1f, 1f, 0.5f, false, false, 1f), 1e-5f, "ducked");
            Assert.AreEqual(0.8f, AudioService.ComputeVolume(0.8f, 1f, 1f, 0.5f, true, false, 1f), 1e-5f, "the ducking sound itself is not ducked");
            Assert.AreEqual(0.8f * AudioService.QuietLoudFactor, AudioService.ComputeVolume(0.8f, 1f, 1f, 1f, false, true, 1f), 1e-5f, "quiet mode");
            Assert.AreEqual(0.2f, AudioService.ComputeVolume(0.8f, 1f, 1f, 1f, false, false, 0.25f), 1e-5f, "fading");
            Assert.AreEqual(1f, AudioService.ComputeVolume(3f, 1f, 1f, 1f, false, false, 1f), "clamped");
        }

        [Test]
        public void Pause_UsesListenerPause()
        {
            var service = new AudioService(_root.transform, 2);
            service.SetPaused(true);
            Assert.IsTrue(AudioListener.pause);
            Assert.IsTrue(service.IsPaused);
            service.SetPaused(false);
            Assert.IsFalse(AudioListener.pause);
        }
    }
}
