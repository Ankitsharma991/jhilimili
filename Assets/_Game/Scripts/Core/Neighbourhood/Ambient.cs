using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    public enum NightPhase
    {
        Dusk = 0,
        Evening = 1,
        Night = 2,
        Late = 3,
    }

    /// <summary>How the night looks and feels at a moment (PRD 19 Phase 8: the Night Timeline).</summary>
    public readonly struct NightState
    {
        public NightState(NightPhase phase, float skyExposure, float ambient, float burstsPerMinute, float people, string clock)
        {
            Phase = phase;
            SkyExposure = skyExposure;
            Ambient = ambient;
            BurstsPerMinute = burstsPerMinute;
            People = people;
            Clock = clock;
        }

        public NightPhase Phase { get; }

        /// <summary>Multiplier on the night sky's exposure: brighter at dusk, darkest late.</summary>
        public float SkyExposure { get; }

        /// <summary>Multiplier on the scene's ambient light.</summary>
        public float Ambient { get; }

        /// <summary>Neighbours' distant bursts per minute before the tier and Quiet mode.</summary>
        public float BurstsPerMinute { get; }

        /// <summary>Share (0 to 1) of the tier's neighbours and crowd who are out.</summary>
        public float People { get; }

        /// <summary>The time the night shows, for the HUD ("7:30 pm").</summary>
        public string Clock { get; }
    }

    /// <summary>
    /// The Night Timeline (Phase 8 step 5): over a session's first <see cref="Length"/> minutes of play the evening moves from
    /// dusk to late night: the sky darkens, neighbours come out, distant bursts build to a peak and then thin out; after that it
    /// holds at late night. Keyframes are linear between; the clock runs 6:30 pm to 1 am.
    /// </summary>
    public static class NightTimeline
    {
        public const float Length = 24f;

        private static readonly (float minute, NightPhase phase, float sky, float ambient, float bursts, float people)[] Keys =
        {
            (0f, NightPhase.Dusk, 1.3f, 1.35f, 2f, 0.5f),
            (3f, NightPhase.Evening, 1.12f, 1.15f, 4f, 0.9f),
            (8f, NightPhase.Night, 1f, 1f, 8f, 1f),
            (18f, NightPhase.Late, 0.95f, 0.95f, 6f, 0.8f),
            (24f, NightPhase.Late, 0.9f, 0.9f, 2f, 0.4f),
        };

        public static NightState At(float minutes)
        {
            float m = Math.Max(0f, Math.Min(Length, minutes));
            int i = 0;
            while (i + 2 < Keys.Length && m >= Keys[i + 1].minute)
            {
                i++;
            }

            var a = Keys[i];
            var b = Keys[i + 1];
            float t = Math.Max(0f, Math.Min(1f, (m - a.minute) / (b.minute - a.minute)));
            NightPhase phase = m >= b.minute ? b.phase : a.phase;
            return new NightState(phase, Lerp(a.sky, b.sky, t), Lerp(a.ambient, b.ambient, t), Lerp(a.bursts, b.bursts, t), Lerp(a.people, b.people, t), Clock(m));
        }

        /// <summary>6:30 pm at the start, 1 am at the end.</summary>
        public static string Clock(float minutes)
        {
            int total = 18 * 60 + 30 + (int)(Math.Max(0f, Math.Min(Length, minutes)) / Length * 390f);
            int h = total / 60 % 24, mm = total % 60 / 15 * 15;
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{h12}:{mm:00} {(h < 12 ? "am" : "pm")}";
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>How much ambient life to show (PRD 10 "ambient activity is adaptive").</summary>
    public readonly struct AmbientPlan
    {
        public AmbientPlan(float burstsPerMinute, int neighbours, int crowd)
        {
            BurstsPerMinute = burstsPerMinute;
            Neighbours = neighbours;
            Crowd = crowd;
        }

        public float BurstsPerMinute { get; }

        public int Neighbours { get; }

        public int Crowd { get; }

        /// <summary>Most neighbours and crowd per tier (low, mid, high): none on low, a few on mid, a crowd on high only.</summary>
        public static readonly int[] MaxNeighbours = { 0, 3, 6 };
        public static readonly int[] MaxCrowd = { 0, 0, 12 };

        /// <summary>
        /// Lit windows and string lights are always on. Distant bursts are on every tier; looped neighbours on mid and high;
        /// crowds on high only; Quiet mode turns the bursts and people off; low memory keeps only two neighbours and no crowd.
        /// </summary>
        public static AmbientPlan For(int tier, bool quiet, NightState night, bool lowMemory = false)
        {
            if (quiet)
            {
                return new AmbientPlan(0f, 0, 0);
            }

            int t = Math.Max(0, Math.Min(2, tier));
            int neighbours = (int)Math.Round(MaxNeighbours[t] * night.People);
            int crowd = (int)Math.Round(MaxCrowd[t] * night.People);
            if (lowMemory)
            {
                neighbours = Math.Min(neighbours, 2);
                crowd = 0;
            }

            float bursts = night.BurstsPerMinute * (t == 0 ? 0.6f : 1f);
            return new AmbientPlan(bursts, neighbours, crowd);
        }
    }

    /// <summary>One distant burst: where (world), how big, and which colour of the palette.</summary>
    public readonly struct DistantBurst
    {
        public DistantBurst(V3 position, float size, int colour)
        {
            Position = position;
            Size = size;
            Colour = colour;
        }

        public V3 Position { get; }

        public float Size { get; }

        public int Colour { get; }
    }

    /// <summary>
    /// Neighbours' fireworks far beyond the settlement (PRD 10 "distant bursts always"): a seeded schedule at a rate per
    /// minute, each burst somewhere in a band of sky past the far lots, high enough to stay above every roof.
    /// </summary>
    public sealed class DistantBursts
    {
        public const int Colours = 6;

        private readonly Random _rng;
        private float _wait;

        public DistantBursts(int seed) => _rng = new Random(seed);

        /// <summary>Advances the schedule; adds any bursts due in this time to <paramref name="due"/> (reused, not allocated).</summary>
        public void Tick(float deltaSeconds, float perMinute, List<DistantBurst> due)
        {
            if (perMinute <= 0f)
            {
                return;
            }

            _wait -= deltaSeconds;
            while (_wait <= 0f)
            {
                // Exponential gaps: bursts arrive at random, averaging the rate.
                double mean = 60.0 / perMinute;
                _wait += (float)(-Math.Log(1.0 - _rng.NextDouble() * 0.999) * mean);
                float x = (float)(_rng.NextDouble() * 140 - 70);
                float z = (float)(55 + _rng.NextDouble() * 50);
                float y = (float)(26 + _rng.NextDouble() * 22);
                due.Add(new DistantBurst(new V3(x, y, z), (float)(6 + _rng.NextDouble() * 6), _rng.Next(Colours)));
            }
        }
    }

    /// <summary>Where ambient people stand (PRD 10): a spot in a neighbour lot's courtyard, or in the crowd on the open space.</summary>
    public readonly struct AmbientAnchor
    {
        public AmbientAnchor(V3 position, float yaw, bool crowd)
        {
            Position = position;
            Yaw = yaw;
            Crowd = crowd;
        }

        public V3 Position { get; }

        public float Yaw { get; }

        public bool Crowd { get; }
    }

    public static class AmbientAnchors
    {
        /// <summary>
        /// One anchor per built neighbour lot (in its courtyard, by its house, facing its gate) and a ring of crowd anchors on
        /// the open space, opposite the open space's celebration spot. Never inside a wall or a spot's play area.
        /// </summary>
        public static List<AmbientAnchor> For(SettlementLayout layout)
        {
            var anchors = new List<AmbientAnchor>();
            int i = 0;
            foreach (LotDef lot in layout.lots)
            {
                if (lot.home || lot.vacant)
                {
                    continue;
                }

                float side = i++ % 2 == 0 ? -1.4f : 1.2f;
                V3 p = lot.ToWorld(new V3(side, 0f, 2.4f));
                anchors.Add(new AmbientAnchor(p, lot.yaw, false));
            }

            SpotDef open = layout.spots.Find(s => s.id == "open");
            float cx = open != null && open.x > 0f ? -8f : 8f;
            float cz = (layout.open.z0 + layout.open.z1) / 2f;
            for (int k = 0; k < AmbientPlan.MaxCrowd[2]; k++)
            {
                double a = 2 * Math.PI * k / AmbientPlan.MaxCrowd[2];
                float r = k % 2 == 0 ? 2.2f : 3.1f;
                var p = new V3(cx + r * (float)Math.Cos(a), 0f, cz + r * (float)Math.Sin(a) * 0.7f);

                // Facing the middle of the ring, where their own fountain would be.
                float yaw = (float)(Math.Atan2(cx - p.X, cz - p.Z) * 180.0 / Math.PI);
                anchors.Add(new AmbientAnchor(p, yaw, true));
            }

            return anchors;
        }
    }
}
