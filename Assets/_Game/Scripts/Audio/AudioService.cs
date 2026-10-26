using UnityEngine;
using UtsavNights.Core.Audio;
using UtsavNights.Core.Services;

namespace UtsavNights.Audio
{
    /// <summary>Opaque reference to a playing sound; stale handles are ignored.</summary>
    public readonly struct VoiceHandle
    {
        internal VoiceHandle(int index, int version)
        {
            Index = index;
            Version = version;
        }

        internal int Index { get; }

        internal int Version { get; }

        public bool IsValid => Version > 0;

        public static VoiceHandle None => default;
    }

    /// <summary>Priority bands. Higher wins when voices run out; a sound never cuts a higher band.</summary>
    public static class SoundPriority
    {
        public const int Ambient = 0;
        public const int Fuse = 32;
        public const int Effect = 64;
        public const int Payoff = 96;
        public const int Bang = 128;
        public const int Interface = 160;

        /// <summary>Sounds at or above this band count as loud for Quiet mode.</summary>
        public const int LoudThreshold = Payoff;
    }

    /// <summary>
    /// Audio manager v1 (PRD 11, 13.1): AudioSource voice pool sized per tier, priority stealing,
    /// ducking of other voices under big sounds, fades, Quiet mode and pause handling.
    /// Ducking and levels are applied in code; an AudioMixer for the settings sliders comes in Phase 3.
    /// </summary>
    public sealed class AudioService : ITickable
    {
        /// <summary>Quiet mode scales loud sounds (bangs, payoffs) by this factor (PRD 11: lowers bang level).</summary>
        public const float QuietLoudFactor = 0.35f;

        public const int PreferredSampleRate = 48000;
        public const int PreferredDspBuffer = 256;

        private readonly AudioSource[] _sources;
        private readonly Voice[] _voices;
        private readonly VoiceAllocator _allocator;
        private readonly DuckingEnvelope _duck = new DuckingEnvelope();
        private int _nextVersion = 1;

        public AudioService(Transform root, int voiceCount)
        {
            _allocator = new VoiceAllocator(voiceCount);
            _sources = new AudioSource[voiceCount];
            _voices = new Voice[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(root, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sources[i] = source;
            }
        }

        public float MasterVolume { get; set; } = 1f;

        public float EffectsVolume { get; set; } = 1f;

        public bool QuietMode { get; set; }

        public bool IsPaused { get; private set; }

        public int VoiceCount => _sources.Length;

        public int ActiveVoices => _allocator.ActiveCount;

        public float DuckGain => _duck.Gain;

        public VoiceAllocator Allocator => _allocator;

        private struct Voice
        {
            public int Version;
            public float BaseVolume;
            public int Priority;
            public bool DucksOthers;
            public float FadeRemaining;
            public float FadeDuration;
        }

        /// <summary>
        /// Asks Unity for 48 kHz output with a 256-sample buffer. Some Android devices start the mixer at
        /// 24 kHz, doubling latency (found in Phase 1). Call once at boot, before any sound plays:
        /// AudioSettings.Reset stops all audio.
        /// </summary>
        public static bool EnsureLowLatencyOutput()
        {
            AudioConfiguration config = AudioSettings.GetConfiguration();
            if (config.sampleRate >= PreferredSampleRate && config.dspBufferSize <= PreferredDspBuffer && config.dspBufferSize > 0)
            {
                return false;
            }

            config.sampleRate = Mathf.Max(config.sampleRate, PreferredSampleRate);
            config.dspBufferSize = PreferredDspBuffer;
            return AudioSettings.Reset(config);
        }

        /// <summary>Final source volume for a voice. Public and pure for testing.</summary>
        public static float ComputeVolume(float baseVolume, float master, float effects, float duckGain, bool ducksOthers, bool quietAndLoud, float fade)
        {
            float v = baseVolume * master * effects * fade;
            if (!ducksOthers)
            {
                v *= duckGain;
            }

            if (quietAndLoud)
            {
                v *= QuietLoudFactor;
            }

            return Mathf.Clamp01(v);
        }

        /// <param name="duckOthers">0..1: how much this sound lowers every other voice (big bangs).</param>
        public VoiceHandle Play(AudioClip clip, float volume, int priority, bool loop = false,
            float duckOthers = 0f, float duckHoldSeconds = 0.25f, float duckReleaseSeconds = 0.6f)
        {
            if (clip == null)
            {
                return VoiceHandle.None;
            }

            int index = _allocator.Allocate(priority);
            if (index < 0)
            {
                return VoiceHandle.None;
            }

            int version = _nextVersion++;
            _voices[index] = new Voice
            {
                Version = version,
                BaseVolume = Mathf.Clamp01(volume),
                Priority = priority,
                DucksOthers = duckOthers > 0f,
            };

            if (duckOthers > 0f)
            {
                _duck.Duck(duckOthers, duckHoldSeconds, duckReleaseSeconds);
            }

            AudioSource source = _sources[index];
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.volume = VolumeFor(index);
            source.Play();
            return new VoiceHandle(index, version);
        }

        public bool IsPlaying(VoiceHandle handle) => Resolve(handle) >= 0;

        /// <summary>Stops a sound, optionally fading out (for fuse loops ending into the payoff).</summary>
        public bool Stop(VoiceHandle handle, float fadeSeconds = 0f)
        {
            int index = Resolve(handle);
            if (index < 0)
            {
                return false;
            }

            if (fadeSeconds > 0f)
            {
                _voices[index].FadeDuration = fadeSeconds;
                _voices[index].FadeRemaining = fadeSeconds;
            }
            else
            {
                Release(index);
            }

            return true;
        }

        public void StopAll()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_allocator.IsActive(i))
                {
                    Release(i);
                }
            }

            _duck.Reset();
        }

        /// <summary>Pauses all audio for calls, focus loss and backgrounding (PRD 11 interruptions).</summary>
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            AudioListener.pause = paused;
        }

        public void Tick(float deltaSeconds)
        {
            if (IsPaused)
            {
                return;
            }

            _duck.Tick(deltaSeconds);
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_allocator.IsActive(i))
                {
                    continue;
                }

                AudioSource source = _sources[i];
                if (source == null)
                {
                    _allocator.Release(i);
                    continue;
                }

                if (_voices[i].FadeDuration > 0f)
                {
                    _voices[i].FadeRemaining -= deltaSeconds;
                    if (_voices[i].FadeRemaining <= 0f)
                    {
                        Release(i);
                        continue;
                    }
                }

                if (!source.isPlaying)
                {
                    Release(i);
                    continue;
                }

                source.volume = VolumeFor(i);
            }
        }

        private float VolumeFor(int index)
        {
            Voice v = _voices[index];
            float fade = v.FadeDuration > 0f ? Mathf.Clamp01(v.FadeRemaining / v.FadeDuration) : 1f;
            bool quietLoud = QuietMode && v.Priority >= SoundPriority.LoudThreshold;
            return ComputeVolume(v.BaseVolume, MasterVolume, EffectsVolume, _duck.Gain, v.DucksOthers, quietLoud, fade);
        }

        private int Resolve(VoiceHandle handle)
        {
            if (!handle.IsValid || handle.Index >= _voices.Length)
            {
                return -1;
            }

            return _allocator.IsActive(handle.Index) && _voices[handle.Index].Version == handle.Version ? handle.Index : -1;
        }

        private void Release(int index)
        {
            if (_sources[index] != null)
            {
                _sources[index].Stop();
                _sources[index].clip = null;
            }

            _voices[index] = default;
            _allocator.Release(index);
        }
    }
}
