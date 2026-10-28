using System;
using NUnit.Framework;
using UtsavNights.Core.Art;

namespace UtsavNights.Tests
{
    /// <summary>Phase 5 visual pass: the sky, floor, plaster and wrapper textures are well formed.</summary>
    public class EnvironmentPainterTests
    {
        private static float Luma(PaintCanvas c, int x, int y)
        {
            Rgb p = c.Get(x, y, out _);
            return 0.3f * p.R + 0.59f * p.G + 0.11f * p.B;
        }

        [Test]
        public void NightSky_IsDarkOverheadAndWarmerAtTheHorizon()
        {
            PaintCanvas sky = EnvironmentPainter.NightSky(512, 256);
            Rgb zenith = sky.Get(3, 2, out _);
            Rgb horizon = sky.Get(3, 126, out _);
            Assert.Less(Luma(sky, 3, 2), Luma(sky, 3, 126));
            Assert.Greater(horizon.R - horizon.B, zenith.R - zenith.B, "city-light haze at the horizon");
        }

        [Test]
        public void NightSky_HasTheMoonWhereTheCameraLooks()
        {
            PaintCanvas sky = EnvironmentPainter.NightSky(512, 256);
            int x = (int)(EnvironmentPainter.MoonU * 512);
            int y = (int)((1f - EnvironmentPainter.MoonElevationDegrees / 90f) * 128);
            Assert.Greater(Luma(sky, x, y), 0.8f);
        }

        [Test]
        public void FloorAndPlaster_TileWithoutSeams()
        {
            foreach (PaintCanvas t in new[] { EnvironmentPainter.StoneTiles(128), EnvironmentPainter.Plaster(128) })
            {
                float seam = 0f, inside = 0f;
                for (int y = 0; y < t.Height; y++)
                {
                    seam += Math.Abs(Luma(t, 0, y) - Luma(t, t.Width - 1, y));
                    inside += Math.Abs(Luma(t, 60, y) - Luma(t, 61, y));
                }

                Assert.Less(seam, inside * 3f + 1f, "the left and right edges match about as well as neighbouring pixels");
            }
        }

        [TestCase(WrapperPattern.SpiralStripes)]
        [TestCase(WrapperPattern.Stars)]
        [TestCase(WrapperPattern.Sunburst)]
        [TestCase(WrapperPattern.Zigzag)]
        [TestCase(WrapperPattern.Bunting)]
        [TestCase(WrapperPattern.FeatherEyes)]
        [TestCase(WrapperPattern.Dots)]
        public void Wrappers_ShowTheAccentOnTheBase(WrapperPattern pattern)
        {
            var red = new Rgb(0.8f, 0.1f, 0.1f);
            var gold = new Rgb(0.95f, 0.75f, 0.2f);
            PaintCanvas w = WrapperPainter.Paint(pattern, red, gold, gold, 128, 64);
            int accent = 0;
            for (int y = 0; y < w.Height; y++)
            {
                for (int x = 0; x < w.Width; x++)
                {
                    if (w.Get(x, y, out _).G > 0.5f)
                    {
                        accent++;
                    }
                }
            }

            float share = accent / (float)(w.Width * w.Height);
            Assert.That(share, Is.InRange(0.03f, 0.8f), "a visible pattern, not a blank or flooded wrapper");
        }
    }
}
