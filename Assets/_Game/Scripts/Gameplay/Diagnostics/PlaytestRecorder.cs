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

            Begin("session_start");
            Field("device", SystemInfo.deviceModel);
            Field("os", SystemInfo.operatingSystem);
            Field("ram_mb", SystemInfo.systemMemorySize);
            Field("gpu", SystemInfo.graphicsDeviceName);
            Field("app", Application.version);
            Emit();
        }

        private void OnEnable()
        {
            Game.Loop.Add(this);
            _memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        }

        private void OnDisable()
        {
            Game.Loop.Remove(this);
            _memory.Dispose();
        }

        private void OnDestroy()
        {
            Game.Events.Unsubscribe<SceneReadyEvent>(OnSceneReady);
            Game.Events.Unsubscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Unsubscribe<PayoffCompleteEvent>(OnPayoff);
            Game.Events.Unsubscribe<FlameOutEvent>(OnFlameOut);
            Game.Events.Unsubscribe<QualityChangedEvent>(OnQualityChanged);
            Game.Events.Unsubscribe<AppPausedEvent>(OnAppPaused);
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
            bool firstInSession = _session.RecordIgnition(Now);
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
            string line = _sb.ToString();
            Debug.Log($"{LogTag} {line}");
            try
            {
                File.AppendAllText(FilePath, line + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} could not write log: {e.Message}");
            }
        }
    }
}
