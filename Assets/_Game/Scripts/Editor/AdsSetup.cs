using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UtsavNights.Gameplay;

namespace UtsavNights.EditorTools
{
    /// <summary>
    /// Keeps the AdMob configuration in one place (Resources/AdsConfig) and copies the app ID into the
    /// Google Mobile Ads plugin settings before every build, so the plugin's manifest step always has it.
    /// </summary>
    public static class AdsSetup
    {
        public const string ConfigPath = "Assets/_Game/Resources/AdsConfig.asset";
        private const string PluginSettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        private const string PluginSettingsType = "GoogleMobileAds.Editor.GoogleMobileAdsSettings";

        public static AdsConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<AdsConfig>(ConfigPath);
            if (config == null)
            {
                EditorAssetUtil.EnsureFolder("Assets/_Game/Resources");
                config = ScriptableObject.CreateInstance<AdsConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        /// <summary>
        /// Writes the app ID into the plugin's settings asset. Returns false when the Google Mobile Ads
        /// package is not installed (nothing to configure). Warns when a release build uses test IDs.
        /// </summary>
        [MenuItem("Utsav/Setup/Apply Ads Config")]
        public static bool Apply() => Apply(release: false);

        public static bool Apply(bool release)
        {
            AdsConfig config = EnsureConfig();
            Type settingsType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(PluginSettingsType, false))
                .FirstOrDefault(t => t != null);
            if (settingsType == null)
            {
                return false;
            }

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PluginSettingsPath);
            if (settings == null)
            {
                EditorAssetUtil.EnsureFolder("Assets/GoogleMobileAds/Resources");
                settings = ScriptableObject.CreateInstance(settingsType);
                AssetDatabase.CreateAsset(settings, PluginSettingsPath);
            }

            var serialized = new SerializedObject(settings);
            SerializedProperty appId = serialized.FindProperty("adMobAndroidAppId");
            if (appId == null)
            {
                Debug.LogError("[Ads] the Google Mobile Ads settings format changed; set the Android app ID by hand.");
                return false;
            }

            if (appId.stringValue != config.androidAppId)
            {
                appId.stringValue = config.androidAppId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }

            if (release && config.UsesTestIds)
            {
                Debug.LogWarning("[Ads] this build uses Google's test ad IDs. Replace them in " + ConfigPath +
                                 " with your AdMob IDs only for the closed-test build (Phase 4 exit gate).");
            }

            return true;
        }
    }
}
