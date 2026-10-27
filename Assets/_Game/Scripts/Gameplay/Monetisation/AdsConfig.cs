using System.Collections.Generic;
using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// AdMob identifiers (Resources/AdsConfig). Ships with Google's public test IDs, which always serve
    /// test ads and are safe to tap. Replace with the owner's live IDs only for the closed-test build
    /// (Phase 4 exit gate); the build script warns while test IDs are in a release build.
    /// </summary>
    [CreateAssetMenu(menuName = "Utsav/Ads Config", fileName = "AdsConfig")]
    public sealed class AdsConfig : ScriptableObject
    {
        public const string ResourceName = "AdsConfig";
        public const string TestAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string TestRewardedId = "ca-app-pub-3940256099942544/5224354917";
        public const string TestInterstitialId = "ca-app-pub-3940256099942544/1033173712";

        [Tooltip("AdMob Android app ID (ca-app-pub-...~...). Copied into the Google Mobile Ads settings at build.")]
        public string androidAppId = TestAppId;

        public string rewardedUnitId = TestRewardedId;

        public string interstitialUnitId = TestInterstitialId;

        [Tooltip("Hashed device ids that get test ads and UMP debug geography (logged by the SDKs on first run).")]
        public List<string> testDeviceIds = new List<string>();

        [Tooltip("UMP debug geography for test devices: pretend to be in the EEA to test the consent form.")]
        public bool debugGeographyEea;

        public bool UsesTestIds => androidAppId == TestAppId || rewardedUnitId == TestRewardedId || interstitialUnitId == TestInterstitialId;

        public static AdsConfig Load()
        {
            var config = Resources.Load<AdsConfig>(ResourceName);
            return config != null ? config : CreateInstance<AdsConfig>();
        }
    }
}
