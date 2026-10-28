using System;
using UnityEngine;
using UtsavNights.Core.Engagement;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Asks for a Google Play rating once the session has gone well (Phase 5 step 4, <see cref="ReviewPolicy"/>).
    /// Checks once a second; the busy state is the same one the ad rules use, plus any open sheet.
    /// Counts this launch as a session when created.
    /// </summary>
    public sealed class ReviewService : ITickable, IDisposable
    {
        private const float CheckSeconds = 1f;

        private readonly ReviewStore _store;
        private readonly AdService _ads;
        private readonly Func<float> _clock;
        private readonly Func<long> _unixNow;
        private FinaleController _finale;
        private FinaleController.FinaleState _finaleState;
        private float _sinceCheck;
        private float _lastPayoff = float.NegativeInfinity;
        private float _lastAd = float.NegativeInfinity;

        public ReviewService(IReviewBackend backend, ReviewStore store, AdService ads, Func<float> clock, Func<long> unixNow)
        {
            Backend = backend;
            _store = store;
            _ads = ads;
            _clock = clock;
            _unixNow = unixNow;
            State = store.Load();
            ReviewPolicy.StartSession(State);
            store.Save(State);
            Game.Events.Subscribe<PayoffCompleteEvent>(OnPayoff);
            Game.Events.Subscribe<AdEvent>(OnAd);
        }

        public IReviewBackend Backend { get; }

        public ReviewState State { get; private set; }

        public bool AskedThisSession { get; private set; }

        public int Payoffs { get; private set; }

        public int FinalesCompleted { get; private set; }

        public ReviewBlock LastBlock { get; private set; } = ReviewBlock.FirstSession;

        private float Now => _clock();

        public void Dispose()
        {
            Game.Events.Unsubscribe<PayoffCompleteEvent>(OnPayoff);
            Game.Events.Unsubscribe<AdEvent>(OnAd);
            if (_finale != null)
            {
                _finale.StateChanged -= OnFinaleState;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_finale == null && Game.Services.TryGet(out _finale))
            {
                _finale.StateChanged += OnFinaleState;
                _finaleState = _finale.State;
            }

            _sinceCheck += deltaSeconds;
            if (AskedThisSession || _sinceCheck < CheckSeconds)
            {
                return;
            }

            _sinceCheck = 0f;
            LastBlock = ReviewPolicy.Check(State, Moment());
            if (LastBlock == ReviewBlock.None)
            {
                Ask(FinalesCompleted > 0 ? "finale" : "payoffs");
            }
        }

        /// <summary>Developer overlay: show the prompt now, ignoring the rules and without recording it.</summary>
        public void AskNowForTesting() => Request("developer");

        /// <summary>Developer overlay: forget earlier prompts, as on a second launch of a fresh install.</summary>
        public void ResetForTesting()
        {
            State = new ReviewState { sessions = ReviewPolicy.MinSessions };
            _store.Save(State);
            AskedThisSession = false;
        }

        private ReviewMoment Moment()
        {
            AdScene scene = _ads.CurrentScene();
            bool sheetOpen = InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            return new ReviewMoment
            {
                NowUnix = _unixNow(),
                AskedThisSession = AskedThisSession,
                FinalesCompleted = FinalesCompleted,
                Payoffs = Payoffs,
                SecondsSinceAd = Now - _lastAd,
                SecondsSincePayoff = Now - _lastPayoff,
                SecondsSinceTouch = scene.SecondsSinceTouch,
                Busy = scene.CrackerActive || scene.Placing || scene.FinaleRunning || _ads.IsShowing || sheetOpen,
            };
        }

        private void Ask(string trigger)
        {
            AskedThisSession = true;
            ReviewPolicy.RecordPrompt(State, _unixNow());
            _store.Save(State);
            Request(trigger);
        }

        private void Request(string trigger)
        {
            Debug.Log($"[Review] asking ({trigger}) via {Backend.Name}");
            Backend.Request(ran => Game.Events.Publish(new ReviewPromptEvent(Backend.Name, ran, trigger)));
        }

        private void OnFinaleState(FinaleController.FinaleState state)
        {
            if (_finaleState == FinaleController.FinaleState.Running && state == FinaleController.FinaleState.Idle)
            {
                FinalesCompleted++;
            }

            _finaleState = state;
        }

        private void OnPayoff(PayoffCompleteEvent e)
        {
            Payoffs++;
            _lastPayoff = Now;
        }

        private void OnAd(AdEvent e)
        {
            if (e.Type != "ad_blocked")
            {
                _lastAd = Now;
            }
        }
    }
}
