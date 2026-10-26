using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UtsavNights.Content;
using UtsavNights.Core;
using UtsavNights.Core.Quality;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using UtsavNights.Effects;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Quality governor v1 (F08, PRD 14). First run: tier from hardware, refined by a 3 s in-scene
    /// benchmark, then saved. Every run: the governor steps down particles, then bloom, then resolution
    /// when frames stay over budget, and steps up at most once per session. Low-memory warnings drop a tier.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    public sealed class QualityManager : MonoBehaviour, ITickable
    {
        public const string TierPrefKey = "quality.tier";
        public const string ManualPrefKey = "quality.manual";

        /// <summary>Particle cap multiplier for the first degradation step.</summary>
        public const float ReducedParticleFactor = 0.6f;

        /// <summary>Render scale multiplier for the third degradation step.</summary>
        public const float ReducedResolutionFactor = 0.8f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private float benchmarkWarmupSeconds = 1f;
        [SerializeField] private float benchmarkSeconds = 3f;

        private readonly RollingStats _benchmark = new RollingStats(1024);
        private QualityGovernor _governor;
        private bool _benchmarkPending;
        private float _benchmarkClock;
        private UniversalRenderPipelineAsset _pipeline;
        private float _baseRenderScale = -1f;
        private bool _registered;

        public int Tier { get; private set; } = TierDetection.Mid;

        public QualityGovernor Governor => _governor;

        public bool BenchmarkPending => _benchmarkPending;

        public void Configure(Camera cameraToControl) => targetCamera = cameraToControl;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            int saved = PlayerPrefs.GetInt(TierPrefKey, -1);
            if (saved < 0)
            {
                int guess = TierDetection.FromHardware(SystemInfo.systemMemorySize, SystemInfo.processorCount);
                Debug.Log($"[Quality] first run: {SystemInfo.systemMemorySize} MB, {SystemInfo.processorCount} cores -> tier {guess}; benchmarking");
                _benchmarkPending = true;
                ApplyTier(guess, "hardware");
            }
            else
            {
                ApplyTier(saved, "saved");
            }

            if (!Game.Services.IsRegistered<QualityManager>())
            {
                Game.Services.Register(this);
                _registered = true;
            }

            Application.lowMemory += OnLowMemory;
        }

        private void OnEnable() => Game.Loop.Add(this);

        private void OnDisable() => Game.Loop.Remove(this);

        private void OnDestroy()
        {
            Application.lowMemory -= OnLowMemory;
            RestoreRenderScale();
            if (_registered)
            {
                Game.Services.Unregister<QualityManager>();
            }
        }

        private void OnApplicationQuit() => RestoreRenderScale();

        /// <summary>Settings sheet override (F08). Saved; the governor keeps protecting frame rate.</summary>
        public void SetTierManually(int tier)
        {
            tier = TierDetection.Clamp(tier);
            PlayerPrefs.SetInt(TierPrefKey, tier);
            PlayerPrefs.SetInt(ManualPrefKey, 1);
            PlayerPrefs.Save();
            _benchmarkPending = false;
            ApplyTier(tier, "manual");
        }

        public void Tick(float deltaSeconds)
        {
            float frameMs = Time.unscaledDeltaTime * 1000f;
            if (_benchmarkPending)
            {
                TickBenchmark(frameMs);
                return;
            }

            _governor.Sample(frameMs, Time.unscaledDeltaTime);
        }

        private void TickBenchmark(float frameMs)
        {
            _benchmarkClock += Time.unscaledDeltaTime;
            if (_benchmarkClock < benchmarkWarmupSeconds)
            {
                return;
            }

            if (frameMs > 0f && frameMs < QualityGovernor.HitchMs)
            {
                _benchmark.Add(frameMs);
            }

            if (_benchmarkClock < benchmarkWarmupSeconds + benchmarkSeconds)
            {
                return;
            }

            _benchmarkPending = false;
            float average = _benchmark.Average;
            int adjusted = TierDetection.AdjustForBenchmark(Tier, average);
            Debug.Log($"[Quality] benchmark avg {average:0.0} ms on tier {Tier} -> tier {adjusted}");
            PlayerPrefs.SetInt(TierPrefKey, adjusted);
            PlayerPrefs.Save();
            if (adjusted != Tier)
            {
                ApplyTier(adjusted, "benchmark");
            }
        }

        private void ApplyTier(int tier, string reason)
        {
            RestoreRenderScale();
            Tier = TierDetection.Clamp(tier);

            // PRD 14 targets: 30 fps on Low saves heat and battery; Mid and High aim for 60.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Tier == TierDetection.Low ? 30 : 60;
            if (Tier < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(Tier, true);
            }

            _pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            _baseRenderScale = _pipeline != null ? _pipeline.renderScale : -1f;

            if (Game.Services.TryGet(out EffectsService effects))
            {
                effects.SetTier((QualityTier)Tier);
            }

            SetBloom(Tier > TierDetection.Low);

            if (_governor != null)
            {
                _governor.SteppedDown -= OnSteppedDown;
                _governor.SteppedUp -= OnSteppedUp;
            }

            _governor = new QualityGovernor(TierDetection.TargetFrameMs(Tier));
            _governor.SteppedDown += OnSteppedDown;
            _governor.SteppedUp += OnSteppedUp;
            Publish(reason);
        }

        private void OnSteppedDown(DegradeStep step)
        {
            switch (step)
            {
                case DegradeStep.ReduceParticles:
                    if (Game.Services.TryGet(out EffectsService effects))
                    {
                        effects.Budget.Cap = Mathf.RoundToInt(QualityBudgets.MaxSceneParticles((QualityTier)Tier) * ReducedParticleFactor);
                    }

                    break;
                case DegradeStep.DisableBloom:
                    SetBloom(false);
                    break;
                case DegradeStep.ReduceResolution:
                    if (_pipeline != null && _baseRenderScale > 0f)
                    {
                        _pipeline.renderScale = _baseRenderScale * ReducedResolutionFactor;
                    }

                    break;
            }

            Debug.Log($"[Quality] step down: {step} (tier {Tier})");
            Publish("step_down_" + step);
        }

        private void OnSteppedUp(DegradeStep undone)
        {
            switch (undone)
            {
                case DegradeStep.ReduceParticles:
                    if (Game.Services.TryGet(out EffectsService effects))
                    {
                        effects.SetTier((QualityTier)Tier);
                    }

                    break;
                case DegradeStep.DisableBloom:
                    SetBloom(Tier > TierDetection.Low);
                    break;
                case DegradeStep.ReduceResolution:
                    RestoreRenderScale();
                    break;
            }

            Debug.Log($"[Quality] step up: undo {undone} (tier {Tier})");
            Publish("step_up_" + undone);
        }

        private void OnLowMemory()
        {
            if (Tier > TierDetection.Low)
            {
                Debug.LogWarning("[Quality] low memory warning: dropping one tier");
                PlayerPrefs.SetInt(TierPrefKey, Tier - 1);
                PlayerPrefs.Save();
                ApplyTier(Tier - 1, "low_memory");
            }
        }

        private void SetBloom(bool enabled)
        {
            if (targetCamera != null)
            {
                targetCamera.GetUniversalAdditionalCameraData().renderPostProcessing = enabled;
            }
        }

        // URP assets are shared; in the editor a runtime change would be saved into the asset.
        private void RestoreRenderScale()
        {
            if (_pipeline != null && _baseRenderScale > 0f)
            {
                _pipeline.renderScale = _baseRenderScale;
            }
        }

        private void Publish(string reason)
        {
            int steps = _governor != null ? _governor.StepsApplied : 0;
            Game.Events.Publish(new QualityChangedEvent(Tier, steps, reason));
        }
    }
}
