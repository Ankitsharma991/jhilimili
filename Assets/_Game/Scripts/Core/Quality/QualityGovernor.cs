using System;

namespace UtsavNights.Core.Quality
{
    /// <summary>Runtime degradation steps, applied in this order (PRD 14).</summary>
    public enum DegradeStep
    {
        None = 0,
        ReduceParticles = 1,
        DisableBloom = 2,
        ReduceResolution = 3,
    }

    /// <summary>
    /// First-run tier choice (F08). Tiers are 0 = Low, 1 = Mid, 2 = High, matching the quality levels.
    /// Thresholds are hypotheses from PRD 14 until the budget phone is measured.
    /// </summary>
    public static class TierDetection
    {
        public const int Low = 0;
        public const int Mid = 1;
        public const int High = 2;

        /// <summary>Phones under this much RAM start on Low (PRD 2, 14: 3 to 4 GB budget phones).</summary>
        public const int LowRamMb = 4500;

        /// <summary>Phones at or above this reported RAM (8 GB devices report about 7.2 to 7.6 GB) with enough cores start on High.</summary>
        public const int HighRamMb = 7000;

        public const int HighMinCores = 8;

        /// <summary>Frame budget per tier: 30, 45 and 60 fps (PRD 14 frame-rate targets).</summary>
        public static float TargetFrameMs(int tier)
        {
            switch (Clamp(tier))
            {
                case Low: return 1000f / 30f;
                case Mid: return 1000f / 45f;
                default: return 1000f / 60f;
            }
        }

        public static int FromHardware(int systemMemoryMb, int processorCount)
        {
            if (systemMemoryMb > 0 && systemMemoryMb < LowRamMb)
            {
                return Low;
            }

            if (systemMemoryMb >= HighRamMb && processorCount >= HighMinCores)
            {
                return High;
            }

            return Mid;
        }

        /// <summary>
        /// Corrects the hardware guess with a short in-scene benchmark: more than 15 % over the tier's
        /// budget drops one tier, more than 60 % over drops two. Never raises the tier.
        /// </summary>
        public static int AdjustForBenchmark(int tier, float averageFrameMs)
        {
            tier = Clamp(tier);
            if (!(averageFrameMs > 0f))
            {
                return tier;
            }

            float budget = TargetFrameMs(tier);
            if (averageFrameMs > budget * 1.6f)
            {
                return Clamp(tier - 2);
            }

            if (averageFrameMs > budget * 1.15f)
            {
                return Clamp(tier - 1);
            }

            return tier;
        }

        public static int Clamp(int tier) => tier < Low ? Low : tier > High ? High : tier;
    }

    /// <summary>
    /// PRD 14 quality governor: follows a smoothed frame time; after <see cref="OverBudgetSeconds"/> over
    /// budget it steps down (particles, then bloom, then resolution). It steps back up at most once per
    /// session, after a long stretch with clear headroom. Hitches above 250 ms (loading, GC) are ignored.
    /// </summary>
    public sealed class QualityGovernor
    {
        public const float OverBudgetSeconds = 2f;
        public const float OverBudgetFactor = 1.1f;
        public const float StepUpAfterSeconds = 20f;
        public const float StepUpHeadroom = 0.7f;
        public const float HitchMs = 250f;
        private const float SmoothingSeconds = 0.5f;

        private float _smoothedMs = -1f;
        private float _overSeconds;
        private float _underSeconds;

        public QualityGovernor(float targetFrameMs)
        {
            if (!(targetFrameMs > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(targetFrameMs));
            }

            TargetFrameMs = targetFrameMs;
        }

        public event Action<DegradeStep> SteppedDown;

        /// <summary>Raised with the step being undone.</summary>
        public event Action<DegradeStep> SteppedUp;

        public float TargetFrameMs { get; }

        public float SmoothedFrameMs => _smoothedMs < 0f ? 0f : _smoothedMs;

        /// <summary>Number of steps currently applied (0 to 3).</summary>
        public int StepsApplied { get; private set; }

        public bool SteppedUpThisSession { get; private set; }

        public DegradeStep CurrentStep => (DegradeStep)StepsApplied;

        public bool Enabled { get; set; } = true;

        public void Sample(float frameMs, float deltaSeconds)
        {
            if (!Enabled || !(frameMs > 0f) || frameMs > HitchMs || deltaSeconds <= 0f)
            {
                return;
            }

            if (_smoothedMs < 0f)
            {
                _smoothedMs = frameMs;
            }
            else
            {
                float k = Math.Min(1f, deltaSeconds / SmoothingSeconds);
                _smoothedMs += (frameMs - _smoothedMs) * k;
            }

            if (_smoothedMs > TargetFrameMs * OverBudgetFactor)
            {
                _overSeconds += deltaSeconds;
                _underSeconds = 0f;
                if (_overSeconds >= OverBudgetSeconds && StepsApplied < (int)DegradeStep.ReduceResolution)
                {
                    StepsApplied++;
                    _overSeconds = 0f;
                    SteppedDown?.Invoke((DegradeStep)StepsApplied);
                }

                return;
            }

            _overSeconds = 0f;
            if (_smoothedMs < TargetFrameMs * StepUpHeadroom && StepsApplied > 0 && !SteppedUpThisSession)
            {
                _underSeconds += deltaSeconds;
                if (_underSeconds >= StepUpAfterSeconds)
                {
                    DegradeStep undone = (DegradeStep)StepsApplied;
                    StepsApplied--;
                    SteppedUpThisSession = true;
                    _underSeconds = 0f;
                    SteppedUp?.Invoke(undone);
                }
            }
            else
            {
                _underSeconds = 0f;
            }
        }
    }

    /// <summary>Published when the tier or a degradation step changes (analytics perf_sample, toasts).</summary>
    public readonly struct QualityChangedEvent
    {
        public QualityChangedEvent(int tier, int stepsApplied, string reason)
        {
            Tier = tier;
            StepsApplied = stepsApplied;
            Reason = reason;
        }

        public int Tier { get; }

        public int StepsApplied { get; }

        public string Reason { get; }
    }
}
