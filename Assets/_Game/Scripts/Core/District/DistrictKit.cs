using System;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.District
{
    /// <summary>
    /// The district's look (Phase 12 step 3): one mesh per tile, built from code like the house: the ground, roads and
    /// paving, the river and ghat steps, the buildings (full: floor bands, lit and dark windows with sills, shopfronts with
    /// shutters, counters and awnings, balconies, parapets and water tanks; simple: the walls and the lit windows only,
    /// for far tiles), market stalls, lamp posts and the clock tower. Each paint is one material across the whole tile.
    /// </summary>
    public static class DistrictKit
    {
        // The paints doing the district's other jobs (DistrictPalette gives their colours).
        public const HousePaint Road = HousePaint.Iron;
        public const HousePaint Paving = HousePaint.Stone;
        public const HousePaint Earth = HousePaint.Leaf;
        public const HousePaint Water = HousePaint.Accent;
        public const HousePaint Lit = HousePaint.Glass;
        public const HousePaint LampHead = HousePaint.Flame;

        public static HouseMesh Tile(DistrictPlan p, int tx, int tz, bool simple)
        {
            var m = new HouseMesh();
            Area t = p.TileArea(tx, tz);
            Ground(m, p, t);
            for (int i = 0; i < p.Buildings.Count; i++)
            {
                DistrictBuilding b = p.Buildings[i];
                if (p.TileOf(b.Footprint) == (tx, tz))
                {
                    Building(m, b, simple);
                }
            }

            foreach (DistrictProp prop in p.Props)
            {
                if (p.TileOf(prop.Footprint) != (tx, tz) || (simple && prop.Kind == DistrictProp.Stall))
                {
                    continue;
                }

                switch (prop.Kind)
                {
                    case DistrictProp.Stall:
                        Stall(m, prop);
                        break;
                    case DistrictProp.Tower:
                        Tower(m, prop, simple);
                        break;
                    default:
                        Lamp(m, prop, simple);
                        break;
                }
            }

            return m;
        }

        private static void Ground(HouseMesh m, DistrictPlan p, Area t)
        {
            Area land = Clip(t, p.Bounds);
            if (land.Width <= 0f || land.Depth <= 0f)
            {
                return;
            }

            // Packed earth under everything, then the roads, the square and the ghat on top (a hair higher each, no flicker).
            Area dry = Clip(land, new Area(p.Bounds.X0, p.Bounds.Z0, p.Bounds.X1, p.Ghat.Z0));
            Slab(m, dry, -0.05f, 0f, Earth);
            foreach (DistrictRoad r in p.Roads)
            {
                Area a = Clip(t, r.Area);
                if (a.Width > 0f && a.Depth > 0f)
                {
                    Slab(m, a, 0f, 0.012f, r.Kind == RoadKind.Promenade ? Paving : Road);
                }
            }

            Area sq = Clip(t, p.Square);
            if (sq.Width > 0f && sq.Depth > 0f)
            {
                Slab(m, sq, 0f, 0.02f, Paving);
            }

            // Ghat steps down to the water, and the river.
            Area ghat = Clip(t, p.Ghat);
            if (ghat.Width > 0f && ghat.Depth > 0f)
            {
                const int steps = 5;
                float run = p.Ghat.Depth / steps;
                for (int i = 0; i < steps; i++)
                {
                    var step = new Area(ghat.X0, p.Ghat.Z0 + i * run, ghat.X1, p.Ghat.Z0 + (i + 1) * run);
                    Area s = Clip(step, ghat);
                    if (s.Width > 0f && s.Depth > 0f)
                    {
                        Slab(m, s, -0.9f, -i * 0.15f, Paving);
                    }
                }
            }

            Area water = Clip(t, p.River);
            if (water.Width > 0f && water.Depth > 0f)
            {
                Slab(m, water, -1.2f, -0.85f, Water);
            }
        }

        private static void Building(HouseMesh m, DistrictBuilding b, bool simple)
        {
            var f = new Frame(b);
            float w = f.Width, d = f.Depth, h = b.Height, fh = DistrictPlan.FloorHeight;
            f.Box(m, 0f, 0f, 0f, w, h, d, b.Wall);
            int windows = Math.Max(1, (int)(w / 2.4f));
            var random = new Random(b.Seed);
            for (int floor = 0; floor < b.Floors; floor++)
            {
                float y0 = floor * fh;
                if (floor == 0 && b.Shop)
                {
                    if (!simple)
                    {
                        Shopfront(m, f, b, w);
                    }
                    else
                    {
                        f.Box(m, 0.5f, 0.2f, -0.03f, w - 0.5f, 2.6f, 0f, Lit);
                    }

                    continue;
                }

                for (int i = 0; i < windows; i++)
                {
                    float u = (i + 0.5f) * w / windows;
                    bool lit = random.NextDouble() < 0.62;
                    HousePaint pane = lit ? Lit : HousePaint.Dark;
                    if (simple && !lit)
                    {
                        continue;
                    }

                    f.Box(m, u - 0.55f, y0 + 0.9f, -0.04f, u + 0.55f, y0 + 2.3f, 0f, pane);
                    if (!simple)
                    {
                        f.Box(m, u - 0.65f, y0 + 0.8f, -0.14f, u + 0.65f, y0 + 0.9f, 0f, HousePaint.Trim);
                        f.Box(m, u - 0.65f, y0 + 2.3f, -0.1f, u + 0.65f, y0 + 2.42f, 0f, HousePaint.Trim);
                    }
                }

                if (!simple && floor > 0)
                {
                    f.Box(m, -0.05f, y0 - 0.1f, -0.1f, w + 0.05f, y0 + 0.05f, 0f, HousePaint.Trim);
                }
            }

            if (simple)
            {
                return;
            }

            if (b.Balcony)
            {
                float u0 = w * 0.25f, u1 = w * 0.75f, y = fh;
                f.Box(m, u0, y, -1.1f, u1, y + 0.15f, 0f, HousePaint.Trim);
                f.Box(m, u0, y + 0.15f, -1.1f, u1, y + 0.95f, -1.05f, HousePaint.Iron);
                f.Box(m, u0, y + 0.15f, -1.1f, u0 + 0.05f, y + 0.95f, 0f, HousePaint.Iron);
                f.Box(m, u1 - 0.05f, y + 0.15f, -1.1f, u1, y + 0.95f, 0f, HousePaint.Iron);
            }

            // Parapet round the flat roof, and a water tank on some.
            f.Box(m, 0f, h, 0f, w, h + 0.5f, 0.2f, HousePaint.Trim);
            f.Box(m, 0f, h, d - 0.2f, w, h + 0.5f, d, HousePaint.Trim);
            f.Box(m, 0f, h, 0.2f, 0.2f, h + 0.5f, d - 0.2f, HousePaint.Trim);
            f.Box(m, w - 0.2f, h, 0.2f, w, h + 0.5f, d - 0.2f, HousePaint.Trim);
            if (b.Tank)
            {
                V3 c = f.Point(w * 0.75f, h, d * 0.7f);
                m.Lathe(c.X, c.Z, new[] { 0.55f, h, 0.55f, h + 1.1f, 0.25f, h + 1.25f }, 8, HousePaint.RoofTile);
            }
        }

        private static void Shopfront(HouseMesh m, Frame f, DistrictBuilding b, float w)
        {
            f.Box(m, 0.5f, 0.15f, -0.03f, w - 0.5f, 2.7f, 0f, HousePaint.Dark);                // the open shop
            f.Box(m, 0.6f, 0.2f, -0.02f, w - 0.6f, 2.6f, 0.02f, Lit);                         // lit inside
            f.Box(m, 0.5f, 2.15f, -0.08f, w - 0.5f, 2.7f, -0.02f, HousePaint.Iron);            // shutter, half rolled
            f.Box(m, 0.8f, 0f, -0.9f, w - 0.8f, 0.85f, -0.3f, HousePaint.Wood);                // counter
            V3 a = f.Point(0.2f, 3.0f, 0f), c = f.Point(w - 0.2f, 3.0f, 0f);
            V3 bOut = f.Point(0.2f, 2.55f, -1.5f), dOut = f.Point(w - 0.2f, 2.55f, -1.5f);
            m.QuadToward(a, c, dOut, bOut, new V3(0f, 1f, 0f), b.Awning);
            m.QuadToward(a, bOut, dOut, c, new V3(0f, -1f, 0f), b.Awning);
            f.Box(m, 0.2f, 2.45f, -1.55f, w - 0.2f, 2.6f, -1.45f, HousePaint.Trim);
        }

        private static void Stall(HouseMesh m, DistrictProp s)
        {
            Area a = s.Footprint;
            m.Box(a.X0, 0f, a.Z0, a.X1, 0.85f, a.Z1, HousePaint.Wood);
            foreach ((float x, float z) in new[] { (a.X0, a.Z0), (a.X1 - 0.08f, a.Z0), (a.X0, a.Z1 - 0.08f), (a.X1 - 0.08f, a.Z1 - 0.08f) })
            {
                m.Box(x, 0.85f, z, x + 0.08f, 2.2f, z + 0.08f, HousePaint.Wood);
            }

            HousePaint canopy = DistrictGenerator.AwningPaints[Math.Abs(s.Seed) % DistrictGenerator.AwningPaints.Length];
            m.Box(a.X0 - 0.15f, 2.2f, a.Z0 - 0.15f, a.X1 + 0.15f, 2.28f, a.Z1 + 0.15f, canopy);
            var random = new Random(s.Seed);
            for (int i = 0; i < 4; i++)
            {
                float x = a.X0 + 0.25f + i * (a.Width - 0.5f) / 4f;
                HousePaint goods = random.NextDouble() < 0.5 ? HousePaint.Marigold : HousePaint.Clay;
                m.Box(x, 0.85f, a.CentreZ - 0.3f, x + 0.35f, 1.05f + (float)random.NextDouble() * 0.15f, a.CentreZ + 0.3f, goods);
            }
        }

        private static void Lamp(HouseMesh m, DistrictProp l, bool simple)
        {
            if (!simple)
            {
                m.Box(l.X - 0.06f, 0f, l.Z - 0.06f, l.X + 0.06f, 4.2f, l.Z + 0.06f, HousePaint.Iron);
            }

            m.Box(l.X - 0.2f, 4.2f, l.Z - 0.2f, l.X + 0.2f, 4.55f, l.Z + 0.2f, LampHead);
        }

        private static void Tower(HouseMesh m, DistrictProp t, bool simple)
        {
            float x = t.X, z = t.Z;
            m.Box(x - 2.2f, 0f, z - 2.2f, x + 2.2f, 1f, z + 2.2f, Paving);
            m.Box(x - 1.3f, 1f, z - 1.3f, x + 1.3f, 12f, z + 1.3f, HousePaint.Wall);
            m.Box(x - 1.5f, 12f, z - 1.5f, x + 1.5f, 12.4f, z + 1.5f, HousePaint.Trim);
            if (!simple)
            {
                m.Box(x - 1.4f, 4f, z - 1.4f, x + 1.4f, 4.2f, z + 1.4f, HousePaint.Trim);
                m.Box(x - 1.4f, 8f, z - 1.4f, x + 1.4f, 8.2f, z + 1.4f, HousePaint.Trim);
                m.Dome(x, 12.4f, z, 1.4f, 10, 5, HousePaint.RoofTile);
            }

            // The four clock faces, lit.
            m.Box(x - 0.7f, 9.2f, z - 1.36f, x + 0.7f, 10.6f, z - 1.3f, Lit);
            m.Box(x - 0.7f, 9.2f, z + 1.3f, x + 0.7f, 10.6f, z + 1.36f, Lit);
            m.Box(x - 1.36f, 9.2f, z - 0.7f, x - 1.3f, 10.6f, z + 0.7f, Lit);
            m.Box(x + 1.3f, 9.2f, z - 0.7f, x + 1.36f, 10.6f, z + 0.7f, Lit);
        }

        private static void Slab(HouseMesh m, Area a, float y0, float y1, HousePaint paint) => m.Box(a.X0, y0, a.Z0, a.X1, y1, a.Z1, paint);

        private static Area Clip(Area a, Area b) =>
            new Area(Math.Max(a.X0, b.X0), Math.Max(a.Z0, b.Z0), Math.Max(Math.Max(a.X0, b.X0), Math.Min(a.X1, b.X1)), Math.Max(Math.Max(a.Z0, b.Z0), Math.Min(a.Z1, b.Z1)));

        /// <summary>A building's own frame: u along its front, v back from the front, y up; turned to whichever way it faces.</summary>
        private readonly struct Frame
        {
            private readonly DistrictBuilding _b;

            public Frame(DistrictBuilding b) => _b = b;

            public float Width => _b.Front == Facing.North || _b.Front == Facing.South ? _b.Footprint.Width : _b.Footprint.Depth;

            public float Depth => _b.Front == Facing.North || _b.Front == Facing.South ? _b.Footprint.Depth : _b.Footprint.Width;

            public V3 Point(float u, float y, float v)
            {
                Area f = _b.Footprint;
                return _b.Front switch
                {
                    Facing.South => new V3(f.X0 + u, y, f.Z0 + v),
                    Facing.North => new V3(f.X1 - u, y, f.Z1 - v),
                    Facing.West => new V3(f.X0 + v, y, f.Z1 - u),
                    _ => new V3(f.X1 - v, y, f.Z0 + u),
                };
            }

            public void Box(HouseMesh m, float u0, float y0, float v0, float u1, float y1, float v1, HousePaint paint)
            {
                V3 a = Point(u0, y0, v0), b = Point(u1, y1, v1);
                m.Box(Math.Min(a.X, b.X), y0, Math.Min(a.Z, b.Z), Math.Max(a.X, b.X), y1, Math.Max(a.Z, b.Z), paint);
            }
        }
    }

    /// <summary>The district's colours, a bazaar at night: washed walls in a few colours, warm lit windows, dark roads.</summary>
    public static class DistrictPalette
    {
        public static Rgb Colour(HousePaint paint)
        {
            switch (paint)
            {
                case HousePaint.Wall: return new Rgb(0.86f, 0.8f, 0.68f);
                case HousePaint.Brick: return new Rgb(0.62f, 0.32f, 0.24f);
                case HousePaint.Clay: return new Rgb(0.82f, 0.57f, 0.28f);
                case HousePaint.Powder1: return new Rgb(0.86f, 0.56f, 0.6f);
                case HousePaint.Powder2: return new Rgb(0.56f, 0.78f, 0.66f);
                case HousePaint.Powder3: return new Rgb(0.52f, 0.68f, 0.86f);
                case HousePaint.Powder4: return new Rgb(0.92f, 0.8f, 0.42f);
                case HousePaint.Trim: return new Rgb(0.93f, 0.9f, 0.84f);
                case HousePaint.Glass: return new Rgb(1f, 0.72f, 0.38f);
                case HousePaint.Dark: return new Rgb(0.05f, 0.045f, 0.05f);
                case HousePaint.Iron: return new Rgb(0.17f, 0.17f, 0.19f);
                case HousePaint.Wood: return new Rgb(0.42f, 0.27f, 0.15f);
                case HousePaint.Fabric: return new Rgb(0.76f, 0.16f, 0.13f);
                case HousePaint.Marigold: return new Rgb(0.95f, 0.56f, 0.12f);
                case HousePaint.MarigoldAlt: return new Rgb(0.16f, 0.5f, 0.32f);
                case HousePaint.Stone: return new Rgb(0.52f, 0.47f, 0.41f);
                case HousePaint.Leaf: return new Rgb(0.32f, 0.27f, 0.2f);
                case HousePaint.Accent: return new Rgb(0.05f, 0.12f, 0.22f);
                case HousePaint.RoofTile: return new Rgb(0.11f, 0.11f, 0.12f);
                case HousePaint.Flame: return new Rgb(1f, 0.85f, 0.55f);
                default: return new Rgb(0.3f, 0.3f, 0.3f);
            }
        }
    }
}
