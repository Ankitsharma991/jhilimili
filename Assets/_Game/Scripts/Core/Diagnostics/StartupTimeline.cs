using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UtsavNights.Core.Diagnostics
{
    /// <summary>
    /// Cold start from the moment Android starts the process to the moment the scene takes taps
    /// (PRD 14: 6 s or less on the low tier, 4 s mid, 3 s high), with marks in between: Unity runtime
    /// ready, main scene loaded, interactive, then the SDKs that finish in the background (consent,
    /// ads, store). Times are seconds since the engine started; <see cref="PreEngineSeconds"/> is the
    /// part before it (process start, Android activity, Unity player init).
    /// </summary>
    public sealed class StartupTimeline
    {
        public const string Interactive = "scene_ready";

        private readonly List<KeyValuePair<string, float>> _marks = new List<KeyValuePair<string, float>>();

        /// <summary>Process start to engine start; 0 where unknown (the editor).</summary>
        public float PreEngineSeconds { get; set; }

        public IReadOnlyList<KeyValuePair<string, float>> Marks => _marks;

        public bool IsInteractive => TryGet(Interactive, out _);

        /// <summary>Process start to <see cref="Interactive"/>, or -1 before it.</summary>
        public float InteractiveSeconds => TryGet(Interactive, out float t) ? PreEngineSeconds + t : -1f;

        /// <summary>Records the first time only; later calls with the same name are ignored.</summary>
        public bool Mark(string name, float secondsSinceEngineStart)
        {
            if (TryGet(name, out _))
            {
                return false;
            }

            _marks.Add(new KeyValuePair<string, float>(name, secondsSinceEngineStart));
            return true;
        }

        public bool TryGet(string name, out float seconds)
        {
            foreach (KeyValuePair<string, float> mark in _marks)
            {
                if (mark.Key == name)
                {
                    seconds = mark.Value;
                    return true;
                }
            }

            seconds = 0f;
            return false;
        }

        /// <summary>PRD 14 cold start target for a tier (0 low, 1 mid, 2 high).</summary>
        public static float TargetSeconds(int tier) => tier <= 0 ? 6f : tier == 1 ? 4f : 3f;

        /// <summary>
        /// One grep-friendly line: "pre_engine_ms=420 runtime_init_ms=35 ... interactive_ms=1270". Mark times
        /// are milliseconds since the process started, so they add up the way a player feels them.
        /// </summary>
        public string Describe()
        {
            var sb = new StringBuilder(160);
            sb.Append("pre_engine_ms=").Append(Ms(PreEngineSeconds));
            foreach (KeyValuePair<string, float> mark in _marks)
            {
                sb.Append(' ').Append(mark.Key).Append("_ms=").Append(Ms(PreEngineSeconds + mark.Value));
            }

            if (IsInteractive)
            {
                sb.Append(" interactive_ms=").Append(Ms(InteractiveSeconds));
            }

            return sb.ToString();
        }

        private static string Ms(float seconds) => ((int)(seconds * 1000f + 0.5f)).ToString(CultureInfo.InvariantCulture);
    }
}
