using UnityEngine;
using UtsavNights.Core.Effects;
using UtsavNights.Core.Services;

namespace UtsavNights.Effects
{
    /// <summary>
    /// A few short-lived point lights for bursts (PRD 13.3: no per-particle lights; a few lights only on
    /// active effects). Rate-limited to <see cref="FlashRateLimiter"/>, halved by reduce-flash, and off
    /// entirely when the tier disables lights.
    /// </summary>
    public sealed class FlashLightPool : ITickable
    {
        public const float ReduceFlashFactor = 0.5f;

        private readonly Flash[] _flashes;
        private readonly FlashRateLimiter _limiter;
        private double _clock;

        public FlashLightPool(Transform root, int lightCount, FlashRateLimiter limiter)
        {
            _limiter = limiter;
            _flashes = new Flash[Mathf.Max(1, lightCount)];
            for (int i = 0; i < _flashes.Length; i++)
            {
                var go = new GameObject("FlashLight" + i);
                go.transform.SetParent(root, false);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.intensity = 0f;
                light.enabled = false;
                _flashes[i].Light = light;
            }
        }

        public bool Enabled { get; set; } = true;

        public bool ReduceFlash { get; set; }

        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (Flash f in _flashes)
                {
                    if (f.Active)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        private struct Flash
        {
            public Light Light;
            public float Peak;
            public float Duration;
            public float Age;
            public bool Active;
        }

        /// <returns>False when lights are off for this tier or the rate cap was hit.</returns>
        public bool TryFlash(Vector3 position, Color color, float peak, float duration = 0.25f, float range = 12f)
        {
            if (!Enabled || peak <= 0f || duration <= 0f)
            {
                return false;
            }

            if (!_limiter.TryAcquire(_clock))
            {
                return false;
            }

            int index = 0;
            float oldest = -1f;
            for (int i = 0; i < _flashes.Length; i++)
            {
                if (!_flashes[i].Active)
                {
                    index = i;
                    break;
                }

                if (_flashes[i].Age > oldest)
                {
                    oldest = _flashes[i].Age;
                    index = i;
                }
            }

            ref Flash f = ref _flashes[index];
            f.Peak = peak;
            f.Duration = duration;
            f.Age = 0f;
            f.Active = true;
            f.Light.transform.position = position;
            f.Light.color = color;
            f.Light.range = range;
            f.Light.intensity = CurrentIntensity(f);
            f.Light.enabled = true;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            _clock += deltaSeconds;
            for (int i = 0; i < _flashes.Length; i++)
            {
                ref Flash f = ref _flashes[i];
                if (!f.Active)
                {
                    continue;
                }

                f.Age += deltaSeconds;
                if (f.Age >= f.Duration || !Enabled)
                {
                    f.Active = false;
                    f.Light.intensity = 0f;
                    f.Light.enabled = false;
                    continue;
                }

                f.Light.intensity = CurrentIntensity(f);
            }
        }

        /// <summary>Intensity of light <paramref name="index"/> right now (for tests and debugging).</summary>
        public float IntensityOf(int index) => _flashes[index].Light.intensity;

        private float CurrentIntensity(Flash f)
        {
            float t = 1f - Mathf.Clamp01(f.Age / f.Duration);
            float scale = ReduceFlash ? ReduceFlashFactor : 1f;
            return f.Peak * t * t * scale;
        }
    }
}
