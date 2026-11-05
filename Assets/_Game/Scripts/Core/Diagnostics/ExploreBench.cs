using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UtsavNights.Core.Diagnostics
{
    /// <summary>One leg of the Explore bench: what was measured while it ran.</summary>
    public sealed class BenchLeg
    {
        public string Name = string.Empty;
        public float Seconds;
        public int Frames;
        public float AvgMs;
        public float P50Ms;
        public float P95Ms;
        public float P99Ms;
        public float MaxMs;

        /// <summary>The share of frames over twice the frame budget (PRD 14: under 5).</summary>
        public float SpikePct;

        /// <summary>Managed garbage per frame; -1 when the player can't measure it.</summary>
        public float GcKbAvg = -1f;

        public float GcKbMax = -1f;

        /// <summary>The largest the app's memory got during the leg; -1 when unknown.</summary>
        public float MemoryMb = -1f;

        /// <summary>The most crackers in the scene at once.</summary>
        public int Crackers;

        /// <summary>Crackers a show asked for that the scene's limit turned away.</summary>
        public int Refused;

        /// <summary>People simulated, and drawn, at the end of the leg.</summary>
        public int People;

        public int PeopleDrawn;

        public int Vehicles;

        public float AvgFps => AvgMs > 0f ? 1000f / AvgMs : 0f;
    }

    /// <summary>
    /// What the open world must hold to on a phone (Phase 12 step 8; PRD 14 and the open-world plan): 30 fps at least on
    /// every tier that has the town, under 5% of frames over twice the 30 fps budget, the GC guard's 4 KB a frame, and the
    /// tier's memory budget (700, 900, 1200 MB).
    /// </summary>
    public static class ExploreBudget
    {
        public const float TargetFps = 30f;
        public const float MaxSpikePct = 5f;
        public const float MaxGcKb = 4f;

        /// <summary>A spike: a frame over twice the 30 fps budget.</summary>
        public static float SpikeMs => 2f * 1000f / TargetFps;

        public static float MemoryMb(int tier) => tier <= 0 ? 700f : tier == 1 ? 900f : 1200f;

        /// <summary>The budgets a leg missed, in plain words (empty when it held to all of them).</summary>
        public static List<string> Misses(BenchLeg leg, int tier)
        {
            var misses = new List<string>();
            if (leg.Frames == 0)
            {
                misses.Add("no frames measured");
                return misses;
            }

            if (leg.AvgFps < TargetFps)
            {
                misses.Add($"{leg.AvgFps.ToString("0.0", CultureInfo.InvariantCulture)} fps, under {TargetFps:0}");
            }

            if (leg.SpikePct >= MaxSpikePct)
            {
                misses.Add($"{leg.SpikePct.ToString("0.0", CultureInfo.InvariantCulture)}% spikes, {MaxSpikePct:0}% at most");
            }

            if (leg.GcKbAvg > MaxGcKb)
            {
                misses.Add($"{leg.GcKbAvg.ToString("0.00", CultureInfo.InvariantCulture)} KB of garbage a frame, {MaxGcKb:0} at most");
            }

            if (leg.MemoryMb > MemoryMb(tier))
            {
                misses.Add($"{leg.MemoryMb:0} MB, {MemoryMb(tier):0} at most on this tier");
            }

            return misses;
        }

        /// <summary>Fills a leg's frame figures from its frame times in ms (any order; the array is sorted in place).</summary>
        public static void FillFrames(BenchLeg leg, float[] frameMs, int count)
        {
            leg.Frames = count;
            if (count <= 0)
            {
                return;
            }

            double sum = 0;
            int spikes = 0;
            for (int i = 0; i < count; i++)
            {
                sum += frameMs[i];
                if (frameMs[i] > SpikeMs)
                {
                    spikes++;
                }
            }

            Array.Sort(frameMs, 0, count);
            leg.AvgMs = (float)(sum / count);
            leg.P50Ms = Rank(frameMs, count, 50f);
            leg.P95Ms = Rank(frameMs, count, 95f);
            leg.P99Ms = Rank(frameMs, count, 99f);
            leg.MaxMs = frameMs[count - 1];
            leg.SpikePct = 100f * spikes / count;
        }

        /// <summary>One JSON line for the results file.</summary>
        public static string ToJson(BenchLeg leg, string run, string device, int tier)
        {
            List<string> misses = Misses(leg, tier);
            var sb = new StringBuilder(400);
            sb.Append("{\"run\":\"").Append(Clean(run)).Append("\",\"device\":\"").Append(Clean(device)).Append("\",\"tier\":").Append(tier)
                .Append(",\"leg\":\"").Append(Clean(leg.Name)).Append('"');
            Num(sb, "seconds", leg.Seconds);
            sb.Append(",\"frames\":").Append(leg.Frames);
            Num(sb, "avg_fps", leg.AvgFps);
            Num(sb, "avg_ms", leg.AvgMs);
            Num(sb, "p50_ms", leg.P50Ms);
            Num(sb, "p95_ms", leg.P95Ms);
            Num(sb, "p99_ms", leg.P99Ms);
            Num(sb, "max_ms", leg.MaxMs);
            Num(sb, "spike_pct", leg.SpikePct);
            Num(sb, "gc_kb_avg", leg.GcKbAvg);
            Num(sb, "gc_kb_max", leg.GcKbMax);
            Num(sb, "memory_mb", leg.MemoryMb);
            sb.Append(",\"crackers\":").Append(leg.Crackers).Append(",\"refused\":").Append(leg.Refused)
                .Append(",\"people\":").Append(leg.People).Append(",\"people_drawn\":").Append(leg.PeopleDrawn)
                .Append(",\"vehicles\":").Append(leg.Vehicles)
                .Append(",\"ok\":").Append(misses.Count == 0 ? "true" : "false").Append(",\"misses\":[");
            for (int i = 0; i < misses.Count; i++)
            {
                sb.Append(i > 0 ? "," : string.Empty).Append('"').Append(Clean(misses[i])).Append('"');
            }

            return sb.Append("]}").ToString();
        }

        private static float Rank(float[] sorted, int count, float p)
        {
            int rank = (int)Math.Ceiling(p / 100f * count) - 1;
            return sorted[Math.Max(0, Math.Min(count - 1, rank))];
        }

        private static void Num(StringBuilder sb, string key, float value) =>
            sb.Append(",\"").Append(key).Append("\":").Append(value.ToString("0.##", CultureInfo.InvariantCulture));

        private static string Clean(string s)
        {
            var sb = new StringBuilder(s?.Length ?? 0);
            foreach (char c in s ?? string.Empty)
            {
                if (c >= ' ' && c != '"' && c != '\\')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
