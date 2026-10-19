using System;
using NUnit.Framework;
using UtsavNights.Core;

namespace UtsavNights.Tests
{
    public class ProceduralSoundsTests
    {
        private const int Rate = 48000;

        [Test]
        public void ThereAreEightDistinctSounds()
        {
            Assert.AreEqual(8, ProceduralSounds.AllKinds.Length);
            CollectionAssert.AllItemsAreUnique(ProceduralSounds.AllKinds);
        }

        [Test]
        public void EverySound_HasExpectedLength_AndPeakWithinLimit([ValueSource(nameof(Kinds))] ProceduralSounds.Kind kind)
        {
            float[] data = ProceduralSounds.Generate(kind, Rate, 7);
            Assert.AreEqual(ProceduralSounds.SampleCount(kind, Rate), data.Length);

            float peak = 0f;
            foreach (float s in data)
            {
                Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s));
                peak = Math.Max(peak, Math.Abs(s));
            }

            Assert.LessOrEqual(peak, ProceduralSounds.PeakLevel + 1e-4f);
            Assert.Greater(peak, 0.5f, "sound should not be near-silent");
        }

        [Test]
        public void SameSeed_IsDeterministic()
        {
            float[] a = ProceduralSounds.Generate(ProceduralSounds.Kind.Crackle, Rate, 42);
            float[] b = ProceduralSounds.Generate(ProceduralSounds.Kind.Crackle, Rate, 42);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void InvalidSampleRate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralSounds.Generate(ProceduralSounds.Kind.Pop, 0, 1));
        }

        private static ProceduralSounds.Kind[] Kinds() => ProceduralSounds.AllKinds;
    }
}
