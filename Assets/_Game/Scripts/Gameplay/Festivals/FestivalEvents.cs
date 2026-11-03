namespace UtsavNights.Gameplay
{
    /// <summary>The New Year countdown reached midnight (Phase 11; analytics: whether the Midnight show could play at the spot).</summary>
    public readonly struct MidnightEvent
    {
        public MidnightEvent(bool showPlayed) => ShowPlayed = showPlayed;

        public bool ShowPlayed { get; }
    }
}
