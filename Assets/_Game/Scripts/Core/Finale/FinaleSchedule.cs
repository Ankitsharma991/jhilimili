using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Finale
{
    /// <summary>One candidate for the finale: its position in the scene list, fuse length and climax rank.</summary>
    public readonly struct FinaleItem
    {
        public FinaleItem(int index, float fuseSeconds, int rank)
        {
            Index = index;
            FuseSeconds = fuseSeconds;
            Rank = rank;
        }

        public int Index { get; }

        public float FuseSeconds { get; }

        /// <summary>Lower goes earlier; aerials and the cake rank highest so the show builds to a climax.</summary>
        public int Rank { get; }
    }

    /// <summary>When one finale item is lit and when its payoff lands (seconds from the finale start).</summary>
    public readonly struct FinaleSlot
    {
        public FinaleSlot(int index, float igniteAt, float payoffAt)
        {
            Index = index;
            IgniteAt = igniteAt;
            PayoffAt = payoffAt;
        }

        public int Index { get; }

        public float IgniteAt { get; }

        public float PayoffAt { get; }
    }

    /// <summary>
    /// Finale mode v1 (F13: chain 3 to 6 items with fixed delays). Payoffs land at a fixed gap in climax order;
    /// ignitions are worked back from each fuse so different fuse lengths still hit the rhythm.
    /// </summary>
    public static class FinaleSchedule
    {
        public const int MinItems = 3;
        public const int MaxItems = 6;
        public const float DefaultGapSeconds = 1.4f;

        public static List<FinaleSlot> Build(IReadOnlyList<FinaleItem> items, float gapSeconds = DefaultGapSeconds)
        {
            var slots = new List<FinaleSlot>(MaxItems);
            if (items == null || items.Count < MinItems)
            {
                return slots;
            }

            var ordered = new List<FinaleItem>(items);
            ordered.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Index.CompareTo(b.Index));
            int count = Math.Min(MaxItems, ordered.Count);
            float gap = Math.Max(0f, gapSeconds);

            float earliest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float ignite = i * gap - Math.Max(0f, ordered[i].FuseSeconds);
                earliest = Math.Min(earliest, ignite);
            }

            for (int i = 0; i < count; i++)
            {
                float payoff = i * gap - earliest;
                float ignite = payoff - Math.Max(0f, ordered[i].FuseSeconds);
                slots.Add(new FinaleSlot(ordered[i].Index, ignite, payoff));
            }

            slots.Sort((a, b) => a.IgniteAt.CompareTo(b.IgniteAt));
            return slots;
        }
    }
}
