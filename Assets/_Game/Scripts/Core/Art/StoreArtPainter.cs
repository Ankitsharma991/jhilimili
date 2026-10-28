using System;

namespace UtsavNights.Core.Art
{
    /// <summary>A float RGBA image, top row first, with additive light painting (sparks, glows, streaks).</summary>
    public sealed class PaintCanvas
    {
        public PaintCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new float[width * height * 4];
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>r, g, b, a per pixel, 0 to 1 after <see cref="Clamp"/>.</summary>
        public float[] Pixels { get; }

        public void Fill(float r, float g, float b, float a)
        {
            for (int i = 0; i < Pixels.Length; i += 4)
            {
                Pixels[i] = r;
                Pixels[i + 1] = g;
                Pixels[i + 2] = b;
                Pixels[i + 3] = a;
            }
        }

        /// <summary>Opaque vertical gradient from top colour to bottom colour.</summary>
        public void VerticalGradient(Rgb top, Rgb bottom)
        {
            for (int y = 0; y < Height; y++)
            {
                float t = Height == 1 ? 0f : (float)y / (Height - 1);
                Rgb c = Rgb.Lerp(top, bottom, t);
                for (int x = 0; x < Width; x++)
                {
                    Set(x, y, c, 1f);
                }
            }
        }

        /// <summary>Soft round light added on top (gaussian falloff), also raising alpha so it works on transparent layers.</summary>
        public void Glow(float cx, float cy, float radius, Rgb color, float intensity)
        {
            int r = (int)Math.Ceiling(radius * 3f);
            int x0 = Math.Max(0, (int)cx - r), x1 = Math.Min(Width - 1, (int)cx + r);
            int y0 = Math.Max(0, (int)cy - r), y1 = Math.Min(Height - 1, (int)cy + r);
            float inv = 1f / (2f * radius * radius);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float k = intensity * (float)Math.Exp(-(dx * dx + dy * dy) * inv);
                    if (k > 0.002f)
                    {
                        Add(x, y, color, k);
                    }
                }
            }
        }

        /// <summary>Like <see cref="Glow"/>, but stretched horizontally by <paramref name="stretchX"/> (panorama stars near the zenith).</summary>
        public void GlowStretched(float cx, float cy, float radius, float stretchX, Rgb color, float intensity)
        {
            float rx = radius * stretchX;
            int ix = (int)Math.Ceiling(rx * 3f), iy = (int)Math.Ceiling(radius * 3f);
            float invX = 1f / (2f * rx * rx), invY = 1f / (2f * radius * radius);
            for (int y = Math.Max(0, (int)cy - iy); y <= Math.Min(Height - 1, (int)cy + iy); y++)
            {
                for (int x = (int)cx - ix; x <= (int)cx + ix; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float k = intensity * (float)Math.Exp(-(dx * dx * invX + dy * dy * invY));
                    if (k > 0.002f)
                    {
                        // Wrap in x: the panorama is continuous around the horizon.
                        Add(((x % Width) + Width) % Width, y, color, k);
                    }
                }
            }
        }

        /// <summary>A tapered streak of light from (x0, y0) to (x1, y1): bright and wide at the start, fading out.</summary>
        public void Streak(float x0, float y0, float x1, float y1, float width, Rgb color, float intensity)
        {
            float length = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Math.Max(2, (int)(length / Math.Max(0.6f, width * 0.35f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float fade = 1f - t;
                Glow(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, Math.Max(0.6f, width * (0.35f + 0.65f * fade)), color, intensity * fade * fade);
            }
        }

        /// <summary>Solid shape: fills pixels where inside(x, y) is true with an opaque colour, with a 1-pixel soft edge.</summary>
        public void FillShape(Func<float, float, float> signedDistance, Rgb color)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float d = signedDistance(x + 0.5f, y + 0.5f);
                    float cover = d <= -0.5f ? 1f : d >= 0.5f ? 0f : 0.5f - d;
                    if (cover > 0f)
                    {
                        Blend(x, y, color, cover);
                    }
                }
            }
        }

        public void Clamp()
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = Math.Max(0f, Math.Min(1f, Pixels[i]));
            }
        }

        public Rgb Get(int x, int y, out float alpha)
        {
            int i = (y * Width + x) * 4;
            alpha = Pixels[i + 3];
            return new Rgb(Pixels[i], Pixels[i + 1], Pixels[i + 2]);
        }

        private void Set(int x, int y, Rgb c, float a)
        {
            int i = (y * Width + x) * 4;
            Pixels[i] = c.R;
            Pixels[i + 1] = c.G;
            Pixels[i + 2] = c.B;
            Pixels[i + 3] = a;
        }

        private void Add(int x, int y, Rgb c, float k)
        {
            int i = (y * Width + x) * 4;
            Pixels[i] += c.R * k;
            Pixels[i + 1] += c.G * k;
            Pixels[i + 2] += c.B * k;
            Pixels[i + 3] = Math.Min(1f, Pixels[i + 3] + k);
        }

        private void Blend(int x, int y, Rgb c, float cover)
        {
            int i = (y * Width + x) * 4;
            Pixels[i] += (c.R - Pixels[i]) * cover;
            Pixels[i + 1] += (c.G - Pixels[i + 1]) * cover;
            Pixels[i + 2] += (c.B - Pixels[i + 2]) * cover;
            Pixels[i + 3] += (1f - Pixels[i + 3]) * cover;
        }
    }

    public readonly struct Rgb
    {
        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public float R { get; }

        public float G { get; }

        public float B { get; }

        public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    /// <summary>
    /// Own procedural store art (Phase 5 step 5), in the game's palette (UiTheme): the app icon (a flowerpot
    /// fountain spraying gold and saffron sparks into an indigo night) and the Play feature graphic (a
    /// courtyard at night with aerial bursts, a fountain and string lights). Seeded, so every run paints
    /// the same pixels.
    /// </summary>
    public static class StoreArtPainter
    {
        public static readonly Rgb NightTop = new Rgb(0.05f, 0.035f, 0.14f);
        public static readonly Rgb NightBottom = new Rgb(0.17f, 0.10f, 0.32f);
        public static readonly Rgb Saffron = new Rgb(1f, 0.6f, 0.2f);
        public static readonly Rgb Marigold = new Rgb(1f, 0.76f, 0.16f);
        public static readonly Rgb Magenta = new Rgb(0.92f, 0.22f, 0.6f);
        public static readonly Rgb Spark = new Rgb(1f, 0.93f, 0.72f);
        public static readonly Rgb Clay = new Rgb(0.50f, 0.17f, 0.09f);
        public static readonly Rgb ClayBand = new Rgb(0.95f, 0.75f, 0.2f);
        public static readonly Rgb ClayStripe = new Rgb(0.80f, 0.16f, 0.45f);
        public static readonly Rgb Rooftop = new Rgb(0.035f, 0.025f, 0.08f);

        /// <summary>Android adaptive icons keep only the central 66 percent visible on every launcher mask.</summary>
        public const float AdaptiveSafeZone = 0.66f;

        /// <summary>The full icon: background and fountain together (Play's 512 px icon, legacy and round icons).</summary>
        public static PaintCanvas Icon(int size)
        {
            PaintCanvas canvas = IconBackground(size);
            PaintFountain(canvas, size * 0.5f, size * 0.80f, size * 0.92f);
            canvas.Clamp();
            return canvas;
        }

        public static PaintCanvas IconBackground(int size)
        {
            var canvas = new PaintCanvas(size, size);
            canvas.VerticalGradient(NightTop, NightBottom);
            canvas.Glow(size * 0.5f, size * 0.42f, size * 0.30f, Magenta, 0.10f);
            canvas.Clamp();
            return canvas;
        }

        /// <summary>The fountain alone on transparent, scaled into the adaptive icon safe zone.</summary>
        public static PaintCanvas IconForeground(int size)
        {
            var canvas = new PaintCanvas(size, size);
            canvas.Fill(0f, 0f, 0f, 0f);
            // 0.9: the outermost sparks stay inside the circular safe zone too.
            PaintFountain(canvas, size * 0.5f, size * (0.5f + AdaptiveSafeZone * 0.27f), size * AdaptiveSafeZone * 0.9f);
            canvas.Clamp();
            return canvas;
        }

        /// <summary>Play feature graphic, 1024 x 500: no text, so it works in every language.</summary>
        public static PaintCanvas FeatureGraphic(int width = 1024, int height = 500)
        {
            var canvas = new PaintCanvas(width, height);
            canvas.VerticalGradient(NightTop, NightBottom);
            var random = new Random(20261028);
            for (int i = 0; i < 70; i++)
            {
                canvas.Glow((float)random.NextDouble() * width, (float)random.NextDouble() * height * 0.55f, 0.8f, Spark,
                    0.25f + 0.35f * (float)random.NextDouble());
            }

            Burst(canvas, width * 0.24f, height * 0.30f, height * 0.24f, Magenta, 34, random);
            Burst(canvas, width * 0.55f, height * 0.20f, height * 0.30f, Marigold, 40, random);
            Burst(canvas, width * 0.82f, height * 0.34f, height * 0.20f, Saffron, 30, random);

            float ground = height * 0.80f;
            canvas.FillShape((x, y) => Skyline(x, y, width, ground), Rooftop);
            for (int i = 0; i < 26; i++)
            {
                float x = width * (0.04f + 0.92f * i / 25f);
                float sag = (float)Math.Sin(Math.PI * i / 25f) * height * 0.05f;
                Rgb bulb = i % 3 == 0 ? Magenta : i % 3 == 1 ? Marigold : Saffron;
                canvas.Glow(x, ground - height * 0.13f + sag, height * 0.008f, bulb, 1.4f);
                canvas.Glow(x, ground - height * 0.13f + sag, height * 0.025f, bulb, 0.25f);
            }

            PaintFountain(canvas, width * 0.40f, height * 0.96f, height * 0.55f);
            canvas.Clamp();
            return canvas;
        }

        /// <summary>Clay anar pot standing on baseY with curved sides and two thin bands, spraying sparks upward in arcs; total height h.</summary>
        private static void PaintFountain(PaintCanvas canvas, float cx, float baseY, float h)
        {
            var random = new Random(7);
            float potH = h * 0.17f, potW = h * 0.26f;
            float nozzleY = baseY - potH;
            canvas.Glow(cx, nozzleY, h * 0.14f, Saffron, 0.22f);
            for (int i = 0; i < 90; i++)
            {
                double angle = (random.NextDouble() - 0.5) * 1.25;
                double speed = h * (0.70 + 0.45 * random.NextDouble());
                double vx = Math.Sin(angle) * speed, vy = -Math.Cos(angle) * speed;
                double gravity = h * 1.35;
                double life = 0.5 + 0.4 * random.NextDouble();
                Rgb color = i % 4 == 0 ? Saffron : i % 4 == 1 ? Spark : Marigold;
                float px = cx, py = nozzleY;
                const int steps = 9;
                for (int s = 1; s <= steps; s++)
                {
                    double t = life * s / steps;
                    float x = cx + (float)(vx * t);
                    float y = nozzleY + (float)(vy * t + 0.5 * gravity * t * t);
                    float fade = 1f - (float)s / (steps + 1);
                    canvas.Streak(px, py, x, y, h * 0.0055f, color, 0.42f * fade);
                    px = x;
                    py = y;
                }

                canvas.Glow(px, py, h * 0.006f, Spark, 0.9f);
            }

            canvas.Glow(cx, nozzleY, h * 0.022f, Spark, 1.1f);
            canvas.FillShape((x, y) => Pot(x, y, cx, baseY, potW, potH), Clay);
            canvas.FillShape((x, y) => Stripe(x, y, cx, baseY, potW, potH, 0.45f, 0.07f), ClayBand);
            canvas.FillShape((x, y) => Stripe(x, y, cx, baseY, potW, potH, 0.72f, 0.05f), ClayStripe);
        }

        private static void Burst(PaintCanvas canvas, float cx, float cy, float radius, Rgb color, int rays, Random random)
        {
            canvas.Glow(cx, cy, radius * 0.22f, color, 0.5f);
            for (int i = 0; i < rays; i++)
            {
                double a = Math.PI * 2 * i / rays + random.NextDouble() * 0.12;
                float r = radius * (0.8f + 0.2f * (float)random.NextDouble());
                float ex = cx + (float)Math.Cos(a) * r, ey = cy + (float)Math.Sin(a) * r + radius * 0.12f;
                canvas.Streak(cx + (float)Math.Cos(a) * radius * 0.15f, cy + (float)Math.Sin(a) * radius * 0.15f, ex, ey,
                    radius * 0.035f, color, 0.6f);
                canvas.Glow(ex, ey, radius * 0.03f, Spark, 0.9f);
            }

            canvas.Glow(cx, cy, radius * 0.06f, Spark, 1.2f);
        }

        /// <summary>Half the pot's width at t (0 at the nozzle, 1 at the base): curved sides, narrow neck.</summary>
        private static float PotHalf(float width, float t) => width * (0.09f + 0.41f * (float)Math.Sqrt(Math.Max(0f, t)));

        /// <summary>Signed distance (pixels, negative inside) to the pot standing on baseY.</summary>
        private static float Pot(float x, float y, float cx, float baseY, float width, float height)
        {
            float top = baseY - height;
            float t = Math.Max(0f, Math.Min(1f, (y - top) / height));
            float outsideY = Math.Max(top - y, y - baseY);
            return Math.Max(outsideY, Math.Abs(x - cx) - PotHalf(width, t));
        }

        /// <summary>A band around the pot centred at fraction t down from the nozzle, thickness as a fraction of the height.</summary>
        private static float Stripe(float x, float y, float cx, float baseY, float width, float height, float t, float thickness)
        {
            float bandY = baseY - height + height * t;
            float half = height * thickness;
            float local = Math.Max(0f, Math.Min(1f, (y - (baseY - height)) / height));
            return Math.Max(Math.Abs(y - bandY) - half, Math.Abs(x - cx) - PotHalf(width, local) + 0.5f);
        }

        /// <summary>Flat rooftops and a temple shikhara against the sky, everything below ground filled.</summary>
        private static float Skyline(float x, float y, int width, float ground)
        {
            float u = x / width;
            float roof = ground;
            if (u < 0.18f)
            {
                roof = ground - width * 0.06f;
            }
            else if (u < 0.30f)
            {
                roof = ground - width * 0.035f;
            }
            else if (u > 0.66f && u < 0.78f)
            {
                // Temple spire: a steep triangle.
                float peak = ground - width * 0.12f;
                float d = Math.Abs(u - 0.72f) / 0.06f;
                roof = peak + (ground - width * 0.04f - peak) * d;
            }
            else if (u >= 0.78f)
            {
                roof = ground - width * 0.05f;
            }

            return roof - y;
        }
    }
}
