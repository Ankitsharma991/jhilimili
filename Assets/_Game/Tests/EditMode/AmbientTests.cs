using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Tests
{
    /// <summary>Phase 8 step 5: the Night Timeline, ambient life by tier and Quiet mode, distant bursts, and where people stand.</summary>
    public class AmbientTests
    {
        [Test]
        public void Timeline_RunsDuskToLateAndHolds()
        {
            Assert.AreEqual(NightPhase.Dusk, NightTimeline.At(0f).Phase);
            Assert.AreEqual(NightPhase.Evening, NightTimeline.At(5f).Phase);
            Assert.AreEqual(NightPhase.Night, NightTimeline.At(12f).Phase);
            Assert.AreEqual(NightPhase.Late, NightTimeline.At(20f).Phase);
            Assert.AreEqual(NightPhase.Late, NightTimeline.At(500f).Phase, "holds at late night");
            Assert.Greater(NightTimeline.At(0f).SkyExposure, NightTimeline.At(24f).SkyExposure, "the sky darkens");
            float peak = Enumerable.Range(0, 25).Max(m => NightTimeline.At(m).BurstsPerMinute);
            Assert.AreEqual(peak, NightTimeline.At(8f).BurstsPerMinute, 1e-4f, "the bursts peak at night");
            Assert.Less(NightTimeline.At(24f).BurstsPerMinute, peak, "and thin out late");
            Assert.AreEqual("6:30 pm", NightTimeline.Clock(0f));
            Assert.AreEqual("1:00 am", NightTimeline.Clock(24f));
            Assert.AreEqual("9:45 pm", NightTimeline.Clock(12f));
            for (float m = 0f; m < 24f; m += 0.5f)
            {
                Assert.LessOrEqual((int)NightTimeline.At(m).Phase, (int)NightTimeline.At(m + 0.5f).Phase, "phases only move forward");
            }
        }

        [Test]
        public void Plan_FollowsTheTierQuietModeAndMemory()
        {
            NightState night = NightTimeline.At(10f);
            AmbientPlan low = AmbientPlan.For(0, false, night), mid = AmbientPlan.For(1, false, night), high = AmbientPlan.For(2, false, night);
            Assert.Greater(low.BurstsPerMinute, 0f, "distant bursts on every tier");
            Assert.AreEqual(0, low.Neighbours, "no looped neighbours on low");
            Assert.AreEqual(3, mid.Neighbours);
            Assert.AreEqual(0, mid.Crowd, "crowds on high only");
            Assert.AreEqual(6, high.Neighbours);
            Assert.AreEqual(12, high.Crowd);

            AmbientPlan quiet = AmbientPlan.For(2, true, night);
            Assert.AreEqual(0f, quiet.BurstsPerMinute, "Quiet mode turns it all off");
            Assert.AreEqual(0, quiet.Neighbours + quiet.Crowd);

            AmbientPlan pressed = AmbientPlan.For(2, false, night, lowMemory: true);
            Assert.LessOrEqual(pressed.Neighbours, 2);
            Assert.AreEqual(0, pressed.Crowd, "low memory reduces ambient characters (PRD 13.3)");
            Assert.Less(AmbientPlan.For(2, false, NightTimeline.At(0f)).Crowd, high.Crowd, "fewer people out at dusk");
        }

        [Test]
        public void Bursts_AreSeededFarAndAtTheRate()
        {
            var a = new DistantBursts(7);
            var b = new DistantBursts(7);
            var da = new List<DistantBurst>();
            var db = new List<DistantBurst>();
            for (int i = 0; i < 6000; i++)
            {
                a.Tick(0.1f, 8f, da);
                b.Tick(0.1f, 8f, db);
            }

            Assert.AreEqual(da.Count, db.Count, "deterministic");
            Assert.AreEqual(80f, da.Count, 20f, "about 8 a minute over 10 minutes");
            foreach (DistantBurst burst in da)
            {
                Assert.GreaterOrEqual(burst.Position.Z, 50f, "beyond the far lots");
                Assert.GreaterOrEqual(burst.Position.Y, 25f, "above every roof");
                Assert.That(burst.Colour, Is.InRange(0, DistantBursts.Colours - 1));
            }

            var none = new List<DistantBurst>();
            new DistantBursts(1).Tick(100f, 0f, none);
            CollectionAssert.IsEmpty(none, "no rate, no bursts");
        }

        [Test]
        public void Anchors_AreClearOfWallsAndSpots()
        {
            foreach (EnvironmentInfo env in Environments.All)
            {
                SettlementLayout layout = SettlementLibrary.For(env.Id);
                List<AmbientAnchor> anchors = AmbientAnchors.For(layout);
                List<Box3> solids = SettlementLint.Solids(layout);
                int built = layout.lots.Count(l => !l.home && !l.vacant);
                Assert.AreEqual(built, anchors.Count(a => !a.Crowd), env.Id);
                Assert.AreEqual(AmbientPlan.MaxCrowd[2], anchors.Count(a => a.Crowd));
                foreach (AmbientAnchor a in anchors)
                {
                    foreach (float h in new[] { 0.3f, 1f, 1.6f })
                    {
                        V3 p = a.Position + new V3(0f, h, 0f);
                        Assert.IsFalse(solids.Any(b => p.X > b.Min.X + 0.25f && p.X < b.Max.X - 0.25f && p.Y > b.Min.Y && p.Y < b.Max.Y && p.Z > b.Min.Z + 0.25f && p.Z < b.Max.Z - 0.25f),
                            $"{env.Id}: someone stands in a wall at {a.Position}");
                    }

                    foreach (SpotDef spot in layout.spots)
                    {
                        Assert.IsFalse(CelebrationRules.InArea(spot, a.Position), $"{env.Id}: someone stands in the {spot.id} play area");
                    }

                    if (a.Crowd)
                    {
                        Assert.IsTrue(layout.open.Contains(a.Position.X, a.Position.Z), "the crowd is on the open space");
                    }
                }
            }
        }
    }
}
