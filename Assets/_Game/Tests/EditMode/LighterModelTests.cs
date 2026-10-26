using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Ignition;

namespace UtsavNights.Tests
{
    public class LighterModelTests
    {
        private const float Frame = 1f / 60f;

        private sealed class Rec
        {
            public int Lit;
            public int Out;
            public readonly List<int> Ignited = new List<int>();

            public Rec(LighterModel m)
            {
                m.FlameLit += () => Lit++;
                m.FlameWentOut += () => Out++;
                m.Ignited += id => Ignited.Add(id);
            }
        }

        private static void Run(LighterModel m, bool touching, int target, float seconds)
        {
            for (float t = 0f; t < seconds - 1e-6f; t += Frame)
            {
                m.Update(touching, target, Frame);
            }
        }

        [Test]
        public void TouchLightsFlame_ReleaseHidesIt()
        {
            var m = new LighterModel();
            var rec = new Rec(m);
            Assert.AreEqual(FlameState.Hidden, m.State);
            m.Update(true, -1, Frame);
            Assert.AreEqual(FlameState.Burning, m.State);
            Assert.AreEqual(1, rec.Lit);
            m.Update(false, -1, Frame);
            Assert.AreEqual(FlameState.Hidden, m.State);
        }

        [Test]
        public void HoldingNearFuse_IgnitesOnceAfterHalfSecond()
        {
            var m = new LighterModel();
            var rec = new Rec(m);
            Run(m, true, 7, 0.45f);
            Assert.IsEmpty(rec.Ignited);
            Assert.Greater(m.HoldProgress, 0.8f);
            Run(m, true, 7, 0.1f);
            CollectionAssert.AreEqual(new[] { 7 }, rec.Ignited);
            Run(m, true, 7, 1f);
            Assert.AreEqual(1, rec.Ignited.Count, "a lit fuse is not lit again during the same hold");
        }

        [Test]
        public void SwitchingFuse_RestartsHold()
        {
            var m = new LighterModel();
            var rec = new Rec(m);
            Run(m, true, 1, 0.4f);
            Run(m, true, 2, 0.4f);
            Assert.IsEmpty(rec.Ignited);
            Assert.AreEqual(2, m.TargetId);
            Run(m, true, 2, 0.15f);
            CollectionAssert.AreEqual(new[] { 2 }, rec.Ignited);
        }

        [Test]
        public void LeavingFuse_ResetsHold()
        {
            var m = new LighterModel();
            var rec = new Rec(m);
            Run(m, true, 1, 0.4f);
            m.Update(true, -1, Frame);
            Assert.AreEqual(0f, m.HoldProgress);
            Assert.AreEqual(-1, m.TargetId);
            Run(m, true, 1, 0.4f);
            Assert.IsEmpty(rec.Ignited);
        }

        [Test]
        public void ThreeSecondsAway_FlameGoesOut_AndCannotIgnite()
        {
            var m = new LighterModel();
            var rec = new Rec(m);
            Run(m, true, -1, 2.9f);
            Assert.AreEqual(FlameState.Burning, m.State);
            Run(m, true, -1, 0.2f);
            Assert.AreEqual(FlameState.Out, m.State);
            Assert.AreEqual(1, rec.Out);
            Run(m, true, 5, 1f);
            Assert.IsEmpty(rec.Ignited, "an out flame lights nothing");
            Assert.AreEqual(1, rec.Out, "goes out once");
        }

        [Test]
        public void LiftAndTouchAgain_Relights()
        {
            var m = new LighterModel();
            var rec = new Rec(m);
            Run(m, true, -1, 3.1f);
            Assert.AreEqual(FlameState.Out, m.State);
            m.Update(false, -1, Frame);
            m.Update(true, -1, Frame);
            Assert.AreEqual(FlameState.Burning, m.State);
            Assert.AreEqual(2, rec.Lit);
            Assert.AreEqual(Frame, m.AwaySeconds, 1e-5f, "away timer restarts");
        }

        [Test]
        public void NearAFuse_AwayTimerResets()
        {
            var m = new LighterModel();
            Run(m, true, -1, 2.5f);
            m.Update(true, 3, Frame);
            Assert.AreEqual(0f, m.AwaySeconds);
            Run(m, true, -1, 2.5f);
            Assert.AreEqual(FlameState.Burning, m.State);
        }

        [Test]
        public void InvalidFlameOut_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new LighterModel(0.5f, 0f));
        }
    }
}
