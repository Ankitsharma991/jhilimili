using UnityEngine;
using UtsavNights.Gameplay;

namespace UtsavNights.FirebaseSdk
{
    /// <summary>
    /// On a device, makes Firebase the analytics and crash provider before the first scene loads. The editor and
    /// tests keep the local development log (see <see cref="MonetisationProviders"/>).
    /// </summary>
    internal static class FirebaseRegistration
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            MonetisationProviders.Analytics = () => new FirebaseAnalyticsBackend();
            MonetisationProviders.Crashes = () => new FirebaseCrashReporter();
        }
#endif
    }
}
