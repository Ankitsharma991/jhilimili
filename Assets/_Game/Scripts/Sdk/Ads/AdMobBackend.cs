using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;
using UtsavNights.Core.Monetisation;
using UtsavNights.Gameplay;

namespace UtsavNights.Ads
{
    /// <summary>
    /// Google Mobile Ads (AdMob) provider for <see cref="AdService"/>. The SDK starts on the first load,
    /// which <see cref="AdService.Preload"/> only requests after consent allows ads (PRD 15.2). One
    /// rewarded and one interstitial ad are kept loaded. SDK callbacks may arrive on another thread, so
    /// every one is moved to the main thread with <see cref="MobileAdsEventExecutor.ExecuteInUpdate"/>.
    /// </summary>
    public sealed class AdMobBackend : IAdBackend
    {
        private readonly AdsConfig _config;
        private bool _started;
        private bool _initialized;
        private bool _wantRewarded;
        private bool _wantInterstitial;
        private bool _loadingRewarded;
        private bool _loadingInterstitial;
        private RewardedAd _rewarded;
        private InterstitialAd _interstitial;

        public AdMobBackend(AdsConfig config)
        {
            _config = config;
            MobileAds.SetRequestConfiguration(new RequestConfiguration { TestDeviceIds = config.testDeviceIds });
        }

        public string Name => _config.UsesTestIds ? "admob (test ids)" : "admob";

        public bool IsReady(AdKind kind) =>
            kind == AdKind.Rewarded ? _rewarded != null && _rewarded.CanShowAd() : _interstitial != null && _interstitial.CanShowAd();

        public void Load(AdKind kind)
        {
            if (kind == AdKind.Rewarded)
            {
                _wantRewarded = true;
            }
            else
            {
                _wantInterstitial = true;
            }

            if (!_initialized)
            {
                Start();
                return;
            }

            if (kind == AdKind.Rewarded)
            {
                LoadRewarded();
            }
            else
            {
                LoadInterstitial();
            }
        }

        public void Show(AdKind kind, string placement, Action<AdOutcome> done)
        {
            if (!IsReady(kind))
            {
                done?.Invoke(AdOutcome.Failed);
                return;
            }

            bool finished = false;
            void Finish(AdOutcome outcome)
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                done?.Invoke(outcome);
            }

            if (kind == AdKind.Rewarded)
            {
                RewardedAd ad = _rewarded;
                _rewarded = null;
                bool earned = false;
                ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    // Queued after the reward callback when both arrive together, so the reward is never lost.
                    MobileAdsEventExecutor.ExecuteInUpdate(() => Finish(earned ? AdOutcome.Rewarded : AdOutcome.Closed));
                    ad.Destroy();
                });
                ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    Debug.LogWarning($"[Ads] rewarded failed to show: {error?.GetMessage()}");
                    Finish(AdOutcome.Failed);
                    ad.Destroy();
                });
                ad.Show(reward => MobileAdsEventExecutor.ExecuteInUpdate(() => earned = true));
            }
            else
            {
                InterstitialAd ad = _interstitial;
                _interstitial = null;
                ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    Finish(AdOutcome.Closed);
                    ad.Destroy();
                });
                ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    Debug.LogWarning($"[Ads] interstitial failed to show: {error?.GetMessage()}");
                    Finish(AdOutcome.Failed);
                    ad.Destroy();
                });
                ad.Show();
            }
        }

        private void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            MobileAds.Initialize(status => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _initialized = status != null;
                if (!_initialized)
                {
                    Debug.LogWarning("[Ads] Google Mobile Ads failed to initialise; the game continues without ads");
                    _started = false;
                    return;
                }

                if (_wantRewarded)
                {
                    LoadRewarded();
                }

                if (_wantInterstitial)
                {
                    LoadInterstitial();
                }
            }));
        }

        private void LoadRewarded()
        {
            if (_loadingRewarded || IsReady(AdKind.Rewarded))
            {
                return;
            }

            _loadingRewarded = true;
            RewardedAd.Load(_config.rewardedUnitId, new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _loadingRewarded = false;
                if (error != null || ad == null)
                {
                    Debug.Log($"[Ads] rewarded not loaded: {error?.GetMessage()}");
                    return;
                }

                _rewarded = ad;
            }));
        }

        private void LoadInterstitial()
        {
            if (_loadingInterstitial || IsReady(AdKind.Interstitial))
            {
                return;
            }

            _loadingInterstitial = true;
            InterstitialAd.Load(_config.interstitialUnitId, new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    Debug.Log($"[Ads] interstitial not loaded: {error?.GetMessage()}");
                    return;
                }

                _interstitial = ad;
            }));
        }
    }
}
