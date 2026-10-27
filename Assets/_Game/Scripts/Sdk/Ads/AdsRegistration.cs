using UnityEngine;
using UtsavNights.Gameplay;

namespace UtsavNights.Ads
{
    /// <summary>
    /// On a device, makes AdMob and UMP the ad and consent providers before the first scene loads. The
    /// editor and tests keep the development providers (see <see cref="MonetisationProviders"/>).
    /// </summary>
    internal static class AdsRegistration
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            AdsConfig config = AdsConfig.Load();
            MonetisationProviders.Ads = () => new AdMobBackend(config);
            MonetisationProviders.Consent = () => new UmpConsentBackend(config);
            if (config.UsesTestIds)
            {
                Debug.Log("[Ads] using Google test ad ids");
            }
        }
#endif
    }
}
