using System;
using NUnit.Framework;
using UtsavNights.Core.Art;

namespace UtsavNights.Tests
{
    /// <summary>Phase 5 step 5: the app icon and feature graphic meet Play's and Android's format rules.</summary>
    public class StoreArtPainterTests
    {
        [Test]
        public void FeatureGraphic_IsPlaySizeAndOpaque()
        {
            PaintCanvas art = StoreArtPainter.FeatureGraphic();
            Assert.AreEqual(1024, art.Width);
            Assert.AreEqual(500, art.Height);
            for (int i = 3; i < art.Pixels.Length; i += 4)
            {
                Assert.AreEqual(1f, art.Pixels[i], "Play rejects transparent feature graphics");
            }
        }

        [Test]
        public void Icon_IsOpaqueEverywhere()
        {
            PaintCanvas icon = StoreArtPainter.Icon(128);
            for (int i = 3; i < icon.Pixels.Length; i += 4)
            {
                Assert.AreEqual(1f, icon.Pixels[i]);
            }
        }

        [Test]
        public void AdaptiveForeground_StaysInsideTheSafeZoneCircle()
        {
            const int size = 256;
            PaintCanvas fg = StoreArtPainter.IconForeground(size);
            float radius = size * StoreArtPainter.AdaptiveSafeZone * 0.5f;
            float outside = 0f, inside = 0f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    fg.Get(x, y, out float alpha);
                    float d = (float)Math.Sqrt((x + 0.5f - size / 2f) * (x + 0.5f - size / 2f) + (y + 0.5f - size / 2f) * (y + 0.5f - size / 2f));
                    if (d > radius)
                    {
                        outside += alpha;
                    }
                    else
                    {
                        inside += alpha;
                    }
                }
            }

            Assert.Greater(inside, 0f);
            Assert.Less(outside / (inside + outside), 0.05f, "less than 5 percent of the drawing may fall where launchers crop");
        }

        [Test]
        public void Painting_IsRepeatable()
        {
            CollectionAssert.AreEqual(StoreArtPainter.Icon(64).Pixels, StoreArtPainter.Icon(64).Pixels);
        }

        [Test]
        public void TheNightSkyHasLightInIt()
        {
            PaintCanvas icon = StoreArtPainter.Icon(128);
            Rgb corner = icon.Get(2, 2, out _);
            Rgb spray = icon.Get(64, 64, out _);
            Assert.Less(corner.R + corner.G + corner.B, 0.6f, "dark night in the corner");
            Assert.Greater(spray.R + spray.G, corner.R + corner.G + 0.5f, "bright sparks above the pot");
        }
    }
}
