using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core;
using UtsavNights.Core.Analytics;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using UtsavNights.Core.Settings;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// PRD 17 events (F12), built from the game's own events, checked against <see cref="AnalyticsSchema"/>
    /// and sent only with consent and the "Share usage statistics" switch on. Events before the consent
    /// check are held, then sent or dropped. Settings changes are reported once they settle; performance
    /// is sampled once a minute, at most five times a session.
    /// </summary>
    public sealed class AnalyticsService : ITickable
    {
        public const string FirstOpenKey = "utsav.analytics.first_open";
        public const float PerfIntervalSeconds = 60f;
        public const int MaxPerfSamples = 5;
        public const float SettingsSettleSeconds = 2f;

        private readonly AnalyticsGate _gate = new AnalyticsGate();
        private readonly RollingStats _frames = new RollingStats(4096);
        private float _sinceSample;
        private int _samples;
        private int _ignitions;
        private float _sessionStart;
        private GameSettings _reportedSettings;
        private GameSettings _pendingSettings;
        private float _settingsSettle = -1f;
        private bool _sessionEnded;

        public AnalyticsService(IAnalyticsBackend backend, ICrashReporter crashes)
        {
            Backend = backend;
            Crashes = crashes;
            _sessionStart = Now;
            if (Game.Services.TryGet(out SettingsService settings))
            {
                _reportedSettings = settings.Current;
            }

            Game.Events.Subscribe<ConsentChangedEvent>(OnConsent);
            Game.Events.Subscribe<SceneReadyEvent>(OnSceneReady);
            Game.Events.Subscribe<CrackerPlacedEvent>(OnPlaced);
            Game.Events.Subscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Subscribe<PayoffCompleteEvent>(OnPayoff);
            Game.Events.Subscribe<AdEvent>(OnAd);
            Game.Events.Subscribe<PurchaseEvent>(OnPurchase);
            Game.Events.Subscribe<SettingsChangedEvent>(OnSettings);
            Game.Events.Subscribe<AppPausedEvent>(OnPaused);
            Game.Events.Subscribe<AvatarCreatorOpenedEvent>(OnCreatorOpened);
            Game.Events.Subscribe<AvatarSavedEvent>(OnAvatarSaved);
            Game.Events.Subscribe<AvatarSkippedEvent>(OnAvatarSkipped);
            Game.Events.Subscribe<DecorModeOpenedEvent>(OnDecorOpened);
            Game.Events.Subscribe<DecorModeClosedEvent>(OnDecorClosed);
            Game.Events.Subscribe<DecorationPlacedEvent>(OnDecorationPlaced);
            Game.Events.Subscribe<HouseModuleChangedEvent>(OnModuleChanged);
            Game.Events.Subscribe<HouseLayoutEvent>(OnLayout);
            Game.Events.Subscribe<WalkStartedEvent>(OnWalkStarted);
            Game.Events.Subscribe<WalkEndedEvent>(OnWalkEnded);
            Game.Events.Subscribe<FastTravelEvent>(OnFastTravel);
            Game.Events.Subscribe<SpotChosenEvent>(OnSpotChosen);
            Game.Events.Subscribe<EnvironmentChosenEvent>(OnEnvironmentChosen);
            Game.Events.Subscribe<PackInstalledEvent>(OnPackInstalled);
            Game.Events.Subscribe<PackFailedEvent>(OnPackFailed);
            Game.Events.Subscribe<PackDeletedEvent>(OnPackDeleted);
            Game.Events.Subscribe<ShowSavedEvent>(OnShowSaved);
            Game.Events.Subscribe<ShowPlayedEvent>(OnShowPlayed);
            Game.Events.Subscribe<ClipRecordedEvent>(OnClipRecorded);
            Game.Events.Subscribe<ClipSharedEvent>(OnClipShared);
        }

        private bool _reportedRestore;
        private int _walkSeconds;

        public IAnalyticsBackend Backend { get; }

        public ICrashReporter Crashes { get; }

        public AnalyticsGate.State ConsentState => _gate.Current;

        public int HeldEvents => _gate.HeldCount;

        /// <summary>Events rejected by the schema (a programming error; should stay 0).</summary>
        public int Rejected { get; private set; }

        private static float Now => (float)Time.realtimeSinceStartupAsDouble;

        public void Dispose()
        {
            Game.Events.Unsubscribe<ConsentChangedEvent>(OnConsent);
            Game.Events.Unsubscribe<SceneReadyEvent>(OnSceneReady);
            Game.Events.Unsubscribe<CrackerPlacedEvent>(OnPlaced);
            Game.Events.Unsubscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Unsubscribe<PayoffCompleteEvent>(OnPayoff);
            Game.Events.Unsubscribe<AdEvent>(OnAd);
            Game.Events.Unsubscribe<PurchaseEvent>(OnPurchase);
            Game.Events.Unsubscribe<SettingsChangedEvent>(OnSettings);
            Game.Events.Unsubscribe<AppPausedEvent>(OnPaused);
            Game.Events.Unsubscribe<AvatarCreatorOpenedEvent>(OnCreatorOpened);
            Game.Events.Unsubscribe<AvatarSavedEvent>(OnAvatarSaved);
            Game.Events.Unsubscribe<AvatarSkippedEvent>(OnAvatarSkipped);
            Game.Events.Unsubscribe<DecorModeOpenedEvent>(OnDecorOpened);
            Game.Events.Unsubscribe<DecorModeClosedEvent>(OnDecorClosed);
            Game.Events.Unsubscribe<DecorationPlacedEvent>(OnDecorationPlaced);
            Game.Events.Unsubscribe<HouseModuleChangedEvent>(OnModuleChanged);
            Game.Events.Unsubscribe<HouseLayoutEvent>(OnLayout);
            Game.Events.Unsubscribe<WalkStartedEvent>(OnWalkStarted);
            Game.Events.Unsubscribe<WalkEndedEvent>(OnWalkEnded);
            Game.Events.Unsubscribe<FastTravelEvent>(OnFastTravel);
            Game.Events.Unsubscribe<SpotChosenEvent>(OnSpotChosen);
            Game.Events.Unsubscribe<EnvironmentChosenEvent>(OnEnvironmentChosen);
            Game.Events.Unsubscribe<PackInstalledEvent>(OnPackInstalled);
            Game.Events.Unsubscribe<PackFailedEvent>(OnPackFailed);
            Game.Events.Unsubscribe<PackDeletedEvent>(OnPackDeleted);
            Game.Events.Unsubscribe<ShowSavedEvent>(OnShowSaved);
            Game.Events.Unsubscribe<ShowPlayedEvent>(OnShowPlayed);
            Game.Events.Unsubscribe<ClipRecordedEvent>(OnClipRecorded);
            Game.Events.Unsubscribe<ClipSharedEvent>(OnClipShared);
        }

        /// <summary>app_first_open once per install (PRD 17: install quality).</summary>
        public void ReportFirstOpen(int tier)
        {
            if (PlayerPrefs.GetInt(FirstOpenKey, 0) == 1)
            {
                return;
            }

            PlayerPrefs.SetInt(FirstOpenKey, 1);
            PlayerPrefs.Save();
            Send("app_first_open", new EventParams().Add("tier", TierName(tier)).Add("locale", Application.systemLanguage.ToString().ToLowerInvariant()));
        }

        /// <summary>Applies the consent decision directly (also called from <see cref="ConsentChangedEvent"/>).</summary>
        public void SetAllowed(bool allowed)
        {
            Backend.SetCollectionEnabled(allowed);
            Crashes?.SetCollectionEnabled(allowed);
            foreach (KeyValuePair<string, EventParams> held in _gate.Decide(allowed))
            {
                Backend.Log(held.Key, held.Value);
            }
        }

        /// <summary>session_end on quit or background (PRD 17: duration_s, ignitions).</summary>
        public void EndSession()
        {
            if (_sessionEnded)
            {
                return;
            }

            _sessionEnded = true;
            FlushSettings();
            bool hasAvatar = Game.Services.TryGet(out ProfileService profiles) && profiles.Current.created;
            long decorations = Game.Services.TryGet(out HomeService home) ? home.Design.decorations.Count : 0L;
            string environment = Game.Services.TryGet(out SettlementView settlement) ? settlement.Layout.environment : "none";
            Send("session_end", new EventParams().Add("duration_s", (long)(Now - _sessionStart)).Add("ignitions", (long)_ignitions).Add("has_avatar", hasAvatar)
                .Add("decorations", decorations).Add("environment", environment).Add("walk_s", (long)_walkSeconds));
        }

        public void Tick(float deltaSeconds)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f && dt < 1f)
            {
                _frames.Add(dt * 1000f);
            }

            if (_settingsSettle >= 0f)
            {
                _settingsSettle -= dt;
                if (_settingsSettle < 0f)
                {
                    FlushSettings();
                }
            }

            if (_samples >= MaxPerfSamples)
            {
                return;
            }

            _sinceSample += dt;
            if (_sinceSample >= PerfIntervalSeconds)
            {
                _sinceSample = 0f;
                _samples++;
                int tier = 0;
                int downgrades = 0;
                if (Game.Services.TryGet(out QualityManager quality))
                {
                    tier = quality.Tier;
                    downgrades = quality.Governor != null ? quality.Governor.StepsApplied : 0;
                }

                Send("perf_sample", new EventParams()
                    .Add("avg_fps", System.Math.Round(SpikeMath.FpsFromFrameMs(_frames.Average), 1))
                    .Add("tier", TierName(tier))
                    .Add("downgrades", (long)downgrades));
                _frames.Clear();
            }
        }

        private void Send(string eventName, EventParams parameters)
        {
            List<string> errors = AnalyticsSchema.Validate(eventName, parameters);
            if (errors.Count > 0)
            {
                Rejected++;
                Debug.LogWarning("[Analytics] dropped: " + string.Join("; ", errors));
                return;
            }

            if (_gate.Offer(eventName, parameters))
            {
                Backend.Log(eventName, parameters);
            }
        }

        private void FlushSettings()
        {
            _settingsSettle = -1f;
            if (_pendingSettings == null)
            {
                return;
            }

            if (_reportedSettings != null)
            {
                foreach (KeyValuePair<string, string> change in SettingsDiff.Changes(_reportedSettings, _pendingSettings))
                {
                    Send("settings_changed", new EventParams().Add("key", change.Key).Add("value", change.Value));
                }
            }

            _reportedSettings = _pendingSettings;
            _pendingSettings = null;
        }

        private static string TierName(int tier) => tier <= 0 ? "low" : tier == 1 ? "mid" : "high";

        private void OnConsent(ConsentChangedEvent e) => Apply(e.Status);

        /// <summary>
        /// While a consent choice is pending, events are held (not dropped) so the first-open and launch
        /// events still arrive once the player agrees; the usage-statistics switch off always drops.
        /// </summary>
        public void Apply(Core.Monetisation.ConsentStatus status)
        {
            bool share = !Game.Services.TryGet(out SettingsService settings) || settings.Current.shareUsageStats;
            bool pending = status == Core.Monetisation.ConsentStatus.Required || status == Core.Monetisation.ConsentStatus.Unknown;
            if (share && pending)
            {
                Backend.SetCollectionEnabled(false);
                Crashes?.SetCollectionEnabled(false);
                _gate.Hold();
                return;
            }

            SetAllowed(Core.Monetisation.ConsentRules.AnalyticsAllowed(share, status));
        }

        private void OnSceneReady(SceneReadyEvent e)
        {
            Send("scene_ready", new EventParams().Add("ms_since_launch", (long)(e.SecondsSinceLaunch * 1000f)));

            // An interrupted decorating session that the journal brought back (once per scene).
            if (Game.Services.TryGet(out HomeService home) && home.Recovered > 0 && !_reportedRestore)
            {
                _reportedRestore = true;
                Send("house_restored", new EventParams().Add("lines", (long)home.Recovered));
            }
        }

        private void OnDecorOpened(DecorModeOpenedEvent e) => Send("decor_mode_opened", new EventParams().Add("decorations", (long)e.Decorations));

        private void OnDecorClosed(DecorModeClosedEvent e) =>
            Send("decor_mode_closed", new EventParams().Add("edits", (long)e.Edits).Add("saved", e.Saved).Add("decorations", (long)e.Decorations));

        private void OnDecorationPlaced(DecorationPlacedEvent e) =>
            Send("decoration_placed", new EventParams().Add("id", e.Item).Add("category", e.Category.ToString().ToLowerInvariant()));

        private void OnModuleChanged(HouseModuleChangedEvent e) => Send("house_module_changed", new EventParams().Add("point", e.Point).Add("module", e.Module));

        private void OnWalkStarted(WalkStartedEvent e) => Send("walk_started", new EventParams().Add("environment", e.Environment));

        private void OnWalkEnded(WalkEndedEvent e)
        {
            _walkSeconds += e.Seconds;
            Send("walk_ended", new EventParams().Add("seconds", (long)e.Seconds).Add("metres", (long)e.Metres));
        }

        private void OnFastTravel(FastTravelEvent e) => Send("fast_travel", new EventParams().Add("spot", e.Spot));

        private void OnSpotChosen(SpotChosenEvent e) =>
            Send("spot_chosen", new EventParams().Add("spot", e.Spot).Add("kind", e.Kind).Add("environment", e.Environment));

        private void OnEnvironmentChosen(EnvironmentChosenEvent e) => Send("environment_chosen", new EventParams().Add("environment", e.Environment));

        private void OnPackInstalled(PackInstalledEvent e) =>
            Send("pack_installed", new EventParams().Add("pack", e.PackId).Add("version", e.Version).Add("type", e.Type));

        private void OnPackFailed(PackFailedEvent e) => Send("pack_failed", new EventParams().Add("pack", e.PackId).Add("reason", e.Reason));

        private void OnPackDeleted(PackDeletedEvent e) => Send("pack_deleted", new EventParams().Add("pack", e.PackId));

        private void OnShowSaved(ShowSavedEvent e) =>
            Send("show_saved", new EventParams().Add("pads", (long)e.Pads).Add("cues", (long)e.Cues).Add("seconds", (long)e.Seconds));

        private void OnShowPlayed(ShowPlayedEvent e) =>
            Send("show_played", new EventParams().Add("cues", (long)e.Cues).Add("seconds", (long)e.Seconds).Add("replay", e.Replay));

        private void OnClipRecorded(ClipRecordedEvent e)
        {
            if (e.Ok)
            {
                Send("clip_recorded", new EventParams().Add("seconds", (long)e.Seconds).Add("source", e.Source).Add("frames", (long)e.Frames));
            }
            else
            {
                Send("clip_failed", new EventParams().Add("reason", e.Reason).Add("source", e.Source));
            }
        }

        private void OnClipShared(ClipSharedEvent e) => Send("clip_shared", new EventParams().Add("target", e.Target.ToString().ToLowerInvariant())
            .Add("seconds", (long)e.Seconds).Add("source", e.Source).Add("fell_back", e.FellBack));

        private void OnLayout(HouseLayoutEvent e) => Send(e.Saved ? "layout_saved" : "layout_loaded", new EventParams().Add("slot", (long)e.Slot));

        private void OnCreatorOpened(AvatarCreatorOpenedEvent e) => Send("avatar_creator_opened", new EventParams().Add("first", e.First));

        private void OnAvatarSaved(AvatarSavedEvent e) => Send("avatar_saved", new EventParams().Add("from_preset", e.FromPreset).Add("garments", (long)e.Garments)
            .Add("top", e.Top).Add("bottom", e.Bottom).Add("full", e.Full).Add("headwear", e.Headwear));

        private void OnAvatarSkipped(AvatarSkippedEvent e) => Send("avatar_skipped", new EventParams());

        private void OnPlaced(CrackerPlacedEvent e) => Send("cracker_placed", new EventParams().Add("id", e.CrackerId));

        private void OnIgnited(CrackerIgnitedEvent e)
        {
            _ignitions++;
            Send("cracker_ignited", new EventParams().Add("id", e.CrackerId).Add("is_first", e.IsFirst));
        }

        private void OnPayoff(PayoffCompleteEvent e) => Send("payoff_complete", new EventParams().Add("id", e.CrackerId));

        private void OnAd(AdEvent e)
        {
            // ad_blocked and ad_closed stay in the local playtest log; PRD 17 lists these three.
            if (e.Type == "ad_shown" || e.Type == "ad_failed" || e.Type == "ad_reward")
            {
                Send(e.Type, new EventParams().Add("placement", e.Placement).Add("ms_since_payoff", (long)e.MsSincePayoff));
            }
        }

        private void OnPurchase(PurchaseEvent e)
        {
            if (e.Type == "purchase_started" || e.Type == "purchase_done")
            {
                Send(e.Type, new EventParams().Add("sku", e.Sku));
            }
        }

        private void OnSettings(SettingsChangedEvent e)
        {
            _pendingSettings = e.Settings;
            _settingsSettle = SettingsSettleSeconds;
        }

        private void OnPaused(AppPausedEvent e)
        {
            if (e.Paused)
            {
                EndSession();
            }
            else
            {
                // Coming back starts a new session (Android may have kept the process alive).
                _sessionEnded = false;
                _sessionStart = Now;
                _ignitions = 0;
            }
        }
    }
}
