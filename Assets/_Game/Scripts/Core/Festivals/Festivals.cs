using System;
using System.Collections.Generic;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Festivals
{
    /// <summary>
    /// A festival night (Phase 11 step 1, PRD 7 "festival" axis): which crackers and decorations belong to it, its show
    /// template and whether it counts down to midnight. Festivals are data, so Eid or Lunar New Year can be added once an
    /// advisor has reviewed them, without new code.
    /// </summary>
    public sealed class FestivalInfo
    {
        public FestivalInfo(string id, string name, Festival flags, string[] crackerTags, bool countdown, string greeting)
        {
            Id = id;
            Name = name;
            Flags = flags;
            CrackerTags = crackerTags;
            Countdown = countdown;
            Greeting = greeting;
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>Decoration festivals shown on this night (Everyday items always are).</summary>
        public Festival Flags { get; }

        /// <summary>The tags in a cracker definition's festival list that put it in the tray tonight.</summary>
        public string[] CrackerTags { get; }

        /// <summary>The night counts down to midnight (New Year).</summary>
        public bool Countdown { get; }

        public string Greeting { get; }
    }

    public static class Festivals
    {
        public const string DiwaliTiharId = "diwali_tihar";
        public const string NewYearId = "new_year";

        public static readonly FestivalInfo DiwaliTihar = new FestivalInfo(DiwaliTiharId, "Diwali · Tihar", Festival.Diwali | Festival.Tihar,
            new[] { "diwali", "tihar" }, false, "Shubh Deepawali!");

        public static readonly FestivalInfo NewYear = new FestivalInfo(NewYearId, "New Year", Festival.NewYear, new[] { "new_year" }, true, "Happy New Year!");

        /// <summary>In the order the Settings switch shows them; the first is the default.</summary>
        public static readonly FestivalInfo[] All = { DiwaliTihar, NewYear };

        public static FestivalInfo Default => DiwaliTihar;

        public static FestivalInfo Find(string id) => Array.Find(All, f => f.Id == id) ?? Default;

        public static bool IsKnown(string id) => Array.Exists(All, f => f.Id == id);

        /// <summary>A cracker is in tonight's tray when one of its festival tags is tonight's.</summary>
        public static bool HasCracker(FestivalInfo festival, IEnumerable<string> crackerFestivals)
        {
            if (crackerFestivals == null)
            {
                return false;
            }

            foreach (string tag in crackerFestivals)
            {
                if (Array.IndexOf(festival.CrackerTags, tag) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A decoration is on tonight's shelf when it belongs to the festival or to every day.</summary>
        public static bool HasDecoration(FestivalInfo festival, DecorInfo info) => info != null && (info.Festivals & (festival.Flags | Festival.Everyday)) != 0;
    }

    /// <summary>
    /// The countdown to midnight (New Year): ten seconds shown one by one, then midnight: the greeting, the show, the cheer
    /// and the sky filling with neighbours' bursts.
    /// </summary>
    public static class MidnightCountdown
    {
        public const int Seconds = 10;

        /// <summary>How long the greeting stays after midnight.</summary>
        public const float GreetingSeconds = 6f;

        /// <summary>How long the neighbours' bursts surge after midnight, and how many a minute.</summary>
        public const float SurgeSeconds = 45f;
        public const float SurgeBurstsPerMinute = 40f;

        /// <summary>The number to show at <paramref name="elapsed"/> seconds into the countdown (10 to 1), or 0 at and after midnight.</summary>
        public static int Shown(float elapsed) => elapsed >= Seconds ? 0 : Seconds - (int)Math.Floor(Math.Max(0f, elapsed));

        public static bool IsMidnight(float elapsed) => elapsed >= Seconds;

        /// <summary>The clock the night shows during the countdown: 11:59 pm, then 12:00 am.</summary>
        public static string Clock(float elapsed) => IsMidnight(elapsed) ? "12:00 am" : "11:59 pm";
    }

    /// <summary>
    /// A crowd's cheer, synthesised (no recording to licence): band-limited noise shaped like many voices rising at once,
    /// with a few whistles, swelling for a second and fading over four.
    /// </summary>
    public static class CheerSound
    {
        public static float[] Generate(int sampleRate, int seed, float seconds = 5f)
        {
            var random = new Random(seed);
            int n = (int)(sampleRate * seconds);
            var data = new float[n];
            float low = 0f, band = 0f;
            const float cut = 0.12f, q = 0.35f;
            var whistles = new (float start, float length, float freq)[4];
            for (int w = 0; w < whistles.Length; w++)
            {
                whistles[w] = (0.3f + (float)random.NextDouble() * 2.5f, 0.4f + (float)random.NextDouble() * 0.5f, 1400f + (float)random.NextDouble() * 1200f);
            }

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);

                // A state-variable band pass around voice frequencies.
                float high = white - low - q * band;
                band += cut * high;
                low += cut * band;
                float envelope = t < 1f ? t : (float)Math.Exp(-(t - 1f) * 0.9f);
                float flutter = 0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * (3.1 + 0.7 * Math.Sin(t)) * t);
                float s = band * 1.8f * envelope * flutter;
                foreach ((float start, float length, float freq) in whistles)
                {
                    if (t >= start && t < start + length)
                    {
                        float u = (t - start) / length;
                        s += 0.12f * (float)Math.Sin(2 * Math.PI * freq * (1f + 0.15f * u) * t) * (float)Math.Sin(Math.PI * u);
                    }
                }

                data[i] = s;
            }

            float peak = 0f;
            foreach (float s in data)
            {
                peak = Math.Max(peak, Math.Abs(s));
            }

            float gain = peak > 0f ? 0.8f / peak : 1f;
            for (int i = 0; i < n; i++)
            {
                data[i] *= gain;
            }

            return data;
        }
    }
}
