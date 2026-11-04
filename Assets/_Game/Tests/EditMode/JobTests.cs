using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.District;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Tests
{
    /// <summary>Phase 12 step 6: the jobs run step by step, pay once in full, and the map finds the way.</summary>
    public class JobTests
    {
        private static readonly DistrictPlan Plan = DistrictGenerator.Bazaar;

        [Test]
        public void Catalog_ThreeJobs_EveryPlaceReachable()
        {
            List<JobDef> jobs = JobCatalog.Build(Plan);
            CollectionAssert.AreEqual(new[] { JobCatalog.Lane, JobCatalog.Diyas, JobCatalog.Rooftop }, jobs.Select(j => j.Id).ToArray());
            foreach (JobDef job in jobs)
            {
                Assert.IsNotEmpty(job.Title);
                Assert.IsNotEmpty(job.Offer);
                Assert.IsNotEmpty(job.Thanks);
                Assert.Greater(job.Coins, 0);
                Assert.Greater(job.Reputation, 0);
                foreach (JobStep s in job.Steps)
                {
                    Assert.IsNotEmpty(s.Objective, $"{job.Id} {s.Id}");
                    Assert.IsTrue(Reachable(s.X, s.Y, s.Z), $"{job.Id} {s.Id} at ({s.X:0.0}, {s.Y:0.0}, {s.Z:0.0}) can be stood on");
                    if (s.MovesPlayer)
                    {
                        Assert.IsTrue(Plan.IsWalkableAt(s.ToX, s.ToY, s.ToZ), $"{job.Id} {s.Id} moves the player somewhere they can stand");
                    }
                }
            }

            JobDef diyas = jobs[1];
            Assert.AreEqual(4, diyas.Steps.Count, "a pickup and three deliveries");
            Assert.AreEqual(3, diyas.Steps.Select(s => (s.X, s.Z)).Distinct().Count() - 1, "to three different shops");
            Assert.Greater(diyas.TimeLimit, 0f);

            DistrictBuilding house = JobCatalog.RooftopHouse(Plan);
            Assert.IsNotNull(house);
            Assert.GreaterOrEqual(house.Floors, 2);
            Assert.AreEqual(2, jobs[2].MinLevel, "the rooftop show is for someone the town knows");
        }

        [Test]
        public void LaneJob_AnyOrder_Completes()
        {
            var run = new JobRun(JobCatalog.LaneJob());
            Assert.AreEqual(8, run.Def.Steps.Count);
            StringAssert.Contains("(0 of 8)", run.Objective);
            Assert.AreEqual(JobProgress.None, run.Interact(0, 100f, 0f, 100f), "not from across town");
            int[] order = { 5, 0, 7, 2, 1, 6, 3 };
            foreach (int i in order)
            {
                JobStep s = run.Def.Steps[i];
                Assert.AreEqual(JobProgress.StepDone, run.Interact(i, s.X + 1f, 0f, s.Z));
            }

            StringAssert.Contains("(7 of 8)", run.Objective);
            Assert.AreEqual(JobProgress.None, run.Interact(5, run.Def.Steps[5].X, 0f, run.Def.Steps[5].Z), "not twice");
            JobStep last = run.Def.Steps[4];
            Assert.AreEqual(4, run.NearestOpen(0f, 0f), "the way to the last one");
            Assert.AreEqual(JobProgress.Completed, run.Interact(4, last.X, 0f, last.Z));
            Assert.AreEqual(JobStatus.Completed, run.Status);
            Assert.AreEqual(-1, run.NearestOpen(0f, 0f));
            Assert.AreEqual(-1f, run.TimeLeft, "no hurry");
        }

        [Test]
        public void DiyaJob_PickUpFirst_ThenDeliver_AgainstTheClock()
        {
            var run = new JobRun(JobCatalog.DiyaJob(Plan));
            JobStep pickup = run.Def.Steps[0], drop = run.Def.Steps[1];
            Assert.AreEqual(JobProgress.None, run.Interact(1, drop.X, 0f, drop.Z), "nothing to deliver yet");
            Assert.AreEqual(-1, run.InteractableAt(drop.X, 0f, drop.Z), "nothing to do at the shop yet");
            Assert.AreEqual(0, run.InteractableAt(pickup.X, 0f, pickup.Z));
            Assert.AreEqual(JobProgress.StepDone, run.Interact(0, pickup.X, 0f, pickup.Z));
            StringAssert.Contains("(0 of 3)", run.Objective);
            Assert.AreEqual(JobProgress.None, run.Tick(299f, 0f, 0f, 0f));
            Assert.AreEqual(1f, run.TimeLeft, 0.01f);
            Assert.AreEqual(JobProgress.Failed, run.Tick(2f, 0f, 0f, 0f));
            Assert.AreEqual(JobStatus.Failed, run.Status);
            Assert.AreEqual(JobProgress.None, run.Interact(1, drop.X, 0f, drop.Z), "too late");

            var quick = new JobRun(JobCatalog.DiyaJob(Plan));
            quick.Interact(0, pickup.X, 0f, pickup.Z);
            for (int i = 1; i <= 3; i++)
            {
                quick.Tick(30f, 0f, 0f, 0f);
                JobStep s = quick.Def.Steps[i];
                Assert.AreNotEqual(JobProgress.None, quick.Interact(i, s.X, 0f, s.Z));
            }

            Assert.AreEqual(JobStatus.Completed, quick.Status);
        }

        [Test]
        public void Rooftop_UpTheStairs_RacksOnTheRoof_ThenTheFinale()
        {
            var run = new JobRun(JobCatalog.RooftopJob(Plan));
            JobStep crate = run.Def.Steps[0], up = run.Def.Steps[1];
            run.Interact(0, crate.X, 0f, crate.Z);
            Assert.AreEqual(JobProgress.StepDone, run.Interact(1, up.X, 0f, up.Z));
            Assert.IsTrue(up.MovesPlayer);
            for (int i = 2; i <= 4; i++)
            {
                JobStep rack = run.Def.Steps[i];
                Assert.AreEqual(-1, run.InteractableAt(rack.X, 0f, rack.Z), "not from the street below");
                Assert.AreEqual(JobProgress.StepDone, run.Interact(i, rack.X, up.ToY, rack.Z));
                Assert.IsTrue(Plan.IsWalkableAt(rack.X, rack.Y, rack.Z), "on the roof, inside the parapet");
            }

            Assert.IsFalse(run.FinaleDue);
            JobStep light = run.Def.Steps[5];
            Assert.AreEqual(JobProgress.Completed, run.Interact(5, light.X, up.ToY, light.Z));
            Assert.IsTrue(run.FinaleDue, "the show goes up");
        }

        [Test]
        public void ReachSteps_DoneOnArriving_AndAbandon()
        {
            var def = new JobDef { Id = "t", Title = "t", Coins = 1, Reputation = 1 };
            def.Steps.Add(new JobStep { Id = "a", Kind = JobStepKind.Reach, Objective = "Go", X = 10f, Z = 10f, Radius = 2f });
            def.Steps.Add(new JobStep { Id = "b", Kind = JobStepKind.Reach, Objective = "Go on", X = 20f, Z = 10f, Radius = 2f, Group = 1 });
            var run = new JobRun(def);
            Assert.AreEqual(JobProgress.None, run.Tick(0.1f, 20f, 0f, 10f), "in order");
            Assert.AreEqual(JobProgress.StepDone, run.Tick(0.1f, 11f, 0f, 10f));
            Assert.AreEqual("Go on", run.Objective);
            Assert.AreEqual(JobProgress.Completed, run.Tick(0.1f, 20f, 0f, 11f));

            var left = new JobRun(def);
            left.Abandon();
            Assert.AreEqual(JobStatus.Abandoned, left.Status);
            Assert.AreEqual(JobProgress.None, left.Tick(0.1f, 11f, 0f, 10f));
        }

        [Test]
        public void Pay_InFullOnce_AQuarterAgain_AndTheRecordKeepsCount()
        {
            JobDef lane = JobCatalog.LaneJob();
            Assert.AreEqual((80L, 40), JobPay.For(lane, 0));
            Assert.AreEqual((20L, 10), JobPay.For(lane, 3));

            var record = new PlayerRecord();
            Assert.AreEqual(0, record.TimesDone(JobCatalog.Lane));
            record.FinishJob(JobCatalog.Lane);
            record.FinishJob(JobCatalog.Lane);
            record.FinishJob(JobCatalog.Diyas);
            Assert.AreEqual(2, record.TimesDone(JobCatalog.Lane));
            Assert.AreEqual(3, record.jobsDone);
            PlayerRecord copy = record.Clone();
            copy.FinishJob(JobCatalog.Rooftop);
            Assert.AreEqual(0, record.TimesDone(JobCatalog.Rooftop), "a copy is its own");

            for (int i = 0; i < PlayerRecord.MaxJobsKept + 10; i++)
            {
                record.FinishJob("x");
            }

            Assert.AreEqual(PlayerRecord.MaxJobsKept, record.jobsFinished.Count, "kept to a cap");

            var old = new PlayerRecord { version = 1, jobsFinished = null };
            Assert.IsTrue(old.Migrate());
            Assert.AreEqual(PlayerRecord.CurrentVersion, old.version);
            Assert.IsNotNull(old.jobsFinished);
            var damaged = new PlayerRecord { jobsFinished = new List<string> { "a", null } };
            Assert.IsTrue(damaged.Sanitize());
            CollectionAssert.AreEqual(new[] { "a" }, damaged.jobsFinished);
        }

        [Test]
        public void Roofs_WalkableInsideTheParapet_Only()
        {
            DistrictBuilding b = JobCatalog.RooftopHouse(Plan);
            Area f = b.Footprint;
            Assert.IsTrue(Plan.IsWalkableAt(f.CentreX, b.Height, f.CentreZ));
            Assert.IsFalse(Plan.IsWalkableAt(f.X0 + 0.2f, b.Height, f.CentreZ), "not on the parapet");
            Assert.IsFalse(Plan.IsWalkableAt(f.CentreX, b.Height + 3f, f.CentreZ), "not in the air");
            Assert.IsFalse(Plan.IsWalkableAt(f.CentreX, 0f, f.CentreZ), "not inside the house");
            Assert.IsTrue(Plan.IsWalkableAt(Plan.Square.CentreX + 10f, 0f, Plan.Square.CentreZ), "the ground as before");
            Assert.AreSame(b, Plan.RoofAt(f.CentreX, f.CentreZ, b.Height));
        }

        [Test]
        public void Map_FramesTheLaneAndTheBazaar_AndDrawsThem()
        {
            var frame = new MapFrame(Plan);
            (float u, float v) = frame.ToUV(0f, 0f);
            Assert.That(u, Is.InRange(0f, 0.2f), "home near the west edge");
            Assert.That(v, Is.InRange(0f, 1f));
            (float x, float z) = frame.FromUV(0.3f, 0.6f);
            (float u2, float v2) = frame.ToUV(x, z);
            Assert.AreEqual(0.3f, u2, 1e-4f);
            Assert.AreEqual(0.6f, v2, 1e-4f);
            Assert.AreEqual(MapFrame.Width * (frame.Z1 - frame.Z0) / (frame.X1 - frame.X0), frame.Height, 1f, "square pixels");

            byte[] px = MapRaster.Draw(Plan, frame);
            Assert.AreEqual(MapFrame.Width * frame.Height * 4, px.Length);
            CollectionAssert.AreEqual(MapRaster.Home, Pixel(px, frame, 0f, -3f), "home in saffron");
            CollectionAssert.AreEqual(MapRaster.Water, Pixel(px, frame, 200f, 138f), "the river");
            CollectionAssert.AreEqual(MapRaster.Road, Pixel(px, frame, 100f, 7f), "the bazaar road");
            CollectionAssert.AreEqual(MapRaster.Road, Pixel(px, frame, -15f, 6.8f), "the lane's street");
        }

        [Test]
        public void Route_FromHome_ToTheRooftop_AndBack_OnTheWalkways()
        {
            var route = new MapRoute(Plan, WalkGraph.Build(Plan));
            var xs = new float[MapRoute.MaxPoints];
            var zs = new float[MapRoute.MaxPoints];
            JobStep door = JobCatalog.RooftopJob(Plan).Steps[1];
            int n = route.Find(0f, SettlementLibrary.StreetZ0 + 0.5f, door.X, door.Z, xs, zs);
            Assert.Greater(n, 4);
            Assert.AreEqual((0f, SettlementLibrary.StreetZ0 + 0.5f), (xs[0], zs[0]), "starts at the player");
            Assert.AreEqual((door.X, door.Z), (xs[n - 1], zs[n - 1]), "ends at the door");
            float straight = (float)Math.Sqrt(door.X * door.X + door.Z * door.Z);
            float length = MapRoute.Length(xs, zs, n);
            Assert.That(length, Is.InRange(straight, straight * 2f), "a sensible way round");
            for (int i = 1; i < n - 1; i++)
            {
                bool lane = xs[i] <= Plan.Join.X1 + 0.01f && zs[i] >= SettlementLibrary.StreetZ0 && zs[i] <= SettlementLibrary.StreetZ1;
                Assert.IsTrue(lane || Plan.IsWalkable(xs[i], zs[i], 0.2f), $"point {i} ({xs[i]:0.0}, {zs[i]:0.0}) is on a walkway");
            }

            int back = route.Find(door.X, door.Z, -20f, 4.3f, xs, zs);
            Assert.AreEqual((-20f, 4.3f), (xs[back - 1], zs[back - 1]));
            Assert.AreEqual(SettlementLibrary.StreetLine, zs[back - 2], 0.01f, "along the lane's street at the end");

            int short_ = route.Find(-20f, 4.3f, 20f, 9.3f, xs, zs);
            Assert.AreEqual(4, short_, "within the lane: along the street");
        }

        private static bool Reachable(float x, float y, float z)
        {
            bool lane = x < Plan.Join.X0 && z >= SettlementLibrary.StreetZ0 && z <= SettlementLibrary.StreetZ1;
            return lane || Plan.IsWalkableAt(x, y, z);
        }

        private static byte[] Pixel(byte[] px, MapFrame f, float x, float z)
        {
            (float u, float v) = f.ToUV(x, z);
            int i = ((int)(v * f.Height) * MapFrame.Width + (int)(u * MapFrame.Width)) * 4;
            return new[] { px[i], px[i + 1], px[i + 2], px[i + 3] };
        }
    }
}
