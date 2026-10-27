using System;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Diagnostics;

namespace UtsavNights.Tests
{
    public class BlindSoundTestTests
    {
        private static readonly string[] Ids =
        {
            "flowerpot_gold", "sparkler_classic", "chakri_classic", "chain_lar",
            "bomb_single", "rocket_basic", "skyshot_green", "cake_twelve",
        };

        [Test]
        public void PlaysEveryCrackerOnce_InASeededOrder()
        {
            var a = new BlindSoundTest(Ids, 42);
            var b = new BlindSoundTest(Ids, 42);
            string[] order = Enumerable.Range(0, a.Count).Select(a.PlayedAt).ToArray();
            CollectionAssert.AreEquivalent(Ids, order);
            CollectionAssert.AreEqual(order, Enumerable.Range(0, b.Count).Select(b.PlayedAt).ToArray(), "same seed, same order");
            bool anyDiffers = Enumerable.Range(1, 20).Any(seed =>
                !Enumerable.Range(0, Ids.Length).Select(new BlindSoundTest(Ids, seed).PlayedAt).SequenceEqual(order));
            Assert.IsTrue(anyDiffers, "other seeds shuffle differently");
        }

        [Test]
        public void SixOfEightPasses_FiveFails()
        {
            Assert.IsTrue(Run(6).Passed);
            Assert.IsFalse(Run(5).Passed);
            Assert.AreEqual(6, Run(0).RequiredCorrect);
        }

        [Test]
        public void NotSureAndReplaysAreRecorded()
        {
            var t = new BlindSoundTest(Ids, 7);
            t.RecordReplay();
            t.RecordReplay();
            BlindSoundTest.Answer first = t.Guess(null);
            Assert.AreEqual(BlindSoundTest.NotSure, first.GuessId);
            Assert.IsFalse(first.Correct);
            Assert.AreEqual(2, first.Replays);
            Assert.AreEqual(0, t.Guess(t.Current).Replays, "replays reset per sound");
            Assert.AreEqual(1, t.CorrectCount);
        }

        [Test]
        public void GuessAfterCompleteThrows()
        {
            BlindSoundTest t = Run(8);
            Assert.IsTrue(t.IsComplete);
            Assert.IsNull(t.Current);
            Assert.Throws<InvalidOperationException>(() => t.Guess("bomb_single"));
            Assert.IsFalse(Run(0, complete: false).Passed, "an unfinished test never passes");
        }

        private static BlindSoundTest Run(int correct, bool complete = true)
        {
            var t = new BlindSoundTest(Ids, 3);
            int answers = complete ? Ids.Length : Ids.Length - 1;
            for (int i = 0; i < answers; i++)
            {
                t.Guess(i < correct ? t.Current : BlindSoundTest.NotSure);
            }

            return t;
        }
    }
}
