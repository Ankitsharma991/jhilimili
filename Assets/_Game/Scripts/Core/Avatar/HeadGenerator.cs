using System;

namespace UtsavNights.Core.Avatar
{
    /// <summary>
    /// The head and everything on it (Phase 6 step 2): face shape, ears, eyes, nose, mouth, brows, hairstyles, facial hair
    /// and glasses, rigid on the Head bone. Three submeshes: <see cref="Skin"/> (tinted to the skin tone), <see cref="Hair"/>
    /// (hair colour, drawn double-sided), and <see cref="Features"/>, which samples a small palette texture by uv
    /// (eye whites, irises, pupils, lips, frames).
    /// </summary>
    public static class HeadGenerator
    {
        public const int Skin = 0, Hair = 1, Features = 2;

        /// <summary>Palette cells (u = (cell + 0.5) / PaletteCells): white, black, lips, then the six eye colours.</summary>
        public const int PaletteCells = 16, CellWhite = 0, CellBlack = 1, CellLips = 2, CellEyeFirst = 3, CellLens = 9;

        /// <summary>Centre of the head, in avatar space at medium height.</summary>
        public static readonly V3 Center = new V3(0f, 1.665f, 0.01f);

        public const float Rx = 0.108f, Ry = 0.128f, Rz = 0.118f;

        public static MeshData Build(AvatarProfile p, bool hideHair)
        {
            var mesh = new MeshData();
            Face(mesh, p.faceShape);
            Ears(mesh);
            Eyes(mesh, p.eyes, p.eyeColour);
            Nose(mesh, p.nose);
            Mouth(mesh, p.mouth);
            Brows(mesh, p.brows);
            if (!hideHair)
            {
                HairStyle(mesh, p.hairStyle);
            }

            FacialHair(mesh, p.facialHair);
            Glasses(mesh, p.glasses);

            // Keep the submesh count fixed (3) even when hair or features are empty.
            while (mesh.Submeshes.Count < 3)
            {
                mesh.Submeshes.Add(new System.Collections.Generic.List<int>());
                mesh.TriangleRegions.Add(new System.Collections.Generic.List<BodyRegion>());
            }

            return mesh;
        }

        /// <summary>Where the face surface is in front of (x, y), for placing features on it.</summary>
        public static float FrontZ(float x, float y, float inset = 0f)
        {
            float nx = x / Rx, ny = (y - Center.Y) / Ry;
            float k = Math.Max(0f, 1f - nx * nx - ny * ny);
            return Center.Z + Rz * (float)Math.Sqrt(k) - inset;
        }

        private static void Face(MeshData mesh, int shape)
        {
            // Width multiplier by height on the head (t = -1 chin to 1 crown) and cross-section squareness per face shape.
            Func<float, float> width;
            float power = 2f, tall = 1f;
            switch (shape)
            {
                case 1: // oval: softer, narrower jaw
                    width = t => t < 0 ? 0.82f + 0.18f * (1f + t) : 1f;
                    tall = 1.04f;
                    break;
                case 2: // square: full jaw, boxy
                    width = t => t < 0 ? 0.95f : 1f;
                    power = 2.7f;
                    break;
                case 3: // heart: wide cheekbones, pointed chin
                    width = t => t < -0.2f ? 0.7f + 0.35f * (1f + t) : t < 0.3f ? 1.04f : 1f;
                    break;
                case 4: // long
                    width = t => t < 0 ? 0.88f + 0.12f * (1f + t) : 0.96f;
                    tall = 1.1f;
                    break;
                default: // round
                    width = t => 1f;
                    break;
            }

            const int rings = 14, segments = 18;
            var parts = new System.Collections.Generic.List<Ring>();
            for (int i = 0; i <= rings; i++)
            {
                float t = -1f + 2f * i / rings;
                float profile = (float)Math.Sqrt(Math.Max(0.0025, 1.0 - t * t));
                float w = width(t);
                parts.Add(new Ring
                {
                    Center = new V3(0f, Center.Y + t * Ry * tall, Center.Z),
                    Axis = V3.Up,
                    Side = Rx * profile * w,
                    Depth = Rz * profile * (0.92f + 0.08f * w),
                    BoneA = AvatarSkeleton.Head,
                    BoneB = AvatarSkeleton.Head,
                    Power = power,
                    Region = BodyRegion.None,
                });
            }

            BodyGenerator.Loft(mesh, Skin, parts, segments, capStart: true, capEnd: true);
        }

