using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>
    /// Checks a settlement layout (Phase 8 step 1, PRD 10 and F19): 6 to 12 lots, the home lot where the Main scene expects
    /// it, no lot on another lot, the street or the open space, every spot's play area and camera clear of walls, pads in
    /// their spot, the sky above every fireworks camera open, and every spot reachable on foot without walking through a
    /// wall. Used by the tests and by the pack linter later (Phase 9).
    /// </summary>
    public static class SettlementLint
    {
        public const int MinLots = 6;
        public const int MaxLots = 12;

        /// <summary>Elevations (degrees) and side angles at which the sky above a fireworks camera must be clear.</summary>
        private static readonly float[] SkyElevations = { 18f, 30f };
        private static readonly float[] SkySides = { -12f, 0f, 12f };

        public static List<string> Check(SettlementLayout layout, HouseDesign home = null, EnvironmentInfo env = null)
        {
            var problems = new List<string>();
            if (layout == null)
            {
                problems.Add("no layout");
                return problems;
            }

            env = env ?? Environments.Find(layout.environment);
            if (env == null || env.Id != layout.environment)
            {
                problems.Add($"unknown environment '{layout.environment}'");
                return problems;
            }

            if (Array.IndexOf(EnvironmentFeatures.Features.All, layout.feature ?? string.Empty) < 0)
            {
                problems.Add($"unknown landmark '{layout.feature}'");
            }

            CheckLots(layout, problems);
            List<Box3> solids = Solids(layout, home, env);
            CheckSpots(layout, solids, problems);
            CheckWalkGraph(layout, solids, problems);
            return problems;
        }

        /// <summary>Every solid box of every built lot, in world space.</summary>
        public static List<Box3> Solids(SettlementLayout layout, HouseDesign home = null, EnvironmentInfo env = null)
        {
            var solids = new List<Box3>();
            foreach (LotDef lot in layout.lots)
            {
                HouseDesign design = SettlementLibrary.DesignFor(layout, lot, home, env);
                if (design == null)
                {
                    continue;
                }

                foreach (Box3 b in DecorPlacement.For(design, 2).Solids)
                {
                    solids.Add(lot.ToWorld(b));
                }
            }

            solids.AddRange(EnvironmentFeatures.Solids(layout));
            return solids;
        }

        /// <summary>True when the ray from <paramref name="origin"/> along <paramref name="dir"/> hits a box within <paramref name="range"/>.</summary>
        public static bool RayHits(Box3 b, V3 origin, V3 dir, float range)
        {
            float tMin = 0f, tMax = range;
            float[] o = { origin.X, origin.Y, origin.Z }, d = { dir.X, dir.Y, dir.Z };
            float[] lo = { b.Min.X, b.Min.Y, b.Min.Z }, hi = { b.Max.X, b.Max.Y, b.Max.Z };
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(d[i]) < 1e-8f)
                {
                    if (o[i] < lo[i] || o[i] > hi[i])
                    {
                        return false;
                    }

                    continue;
                }

                float t0 = (lo[i] - o[i]) / d[i], t1 = (hi[i] - o[i]) / d[i];
                if (t0 > t1)
                {
                    (t0, t1) = (t1, t0);
                }

                tMin = Math.Max(tMin, t0);
                tMax = Math.Min(tMax, t1);
                if (tMin > tMax)
                {
                    return false;
                }
            }

            return true;
        }

        public static V3 Direction(float yawDegrees, float elevationDegrees)
        {
            double y = yawDegrees * Math.PI / 180.0, e = elevationDegrees * Math.PI / 180.0;
            return new V3((float)(Math.Sin(y) * Math.Cos(e)), (float)Math.Sin(e), (float)(Math.Cos(y) * Math.Cos(e)));
        }

        private static void CheckLots(SettlementLayout layout, List<string> problems)
        {
            int count = layout.lots.Count;
            if (count < MinLots || count > MaxLots)
            {
                problems.Add($"{count} lots; a settlement has {MinLots} to {MaxLots} (F19)");
            }

            var ids = new HashSet<string>();
            int homes = 0;
            foreach (LotDef lot in layout.lots)
            {
                if (!ids.Add(lot.id))
                {
                    problems.Add($"lot id '{lot.id}' repeated");
                }

                if (Math.Abs(lot.yaw) > 0.01f && Math.Abs(lot.yaw - 180f) > 0.01f)
                {
                    problems.Add($"lot '{lot.id}' faces {lot.yaw} degrees; lots face 0 or 180");
                }

                if (lot.home)
                {
                    homes++;
                    if (Math.Abs(lot.x) > 1e-3f || Math.Abs(lot.z - HouseGeometry.WorldFrontZ) > 1e-3f || Math.Abs(lot.yaw) > 0.01f)
                    {
                        problems.Add("the home lot must be the Main scene's house (x 0, front at z -2.4, facing +z)");
                    }
                }
            }

            if (homes != 1)
            {
                problems.Add($"{homes} home lots; exactly one");
            }

            for (int i = 0; i < count; i++)
            {
                Box3 a = layout.lots[i].Footprint;
                for (int j = i + 1; j < count; j++)
                {
                    if (Flat(a).Overlaps(Flat(layout.lots[j].Footprint)))
                    {
                        problems.Add($"lots '{layout.lots[i].id}' and '{layout.lots[j].id}' overlap");
                    }
                }

                foreach (GroundArea area in new[] { layout.street, layout.open })
                {
                    if (Flat(a).Overlaps(Flat(new Box3(area.x0, 0f, area.z0, area.x1, 1f, area.z1))))
                    {
                        problems.Add($"lot '{layout.lots[i].id}' overlaps the {area.kind}");
                    }
                }
            }

            if (Flat(new Box3(layout.street.x0, 0f, layout.street.z0, layout.street.x1, 1f, layout.street.z1))
                .Overlaps(Flat(new Box3(layout.open.x0, 0f, layout.open.z0, layout.open.x1, 1f, layout.open.z1))))
            {
                problems.Add("the street and the open space overlap");
            }
        }

        private static void CheckSpots(SettlementLayout layout, List<Box3> solids, List<string> problems)
        {
            var ids = new HashSet<string>();
            foreach (SpotDef spot in layout.spots)
            {
                if (!ids.Add(spot.id))
                {
                    problems.Add($"spot id '{spot.id}' repeated");
                }

                if (Array.IndexOf(SpotKinds.All, spot.kind) < 0)
                {
                    problems.Add($"spot '{spot.id}' has unknown kind '{spot.kind}'");
                }

                foreach (string tag in spot.requires ?? new string[0])
                {
                    if (Array.IndexOf(SpotTags.All, tag) < 0)
                    {
                        problems.Add($"spot '{spot.id}' requires unknown tag '{tag}'");
                    }
                }

                if (!string.IsNullOrEmpty(spot.lot) && layout.Lot(spot.lot) == null)
                {
                    problems.Add($"spot '{spot.id}' belongs to missing lot '{spot.lot}'");
                }

                if (spot.aerials != Aerials.Open && spot.aerials != Aerials.Pads && spot.aerials != Aerials.None)
                {
                    problems.Add($"spot '{spot.id}' has unknown aerials rule '{spot.aerials}'");
                }

                if (spot.aerials == Aerials.Pads && spot.pads.Count == 0)
                {
                    problems.Add($"spot '{spot.id}' allows aerials only from pads but has none");
                }

                // The play area: its centre and rim, a hand above the ground, are clear of walls.
                for (int i = 0; i <= 16; i++)
                {
                    double a = 2 * Math.PI * i / 16;
                    float r = i == 16 ? 0f : spot.radius;
                    var p = new V3(spot.x + r * (float)Math.Cos(a), spot.y + 0.15f, spot.z + r * (float)Math.Sin(a));
                    if (InsideAny(solids, p))
                    {
                        problems.Add($"spot '{spot.id}': its play area runs into a wall near {p}");
                        break;
                    }
                }

                foreach (LaunchPad pad in spot.pads)
                {
                    var p = new V3(pad.x, pad.y + 0.15f, pad.z);
                    if (InsideAny(solids, p) || (p - new V3(spot.x, p.Y, spot.z)).Length > spot.radius + 1e-3f)
                    {
                        problems.Add($"spot '{spot.id}': a launch pad is outside its play area or in a wall");
                    }
                }

                if (InsideAny(solids, spot.Camera))
                {
                    problems.Add($"spot '{spot.id}': the camera is inside a wall");
                }

                // The fireworks need sky: nothing in the upper part of the camera's view.
                foreach (float elevation in SkyElevations)
                {
                    foreach (float side in SkySides)
                    {
                        V3 dir = Direction(spot.camYaw + side, elevation);
                        foreach (Box3 b in solids)
                        {
                            if (RayHits(b, spot.Camera, dir, 120f))
                            {
                                problems.Add($"spot '{spot.id}': a building blocks the sky {elevation} degrees up");
                                goto nextSpot;
                            }
                        }
                    }
                }

            nextSpot:;
            }
        }

        private static void CheckWalkGraph(SettlementLayout layout, List<Box3> solids, List<string> problems)
        {
            var ids = new HashSet<string>();
            foreach (WalkNode n in layout.nodes)
            {
                if (!ids.Add(n.id))
                {
                    problems.Add($"walk node '{n.id}' repeated");
                }
            }

            var links = new Dictionary<string, List<string>>();
            foreach (WalkEdge e in layout.edges)
            {
                WalkNode a = layout.Node(e.a), b = layout.Node(e.b);
                if (a == null || b == null)
                {
                    problems.Add($"walk edge {e.a}-{e.b} names a missing node");
                    continue;
                }

                Link(links, e.a, e.b);
                Link(links, e.b, e.a);
                if (e.kind == EdgeKinds.Walk)
                {
                    float length = (b.Position - a.Position).Length;
                    int steps = Math.Max(2, (int)(length / 0.25f));
                    for (int i = 0; i <= steps; i++)
                    {
                        V3 p = V3.Lerp(a.Position, b.Position, i / (float)steps) + new V3(0f, 0.3f, 0f);
                        if (InsideAny(solids, p))
                        {
                            problems.Add($"walking {e.a} to {e.b} goes through a wall near {p}");
                            break;
                        }
                    }
                }
            }

            if (layout.Node("courtyard") == null)
            {
                problems.Add("no walk node in the courtyard");
                return;
            }

            // Every spot has a node of the same id, reachable from the courtyard.
            var reached = new HashSet<string> { "courtyard" };
            var queue = new Queue<string>();
            queue.Enqueue("courtyard");
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                if (!links.TryGetValue(at, out List<string> next))
                {
                    continue;
                }

                foreach (string n in next)
                {
                    if (reached.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }

            foreach (SpotDef spot in layout.spots)
            {
                if (layout.Node(spot.id) == null)
                {
                    problems.Add($"spot '{spot.id}' has no walk node");
                }
                else if (!reached.Contains(spot.id))
                {
                    problems.Add($"spot '{spot.id}' cannot be reached on foot");
                }
            }
        }

        private static void Link(Dictionary<string, List<string>> links, string a, string b)
        {
            if (!links.TryGetValue(a, out List<string> list))
            {
                links[a] = list = new List<string>();
            }

            list.Add(b);
        }

        private static bool InsideAny(List<Box3> solids, V3 p)
        {
            foreach (Box3 b in solids)
            {
                if (p.X > b.Min.X + 0.01f && p.X < b.Max.X - 0.01f && p.Y > b.Min.Y + 0.01f && p.Y < b.Max.Y - 0.01f &&
                    p.Z > b.Min.Z + 0.01f && p.Z < b.Max.Z - 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private static Box3 Flat(Box3 b) => new Box3(b.Min.X, 0f, b.Min.Z, b.Max.X, 1f, b.Max.Z);
    }
}
