using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;

namespace UtsavNights.Effects
{
    /// <summary>
    /// Scene component that creates the <see cref="EffectsService"/>, registers it, ticks it from the
    /// central loop and pauses effects with the app. Put one in the main scene.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class EffectsInstaller : MonoBehaviour
    {
        [SerializeField] private QualityTier initialTier = QualityTier.Mid;

        public EffectsService Service { get; private set; }

        private void Awake()
        {
            Service = new EffectsService(transform, initialTier);
            if (Game.Services.IsRegistered<EffectsService>())
            {
                Debug.LogWarning("[Effects] An EffectsService is already registered; this installer is ignored.", this);
                return;
            }

            Game.Services.Register(Service);
            Game.Loop.Add(Service);
            Game.Events.Subscribe<AppPausedEvent>(OnAppPaused);
        }

        private void OnDestroy()
        {
            if (Service == null || !Game.Services.TryGet(out EffectsService registered) || registered != Service)
            {
                return;
            }

            Game.Events.Unsubscribe<AppPausedEvent>(OnAppPaused);
            Game.Loop.Remove(Service);
            Game.Services.Unregister<EffectsService>();
        }

        private void OnAppPaused(AppPausedEvent e) => Service.SetPaused(e.Paused);
    }
}
