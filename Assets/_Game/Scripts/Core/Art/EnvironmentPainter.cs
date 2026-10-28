using System;

namespace UtsavNights.Core.Art
{
    /// <summary>
    /// Own procedural textures for the courtyard (Phase 5 visual pass): the night sky panorama, the stone floor
    /// tiles and the lime plaster. Seeded, so every build paints the same pixels.
    /// </summary>
    public static class EnvironmentPainter
    {
        // Not darker than this: the neutral tonemapper's toe stretches the darkest 8-bit steps into visible rings.
        public static readonly Rgb Zenith = new Rgb(0.03f, 0.03f, 0.09f);
        public static readonly Rgb Horizon = new Rgb(0.13f, 0.075f, 0.22f);
        public static readonly Rgb CityGlow = new Rgb(0.55f, 0.28f, 0.16f);
        public static readonly Rgb Below = new Rgb(0.02f, 0.015f, 0.035f);
        public static readonly Rgb Moon = new Rgb(0.95f, 0.93f, 0.85f);

        /// <summary>Where the moon sits in the panorama: u = 0.25 faces the game camera (+z); 11 degrees left and 17 up, just above the skyline.</summary>
        public const float MoonU = 0.22f;
        public const float MoonElevationDegrees = 17f;

        /// <summary>
        /// Equirectangular night sky for Unity's Skybox/Panoramic: top row is straight up, the middle row the
        /// horizon. Deep indigo overhead, a warm city-light haze just above the horizon, stars thinning towards
        /// the haze, and a soft moon.
        /// </summary>
        public static PaintCanvas NightSky(int width = 2048, int height = 1024)
        {
            var canvas = new PaintCanvas(width, height);
            int horizon = height / 2;
            for (int y = 0; y < height; y++)
            {
                Rgb c;
                if (y < horizon)
                {
                    float elevation = 1f - (float)y / horizon; // 1 at the zenith, 0 at the horizon
                    c = Rgb.Lerp(Horizon, Zenith, (float)Math.Pow(elevation, 0.55));
                    float haze = (float)Math.Exp(-elevation * 9f);
                    c = new Rgb(c.R + CityGlow.R * haze * 0.45f, c.G + CityGlow.G * haze * 0.45f, c.B + CityGlow.B * haze * 0.45f);
                }
                else
                {
                    c = Below;
                }

                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    canvas.Pixels[i] = c.R;
                    canvas.Pixels[i + 1] = c.G;
                    canvas.Pixels[i + 2] = c.B;
                    canvas.Pixels[i + 3] = 1f;
                }
            }

            var random = new Random(8112026);
            for (int i = 0; i < 900; i++)
            {
                // More stars overhead, few in the haze.
                float elevation = (float)Math.Sqrt(random.NextDouble());
                float y = (1f - elevation) * horizon * 0.97f;
                float x = (float)random.NextDouble() * width;
                float bright = 0.25f + 0.75f * (float)Math.Pow(random.NextDouble(), 3) ;
                double tint = random.NextDouble();
                Rgb star = tint < 0.15 ? new Rgb(0.75f, 0.82f, 1f) : tint < 0.3 ? new Rgb(1f, 0.88f, 0.7f) : new Rgb(1f, 1f, 1f);
                // An equirectangular panorama squeezes rows near the zenith; widen stars there so they look round.
                double degrees = 90.0 * (1.0 - y / horizon);
                float stretch = (float)Math.Min(8.0, 1.0 / Math.Max(0.125, Math.Cos(degrees * Math.PI / 180.0)));
                canvas.GlowStretched(x, y, 0.55f + bright * 0.6f, stretch, star, bright * elevation * 1.4f);
            }

            // About one 8-bit level of noise breaks the gradient's steps into fine grain instead of bands.
            var dither = new Random(4242);
            for (int i = 0; i < horizon * width * 4; i += 4)
            {
                float n = ((float)dither.NextDouble() - 0.5f) / 255f;
                canvas.Pixels[i] += n;
                canvas.Pixels[i + 1] += n;
                canvas.Pixels[i + 2] += n;
            }

            float moonX = MoonU * width;
            float moonY = (1f - MoonElevationDegrees / 90f) * horizon;
            canvas.Glow(moonX, moonY, height * 0.045f, new Rgb(0.35f, 0.38f, 0.55f), 0.35f);
            canvas.FillShape((px, py) => Distance(px, py, moonX, moonY) - height * 0.0075f, Moon);
            canvas.Clamp();
            return canvas;
        }

