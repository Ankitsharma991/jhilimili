using System;
using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Crackers;

namespace UtsavNights.Tests
{
    public class CrackerStateMachineTests
    {
        // Flowerpot shape from PRD 7.1: fuse spark at 0, fountain (payoff) at 2.5, stop at 10, 2 s cooldown.
        private static CrackerTimeline Flowerpot(float cooldown = 2f)
        {
            return new CrackerTimeline(2.5f, cooldown, new[]
            {
                new TimelineCue(0f, CueKind.Normal, 0),
                new TimelineCue(2.5f, CueKind.Payoff, 1),
                new TimelineCue(10f, CueKind.Stop, 2),
            });
        }

        private sealed class Recorder
        {
            public readonly List<string> Log = new List<string>();
            public int Finished;

            public Recorder(CrackerStateMachine sm)
            {
                sm.StateChanged += (a, b) => Log.Add(a + ">" + b);
                sm.CueReached += i => Log.Add("cue" + i);
                sm.Finished += () => Finished++;
            }
        }

        [Test]
        public void StartsIdle_AndOnlyPlacedCrackersCanBeLit()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            Assert.AreEqual(CrackerState.Idle, sm.State);
            Assert.IsFalse(sm.Ignite(), "cannot light an unplaced cracker");
            Assert.IsTrue(sm.Place());
            Assert.IsFalse(sm.Place(), "already placed");
            Assert.IsTrue(sm.Ignite());
            Assert.IsFalse(sm.Ignite(), "already lit");
        }

