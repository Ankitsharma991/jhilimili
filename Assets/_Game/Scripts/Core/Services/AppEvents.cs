namespace UtsavNights.Core.Services
{
    /// <summary>App went to background (paused = true) or returned (false). PRD 6.3 and 11 interruptions.</summary>
    public readonly struct AppPausedEvent
    {
        public AppPausedEvent(bool paused) => Paused = paused;

        public bool Paused { get; }
    }

    /// <summary>Raised once when boot finished and the main scene is interactive (analytics: scene_ready).</summary>
    public readonly struct SceneReadyEvent
    {
        public SceneReadyEvent(float secondsSinceLaunch) => SecondsSinceLaunch = secondsSinceLaunch;

        public float SecondsSinceLaunch { get; }
    }
}
