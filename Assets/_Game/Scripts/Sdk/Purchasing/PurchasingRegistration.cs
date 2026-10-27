using UnityEngine;
using UtsavNights.Gameplay;

namespace UtsavNights.Purchasing
{
    /// <summary>On a device, makes Google Play Billing the purchase provider (see <see cref="MonetisationProviders"/>).</summary>
    internal static class PurchasingRegistration
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register() => MonetisationProviders.Purchases = () => new UnityIapBackend();
#endif
    }
}
