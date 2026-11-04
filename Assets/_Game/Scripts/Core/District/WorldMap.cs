using System;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Core.District
{
    /// <summary>
    /// The town map's frame (Phase 12 step 6): the player's lane and the bazaar beside it, north up, in a picture of
    /// <see cref="Width"/> by <see cref="Height"/> pixels. Converts world points to the picture's 0..1 coordinates and back.
    /// </summary>
    public sealed class MapFrame
    {
        public const int Width = 512;

        public MapFrame(DistrictPlan plan)
        {
            X0 = -LaneHalf - 4f;
            X1 = plan.Bounds.X1 + 4f;
            Z0 = plan.Bounds.Z0 - 4f;
            Z1 = plan.River.Z0 + 20f;
            Height = (int)Math.Round(Width * (Z1 - Z0) / (X1 - X0));
        }

        /// <summary>Half the lane's street, either side of the player's home.</summary>
        public const float LaneHalf = 29.4f;

        public float X0 { get; }

        public float X1 { get; }

        public float Z0 { get; }

        public float Z1 { get; }

        public int Height { get; }

        public float MetresPerPixel => (X1 - X0) / Width;

        public (float u, float v) ToUV(float x, float z) => ((x - X0) / (X1 - X0), (z - Z0) / (Z1 - Z0));

        public (float x, float z) FromUV(float u, float v) => (X0 + u * (X1 - X0), Z0 + v * (Z1 - Z0));
    }

    /// <summary>
    /// Draws the town map as RGBA pixels (bottom row first, as textures load): dark ground, the river, light roads and
    /// paving, the buildings in their wall colours dimmed, the square and the player's home picked out in saffron.
    /// </summary>
    public static class MapRaster
    {
        public static readonly byte[] Ground = { 28, 26, 40, 255 };
        public static readonly byte[] Road = { 150, 140, 125, 255 };
        public static readonly byte[] Lane = { 112, 104, 96, 255 };
        public static readonly byte[] Paving = { 128, 118, 104, 255 };
        public static readonly byte[] Water = { 30, 62, 104, 255 };
        public static readonly byte[] Building = { 78, 70, 82, 255 };
        public static readonly byte[] Home = { 240, 150, 40, 255 };

        public static byte[] Draw(DistrictPlan plan, MapFrame frame)
        {
            int w = MapFrame.Width, h = frame.Height;
            var px = new byte[w * h * 4];
            Fill(px, frame, new Area(frame.X0, frame.Z0, frame.X1, frame.Z1), Ground);
            Fill(px, frame, plan.River, Water);
            Fill(px, frame, plan.Ghat, Paving);

            // The player's lane: its street, the houses either side and the open ground across it.
            Fill(px, frame, new Area(-MapFrame.LaneHalf, SettlementLibrary.StreetZ0, MapFrame.LaneHalf, SettlementLibrary.StreetZ1), Road);
            for (int k = -3; k <= 3; k++)
            {
                float x = k * SettlementLibrary.LotSpacing;
                Fill(px, frame, new Area(x - 3.4f, -7f, x + 3.4f, SettlementLibrary.StreetZ0 - 0.8f), k == 0 ? Home : Building);
            }

            foreach (DistrictRoad r in plan.Roads)
            {
                Fill(px, frame, r.Area, r.Kind == RoadKind.Lane ? Lane : r.Kind == RoadKind.Promenade ? Paving : Road);
            }

            Fill(px, frame, plan.Square, Paving);
            foreach (DistrictBuilding b in plan.Buildings)
            {
                Fill(px, frame, b.Footprint.Grow(-0.3f), Building);
            }

            return px;
        }

        private static void Fill(byte[] px, MapFrame f, Area a, byte[] c)
        {
            int w = MapFrame.Width, h = f.Height;
            (float u0, float v0) = f.ToUV(a.X0, a.Z0);
            (float u1, float v1) = f.ToUV(a.X1, a.Z1);
            int x0 = Math.Max(0, (int)Math.Floor(u0 * w)), x1 = Math.Min(w - 1, (int)Math.Ceiling(u1 * w) - 1);
            int y0 = Math.Max(0, (int)Math.Floor(v0 * h)), y1 = Math.Min(h - 1, (int)Math.Ceiling(v1 * h) - 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = (y * w + x) * 4;
                    px[i] = c[0];
                    px[i + 1] = c[1];
                    px[i + 2] = c[2];
                    px[i + 3] = c[3];
                }
            }
        }
    }

    /// <summary>
    /// Routes for the map and the minimap (Phase 12 step 6): along the player's lane to where it meets the bazaar, then
    /// along the district's footpaths (the crowd's walk graph) to the point. Written into the caller's buffers; finding
    /// one allocates nothing.
    /// </summary>
    public sealed class MapRoute
    {
        public const int MaxPoints = 96;

        private readonly WalkGraph _graph;
        private readonly DistrictPlan _plan;
        private readonly int[] _path = new int[MaxPoints];
        private readonly float[] _dist;
        private readonly int[] _prev;
        private readonly bool[] _done;

        public MapRoute(DistrictPlan plan, WalkGraph graph)
        {
            _plan = plan;
            _graph = graph;
            _dist = new float[graph.Count];
            _prev = new int[graph.Count];
            _done = new bool[graph.Count];
        }

        /// <summary>Whether a point is on the player's lane (west of where it meets the bazaar).</summary>
        public bool InLane(float x) => x < _plan.Join.X0 + 0.5f;

        /// <summary>The route from one point to another as points (x, z); returns how many. Both ends are included.</summary>
        public int Find(float fromX, float fromZ, float toX, float toZ, float[] xs, float[] zs)
        {
            int n = 0;
            Add(xs, zs, ref n, fromX, fromZ);
            bool fromLane = InLane(fromX), toLane = InLane(toX);
            float line = SettlementLibrary.StreetLine, joinX = _plan.Join.X1;
            if (fromLane && toLane)
            {
                Add(xs, zs, ref n, fromX, line);
                Add(xs, zs, ref n, toX, line);
                Add(xs, zs, ref n, toX, toZ);
                return n;
            }

            float ax = fromX, az = fromZ;
            if (fromLane)
            {
                Add(xs, zs, ref n, fromX, line);
                Add(xs, zs, ref n, joinX, line);
                ax = joinX;
                az = line;
            }

            float bx = toLane ? joinX : toX, bz = toLane ? line : toZ;
            int a = _graph.Nearest(ax, az), b = _graph.Nearest(bx, bz);
            int count = a == b ? 1 : _graph.Path(a, b, _path, _dist, _prev, _done);
            if (count <= 0)
            {
                _path[0] = b;
                count = 1;
            }

            for (int i = 0; i < count && n < MaxPoints - 4; i++)
            {
                (float x, float z) = _graph.Nodes[_path[i]];
                Add(xs, zs, ref n, x, z);
            }

            if (toLane)
            {
                Add(xs, zs, ref n, joinX, line);
                Add(xs, zs, ref n, toX, line);
            }

            Add(xs, zs, ref n, toX, toZ);
            return n;
        }

        /// <summary>The route's length in metres.</summary>
        public static float Length(float[] xs, float[] zs, int count)
        {
            float total = 0f;
            for (int i = 1; i < count; i++)
            {
                float dx = xs[i] - xs[i - 1], dz = zs[i] - zs[i - 1];
                total += (float)Math.Sqrt(dx * dx + dz * dz);
            }

            return total;
        }

        private static void Add(float[] xs, float[] zs, ref int n, float x, float z)
        {
            if (n >= xs.Length)
            {
                return;
            }

            xs[n] = x;
            zs[n] = z;
            n++;
        }
    }
}
