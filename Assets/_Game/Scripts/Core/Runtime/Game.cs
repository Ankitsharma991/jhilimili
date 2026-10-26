using UnityEngine;
using UtsavNights.Core.Services;

namespace UtsavNights.Core.Runtime
{
    /// <summary>
    /// Global access to the core services. Created automatically before the first scene loads, so every
    /// scene (including test scenes opened directly in the editor) has a working loop and event bus.
    /// </summary>
    public static class Game
    {
        public static ServiceRegistry Services { get; private set; } = new ServiceRegistry();

        public static EventBus Events { get; private set; } = new EventBus();

        public static TickLoop Loop { get; private set; } = new TickLoop();

        public static GameLoop Host { get; private set; }

        public static bool IsBooted => Host != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Supports "Enter Play Mode" without domain reload.
            Services = new ServiceRegistry();
            Events = new EventBus();
            Loop = new TickLoop();
            Host = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBoot()
        {
            if (Host != null)
            {
                return;
            }

            var go = new GameObject("[Game]");
            Object.DontDestroyOnLoad(go);
            Host = go.AddComponent<GameLoop>();
            Events.HandlerFailed += (type, e) => Debug.LogException(e);
            Loop.TickFailed += (item, e) => Debug.LogException(e);
        }
    }
}
