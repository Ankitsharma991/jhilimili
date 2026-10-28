using System;

namespace UtsavNights.Core.Art
{
    /// <summary>Printed-paper looks for the cracker wrappers, after the bold colours of Indian cracker packaging (no text, no brands).</summary>
    public enum WrapperPattern
    {
        /// <summary>Spiral stripes: rocket tube.</summary>
        SpiralStripes,

        /// <summary>Rows of five-pointed stars around a centre band: skyshot.</summary>
        Stars,

        /// <summary>A sunburst on every face: multi-shot cake box.</summary>
        Sunburst,

        /// <summary>A zigzag band: single bang.</summary>
        Zigzag,

        /// <summary>Bunting triangles between borders: the anar's paper band.</summary>
        Bunting,

        /// <summary>Peacock-feather eyes: the premium fountain.</summary>
        FeatherEyes,

        /// <summary>Small dots in a staggered grid: chakri and chain paper.</summary>
        Dots,
    }

    /// <summary>
    /// Own procedural wrapper textures (Phase 5 visual pass, LICENCE_LOG own-006). Width wraps once around a
    /// cylinder (Unity's cylinder UVs), so patterns repeat seamlessly in u.
    /// </summary>
    public static class WrapperPainter
    {
        public static PaintCanvas Paint(WrapperPattern pattern, Rgb baseColor, Rgb accent, Rgb trim, int width = 256, int height = 128)
        {
            var c = new PaintCanvas(width, height);
            Fill(c, baseColor);
            float w = width, h = height;
            switch (pattern)
            {
                case WrapperPattern.SpiralStripes:
                    for (int i = -4; i < 8; i++)
                    {
                        float x0 = i * w / 4f;
                        c.FillShape((x, y) => Stripe(x - y * 0.9f, x0, w * 0.07f, w), accent);
                    }

                    Borders(c, trim, h * 0.06f);
                    break;

                case WrapperPattern.Stars:
                    c.FillShape((x, y) => Math.Abs(y - h / 2f) - h * 0.1f, trim);
                    for (int row = 0; row < 2; row++)
                    {
                        float cy = row == 0 ? h * 0.2f : h * 0.8f;
                        for (int i = 0; i < 6; i++)
                        {
                            float cx = (i + (row == 0 ? 0.25f : 0.75f)) * w / 6f;
                            c.FillShape((x, y) => Star(x, y, cx, cy, h * 0.13f), accent);
                        }
                    }

                    break;

                case WrapperPattern.Sunburst:
                    float sx = w / 2f, sy = h / 2f;
                    c.FillShape((x, y) => Rays(x, y, sx, sy, 16), accent);
                    c.FillShape((x, y) => Dist(x, y, sx, sy) - h * 0.2f, trim);
                    c.FillShape((x, y) => Star(x, y, sx, sy, h * 0.13f), baseColor);
                    Borders(c, trim, h * 0.05f);
                    break;

                case WrapperPattern.Zigzag:
                    c.FillShape((x, y) => Math.Abs(y - (h / 2f + Tri(x / (w / 8f)) * h * 0.18f)) - h * 0.08f, accent);
                    Borders(c, trim, h * 0.07f);
                    break;

                case WrapperPattern.Bunting:
                    Borders(c, trim, h * 0.12f);
                    for (int i = 0; i < 10; i++)
                    {
                        float cx = (i + 0.5f) * w / 10f;
                        Rgb flag = i % 2 == 0 ? accent : new Rgb(0.16f, 0.55f, 0.3f);
                        c.FillShape((x, y) => Triangle(x, y, cx, h * 0.12f, w / 22f, h * 0.45f), flag);
                    }

                    break;

                case WrapperPattern.FeatherEyes:
                    for (int row = 0; row < 3; row++)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            float cx = (i + (row % 2 == 0 ? 0.3f : 0.8f)) * w / 5f, cy = (row + 0.5f) * h / 3f;
                            c.FillShape((x, y) => Ellipse(x, y, cx, cy, h * 0.14f, h * 0.17f), trim);
                            c.FillShape((x, y) => Ellipse(x, y, cx, cy, h * 0.09f, h * 0.12f), new Rgb(0.1f, 0.5f, 0.35f));
                            c.FillShape((x, y) => Ellipse(x, y, cx, cy + h * 0.02f, h * 0.05f, h * 0.065f), accent);
                        }
                    }

                    break;

                default:
                    for (int row = 0; row < 4; row++)
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            float cx = (i + (row % 2 == 0 ? 0.25f : 0.75f)) * w / 12f, cy = (row + 0.5f) * h / 4f;
                            c.FillShape((x, y) => Dist(x, y, cx, cy) - h * 0.045f, accent);
                        }
                    }

                    Borders(c, trim, h * 0.05f);
                    break;
            }

            c.Clamp();
            return c;
        }

        private static void Fill(PaintCanvas c, Rgb color) => c.Fill(color.R, color.G, color.B, 1f);

        private static void Borders(PaintCanvas c, Rgb color, float thickness) =>
            c.FillShape((x, y) => Math.Min(y, c.Height - y) - thickness, color);

        /// <summary>Distance to a vertical band of half-width at x0, repeating every period (so it wraps in u).</summary>
        private static float Stripe(float x, float x0, float halfWidth, float period)
        {
            float d = (x - x0) % period;
            if (d < 0f)
            {
                d += period;
            }

            return Math.Min(d, period - d) - halfWidth;
        }

        private static float Dist(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return ((float)Math.Sqrt(dx * dx + dy * dy) - 1f) * Math.Min(rx, ry);
        }

        /// <summary>Triangle wave, -1 to 1, period 1.</summary>
        private static float Tri(float t)
        {
            float f = t - (float)Math.Floor(t);
            return 4f * Math.Abs(f - 0.5f) - 1f;
        }

        /// <summary>Downward pennant hanging from (cx, top), half-width hw and length len.</summary>
        private static float Triangle(float x, float y, float cx, float top, float hw, float len)
        {
            if (y < top || y > top + len)
            {
                return Math.Max(top - y, y - top - len);
            }

            float half = hw * (1f - (y - top) / len);
            return Math.Abs(x - cx) - half;
        }

        /// <summary>Sunburst rays: alternate wedges around (cx, cy).</summary>
        private static float Rays(float x, float y, float cx, float cy, int count)
        {
            double a = Math.Atan2(y - cy, x - cx);
            double wedge = Math.PI * 2 / count;
            double f = (a + Math.PI) / wedge;
            double frac = f - Math.Floor(f);
            return (float)((Math.Abs(frac - 0.5) - 0.25) * wedge * Dist(x, y, cx, cy));
        }

        /// <summary>Approximate signed distance to a five-pointed star of outer radius r.</summary>
        private static float Star(float x, float y, float cx, float cy, float r)
        {
            double a = Math.Atan2(x - cx, -(y - cy));
            double sector = Math.PI * 2 / 5;
            double local = Math.Abs(((a % sector) + sector) % sector - sector / 2) / (sector / 2);
            double radius = r * (0.42 + 0.58 * local);
            return (float)(Dist(x, y, cx, cy) - radius);
        }
    }
}
