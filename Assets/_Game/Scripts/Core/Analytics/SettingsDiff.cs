using System.Collections.Generic;
using System.Globalization;
using UtsavNights.Core.Settings;

namespace UtsavNights.Core.Analytics
{
    /// <summary>
    /// settings_changed key and value pairs between two settings snapshots (PRD 17: Quiet mode and
    /// haptics demand). Volumes are rounded to tenths so a slider drag reports once, not per step.
    /// </summary>
    public static class SettingsDiff
    {
        public static List<KeyValuePair<string, string>> Changes(GameSettings before, GameSettings after)
        {
            var changes = new List<KeyValuePair<string, string>>();
            Add(changes, "master_volume", Tenths(before.masterVolume), Tenths(after.masterVolume));
            Add(changes, "effects_volume", Tenths(before.effectsVolume), Tenths(after.effectsVolume));
            Add(changes, "quiet_mode", OnOff(before.quietMode), OnOff(after.quietMode));
            Add(changes, "reduce_flash", OnOff(before.reduceFlash), OnOff(after.reduceFlash));
            Add(changes, "reduce_motion", OnOff(before.reduceShake), OnOff(after.reduceShake));
            Add(changes, "haptics", OnOff(before.hapticsEnabled), OnOff(after.hapticsEnabled));
            Add(changes, "haptics_strength", Tenths(before.hapticsIntensity), Tenths(after.hapticsIntensity));
            Add(changes, "quality", Tier(before.qualityTier), Tier(after.qualityTier));
            return changes;
        }

        private static void Add(List<KeyValuePair<string, string>> changes, string key, string before, string after)
        {
            if (before != after)
            {
                changes.Add(new KeyValuePair<string, string>(key, after));
            }
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        private static string Tenths(float value) => (System.Math.Round(value * 10f) / 10.0).ToString("0.0", CultureInfo.InvariantCulture);

        private static string Tier(int tier)
        {
            switch (tier)
            {
                case 0:
                    return "low";
                case 1:
                    return "mid";
                case 2:
                    return "high";
                default:
                    return "auto";
            }
        }
    }
}
