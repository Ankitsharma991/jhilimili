using System;

namespace UtsavNights.Core.Crackers
{
    /// <summary>How repeated stages spread out (chain lar in a line, cake shots in a fan).</summary>
    public enum RepeatPattern
    {
        /// <summary>Repeat k sits at k times the offset: 0, 1, 2, 3...</summary>
        Line = 0,

        /// <summary>Alternates sides: 0, +1, -1, +2, -2...</summary>
        Fan = 1,
    }

    /// <summary>Engine-free maths for stage repeats and motion.</summary>
    public static class StageMath
    {
        /// <summary>Multiplier applied to a stage's repeat offset for repetition <paramref name="repeat"/>.</summary>
        public static float OffsetMultiplier(int repeat, RepeatPattern pattern)
        {
            if (repeat <= 0)
            {
                return 0f;
            }

            if (pattern == RepeatPattern.Line)
            {
                return repeat;
            }

            int step = (repeat + 1) / 2;
            return repeat % 2 == 1 ? step : -step;
        }

        public static float RepeatTime(float baseTime, int repeat, float interval) => baseTime + Math.Max(0, repeat) * Math.Max(0f, interval);

        /// <summary>
        /// Height of a launched shell after <paramref name="elapsed"/> seconds of a <paramref name="riseSeconds"/>
        /// climb: fast off the pad, slowing into the apex (ease-out), clamped at the apex.
        /// </summary>
        public static float LaunchHeight(float elapsed, float riseSeconds, float apexHeight)
        {
            if (riseSeconds <= 0f)
            {
                return apexHeight;
            }

            float t = elapsed / riseSeconds;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float eased = 1f - (1f - t) * (1f - t);
            return apexHeight * eased;
        }

        public static bool LaunchFinished(float elapsed, float riseSeconds) => elapsed >= riseSeconds;
    }
}
