using System;
using UnityEngine;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Settings;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Creates the <see cref="ProgressService"/> (progress.json), the <see cref="AdService"/> and the
    /// <see cref="ConsentService"/> and the <see cref="PurchaseService"/> with the providers registered in
    /// <see cref="MonetisationProviders"/> (AdMob, UMP, Unity IAP on a device), otherwise the development
    /// card, form and store. Consent is gathered in Start, before any ad loads. Also the
    /// <see cref="ReviewService"/> (Phase 5 step 4): Google Play's rating prompt in store builds, a test card
    /// otherwise.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public sealed class MonetisationInstaller : MonoBehaviour
    {
        [Tooltip("Interstitials at natural breaks (PRD 15.1). Off for R0 until a closed test compares with and without.")]
        [SerializeField] private bool interstitialsEnabled = false;

        private AdService _ads;
        private ConsentService _consent;
        private PurchaseService _purchases;
        private ReviewService _review;
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

            IReviewBackend review = BuildFlavor.IsStore ? new PlayReviewBackend() : (IReviewBackend)gameObject.AddComponent<DevReviewBackend>();
            _review = new ReviewService(review, new ReviewStore(), _ads, () => (float)Time.realtimeSinceStartupAsDouble,
                () => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Game.Services.Register(_review);
            Game.Loop.Add(_review);
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
            if (_purchases.Backend is DevPurchaseBackend store && FindFirstObjectByType<DevOverlay>() is DevOverlay tools)
            {
                tools.AddTool("Store: refund", () =>
                {
                    store.Refund();
                    _purchases.Restore(message => Debug.Log($"[Store] refund simulated: {message}"));
                });
            }

            DevOverlay overlay = FindFirstObjectByType<DevOverlay>();

            // Phase 4 audit runs on a device: interstitials are off for R0, so switch them on to test them.
            overlay?.AddTool("Interstitials on/off", () =>
            {
                _ads.Policy.InterstitialsEnabled = !_ads.Policy.InterstitialsEnabled;
                Debug.Log($"[Ads] interstitials {(_ads.Policy.InterstitialsEnabled ? "on" : "off")} for this session");
            });

            overlay?.AddTool("Review: ask now", _review.AskNowForTesting);
            overlay?.AddTool("Review: reset", () =>
            {
                _review.ResetForTesting();
                Debug.Log("[Review] reset: the prompt may come after a finale or 10 payoffs");
            });

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
            Game.Loop.Remove(_review);
            _review.Dispose();
            Game.Services.Unregister<ReviewService>();

            Game.Loop.Remove(_ads);
            _ads.Dispose();
            Game.Services.Unregister<AdService>();
            Game.Services.Unregister<ProgressService>();
        }

        private IPurchaseBackend CreatePurchaseBackend() =>
            MonetisationProviders.Purchases?.Invoke() ?? gameObject.AddComponent<DevPurchaseBackend>();

        private IConsentBackend CreateConsentBackend() =>
            MonetisationProviders.Consent?.Invoke() ?? gameObject.AddComponent<DevConsentBackend>();

        private IAdBackend CreateBackend() =>
            MonetisationProviders.Ads?.Invoke() ?? gameObject.AddComponent<DevAdBackend>();
    }
}
