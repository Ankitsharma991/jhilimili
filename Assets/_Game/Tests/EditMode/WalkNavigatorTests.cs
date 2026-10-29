using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Tests
{
    /// <summary>Phase 8 step 3: walking routes, areas, stairs and the walking agent (PRD 10 tap-to-move).</summary>
    public class WalkNavigatorTests
    {
        private static readonly object[][] Layouts = Environments.All.Select(e => new object[] { e.Id }).ToArray();

        [TestCaseSource(nameof(Layouts))]
        public void EverySpot_IsReachableOnFootWithoutCrossingWalls(string environment)
        {
            SettlementLayout layout = SettlementLibrary.For(environment);
            var nav = new WalkNavigator(layout, HouseDesign.Default());
            V3 start = layout.Node("courtyard").Position;
            // The home gate's leaves swing open for walking; every other solid stays in the way.
            Box3 gate = layout.Home.ToWorld(HouseGeometry.GateLeaves);
            List<Box3> solids = SettlementLint.Solids(layout).Where(b => b.ToString() != gate.ToString()).ToList();
            foreach (SpotDef spot in layout.spots)
            {
                List<Waypoint> route = nav.RouteToSpot(start, spot.id);
                Assert.IsNotNull(route, $"{environment}: no route to {spot.id}");
                if (route.Count == 0)
                {
                    Assert.AreEqual(0f, (start - layout.Node(spot.id).Position).Length, 0.05f, "an empty route means already there");
                    continue;
                }

                Assert.AreEqual(0f, (route[route.Count - 1].Position - layout.Node(spot.id).Position).Length, 1e-3f, spot.id);
                V3 at = start;
                foreach (Waypoint w in route)
                {
                    if (!w.Fade)
                    {
                        int steps = (int)((w.Position - at).Length / 0.2f) + 1;
                        for (int i = 0; i <= steps; i++)
                        {
                            V3 p = V3.Lerp(at, w.Position, i / (float)steps) + new V3(0f, 0.5f, 0f);
                            Assert.IsFalse(solids.Any(b => Inside(b, p)), $"{environment}: walking to {spot.id} passes through a wall at {p}");
                        }
                    }

                    at = w.Position;
                }
            }
        }

        [Test]
        public void ToTheRoof_UsesTheStairs_AndATiledRoofHasNoDeck()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            var nav = new WalkNavigator(layout, HouseDesign.Default());
            List<Waypoint> route = nav.RouteToSpot(layout.Node("courtyard").Position, "rooftop");
            Assert.IsTrue(route.Any(w => w.Fade), "a fade for the stairs");
            Assert.AreEqual(HouseGeometry.Eaves, route[route.Count - 1].Position.Y, 1e-4f);

            HouseDesign tiled = HouseDesign.Default();
            tiled.ModuleAt("roof").module = "roof_tiled_slope";
            tiled.ModuleAt("balcony").module = string.Empty;
            var noDeck = new WalkNavigator(layout, tiled);
            Assert.IsFalse(noDeck.Areas.Any(a => a.Id == "rooftop" || a.Id == "balcony"));
            Assert.IsFalse(WalkNavigator.HomeTags(tiled).Contains(SpotTags.HasRooftop));
            Assert.IsTrue(WalkNavigator.HomeTags(HouseDesign.Default()).Contains(SpotTags.HasBalcony));
        }

        [Test]
        public void ThroughTheGate_IsAStraightWalk_ButNotThroughTheWall()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            var nav = new WalkNavigator(layout, HouseDesign.Default());
            Assert.IsTrue(nav.Straight(new V3(HouseGeometry.GateX, 0f, 2f), new V3(HouseGeometry.GateX, 0f, 6f)), "out through the gate");
            Assert.IsFalse(nav.Straight(new V3(-2f, 0f, 2f), new V3(-2f, 0f, 6f)), "not through the back wall");
            List<Waypoint> route = nav.Route(new V3(-2f, 0f, 2f), new V3(-2f, 0f, 6f));
            Assert.IsNotNull(route);
            Assert.Greater(route.Count, 1, "round by the gate");
        }

        [Test]
        public void TapsOnWallsOrNeighbourYards_LandAtTheNearestWalkablePoint()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            var nav = new WalkNavigator(layout, HouseDesign.Default());
            Assert.IsFalse(nav.IsWalkable(new V3(-8.4f, 0f, 1f)), "a neighbour's courtyard");
            V3 n = nav.Nearest(new V3(-8.4f, 0f, 1f));
            Assert.IsTrue(nav.IsWalkable(n));
            List<Waypoint> route = nav.Route(layout.Node("courtyard").Position, new V3(-8.4f, 0f, 1f));
            Assert.IsNotNull(route);
            Assert.IsTrue(nav.IsWalkable(route[route.Count - 1].Position));
        }

        [Test]
        public void Joystick_SlidesAlongWalls()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            var nav = new WalkNavigator(layout, HouseDesign.Default());
            var p = new V3(-2f, 0f, 3.0f);
            V3 q = nav.Slide(p, new V3(0.3f, 0f, 0.3f));
            Assert.IsTrue(nav.IsWalkable(q));
            Assert.Greater(q.X, p.X, "slides along the back wall instead of stopping");
        }

        [Test]
        public void Agent_WalksTheRouteAtWalkingPace()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            var nav = new WalkNavigator(layout, HouseDesign.Default());
            V3 start = layout.Node("courtyard").Position;
            List<Waypoint> route = nav.RouteToSpot(start, "open");
            float length = 0f;
            V3 at = start;
            foreach (Waypoint w in route)
            {
                length += (w.Position - at).Length;
                at = w.Position;
            }

            var agent = new WalkAgent(start);
            agent.Follow(route);
            float t = 0f;
            while (agent.Walking && t < 120f)
            {
                agent.Tick(0.05f);
                t += 0.05f;
            }

            Assert.IsFalse(agent.Walking);
            Assert.AreEqual(0f, (agent.Position - layout.Node("open").Position).Length, 1e-3f);
            Assert.AreEqual(length / WalkAgent.Speed, t, 0.2f, "walking pace");
            Assert.Less(t, 60f, "the settlement is a short walk across");

            agent.Follow(nav.RouteToSpot(agent.Position, "rooftop"));
            while (agent.Walking)
            {
                agent.Tick(0.05f);
            }

            Assert.AreEqual(1, agent.Faded, "one fade on the stairs");
        }

        private static bool Inside(Box3 b, V3 p) =>
            p.X > b.Min.X + 0.01f && p.X < b.Max.X - 0.01f && p.Y > b.Min.Y + 0.01f && p.Y < b.Max.Y - 0.01f && p.Z > b.Min.Z + 0.01f && p.Z < b.Max.Z - 0.01f;
    }
}
