using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UtsavNights.Spikes
{
    [Serializable]
    public sealed class SpikeEntry
    {
        public string utc;
        public string spike;
        public string metric;
        public float value;
        public string unit;
        public string tier;
    }

    [Serializable]
    public sealed class SpikeReportData
    {
        public string device;
        public string gpu;
        public string os;
        public int ramMb;
        public int cpuCores;
        public string unityVersion;
        public string appVersion;
        public List<SpikeEntry> entries = new List<SpikeEntry>();
    }

    /// <summary>
    /// Collects spike measurements, prints each to the log with a grep-able tag and saves
    /// them to persistentDataPath/spike_results.json (atomic write). Pull with tools/pull_spike_results.sh.
    /// </summary>
    public static class SpikeReport
    {
        public const string LogTag = "[UTSAV_SPIKE]";
        public const string FileName = "spike_results.json";

        private static SpikeReportData _data;

        public static string CurrentTier { get; set; } = "unknown";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static IReadOnlyList<SpikeEntry> Entries => Data.entries;

        private static SpikeReportData Data => _data ?? (_data = LoadOrCreate());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _data = null;
            CurrentTier = "unknown";
        }

        public static SpikeEntry Record(string spike, string metric, float value, string unit, bool save = true)
        {
            var entry = new SpikeEntry
            {
                utc = DateTime.UtcNow.ToString("o"),
                spike = spike,
                metric = metric,
                value = value,
                unit = unit,
                tier = CurrentTier,
            };
            Data.entries.Add(entry);
            Debug.Log($"{LogTag} {spike}.{metric}={value:0.###} {unit} tier={CurrentTier}");
            if (save)
            {
                Save();
            }

            return entry;
        }

        public static string ToJson() => JsonUtility.ToJson(Data, true);

        public static void Save()
        {
            try
            {
                string path = FilePath;
                string temp = path + ".tmp";
                File.WriteAllText(temp, ToJson());
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
            }
            catch (Exception e)
            {
                // A failed save must never break the scene (PRD 13.3 error handling).
                Debug.LogWarning($"{LogTag} could not save report: {e.Message}");
            }
        }

        /// <summary>Starts a fresh in-memory report without touching the file. Used by tests.</summary>
        public static void ClearInMemory()
        {
            _data = CreateEmpty();
        }

        private static SpikeReportData LoadOrCreate()
        {
            try
            {
                string path = FilePath;
                if (File.Exists(path))
                {
                    var loaded = JsonUtility.FromJson<SpikeReportData>(File.ReadAllText(path));
                    if (loaded != null)
                    {
                        loaded.entries = loaded.entries ?? new List<SpikeEntry>();
                        return loaded;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} could not read previous report, starting fresh: {e.Message}");
            }

            return CreateEmpty();
        }

        private static SpikeReportData CreateEmpty()
        {
            return new SpikeReportData
            {
                device = SystemInfo.deviceModel,
                gpu = SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType,
                os = SystemInfo.operatingSystem,
                ramMb = SystemInfo.systemMemorySize,
                cpuCores = SystemInfo.processorCount,
                unityVersion = Application.unityVersion,
                appVersion = Application.version,
            };
        }
    }
}
