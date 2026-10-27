using UnityEngine;
using UnityEngine.SceneManagement;
using UtsavNights.Core.Diagnostics;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Fills the <see cref="StartupTimeline"/> for this launch (Phase 5 step 3) and logs it to logcat as
    /// "[UTSAV_STARTUP] pre_engine_ms=… interactive_ms=…", once when the scene is interactive and again
    /// for each SDK that finishes later. tools/measure_cold_start.sh reads that line. On Android the
    /// pre-engine part comes from the real process start time, so the total is what a player waits.
    /// SDK backends call <see cref="Mark"/> when they are ready.
    /// </summary>
    public static class StartupProbe
    {
        public const string LogTag = "[UTSAV_STARTUP]";

        public static StartupTimeline Timeline { get; private set; } = new StartupTimeline();

        private static float Now => (float)Time.realtimeSinceStartupAsDouble;

        /// <summary>Records a named moment the first time it happens; logs it once the scene is interactive.</summary>
        public static void Mark(string name)
        {
            if (Timeline.Mark(name, Now) && Timeline.IsInteractive)
            {
                Debug.Log($"{LogTag} {Timeline.Describe()}");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Timeline = new StartupTimeline { PreEngineSeconds = PreEngineSeconds() };
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            Timeline.Mark("first_scene", Now);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Game.Events.Subscribe<SceneReadyEvent>(OnSceneReady);
            Game.Events.Subscribe<ConsentChangedEvent>(OnConsent);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
            {
                Timeline.Mark("main_loaded", Now);
            }
        }

        private static void OnSceneReady(SceneReadyEvent e)
        {
            if (Timeline.Mark(StartupTimeline.Interactive, e.SecondsSinceLaunch))
            {
                Debug.Log($"{LogTag} {Timeline.Describe()}");
            }
        }

        private static void OnConsent(ConsentChangedEvent e)
        {
            if (e.Status != ConsentStatus.Unknown)
            {
                Mark("consent");
            }
        }

        /// <summary>Process start to engine start, from android.os.Process; 0 elsewhere.</summary>
        private static float PreEngineSeconds()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var process = new AndroidJavaClass("android.os.Process"))
                using (var clock = new AndroidJavaClass("android.os.SystemClock"))
                {
                    long started = process.CallStatic<long>("getStartElapsedRealtime");
                    long now = clock.CallStatic<long>("elapsedRealtime");
                    return Mathf.Max(0f, (now - started) / 1000f - Now);
                }
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning($"{LogTag} no process start time: {e.Message}");
                return 0f;
            }
#else
            return 0f;
#endif
        }
    }
}