        /// <summary>Square courtyard tiles, 4 x 4 per texture, worn sandstone with dark grout. Tiles seamlessly.</summary>
        public static PaintCanvas StoneTiles(int size = 512)
        {
            var canvas = new PaintCanvas(size, size);
            var random = new Random(31);
            const int tiles = 4;
            float tile = size / (float)tiles;
            var shade = new float[tiles * tiles];
            for (int i = 0; i < shade.Length; i++)
            {
                shade[i] = 0.82f + 0.3f * (float)random.NextDouble();
            }

            var noise = new ValueNoise(17, 32);
            Rgb sand = new Rgb(0.42f, 0.33f, 0.27f);
            Rgb grout = new Rgb(0.10f, 0.08f, 0.07f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int tx = (int)(x / tile), ty = (int)(y / tile);
                    float gx = Math.Min(x - tx * tile, (tx + 1) * tile - 1 - x);
                    float gy = Math.Min(y - ty * tile, (ty + 1) * tile - 1 - y);
                    float edge = Math.Min(gx, gy);
                    float n = noise.Sample(x / (float)size, y / (float)size) * 0.6f + noise.Sample(x * 4f / size, y * 4f / size) * 0.4f;
                    float k = shade[ty * tiles + tx] * (0.78f + 0.4f * n);
                    Rgb c = new Rgb(sand.R * k, sand.G * k, sand.B * k);
                    if (edge < 2.5f)
                    {
                        c = Rgb.Lerp(grout, c, Math.Max(0f, edge - 1f) / 1.5f);
                    }

                    Set(canvas, x, y, c);
                }
            }

            return canvas;
        }

        /// <summary>Lime plaster in the warm wall colour: soft mottling and faint vertical rain streaks. Tiles seamlessly.</summary>
        public static PaintCanvas Plaster(int size = 512)
        {
            var canvas = new PaintCanvas(size, size);
            var coarse = new ValueNoise(5, 8);
            var fine = new ValueNoise(9, 64);
            var streaks = new ValueNoise(13, 48);
            Rgb lime = new Rgb(0.66f, 0.47f, 0.34f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float k = 0.86f + 0.16f * coarse.Sample(u, v) + 0.07f * fine.Sample(u, v)
                              - 0.06f * Math.Max(0f, streaks.Sample(u, v * 0.06f) - 0.5f) * 2f;
                    Set(canvas, x, y, new Rgb(lime.R * k, lime.G * k, lime.B * k));
                }
            }

            return canvas;
        }

        private static void Set(PaintCanvas canvas, int x, int y, Rgb c)
        {
            int i = (y * canvas.Width + x) * 4;
            canvas.Pixels[i] = Math.Min(1f, c.R);
            canvas.Pixels[i + 1] = Math.Min(1f, c.G);
            canvas.Pixels[i + 2] = Math.Min(1f, c.B);
            canvas.Pixels[i + 3] = 1f;
        }

        private static float Distance(float x0, float y0, float x1, float y1) =>
            (float)Math.Sqrt((x0 - x1) * (x0 - x1) + (y0 - y1) * (y0 - y1));
    }

    /// <summary>Smooth value noise on a periodic grid (wraps at 1), so textures tile without seams.</summary>
    public sealed class ValueNoise
    {
        private readonly float[] _grid;
        private readonly int _cells;

        public ValueNoise(int seed, int cells)
        {
            _cells = cells;
            _grid = new float[cells * cells];
            var random = new Random(seed);
            for (int i = 0; i < _grid.Length; i++)
            {
                _grid[i] = (float)random.NextDouble();
            }
        }

        /// <summary>0 to 1 at (u, v); u and v wrap every 1.0.</summary>
        public float Sample(float u, float v)
        {
            float x = Wrap(u) * _cells, y = Wrap(v) * _cells;
            int x0 = (int)x % _cells, y0 = (int)y % _cells;
            int x1 = (x0 + 1) % _cells, y1 = (y0 + 1) % _cells;
            float fx = Smooth(x - (int)x), fy = Smooth(y - (int)y);
            float a = _grid[y0 * _cells + x0] + (_grid[y0 * _cells + x1] - _grid[y0 * _cells + x0]) * fx;
            float b = _grid[y1 * _cells + x0] + (_grid[y1 * _cells + x1] - _grid[y1 * _cells + x0]) * fx;
            return a + (b - a) * fy;
        }

        private static float Wrap(float t) => t - (float)Math.Floor(t);

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
