using System;
using UnityEngine;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>Raised when consent or the usage-statistics setting changes what may run.</summary>
    public readonly struct ConsentChangedEvent
    {
        public ConsentChangedEvent(ConsentStatus status, bool adsAllowed, bool analyticsAllowed)
        {
            Status = status;
            AdsAllowed = adsAllowed;
            AnalyticsAllowed = analyticsAllowed;
        }

        public ConsentStatus Status { get; }

        public bool AdsAllowed { get; }

        public bool AnalyticsAllowed { get; }
    }

    /// <summary>
    /// Gathers consent at launch before any ad loads (PRD 15.2), offers the Privacy options entry in
    /// Settings, and keeps ads and analytics in line with the result. Errors are logged and leave ads off;
    /// the game itself never waits on consent.
    /// </summary>
    public sealed class ConsentService
    {
        private readonly AdService _ads;
        private readonly Func<bool> _shareUsageStats;

        public ConsentService(IConsentBackend backend, AdService ads, Func<bool> shareUsageStats)
        {
            Backend = backend;
            _ads = ads;
            _shareUsageStats = shareUsageStats;
            ApplyToAds();
        }

        public IConsentBackend Backend { get; }

        public ConsentStatus Status => Backend.Status;

        public bool Gathered { get; private set; }

        public bool AnalyticsAllowed => ConsentRules.AnalyticsAllowed(_shareUsageStats(), Backend.Status);

        public string Description => ConsentRules.Describe(Backend.Status, Backend.PrivacyOptionsRequired);

        public void Gather()
        {
            Backend.Gather(error =>
            {
                Gathered = true;
                if (error != null)
                {
                    Debug.LogWarning($"[Consent] {error}");
                }

                Apply();
                _ads?.Preload();
            });

            // While a form is up (or after a reset) the status is already Required: stop ads now, not
            // only when the player answers.
            ApplyToAds();
        }

        /// <summary>The Settings "Privacy options" entry.</summary>
        public void ShowPrivacyOptions(Action done = null)
        {
            Backend.ShowPrivacyOptions(error =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[Consent] {error}");
                }

                Apply();
                done?.Invoke();
            });
        }

        /// <summary>Re-evaluates after a settings change (the usage-statistics switch).</summary>
        public void Apply()
        {
            ApplyToAds();
            Game.Events.Publish(new ConsentChangedEvent(Backend.Status, Backend.CanRequestAds, AnalyticsAllowed));
        }

        private void ApplyToAds()
        {
            if (_ads != null)
            {
                _ads.Policy.ConsentAllowsAds = Backend.CanRequestAds;
            }
        }
    }
}
