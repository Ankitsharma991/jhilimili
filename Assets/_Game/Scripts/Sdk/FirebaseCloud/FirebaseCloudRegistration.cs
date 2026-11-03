using UnityEngine;
using UtsavNights.Gameplay;

namespace UtsavNights.FirebaseCloud
{
    /// <summary>
    /// On a device, makes Firebase the game's cloud (Phase 10 step 6) before the first scene loads. The editor and tests
    /// keep the memory cloud (see CloudInstaller).
    /// </summary>
    internal static class FirebaseCloudRegistration
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register() => MonetisationProviders.Cloud = () => new FirebaseCloud();
#endif
    }
}
