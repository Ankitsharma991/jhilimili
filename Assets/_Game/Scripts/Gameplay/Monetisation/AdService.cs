using System;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// The one place ads are shown from (PRD 15.2 ad service wrapper). Every request goes through
    /// <see cref="AdPolicy"/> with the live scene state; ads are preloaded; failures are silent and
    /// never block play. Publishes an <see cref="AdEvent"/> for every show, reward, close, failure and
    /// refused request.
    /// </summary>
    public sealed class AdService : ITickable
    {
        private const float InterstitialCheckSeconds = 1f;

        private readonly Func<float> _clock;
        private float _sinceCheck;
        private float _lastTouch = float.NegativeInfinity;

        public AdService(IAdBackend backend, Func<float> clock)
        {
            Backend = backend;
            _clock = clock;
            Policy = new AdPolicy(clock());
            Game.Events.Subscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Subscribe<PayoffCompleteEvent>(OnPayoff);
            Backend.Load(AdKind.Rewarded);
            Backend.Load(AdKind.Interstitial);
        }

        public IAdBackend Backend { get; }

        public AdPolicy Policy { get; }

        public bool IsShowing { get; private set; }

        private float Now => _clock();

        public void Dispose()
        {
            Game.Events.Unsubscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Unsubscribe<PayoffCompleteEvent>(OnPayoff);
        }

        public AdBlock Check(AdKind kind) => IsShowing ? AdBlock.FeatureOff : Policy.Check(kind, Now, Scene());

        /// <summary>The player asked for a rewarded ad. <c>done</c> gets the outcome and, when refused, a message for the player.</summary>
        public void ShowRewarded(string placement, Action<AdOutcome, string> done)
        {
            AdBlock block = Check(AdKind.Rewarded);
            if (block != AdBlock.None)
            {
                Publish("ad_blocked", AdKind.Rewarded, placement, block);
                done?.Invoke(AdOutcome.Blocked, AdPolicy.Message(block));
                return;
            }

            if (!Backend.IsReady(AdKind.Rewarded))
            {
                Backend.Load(AdKind.Rewarded);
                Publish("ad_failed", AdKind.Rewarded, placement);
                done?.Invoke(AdOutcome.Failed, "No ad available right now. Try again in a moment.");
                return;
            }

            Show(AdKind.Rewarded, placement, outcome =>
                done?.Invoke(outcome, outcome == AdOutcome.Failed ? "No ad available right now. Try again in a moment." : string.Empty));
        }

        /// <summary>Shows an interstitial only if every rule allows it now. Returns true when one started.</summary>
        public bool MaybeShowInterstitial(string trigger)
        {
            if (Check(AdKind.Interstitial) != AdBlock.None || !Backend.IsReady(AdKind.Interstitial))
            {
                return false;
            }

            Show(AdKind.Interstitial, trigger, null);
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (ETouch.activeTouches.Count > 0)
            {
                _lastTouch = Now;
            }

            if (!Policy.InterstitialsEnabled || IsShowing)
            {
                return;
            }

            _sinceCheck += deltaSeconds;
            if (_sinceCheck >= InterstitialCheckSeconds)
            {
                _sinceCheck = 0f;
                MaybeShowInterstitial("natural_break");
            }
        }

        private void Show(AdKind kind, string placement, Action<AdOutcome> done)
        {
            IsShowing = true;
            Publish("ad_shown", kind, placement);
            Policy.RecordAdShown(Now);
            Backend.Show(kind, placement, outcome =>
            {
                IsShowing = false;
                Publish(outcome == AdOutcome.Rewarded ? "ad_reward" : outcome == AdOutcome.Failed ? "ad_failed" : "ad_closed", kind, placement);
                Backend.Load(kind);
                done?.Invoke(outcome);
            });
        }

        private AdScene Scene()
        {
            bool active = false;
            if (Game.Services.TryGet(out CrackerSpawner spawner))
            {
                foreach (CrackerRuntime runtime in spawner.Active)
                {
                    if (runtime != null && runtime.IsBurning)
                    {
                        active = true;
                        break;
                    }
                }
            }

            bool placing = Game.Services.TryGet(out PlacementController placement) && placement.IsPlacing;
            bool finale = Game.Services.TryGet(out FinaleController f) && f.State == FinaleController.FinaleState.Running;
            return new AdScene(active, placing, finale, Now - _lastTouch);
        }

        private void Publish(string type, AdKind kind, string placement, AdBlock block = AdBlock.None)
        {
            float since = Policy.SecondsSincePayoff(Now);
            Game.Events.Publish(new AdEvent(type, kind, placement, since < 0f ? -1 : (int)(since * 1000f), block));
        }

        private void OnIgnited(CrackerIgnitedEvent e) => Policy.RecordIgnition();

        private void OnPayoff(PayoffCompleteEvent e) => Policy.RecordPayoff(Now);
    }
}
