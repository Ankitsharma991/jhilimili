using UnityEngine;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Creates the <see cref="ProgressService"/> (progress.json) and the <see cref="AdService"/> with the
    /// ad network for this build: AdMob when UTSAV_ADMOB is defined, otherwise the development card.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public sealed class MonetisationInstaller : MonoBehaviour
    {
        [Tooltip("Interstitials at natural breaks (PRD 15.1). Off for R0 until a closed test compares with and without.")]
        [SerializeField] private bool interstitialsEnabled = false;

        private AdService _ads;
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
            Debug.Log($"[Ads] backend={backend.Name} interstitials={interstitialsEnabled} adsRemoved={progress.AdsRemoved}");
        }

        private void OnDestroy()
        {
            if (!_owner)
            {
                return;
            }

            Game.Loop.Remove(_ads);
            _ads.Dispose();
            Game.Services.Unregister<AdService>();
            Game.Services.Unregister<ProgressService>();
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
