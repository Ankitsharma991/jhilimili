using System.Collections.Generic;

namespace UtsavNights.Core.Analytics
{
    /// <summary>
    /// PRD 17 event list and the personal-data guard (F12: no PII). Only listed events and parameters
    /// pass. String values must be short lowercase ids (cracker ids, placements, skus, setting keys,
    /// locale), so free text, emails, phone numbers and device identifiers can never be sent.
    /// </summary>
    public static class AnalyticsSchema
    {
        public const int MaxStringLength = 40;

        private static readonly Dictionary<string, string[]> Events = new Dictionary<string, string[]>
        {
            { "app_first_open", new[] { "tier", "locale" } },
            { "scene_ready", new[] { "ms_since_launch" } },
            { "cracker_placed", new[] { "id" } },
            { "cracker_ignited", new[] { "id", "is_first" } },
            { "payoff_complete", new[] { "id" } },
            { "session_end", new[] { "duration_s", "ignitions" } },
            { "settings_changed", new[] { "key", "value" } },
            { "ad_shown", new[] { "placement", "ms_since_payoff" } },
            { "ad_failed", new[] { "placement", "ms_since_payoff" } },
            { "ad_reward", new[] { "placement", "ms_since_payoff" } },
            { "purchase_started", new[] { "sku" } },
            { "purchase_done", new[] { "sku" } },
            { "clip_shared", new[] { "target" } },
            { "perf_sample", new[] { "avg_fps", "tier", "downgrades" } },
        };

        public static IEnumerable<string> EventNames => Events.Keys;

        public static bool IsKnown(string eventName) => eventName != null && Events.ContainsKey(eventName);

        /// <summary>Every problem with an event (empty when it may be sent).</summary>
        public static List<string> Validate(string eventName, EventParams parameters)
        {
            var errors = new List<string>();
            if (!IsKnown(eventName))
            {
                errors.Add($"unknown event '{eventName}'");
                return errors;
            }

            string[] allowed = Events[eventName];
            if (parameters == null)
            {
                return errors;
            }

            var seen = new HashSet<string>();
            foreach (KeyValuePair<string, object> item in parameters.Items)
            {
                if (System.Array.IndexOf(allowed, item.Key) < 0)
                {
                    errors.Add($"{eventName}: parameter '{item.Key}' is not in the schema");
                }

                if (!seen.Add(item.Key))
                {
                    errors.Add($"{eventName}: parameter '{item.Key}' repeated");
                }

                if (item.Value is string s && !IsSafeId(s))
                {
                    errors.Add($"{eventName}: '{item.Key}' must be a short id (no free text or personal data)");
                }
            }

            return errors;
        }

        /// <summary>Lowercase letters, digits, '_', '.', '-' only, at most 40 characters, and no long digit runs (phone numbers).</summary>
        public static bool IsSafeId(string value)
        {
            if (value == null || value.Length > MaxStringLength)
            {
                return false;
            }

            int digitRun = 0;
            foreach (char c in value)
            {
                bool digit = c >= '0' && c <= '9';
                bool ok = (c >= 'a' && c <= 'z') || digit || c == '_' || c == '.' || c == '-';
                if (!ok)
                {
                    return false;
                }

                digitRun = digit ? digitRun + 1 : 0;
                if (digitRun >= 7)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
