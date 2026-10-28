using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UtsavNights.Audio;
using UtsavNights.Core.Diagnostics;
using UtsavNights.Core.Quality;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using UtsavNights.Effects;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Local playtest log (Phase 2 step 12) until Firebase analytics arrives in Phase 4. Writes one JSON
    /// line per event to persistentDataPath/playtest_sessions.jsonl and to logcat with the tag
    /// [UTSAV_PLAY]: session start, scene ready, ignitions (with time to first ignition), payoffs,
    /// flame-outs, quality changes, a performance sample every 30 s, pauses and the session end.
    /// No personal data: device model, tier and timings only. Pull with tools/pull_playtest.sh.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class PlaytestRecorder : MonoBehaviour, ITickable
    {
        public const string LogTag = "[UTSAV_PLAY]";
        public const string FileName = "playtest_sessions.jsonl";

        private readonly StringBuilder _sb = new StringBuilder(256);
        private PlaytestSession _session;
        private string _sessionId;
        private ProfilerRecorder _memory;

        // Managed allocations per frame over the last 64 frames: steady play should allocate next to nothing (GC spikes).
        private ProfilerRecorder _gcPerFrame;
        private PlacementController _placement;
        private FinaleController _finale;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public PlaytestSession Session => _session;

        private static float Now => (float)Time.realtimeSinceStartupAsDouble;

        private void Awake()
        {
            _session = new PlaytestSession(Now);
            _sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            Game.Events.Subscribe<SceneReadyEvent>(OnSceneReady);
            Game.Events.Subscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Subscribe<PayoffCompleteEvent>(OnPayoff);
            Game.Events.Subscribe<FlameOutEvent>(OnFlameOut);
            Game.Events.Subscribe<QualityChangedEvent>(OnQualityChanged);
            Game.Events.Subscribe<AppPausedEvent>(OnAppPaused);
            Game.Events.Subscribe<AdEvent>(OnAd);
            Game.Events.Subscribe<PurchaseEvent>(OnPurchase);
            Game.Events.Subscribe<ReviewPromptEvent>(OnReviewPrompt);

            Begin("session_start");
            Field("device", SystemInfo.deviceModel);
            Field("os", SystemInfo.operatingSystem);
            Field("ram_mb", SystemInfo.systemMemorySize);
            Field("gpu", SystemInfo.graphicsDeviceName);
            Field("app", Application.version);
            Emit();
        }

        private void Start()
        {
            if (Game.Services.TryGet(out _placement))
            {
                _placement.SelectionChanged += OnSelectionChanged;
                _placement.Placed += OnPlaced;
                _placement.Rejected += OnRejected;
            }

            if (Game.Services.TryGet(out _finale))
            {
                _finale.StateChanged += OnFinaleState;
            }
        }

        private void OnEnable()
        {
            Game.Loop.Add(this);
            _memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _gcPerFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 64);
        }

        private void OnDisable()
        {
            Game.Loop.Remove(this);
            _memory.Dispose();
            _gcPerFrame.Dispose();
        }

        private static double AverageKb(ProfilerRecorder recorder)
        {
            long sum = 0;
            for (int i = 0; i < recorder.Count; i++)
            {
                sum += recorder.GetSample(i).Value;
            }

            return sum / (double)recorder.Count / 1024d;
        }

        private void OnDestroy()
        {
            Game.Events.Unsubscribe<SceneReadyEvent>(OnSceneReady);
            Game.Events.Unsubscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Unsubscribe<PayoffCompleteEvent>(OnPayoff);
            Game.Events.Unsubscribe<FlameOutEvent>(OnFlameOut);
            Game.Events.Unsubscribe<QualityChangedEvent>(OnQualityChanged);
            Game.Events.Unsubscribe<AppPausedEvent>(OnAppPaused);
            Game.Events.Unsubscribe<AdEvent>(OnAd);
            Game.Events.Unsubscribe<PurchaseEvent>(OnPurchase);
            Game.Events.Unsubscribe<ReviewPromptEvent>(OnReviewPrompt);
            if (_placement != null)
            {
                _placement.SelectionChanged -= OnSelectionChanged;
                _placement.Placed -= OnPlaced;
                _placement.Rejected -= OnRejected;
            }

            if (_finale != null)
            {
                _finale.StateChanged -= OnFinaleState;
            }
        }

        private void OnApplicationQuit() => WriteSummary("session_end");

        public void Tick(float deltaSeconds)
        {
            if (_session.SceneReadyTime < 0f)
            {
                // Main opened directly (editor) without the Boot scene's ready event.
                MarkReady(Now);
            }

            float dt = Time.unscaledDeltaTime;
            if (_session.AddFrame(dt * 1000f, dt))
            {
                WritePerformance();
            }
        }

        private void OnSceneReady(SceneReadyEvent e) => MarkReady(e.SecondsSinceLaunch);

        private void MarkReady(float time)
        {
            _session.MarkSceneReady(time);
            Begin("scene_ready");
            Field("ms_since_launch", Mathf.RoundToInt(time * 1000f));
            Emit();
        }

        private void OnIgnited(CrackerIgnitedEvent e)
        {
            bool firstInSession = _session.RecordIgnition(Now, e.CrackerId);
            Begin("cracker_ignited");
            Field("id", e.CrackerId);
            Field("is_first_install", e.IsFirst);
            Field("ignitions", _session.Ignitions);
            if (firstInSession)
            {
                Field("time_to_first_ignition_s", _session.TimeToFirstIgnition);
            }

            Emit();
        }

        private void OnPayoff(PayoffCompleteEvent e)
        {
            _session.RecordPayoff();
            Begin("payoff_complete");
            Field("id", e.CrackerId);
            Field("payoffs", _session.Payoffs);
            Emit();
        }

        private void OnSelectionChanged(Content.CrackerDefinition selected)
        {
            int cancelsBefore = _session.Cancels;
            _session.RecordSelection(selected != null ? selected.Id : null);
            if (selected != null)
            {
                Begin("tray_select");
                Field("id", selected.Id);
                Emit();
            }
            else if (_session.Cancels > cancelsBefore)
            {
                Begin("tray_cancel");
                Field("cancels", _session.Cancels);
                Emit();
            }
        }

        private void OnPlaced(CrackerRuntime runtime)
        {
            _session.RecordPlacement();
            Begin("cracker_placed");
            Field("id", runtime.Definition != null ? runtime.Definition.Id : string.Empty);
            Field("placements", _session.Placements);
            Emit();
        }

        private void OnRejected(string reason)
        {
            _session.RecordRejection();
            Begin("placement_rejected");
            Field("reason", reason);
            Field("rejections", _session.Rejections);
            Emit();
        }

        private void OnFinaleState(FinaleController.FinaleState state)
        {
            if (state != FinaleController.FinaleState.Running)
            {
                // The audit (tools/ad_audit.py) needs to know when the finale window closes.
                Begin("finale_end");
                Emit();
                return;
            }

            _session.RecordFinale();
            Begin("finale_start");
            Field("items", _finale.Total);
            Field("finales", _session.Finales);
            Emit();
        }

        // Every ad decision, for the placement audit (Phase 4 step 9).
        private void OnAd(AdEvent e)
        {
            Begin(e.Type);
            Field("kind", e.Kind.ToString());
            Field("placement", e.Placement);
            Field("ms_since_payoff", e.MsSincePayoff);
            if (e.Block != Core.Monetisation.AdBlock.None)
            {
                Field("block", e.Block.ToString());
            }

            Emit();
        }

        private void OnPurchase(PurchaseEvent e)
        {
            Begin(e.Type);
            Field("sku", e.Sku);
            Emit();
        }

        private void OnFlameOut(FlameOutEvent e)
        {
            _session.RecordFlameOut();
            Begin("flame_out");
            Field("flame_outs", _session.FlameOuts);
            Emit();
        }

        private void OnQualityChanged(QualityChangedEvent e)
        {
            _session.RecordQualityChange();
            Begin("quality_changed");
            Field("tier", e.Tier);
            Field("steps", e.StepsApplied);
            Field("reason", e.Reason);
            Emit();
        }

        private void OnReviewPrompt(ReviewPromptEvent e)
        {
            Begin("review_prompt");
            Field("trigger", e.Trigger);
            Field("backend", e.Backend);
            Field("flow_ran", e.FlowRan);
            Emit();
        }

        private void OnAppPaused(AppPausedEvent e)
        {
            // Android may kill a backgrounded app, so write the summary on every pause.
            WriteSummary(e.Paused ? "session_pause" : "session_resume");
        }

        private void WritePerformance()
        {
            Begin("perf_sample");
            Field("avg_fps", _session.WindowAverageFps);
            Field("p95_ms", _session.WindowP95Ms);
            Field("frames", _session.WindowFrames);
            if (Application.targetFrameRate > 0)
            {
                Field("spike_pct", 100f * _session.WindowSpikeShare(1000f / Application.targetFrameRate));
            }

            if (_gcPerFrame.Valid)
            {
                Field("gc_kb_per_frame", (float)(_gcPerFrame.Count > 0 ? AverageKb(_gcPerFrame) : 0d));
            }
            if (Game.Services.TryGet(out QualityManager quality))
            {
                Field("tier", quality.Tier);
                Field("steps", quality.Governor != null ? quality.Governor.StepsApplied : 0);
            }

            if (Game.Services.TryGet(out EffectsService effects))
            {
                Field("particles_reserved", effects.Budget.Reserved);
                Field("particle_cap", effects.Budget.Cap);
            }

            if (Game.Services.TryGet(out AudioService audio))
            {
                Field("voices", audio.ActiveVoices);
            }

            if (_memory.Valid)
            {
                Field("memory_mb", Mathf.RoundToInt(_memory.LastValue / 1048576f));
            }

            Emit();
            _session.ClearWindow();
        }

        private void WriteSummary(string type)
        {
            if (_session == null)
            {
                return;
            }

            Begin(type);
            Field("duration_s", _session.Duration(Now));
            Field("ignitions", _session.Ignitions);
            Field("payoffs", _session.Payoffs);
            Field("flame_outs", _session.FlameOuts);
            Field("time_to_first_ignition_s", _session.TimeToFirstIgnition);
            Field("quality_changes", _session.QualityChanges);
            Field("distinct_ignited", _session.DistinctIgnited);
            Field("selections", _session.Selections);
            Field("placements", _session.Placements);
            Field("rejections", _session.Rejections);
            Field("cancels", _session.Cancels);
            Field("finales", _session.Finales);
            Emit();
        }

        private void Begin(string type)
        {
            _sb.Clear();
            _sb.Append("{\"session\":\"").Append(_sessionId).Append("\",\"t\":");
            _sb.Append(Now.ToString("0.00", CultureInfo.InvariantCulture));
            _sb.Append(",\"type\":\"").Append(type).Append('"');
        }

        private void Field(string key, string value)
        {
            _sb.Append(",\"").Append(key).Append("\":\"");
            foreach (char c in value ?? string.Empty)
            {
                if (c == '"' || c == '\\')
                {
                    _sb.Append('\\');
                }

                if (c >= ' ')
                {
                    _sb.Append(c);
                }
            }

            _sb.Append('"');
        }

        private void Field(string key, int value) => _sb.Append(",\"").Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));

        private void Field(string key, float value) => _sb.Append(",\"").Append(key).Append("\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture));

        private void Field(string key, bool value) => _sb.Append(",\"").Append(key).Append("\":").Append(value ? "true" : "false");

        private void Emit()
        {
            _sb.Append('}');
            if (BuildFlavor.IsStore)
            {
                // Players' phones keep no playtest log; the session numbers still feed Session.
                return;
            }

            string line = _sb.ToString();
            Debug.Log($"{LogTag} {line}");
            try
            {
                LocalLog.Append(FilePath, line);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} could not write log: {e.Message}");
            }
        }
    }
}
