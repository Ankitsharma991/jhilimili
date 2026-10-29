using System;
using NUnit.Framework;
using UtsavNights.Core.Clips;

namespace UtsavNights.Tests
{
    /// <summary>Phase 9 step 6 (F14): clip sizes, lengths, names, retention, PCM and the I420 colour conversion.</summary>
    public class ClipRulesTests
    {
        [TestCase(2000, 1200, 1280, 768)]
        [TestCase(1200, 2000, 768, 1280)]
        [TestCase(1080, 2400, 576, 1280)]
        [TestCase(800, 480, 800, 480)]
        [TestCase(1281, 721, 1280, 720)]
        public void Format_KeepsTheShape_FitsTheLongSide_InMultiplesOf16(int sw, int sh, int w, int h)
        {
            ClipFormat f = ClipRules.FormatFor(sw, sh);
            Assert.AreEqual(w, f.Width);
            Assert.AreEqual(h, f.Height);
            Assert.AreEqual(0, f.Width % 16);
            Assert.AreEqual(0, f.Height % 16);
            Assert.AreEqual(f.Width * f.Height * 3 / 2, f.FrameBytes);
            Assert.That(f.Bitrate, Is.InRange(2_000_000, 12_000_000));
        }

        [Test]
        public void Lengths_StayBetween10And20Seconds()
        {
            Assert.AreEqual(10f, ClipRules.ShowClipSeconds(3f));
            Assert.AreEqual(15f, ClipRules.ShowClipSeconds(14f));
            Assert.AreEqual(20f, ClipRules.ShowClipSeconds(60f));
            Assert.IsFalse(ClipRules.CanStop(9.9f));
            Assert.IsTrue(ClipRules.CanStop(10f));
        }

        [Test]
        public void Names_SortByTime_AndOnlyTheNewestFiveStay()
        {
            Assert.AreEqual("utsav_20261029_213142.mp4", ClipRules.FileName(new DateTime(2026, 10, 29, 21, 31, 42)));
            var names = new[]
            {
                "utsav_20261029_100000.mp4", "utsav_20261029_110000.mp4", "utsav_20261029_120000.mp4", "utsav_20261029_130000.mp4",
                "utsav_20261029_140000.mp4", "utsav_20261029_150000.mp4", "utsav_20261029_160000.mp4", "notes.txt",
            };
            CollectionAssert.AreEquivalent(new[] { "utsav_20261029_100000.mp4", "utsav_20261029_110000.mp4" }, ClipRules.ToDelete(names));
            CollectionAssert.IsEmpty(ClipRules.ToDelete(new[] { "utsav_20261029_100000.mp4" }));
        }

        [Test]
        public void Pcm16_IsLittleEndian_AndClipped()
        {
            var bytes = new byte[8];
            int n = ClipRules.ToPcm16(new[] { 0f, 1f, -1f, 2f }, 4, bytes);
            Assert.AreEqual(8, n);
            Assert.AreEqual(0, BitConverter.ToInt16(bytes, 0));
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 2));
            Assert.AreEqual(-32767, BitConverter.ToInt16(bytes, 4));
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 6), "out of range is clipped, never wrapped");
            Assert.AreEqual(4, ClipRules.ToPcm16(new[] { 0.5f, 0.5f, 0.5f }, 3, new byte[4]), "never past the buffer");
            Assert.AreEqual(1_000_000L, ClipRules.AudioMicros(96000, 48000, 2));
        }

        [Test]
        public void I420_RoundTripsColours()
        {
            const int w = 4, h = 2;
            var rgb = new byte[w * h * 3];
            byte[][] colours = { new byte[] { 255, 40, 20 }, new byte[] { 20, 200, 60 } };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte[] c = colours[x / 2];
                    Array.Copy(c, 0, rgb, (y * w + x) * 3, 3);
                }
            }

            byte[] yuv = I420.FromRgb(rgb, w, h);
            Assert.AreEqual(w * h * 3 / 2, yuv.Length);
            byte[] back = I420.ToRgb(yuv, w, h);
            for (int i = 0; i < rgb.Length; i++)
            {
                Assert.AreEqual(rgb[i], back[i], 6, $"byte {i}");
            }

            Assert.AreEqual(16, I420.Y(0, 0, 0));
            Assert.AreEqual(235, I420.Y(255, 255, 255));
            Assert.AreEqual(128, I420.U(128, 128, 128));
        }

        [Test]
        public void I420_SourceMap_MatchesThePlaneLayout()
        {
            const int w = 8, h = 4;
            Assert.AreEqual((0, 0.5f, 0.5f), I420.Source(0, w, h));
            Assert.AreEqual((0, 7.5f, 3.5f), I420.Source(w * h - 1, w, h));
            Assert.AreEqual((1, 1f, 1f), I420.Source(w * h, w, h), "U starts at the first 2 x 2 block");
            Assert.AreEqual((1, 7f, 3f), I420.Source(w * h + 7, w, h), "the last U sample");
            Assert.AreEqual((2, 1f, 1f), I420.Source(w * h + 8, w, h), "V follows U");
        }
    }
}
