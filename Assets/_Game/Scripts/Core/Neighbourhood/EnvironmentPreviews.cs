using System;
using UtsavNights.Core.Art;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>
    /// Painted preview cards for the environment browser (Phase 8 step 6, PRD 12): a night sky with a moon, stars and a
    /// burst; a row of the environment's houses in its own wall colours with lit windows (tiled roofs where it has them);
    /// the hills behind the hill village; the ghat steps, river and floating diyas of the riverside. Seeded: every build
    /// paints the same card.
    /// </summary>
    public static class EnvironmentPreviews
    {
        public const int Width = 512;
        public const int Height = 288;

        public static PaintCanvas Paint(EnvironmentInfo env, int width = Width, int height = Height)
        {
            var c = new PaintCanvas(width, height);
            c.VerticalGradient(StoreArtPainter.NightTop, StoreArtPainter.NightBottom);
            var rng = new Random(env.Id.Length * 131 + env.Id[0]);
            for (int i = 0; i < 70; i++)
            {
                float x = (float)rng.NextDouble() * width, y = (float)rng.NextDouble() * height * 0.55f;
                c.Glow(x, y, 1.2f + (float)rng.NextDouble(), StoreArtPainter.Spark, 0.6f);
            }

            c.Glow(width * 0.82f, height * 0.16f, height * 0.06f, new Rgb(0.95f, 0.93f, 0.85f), 1.4f);
            float bx = width * (0.25f + 0.3f * (float)rng.NextDouble()), by = height * 0.22f;
            Rgb burst = rng.Next(2) == 0 ? StoreArtPainter.Marigold : StoreArtPainter.Magenta;
            for (int i = 0; i < 18; i++)
            {
                double a = 2 * Math.PI * i / 18;
                c.Streak(bx, by, bx + (float)Math.Cos(a) * height * 0.12f, by + (float)Math.Sin(a) * height * 0.12f, 1.4f, burst, 0.9f);
            }

            bool village = env.Id == Environments.HillVillage, river = env.Id == Environments.Riverside;
            float ground = height * (river ? 0.62f : 0.76f);
            if (village)
            {
                // Ridges behind the houses.
                for (int i = 0; i < 6; i++)
                {
                    float cx = width * (i / 5f), w = width * 0.32f, h = height * (0.22f + 0.08f * (i % 2));
                    c.FillShape((px, py) => Ridge(px, py, cx, ground, w, h), new Rgb(0.07f, 0.05f, 0.13f));
                }
            }

            // The houses.
            int houses = 6;
            float hw = width / (float)houses;
            for (int i = 0; i < houses; i++)
            {
                Rgb wall = HomeCatalog.WallColours[env.WallColours[i % env.WallColours.Length]];
                Rgb dark = new Rgb(wall.R * 0.33f, wall.G * 0.3f, wall.B * 0.36f);
                float x0 = i * hw + hw * 0.06f, x1 = (i + 1) * hw - hw * 0.06f;
                float top = ground - height * (0.2f + 0.06f * ((i * 7) % 3));
                c.FillShape((px, py) => Box(px, py, x0, top, x1, ground), dark);
                if (village || (i % 3 == 2 && !river))
                {
                    float roofH = height * 0.07f;
                    c.FillShape((px, py) => Roof(px, py, x0 - 4f, x1 + 4f, top, roofH), new Rgb(0.3f, 0.12f, 0.08f));
                }

                for (int f = 0; f < 2; f++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float wx = x0 + (x1 - x0) * (0.18f + 0.3f * k), wy = top + (ground - top) * (0.22f + 0.42f * f);
                        float ww = (x1 - x0) * 0.13f, wh = (ground - top) * 0.2f;
                        if ((i + f + k) % 4 != 0)
                        {
                            c.FillShape((px, py) => Box(px, py, wx, wy, wx + ww, wy + wh), new Rgb(1f, 0.68f, 0.32f));
                            c.Glow(wx + ww / 2f, wy + wh / 2f, ww, new Rgb(1f, 0.6f, 0.25f), 0.25f);
                        }
                    }
                }

                // A string of lights along the roofline.
                for (float lx = x0; lx < x1; lx += 7f)
                {
                    c.Glow(lx, top + 3f, 1.6f, i % 2 == 0 ? StoreArtPainter.Marigold : StoreArtPainter.Magenta, 0.9f);
                }
            }

            Rgb earth = new Rgb(env.Ground.R * 0.6f, env.Ground.G * 0.6f, env.Ground.B * 0.6f);
            c.FillShape((px, py) => Box(px, py, 0f, ground, width, height), earth);
            if (river)
            {
                // Ghat steps, then the river with floating diyas and their reflections.
                for (int s = 0; s < 4; s++)
                {
                    float y0 = ground + s * height * 0.035f;
                    c.FillShape((px, py) => Box(px, py, 0f, y0, width, y0 + height * 0.012f), new Rgb(0.45f, 0.38f, 0.32f));
                }

                float water = ground + height * 0.15f;
                c.FillShape((px, py) => Box(px, py, 0f, water, width, height), new Rgb(0.04f, 0.05f, 0.14f));
                for (int i = 0; i < 40; i++)
                {
                    float x = (float)rng.NextDouble() * width, y = water + (float)rng.NextDouble() * (height - water);
                    c.Glow(x, y, 2.2f, StoreArtPainter.Saffron, 1.2f);
                    c.GlowStretched(x, y + 6f, 3f, 0.3f, StoreArtPainter.Saffron, 0.25f);
                }
            }
            else
            {
                // Diyas along the ground.
                for (float x = 10f; x < width; x += 26f)
                {
                    c.Glow(x, ground + height * 0.05f, 2.2f, StoreArtPainter.Saffron, 1.1f);
                }
            }

            c.Clamp();
            return c;
        }

        private static float Box(float px, float py, float x0, float y0, float x1, float y1)
        {
            float dx = Math.Max(x0 - px, px - x1), dy = Math.Max(y0 - py, py - y1);
            return Math.Max(dx, dy);
        }

        /// <summary>A gable roof sitting on <paramref name="baseY"/>, from x0 to x1, <paramref name="h"/> tall (y grows downward).</summary>
        private static float Roof(float px, float py, float x0, float x1, float baseY, float h)
        {
            float mid = (x0 + x1) / 2f, half = (x1 - x0) / 2f;
            float rise = h * (1f - Math.Abs(px - mid) / half);
            return Math.Max(Math.Max(x0 - px, px - x1), Math.Max(baseY - rise - py, py - baseY));
        }

        private static float Ridge(float px, float py, float cx, float baseY, float w, float h)
        {
            float t = Math.Abs(px - cx) / (w / 2f);
            float top = baseY - h * Math.Max(0f, 1f - t * t);
            return Math.Max(top - py, py - baseY);
        }
    }
}
