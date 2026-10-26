using System;

namespace UtsavNights.Core.Ignition
{
    public enum FlameState
    {
        /// <summary>No finger down; the lighter is not shown.</summary>
        Hidden = 0,

        /// <summary>Finger down, flame burning.</summary>
        Burning = 1,

        /// <summary>Flame went out after too long away from a fuse; lift and touch again to relight.</summary>
        Out = 2,
    }

    /// <summary>
    /// Lighter rules (PRD 6.1, 6.3), engine-free:
    /// a new touch lights the flame; holding it near one fuse for the hold time ignites that fuse;
    /// moving to another fuse restarts the hold; after the flame-out time away from every fuse the
    /// flame goes out until the finger is lifted and put down again. No random duds.
    /// </summary>
    public sealed class LighterModel
    {
        public const float DefaultHoldSeconds = 0.5f;
        public const float DefaultFlameOutSeconds = 3f;

        private readonly IgnitionHoldTracker _hold;
        private readonly float _flameOutSeconds;

        public LighterModel(float holdSeconds = DefaultHoldSeconds, float flameOutSeconds = DefaultFlameOutSeconds)
        {
            if (!(flameOutSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(flameOutSeconds));
            }

            _hold = new IgnitionHoldTracker(holdSeconds);
            _flameOutSeconds = flameOutSeconds;
        }

        public event Action FlameLit;

        public event Action FlameWentOut;

        /// <summary>Raised with the target id when a fuse catches.</summary>
        public event Action<int> Ignited;

        public FlameState State { get; private set; } = FlameState.Hidden;

        /// <summary>Fuse currently being heated, or -1.</summary>
        public int TargetId { get; private set; } = -1;

        public float HoldProgress => _hold.Progress;

        public float AwaySeconds { get; private set; }

        public float FlameOutSeconds => _flameOutSeconds;

        /// <param name="touching">A finger is driving the lighter this frame.</param>
        /// <param name="targetInRange">Id of the ignitable fuse under the flame, or -1.</param>
        public void Update(bool touching, int targetInRange, float deltaSeconds)
        {
            if (!touching)
            {
                if (State != FlameState.Hidden)
                {
                    State = FlameState.Hidden;
                    ClearTarget();
                    AwaySeconds = 0f;
                }

                return;
            }

            if (State == FlameState.Hidden)
            {
                State = FlameState.Burning;
                AwaySeconds = 0f;
                FlameLit?.Invoke();
            }

            if (State == FlameState.Out)
            {
                ClearTarget();
                return;
            }

            if (targetInRange >= 0)
            {
                AwaySeconds = 0f;
                if (targetInRange != TargetId)
                {
                    _hold.Reset();
                    TargetId = targetInRange;
                }

                if (_hold.Tick(true, deltaSeconds))
                {
                    Ignited?.Invoke(TargetId);
                }

                return;
            }

            ClearTarget();
            if (deltaSeconds > 0f)
            {
                AwaySeconds += deltaSeconds;
            }

            if (AwaySeconds >= _flameOutSeconds)
            {
                State = FlameState.Out;
                FlameWentOut?.Invoke();
            }
        }

        private void ClearTarget()
        {
            TargetId = -1;
            _hold.Reset();
        }
    }
}