        [Test]
        public void Ignite_FiresTimeZeroCueImmediately()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            var rec = new Recorder(sm);
            sm.Place();
            sm.Ignite();
            CollectionAssert.AreEqual(new[] { "Idle>Placed", "Placed>Lit", "cue0" }, rec.Log);
        }

        [Test]
        public void FullRun_VisitsEveryStateInOrder()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            var rec = new Recorder(sm);
            sm.Place();
            sm.Ignite();
            for (int i = 0; i < 1000 && rec.Finished == 0; i++)
            {
                sm.Tick(1f / 60f);
            }

            CollectionAssert.AreEqual(
                new[] { "Idle>Placed", "Placed>Lit", "cue0", "Lit>Burning", "cue1", "Burning>Payoff", "cue2", "Payoff>Cooldown", "Cooldown>Idle" },
                rec.Log);
            Assert.AreEqual(1, rec.Finished);
            Assert.AreEqual(CrackerState.Idle, sm.State);
        }

        [Test]
        public void OneHugeTick_ProcessesEverythingInOrder()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            var rec = new Recorder(sm);
            sm.Place();
            sm.Ignite();
            sm.Tick(100f);
            Assert.AreEqual(1, rec.Finished);
            CollectionAssert.AreEqual(
                new[] { "Idle>Placed", "Placed>Lit", "cue0", "Lit>Burning", "cue1", "Burning>Payoff", "cue2", "Payoff>Cooldown", "Cooldown>Idle" },
                rec.Log);
        }

        [Test]
        public void StateAtSpecificTimes()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            sm.Place();
            sm.Ignite();
            sm.Tick(1f);
            Assert.AreEqual(CrackerState.Lit, sm.State);
            Assert.AreEqual(0.4f, sm.FuseProgress, 1e-4f);
            sm.Tick(2f);
            Assert.AreEqual(CrackerState.Payoff, sm.State);
            Assert.AreEqual(3f, sm.Elapsed, 1e-4f);
            sm.Tick(7.5f);
            Assert.AreEqual(CrackerState.Cooldown, sm.State, "stop reached at 10 s, 0.5 s into cooldown");
            sm.Tick(1.4f);
            Assert.AreEqual(CrackerState.Cooldown, sm.State);
            sm.Tick(0.2f);
            Assert.AreEqual(CrackerState.Idle, sm.State);
        }

        [Test]
        public void BurningWithoutPayoff_WhenEffectStartsAfterFuse()
        {
            var timeline = new CrackerTimeline(1f, 0f, new[]
            {
                new TimelineCue(0f, CueKind.Normal, 0),
                new TimelineCue(1f, CueKind.Normal, 1),
                new TimelineCue(4f, CueKind.Stop, 2),
            });
            var sm = new CrackerStateMachine(timeline);
            sm.Place();
            sm.Ignite();
            sm.Tick(2f);
            Assert.AreEqual(CrackerState.Burning, sm.State);
        }

        [Test]
        public void ZeroCooldown_FinishesOnStop()
        {
            var sm = new CrackerStateMachine(Flowerpot(cooldown: 0f));
            var rec = new Recorder(sm);
            sm.Place();
            sm.Ignite();
            sm.Tick(10f);
            Assert.AreEqual(1, rec.Finished);
            Assert.AreEqual(CrackerState.Idle, sm.State);
        }

        [Test]
        public void Pause_FreezesTime()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            sm.Place();
            sm.Ignite();
            sm.Tick(1f);
            sm.Pause();
            sm.Tick(50f);
            Assert.AreEqual(CrackerState.Lit, sm.State);
            Assert.AreEqual(1f, sm.Elapsed, 1e-4f);
            sm.Resume();
            sm.Tick(2f);
            Assert.AreEqual(CrackerState.Payoff, sm.State);
        }

        [Test]
        public void ResetToIdle_AbandonsWithoutFinished_AndCanBeReused()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            var rec = new Recorder(sm);
            sm.Place();
            sm.Ignite();
            sm.Tick(3f);
            sm.ResetToIdle();
            Assert.AreEqual(CrackerState.Idle, sm.State);
            Assert.AreEqual(0, rec.Finished);
            Assert.IsTrue(sm.Place());
            Assert.IsTrue(sm.Ignite());
            Assert.AreEqual(0f, sm.Elapsed);
        }

        [Test]
        public void Unplace_OnlyBeforeIgnition()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            sm.Place();
            Assert.IsTrue(sm.Unplace());
            Assert.AreEqual(CrackerState.Idle, sm.State);
            sm.Place();
            sm.Ignite();
            Assert.IsFalse(sm.Unplace());
        }

        [Test]
        public void TicksBeforeIgnition_DoNothing()
        {
            var sm = new CrackerStateMachine(Flowerpot());
            sm.Place();
            sm.Tick(100f);
            Assert.AreEqual(CrackerState.Placed, sm.State);
            Assert.AreEqual(0f, sm.FuseProgress);
        }

        [Test]
        public void NullTimeline_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CrackerStateMachine(null));
        }
    }

    public class CrackerTimelineTests
    {
        [Test]
        public void Valid_ReportsStopTime()
        {
            var t = new CrackerTimeline(2f, 1f, new[] { new TimelineCue(0f, CueKind.Normal, 0), new TimelineCue(5f, CueKind.Stop, 1) });
            Assert.AreEqual(5f, t.StopSeconds);
            Assert.AreEqual(2, t.CueCount);
        }

        [Test]
        public void MissingStop_IsRejected()
        {
            List<string> errors = CrackerTimeline.Validate(2f, 1f, new[] { new TimelineCue(0f, CueKind.Normal, 0) });
            Assert.IsTrue(errors.Exists(e => e.Contains("stop")));
        }

        [Test]
        public void StopNotLast_IsRejected()
        {
            List<string> errors = CrackerTimeline.Validate(1f, 1f, new[]
            {
                new TimelineCue(2f, CueKind.Stop, 0),
                new TimelineCue(2f, CueKind.Normal, 1),
            });
            Assert.IsTrue(errors.Exists(e => e.Contains("last")));
        }

        [Test]
        public void OutOfOrderTimes_AreRejected()
        {
            List<string> errors = CrackerTimeline.Validate(1f, 1f, new[]
            {
                new TimelineCue(3f, CueKind.Normal, 0),
                new TimelineCue(1f, CueKind.Normal, 1),
                new TimelineCue(5f, CueKind.Stop, 2),
            });
            Assert.IsTrue(errors.Exists(e => e.Contains("earlier")));
        }

        [Test]
        public void StopBeforeFuseEnds_IsRejected()
        {
            List<string> errors = CrackerTimeline.Validate(3f, 1f, new[] { new TimelineCue(2f, CueKind.Stop, 0) });
            Assert.IsTrue(errors.Exists(e => e.Contains("before the fuse")));
        }

        [Test]
        public void BadNumbers_AreRejected()
        {
            List<string> errors = CrackerTimeline.Validate(0f, -1f, new[] { new TimelineCue(-1f, CueKind.Stop, 0) });
            Assert.GreaterOrEqual(errors.Count, 3);
        }

        [Test]
        public void Constructor_ThrowsOnInvalid()
        {
            Assert.Throws<ArgumentException>(() => new CrackerTimeline(1f, 1f, Array.Empty<TimelineCue>()));
        }
    }
}
