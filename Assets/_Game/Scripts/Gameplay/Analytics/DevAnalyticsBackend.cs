using System;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Analytics;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Development analytics: each event becomes a JSON line in persistentDataPath/analytics_events.jsonl
    /// and in logcat with the tag [UTSAV_ANALYTICS], exactly as it would go to Firebase. Pull with
    /// tools/pull_analytics.sh to see the event counts (the stand-in dashboard).
    /// </summary>
    public sealed class DevAnalyticsBackend : IAnalyticsBackend
    {
        public const string LogTag = "[UTSAV_ANALYTICS]";
        public const string FileName = "analytics_events.jsonl";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public string Name => "development";

        public bool CollectionEnabled { get; private set; }

        public int Sent { get; private set; }

        public string LastEvent { get; private set; }

        public void SetCollectionEnabled(bool enabled) => CollectionEnabled = enabled;

        public void Log(string eventName, EventParams parameters)
        {
            if (!CollectionEnabled)
            {
                return;
            }

            Sent++;
            LastEvent = eventName;
            string line = (parameters ?? new EventParams()).ToJson(eventName);
            Debug.Log($"{LogTag} {line}");
            try
            {
                File.AppendAllText(FilePath, line + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} could not write: {e.Message}");
            }
        }
    }
}
