using System;

namespace UtsavNights.Core.Engagement
{
    /// <summary>Why the review prompt was not asked for now; None means ask.</summary>
    public enum ReviewBlock
    {
        None,
        FirstSession,
        Cooldown,
        AskedThisSession,
        NotAGoodSession,
        TooSoonAfterAd,
        Busy,
        Settling,
    }

    /// <summary>Saved between launches (review.json).</summary>
    [Serializable]
    public sealed class ReviewState
    {
        public int sessions;
        public long lastPromptUnix;
        public int prompts;
    }

    /// <summary>The moment the review prompt would appear.</summary>
    public struct ReviewMoment
    {
        public long NowUnix;
        public bool AskedThisSession;
        public int FinalesCompleted;
        public int Payoffs;

        /// <summary>Since the last ad closed; PositiveInfinity when no ad showed this session.</summary>
        public float SecondsSinceAd;

        /// <summary>Since the last payoff; PositiveInfinity before the first.</summary>
        public float SecondsSincePayoff;

        public float SecondsSinceTouch;

        /// <summary>Something burning, the player placing, a finale running, an ad or a sheet open.</summary>
        public bool Busy;
    }

    /// <summary>
    /// When to ask for a Google Play rating (PRD 19 Phase 5: "after a good session, not after an ad").
    /// Ask only from the second session on, after the player has enjoyed this one (a finale finished, or
    /// at least <see cref="GoodSessionPayoffs"/> payoffs), at a quiet moment: nothing burning, the last
    /// burst settled, no touch for a moment, and well clear of any ad. At most once a session and once
    /// every 30 days. Play applies its own quota on top and may show nothing; the game never knows
    /// whether the player rated, and never asks twice in a row because of that.
    /// </summary>
    public static class ReviewPolicy
    {
        public const int MinSessions = 2;
        public const long CooldownSeconds = 30L * 24 * 60 * 60;

        /// <summary>[Hypothesis] about two minutes of active play; tune from closed-test session data.</summary>
        public const int GoodSessionPayoffs = 10;

        public const float AdQuietSeconds = 120f;
        public const float PayoffSettleSeconds = 3f;
        public const float TouchQuietSeconds = 2f;

        public static ReviewBlock Check(ReviewState state, ReviewMoment moment)
        {
            if (state == null || state.sessions < MinSessions)
            {
                return ReviewBlock.FirstSession;
            }

            if (moment.AskedThisSession)
            {
                return ReviewBlock.AskedThisSession;
            }

            if (state.lastPromptUnix > 0 && moment.NowUnix - state.lastPromptUnix < CooldownSeconds)
            {
                return ReviewBlock.Cooldown;
            }

            if (moment.FinalesCompleted < 1 && moment.Payoffs < GoodSessionPayoffs)
            {
                return ReviewBlock.NotAGoodSession;
            }

            if (moment.SecondsSinceAd < AdQuietSeconds)
            {
                return ReviewBlock.TooSoonAfterAd;
            }

            if (moment.Busy)
            {
                return ReviewBlock.Busy;
            }

            if (moment.SecondsSincePayoff < PayoffSettleSeconds || moment.SecondsSinceTouch < TouchQuietSeconds)
            {
                return ReviewBlock.Settling;
            }

            return ReviewBlock.None;
        }

        /// <summary>Call once per app launch, before any check.</summary>
        public static void StartSession(ReviewState state) => state.sessions++;

        /// <summary>Call when the prompt is requested, whatever Play then shows.</summary>
        public static void RecordPrompt(ReviewState state, long nowUnix)
        {
            state.lastPromptUnix = nowUnix;
            state.prompts++;
        }
    }
}
