using System;
using NUnit.Framework;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Tests
{
    /// <summary>Phase 11 step 3 (F26): the photo suggestion from synthetic faces: skin tone, hair colour and face shape.</summary>
    public class PhotoAnalysisTests
    {
        private const int W = 320, H = 400;
        private const float E = 40f, MidX = 160f, MidY = 170f;

        /// <summary>A face drawn row by row: <paramref name="halfWidth"/> gives its half width at a height above (-) or below (+) the eyes.</summary>
        private static byte[] Face(Rgb skin, Rgb hair, Func<float, float> halfWidth, float top, float bottom, bool hairOn = true)
        {
            var rgb = new byte[W * H * 3];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dy = (y - MidY) / E;
                    float hw = dy >= top && dy <= bottom ? halfWidth(dy) * E : -1f;
                    Rgb c = new Rgb(0.2f, 0.35f, 0.75f); // a blue wall behind
                    if (Math.Abs(x - MidX) <= hw)
                    {
                        c = skin;
                    }

                    if (hairOn && dy >= top - 0.8f && dy < top + 0.05f && Math.Abs(x - MidX) <= 1.2f * E)
                    {
                        c = hair;
                    }

                    foreach (float ex in new[] { MidX - E / 2f, MidX + E / 2f })
                    {
                        if ((x - ex) * (x - ex) + (y - MidY) * (y - MidY) < 0.12f * E * 0.12f * E)
                        {
                            c = new Rgb(0.08f, 0.06f, 0.05f);
                        }
                    }

                    int i = (y * W + x) * 3;
                    rgb[i] = (byte)(c.R * 255f);
                    rgb[i + 1] = (byte)(c.G * 255f);
                    rgb[i + 2] = (byte)(c.B * 255f);
                }
            }

            return rgb;
        }

        private static Func<float, float> Ellipse(float a, float b, float centre) => dy => a * (float)Math.Sqrt(Math.Max(0, 1 - (dy - centre) * (dy - centre) / (b * b)));

        private static PhotoSuggestion Read(byte[] rgb) => PhotoAnalysis.Analyse(rgb, W, H, new FaceBox(MidX, MidY, E)) ?? throw new AssertionException("no suggestion");

        [TestCase(0.15f)]
        [TestCase(0.5f)]
        [TestCase(0.85f)]
        public void SkinTone_IsRecovered(float tone)
        {
            Rgb skin = SkinTones.Evaluate(tone);
            PhotoSuggestion s = Read(Face(skin, HairColours.All[1], Ellipse(1.1f, 1.5f, 0.4f), -1.1f, 1.9f));
            Assert.AreEqual(tone, s.SkinTone, 0.06f);
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(5)]
        public void HairColour_IsRecovered(int hair)
        {
            PhotoSuggestion s = Read(Face(SkinTones.Evaluate(0.5f), HairColours.All[hair], Ellipse(1.1f, 1.5f, 0.4f), -1.1f, 1.9f));
            Assert.AreEqual(hair, s.HairColour);
        }

        [Test]
        public void NoHairSeen_KeepsTheAvatarsOwn()
        {
            PhotoSuggestion s = Read(Face(SkinTones.Evaluate(0.5f), default, Ellipse(1.1f, 1.5f, 0.4f), -1.1f, 1.9f, hairOn: false));
            Assert.AreEqual(-1, s.HairColour, "a blue wall behind isn't taken for hair");
            var profile = new AvatarProfile { hairColour = 4 };
            s.ApplyTo(profile);
            Assert.AreEqual(4, profile.hairColour);
        }

        [Test]
        public void FaceShapes_AreTold()
        {
            Rgb skin = SkinTones.Evaluate(0.45f);
            Rgb hair = HairColours.All[0];
            Assert.AreEqual((int)PhotoAnalysis.FaceShapeId.Round, Read(Face(skin, hair, Ellipse(1.25f, 1.35f, 0.4f), -0.95f, 1.75f)).FaceShape, "round");
            Assert.AreEqual((int)PhotoAnalysis.FaceShapeId.Oval, Read(Face(skin, hair, Ellipse(1.1f, 1.55f, 0.4f), -1.15f, 1.95f)).FaceShape, "oval");
            Assert.AreEqual((int)PhotoAnalysis.FaceShapeId.Long, Read(Face(skin, hair, Ellipse(0.95f, 1.65f, 0.45f), -1.2f, 2.1f)).FaceShape, "long");
            Assert.AreEqual((int)PhotoAnalysis.FaceShapeId.Square, Read(Face(skin, hair, _ => 1.1f, -1f, 1.8f)).FaceShape, "square");
            Assert.AreEqual((int)PhotoAnalysis.FaceShapeId.Heart, Read(Face(skin, hair, dy => 1.25f - Math.Max(0f, dy) * 0.32f, -1f, 2f)).FaceShape, "heart");
        }

        [Test]
        public void TooSmallOrAtTheEdge_GivesNothing()
        {
            byte[] rgb = Face(SkinTones.Evaluate(0.5f), HairColours.All[1], Ellipse(1.1f, 1.5f, 0.4f), -1.1f, 1.9f);
            Assert.IsNull(PhotoAnalysis.Analyse(rgb, W, H, new FaceBox(MidX, MidY, 8f)), "too small");
            Assert.IsNull(PhotoAnalysis.Analyse(rgb, W, H, new FaceBox(4f, 4f, E)), "off the picture");
            Assert.IsNull(PhotoAnalysis.Analyse(new byte[10], W, H, new FaceBox(MidX, MidY, E)), "no pixels");
            Assert.AreEqual(18, PhotoAnalysis.MinimumAge);
        }
    }
}
