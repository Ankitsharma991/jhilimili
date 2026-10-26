using System;

namespace UtsavNights.Core.Crackers
{
    /// <summary>
    /// Drives one cracker through Idle, Placed, Lit, Burning, Payoff and Cooldown from its timeline.
    /// Engine-free and allocation-free per tick: a central ticker calls <see cref="Tick"/>; listeners
    /// subscribe once to <see cref="CueReached"/> and <see cref="StateChanged"/> to play effects.
    /// </summary>
    public sealed class CrackerStateMachine
    {
        private readonly CrackerTimeline _timeline;
        private int _nextCue;
        private float _cooldownElapsed;

        public CrackerStateMachine(CrackerTimeline timeline)
        {
            _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        }

        /// <summary>Raised with (previous, current) on every state change.</summary>
        public event Action<CrackerState, CrackerState> StateChanged;

        /// <summary>Raised with the stage index when a cue's time is reached, before any state change it causes.</summary>
        public event Action<int> CueReached;

        /// <summary>Raised once when a used cracker finishes its cooldown and returns to Idle.</summary>
        public event Action Finished;

        public CrackerTimeline Timeline => _timeline;

        public CrackerState State { get; private set; } = CrackerState.Idle;

        /// <summary>Seconds since ignition; 0 before ignition.</summary>
        public float Elapsed { get; private set; }

        public bool IsPaused { get; private set; }

        /// <summary>True from ignition until the cooldown ends.</summary>
        public bool IsActive => State >= CrackerState.Lit;

        /// <summary>Fuse progress 0..1 while Lit; 1 afterwards; 0 before ignition.</summary>
        public float FuseProgress
        {
            get
            {
                if (State < CrackerState.Lit)
                {
                    return 0f;
                }

                return State == CrackerState.Lit ? Math.Min(1f, Elapsed / _timeline.FuseSeconds) : 1f;
            }
        }

        public bool Place()
        {
            if (State != CrackerState.Idle)
            {
                return false;
            }

            SetState(CrackerState.Placed);
            return true;
        }

        /// <summary>Removes a placed cracker that has not been lit.</summary>
        public bool Unplace()
        {
            if (State != CrackerState.Placed)
            {
                return false;
            }

            SetState(CrackerState.Idle);
            return true;
        }

        /// <summary>Starts the fuse. Cues at time 0 fire immediately.</summary>
        public bool Ignite()
        {
            if (State != CrackerState.Placed)
            {
                return false;
            }

            Elapsed = 0f;
            _nextCue = 0;
            _cooldownElapsed = 0f;
            SetState(CrackerState.Lit);
            Advance(0f);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (IsPaused || deltaSeconds <= 0f || State < CrackerState.Lit)
            {
                return;
            }

            if (State == CrackerState.Cooldown)
            {
                TickCooldown(deltaSeconds);
                return;
            }

            Advance(deltaSeconds);
        }

        /// <summary>Freezes timing, for app backgrounding (PRD 6.3). Visual and audio pausing is the caller's job.</summary>
        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;

        /// <summary>Safely abandons any in-progress cracker and returns it to Idle without raising Finished.</summary>
        public void ResetToIdle()
        {
            Elapsed = 0f;
            _nextCue = 0;
            _cooldownElapsed = 0f;
            IsPaused = false;
            if (State != CrackerState.Idle)
            {
                SetState(CrackerState.Idle);
            }
        }

        private void Advance(float deltaSeconds)
        {
            float target = Elapsed + deltaSeconds;

            // Process events strictly in time order; at equal times the fuse ending comes before cues.
            while (State != CrackerState.Cooldown && State != CrackerState.Idle)
            {
                bool fusePending = State == CrackerState.Lit;
                float fuseTime = _timeline.FuseSeconds;
                bool cuePending = _nextCue < _timeline.CueCount;
                float cueTime = cuePending ? _timeline[_nextCue].Time : float.MaxValue;

                if (fusePending && fuseTime <= target && fuseTime <= cueTime)
                {
                    Elapsed = fuseTime;
                    SetState(CrackerState.Burning);
                    continue;
                }

                if (cuePending && cueTime <= target)
                {
                    Elapsed = Math.Max(Elapsed, cueTime);
                    TimelineCue cue = _timeline[_nextCue];
                    _nextCue++;
                    CueReached?.Invoke(cue.StageIndex);
                    ApplyCue(cue);
                    continue;
                }

                break;
            }

            if (State == CrackerState.Cooldown)
            {
                float overflow = target - _timeline.StopSeconds;
                if (overflow > 0f)
                {
                    TickCooldown(overflow);
                }
            }
            else if (State != CrackerState.Idle)
            {
                Elapsed = target;
            }
        }

        private void ApplyCue(TimelineCue cue)
        {
            switch (cue.Kind)
            {
                case CueKind.Payoff:
                    if (State != CrackerState.Payoff)
                    {
                        if (State == CrackerState.Lit)
                        {
                            // A payoff before the fuse ends still counts as the fuse finishing.
                            SetState(CrackerState.Burning);
                        }

                        SetState(CrackerState.Payoff);
                    }

                    break;
                case CueKind.Stop:
                    Elapsed = _timeline.StopSeconds;
                    _cooldownElapsed = 0f;
                    SetState(CrackerState.Cooldown);
                    if (_timeline.CooldownSeconds <= 0f)
                    {
                        Finish();
                    }

                    break;
            }
        }

        private void TickCooldown(float deltaSeconds)
        {
            if (State != CrackerState.Cooldown)
            {
                return;
            }

            _cooldownElapsed += deltaSeconds;
            if (_cooldownElapsed >= _timeline.CooldownSeconds)
            {
                Finish();
            }
        }

        private void Finish()
        {
            Elapsed = 0f;
            _nextCue = 0;
            _cooldownElapsed = 0f;
            SetState(CrackerState.Idle);
            Finished?.Invoke();
        }

        private void SetState(CrackerState next)
        {
            CrackerState previous = State;
            if (previous == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
