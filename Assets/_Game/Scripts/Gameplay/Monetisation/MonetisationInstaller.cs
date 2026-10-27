using UnityEngine;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Settings;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Creates the <see cref="ProgressService"/> (progress.json), the <see cref="AdService"/> and the
    /// <see cref="ConsentService"/> and the <see cref="PurchaseService"/> with the providers for this build:
    /// AdMob and UMP when UTSAV_ADMOB is defined, Unity IAP when UTSAV_IAP is defined, otherwise the
    /// development card, form and store. Consent is gathered in Start, before any ad loads.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public sealed class MonetisationInstaller : MonoBehaviour
    {
        [Tooltip("Interstitials at natural breaks (PRD 15.1). Off for R0 until a closed test compares with and without.")]
        [SerializeField] private bool interstitialsEnabled = false;

        private AdService _ads;
        private ConsentService _consent;
        private PurchaseService _purchases;
        private bool _owner;

        private void Awake()
        {
            if (Game.Services.IsRegistered<AdService>())
            {
                return;
            }

            _owner = true;
            var progress = new ProgressService(new ProgressStore());
            Game.Services.Register(progress);

            IAdBackend backend = CreateBackend();
            _ads = new AdService(backend, () => (float)Time.realtimeSinceStartupAsDouble);
            _ads.Policy.InterstitialsEnabled = interstitialsEnabled;
            _ads.Policy.AdsRemoved = progress.AdsRemoved;
            progress.Changed += () => _ads.Policy.AdsRemoved = progress.AdsRemoved;
            Game.Services.Register(_ads);
            Game.Loop.Add(_ads);

            Game.Services.TryGet(out SettingsService settings);
            _consent = new ConsentService(CreateConsentBackend(), _ads, () => settings == null || settings.Current.shareUsageStats);
            Game.Services.Register(_consent);
            Game.Events.Subscribe<SettingsChangedEvent>(OnSettingsChanged);

            _purchases = new PurchaseService(CreatePurchaseBackend(), progress);
            Game.Services.Register(_purchases);
            Debug.Log($"[Ads] backend={backend.Name} interstitials={interstitialsEnabled} adsRemoved={progress.AdsRemoved}");
        }

        private void Start()
        {
            if (!_owner)
            {
                return;
            }

            _consent.Gather();
            _purchases.Initialize();
#if !UTSAV_IAP
            if (_purchases.Backend is DevPurchaseBackend store && FindFirstObjectByType<DevOverlay>() is DevOverlay tools)
            {
                tools.AddTool("Store: refund", () =>
                {
                    store.Refund();
                    _purchases.Restore(message => Debug.Log($"[Store] refund simulated: {message}"));
                });
            }
#endif
#if !UTSAV_ADMOB
            DevOverlay overlay = FindFirstObjectByType<DevOverlay>();
            if (overlay != null && _consent.Backend is DevConsentBackend dev)
            {
                overlay.AddTool("Consent: EEA on/off", () =>
                {
                    dev.InEea = !dev.InEea;
                    dev.Reset();
                    _consent.Gather();
                    Debug.Log($"[Consent] debug geography EEA={dev.InEea}");
                });
            }
#endif
        }

        private void OnSettingsChanged(SettingsChangedEvent e) => _consent.Apply();

        private void OnDestroy()
        {
            if (!_owner)
            {
                return;
            }

            Game.Events.Unsubscribe<SettingsChangedEvent>(OnSettingsChanged);
            Game.Services.Unregister<ConsentService>();
            Game.Services.Unregister<PurchaseService>();

            Game.Loop.Remove(_ads);
            _ads.Dispose();
            Game.Services.Unregister<AdService>();
            Game.Services.Unregister<ProgressService>();
        }

        private IPurchaseBackend CreatePurchaseBackend()
        {
#if UTSAV_IAP
            return new UnityIapBackend();
#else
            return gameObject.AddComponent<DevPurchaseBackend>();
#endif
        }

        private IConsentBackend CreateConsentBackend()
        {
#if UTSAV_ADMOB
            return new UmpConsentBackend();
#else
            return gameObject.AddComponent<DevConsentBackend>();
#endif
        }

        private IAdBackend CreateBackend()
        {
#if UTSAV_ADMOB
            return new AdMobBackend();
#else
            return gameObject.AddComponent<DevAdBackend>();
#endif
        }
    }
}
