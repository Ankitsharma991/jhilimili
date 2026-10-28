namespace UtsavNights.Gameplay
{
    /// <summary>PRD 17 cracker_placed.</summary>
    public readonly struct CrackerPlacedEvent
    {
        public CrackerPlacedEvent(string crackerId) => CrackerId = crackerId;

        public string CrackerId { get; }
    }

    /// <summary>PRD 17 cracker_ignited (is_first: first ignition of this install).</summary>
    public readonly struct CrackerIgnitedEvent
    {
        public CrackerIgnitedEvent(string crackerId, bool isFirst, float secondsSinceLaunch)
        {
            CrackerId = crackerId;
            IsFirst = isFirst;
            SecondsSinceLaunch = secondsSinceLaunch;
        }

        public string CrackerId { get; }

        public bool IsFirst { get; }

        public float SecondsSinceLaunch { get; }
    }

    /// <summary>The payoff (the burst, the fountain's peak) begins; the courtyard avatar celebrates (Phase 6 step 6).</summary>
    public readonly struct PayoffStartedEvent
    {
        public PayoffStartedEvent(string crackerId, UnityEngine.Vector3 position)
        {
            CrackerId = crackerId;
            Position = position;
        }

        public string CrackerId { get; }

        public UnityEngine.Vector3 Position { get; }
    }

    /// <summary>PRD 17 payoff_complete: the effect reached its stop stage.</summary>
    public readonly struct PayoffCompleteEvent
    {
        public PayoffCompleteEvent(string crackerId) => CrackerId = crackerId;

        public string CrackerId { get; }
    }

    /// <summary>The lighter flame went out after too long away from a fuse.</summary>
    public readonly struct FlameOutEvent
    {
    }
}
