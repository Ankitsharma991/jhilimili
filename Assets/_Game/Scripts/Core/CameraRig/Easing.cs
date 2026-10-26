using System;

namespace UtsavNights.Core.CameraRig
{
    public static class Easing
    {
        public static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;

        /// <summary>Smooth start and end; used for camera moves so nothing jerks.</summary>
        public static float InOutCubic(float t)
        {
            t = Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }

        public static float OutQuad(float t)
        {
            t = Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }

    /// <summary>
    /// Aerial camera weight over time (PRD 6.2: eases up over 2 s, holds, returns; cancelable by touch).
    /// 0 = ground shot, 1 = full aerial shot. Cancelling starts the return from wherever it is.
    /// </summary>
    public sealed class AerialMove
    {
        private enum Phase
        {
            Idle,
            Rising,
            Holding,
            Returning,
        }

        private Phase _phase = Phase.Idle;
        private float _elapsed;
        private float _rise;
        private float _hold;
        private float _return;
        private float _returnFrom;

        public float Weight { get; private set; }

        public bool IsActive => _phase != Phase.Idle;

        public bool IsReturning => _phase == Phase.Returning;

        public void Start(float riseSeconds, float holdSeconds, float returnSeconds)
        {
            _rise = Math.Max(0.01f, riseSeconds);
            _hold = Math.Max(0f, holdSeconds);
            _return = Math.Max(0.01f, returnSeconds);
            _elapsed = _phase == Phase.Idle ? 0f : _rise * Weight;
            _phase = Phase.Rising;
        }

        /// <summary>Return to the ground shot now (touch, or the next cracker being lit).</summary>
        public void Cancel()
        {
            if (_phase == Phase.Rising || _phase == Phase.Holding)
            {
                BeginReturn();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_phase == Phase.Idle || deltaSeconds <= 0f)
            {
                return;
            }

            _elapsed += deltaSeconds;
            switch (_phase)
            {
                case Phase.Rising:
                    Weight = Easing.InOutCubic(_elapsed / _rise);
                    if (_elapsed >= _rise)
                    {
                        float over = _elapsed - _rise;
                        _phase = Phase.Holding;
                        _elapsed = 0f;
                        Weight = 1f;
                        Tick(over);
                    }

                    break;
                case Phase.Holding:
                    if (_elapsed >= _hold)
                    {
                        float over = _elapsed - _hold;
                        BeginReturn();
                        Tick(over);
                    }

                    break;
                case Phase.Returning:
                    Weight = _returnFrom * (1f - Easing.InOutCubic(_elapsed / _return));
                    if (_elapsed >= _return)
                    {
                        Weight = 0f;
                        _phase = Phase.Idle;
                    }

                    break;
            }
        }

        private void BeginReturn()
        {
            _returnFrom = Weight;
            _elapsed = 0f;
            _phase = Phase.Returning;
        }
    }

    /// <summary>
    /// Small push-in toward the cracker at ignition or payoff (PRD 6.2), as a 0..1 weight that rises
    /// quickly and settles back.
    /// </summary>
    public sealed class PushIn
    {
        private float _elapsed;
        private float _duration;

        public float Weight { get; private set; }

        public bool IsActive => _duration > 0f && _elapsed < _duration;

        public void Start(float durationSeconds)
        {
            _duration = Math.Max(0.05f, durationSeconds);
            _elapsed = 0f;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsActive || deltaSeconds <= 0f)
            {
                return;
            }

            _elapsed += deltaSeconds;
            float t = Easing.Clamp01(_elapsed / _duration);

            // Up in the first 30 %, ease back over the rest.
            Weight = t < 0.3f ? Easing.OutQuad(t / 0.3f) : 1f - Easing.InOutCubic((t - 0.3f) / 0.7f);
            if (t >= 1f)
            {
                Weight = 0f;
            }
        }
    }

    /// <summary>
    /// Camera shake strength over time (PRD 6.2: only on large bangs, 100 to 150 ms, off in Quiet mode
    /// and with reduce-motion). The rig turns the strength into a noise offset.
    /// </summary>
    public sealed class ShakeEnvelope
    {
        public const float MinDuration = 0.1f;
        public const float MaxDuration = 0.15f;

        private float _elapsed;
        private float _duration;
        private float _peak;

        public bool Allowed { get; set; } = true;

        public float Strength { get; private set; }

        public bool Start(float peak, float durationSeconds)
        {
            if (!Allowed || peak <= 0f)
            {
                return false;
            }

            _peak = Math.Max(Strength, peak);
            _duration = Math.Max(MinDuration, Math.Min(MaxDuration, durationSeconds));
            _elapsed = 0f;
            Strength = _peak;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (Strength <= 0f || deltaSeconds <= 0f)
            {
                return;
            }

            if (!Allowed)
            {
                Stop();
                return;
            }

            _elapsed += deltaSeconds;
            float t = Easing.Clamp01(_elapsed / _duration);
            Strength = _peak * (1f - t) * (1f - t);
            if (t >= 1f)
            {
                Stop();
            }
        }

        public void Stop()
        {
            Strength = 0f;
            _peak = 0f;
            _elapsed = 0f;
        }
    }
}
