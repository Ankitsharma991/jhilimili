using System;

namespace UtsavNights.Core.Audio
{
    /// <summary>
    /// Gain applied to everything else while a big sound plays (PRD 11: the loudest bang lowers others).
    /// Instant attack, hold, then linear release. Overlapping ducks keep the deepest one.
    /// </summary>
    public sealed class DuckingEnvelope
    {
        private float _depth;
        private float _hold;
        private float _release;
        private float _releaseElapsed;

        /// <summary>Current multiplier for non-ducking voices, 1 = no ducking.</summary>
        public float Gain { get; private set; } = 1f;

        /// <param name="depth">0..1, how much to reduce (0.5 = half volume).</param>
        public void Duck(float depth, float holdSeconds, float releaseSeconds)
        {
            depth = Math.Max(0f, Math.Min(1f, depth));
            if (depth <= 0f)
            {
                return;
            }

            // Deepest of the current (possibly releasing) duck and the new one; restart the hold.
            _depth = Math.Max(1f - Gain, depth);
            _hold = Math.Max(_hold, Math.Max(0f, holdSeconds));
            _release = Math.Max(0.01f, releaseSeconds);
            _releaseElapsed = 0f;
            Gain = 1f - _depth;
        }

        public void Tick(float deltaSeconds)
        {
            if (Gain >= 1f || deltaSeconds <= 0f)
            {
                return;
            }

            if (_hold > 0f)
            {
                float used = Math.Min(_hold, deltaSeconds);
                _hold -= used;
                deltaSeconds -= used;
                if (deltaSeconds <= 0f)
                {
                    return;
                }
            }

            _releaseElapsed += deltaSeconds;
            float t = Math.Min(1f, _releaseElapsed / _release);
            Gain = 1f - _depth * (1f - t);
            if (t >= 1f)
            {
                Reset();
            }
        }

        public void Reset()
        {
            Gain = 1f;
            _depth = 0f;
            _hold = 0f;
            _releaseElapsed = 0f;
        }
    }
}
