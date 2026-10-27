using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Android.Types;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UtsavNights.Core.Release;
using UtsavNights.Gameplay;

namespace UtsavNights.EditorTools
{
    /// <summary>
    /// Phase 5 step 1: the signed Google Play upload (AAB). Run it through tools/build_store.sh, which reads
    /// the upload key from outside the repository and passes everything as environment variables:
    ///   UTSAV_TRACK (internal, closed, production), UTSAV_VERSION_NAME, UTSAV_VERSION_CODE,
    ///   UTSAV_KEYSTORE, UTSAV_KEY_ALIAS, UTSAV_KEYSTORE_PASS, UTSAV_KEY_PASS, UTSAV_PREFLIGHT_ONLY.
    /// The preflight (<see cref="ReleaseChecks"/>) runs first and blockers stop the build. The store build
    /// compiles with UTSAV_STORE (no developer overlay, no playtest log) and puts native debug symbols
    /// in the bundle for Play's crash reports. Keystore paths and passwords are cleared afterwards so
    /// they never reach ProjectSettings; the new version name and code are kept, so commit them.
    /// </summary>
    public static class StoreBuild
    {
        public const string OutputFolder = "Builds/Android/store";
        public const string HistoryPath = "Docs/Phase5/store_builds.tsv";
        private const string HistoryHeader = "utc\ttrack\tversion_name\tversion_code\tsize_mb\tfile";

        [MenuItem("Utsav/Build/Store preflight (Play upload checks)", priority = 50)]
        public static void PreflightMenu()
        {
            ReleaseInputs input = GatherInputs(ReleaseTrack.Closed, PlayerSettings.bundleVersion,
                PlayerSettings.Android.bundleVersionCode + 1, keystore: null, storePass: null, keyPass: null);
            Report(ReleaseChecks.Evaluate(input), input);
        }

