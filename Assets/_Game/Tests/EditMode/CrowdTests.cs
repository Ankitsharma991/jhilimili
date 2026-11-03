using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.District;

namespace UtsavNights.Tests
{
    /// <summary>Phase 12 step 4: the district's people walk the footpaths, stand a while, and react.</summary>
    public class CrowdTests
    {
        private static DistrictPlan Plan => DistrictGenerator.Bazaar;

        [Test]
        public void Graph_JoinsEveryRoad_RoundTheSquare_ToThePromenade()
        {
            WalkGraph g = WalkGraph.Build(Plan);
            var seen = new HashSet<int> { 0 };
            var queue = new Queue<int>(new[] { 0 });
            while (queue.Count > 0)
            {
                foreach ((int to, float _) in g.Links[queue.Dequeue()])
                {
                    if (seen.Add(to))
                    {
                        queue.Enqueue(to);
                    }
                }
            }

            Assert.AreEqual(g.Count, seen.Count, "every point can reach every other");
            Assert.IsTrue(g.Nodes.Any(n => n.z > 110f && n.z < 118f), "the promenade is on it");
            Assert.IsFalse(g.Nodes.Any(n => Plan.Square.Contains(n.x, n.z, -7f)), "nobody aims for the middle of the square (the tower)");
            var path = new int[CrowdSim.MaxPath];
            int count = g.Path(g.Nearest(40f, -130f), g.Nearest(Plan.Square.CentreX, Plan.Square.CentreZ), path, new float[g.Count], new int[g.Count], new bool[g.Count]);
            Assert.Greater(count, 2, "from a far corner to the square");
        }

        [Test]
        public void People_StayOnTheFootpaths_AndGetAbout()
        {
            var sim = new CrowdSim(Plan, 140, 11);
            Assert.AreEqual(140 + Plan.Props.Count(p => p.Kind == DistrictProp.Stall), sim.People.Length, "residents and a shopkeeper per stall");
            float[] startX = sim.People.Select(p => p.X).ToArray();
            int walkingSeen = 0;
            for (int t = 0; t < 1200; t++)
            {
                sim.Tick(0.1f, -500f, -500f, true);
                if (t % 50 == 0)
                {
                    foreach (Person p in sim.People)
                    {
                        Assert.IsTrue(Plan.IsWalkable(p.X, p.Z, 0f), $"on walkable ground at {p.X:0.0},{p.Z:0.0}");
                    }

                    walkingSeen += sim.People.Count(p => p.State == PersonState.Walking);
                }
            }

            Assert.Greater(walkingSeen, 300, "people walk errands");
            int moved = sim.People.Where((p, i) => !p.Shopkeeper && Math.Abs(p.X - startX[i]) > 10f).Count();
            Assert.Greater(moved, 60, "and get about the town");
            Assert.IsTrue(sim.People.Where(p => p.Shopkeeper).All(p => p.State == PersonState.Standing), "shopkeepers keep their stalls");
        }

        [Test]
        public void Reactions_CheerCoverEarsWaveAndGather()
        {
            var sim = new CrowdSim(Plan, 140, 3);
            ref Person near = ref sim.People[0];
            float x = near.X, z = near.Z;
            sim.Burst(x + 2f, z, loud: true);
            Assert.AreEqual(PersonState.CoveringEars, sim.People[0].State, "a loud bang close by");
            int cheering = sim.People.Count(p => p.State == PersonState.Cheering);
            sim.Burst(x + 20f, z, loud: false);
            Assert.Greater(sim.People.Count(p => p.State == PersonState.Cheering), cheering, "a burst draws cheers");
            Person far = sim.People.OrderByDescending(p => Math.Abs(p.X - x) + Math.Abs(p.Z - z)).First();
            Assert.AreNotEqual(PersonState.Cheering, far.State == PersonState.Cheering && Math.Abs(far.X - x) > CrowdSim.CheerRange ? PersonState.Cheering : PersonState.Standing, "nobody cheers a burst out of range");

            // The player stops beside someone: a wave.
            var calm = new CrowdSim(Plan, 140, 5);
            Person keeper = calm.People.First(p => p.Shopkeeper);
            for (int i = 0; i < 12; i++)
            {
                calm.Tick(0.1f, keeper.X + 1.5f, keeper.Z, false);
            }

            Assert.IsTrue(calm.People.Any(p => p.State == PersonState.Waving), "someone waves at a player who stops nearby");

            // A show at the square draws the people near it.
            var show = new CrowdSim(Plan, 140, 8);
            int coming = show.Gather(Plan.Square.CentreX, Plan.Square.CentreZ, 80f, 60f);
            Assert.Greater(coming, 5);
            for (int t = 0; t < 900; t++)
            {
                show.Tick(0.1f, -500f, -500f, true);
            }

            int nearSquare = show.People.Count(p => !p.Shopkeeper && Plan.Square.Contains(p.X, p.Z, 8f));
            Assert.Greater(nearSquare, coming / 2, "they reach it");
        }

        [Test]
        public void StepsAsideForThePlayer_AndIsTheSameEveryRun()
        {
            var a = new CrowdSim(Plan, 60, 21);
            var b = new CrowdSim(Plan, 60, 21);
            for (int t = 0; t < 300; t++)
            {
                a.Tick(0.1f, 100f, 7f, true);
                b.Tick(0.1f, 100f, 7f, true);
            }

            for (int i = 0; i < a.People.Length; i++)
            {
                Assert.AreEqual(a.People[i].X, b.People[i].X);
                Assert.AreEqual(a.People[i].Z, b.People[i].Z);
            }

            // Someone walking straight at a player standing on their footpath goes round.
            var sim = new CrowdSim(Plan, 140, 13);
            float closest = float.MaxValue;
            for (int t = 0; t < 1500; t++)
            {
                Person p = sim.People[0];
                if (p.State == PersonState.Walking)
                {
                    float px = p.X + (float)Math.Sin(p.Facing * Math.PI / 180.0) * 1.2f, pz = p.Z + (float)Math.Cos(p.Facing * Math.PI / 180.0) * 1.2f;
                    for (int k = 0; k < 20; k++)
                    {
                        sim.Tick(0.05f, px, pz, true);
                        Person q = sim.People[0];
                        closest = Math.Min(closest, (float)Math.Sqrt((q.X - px) * (q.X - px) + (q.Z - pz) * (q.Z - pz)));
                    }

                    break;
                }

                sim.Tick(0.1f, -500f, -500f, true);
            }

            Assert.Greater(closest, 0.35f, "never walks into the player");
        }
    }
}
