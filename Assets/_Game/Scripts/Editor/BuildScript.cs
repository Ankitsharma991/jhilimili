using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UtsavNights.EditorTools
{
    /// <summary>
    /// Spike E / Gate C build entry points. Menu items for local use; Ci* methods for batch mode:
    ///   Unity -batchmode -projectPath . -buildTarget Android -executeMethod UtsavNights.EditorTools.BuildScript.CiReleaseAab -quit
    /// Builds without a custom keystore are signed with the debug key: fine for size and device tests,
    /// not for Play upload (keystore setup is Phase 5).
    /// </summary>
    public static class BuildScript
    {
        public const string OutputFolder = "Builds/Android";

        public enum Kind
        {
            ReleaseAab,
            ReleaseApk,
            DevelopmentApk,
        }

        [MenuItem("Utsav/Build/Android Release AAB (Gate C size)", priority = 40)]
        public static void ReleaseAab() => Build(Kind.ReleaseAab);

        [MenuItem("Utsav/Build/Android Release APK (install for fps and cold start)", priority = 41)]
        public static void ReleaseApk() => Build(Kind.ReleaseApk);

        [MenuItem("Utsav/Build/Android Development APK (Profiler over USB)", priority = 42)]
        public static void DevelopmentApk() => Build(Kind.DevelopmentApk);

        public static void CiReleaseAab() => ExitWith(Build(Kind.ReleaseAab));

        public static void CiReleaseApk() => ExitWith(Build(Kind.ReleaseApk));

        private static void ExitWith(bool ok)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        public static bool Build(Kind kind)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[UTSAV_BUILD] No scenes in Build Settings. Run Utsav/Setup/Run All Phase 1 Setup first.");
                return false;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            bool aab = kind == Kind.ReleaseAab;
            EditorUserBuildSettings.buildAppBundle = aab;
            Directory.CreateDirectory(OutputFolder);
            string fileName = kind switch
            {
                Kind.ReleaseAab => "UtsavNights-release.aab",
                Kind.ReleaseApk => "UtsavNights-release.apk",
                _ => "UtsavNights-dev.apk",
            };
            string path = Path.Combine(OutputFolder, fileName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = kind == Kind.DevelopmentApk
                    ? BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging
                    : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            long bytes = File.Exists(path) ? new FileInfo(path).Length : (long)summary.totalSize;
            string line =
                $"[UTSAV_BUILD] kind={kind} result={summary.result} size_mb={bytes / 1048576f:0.00} " +
                $"duration_s={summary.totalTime.TotalSeconds:0} unity={Application.unityVersion} path={path} utc={DateTime.UtcNow:o}";
            Debug.Log(line);
            File.AppendAllText(Path.Combine(OutputFolder, "build_log.txt"), line + Environment.NewLine);

            // Leave the default as AAB (the Play upload format) after a temporary APK build.
            EditorUserBuildSettings.buildAppBundle = true;
            return summary.result == BuildResult.Succeeded;
        }
    }
}
