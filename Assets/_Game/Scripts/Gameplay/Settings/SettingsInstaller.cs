using UnityEngine;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Loads settings.json, registers the <see cref="SettingsService"/> and applies settings once all
    /// services have installed themselves (Start runs after every Awake).
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class SettingsInstaller : MonoBehaviour
    {
        public SettingsService Service { get; private set; }

        private void Awake()
        {
            if (Game.Services.TryGet(out SettingsService existing))
            {
                Service = existing;
                return;
            }

            var store = new SettingsStore();
            Service = new SettingsService(store, store.Load());
            Game.Services.Register(Service);
            if (store.LastLoadIssue != null)
            {
                Debug.Log($"[Settings] using defaults ({store.LastLoadIssue}): {store.Path}");
            }
        }

        private void Start() => Service.ApplyToRegisteredServices();
    }
}
