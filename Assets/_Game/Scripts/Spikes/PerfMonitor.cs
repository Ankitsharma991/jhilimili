using System.Collections;
using Unity.Profiling;
using UnityEngine;
using UtsavNights.Core;

namespace UtsavNights.Spikes
{
    /// <summary>
    /// Central frame-time, memory and startup measurement for the Phase 1 spikes.
    /// "Capture" records a fixed window; "Soak" logs a sample every minute (PRD 14: 15 min soak).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PerfMonitor : MonoBehaviour
    {
        public const float TargetFrameMs = 1000f / 30f;
        public const float SpikeThresholdMs = 2f * TargetFrameMs;
        private const int WarmupFrames = 10;
        private const float SoakSampleSeconds = 60f;

        private readonly RollingStats _live = new RollingStats(240);
        private readonly RollingStats _capture = new RollingStats(8192);
        private readonly RollingStats _soakWindow = new RollingStats(8192);
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private ProfilerRecorder _systemMemory;
        private ParticleStressSpike _particles;
        private AudioLatencySpike _audio;
        private int _capturePeakParticles;
        private int _capturePeakVoices;
        private long _capturePeakMemory;
        private float _soakNextSample;
        private int _soakMinute;

        public double SceneReadySeconds { get; private set; } = -1d;

        public RollingStats Live => _live;

        public float CpuFrameMs { get; private set; }

        public float GpuFrameMs { get; private set; }

        public long SystemMemoryBytes { get; private set; }

        public long PeakSystemMemoryBytes { get; private set; }

        public float CaptureRemaining { get; private set; }

        public float SoakRemaining { get; private set; }

        public bool IsCapturing => CaptureRemaining > 0f;

        public bool IsSoaking => SoakRemaining > 0f;

        private void Awake()
        {
            // Android defaults to 30 fps; unlock 60 so the spikes show real headroom.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _particles = GetComponent<ParticleStressSpike>();
            _audio = GetComponent<AudioLatencySpike>();
            Application.lowMemory += OnLowMemory;
        }

        private void OnDestroy()
        {
            Application.lowMemory -= OnLowMemory;
        }

        private void OnEnable()
        {
            _systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        }

        private void OnDisable()
        {
            _systemMemory.Dispose();
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            SceneReadySeconds = Time.realtimeSinceStartupAsDouble;
            SpikeReport.Record("gateC_startup", "scene_ready_since_engine_start", (float)(SceneReadySeconds * 1000d), "ms");
        }

        private void OnApplicationPause(bool paused)
        {
            Debug.Log($"{SpikeReport.LogTag} app paused={paused}");
        }

        private void OnLowMemory()
        {
            SpikeReport.Record("memory", "low_memory_warning_at", SystemMemoryBytes / 1048576f, "MB");
        }

        public void StartCapture(float seconds)
        {
            _capture.Clear();
            _capturePeakParticles = 0;
            _capturePeakVoices = 0;
            _capturePeakMemory = 0;
            CaptureRemaining = seconds;
        }

        public void StartSoak(float seconds)
        {
            _soakWindow.Clear();
            _soakNextSample = SoakSampleSeconds;
            _soakMinute = 0;
            SoakRemaining = seconds;
            SpikeReport.Record("soak", "started", seconds / 60f, "min");
        }

        public void ResetPeaks()
        {
            PeakSystemMemoryBytes = SystemMemoryBytes;
            if (_particles != null)
            {
                _particles.ResetPeak();
            }
        }

        private void Update()
        {
            if (Time.frameCount <= WarmupFrames)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            float ms = dt * 1000f;
            _live.Add(ms);

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                CpuFrameMs = (float)_timings[0].cpuFrameTime;
                GpuFrameMs = (float)_timings[0].gpuFrameTime;
            }

            if (_systemMemory.Valid)
            {
                SystemMemoryBytes = _systemMemory.LastValue;
                if (SystemMemoryBytes > PeakSystemMemoryBytes)
                {
                    PeakSystemMemoryBytes = SystemMemoryBytes;
                }
            }

            if (CaptureRemaining > 0f)
            {
                TickCapture(ms, dt);
            }

            if (SoakRemaining > 0f)
            {
                TickSoak(ms, dt);
            }
        }

        private void TickCapture(float ms, float dt)
        {
            _capture.Add(ms);
            if (_particles != null && _particles.LiveParticles > _capturePeakParticles)
            {
                _capturePeakParticles = _particles.LiveParticles;
            }

            if (_audio != null && _audio.ActiveVoices > _capturePeakVoices)
            {
                _capturePeakVoices = _audio.ActiveVoices;
            }

            if (SystemMemoryBytes > _capturePeakMemory)
            {
                _capturePeakMemory = SystemMemoryBytes;
            }

            CaptureRemaining -= dt;
            if (CaptureRemaining <= 0f)
            {
                CaptureRemaining = 0f;
                FinishCapture();
            }
        }

        private void FinishCapture()
        {
            string spike = _particles != null && _particles.IsRunning ? "gateA_particles" : "baseline";
            SpikeReport.Record(spike, "avg_fps", SpikeMath.FpsFromFrameMs(_capture.Average), "fps", false);
            SpikeReport.Record(spike, "p95_frame", _capture.Percentile(95f), "ms", false);
            SpikeReport.Record(spike, "p99_frame", _capture.Percentile(99f), "ms", false);
            SpikeReport.Record(spike, "max_frame", _capture.Max, "ms", false);
            SpikeReport.Record(spike, "pct_frames_over_2x_30fps_budget", _capture.PercentAbove(SpikeThresholdMs), "%", false);
            SpikeReport.Record(spike, "peak_particles", _capturePeakParticles, "particles", false);
            SpikeReport.Record(spike, "peak_voices", _capturePeakVoices, "voices", false);
            SpikeReport.Record(spike, "last_cpu_frame", CpuFrameMs, "ms", false);
            SpikeReport.Record(spike, "last_gpu_frame", GpuFrameMs, "ms", false);
            SpikeReport.Record(spike, "peak_system_memory", _capturePeakMemory / 1048576f, "MB");
        }

        private void TickSoak(float ms, float dt)
        {
            _soakWindow.Add(ms);
            SoakRemaining -= dt;
            _soakNextSample -= dt;
            if (_soakNextSample > 0f && SoakRemaining > 0f)
            {
                return;
            }

            _soakMinute++;
            _soakNextSample += SoakSampleSeconds;
            string metric = "minute_" + _soakMinute.ToString("00");
            SpikeReport.Record("soak", metric + "_avg_fps", SpikeMath.FpsFromFrameMs(_soakWindow.Average), "fps", false);
            SpikeReport.Record("soak", metric + "_pct_over_2x_budget", _soakWindow.PercentAbove(SpikeThresholdMs), "%");
            _soakWindow.Clear();
            if (SoakRemaining <= 0f)
            {
                SoakRemaining = 0f;
                SpikeReport.Record("soak", "finished_peak_memory", PeakSystemMemoryBytes / 1048576f, "MB");
            }
        }
    }
}
