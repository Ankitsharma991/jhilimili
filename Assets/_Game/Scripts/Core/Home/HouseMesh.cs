using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Core.Home
{
    /// <summary>What a surface is painted with; the view gives each one a material and <see cref="HousePalette"/> its colour.</summary>
    public enum HousePaint
    {
        /// <summary>The house walls: wall colour over the wall material's texture.</summary>
        Wall,

        /// <summary>Frames, cornices and pillar caps: the trim colour.</summary>
        Trim,

        /// <summary>The module's own colour variant: door leaves, shutters, railings, gate leaves.</summary>
        Accent,
        Wood,
        Iron,

        /// <summary>Lit window glass and lamps; emissive at night.</summary>
        Glass,
        RoofTile,
        Stone,
        Brick,

        /// <summary>The dark inside of an opening no module fills.</summary>
        Dark,

        // Decorations (Phase 7 step 3): coloured per item and variant by HousePalette.Decor.
        Clay,
        Flame,
        Brass,
        Marigold,
        MarigoldAlt,
        Leaf,
        Paper,
        BulbA,
        BulbB,
        BulbC,
        Fabric,
        Powder1,
        Powder2,
        Powder3,
        Powder4,
    }

    /// <summary>An axis-aligned box in house space (metres).</summary>
    public readonly struct Box3
    {
        public Box3(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            Min = new V3(Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1));
            Max = new V3(Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1));
        }

        public V3 Min { get; }

        public V3 Max { get; }

        public bool Contains(V3 p, float eps = 1e-3f) =>
            p.X >= Min.X - eps && p.X <= Max.X + eps && p.Y >= Min.Y - eps && p.Y <= Max.Y + eps && p.Z >= Min.Z - eps && p.Z <= Max.Z + eps;

        /// <summary>True when the insides overlap; boxes that only touch do not.</summary>
        public bool Overlaps(Box3 o, float eps = 1e-4f) =>
            Min.X < o.Max.X - eps && o.Min.X < Max.X - eps && Min.Y < o.Max.Y - eps && o.Min.Y < Max.Y - eps && Min.Z < o.Max.Z - eps && o.Min.Z < Max.Z - eps;

        public Box3 Offset(V3 by) => new Box3(Min.X + by.X, Min.Y + by.Y, Min.Z + by.Z, Max.X + by.X, Max.Y + by.Y, Max.Z + by.Z);

        public override string ToString() => $"[{Min} .. {Max}]";
    }

    /// <summary>
    /// Engine-free static mesh for the house: positions, normals, uvs in metres (planar, so wall textures keep their scale
    /// on every face) and triangles per <see cref="HousePaint"/>. Primitives take local coordinates and add <see cref="Offset"/>.
    /// Winding follows Unity: a triangle is front-facing when Cross(b - a, c - a) points at the viewer; every primitive
    /// takes the outward normal and winds itself to match.
    /// </summary>
    public sealed class HouseMesh
    {
        public static readonly int PaintCount = Enum.GetValues(typeof(HousePaint)).Length;

        public readonly List<V3> Positions = new List<V3>();
        public readonly List<V3> Normals = new List<V3>();
        public readonly List<float> U = new List<float>();
        public readonly List<float> V = new List<float>();
        public readonly List<int>[] Triangles;

        public HouseMesh()
        {
            Triangles = new List<int>[PaintCount];
            for (int i = 0; i < PaintCount; i++)
            {
                Triangles[i] = new List<int>();
            }
        }

        /// <summary>Added to every local coordinate (the attachment point's origin).</summary>
        public V3 Offset { get; set; }

        public int VertexCount => Positions.Count;

        public int TriangleCount
        {
            get
            {
                int n = 0;
                foreach (List<int> t in Triangles)
                {
                    n += t.Count / 3;
                }

                return n;
            }
        }

        public void Quad(V3 a, V3 b, V3 c, V3 d, V3 normal, HousePaint paint)
        {
            V3 n = normal.Normalized;
            int i0 = Vertex(a, n), i1 = Vertex(b, n), i2 = Vertex(c, n), i3 = Vertex(d, n);
            Triangle(i0, i1, i2, n, paint);
            Triangle(i0, i2, i3, n, paint);
        }

        /// <summary>A flat quad whose normal is taken from its corners, flipped to point toward <paramref name="outward"/>.</summary>
        public void QuadToward(V3 a, V3 b, V3 c, V3 d, V3 outward, HousePaint paint)
        {
            V3 n = V3.Cross(b - a, c - a).Normalized;
            Quad(a, b, c, d, V3.Dot(n, outward) < 0f ? n * -1f : n, paint);
        }

        public void TriangleToward(V3 a, V3 b, V3 c, V3 outward, HousePaint paint)
        {
            V3 n = V3.Cross(b - a, c - a).Normalized;
            if (V3.Dot(n, outward) < 0f)
            {
                n = n * -1f;
            }

            Triangle(Vertex(a, n), Vertex(b, n), Vertex(c, n), n, paint);
        }

        /// <summary>A box from two corners. <paramref name="skip"/> leaves out faces that are always hidden.</summary>
        public void Box(float x0, float y0, float z0, float x1, float y1, float z1, HousePaint paint, Faces skip = Faces.None)
        {
            float ax = Math.Min(x0, x1), bx = Math.Max(x0, x1), ay = Math.Min(y0, y1), by = Math.Max(y0, y1), az = Math.Min(z0, z1), bz = Math.Max(z0, z1);
            V3 p000 = new V3(ax, ay, az), p100 = new V3(bx, ay, az), p010 = new V3(ax, by, az), p110 = new V3(bx, by, az);
            V3 p001 = new V3(ax, ay, bz), p101 = new V3(bx, ay, bz), p011 = new V3(ax, by, bz), p111 = new V3(bx, by, bz);
            if ((skip & Faces.Front) == 0)
            {
                Quad(p001, p101, p111, p011, new V3(0, 0, 1), paint);
            }

            if ((skip & Faces.Back) == 0)
            {
                Quad(p000, p010, p110, p100, new V3(0, 0, -1), paint);
            }

            if ((skip & Faces.Top) == 0)
            {
                Quad(p010, p011, p111, p110, new V3(0, 1, 0), paint);
            }

            if ((skip & Faces.Bottom) == 0)
            {
                Quad(p000, p100, p101, p001, new V3(0, -1, 0), paint);
            }

            if ((skip & Faces.Left) == 0)
            {
                Quad(p000, p001, p011, p010, new V3(-1, 0, 0), paint);
            }

            if ((skip & Faces.Right) == 0)
            {
                Quad(p100, p110, p111, p101, new V3(1, 0, 0), paint);
            }
        }

        /// <summary>A half disc facing +z (an arched pane or leaf top), centre at the spring line.</summary>
        public void HalfDisc(float cx, float cy, float z, float r, int segments, HousePaint paint)
        {
            var n = new V3(0, 0, 1);
            for (int s = 0; s < segments; s++)
            {
                TriangleToward(new V3(cx, cy, z), Arc(cx, cy, z, r, s, segments), Arc(cx, cy, z, r, s + 1, segments), n, paint);
            }
        }

        /// <summary>
        /// A semicircular arch band (radius <paramref name="r0"/> to <paramref name="r1"/>) from z0 to z1: front and back faces,
        /// the soffit facing the centre and the outer face.
        /// </summary>
        public void ArchBand(float cx, float cy, float r0, float r1, float z0, float z1, int segments, HousePaint paint)
        {
            for (int s = 0; s < segments; s++)
            {
                V3 i0 = Arc(cx, cy, z1, r0, s, segments), i1 = Arc(cx, cy, z1, r0, s + 1, segments);
                V3 o0 = Arc(cx, cy, z1, r1, s, segments), o1 = Arc(cx, cy, z1, r1, s + 1, segments);
                V3 bi0 = Arc(cx, cy, z0, r0, s, segments), bi1 = Arc(cx, cy, z0, r0, s + 1, segments);
                V3 bo0 = Arc(cx, cy, z0, r1, s, segments), bo1 = Arc(cx, cy, z0, r1, s + 1, segments);
                QuadToward(i0, o0, o1, i1, new V3(0, 0, 1), paint);
                QuadToward(bi0, bo0, bo1, bi1, new V3(0, 0, -1), paint);
                V3 mid = Arc(cx, cy, z1, 1f, s * 2 + 1, segments * 2) - new V3(cx, cy, z1);
                QuadToward(i0, i1, bi1, bi0, mid * -1f, paint);
                QuadToward(o0, o1, bo1, bo0, mid, paint);
            }
        }

        /// <summary>
        /// Fills the two corners between a semicircle (centre cx, cy; radius r) and the rectangle above it up to
        /// <paramref name="top"/>, at depth z, facing +z. The rectangle is as wide as the arch.
        /// </summary>
        public void Spandrels(float cx, float cy, float r, float top, float z, int segments, HousePaint paint)
        {
            var n = new V3(0, 0, 1);
            var right = new V3(cx + r, top, z);
            var left = new V3(cx - r, top, z);
            int half = segments / 2;
            for (int s = 0; s < segments; s++)
            {
                V3 corner = s < half ? right : left;
                TriangleToward(corner, Arc(cx, cy, z, r, s, segments), Arc(cx, cy, z, r, s + 1, segments), n, paint);
            }

            if (top - (cy + r) > 1e-4f)
            {
                TriangleToward(right, Arc(cx, cy, z, r, half, segments), left, n, paint);
            }
        }

        /// <summary>A dome (half sphere) on the plane y = cy.</summary>
        public void Dome(float cx, float cy, float cz, float r, int segments, int rings, HousePaint paint)
        {
            for (int ring = 0; ring < rings; ring++)
            {
                double a0 = Math.PI / 2 * ring / rings, a1 = Math.PI / 2 * (ring + 1) / rings;
                for (int s = 0; s < segments; s++)
                {
                    double b0 = 2 * Math.PI * s / segments, b1 = 2 * Math.PI * (s + 1) / segments;
                    V3 p00 = Sphere(cx, cy, cz, r, a0, b0), p01 = Sphere(cx, cy, cz, r, a0, b1);
                    V3 p10 = Sphere(cx, cy, cz, r, a1, b0), p11 = Sphere(cx, cy, cz, r, a1, b1);
                    V3 outward = Sphere(0, 0, 0, 1, (a0 + a1) / 2, (b0 + b1) / 2);
                    if (ring == rings - 1)
                    {
                        TriangleToward(p00, p01, p10, outward, paint);
                    }
                    else
                    {
                        QuadToward(p00, p01, p11, p10, outward, paint);
                    }
                }
            }
        }

        /// <summary>
        /// A surface of revolution about the vertical line (cx, cz): <paramref name="profile"/> runs bottom to top as
        /// (radius, y) pairs. Flat shaded; ends with a non-zero radius are capped.
        /// </summary>
        public void Lathe(float cx, float cz, float[] profile, int segments, HousePaint paint, bool capBottom = true, bool capTop = true)
        {
            int n = profile.Length / 2;
            for (int i = 0; i + 1 < n; i++)
            {
                float r0 = profile[i * 2], y0 = profile[i * 2 + 1], r1 = profile[i * 2 + 2], y1 = profile[i * 2 + 3];
                float dr = r1 - r0, dy = y1 - y0;
                if (Math.Abs(dr) < 1e-6f && Math.Abs(dy) < 1e-6f)
                {
                    continue;
                }

                for (int s = 0; s < segments; s++)
                {
                    double a0 = 2 * Math.PI * s / segments, a1 = 2 * Math.PI * (s + 1) / segments, am = (a0 + a1) / 2;
                    var radial = new V3((float)Math.Cos(am), 0f, (float)Math.Sin(am));
                    V3 outward = radial * dy + new V3(0f, -dr, 0f);
                    V3 p00 = Ring(cx, cz, r0, y0, a0), p01 = Ring(cx, cz, r0, y0, a1), p10 = Ring(cx, cz, r1, y1, a0), p11 = Ring(cx, cz, r1, y1, a1);
                    if (r0 < 1e-6f)
                    {
                        TriangleToward(p00, p10, p11, outward, paint);
                    }
                    else if (r1 < 1e-6f)
                    {
                        TriangleToward(p00, p01, p10, outward, paint);
                    }
                    else
                    {
                        QuadToward(p00, p01, p11, p10, outward, paint);
                    }
                }
            }

            if (capBottom && profile[0] > 1e-6f)
            {
                Disc(cx, profile[1], cz, profile[0], segments, false, paint);
            }

            if (capTop && profile[(n - 1) * 2] > 1e-6f)
            {
                Disc(cx, profile[(n - 1) * 2 + 1], cz, profile[(n - 1) * 2], segments, true, paint);
            }
        }

        /// <summary>A low-poly ball: <paramref name="rings"/> bands from pole to pole.</summary>
        public void Ball(float cx, float cy, float cz, float r, int segments, int rings, HousePaint paint)
        {
            var profile = new float[(rings + 1) * 2];
            for (int i = 0; i <= rings; i++)
            {
                double a = -Math.PI / 2 + Math.PI * i / rings;
                profile[i * 2] = i == 0 || i == rings ? 0f : r * (float)Math.Cos(a);
                profile[i * 2 + 1] = cy + r * (float)Math.Sin(a);
            }

            Lathe(cx, cz, profile, segments, paint, false, false);
        }

        /// <summary>A horizontal disc at height y facing up (or down).</summary>
        public void Disc(float cx, float y, float cz, float r, int segments, bool up, HousePaint paint)
        {
            var n = new V3(0f, up ? 1f : -1f, 0f);
            var c = new V3(cx, y, cz);
            for (int s = 0; s < segments; s++)
            {
                TriangleToward(c, Ring(cx, cz, r, y, 2 * Math.PI * s / segments), Ring(cx, cz, r, y, 2 * Math.PI * (s + 1) / segments), n, paint);
            }
        }

        /// <summary>A square rod of side <paramref name="thickness"/> from a to b (wires, cords, poles at any angle). No end caps.</summary>
        public void Strut(V3 a, V3 b, float thickness, HousePaint paint)
        {
            V3 axis = b - a;
            if (axis.Length < 1e-5f)
            {
                return;
            }

            V3 dir = axis.Normalized;
            V3 side = V3.Cross(dir, Math.Abs(dir.Y) > 0.9f ? new V3(1, 0, 0) : new V3(0, 1, 0)).Normalized * (thickness / 2f);
            V3 up = V3.Cross(side, dir).Normalized * (thickness / 2f);
            V3[] c = { side + up, side * -1f + up, side * -1f + up * -1f, side + up * -1f };
            for (int i = 0; i < 4; i++)
            {
                V3 o0 = c[i], o1 = c[(i + 1) % 4];
                QuadToward(a + o0, a + o1, b + o1, b + o0, o0 + o1, paint);
            }
        }

        /// <summary>A thin flat shape seen from both sides (a leaf, a tassel): the polygon fan and its mirror.</summary>
        public void TwoSided(V3[] outline, V3 normal, HousePaint paint)
        {
            V3 c = V3.Zero;
            foreach (V3 p in outline)
            {
                c = c + p;
            }

            c = c * (1f / outline.Length);
            for (int i = 0; i < outline.Length; i++)
            {
                V3 a = outline[i], b = outline[(i + 1) % outline.Length];
                if (V3.Cross(a - c, b - c).Length < 1e-9f)
                {
                    continue;
                }

                TriangleToward(c, a, b, normal, paint);
                TriangleToward(c, b, a, normal * -1f, paint);
            }
        }

        /// <summary>A flat polygon at height y facing up, fanned from its centroid (convex or star-shaped outlines).</summary>
        public void FlatPolygon(float[] xz, float y, HousePaint paint)
        {
            int n = xz.Length / 2;
            float cx = 0f, cz = 0f;
            for (int i = 0; i < n; i++)
            {
                cx += xz[i * 2];
                cz += xz[i * 2 + 1];
            }

            var c = new V3(cx / n, y, cz / n);
            var up = new V3(0, 1, 0);
            for (int i = 0; i < n; i++)
            {
                var a = new V3(xz[i * 2], y, xz[i * 2 + 1]);
                var b = new V3(xz[(i + 1) % n * 2], y, xz[(i + 1) % n * 2 + 1]);
                if (V3.Cross(a - c, b - c).Length > 1e-9f)
                {
                    TriangleToward(c, a, b, up, paint);
                }
            }
        }

        /// <summary>A flat ring (annulus) at height y facing up.</summary>
        public void FlatRing(float cx, float cz, float r0, float r1, int segments, float y, HousePaint paint)
        {
            var up = new V3(0, 1, 0);
            for (int s = 0; s < segments; s++)
            {
                double a0 = 2 * Math.PI * s / segments, a1 = 2 * Math.PI * (s + 1) / segments;
                QuadToward(Ring(cx, cz, r0, y, a0), Ring(cx, cz, r1, y, a0), Ring(cx, cz, r1, y, a1), Ring(cx, cz, r0, y, a1), up, paint);
            }
        }

        /// <summary>
        /// A wall in the plane z, facing +z, from (x0, y0) to (x1, y1), with rectangular holes. Each hole gets reveals of
        /// <paramref name="depth"/> into the wall and a dark back, so an empty opening never shows through.
        /// </summary>
        public void WallWithHoles(float x0, float y0, float x1, float y1, float z, IList<Rect2> holes, float depth, HousePaint paint)
        {
            var xs = new SortedSet<float> { x0, x1 };
            var ys = new SortedSet<float> { y0, y1 };
            foreach (Rect2 h in holes)
            {
                xs.Add(h.X0);
                xs.Add(h.X1);
                ys.Add(h.Y0);
                ys.Add(h.Y1);
            }

            float[] gx = new List<float>(xs).ToArray(), gy = new List<float>(ys).ToArray();
            var n = new V3(0, 0, 1);
            for (int j = 0; j + 1 < gy.Length; j++)
            {
                // Runs of solid cells along a row become one quad.
                int start = -1;
                for (int i = 0; i + 1 <= gx.Length; i++)
                {
                    bool solid = i + 1 < gx.Length && !InHole(holes, (gx[i] + gx[i + 1]) / 2f, (gy[j] + gy[j + 1]) / 2f);
                    if (solid && start < 0)
                    {
                        start = i;
                    }
                    else if (!solid && start >= 0)
                    {
                        Quad(new V3(gx[start], gy[j], z), new V3(gx[i], gy[j], z), new V3(gx[i], gy[j + 1], z), new V3(gx[start], gy[j + 1], z), n, paint);
                        start = -1;
                    }
                }
            }

            foreach (Rect2 h in holes)
            {
                float zb = z - depth;
                Quad(new V3(h.X0, h.Y0, z), new V3(h.X0, h.Y1, z), new V3(h.X0, h.Y1, zb), new V3(h.X0, h.Y0, zb), new V3(1, 0, 0), paint);
                Quad(new V3(h.X1, h.Y0, z), new V3(h.X1, h.Y0, zb), new V3(h.X1, h.Y1, zb), new V3(h.X1, h.Y1, z), new V3(-1, 0, 0), paint);
                Quad(new V3(h.X0, h.Y1, z), new V3(h.X1, h.Y1, z), new V3(h.X1, h.Y1, zb), new V3(h.X0, h.Y1, zb), new V3(0, -1, 0), paint);
                Quad(new V3(h.X0, h.Y0, z), new V3(h.X0, h.Y0, zb), new V3(h.X1, h.Y0, zb), new V3(h.X1, h.Y0, z), new V3(0, 1, 0), paint);
                Quad(new V3(h.X0, h.Y0, zb), new V3(h.X1, h.Y0, zb), new V3(h.X1, h.Y1, zb), new V3(h.X0, h.Y1, zb), n, HousePaint.Dark);
            }
        }

        public Box3 Bounds()
        {
            if (Positions.Count == 0)
            {
                return new Box3(0, 0, 0, 0, 0, 0);
            }

            float ax = float.MaxValue, ay = float.MaxValue, az = float.MaxValue, bx = float.MinValue, by = float.MinValue, bz = float.MinValue;
            foreach (V3 p in Positions)
            {
                ax = Math.Min(ax, p.X);
                ay = Math.Min(ay, p.Y);
                az = Math.Min(az, p.Z);
                bx = Math.Max(bx, p.X);
                by = Math.Max(by, p.Y);
                bz = Math.Max(bz, p.Z);
            }

            return new Box3(ax, ay, az, bx, by, bz);
        }

        private static bool InHole(IList<Rect2> holes, float x, float y)
        {
            foreach (Rect2 h in holes)
            {
                if (x > h.X0 && x < h.X1 && y > h.Y0 && y < h.Y1)
                {
                    return true;
                }
            }

            return false;
        }

        private static V3 Arc(float cx, float cy, float z, float r, int s, int segments)
        {
            double a = Math.PI * s / segments;
            return new V3(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a), z);
        }

        private static V3 Ring(float cx, float cz, float r, float y, double a) => new V3(cx + r * (float)Math.Cos(a), y, cz + r * (float)Math.Sin(a));

        private static V3 Sphere(float cx, float cy, float cz, float r, double elevation, double azimuth) =>
            new V3(cx + r * (float)(Math.Cos(elevation) * Math.Cos(azimuth)), cy + r * (float)Math.Sin(elevation), cz + r * (float)(Math.Cos(elevation) * Math.Sin(azimuth)));

        private int Vertex(V3 local, V3 n)
        {
            V3 p = local + Offset;
            Positions.Add(p);
            Normals.Add(n);

            // Planar uvs in metres from the dominant axis of the normal.
            float ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            if (ay >= ax && ay >= az)
            {
                U.Add(p.X);
                V.Add(p.Z);
            }
            else if (ax >= az)
            {
                U.Add(p.Z);
                V.Add(p.Y);
            }
            else
            {
                U.Add(p.X);
                V.Add(p.Y);
            }

            return Positions.Count - 1;
        }

        private void Triangle(int a, int b, int c, V3 n, HousePaint paint)
        {
            List<int> list = Triangles[(int)paint];
            if (V3.Dot(V3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]), n) >= 0f)
            {
                list.Add(a);
                list.Add(b);
                list.Add(c);
            }
            else
            {
                list.Add(a);
                list.Add(c);
                list.Add(b);
            }
        }
    }

    [Flags]
    public enum Faces
    {
        None = 0,
        Front = 1,
        Back = 2,
        Top = 4,
        Bottom = 8,
        Left = 16,
        Right = 32,
    }

    /// <summary>A rectangle in a wall plane: x across, y up (house space).</summary>
    public readonly struct Rect2
    {
        public Rect2(float x0, float y0, float x1, float y1)
        {
            X0 = Math.Min(x0, x1);
            Y0 = Math.Min(y0, y1);
            X1 = Math.Max(x0, x1);
            Y1 = Math.Max(y0, y1);
        }

        public float X0 { get; }

        public float Y0 { get; }

        public float X1 { get; }

        public float Y1 { get; }

        public float Width => X1 - X0;

        public float Height => Y1 - Y0;
    }
}
