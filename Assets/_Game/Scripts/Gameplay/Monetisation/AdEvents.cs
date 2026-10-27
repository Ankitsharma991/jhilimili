using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    public enum AdOutcome
    {
        /// <summary>A rewarded ad was watched to the end; grant the reward.</summary>
        Rewarded,

        /// <summary>Shown and closed (an interstitial, or a rewarded ad skipped early).</summary>
        Closed,

        /// <summary>Not shown: the rules said no right now (see <see cref="AdBlock"/>).</summary>
        Blocked,

        /// <summary>Not shown: nothing loaded, or the network or SDK failed. Never blocks play.</summary>
        Failed,
    }

    /// <summary>
    /// PRD 17 ad_shown / ad_failed / ad_reward, and every refusal for the placement audit. Published on
    /// the event bus for the playtest log and analytics.
    /// </summary>
    public readonly struct AdEvent
    {
        public AdEvent(string type, AdKind kind, string placement, int msSincePayoff, AdBlock block = AdBlock.None)
        {
            Type = type;
            Kind = kind;
            Placement = placement;
            MsSincePayoff = msSincePayoff;
            Block = block;
        }

        /// <summary>ad_shown, ad_reward, ad_closed, ad_failed or ad_blocked.</summary>
        public string Type { get; }

        public AdKind Kind { get; }

        public string Placement { get; }

        /// <summary>-1 when there has been no payoff this session.</summary>
        public int MsSincePayoff { get; }

        public AdBlock Block { get; }
    }
}