        private static void Ears(MeshData mesh)
        {
            foreach (int side in new[] { -1, 1 })
            {
                Blob(mesh, Skin, new V3(side * (Rx + 0.004f), Center.Y - 0.005f, Center.Z - 0.01f), new V3(0.012f, 0.03f, 0.022f), 6, 8);
            }
        }

        private static void Eyes(MeshData mesh, int shape, int colour)
        {
            // Width, height and spacing per eye shape: almond, round, narrow, hooded.
            float w = shape == 1 ? 0.016f : shape == 2 ? 0.02f : 0.019f;
            float h = shape == 1 ? 0.013f : shape == 2 ? 0.007f : 0.01f;
            float y = Center.Y + 0.008f;
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 0.04f;
                float z = FrontZ(x, y);
                Blob(mesh, Features, new V3(x, y, z - 0.004f), new V3(w, h, 0.006f), 5, 8, CellWhite);
                Blob(mesh, Features, new V3(x, y, z - 0.0005f), new V3(0.0085f, Math.Min(h, 0.0085f), 0.004f), 4, 8, CellEyeFirst + Math.Max(0, Math.Min(5, colour)));
                Blob(mesh, Features, new V3(x, y, z + 0.001f), new V3(0.004f, 0.004f, 0.003f), 3, 6, CellBlack);
                if (shape == 3)
                {
                    // Hooded: a skin lid over the upper half.
                    Blob(mesh, Skin, new V3(x, y + h * 0.75f, z - 0.002f), new V3(w * 1.08f, h * 0.55f, 0.007f), 4, 8);
                }
            }
        }

        private static void Nose(MeshData mesh, int shape)
        {
            float y = Center.Y - 0.03f;
            float size = shape == 1 ? 1.25f : shape == 2 ? 0.85f : 1f;
            float width = shape == 3 ? 1.35f : 1f;
            Blob(mesh, Skin, new V3(0f, y, FrontZ(0f, y) + 0.004f * size), new V3(0.011f * size * width, 0.02f * size, 0.012f * size), 5, 8);
        }

        private static void Mouth(MeshData mesh, int shape)
        {
            float y = Center.Y - 0.068f;
            float w = shape == 1 ? 0.026f : shape == 2 ? 0.018f : 0.022f;
            float h = shape == 3 ? 0.0075f : 0.005f;
            Blob(mesh, Features, new V3(0f, y, FrontZ(0f, y) - 0.002f), new V3(w, h, 0.006f), 4, 10, CellLips);
            if (shape == 1)
            {
                // A smile: corners raised.
                foreach (int side in new[] { -1, 1 })
                {
                    Blob(mesh, Features, new V3(side * w * 0.9f, y + 0.004f, FrontZ(side * w * 0.9f, y) - 0.002f), new V3(0.005f, 0.004f, 0.004f), 3, 6, CellLips);
                }
            }
        }

