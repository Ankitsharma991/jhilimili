using System;
using System.Collections.Generic;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.District
{
    /// <summary>An axis-aligned rectangle on the ground (world x and z, metres).</summary>
    public readonly struct Area
    {
        public Area(float x0, float z0, float x1, float z1)
        {
            X0 = Math.Min(x0, x1);
            Z0 = Math.Min(z0, z1);
            X1 = Math.Max(x0, x1);
            Z1 = Math.Max(z0, z1);
        }

        public float X0 { get; }

        public float Z0 { get; }

        public float X1 { get; }

        public float Z1 { get; }

        public float Width => X1 - X0;

        public float Depth => Z1 - Z0;

        public float CentreX => (X0 + X1) / 2f;

        public float CentreZ => (Z0 + Z1) / 2f;

        public bool Contains(float x, float z, float pad = 0f) => x >= X0 - pad && x <= X1 + pad && z >= Z0 - pad && z <= Z1 + pad;

        public bool Overlaps(Area o, float gap = 0f) => X0 < o.X1 + gap && X1 > o.X0 - gap && Z0 < o.Z1 + gap && Z1 > o.Z0 - gap;

        public Area Grow(float d) => new Area(X0 - d, Z0 - d, X1 + d, Z1 + d);
    }

    public enum RoadKind
    {
        Main,
        Lane,
        Promenade,
    }

    public sealed class DistrictRoad
    {
        public DistrictRoad(string name, Area area, RoadKind kind)
        {
            Name = name;
            Area = area;
            Kind = kind;
        }

        public string Name { get; }

        public Area Area { get; }

        public RoadKind Kind { get; }
    }

    /// <summary>Which way a building's front faces, onto its road.</summary>
    public enum Facing
    {
        North,
        South,
        East,
        West,
    }

    public sealed class DistrictBuilding
    {
        public int Id;
        public Area Footprint;
        public int Floors;
        public Facing Front;

        /// <summary>The wall colour, as one of the district's wall paints.</summary>
        public HousePaint Wall;

        public bool Shop;
        public HousePaint Awning;
        public bool Balcony;
        public bool Tank;
        public int Seed;

        public float Height => Floors * DistrictPlan.FloorHeight + 0.6f;
    }

    /// <summary>A small thing on the street: a market stall or a lamp post.</summary>
    public sealed class DistrictProp
    {
        public DistrictProp(string kind, float x, float z, Facing front, int seed)
        {
            Kind = kind;
            X = x;
            Z = z;
            Front = front;
            Seed = seed;
        }

        public const string Stall = "stall";
        public const string Lamp = "lamp";

        /// <summary>The clock tower in the middle of the chowk.</summary>
        public const string Tower = "tower";

        public string Kind { get; }

        public float X { get; }

        public float Z { get; }

        public Facing Front { get; }

        public int Seed { get; }

        public Area Footprint => Kind == Stall ? new Area(X - 1.1f, Z - 0.8f, X + 1.1f, Z + 0.8f)
            : Kind == Tower ? new Area(X - 2.2f, Z - 2.2f, X + 2.2f, Z + 2.2f)
            : new Area(X - 0.15f, Z - 0.15f, X + 0.15f, Z + 0.15f);

        /// <summary>Stalls and the tower block the way; lamp posts are thin enough to pass.</summary>
        public bool Solid => Kind != Lamp;
    }

    /// <summary>
    /// The district (Phase 12 step 3): an original bazaar town joined to the east end of the player's lane. Its main road
    /// carries the lane's street on; a second main road crosses it at the chowk, a square with a clock tower; lanes divide
    /// the blocks; a promenade and ghat steps run along the river on the north side. Buildings line every road with their
    /// fronts to it. World space, metres; the lane's street is z 3.8 to 9.8 and ends at x 29.4.
    /// </summary>
    public sealed class DistrictPlan
    {
        public const float FloorHeight = 3.1f;
        public const float TileSize = 50f;

        public Area Bounds;
        public Area Square;
        public Area Ghat;
        public Area River;
        public Area Join;
        public readonly List<DistrictRoad> Roads = new List<DistrictRoad>();
        public readonly List<DistrictBuilding> Buildings = new List<DistrictBuilding>();
        public readonly List<DistrictProp> Props = new List<DistrictProp>();

        public int TilesX => (int)Math.Ceiling(Bounds.Width / TileSize);

        public int TilesZ => (int)Math.Ceiling(Bounds.Depth / TileSize);

        public (int x, int z) TileOf(float x, float z) =>
            ((int)Math.Floor((x - Bounds.X0) / TileSize), (int)Math.Floor((z - Bounds.Z0) / TileSize));

        public Area TileArea(int tx, int tz) =>
            new Area(Bounds.X0 + tx * TileSize, Bounds.Z0 + tz * TileSize, Bounds.X0 + (tx + 1) * TileSize, Bounds.Z0 + (tz + 1) * TileSize);

        /// <summary>The tile a building or prop belongs to: the one its centre is in.</summary>
        public (int x, int z) TileOf(Area a) => TileOf(a.CentreX, a.CentreZ);

        /// <summary>Whether a point can be walked on: inside the district, out of the river, buildings and stalls.</summary>
        public bool IsWalkable(float x, float z, float radius = 0.3f)
        {
            if (!Bounds.Contains(x, z) || River.Contains(x, z, radius))
            {
                return false;
            }

            (int tx, int tz) = TileOf(x, z);
            Index();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!_byTile.TryGetValue(Key(tx + dx, tz + dz), out List<int> list))
                    {
                        continue;
                    }

                    for (int k = 0; k < list.Count; k++)
                    {
                        if (Buildings[list[k]].Footprint.Contains(x, z, radius))
                        {
                            return false;
                        }
                    }
                }
            }

            for (int k = 0; k < Props.Count; k++)
            {
                if (Props[k].Solid && Props[k].Footprint.Contains(x, z, radius))
                {
                    return false;
                }
            }

            return true; // allocation-free: the crowd asks this for every step
        }

        /// <summary>
        /// Phase 12 step 6: walkable at a height. On the ground as <see cref="IsWalkable"/>; up on a flat roof (within its
        /// parapet), where a job takes the player.
        /// </summary>
        public bool IsWalkableAt(float x, float y, float z, float radius = 0.3f)
        {
            if (y < 1f)
            {
                return IsWalkable(x, z, radius);
            }

            DistrictBuilding b = RoofAt(x, z, y);
            return b != null && b.Footprint.Contains(x, z, -(0.2f + radius));
        }

        /// <summary>The building whose roof is at about height <paramref name="y"/> under a point, or null.</summary>
        public DistrictBuilding RoofAt(float x, float z, float y)
        {
            (int tx, int tz) = TileOf(x, z);
            Index();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!_byTile.TryGetValue(Key(tx + dx, tz + dz), out List<int> list))
                    {
                        continue;
                    }

                    for (int k = 0; k < list.Count; k++)
                    {
                        DistrictBuilding b = Buildings[list[k]];
                        if (b.Footprint.Contains(x, z) && y - b.Height > -0.6f && y - b.Height < 1.2f) // room for a jump
                        {
                            return b;
                        }
                    }
                }
            }

            return null;
        }

        public bool OnRoad(float x, float z)
        {
            foreach (DistrictRoad r in Roads)
            {
                if (r.Area.Contains(x, z))
                {
                    return true;
                }
            }

            return Square.Contains(x, z);
        }

        private Dictionary<long, List<int>> _byTile;

        /// <summary>Buildings in a tile and its neighbours (a building can overhang a tile edge).</summary>
        public IEnumerable<int> Near(int tx, int tz)
        {
            Index();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (_byTile.TryGetValue(Key(tx + dx, tz + dz), out List<int> list))
                    {
                        foreach (int i in list)
                        {
                            yield return i;
                        }
                    }
                }
            }
        }

        private void Index()
        {
            if (_byTile == null)
            {
                _byTile = new Dictionary<long, List<int>>();
                for (int i = 0; i < Buildings.Count; i++)
                {
                    (int bx, int bz) = TileOf(Buildings[i].Footprint);
                    long key = Key(bx, bz);
                    if (!_byTile.TryGetValue(key, out List<int> list))
                    {
                        _byTile[key] = list = new List<int>();
                    }

                    list.Add(i);
                }
            }
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }

    /// <summary>Makes the bazaar district, the same every time from its seed.</summary>
    public static class DistrictGenerator
    {
        public const string BazaarId = "bazaar";

        /// <summary>The wall paints a building may take (DistrictPalette gives their colours).</summary>
        public static readonly HousePaint[] WallPaints =
        {
            HousePaint.Wall, HousePaint.Brick, HousePaint.Clay, HousePaint.Powder1, HousePaint.Powder2, HousePaint.Powder3, HousePaint.Powder4,
        };

        public static readonly HousePaint[] AwningPaints = { HousePaint.Fabric, HousePaint.Marigold, HousePaint.MarigoldAlt };

        private static DistrictPlan _bazaar;

        public static DistrictPlan Bazaar => _bazaar ??= Make(2027);

        public static DistrictPlan Make(int seed)
        {
            var random = new Random(seed);
            var p = new DistrictPlan
            {
                Bounds = new Area(28.9f, -140f, 328.9f, 160f),
                Square = new Area(150f, -18f, 200f, 32f),
                Ghat = new Area(28.9f, 118f, 328.9f, 125f),
                River = new Area(20f, 125f, 340f, 175f),
                Join = new Area(28.9f, 3.8f, 34f, 9.8f),
            };

            // Roads: the main road carries the lane's street on east; the second crosses it at the square.
            p.Roads.Add(new DistrictRoad("Bazaar road", new Area(28.9f, 2f, 328.9f, 12f), RoadKind.Main));
            p.Roads.Add(new DistrictRoad("Chowk road", new Area(170f, -140f, 180f, 110f), RoadKind.Main));
            p.Roads.Add(new DistrictRoad("River promenade", new Area(28.9f, 110f, 328.9f, 118f), RoadKind.Promenade));
            foreach (float z in new[] { -95f, -50f, 55f })
            {
                p.Roads.Add(new DistrictRoad("Lane", new Area(28.9f, z, 328.9f, z + 5f), RoadKind.Lane));
            }

            foreach (float x in new[] { 75f, 125f, 225f, 275f })
            {
                p.Roads.Add(new DistrictRoad("Lane", new Area(x, -140f, x + 5f, 110f), RoadKind.Lane));
            }

            // Blocks: the rectangles between roads; buildings line each block's edges, fronts to the road.
            var xs = new List<float> { p.Bounds.X0 };
            var zs = new List<float> { p.Bounds.Z0 };
            foreach (DistrictRoad r in p.Roads)
            {
                if (r.Area.Width < r.Area.Depth)
                {
                    xs.Add(r.Area.X0);
                    xs.Add(r.Area.X1);
                }
                else
                {
                    zs.Add(r.Area.Z0);
                    zs.Add(r.Area.Z1);
                }
            }

            xs.Add(p.Bounds.X1);
            xs.Sort();
            zs.Sort();
            int id = 1;
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                for (int j = 0; j + 1 < zs.Count; j++)
                {
                    var block = new Area(xs[i], zs[j], xs[i + 1], zs[j + 1]);
                    if (block.Width < 8f || block.Depth < 8f || p.OnRoad(block.CentreX, block.CentreZ) || block.Z0 >= 110f)
                    {
                        continue;
                    }

                    FillBlock(p, block, random, ref id);
                }
            }

            p.Props.Add(new DistrictProp(DistrictProp.Tower, p.Square.CentreX, p.Square.CentreZ, Facing.South, 0));

            // Market stalls along the bazaar road, and lamps along the main roads and the promenade.
            for (float x = 40f; x < 146f; x += 7f)
            {
                p.Props.Add(new DistrictProp(DistrictProp.Stall, x, 3.1f, Facing.North, random.Next()));
                p.Props.Add(new DistrictProp(DistrictProp.Stall, x + 3.5f, 10.9f, Facing.South, random.Next()));
            }

            for (float x = 36f; x < 328f; x += 16f)
            {
                if (!p.Square.Contains(x, 7f, 2f))
                {
                    p.Props.Add(new DistrictProp(DistrictProp.Lamp, x, 2.2f, Facing.North, 0));
                }

                p.Props.Add(new DistrictProp(DistrictProp.Lamp, x, 110.4f, Facing.North, 0));
            }

            for (float z = -130f; z < 108f; z += 16f)
            {
                if (!p.Square.Contains(175f, z, 2f) && Math.Abs(z - 7f) > 8f)
                {
                    p.Props.Add(new DistrictProp(DistrictProp.Lamp, 170.4f, z, Facing.East, 0));
                }
            }

            return p;
        }

        /// <summary>Buildings around a block's edges, each facing the road beside it; the middle is back yards.</summary>
        private static void FillBlock(DistrictPlan p, Area block, Random random, ref int id)
        {
            float maxDepth = Math.Min(14f, Math.Min(block.Width, block.Depth) / 2f - 0.5f);
            if (maxDepth < 6f)
            {
                maxDepth = Math.Min(block.Width, block.Depth) - 1f;
            }

            foreach (Facing side in new[] { Facing.South, Facing.North, Facing.West, Facing.East })
            {
                bool alongX = side == Facing.South || side == Facing.North;
                float start = alongX ? block.X0 : block.Z0, end = alongX ? block.X1 : block.Z1;
                float t = start + 0.4f;
                while (t < end - 4f)
                {
                    float width = Math.Min(end - 0.4f - t, 6f + (float)random.NextDouble() * 5f);
                    float depth = Math.Max(5f, maxDepth - (float)random.NextDouble() * 4f);
                    if (width < 4f)
                    {
                        break;
                    }

                    Area f = side switch
                    {
                        Facing.South => new Area(t, block.Z0 + 0.5f, t + width, block.Z0 + 0.5f + depth),
                        Facing.North => new Area(t, block.Z1 - 0.5f - depth, t + width, block.Z1 - 0.5f),
                        Facing.West => new Area(block.X0 + 0.5f, t, block.X0 + 0.5f + depth, t + width),
                        _ => new Area(block.X1 - 0.5f - depth, t, block.X1 - 0.5f, t + width),
                    };

                    var candidate = new DistrictBuilding { Footprint = f, Front = side };
                    if (Fits(p, f) && DistrictLint.Fronts(p, candidate))
                    {
                        bool main = NearMain(p, f);
                        int floors = main ? 2 + random.Next(3) : 1 + random.Next(3);
                        p.Buildings.Add(new DistrictBuilding
                        {
                            Id = id++,
                            Footprint = f,
                            Floors = floors,
                            Front = side,
                            Wall = WallPaints[random.Next(WallPaints.Length)],
                            Shop = main || random.NextDouble() < 0.4,
                            Awning = AwningPaints[random.Next(AwningPaints.Length)],
                            Balcony = floors >= 2 && random.NextDouble() < 0.55,
                            Tank = random.NextDouble() < 0.45,
                            Seed = random.Next(),
                        });
                    }

                    t += width + 0.4f + (random.NextDouble() < 0.15 ? 2.5f : 0f); // now and then a gap to a back yard
                }
            }
        }

        private static bool Fits(DistrictPlan p, Area f)
        {
            if (f.Overlaps(p.Square, 0.5f) || f.Overlaps(p.Ghat) || f.Overlaps(p.Join.Grow(3f)))
            {
                return false;
            }

            foreach (DistrictRoad r in p.Roads)
            {
                if (f.Overlaps(r.Area))
                {
                    return false;
                }
            }

            foreach (DistrictBuilding b in p.Buildings)
            {
                if (f.Overlaps(b.Footprint, 0.3f))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool NearMain(DistrictPlan p, Area f)
        {
            foreach (DistrictRoad r in p.Roads)
            {
                if (r.Kind == RoadKind.Main && f.Overlaps(r.Area, 1f))
                {
                    return true;
                }
            }

            return f.Overlaps(p.Square, 1.5f);
        }
    }

    /// <summary>
    /// What makes a district playable (Phase 12 step 3): it joins the lane, every road is reachable from the join, no
    /// building sits on a road, the square, the ghat or another building, every building fronts a road, and every tile
    /// stays inside the triangle budget at full detail.
    /// </summary>
    public static class DistrictLint
    {
        public const int TileTriangleBudget = 30000;

        public static List<string> Check(DistrictPlan p)
        {
            var problems = new List<string>();
            if (!p.Bounds.Contains(p.Join.X0, p.Join.CentreZ) || !p.IsWalkable(p.Join.CentreX, p.Join.CentreZ))
            {
                problems.Add("the lane's street doesn't open into the district");
            }

            // Roads joined to each other (and so to the join, on the bazaar road).
            var reached = new HashSet<DistrictRoad>();
            var queue = new Queue<DistrictRoad>();
            DistrictRoad first = p.Roads.Find(r => r.Area.Contains(p.Join.CentreX, p.Join.CentreZ, 1f));
            if (first == null)
            {
                problems.Add("no road at the join");
            }
            else
            {
                queue.Enqueue(first);
                reached.Add(first);
                while (queue.Count > 0)
                {
                    DistrictRoad r = queue.Dequeue();
                    foreach (DistrictRoad o in p.Roads)
                    {
                        if (!reached.Contains(o) && (r.Area.Overlaps(o.Area, 0.01f) || (r.Area.Overlaps(p.Square) && o.Area.Overlaps(p.Square))))
                        {
                            reached.Add(o);
                            queue.Enqueue(o);
                        }
                    }
                }

                foreach (DistrictRoad r in p.Roads)
                {
                    if (!reached.Contains(r))
                    {
                        problems.Add($"{r.Name} at {r.Area.CentreX:0},{r.Area.CentreZ:0} can't be reached");
                    }
                }
            }

            for (int i = 0; i < p.Buildings.Count; i++)
            {
                DistrictBuilding b = p.Buildings[i];
                if (b.Footprint.Overlaps(p.Square) || b.Footprint.Overlaps(p.Ghat) || b.Footprint.Overlaps(p.River))
                {
                    problems.Add($"building {b.Id} is on the square, the ghat or the river");
                }

                foreach (DistrictRoad r in p.Roads)
                {
                    if (b.Footprint.Overlaps(r.Area))
                    {
                        problems.Add($"building {b.Id} is on {r.Name}");
                    }
                }

                for (int j = i + 1; j < p.Buildings.Count; j++)
                {
                    if (b.Footprint.Overlaps(p.Buildings[j].Footprint))
                    {
                        problems.Add($"buildings {b.Id} and {p.Buildings[j].Id} overlap");
                    }
                }

                if (!Fronts(p, b))
                {
                    problems.Add($"building {b.Id} doesn't face a road");
                }
            }

            for (int tx = 0; tx < p.TilesX; tx++)
            {
                for (int tz = 0; tz < p.TilesZ; tz++)
                {
                    int tris = DistrictKit.Tile(p, tx, tz, simple: false).TriangleCount;
                    if (tris > TileTriangleBudget)
                    {
                        problems.Add($"tile {tx},{tz} has {tris} triangles (budget {TileTriangleBudget})");
                    }
                }
            }

            return problems;
        }

        /// <summary>A point a step out from the middle of the building's front is on a road (or the square or promenade).</summary>
        public static bool Fronts(DistrictPlan p, DistrictBuilding b)
        {
            Area f = b.Footprint;
            (float x, float z) = b.Front switch
            {
                Facing.South => (f.CentreX, f.Z0 - 1.2f),
                Facing.North => (f.CentreX, f.Z1 + 1.2f),
                Facing.West => (f.X0 - 1.2f, f.CentreZ),
                _ => (f.X1 + 1.2f, f.CentreZ),
            };
            return p.OnRoad(x, z);
        }
    }

    /// <summary>Which tiles to draw and how (Phase 12 step 3): full with colliders near the player, simple farther, none beyond.</summary>
    public enum TileDetail
    {
        Hidden,
        Simple,
        Full,
    }

    public static class TilePlanner
    {
        /// <summary>Full detail within this many metres of the player, by tier (low, mid, high).</summary>
        public static readonly float[] FullRange = { 0f, 60f, 80f };

        /// <summary>Simple detail out to this many metres.</summary>
        public static readonly float[] SimpleRange = { 0f, 130f, 170f };

        public static TileDetail Detail(DistrictPlan p, int tx, int tz, float x, float z, int tier)
        {
            int t = Math.Max(0, Math.Min(FullRange.Length - 1, tier));
            if (SimpleRange[t] <= 0f)
            {
                return TileDetail.Hidden; // the low tier keeps to the lane
            }

            Area a = p.TileArea(tx, tz);
            float dx = Math.Max(0f, Math.Max(a.X0 - x, x - a.X1));
            float dz = Math.Max(0f, Math.Max(a.Z0 - z, z - a.Z1));
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            return d <= FullRange[t] ? TileDetail.Full : d <= SimpleRange[t] ? TileDetail.Simple : TileDetail.Hidden;
        }
    }
}
