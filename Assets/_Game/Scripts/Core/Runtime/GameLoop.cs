using UnityEngine;
using UtsavNights.Core.Services;

namespace UtsavNights.Core.Runtime
{
    /// <summary>
    /// The single Update for gameplay (PRD 13.3 central ticking) and the source of app pause events.
    /// Backgrounding and focus loss (calls, notification shade) pause the loop and publish
    /// <see cref="AppPausedEvent"/> so fuses, effects and audio can pause and resume cleanly (PRD 6.3, 11).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GameLoop : MonoBehaviour
    {
        private bool _appPaused;
        private bool _hasFocus = true;
        private bool _publishedPaused;

        /// <summary>Longest frame step handed to gameplay, so a hitch never skips a whole fuse.</summary>
        public const float MaxDeltaSeconds = 0.1f;

        public bool IsPaused => _publishedPaused;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt > MaxDeltaSeconds)
            {
                dt = MaxDeltaSeconds;
            }

            Game.Loop.Tick(dt);
        }

        private void OnApplicationPause(bool paused)
        {
            _appPaused = paused;
            PublishIfChanged();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            PublishIfChanged();
        }

        private void PublishIfChanged()
        {
            bool paused = _appPaused || !_hasFocus;
            if (paused == _publishedPaused)
            {
                return;
            }

            _publishedPaused = paused;
            Game.Loop.IsPaused = paused;
            Game.Events.Publish(new AppPausedEvent(paused));
        }
    }
}
