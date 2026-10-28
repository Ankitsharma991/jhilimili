using System;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Core.Home
{
    /// <summary>
    /// Decoration budgets (PRD 9.1: object budgets in total, by quality tier). Each item costs <see cref="DecorInfo.Cost"/>
    /// units; a path item costs that per <see cref="PathUnitLength"/> metres. Every item stays within
    /// <see cref="TrianglesPerUnit"/> triangles per unit, so a full budget never exceeds the tier's triangle budget.
    /// </summary>
    public static class DecorBudget
    {
        /// <summary>Units by quality tier: low, mid, high.</summary>
        public static readonly int[] Units = { 60, 100, 160 };

        public static readonly int[] Triangles = { 30000, 60000, 100000 };

        public const int TrianglesPerUnit = 500;
        public const float PathUnitLength = 3f;
        public const float MinPathLength = 0.3f;
        public const float MaxPathLength = 6f;

        public static int CostOf(DecorInfo info, float pathLength = PathUnitLength) =>
            info.Mode == PlacementMode.Path ? info.Cost * Math.Max(1, (int)Math.Ceiling(pathLength / PathUnitLength - 1e-4f)) : info.Cost;
    }

    /// <summary>
    /// Builds the decoration items (Phase 7 step 3, PRD 9). Free and decal items are built once per item in their own
    /// space: base centre at the origin, y up, front +z, filling the catalogue footprint (width on x, depth on z, height
    /// on y) at scale 1; colour variants change only materials. Path items are built in house space along a hanging
    /// curve between two points.
    /// </summary>
    public static class DecorGenerator
    {
        public static HouseMesh Item(string itemId)
        {
            DecorInfo info = HomeCatalog.Decoration(itemId);
            if (info == null || info.Mode == PlacementMode.Path)
            {
                return null;
            }

            var m = new HouseMesh();
            switch (itemId)
            {
                case "diya_clay": Diya(m); break;
                case "diya_brass_stand": BrassStand(m); break;
                case "lantern_akash_kandil": Kandil(m); break;
                case "lantern_paper_round": PaperLantern(m); break;
                case "lamp_post_small": LampPost(m); break;
                case "tulsi_planter": Tulsi(m); break;
                case "pot_marigold": MarigoldPot(m); break;
                case "pot_palm": Palm(m); break;
                case "bench_wooden": Bench(m); break;
                case "chair_cane": Chair(m); break;
                case "charpai": Charpai(m); break;
                case "rangoli_lotus": RangoliLotus(m); break;
                case "rangoli_peacock": RangoliPeacock(m); break;
                case "rangoli_geometric": RangoliGeometric(m); break;
                case "rangoli_diya_ring": RangoliDiyaRing(m); break;
                case "rangoli_mandala_tihar": RangoliMandala(m); break;
                default: return null;
            }

            return m;
        }

        /// <summary>The point on a strand from a to b hanging by <paramref name="sag"/> (0 to 1; 1 drops a quarter of the span).</summary>
        public static V3 PathPoint(V3 a, V3 b, float sag, float t)
        {
            float drop = Math.Max(0f, Math.Min(1f, sag)) * (b - a).Length * 0.25f;
            return V3.Lerp(a, b, t) - new V3(0f, drop * 4f * t * (1f - t), 0f);
        }

        /// <summary>A path item hanging from a to b in house space; null for a non-path item or a span under 5 cm.</summary>
        public static HouseMesh Path(string itemId, V3 a, V3 b, float sag)
        {
            DecorInfo info = HomeCatalog.Decoration(itemId);
            float length = (b - a).Length;
            if (info == null || info.Mode != PlacementMode.Path || length < 0.05f)
            {
                return null;
            }

            var m = new HouseMesh();
            int steps = Math.Max(4, (int)Math.Ceiling(length / 0.25f));
            V3 Point(float t) => PathPoint(a, b, sag, t);
            switch (itemId)
            {
                case "string_lights_warm":
                case "string_lights_colour":
                {
                    Cord(m, Point, steps, 0.008f, HousePaint.Iron);
                    int bulbs = Math.Max(2, (int)Math.Round(length / 0.2f));
                    for (int i = 0; i <= bulbs; i++)
                    {
                        V3 p = Point(i / (float)bulbs);
                        m.Ball(p.X, p.Y - 0.025f, p.Z, 0.016f, 5, 3, Bulb(i));
                    }

                    break;
                }

                case "light_curtain":
                {
                    Cord(m, Point, steps, 0.008f, HousePaint.Iron);
                    int drops = Math.Max(2, (int)Math.Round(length / 0.2f));
                    for (int i = 0; i <= drops; i++)
                    {
                        V3 top = Point(i / (float)drops);
                        float drop = i % 2 == 0 ? 1.2f : 0.95f;
                        m.Strut(top, top - new V3(0f, drop, 0f), 0.005f, HousePaint.Iron);
                        for (int k = 1; k * 0.2f <= drop + 1e-3f; k++)
                        {
                            V3 p = top - new V3(0f, k * 0.2f, 0f);
                            m.Ball(p.X, p.Y, p.Z, 0.012f, 4, 2, Bulb(i + k));
                        }
                    }

                    break;
                }

                case "toran_mango":
                {
                    Cord(m, Point, steps, 0.02f, HousePaint.Fabric);
                    int leaves = Math.Max(3, (int)Math.Round(length / 0.09f));
                    V3 across = (b - a).Normalized;
                    var facing = V3.Cross(across, new V3(0, 1, 0)).Normalized;
                    for (int i = 0; i <= leaves; i++)
                    {
                        V3 p = Point(i / (float)leaves) - new V3(0f, 0.01f, 0f);
                        float len = i % 2 == 0 ? 0.15f : 0.12f;
                        V3 side = across * 0.03f;
                        m.TwoSided(new[] { p - side, p + side, p - new V3(0f, len, 0f) + facing * 0.01f }, facing, HousePaint.Leaf);
                        if (i % 3 == 1)
                        {
                            m.Ball(p.X, p.Y - 0.035f, p.Z + 0.015f, 0.025f, 5, 3, HousePaint.Marigold);
                        }
                    }

                    break;
                }

                case "garland_marigold":
                {
                    Cord(m, Point, steps, 0.01f, HousePaint.Leaf);
                    int flowers = Math.Max(3, (int)Math.Round(length / 0.06f));
                    for (int i = 0; i <= flowers; i++)
                    {
                        V3 p = Point(i / (float)flowers);
                        m.Ball(p.X, p.Y, p.Z, 0.032f, 5, 3, i % 7 == 3 ? HousePaint.MarigoldAlt : HousePaint.Marigold);
                    }

                    break;
                }

                case "garland_marigold_strands":
                {
                    Cord(m, Point, steps, 0.012f, HousePaint.Leaf);
                    int strands = Math.Max(2, (int)Math.Round(length / 0.25f));
                    for (int i = 0; i <= strands; i++)
                    {
                        V3 top = Point(i / (float)strands);
                        float drop = i % 2 == 0 ? 0.8f : 0.6f;
                        for (float d = 0.03f; d <= drop - 0.03f; d += 0.055f)
                        {
                            m.Ball(top.X, top.Y - d, top.Z, 0.026f, 4, 2, ((int)(d / 0.055f) % 4 == 3) ? HousePaint.MarigoldAlt : HousePaint.Marigold);
                        }

                        m.Strut(top - new V3(0f, drop - 0.03f, 0f), top - new V3(0f, drop + 0.04f, 0f), 0.02f, HousePaint.Fabric);
                    }

                    break;
                }
            }

            return m;
        }

        private static HousePaint Bulb(int i) => i % 3 == 0 ? HousePaint.BulbA : i % 3 == 1 ? HousePaint.BulbB : HousePaint.BulbC;

        private static void Cord(HouseMesh m, Func<float, V3> point, int steps, float thickness, HousePaint paint)
        {
            for (int i = 0; i < steps; i++)
            {
                m.Strut(point(i / (float)steps), point((i + 1) / (float)steps), thickness, paint);
            }
        }

        // ---- Lamps ----

        private static void Diya(HouseMesh m)
        {
            // A clay bowl with a pinched spout and a flame on the wick.
            m.Lathe(0f, 0f, new[] { 0.02f, 0f, 0.035f, 0.005f, 0.05f, 0.026f, 0.046f, 0.032f, 0.03f, 0.026f, 0f, 0.024f }, 10, HousePaint.Clay);
            m.TwoSided(new[] { new V3(-0.012f, 0.03f, 0.03f), new V3(0.012f, 0.03f, 0.03f), new V3(0f, 0.033f, 0.05f) }, new V3(0, 1, 0), HousePaint.Clay);
            m.Lathe(0f, 0.034f, new[] { 0f, 0.03f, 0.008f, 0.04f, 0f, 0.06f }, 6, HousePaint.Flame);
        }

        private static void BrassStand(HouseMesh m)
        {
            // A samai: stepped foot, turned stem, a five-wick bowl.
            m.Lathe(0f, 0f, new[]
            {
                0.11f, 0f, 0.11f, 0.02f, 0.07f, 0.04f, 0.03f, 0.08f, 0.022f, 0.2f, 0.035f, 0.22f, 0.022f, 0.24f, 0.02f, 0.52f,
                0.04f, 0.56f, 0.1f, 0.61f, 0.105f, 0.635f, 0.06f, 0.625f, 0f, 0.62f,
            }, 12, HousePaint.Brass, capBottom: true, capTop: false);
            for (int i = 0; i < 5; i++)
            {
                double a = Math.PI / 2 + 2 * Math.PI * i / 5;
                float x = 0.08f * (float)Math.Cos(a), z = 0.08f * (float)Math.Sin(a);
                m.Lathe(x, z, new[] { 0f, 0.63f, 0.009f, 0.65f, 0f, 0.7f }, 5, HousePaint.Flame);
            }
        }

        private static void Kandil(HouseMesh m)
        {
            // Akash kandil: a five-point paper star lantern with tails, hung by a cord.
            const float cy = 0.38f, outer = 0.22f, inner = 0.1f, half = 0.09f;
            var front = new V3[10];
            var back = new V3[10];
            for (int i = 0; i < 10; i++)
            {
                double a = Math.PI / 2 + Math.PI * i / 5;
                float r = i % 2 == 0 ? outer : inner;
                front[i] = new V3(r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a), half);
                back[i] = new V3(front[i].X, front[i].Y, -half);
            }

            var centreF = new V3(0f, cy, half + 0.04f);
            var centreB = new V3(0f, cy, -half - 0.04f);
            for (int i = 0; i < 10; i++)
            {
                int j = (i + 1) % 10;
                m.TriangleToward(centreF, front[i], front[j], new V3(0, 0, 1), HousePaint.Paper);
                m.TriangleToward(centreB, back[i], back[j], new V3(0, 0, -1), HousePaint.Paper);
                V3 mid = (front[i] + front[j]) * 0.5f;
                m.QuadToward(front[i], front[j], back[j], back[i], new V3(mid.X, mid.Y - cy, 0f), HousePaint.Paper);
            }

            m.Strut(new V3(0f, cy + outer, 0f), new V3(0f, 0.6f, 0f), 0.01f, HousePaint.Iron);
            for (int t = -1; t <= 1; t++)
            {
                float x = t * 0.06f, top = cy - 0.12f;
                m.TwoSided(new[] { new V3(x - 0.02f, top, 0f), new V3(x + 0.02f, top, 0f), new V3(x + 0.015f, 0f, 0f), new V3(x - 0.015f, 0f, 0f) }, new V3(0, 0, 1), HousePaint.Fabric);
            }
        }

        private static void PaperLantern(HouseMesh m)
        {
            m.Ball(0f, 0.22f, 0f, 0.15f, 10, 6, HousePaint.Paper);
            m.Lathe(0f, 0f, new[] { 0.05f, 0.35f, 0.05f, 0.38f, 0.01f, 0.4f }, 8, HousePaint.Fabric);
            m.Lathe(0f, 0f, new[] { 0.06f, 0.06f, 0.06f, 0.085f }, 8, HousePaint.Fabric);
            m.Strut(new V3(0f, 0.06f, 0f), new V3(0f, 0f, 0f), 0.025f, HousePaint.Fabric);
        }

        private static void LampPost(HouseMesh m)
        {
            m.Lathe(0f, 0f, new[] { 0.1f, 0f, 0.1f, 0.04f, 0.035f, 0.09f }, 8, HousePaint.Iron, capTop: false);
            m.Box(-0.018f, 0.08f, -0.018f, 0.018f, 0.95f, 0.018f, HousePaint.Iron, Faces.Bottom | Faces.Top);
            m.Box(-0.07f, 0.94f, -0.07f, 0.07f, 0.96f, 0.07f, HousePaint.Iron);
            m.Box(-0.06f, 0.96f, -0.06f, 0.06f, 1.1f, 0.06f, HousePaint.Glass, Faces.Bottom | Faces.Top);
            m.Lathe(0f, 0f, new[] { 0.11f, 1.1f, 0.11f, 1.12f, 0f, 1.2f }, 4, HousePaint.Iron);
        }

        // ---- Plants ----

        private static void Tulsi(HouseMesh m)
        {
            // A tulsi vrindavan: a stepped plinth with a niche for a diya, and the bushy plant on top.
            m.Box(-0.175f, 0f, -0.175f, 0.175f, 0.07f, 0.175f, HousePaint.Clay, Faces.Bottom);
            m.Box(-0.145f, 0.07f, -0.145f, 0.145f, 0.44f, 0.145f, HousePaint.Clay, Faces.Bottom | Faces.Top);
            m.Box(-0.07f, 0.13f, 0.145f, 0.07f, 0.29f, 0.15f, HousePaint.Dark, Faces.Back);
            m.Box(-0.17f, 0.44f, -0.17f, 0.17f, 0.5f, 0.17f, HousePaint.Clay);
            m.Box(-0.012f, 0.5f, -0.012f, 0.012f, 0.62f, 0.012f, HousePaint.Wood, Faces.Bottom);
            float[][] blobs = { new[] { 0f, 0.6f, 0f, 0.09f }, new[] { -0.07f, 0.66f, 0.03f, 0.07f }, new[] { 0.07f, 0.67f, -0.02f, 0.07f }, new[] { 0f, 0.73f, 0f, 0.07f } };
            foreach (float[] b in blobs)
            {
                m.Ball(b[0], b[1], b[2], b[3], 7, 4, HousePaint.Leaf);
            }
        }

        private static void MarigoldPot(HouseMesh m)
        {
            m.Lathe(0f, 0f, new[] { 0.07f, 0f, 0.11f, 0.13f, 0.1f, 0.17f, 0.115f, 0.18f, 0.105f, 0.2f }, 10, HousePaint.Clay, capTop: false);
            m.Ball(0f, 0.24f, 0f, 0.1f, 8, 4, HousePaint.Leaf);
            for (int i = 0; i < 7; i++)
            {
                double a = 2 * Math.PI * i / 6;
                float r = i == 6 ? 0f : 0.065f, y = i == 6 ? 0.315f : 0.29f;
                m.Ball(r * (float)Math.Cos(a), y, r * (float)Math.Sin(a), 0.035f, 6, 3, i % 3 == 2 ? HousePaint.MarigoldAlt : HousePaint.Marigold);
            }
        }

        private static void Palm(HouseMesh m)
        {
            m.Lathe(0f, 0f, new[] { 0.1f, 0f, 0.15f, 0.26f, 0.16f, 0.28f, 0.14f, 0.3f }, 10, HousePaint.Clay, capTop: true);
            m.Lathe(0f, 0f, new[] { 0.035f, 0.3f, 0.025f, 0.85f, 0f, 0.86f }, 6, HousePaint.Wood);
            var crown = new V3(0f, 0.85f, 0f);
            for (int i = 0; i < 9; i++)
            {
                double a = 2 * Math.PI * i / 9 + 0.3;
                var dir = new V3((float)Math.Cos(a), 0f, (float)Math.Sin(a));
                var side = V3.Cross(dir, new V3(0, 1, 0)) * 0.035f;
                bool upright = i % 3 == 0;
                V3 mid = crown + dir * (upright ? 0.07f : 0.12f) + new V3(0f, upright ? 0.3f : 0.2f, 0f);
                V3 tip = crown + dir * (upright ? 0.11f : 0.195f) + new V3(0f, upright ? 0.45f : 0.08f, 0f);
                V3 n = V3.Cross(mid - crown, side);
                m.TwoSided(new[] { crown, mid + side, tip, mid - side }, n, HousePaint.Leaf);
            }
        }

        // ---- Furniture ----

        private static void Bench(HouseMesh m)
        {
            for (int p = 0; p < 3; p++)
            {
                float z0 = -0.2f + p * 0.135f;
                m.Box(-0.6f, 0.4f, z0, 0.6f, 0.45f, z0 + 0.12f, HousePaint.Wood);
            }

            foreach (float x in new[] { -0.5f, 0.5f })
            {
                m.Box(x - 0.04f, 0f, -0.17f, x + 0.04f, 0.4f, -0.12f, HousePaint.Wood, Faces.Top);
                m.Box(x - 0.04f, 0f, 0.12f, x + 0.04f, 0.4f, 0.17f, HousePaint.Wood, Faces.Top);
                m.Box(x - 0.03f, 0.2f, -0.12f, x + 0.03f, 0.25f, 0.12f, HousePaint.Wood, Faces.Left | Faces.Right);
            }
        }

        private static void Chair(HouseMesh m)
        {
            foreach (float x in new[] { -0.24f, 0.24f })
            {
                m.Box(x - 0.025f, 0f, 0.2f, x + 0.025f, 0.62f, 0.25f, HousePaint.Wood, Faces.Bottom);
                m.Box(x - 0.025f, 0f, -0.25f, x + 0.025f, 0.8f, -0.2f, HousePaint.Wood, Faces.Bottom);
                m.Box(x - 0.03f, 0.6f, -0.2f, x + 0.03f, 0.64f, 0.27f, HousePaint.Wood);
            }

            m.Box(-0.22f, 0.38f, -0.22f, 0.22f, 0.43f, 0.22f, HousePaint.Fabric);
            m.Box(-0.22f, 0.45f, -0.235f, 0.22f, 0.78f, -0.215f, HousePaint.Fabric);
            m.Box(-0.26f, 0.76f, -0.25f, 0.26f, 0.8f, -0.2f, HousePaint.Wood);
        }

        private static void Charpai(HouseMesh m)
        {
            const float w = 0.9f, d = 0.45f, top = 0.4f;
            foreach (float x in new[] { -w + 0.04f, w - 0.04f })
            {
                foreach (float z in new[] { -d + 0.04f, d - 0.04f })
                {
                    m.Lathe(x, z, new[] { 0.035f, 0f, 0.045f, 0.12f, 0.03f, 0.25f, 0.04f, 0.33f, 0.035f, 0.45f }, 6, HousePaint.Wood);
                }
            }

            m.Box(-w + 0.04f, top - 0.04f, -d + 0.02f, w - 0.04f, top + 0.02f, -d + 0.07f, HousePaint.Wood);
            m.Box(-w + 0.04f, top - 0.04f, d - 0.07f, w - 0.04f, top + 0.02f, d - 0.02f, HousePaint.Wood);
            m.Box(-w + 0.02f, top - 0.04f, -d + 0.07f, -w + 0.07f, top + 0.02f, d - 0.07f, HousePaint.Wood);
            m.Box(w - 0.07f, top - 0.04f, -d + 0.07f, w - 0.02f, top + 0.02f, d - 0.07f, HousePaint.Wood);

            // Woven rope bed: strands both ways.
            for (float x = -w + 0.12f; x < w - 0.08f; x += 0.08f)
            {
                m.Box(x - 0.018f, top - 0.01f, -d + 0.07f, x + 0.018f, top + 0.005f, d - 0.07f, HousePaint.Fabric, Faces.Bottom | Faces.Front | Faces.Back);
            }

            for (float z = -d + 0.12f; z < d - 0.08f; z += 0.08f)
            {
                m.Box(-w + 0.07f, top - 0.005f, z - 0.018f, w - 0.07f, top + 0.01f, z + 0.018f, HousePaint.Fabric, Faces.Bottom | Faces.Left | Faces.Right);
            }
        }

        // ---- Rangoli: flat powder layers, each a hair above the last ----

        private const float Layer = 0.002f;

        private static void RangoliLotus(HouseMesh m)
        {
            m.FlatRing(0f, 0f, 0.44f, 0.5f, 32, Layer, HousePaint.Powder1);
            for (int i = 0; i < 16; i++)
            {
                double a = 2 * Math.PI * i / 16;
                Dot(m, 0.405f * (float)Math.Cos(a), 0.405f * (float)Math.Sin(a), 0.018f, Layer, HousePaint.Powder3);
            }

            Petals(m, 8, 0.1f, 0.38f, 0.11f, 0f, 2 * Layer, HousePaint.Powder2);
            Petals(m, 8, 0.1f, 0.3f, 0.08f, Math.PI / 8, 3 * Layer, HousePaint.Powder3);
            Petals(m, 8, 0.11f, 0.32f, 0.04f, 0f, 4 * Layer, HousePaint.Powder1);
            Dot(m, 0f, 0f, 0.12f, 3 * Layer, HousePaint.Powder4);
            Dot(m, 0f, 0f, 0.06f, 4 * Layer, HousePaint.Powder2);
        }

        private static void RangoliPeacock(HouseMesh m)
        {
            // A peacock seen from the front: a fanned tail of eyed feathers behind a teardrop body.
            Dot(m, 0f, 0f, 0.58f, Layer, HousePaint.Powder4);
            for (int i = 0; i < 11; i++)
            {
                double a = Math.PI * (0.1 + 0.8 * i / 10.0);
                Petal(m, 0.08f, 0.56f, 0.1f, a, 2 * Layer, HousePaint.Powder3);
                float ex = 0.44f * (float)Math.Cos(a), ez = -0.44f * (float)Math.Sin(a);
                Dot(m, ex, ez, 0.05f, 3 * Layer, HousePaint.Powder2);
                Dot(m, ex, ez, 0.022f, 4 * Layer, HousePaint.Powder1);
            }

            // Body pointing toward the viewer (+z), head and crest.
            m.FlatPolygon(new[] { 0f, -0.2f, 0.09f, -0.05f, 0.07f, 0.18f, 0f, 0.3f, -0.07f, 0.18f, -0.09f, -0.05f }, 5 * Layer, HousePaint.Powder3);
            Dot(m, 0f, 0.36f, 0.06f, 5 * Layer, HousePaint.Powder3);
            for (int c = -1; c <= 1; c++)
            {
                Dot(m, c * 0.035f, 0.47f, 0.016f, 5 * Layer, HousePaint.Powder2);
            }

            Dot(m, 0.02f, 0.37f, 0.012f, 6 * Layer, HousePaint.Powder4);
        }

        private static void RangoliGeometric(HouseMesh m)
        {
            HousePaint[] colours = { HousePaint.Powder1, HousePaint.Powder2, HousePaint.Powder3, HousePaint.Powder4 };
            for (int k = 0; k < 4; k++)
            {
                float r = 0.45f - k * 0.1f;
                double rot = k % 2 == 0 ? 0 : Math.PI / 4;
                var xz = new float[8];
                for (int c = 0; c < 4; c++)
                {
                    double a = rot + Math.PI / 4 + Math.PI / 2 * c;
                    float rr = k % 2 == 0 ? r * 1.414f : r;
                    xz[c * 2] = Math.Max(-0.45f, Math.Min(0.45f, rr * (float)Math.Cos(a)));
                    xz[c * 2 + 1] = Math.Max(-0.45f, Math.Min(0.45f, rr * (float)Math.Sin(a)));
                }

                m.FlatPolygon(xz, (k + 1) * Layer, colours[k]);
            }

            for (int i = 0; i < 4; i++)
            {
                double a = Math.PI / 2 * i;
                Dot(m, 0.36f * (float)Math.Cos(a), 0.36f * (float)Math.Sin(a), 0.035f, 5 * Layer, HousePaint.Powder4);
            }

            Petals(m, 8, 0.02f, 0.14f, 0.05f, 0f, 5 * Layer, HousePaint.Powder1);
            Dot(m, 0f, 0f, 0.04f, 6 * Layer, HousePaint.Powder2);
        }

        private static void RangoliDiyaRing(HouseMesh m)
        {
            m.FlatRing(0f, 0f, 0.46f, 0.5f, 32, Layer, HousePaint.Powder2);
            Dot(m, 0f, 0f, 0.42f, Layer, HousePaint.Powder3);
            for (int i = 0; i < 8; i++)
            {
                double a = 2 * Math.PI * i / 8;
                float cx = 0.3f * (float)Math.Cos(a), cz = 0.3f * (float)Math.Sin(a);

                // A diya: a half-disc bowl with its flame pointing outward.
                var bowl = new float[14];
                for (int k = 0; k <= 6; k++)
                {
                    double b = a + Math.PI / 2 + Math.PI * k / 6;
                    bowl[k * 2] = cx + 0.07f * (float)Math.Cos(b);
                    bowl[k * 2 + 1] = cz + 0.07f * (float)Math.Sin(b);
                }

                m.FlatPolygon(bowl, 2 * Layer, HousePaint.Powder1);
                Petal(m, 0.31f, 0.4f, 0.035f, -a, 3 * Layer, HousePaint.Powder2);
            }

            Petals(m, 8, 0.03f, 0.17f, 0.06f, Math.PI / 8, 2 * Layer, HousePaint.Powder4);
            Dot(m, 0f, 0f, 0.05f, 3 * Layer, HousePaint.Powder1);
        }

        private static void RangoliMandala(HouseMesh m)
        {
            m.FlatRing(0f, 0f, 0.5f, 0.55f, 40, Layer, HousePaint.Powder3);
            Petals(m, 16, 0.32f, 0.5f, 0.07f, 0f, 2 * Layer, HousePaint.Powder1);
            Petals(m, 12, 0.18f, 0.36f, 0.08f, Math.PI / 12, 3 * Layer, HousePaint.Powder2);
            m.FlatRing(0f, 0f, 0.15f, 0.18f, 24, 3 * Layer, HousePaint.Powder4);
            Petals(m, 8, 0.03f, 0.15f, 0.06f, 0f, 4 * Layer, HousePaint.Powder3);
            Dot(m, 0f, 0f, 0.04f, 5 * Layer, HousePaint.Powder1);
        }

        private static void Dot(HouseMesh m, float x, float z, float r, float y, HousePaint paint) =>
            m.Disc(x, y, z, r, r > 0.1f ? 32 : r > 0.03f ? 12 : 8, true, paint);

        /// <summary>A ring of pointed petals from radius r0 to r1.</summary>
        private static void Petals(HouseMesh m, int count, float r0, float r1, float width, double offset, float y, HousePaint paint)
        {
            for (int i = 0; i < count; i++)
            {
                Petal(m, r0, r1, width, offset + 2 * Math.PI * i / count, y, paint);
            }
        }

        /// <summary>A lens-shaped petal along angle a (radians, x toward z) from r0 to r1, <paramref name="width"/> at its widest.</summary>
        private static void Petal(HouseMesh m, float r0, float r1, float width, double a, float y, HousePaint paint)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float len = r1 - r0;
            var xz = new float[12];
            float[] along = { 0f, 0.25f, 0.6f, 1f, 0.6f, 0.25f };
            float[] off = { 0f, 0.8f, 1f, 0f, -1f, -0.8f };
            for (int i = 0; i < 6; i++)
            {
                float r = r0 + len * along[i], o = off[i] * width / 2f;
                xz[i * 2] = r * c - o * s;
                xz[i * 2 + 1] = r * s + o * c;
            }

            m.FlatPolygon(xz, y, paint);
        }
    }
}