        private static void Brows(MeshData mesh, int shape)
        {
            float y = Center.Y + 0.032f;
            float thick = shape == 1 ? 0.006f : shape == 2 ? 0.0035f : 0.0045f;
            float lift = shape == 3 ? 0.004f : 0f;
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 0.041f;
                Blob(mesh, Hair, new V3(x, y + lift, FrontZ(x, y) - 0.001f), new V3(0.019f, thick, 0.005f), 3, 8);
            }
        }

        private static void HairStyle(MeshData mesh, int style)
        {
            const float pad = 1.07f;
            switch (style)
            {
                case 0:
                    return; // shaved
                case 1: // short crop
                    Shell(mesh, pad, n => n.Y > Hairline(n, -0.35f, 0.45f), n => 1f);
                    break;
                case 2: // side part, more volume on one side
                    Shell(mesh, pad, n => n.Y > Hairline(n, -0.35f, 0.42f), n => 1f + 0.08f * Math.Max(0f, n.X) * Math.Max(0f, n.Y));
                    break;
                case 3: // short curls
                    Shell(mesh, 1.1f, n => n.Y > Hairline(n, -0.3f, 0.45f), n => 1f + 0.035f * (float)Math.Sin(n.X * 23f) * (float)Math.Sin(n.Z * 19f + n.Y * 17f));
                    break;
                case 4: // bob to the jaw
                    Shell(mesh, pad, n => n.Y > LongHairline(n), n => 1f + 0.05f * Math.Max(0f, -n.Y));
                    break;
                case 5: // long straight: bob plus a fall down the back
                    Shell(mesh, pad, n => n.Y > LongHairline(n), n => 1f);
                    Panel(mesh, new V3(0f, Center.Y - 0.05f, Center.Z - Rz * 0.92f), 0.2f, 0.32f, 0.05f);
                    break;
                case 6: // bun
                    Shell(mesh, pad, n => n.Y > Hairline(n, -0.4f, 0.45f), n => 1f);
                    Blob(mesh, Hair, Center + new V3(0f, 0.07f, -Rz * 1.05f), new V3(0.05f, 0.045f, 0.045f), 7, 10);
                    break;
                case 7: // braid down the back
                    Shell(mesh, pad, n => n.Y > Hairline(n, -0.4f, 0.45f), n => 1f);
                    for (int i = 0; i < 7; i++)
                    {
                        float r = 0.026f - i * 0.0018f;
                        Blob(mesh, Hair, Center + new V3(0f, -0.06f - i * 0.045f, -Rz * 1.02f - 0.01f - i * 0.006f), new V3(r, r * 1.25f, r), 5, 8);
                    }

                    break;
                case 8: // big curls
                    Shell(mesh, 1.3f, n => n.Y > Hairline(n, -0.45f, 0.5f), n => 1f + 0.05f * (float)Math.Sin(n.X * 17f) * (float)Math.Sin(n.Y * 13f + n.Z * 11f));
                    break;
                default: // ponytail
                    Shell(mesh, pad, n => n.Y > Hairline(n, -0.4f, 0.45f), n => 1f);
                    for (int i = 0; i < 5; i++)
                    {
                        float r = 0.03f - i * 0.004f;
                        Blob(mesh, Hair, Center + new V3(0f, 0.02f - i * 0.05f, -Rz * 1.08f - 0.02f), new V3(r, r * 1.5f, r), 5, 8);
                    }

                    break;
            }
        }

        private static void FacialHair(MeshData mesh, int style)
        {
            if (style == 0)
            {
                return;
            }

            float my = Center.Y - 0.052f;
            if (style == 1 || style == 4)
            {
                // Moustache.
                Blob(mesh, Hair, new V3(0f, my, FrontZ(0f, my) + 0.002f), new V3(0.028f, 0.006f, 0.008f), 4, 10);
            }

            if (style == 2 || style == 4)
            {
                // Beard along the jaw and chin.
                Shell(mesh, 1.05f, n => n.Y < -0.35f && n.Z > -0.45f && !(Math.Abs(n.X) < 0.28f && n.Y > -0.72f && n.Z > 0.55f), n => 1f + 0.06f * Math.Max(0f, -n.Y - 0.6f));
            }

            if (style == 3)
            {
                // Goatee.
                float cy = Center.Y - 0.1f;
                Blob(mesh, Hair, new V3(0f, cy, FrontZ(0f, cy) - 0.006f), new V3(0.022f, 0.022f, 0.016f), 5, 8);
            }
        }

        private static void Glasses(MeshData mesh, int style)
        {
            if (style == 0)
            {
                return;
            }

            float y = Center.Y + 0.008f;
            int cell = style == 3 ? CellLens : CellBlack;
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 0.04f;
                float z = FrontZ(x, y) + 0.012f;
                if (style == 2)
                {
                    Frame(mesh, new V3(x, y, z), 0.022f, 0.016f, 0.0025f, square: true, cell: CellBlack);
                }
                else
                {
                    Frame(mesh, new V3(x, y, z), 0.019f, 0.017f, 0.0025f, square: false, cell: CellBlack);
                }

                if (style == 3)
                {
                    Blob(mesh, Features, new V3(x, y, z - 0.001f), new V3(0.019f, 0.016f, 0.002f), 3, 10, cell);
                }

                // Arm back to the ear.
                Blob(mesh, Features, new V3(side * (Rx + 0.002f), y + 0.002f, Center.Z + 0.03f), new V3(0.002f, 0.002f, 0.05f), 3, 4, CellBlack);
            }

            Blob(mesh, Features, new V3(0f, y + 0.004f, FrontZ(0f, y) + 0.012f), new V3(0.012f, 0.0025f, 0.0025f), 3, 4, CellBlack);
        }

        /// <summary>
        /// Height (normalised, -1 chin to 1 crown) above which short hair grows at a direction: <paramref name="back"/> at the
        /// nape, rising smoothly to <paramref name="front"/> at the forehead, so the face and brows stay clear.
        /// </summary>
        private static float Hairline(V3 n, float back, float front) => back + (front - back) * Smooth(-0.3f, 0.6f, n.Z);

        /// <summary>Long hair: down to the jaw at the sides and back, forehead hairline in front.</summary>
        private static float LongHairline(V3 n) => -0.62f + 1.07f * Smooth(0.2f, 0.55f, n.Z);

        private static float Smooth(float a, float b, float x)
        {
            float t = Math.Max(0f, Math.Min(1f, (x - a) / (b - a)));
            return t * t * (3f - 2f * t);
        }

        /// <summary>A partial ellipsoid around the head (hair, beards): only patches whose direction passes keep() are built.</summary>
        private static void Shell(MeshData mesh, float scale, Func<V3, bool> keep, Func<V3, float> bulge)
        {
            const int lat = 16, lon = 24;
            int[,] index = new int[lat + 1, lon + 1];
            for (int i = 0; i <= lat; i++)
            {
                double phi = Math.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    double theta = 2 * Math.PI * j / lon;
                    var n = new V3((float)(Math.Sin(phi) * Math.Sin(theta)), (float)Math.Cos(phi), (float)(Math.Sin(phi) * Math.Cos(theta)));
                    float k = scale * bulge(n);
                    var p = Center + new V3(n.X * Rx * k, n.Y * Ry * k, n.Z * Rz * k);
                    index[i, j] = mesh.AddVertex(p, n, (float)j / lon, (float)i / lat, AvatarSkeleton.Head);
                }
            }

            for (int i = 0; i < lat; i++)
            {
                for (int j = 0; j < lon; j++)
                {
                    double phi = Math.PI * (i + 0.5) / lat, theta = 2 * Math.PI * (j + 0.5) / lon;
                    var mid = new V3((float)(Math.Sin(phi) * Math.Sin(theta)), (float)Math.Cos(phi), (float)(Math.Sin(phi) * Math.Cos(theta)));
                    if (!keep(mid))
                    {
                        continue;
                    }

                    mesh.AddTriangle(Hair, index[i, j], index[i + 1, j], index[i, j + 1], BodyRegion.Hair);
                    mesh.AddTriangle(Hair, index[i, j + 1], index[i + 1, j], index[i + 1, j + 1], BodyRegion.Hair);
                }
            }
        }

        /// <summary>A small closed ellipsoid; <paramref name="cell"/> selects a palette colour on the features submesh.</summary>
        public static void Blob(MeshData mesh, int submesh, V3 center, V3 radii, int lat, int lon, int cell = -1, int bone = AvatarSkeleton.Head)
        {
            float u = cell >= 0 ? (cell + 0.5f) / PaletteCells : 0.5f;
            int start = mesh.VertexCount;
            for (int i = 0; i <= lat; i++)
            {
                double phi = Math.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    double theta = 2 * Math.PI * j / lon;
                    var n = new V3((float)(Math.Sin(phi) * Math.Sin(theta)), (float)Math.Cos(phi), (float)(Math.Sin(phi) * Math.Cos(theta)));
                    var p = center + new V3(n.X * radii.X, n.Y * radii.Y, n.Z * radii.Z);
                    mesh.AddVertex(p, new V3(n.X / radii.X, n.Y / radii.Y, n.Z / radii.Z), cell >= 0 ? u : (float)j / lon, cell >= 0 ? 0.5f : (float)i / lat, bone);
                }
            }

            int stride = lon + 1;
            for (int i = 0; i < lat; i++)
            {
                for (int j = 0; j < lon; j++)
                {
                    int a = start + i * stride + j;
                    mesh.AddTriangle(submesh, a, a + stride, a + 1, BodyRegion.None);
                    mesh.AddTriangle(submesh, a + 1, a + stride, a + stride + 1, BodyRegion.None);
                }
            }
        }

        private static void Panel(MeshData mesh, V3 top, float width, float length, float thickness) =>
            Blob(mesh, Hair, top + new V3(0f, -length / 2f, -thickness), new V3(width / 2f, length / 2f, thickness), 6, 10);

        /// <summary>A glasses rim: a ring of small segments (oval or square) facing forward.</summary>
        private static void Frame(MeshData mesh, V3 center, float rx, float ry, float thickness, bool square, int cell)
        {
            const int steps = 16;
            for (int k = 0; k < steps; k++)
            {
                double a = 2 * Math.PI * k / steps;
                float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                if (square)
                {
                    float m = Math.Max(Math.Abs(c), Math.Abs(s));
                    c /= m;
                    s /= m;
                }

                Blob(mesh, Features, center + new V3(c * rx, s * ry, 0f), new V3(thickness, thickness, thickness), 2, 4, cell);
            }
        }
    }
}
