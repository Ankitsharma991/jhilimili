using UnityEngine;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Creates the <see cref="AnalyticsService"/> with the providers for this build: Firebase Analytics and
    /// Crashlytics when UTSAV_FIREBASE is defined, otherwise local development logs. Runs after the
    /// consent service exists; nothing is sent until consent and the usage-statistics switch allow it.
    /// </summary>
    [DefaultExecutionOrder(-130)]
    public sealed class AnalyticsInstaller : MonoBehaviour
    {
        private AnalyticsService _analytics;

        private void Awake()
        {
            if (Game.Services.IsRegistered<AnalyticsService>())
            {
                return;
            }

#if UTSAV_FIREBASE
            IAnalyticsBackend backend = new FirebaseAnalyticsBackend();
            ICrashReporter crashes = new CrashlyticsReporter();
#else
            IAnalyticsBackend backend = new DevAnalyticsBackend();
            ICrashReporter crashes = new DevCrashReporter();
#endif
            _analytics = new AnalyticsService(backend, crashes);
            Game.Services.Register(_analytics);
            Game.Loop.Add(_analytics);
        }

        private void Start()
        {
            if (_analytics == null)
            {
                return;
            }

            if (Game.Services.TryGet(out ConsentService consent))
            {
                _analytics.Apply(consent.Status);
            }

            _analytics.ReportFirstOpen(Game.Services.TryGet(out QualityManager quality) ? quality.Tier : 0);
            Debug.Log($"[Analytics] backend={_analytics.Backend.Name} crashes={_analytics.Crashes.Name} state={_analytics.ConsentState}");
        }

        private void OnApplicationQuit() => _analytics?.EndSession();

        private void OnDestroy()
        {
            if (_analytics == null)
            {
                return;
            }

            Game.Loop.Remove(_analytics);
            _analytics.Dispose();
            (_analytics.Crashes as System.IDisposable)?.Dispose();
            Game.Services.Unregister<AnalyticsService>();
        }
    }
}
