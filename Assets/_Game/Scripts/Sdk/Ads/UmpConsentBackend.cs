using System;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UtsavNights.Gameplay;
using ConsentStatus = UtsavNights.Core.Monetisation.ConsentStatus;
using UmpStatus = GoogleMobileAds.Ump.Api.ConsentStatus;

namespace UtsavNights.Ads
{
    /// <summary>
    /// Google User Messaging Platform provider for <see cref="ConsentService"/>: updates consent info at
    /// launch, shows Google's consent form where required (EEA, UK and similar), and the privacy options
    /// form from Settings. Debug geography applies only to the test devices listed in <see cref="AdsConfig"/>.
    /// </summary>
    public sealed class UmpConsentBackend : IConsentBackend
    {
        private readonly AdsConfig _config;

        public UmpConsentBackend(AdsConfig config) => _config = config;

        public string Name => "ump";

        public ConsentStatus Status
        {
            get
            {
                switch (ConsentInformation.ConsentStatus)
                {
                    case UmpStatus.NotRequired:
                        return ConsentStatus.NotRequired;
                    case UmpStatus.Required:
                        return ConsentStatus.Required;
                    case UmpStatus.Obtained:
                        return ConsentStatus.Obtained;
                    default:
                        return ConsentStatus.Unknown;
                }
            }
        }

        public bool CanRequestAds => ConsentInformation.CanRequestAds();

        public bool PrivacyOptionsRequired => ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void Gather(Action<string> done)
        {
            var parameters = new ConsentRequestParameters
            {
                // PRD 2: general audience, not designed for children under 13.
                TagForUnderAgeOfConsent = false,
            };
            if (_config.testDeviceIds.Count > 0)
            {
                parameters.ConsentDebugSettings = new ConsentDebugSettings
                {
                    DebugGeography = _config.debugGeographyEea ? DebugGeography.EEA : DebugGeography.Disabled,
                    TestDeviceHashedIds = _config.testDeviceIds,
                };
            }

            ConsentInformation.Update(parameters, updateError =>
            {
                if (updateError != null)
                {
                    OnMain(done, updateError.Message);
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(done, formError?.Message));
            });
        }

        public void ShowPrivacyOptions(Action<string> done) =>
            ConsentForm.ShowPrivacyOptionsForm(formError => OnMain(done, formError?.Message));

        public void Reset() => ConsentInformation.Reset();

        private static void OnMain(Action<string> done, string error) =>
            MobileAdsEventExecutor.ExecuteInUpdate(() => done?.Invoke(error));
    }
}
