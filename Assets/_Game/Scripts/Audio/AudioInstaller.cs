using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;

namespace UtsavNights.Audio
{
    /// <summary>
    /// Scene component that sets up low-latency output, creates the <see cref="AudioService"/> with the
    /// tier's voice count, registers it, ticks it centrally and pauses it with the app.
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public sealed class AudioInstaller : MonoBehaviour
    {
        [SerializeField] private QualityTier initialTier = QualityTier.Mid;

        public AudioService Service { get; private set; }

        private void Awake()
        {
            if (Game.Services.IsRegistered<AudioService>())
            {
                Debug.LogWarning("[Audio] An AudioService is already registered; this installer is ignored.", this);
                return;
            }

            if (AudioService.EnsureLowLatencyOutput())
            {
                AudioConfiguration c = AudioSettings.GetConfiguration();
                Debug.Log($"[Audio] Output reset to {c.sampleRate} Hz, buffer {c.dspBufferSize}.");
            }

            Service = new AudioService(transform, QualityBudgets.MaxVoices(initialTier));
            Game.Services.Register(Service);
            Game.Loop.Add(Service);
            Game.Events.Subscribe<AppPausedEvent>(OnAppPaused);
        }

        private void OnDestroy()
        {
            if (Service == null)
            {
                return;
            }

            Game.Events.Unsubscribe<AppPausedEvent>(OnAppPaused);
            Game.Loop.Remove(Service);
            Game.Services.Unregister<AudioService>();
            if (Service.IsPaused)
            {
                AudioListener.pause = false;
            }
        }

        private void OnAppPaused(AppPausedEvent e) => Service.SetPaused(e.Paused);
    }
}
