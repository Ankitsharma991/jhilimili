using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>How much of a chunk is shown (PRD 10: LODs, impostor billboards for distant houses, streaming radius).</summary>
    public enum ChunkDetail
    {
        /// <summary>Not built: nothing in memory.</summary>
        Unloaded = 0,

        /// <summary>Each house as one low box with lit windows; no decorations except their glow.</summary>
        Impostor = 1,

        /// <summary>Houses with their modules; decorations only where they glow (diyas, lights, lanterns).</summary>
        Simple = 2,

        /// <summary>Everything: modules, every decoration, colliders.</summary>
        Full = 3,
    }

    /// <summary>
    /// Decides the detail of every street chunk from the player's position along the street and the quality tier (Phase 8
    /// step 2, PRD 10 "additive chunks, memory bounded"). A chunk goes up in detail when the player comes within its
    /// radius and down only a little beyond it, so walking along a boundary does not rebuild chunks back and forth.
    /// </summary>
    public sealed class ChunkPlanner
    {
        /// <summary>Radii in chunks by tier (low, mid, high): full detail, simple, impostors; beyond that, unloaded.</summary>
        public static readonly int[] FullRadius = { 0, 1, 1 };
        public static readonly int[] SimpleRadius = { 1, 1, 2 };
        public static readonly int[] ImpostorRadius = { 3, 3, 4 };

        /// <summary>Metres past a radius before a chunk steps down.</summary>
        public const float Hysteresis = 2f;

        private readonly Dictionary<int, ChunkDetail> _current = new Dictionary<int, ChunkDetail>();
        private readonly List<int> _changed = new List<int>();

        public ChunkPlanner(int firstChunk, int lastChunk, int tier)
        {
            First = firstChunk;
            Last = lastChunk;
            Tier = Math.Max(0, Math.Min(2, tier));
        }

        public int First { get; }

        public int Last { get; }

        public int Tier { get; set; }

        public IReadOnlyDictionary<int, ChunkDetail> Current => _current;

        /// <summary>Decorations a chunk keeps at <see cref="ChunkDetail.Simple"/>: the ones that give light, which read from afar.</summary>
        public static bool KeptAtSimple(DecorInfo info) => info != null && (info.Category == DecorCategory.Lamps || info.Category == DecorCategory.Lights);

        public ChunkDetail DetailOf(int chunk) => _current.TryGetValue(chunk, out ChunkDetail d) ? d : ChunkDetail.Unloaded;

        /// <summary>
        /// Updates every chunk for the player at world x. Returns the chunks whose detail changed (the view rebuilds only those);
        /// the list is reused by the next call, so updating every few frames allocates nothing.
        /// <paramref name="lowMemory"/> (Application.lowMemory) drops everything but the player's chunk to impostors.
        /// </summary>
        public IReadOnlyList<int> Update(float playerX, bool lowMemory = false)
        {
            List<int> changed = _changed;
            changed.Clear();
            for (int c = First; c <= Last; c++)
            {
                float distance = Math.Abs(c * SettlementLayout.ChunkSize - playerX);
                ChunkDetail now = DetailOf(c);
                ChunkDetail want = Want(distance, lowMemory, now);
                if (want != now)
                {
                    changed.Add(c);
                    if (want == ChunkDetail.Unloaded)
                    {
                        _current.Remove(c);
                    }
                    else
                    {
                        _current[c] = want;
                    }
                }
            }

            return changed;
        }

        private ChunkDetail Want(float distance, bool lowMemory, ChunkDetail now)
        {
            float size = SettlementLayout.ChunkSize;
            ChunkDetail Level(float slack)
            {
                if (distance <= (FullRadius[Tier] + 0.5f) * size + slack && !lowMemory)
                {
                    return ChunkDetail.Full;
                }

                if (distance <= (SimpleRadius[Tier] + 0.5f) * size + slack && !lowMemory)
                {
                    return ChunkDetail.Simple;
                }

                if (lowMemory && distance <= 0.5f * size + slack)
                {
                    return ChunkDetail.Full;
                }

                return distance <= (ImpostorRadius[Tier] + 0.5f) * size + slack ? ChunkDetail.Impostor : ChunkDetail.Unloaded;
            }

            ChunkDetail strict = Level(0f), loose = Level(Hysteresis);

            // Step up as soon as the strict radius allows; step down only once even the loose radius no longer holds.
            if (strict > now)
            {
                return strict;
            }

            return loose < now ? loose : now;
        }
    }

    /// <summary>
    /// A distant house as a handful of boxes (PRD 10 "impostor billboards for distant houses"): the body, the roof shape and
    /// a lit quad for every window and door, in house space. About a hundred triangles instead of thousands.
    /// </summary>
    public static class ImpostorBuilder
    {
        public static HouseMesh Build(HouseDesign design)
        {
            var m = new HouseMesh();
            const float w = HouseGeometry.HalfWidth, d = HouseGeometry.Depth, top = HouseGeometry.Eaves;
            m.Box(-w, 0f, -d, w, top, 0f, HousePaint.Wall, Faces.Bottom);
            string roof = design.ModuleAt("roof")?.module ?? string.Empty;
            if (roof == "roof_tiled_slope")
            {
                m.QuadToward(new V3(-w - 0.2f, top, 0.3f), new V3(w + 0.2f, top, 0.3f), new V3(w + 0.2f, top + 1.7f, -d / 2f), new V3(-w - 0.2f, top + 1.7f, -d / 2f),
                    new V3(0, 1, 1), HousePaint.RoofTile);
                m.QuadToward(new V3(-w - 0.2f, top + 1.7f, -d / 2f), new V3(w + 0.2f, top + 1.7f, -d / 2f), new V3(w + 0.2f, top, -d - 0.3f), new V3(-w - 0.2f, top, -d - 0.3f),
                    new V3(0, 1, -1), HousePaint.RoofTile);
                foreach (float sx in new[] { -w, w })
                {
                    m.TriangleToward(new V3(sx, top, 0f), new V3(sx, top + 1.65f, -d / 2f), new V3(sx, top, -d), new V3(sx, 0, 0), HousePaint.Wall);
                }
            }
            else
            {
                m.Box(-w, top, -d, w, top + 0.7f, 0f, HousePaint.Wall, Faces.Bottom);
                m.Box(-w - 0.05f, top + 0.7f, -d, w + 0.05f, top + 0.78f, 0.05f, HousePaint.Trim, Faces.Bottom);
            }

            // Lit windows and the door, a hair in front of the wall.
            foreach (Rect2 o in HouseGeometry.FrontOpenings())
            {
                bool door = o.Y0 < 0.5f;
                m.Quad(new V3(o.X0, o.Y0, 0.01f), new V3(o.X1, o.Y0, 0.01f), new V3(o.X1, o.Y1, 0.01f), new V3(o.X0, o.Y1, 0.01f), new V3(0, 0, 1),
                    door ? HousePaint.Accent : HousePaint.Glass);
            }

            // The boundary wall and gate as one low band, so the courtyard reads from far away.
            const float c = HouseGeometry.BoundaryCentre, t = HouseGeometry.BoundaryThickness / 2f, back = HouseGeometry.BackWallZ;
            m.Box(c - t, 0f, 0f, c + t, HouseGeometry.BoundaryHeight, back, HousePaint.Wall, Faces.Bottom);
            m.Box(-c - t, 0f, 0f, -c + t, HouseGeometry.BoundaryHeight, back, HousePaint.Wall, Faces.Bottom);
            m.Box(-c - t, 0f, back - t, c + t, HouseGeometry.BoundaryHeight, back + t, HousePaint.Wall, Faces.Bottom);
            return m;
        }
    }
}
