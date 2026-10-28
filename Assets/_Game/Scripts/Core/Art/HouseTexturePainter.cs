using System;

namespace UtsavNights.Core.Art
{
    /// <summary>
    /// Wall textures for the house (Phase 7 step 2, LICENCE_LOG own-008): plaster, exposed brick, sandstone ashlar and
    /// wooden planks. They are near white with the material's own pattern, so the wall colour the player picks tints them
    /// to that colour. All tile seamlessly; the view maps one texture to <see cref="Metres"/> of wall.
    /// </summary>
    public static class HouseTexturePainter
    {
        /// <summary>Wall metres per texture repeat, by HomeCatalog.Materials index (plaster, brick, stone, wood).</summary>
        public static readonly float[] Metres = { 2f, 1f, 2f, 1.2f };

        public static PaintCanvas Paint(int material, int size = 512)
        {
            switch (material)
            {
                case 1:
                    return Brick(size);
                case 2:
                    return Ashlar(size);
                case 3:
                    return Planks(size);
                default:
                    return Plaster(size);
            }
        }

        /// <summary>Lime plaster: soft mottling, a fine grain and faint rain streaks.</summary>
        public static PaintCanvas Plaster(int size = 512)
        {
            var canvas = new PaintCanvas(size, size);
            var coarse = new ValueNoise(21, 6);
            var fine = new ValueNoise(22, 96);
            var streaks = new ValueNoise(23, 40);
            Fill(canvas, (u, v) =>
            {
                float k = 0.88f + 0.08f * coarse.Sample(u, v) + 0.05f * fine.Sample(u, v) - 0.05f * Math.Max(0f, streaks.Sample(u, v * 0.05f) - 0.5f) * 2f;
                return new Rgb(k, k, k * 0.98f);
            });
            return canvas;
        }

        /// <summary>Running-bond brick, 4 bricks across and 12 courses per repeat (1 m), with light mortar.</summary>
        public static PaintCanvas Brick(int size = 512)
        {
            var canvas = new PaintCanvas(size, size);
            var noise = new ValueNoise(31, 64);
            var random = new Random(32);
            const int across = 4, courses = 12;
            var shade = new float[across * courses];
            for (int i = 0; i < shade.Length; i++)
            {
                shade[i] = 0.72f + 0.26f * (float)random.NextDouble();
            }

            Fill(canvas, (u, v) =>
            {
                float row = v * courses;
                int r = (int)row;
                float bu = u * across + (r % 2 == 0 ? 0f : 0.5f);
                int b = (int)Math.Floor(bu) % across;
                float fx = bu - (float)Math.Floor(bu), fy = row - r;
                bool mortar = fx < 0.035f || fy < 0.12f;
                float n = 0.9f + 0.1f * noise.Sample(u, v);
                if (mortar)
                {
                    return new Rgb(1f * n, 0.97f * n, 0.92f * n);
                }

                float k = shade[r % courses * across + (b + across) % across] * n;
                return new Rgb(k, k * 0.93f, k * 0.9f);
            });
            return canvas;
        }

        /// <summary>Dressed sandstone blocks, 3 across and 4 courses per repeat (2 m), with thin joints.</summary>
        public static PaintCanvas Ashlar(int size = 512)
        {
            var canvas = new PaintCanvas(size, size);
            var noise = new ValueNoise(41, 48);
            var grain = new ValueNoise(42, 160);
            var random = new Random(43);
            const int across = 3, courses = 4;
            var shade = new float[across * courses];
            for (int i = 0; i < shade.Length; i++)
            {
                shade[i] = 0.84f + 0.14f * (float)random.NextDouble();
            }

            Fill(canvas, (u, v) =>
            {
                float row = v * courses;
                int r = (int)row;
                float bu = u * across + (r % 2 == 0 ? 0f : 0.5f);
                int b = (int)Math.Floor(bu) % across;
                float fx = bu - (float)Math.Floor(bu), fy = row - r;
                float n = 0.88f + 0.08f * noise.Sample(u, v) + 0.04f * grain.Sample(u, v);
                if (fx < 0.012f || fy < 0.02f)
                {
                    return new Rgb(0.62f * n, 0.6f * n, 0.58f * n);
                }

                float k = shade[r % courses * across + (b + across) % across] * n;
                return new Rgb(k, k * 0.97f, k * 0.93f);
            });
            return canvas;
        }

        /// <summary>Vertical planks, 8 per repeat (1.2 m), with grain lines and dark gaps.</summary>
        public static PaintCanvas Planks(int size = 512)
        {
            var canvas = new PaintCanvas(size, size);
            var grain = new ValueNoise(51, 12);
            var fine = new ValueNoise(52, 128);
            var random = new Random(53);
            const int planks = 8;
            var shade = new float[planks];
            for (int i = 0; i < planks; i++)
            {
                shade[i] = 0.78f + 0.2f * (float)random.NextDouble();
            }

            Fill(canvas, (u, v) =>
            {
                float pu = u * planks;
                int p = (int)pu % planks;
                float fx = pu - (int)pu;
                if (fx < 0.03f)
                {
                    return new Rgb(0.35f, 0.33f, 0.32f);
                }

                // Grain: stretched noise along the plank, offset per plank.
                float g = grain.Sample(fx * 0.3f + p * 0.37f, v) * 0.7f + fine.Sample(u, v * 4f) * 0.3f;
                float k = shade[p] * (0.86f + 0.14f * g);
                return new Rgb(k, k * 0.95f, k * 0.9f);
            });
            return canvas;
        }

        private static void Fill(PaintCanvas canvas, Func<float, float, Rgb> colour)
        {
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    Rgb c = colour(x / (float)canvas.Width, y / (float)canvas.Height);
                    int i = (y * canvas.Width + x) * 4;
                    canvas.Pixels[i] = Math.Max(0f, Math.Min(1f, c.R));
                    canvas.Pixels[i + 1] = Math.Max(0f, Math.Min(1f, c.G));
                    canvas.Pixels[i + 2] = Math.Max(0f, Math.Min(1f, c.B));
                    canvas.Pixels[i + 3] = 1f;
                }
            }
        }
    }
}
