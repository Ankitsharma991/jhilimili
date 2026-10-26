using UnityEngine;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;

namespace UtsavNights.Haptics
{
    /// <summary>Creates and registers the <see cref="HapticsService"/> with the platform backend.</summary>
    [DefaultExecutionOrder(-450)]
    public sealed class HapticsInstaller : MonoBehaviour
    {
        public HapticsService Service { get; private set; }

        private void Awake()
        {
            if (Game.Services.IsRegistered<HapticsService>())
            {
                Debug.LogWarning("[Haptics] A HapticsService is already registered; this installer is ignored.", this);
                return;
            }

            Service = new HapticsService(HapticsBackendFactory.CreateDefault());
            Game.Services.Register(Service);
            Game.Loop.Add(Service);
            Game.Events.Subscribe<AppPausedEvent>(OnAppPaused);
            Debug.Log($"[Haptics] vibrator={Service.Backend.HasVibrator} amplitude={Service.Backend.HasAmplitudeControl}");
        }

        private void OnDestroy()
        {
            if (Service == null)
            {
                return;
            }

            Game.Events.Unsubscribe<AppPausedEvent>(OnAppPaused);
            Game.Loop.Remove(Service);
            Game.Services.Unregister<HapticsService>();
        }

        private void OnAppPaused(AppPausedEvent e) => Service.SetPaused(e.Paused);
    }
}
