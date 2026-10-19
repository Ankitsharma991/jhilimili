using System;

namespace UtsavNights.Core
{
    /// <summary>
    /// Tracks how long the lighter flame has stayed near a fuse. Fires once when the hold
    /// reaches the required time (PRD 6.1: ignition after 0.5 s proximity hold) and re-arms
    /// only after the flame leaves the fuse.
    /// </summary>
    public sealed class IgnitionHoldTracker
    {
        private readonly float _requiredSeconds;
        private float _heldSeconds;
        private bool _fired;

        public IgnitionHoldTracker(float requiredSeconds)
        {
            if (requiredSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredSeconds), "Hold time must be positive.");
            }

            _requiredSeconds = requiredSeconds;
        }

        public float RequiredSeconds => _requiredSeconds;

        public float HeldSeconds => _heldSeconds;

        /// <summary>0..1 progress towards ignition, for UI feedback.</summary>
        public float Progress => Math.Min(1f, _heldSeconds / _requiredSeconds);

        /// <returns>True exactly once per continuous hold, on the tick the hold completes.</returns>
        public bool Tick(bool inRange, float deltaSeconds)
        {
            if (!inRange)
            {
                Reset();
                return false;
            }

            if (_fired)
            {
                return false;
            }

            if (deltaSeconds > 0f)
            {
                _heldSeconds += deltaSeconds;
            }

            if (_heldSeconds >= _requiredSeconds)
            {
                _fired = true;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            _heldSeconds = 0f;
            _fired = false;
        }
    }
}
