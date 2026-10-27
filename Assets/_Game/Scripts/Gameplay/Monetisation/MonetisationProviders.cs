using System;
using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Where SDK assemblies plug in. UtsavNights.Ads (AdMob and UMP), UtsavNights.Purchasing (Unity IAP)
    /// and UtsavNights.Firebase compile only when their package is installed, and on a device they set
    /// these factories before the first scene loads. When a factory is null (editor, tests, or the
    /// package not installed) the installers use the development providers, so game code never names an SDK.
    /// </summary>
    public static class MonetisationProviders
    {
        public static Func<IAdBackend> Ads;
        public static Func<IConsentBackend> Consent;
        public static Func<IPurchaseBackend> Purchases;
        public static Func<IAnalyticsBackend> Analytics;
        public static Func<ICrashReporter> Crashes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Ads = null;
            Consent = null;
            Purchases = null;
            Analytics = null;
            Crashes = null;
        }
    }
}
