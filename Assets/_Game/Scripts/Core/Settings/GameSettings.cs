using System;

namespace UtsavNights.Core.Settings
{
    /// <summary>
    /// Player settings (PRD 11, 12, 13.5 settings.json; F07). Public fields so Unity's JsonUtility can
    /// serialise it; always run <see cref="Sanitize"/> after loading. Add new fields with safe defaults
    /// and bump <see cref="CurrentVersion"/> with a step in <see cref="Migrate"/>.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int CurrentVersion = 2;

        /// <summary>Automatic quality: keep the detected tier.</summary>
        public const int AutoTier = -1;

        public int version = CurrentVersion;

        public float masterVolume = 1f;

        /// <summary>Effects default to a moderate level (PRD 11 first-launch loud-sound note).</summary>
        public float effectsVolume = 0.8f;

        public float ambienceVolume = 0.7f;

        /// <summary>Music is off by default (PRD 11).</summary>
        public bool musicEnabled;

        public float musicVolume = 0.6f;

        /// <summary>Lower bang level, no flashes, no shake (PRD 11, 6.2).</summary>
        public bool quietMode;

        public bool reduceFlash;

        /// <summary>Reduce motion: no camera shake (PRD 6.2, 14).</summary>
        public bool reduceShake;

        public bool hapticsEnabled = true;

        public float hapticsIntensity = 0.8f;

        /// <summary><see cref="AutoTier"/> or a manual tier 0 = Low, 1 = Mid, 2 = High (F08).</summary>
        public int qualityTier = AutoTier;

        /// <summary>The one-time Quiet-mode tip has been shown (PRD 11).</summary>
        public bool quietTipShown;

        /// <summary>Anonymous usage statistics and crash reports (PRD 16, F12). Added in version 2.</summary>
        public bool shareUsageStats = true;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>Clamps every value into range and fixes impossible combinations. Returns true if anything changed.</summary>
        public bool Sanitize()
        {
            bool changed = false;
            masterVolume = Clamp01(masterVolume, ref changed);
            effectsVolume = Clamp01(effectsVolume, ref changed);
            ambienceVolume = Clamp01(ambienceVolume, ref changed);
            musicVolume = Clamp01(musicVolume, ref changed);
            hapticsIntensity = Clamp01(hapticsIntensity, ref changed);
            if (qualityTier < AutoTier || qualityTier > 2)
            {
                qualityTier = AutoTier;
                changed = true;
            }

            return changed;
        }

        /// <summary>Brings an older file up to <see cref="CurrentVersion"/>. Returns true if it migrated.</summary>
        public bool Migrate()
        {
            if (version >= CurrentVersion)
            {
                return false;
            }

            // Version 0 (or missing) means a file written before versioning: keep values, stamp the version.
            // Version 2 added shareUsageStats; JsonUtility leaves a missing field at its default (true).
            version = CurrentVersion;
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GameSettings o &&
                   version == o.version && masterVolume == o.masterVolume && effectsVolume == o.effectsVolume &&
                   ambienceVolume == o.ambienceVolume && musicEnabled == o.musicEnabled && musicVolume == o.musicVolume &&
                   quietMode == o.quietMode && reduceFlash == o.reduceFlash && reduceShake == o.reduceShake &&
                   hapticsEnabled == o.hapticsEnabled && hapticsIntensity == o.hapticsIntensity &&
                   qualityTier == o.qualityTier && quietTipShown == o.quietTipShown && shareUsageStats == o.shareUsageStats;
        }

        public override int GetHashCode() => version;

        private static float Clamp01(float value, ref bool changed)
        {
            float clamped = float.IsNaN(value) ? 1f : value < 0f ? 0f : value > 1f ? 1f : value;
            if (!clamped.Equals(value))
            {
                changed = true;
            }

            return clamped;
        }
    }

    /// <summary>How settings combine when applied (kept here so the rules are tested once).</summary>
    public static class SettingsRules
    {
        /// <summary>Flash lights need a tier that allows lights, and Quiet mode removes flashes entirely.</summary>
        public static bool FlashesAllowed(bool tierAllowsLights, GameSettings s) => tierAllowsLights && !s.quietMode;

        /// <summary>Reduce-flash halves intensity (also on whenever Quiet mode is on, as a safe default).</summary>
        public static bool ReduceFlash(GameSettings s) => s.reduceFlash || s.quietMode;

        public static bool ShakeAllowed(GameSettings s) => !s.reduceShake && !s.quietMode;

        public static float EffectsVolume(GameSettings s) => s.effectsVolume;

        public static bool HapticsOn(GameSettings s) => s.hapticsEnabled && s.hapticsIntensity > 0f;
    }

    /// <summary>Published after settings change and have been applied.</summary>
    public readonly struct SettingsChangedEvent
    {
        public SettingsChangedEvent(GameSettings settings) => Settings = settings;

        public GameSettings Settings { get; }
    }
}