        public static void CiStoreAab()
        {
            bool ok;
            try
            {
                ok = BuildFromEnvironment();
            }
            catch (Exception e)
            {
                Debug.LogError("[UTSAV_STORE] " + e);
                ok = false;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        private static bool BuildFromEnvironment()
        {
            if (!Enum.TryParse(Env("UTSAV_TRACK"), ignoreCase: true, out ReleaseTrack track))
            {
                Debug.LogError("[UTSAV_STORE] UTSAV_TRACK must be internal, closed or production");
                return false;
            }

            string versionName = Env("UTSAV_VERSION_NAME");
            int.TryParse(Env("UTSAV_VERSION_CODE"), NumberStyles.None, CultureInfo.InvariantCulture, out int versionCode);
            string keystore = Env("UTSAV_KEYSTORE");
            string alias = string.IsNullOrEmpty(Env("UTSAV_KEY_ALIAS")) ? "upload" : Env("UTSAV_KEY_ALIAS");
            string storePass = Env("UTSAV_KEYSTORE_PASS");
            string keyPass = string.IsNullOrEmpty(Env("UTSAV_KEY_PASS")) ? storePass : Env("UTSAV_KEY_PASS");

            ReleaseInputs input = GatherInputs(track, versionName, versionCode, keystore, storePass, keyPass);
            List<ReleaseCheck> checks = ReleaseChecks.Evaluate(input);
            Report(checks, input);
            if (ReleaseChecks.HasBlocker(checks))
            {
                Debug.LogError("[UTSAV_STORE] preflight has blockers; nothing was built");
                return false;
            }

            if (Env("UTSAV_PREFLIGHT_ONLY") == "1")
            {
                return true;
            }

            return Build(track, versionName, versionCode, keystore, alias, storePass, keyPass);
        }

        internal static ReleaseInputs GatherInputs(ReleaseTrack track, string versionName, int versionCode,
            string keystore, string storePass, string keyPass)
        {
            AdsConfig ads = AdsSetup.EnsureConfig();
            AppLinks links = AdsSetup.EnsureAppLinks();
            string[] assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
            return new ReleaseInputs
            {
                Track = track,
                VersionName = versionName ?? string.Empty,
                VersionCode = versionCode,
                LastStoreVersionCode = LastStoreVersionCode(),
                KeystoreFound = !string.IsNullOrEmpty(keystore) && File.Exists(keystore),
                KeystorePasswordsSet = !string.IsNullOrEmpty(storePass) && !string.IsNullOrEmpty(keyPass),

                // These assemblies compile only when their SDK package is installed (versionDefines).
                AdsSdkInstalled = assemblies.Contains("UtsavNights.Ads"),
                PurchasingSdkInstalled = assemblies.Contains("UtsavNights.Purchasing"),
                AnalyticsSdkInstalled = assemblies.Contains("UtsavNights.Firebase"),
                AdsUseTestIds = ads.UsesTestIds,
                DebugGeographyEea = ads.debugGeographyEea,
                HasPrivacyPolicy = links.HasPrivacyPolicy,
                TargetSdk = EffectiveTargetSdk(),
            };
        }

        /// <summary>The highest code in the store build history, so Play never sees a code twice.</summary>
        internal static int LastStoreVersionCode()
        {
            if (!File.Exists(HistoryPath))
            {
                return 0;
            }

            int last = 0;
            foreach (string row in File.ReadAllLines(HistoryPath).Skip(1))
            {
                string[] cells = row.Split('\t');
                if (cells.Length > 3 && int.TryParse(cells[3], NumberStyles.None, CultureInfo.InvariantCulture, out int code))
                {
                    last = Math.Max(last, code);
                }
            }

            return last;
        }

        /// <summary>
        /// The target API Unity will write. "Automatic" means the highest whole-number platform in the SDK
        /// (Unity skips minor-version folders such as android-37.0).
        /// </summary>
        private static int EffectiveTargetSdk()
        {
            int set = (int)PlayerSettings.Android.targetSdkVersion;
            if (set > 0)
            {
                return set;
            }

            string platforms = Path.Combine(AndroidExternalToolsSettings.sdkRootPath ?? string.Empty, "platforms");
            if (!Directory.Exists(platforms))
            {
                return 0;
            }

            return Directory.GetDirectories(platforms)
                .Select(d => Regex.Match(Path.GetFileName(d), @"^android-(\d+)$"))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0)
                .Max();
        }

        private static void Report(List<ReleaseCheck> checks, ReleaseInputs input)
        {
            Debug.Log($"[UTSAV_STORE] preflight track={input.Track} version={input.VersionName} ({input.VersionCode}) " +
                      $"last_store_code={input.LastStoreVersionCode} target_sdk={input.TargetSdk}: " +
                      (checks.Count == 0 ? "all clear" : $"{checks.Count} item(s)"));
            foreach (ReleaseCheck check in checks)
            {
                if (check.Level == CheckLevel.Blocker)
                {
                    Debug.LogError("[UTSAV_STORE] " + check);
                }
                else
                {
                    Debug.LogWarning("[UTSAV_STORE] " + check);
                }
            }
        }

        private static bool Build(ReleaseTrack track, string versionName, int versionCode, string keystore, string alias,
            string storePass, string keyPass)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            string previousName = PlayerSettings.bundleVersion;
            int previousCode = PlayerSettings.Android.bundleVersionCode;
            DebugSymbolLevel previousLevel = UserBuildSettings.DebugSymbols.level;
            DebugSymbolFormat previousFormat = UserBuildSettings.DebugSymbols.format;
            Directory.CreateDirectory(OutputFolder);
            string fileName = $"UtsavNights-{versionName}-{versionCode}-{track.ToString().ToLowerInvariant()}.aab";
            string path = Path.Combine(OutputFolder, fileName);
            bool ok = false;
            try
            {
                PlayerSettings.bundleVersion = versionName;
                PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = keyPass;

                // Symbols inside the bundle let Play Console symbolicate native crashes (Android vitals);
                // the zip beside it is for Crashlytics once Firebase is connected.
                UserBuildSettings.DebugSymbols.level = DebugSymbolLevel.SymbolTable;
                UserBuildSettings.DebugSymbols.format = DebugSymbolFormat.IncludeInBundle | DebugSymbolFormat.Zip;
                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.development = false;

                AdsSetup.Apply(release: true);
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                    extraScriptingDefines = new[] { "UTSAV_STORE" },
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                ok = report.summary.result == BuildResult.Succeeded;
                long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                string line =
                    $"[UTSAV_BUILD] kind=StoreAab track={track} result={report.summary.result} version={versionName} code={versionCode} " +
                    $"size_mb={bytes / 1048576f:0.00} duration_s={report.summary.totalTime.TotalSeconds:0} path={path} utc={DateTime.UtcNow:o}";
                Debug.Log(line);
                File.AppendAllText(Path.Combine(BuildScript.OutputFolder, "build_log.txt"), line + Environment.NewLine);
                if (ok)
                {
                    AppendHistory(track, versionName, versionCode, bytes, fileName);
                }
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystoreName = string.Empty;
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasName = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                UserBuildSettings.DebugSymbols.level = previousLevel;
                UserBuildSettings.DebugSymbols.format = previousFormat;
                if (!ok)
                {
                    PlayerSettings.bundleVersion = previousName;
                    PlayerSettings.Android.bundleVersionCode = previousCode;
                }

                AssetDatabase.SaveAssets();
                KeepKeystoreLineBlank();
            }

            return ok;
        }

        /// <summary>
        /// Unity saves a cleared keystore path as "'{inproject}: '"; put back the blank line the repository
        /// has, so a store build changes only the version in ProjectSettings.
        /// </summary>
        private static void KeepKeystoreLineBlank()
        {
            const string settings = "ProjectSettings/ProjectSettings.asset";
            string text = File.ReadAllText(settings);
            string fixedText = text.Replace("  AndroidKeystoreName: '{inproject}: '\n", "  AndroidKeystoreName: \n");
            if (fixedText != text)
            {
                File.WriteAllText(settings, fixedText);
            }
        }

        private static void AppendHistory(ReleaseTrack track, string versionName, int versionCode, long bytes, string fileName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath));
            if (!File.Exists(HistoryPath))
            {
                File.WriteAllText(HistoryPath, HistoryHeader + "\n");
            }

            File.AppendAllText(HistoryPath, string.Join("\t",
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                track.ToString().ToLowerInvariant(), versionName, versionCode.ToString(CultureInfo.InvariantCulture),
                (bytes / 1048576f).ToString("0.00", CultureInfo.InvariantCulture), fileName) + "\n");
        }

        private static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? string.Empty;
    }
}
