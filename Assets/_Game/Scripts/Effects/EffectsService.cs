using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Effects;
using UtsavNights.Core.Services;

namespace UtsavNights.Effects
{
    /// <summary>
    /// The one entry point gameplay uses for visuals: stage emitters and flashes, with tier and
    /// accessibility settings applied in one place. Registered in Game.Services by <see cref="EffectsInstaller"/>.
    /// </summary>
    public sealed class EffectsService : ITickable
    {
        public const int FlashLightCount = 3;

        private readonly ParticleBudget _budget;

        public EffectsService(Transform root, QualityTier tier)
        {
            _budget = new ParticleBudget(QualityBudgets.MaxSceneParticles(tier));
            Particles = new EffectPool(root, _budget);
            Flashes = new FlashLightPool(root, FlashLightCount, new FlashRateLimiter(QualityBudgets.MaxFlashesPerSecond));
            SetTier(tier);
        }

        public EffectPool Particles { get; }

        public FlashLightPool Flashes { get; }

        public ParticleBudget Budget => _budget;

        public QualityTier Tier { get; private set; }

        /// <summary>Quiet mode removes flash lights entirely (PRD 11).</summary>
        public bool QuietMode { get; private set; }

        /// <summary>Applies a quality tier: scene particle cap, and flash lights only on Mid and High.</summary>
        public void SetTier(QualityTier tier)
        {
            Tier = tier;
            _budget.Cap = QualityBudgets.MaxSceneParticles(tier);
            UpdateFlashesEnabled();
        }

        public void SetQuietMode(bool quiet)
        {
            QuietMode = quiet;
            UpdateFlashesEnabled();
        }

        private void UpdateFlashesEnabled() => Flashes.Enabled = Tier != QualityTier.Low && !QuietMode;

        public void SetReduceFlash(bool reduce) => Flashes.ReduceFlash = reduce;

        public EffectHandle PlayStage(StageDefinition stage, CrackerDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (stage == null || stage.emitter == null)
            {
                return EffectHandle.None;
            }

            int tierMax = definition != null ? definition.GetQuality(Tier).maxParticles : 0;
            return Particles.Play(stage.emitter, position, rotation, tierMax);
        }

        /// <summary>Flash for a stage, if both the stage and the definition's tier quality allow it.</summary>
        public bool FlashStage(StageDefinition stage, CrackerDefinition definition, Vector3 position)
        {
            if (stage == null || !stage.light)
            {
                return false;
            }

            if (definition != null && !definition.GetQuality(Tier).light)
            {
                return false;
            }

            return Flashes.TryFlash(position, stage.lightColor, stage.lightPeak);
        }

        public void SetPaused(bool paused) => Particles.SetPaused(paused);

        public void Tick(float deltaSeconds)
        {
            Particles.Tick(deltaSeconds);
            Flashes.Tick(deltaSeconds);
        }
    }
}
