using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Development crash reporting: uncaught exceptions and recorded non-fatals become lines in
    /// persistentDataPath/crash_reports.jsonl (type and first stack frame only), so crash-free sessions
    /// can be counted in closed tests before Crashlytics is connected.
    /// </summary>
    public sealed class DevCrashReporter : ICrashReporter, IDisposable
    {
        public const string FileName = "crash_reports.jsonl";

        public DevCrashReporter()
        {
            Application.logMessageReceivedThreaded += OnLog;
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public string Name => "development";

        public bool CollectionEnabled { get; private set; }

        public int Reports { get; private set; }

        public void SetCollectionEnabled(bool enabled) => CollectionEnabled = enabled;

        public void RecordException(Exception exception)
        {
            if (exception != null)
            {
                Write("non_fatal", exception.GetType().Name, exception.StackTrace);
            }
        }

        public void Dispose() => Application.logMessageReceivedThreaded -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                int colon = condition.IndexOf(':');
                Write("exception", colon > 0 ? condition.Substring(0, colon) : "Exception", stackTrace);
            }
        }

        private void Write(string kind, string exceptionType, string stackTrace)
        {
            if (!CollectionEnabled)
            {
                return;
            }

            Reports++;
            string frame = string.Empty;
            if (!string.IsNullOrEmpty(stackTrace))
            {
                int newline = stackTrace.IndexOf('\n');
                frame = (newline > 0 ? stackTrace.Substring(0, newline) : stackTrace).Trim();
            }

            string line = "{\"t\":\"" + DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture) + "\",\"kind\":\"" + kind +
                          "\",\"type\":\"" + Escape(exceptionType) + "\",\"frame\":\"" + Escape(frame) + "\"}";
            try
            {
                File.AppendAllText(FilePath, line + "\n");
            }
            catch (IOException)
            {
                // Never let crash reporting itself crash the game.
            }
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "'");
    }
}
