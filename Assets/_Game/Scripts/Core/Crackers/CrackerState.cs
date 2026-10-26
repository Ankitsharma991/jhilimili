namespace UtsavNights.Core.Crackers
{
    /// <summary>Lifecycle of one placed cracker (PRD 7.1). Gameplay code never names a specific cracker.</summary>
    public enum CrackerState
    {
        /// <summary>Not in the scene, or spent and ready to return to its pool.</summary>
        Idle = 0,

        /// <summary>Placed on a surface, waiting for the flame.</summary>
        Placed = 1,

        /// <summary>Fuse burning (from ignition until the definition's fuse time).</summary>
        Lit = 2,

        /// <summary>Fuse done, main effect running before any payoff stage.</summary>
        Burning = 3,

        /// <summary>The payoff stage is playing (fountain peak, burst, bang).</summary>
        Payoff = 4,

        /// <summary>Effect stopped; spent item lingers before it is cleared (PRD 6.3: 2 s).</summary>
        Cooldown = 5,
    }

    /// <summary>What a timeline cue means for the state machine.</summary>
    public enum CueKind
    {
        /// <summary>Starts emitters, sounds, haptics or lights; no state change.</summary>
        Normal = 0,

        /// <summary>Also moves the cracker into <see cref="CrackerState.Payoff"/>.</summary>
        Payoff = 1,

        /// <summary>Ends the effect and starts the cooldown. Must be the last cue.</summary>
        Stop = 2,
    }
}
