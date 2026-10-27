namespace UtsavNights.Core.Monetisation
{
    public enum AdKind
    {
        /// <summary>The player asked for it (unlock a premium cracker).</summary>
        Rewarded,

        /// <summary>Full screen at a natural break; never asked for.</summary>
        Interstitial,
    }

    /// <summary>Why an ad may not show now; <see cref="None"/> means it may.</summary>
    public enum AdBlock
    {
        None,
        FeatureOff,
        AdsRemoved,
        NoConsent,
        CrackerActive,
        Placing,
        FinaleRunning,
        TooSoonAfterPayoff,
        TooSoonAfterAd,
        TooFewIgnitions,
        RecentTouch,
    }

    /// <summary>What is happening in the scene right now, read when an ad is about to show.</summary>
    public readonly struct AdScene
    {
        public AdScene(bool crackerActive, bool placing, bool finaleRunning, float secondsSinceTouch = float.PositiveInfinity)
        {
            CrackerActive = crackerActive;
            Placing = placing;
            FinaleRunning = finaleRunning;
            SecondsSinceTouch = secondsSinceTouch;
        }

        /// <summary>A fuse is burning, an effect is playing or something is in the air.</summary>
        public bool CrackerActive { get; }

        /// <summary>The player is dragging a cracker into place (PRD 15.1: decoration drag).</summary>
        public bool Placing { get; }

        public bool FinaleRunning { get; }

        /// <summary>Time since the finger last left the screen; an interstitial never lands under a tap.</summary>
        public float SecondsSinceTouch { get; }
    }

    /// <summary>
    /// PRD 15.1 ad rules (F10). Interstitials only at natural breaks: 6 or more ignitions and 4 minutes
    /// or more since the last ad (the session start counts as an ad, so never at launch), never within
    /// 10 s of a payoff. No ad of any kind during a fuse, payoff, launch, placement drag or finale.
    /// Times are seconds on one monotonic clock.
    /// </summary>
    public sealed class AdPolicy
    {
        public const int MinIgnitionsBetween = 6;
        public const float MinSecondsBetween = 240f;
        public const float PayoffQuietSeconds = 10f;

        /// <summary>PRD 15.1: no ad immediately after a tap (accidental-click protection).</summary>
        public const float TouchQuietSeconds = 3f;

        private float _lastAdTime;
        private float _lastPayoffTime = float.NegativeInfinity;

        public AdPolicy(float sessionStart)
        {
            _lastAdTime = sessionStart;
        }

        /// <summary>Interstitials are built but switched off until a closed test compares with and without.</summary>
        public bool InterstitialsEnabled { get; set; }

        public bool AdsEnabled { get; set; } = true;

        public bool AdsRemoved { get; set; }

        public bool ConsentAllowsAds { get; set; } = true;

        public int IgnitionsSinceAd { get; private set; }

        public float LastPayoffTime => _lastPayoffTime;

        public void RecordIgnition() => IgnitionsSinceAd++;

        public void RecordPayoff(float now) => _lastPayoffTime = now;

        public void RecordAdShown(float now)
        {
            _lastAdTime = now;
            IgnitionsSinceAd = 0;
        }

        /// <summary>Seconds since the last payoff, or -1 when there has been none (logged as ms_since_payoff).</summary>
        public float SecondsSincePayoff(float now) =>
            float.IsNegativeInfinity(_lastPayoffTime) ? -1f : now - _lastPayoffTime;

        public AdBlock Check(AdKind kind, float now, AdScene scene)
        {
            if (!AdsEnabled || (kind == AdKind.Interstitial && !InterstitialsEnabled))
            {
                return AdBlock.FeatureOff;
            }

            // A purchase removes interstitials; rewarded ads are pointless then (everything is unlocked).
            if (AdsRemoved)
            {
                return AdBlock.AdsRemoved;
            }

            if (!ConsentAllowsAds)
            {
                return AdBlock.NoConsent;
            }

            if (scene.CrackerActive)
            {
                return AdBlock.CrackerActive;
            }

            if (scene.Placing)
            {
                return AdBlock.Placing;
            }

            if (scene.FinaleRunning)
            {
                return AdBlock.FinaleRunning;
            }

            if (kind == AdKind.Rewarded)
            {
                return AdBlock.None;
            }

            if (scene.SecondsSinceTouch < TouchQuietSeconds)
            {
                return AdBlock.RecentTouch;
            }

            if (now - _lastPayoffTime < PayoffQuietSeconds)
            {
                return AdBlock.TooSoonAfterPayoff;
            }

            if (now - _lastAdTime < MinSecondsBetween)
            {
                return AdBlock.TooSoonAfterAd;
            }

            return IgnitionsSinceAd < MinIgnitionsBetween ? AdBlock.TooFewIgnitions : AdBlock.None;
        }

        /// <summary>Short text for the player when a rewarded ad they asked for cannot show yet.</summary>
        public static string Message(AdBlock block)
        {
            switch (block)
            {
                case AdBlock.CrackerActive:
                case AdBlock.FinaleRunning:
                    return "Wait for the fireworks to finish";
                case AdBlock.Placing:
                    return "Place or cancel the cracker first";
                case AdBlock.NoConsent:
                    return "Ads are off with your privacy choice";
                case AdBlock.None:
                    return string.Empty;
                default:
                    return "Not available right now";
            }
        }
    }
}
