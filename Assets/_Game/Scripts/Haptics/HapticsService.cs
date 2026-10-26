using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Haptics;
using UtsavNights.Core.Services;

namespace UtsavNights.Haptics
{
    /// <summary>
    /// Haptics v1 (PRD 11): tick, thud and rumble, a master toggle and an intensity slider. Each pattern
    /// has a minimum gap so a chain of bangs does not become one continuous buzz.
    /// </summary>
    public sealed class HapticsService : ITickable
    {
        public const float DefaultIntensity = 0.8f;

        private readonly IHapticsBackend _backend;
        private readonly HapticWaveform[] _cache = new HapticWaveform[4];
        private readonly double[] _lastPlayed = { double.MinValue, double.MinValue, double.MinValue, double.MinValue };
        private float _intensity = DefaultIntensity;
        private double _clock;

        public HapticsService(IHapticsBackend backend)
        {
            _backend = backend ?? new NullHapticsBackend();
            RebuildCache();
        }

        public bool Enabled { get; set; } = true;

        public bool IsPaused { get; private set; }

        public IHapticsBackend Backend => _backend;

        public float Intensity
        {
            get => _intensity;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (!Mathf.Approximately(clamped, _intensity))
                {
                    _intensity = clamped;
                    RebuildCache();
                }
            }
        }

        /// <summary>Minimum seconds between two plays of the same pattern.</summary>
        public static float MinGapSeconds(HapticPattern pattern)
        {
            switch (pattern)
            {
                case HapticPattern.Tick: return 0.06f;
                case HapticPattern.Thud: return 0.08f;
                case HapticPattern.Rumble: return 0.6f;
                default: return 0f;
            }
        }

        /// <returns>True when a vibration was sent.</returns>
        public bool Play(HapticPattern pattern)
        {
            int index = (int)pattern;
            if (!Enabled || IsPaused || pattern == HapticPattern.None || index <= 0 || index >= _cache.Length || _intensity <= 0f)
            {
                return false;
            }

            if (_clock - _lastPlayed[index] < MinGapSeconds(pattern))
            {
                return false;
            }

            _lastPlayed[index] = _clock;
            _backend.Play(_cache[index]);
            return true;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            if (paused)
            {
                _backend.Cancel();
            }
        }

        public void Tick(float deltaSeconds) => _clock += deltaSeconds;

        private void RebuildCache()
        {
            _cache[(int)HapticPattern.Tick] = HapticWaveforms.Tick(_intensity);
            _cache[(int)HapticPattern.Thud] = HapticWaveforms.Thud(_intensity);
            _cache[(int)HapticPattern.Rumble] = HapticWaveforms.Rumble(_intensity);
        }
    }
}
