using System;
using System.Collections.Generic;
using System.Globalization;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>
    /// The built-in settlement layouts. All share the Main scene's frame: the home lot (the player's house, Phase 7) at the
    /// origin facing +z, the street past its gate (z 3.8 to 9.8), neighbour lots on the same side every 8.4 m, and the open
    /// space across the street, with far lots (facing back across it) at its far edge or none (the river).
    /// </summary>
    public static class SettlementLibrary
    {
        public const float StreetZ0 = 3.8f;
        public const float StreetZ1 = 9.8f;
        public const float StreetLine = 6.8f;
        public const float LotSpacing = 8.4f;

        private static readonly Dictionary<string, SettlementLayout> Cache = new Dictionary<string, SettlementLayout>();

        public static SettlementLayout For(string environment)
        {
            if (!Cache.TryGetValue(environment ?? string.Empty, out SettlementLayout layout))
            {
                layout = environment switch
                {
                    Environments.TownLane => Build(Environments.TownLane, new[] { -3, -2, -1, 1, 2 }, vacantAt: 3,
                        farX: new[] { -18f, -6f, 6f, 18f }, farFront: 34f, openKind: SpotKinds.Square, openTo: 27.6f, streetSpotAt: -1.5f),
                    Environments.HillVillage => Build(Environments.HillVillage, new[] { -2, -1, 1 }, vacantAt: 2,
                        farX: new[] { -14f, 0f, 14f }, farFront: 30f, openKind: SpotKinds.VillageArea, openTo: 23.6f, streetSpotAt: -1.5f),
                    Environments.Riverside => Build(Environments.Riverside, new[] { -3, -2, -1, 1, 2 }, vacantAt: 3,
                        farX: new float[0], farFront: 0f, openKind: SpotKinds.TownSpace, openTo: 20f, streetSpotAt: -1.5f),
                    _ => null,
                };

                if (layout == null)
                {
                    return null;
                }

                Cache[environment] = layout;
            }

            return layout;
        }

        /// <summary>The house design for a lot: the player's own for the home lot, a seeded neighbour's otherwise.</summary>
        public static HouseDesign DesignFor(SettlementLayout layout, LotDef lot, HouseDesign home)
        {
            if (lot.home)
            {
                return home ?? HouseDesign.Default();
            }

            return lot.vacant ? null : NeighbourDesigns.Generate(Environments.Find(layout.environment), lot.seed);
        }

        private static SettlementLayout Build(string env, int[] sameSide, int vacantAt, float[] farX, float farFront, string openKind, float openTo, float streetSpotAt)
        {
            var l = new SettlementLayout { id = env, environment = env };
            int seedBase = StableHash(env) & 0xffff;
            l.lots.Add(new LotDef { id = "home", x = 0f, z = HouseGeometry.WorldFrontZ, home = true });
            foreach (int k in sameSide)
            {
                l.lots.Add(new LotDef { id = "lot" + Num(k), x = k * LotSpacing, z = HouseGeometry.WorldFrontZ, seed = seedBase * 31 + k * 7919 });
            }

            l.lots.Add(new LotDef { id = "vacant", x = vacantAt * LotSpacing, z = HouseGeometry.WorldFrontZ, vacant = true });
            for (int i = 0; i < farX.Length; i++)
            {
                l.lots.Add(new LotDef { id = "far" + (i + 1), x = farX[i], z = farFront, yaw = 180f, seed = seedBase * 17 + i * 104729 });
            }

            int minK = Math.Min(sameSide[0], vacantAt), maxK = Math.Max(sameSide[sameSide.Length - 1], vacantAt);
            float x0 = minK * LotSpacing - 4.2f, x1 = maxK * LotSpacing + 4.2f;
            l.street = new GroundArea { kind = SpotKinds.Street, x0 = x0, z0 = StreetZ0, x1 = x1, z1 = StreetZ1 };
            l.open = new GroundArea { kind = openKind, x0 = x0, z0 = StreetZ1, x1 = x1, z1 = openTo };

            // Spots. The home ones keep today's courtyard camera; the rooftop and balcony need the home design's tags.
            Spot(l, "courtyard", SpotKinds.Courtyard, "Your courtyard", "home", 0f, 0f, 0.8f, 2.3f, 0f, 1.05f, -2f, 0f, 7f, SpotTags.HasCourtyard);
            Spot(l, "rooftop", SpotKinds.Rooftop, "Your rooftop", "home", 0f, HouseGeometry.Eaves, -5.2f, 1.6f, 0f, HouseGeometry.Eaves + 1.05f, -7.9f, 0f, 12f, SpotTags.HasRooftop);
            Spot(l, "balcony", SpotKinds.Balcony, "Your balcony", "home", 0f, HouseGeometry.UpperFloor, HouseGeometry.WorldFrontZ + 0.45f, 0.35f,
                0f, HouseGeometry.UpperFloor + 1.05f, HouseGeometry.WorldFrontZ + 0.08f, 0f, 20f, SpotTags.HasBalcony).aerials = Aerials.None;
            Spot(l, "house_front", SpotKinds.HouseFront, "Outside your gate", string.Empty, HouseGeometry.GateX, 0f, StreetLine, 1.8f,
                HouseGeometry.GateX, 1.05f, StreetZ0 + 0.6f, 0f, 7f);
            float sx = streetSpotAt * LotSpacing;
            SpotDef street = Spot(l, "street", SpotKinds.Street, "The street", string.Empty, sx, 0f, StreetLine, 1.8f, sx - 3.6f, 1.05f, StreetLine, 90f, 7f);
            street.aerials = Aerials.Pads;
            street.pads.Add(new LaunchPad { x = sx + 1f, z = StreetLine - 0.8f });
            street.pads.Add(new LaunchPad { x = sx + 1f, z = StreetLine + 0.8f });
            string openName = openKind == SpotKinds.Square ? "The chowk" : openKind == SpotKinds.VillageArea ? "The threshing ground" : "The ghat";
            string openTag = openKind == SpotKinds.Square ? SpotTags.Square : null;
            float oz = (StreetZ1 + openTo) / 2f;
            Spot(l, "open", openKind, openName, string.Empty, 8f, 0f, oz, 3.5f, 3.5f, 1.05f, oz, 90f, 7f, openTag);
            float vx = vacantAt * LotSpacing;
            Spot(l, "vacant_lot", SpotKinds.VacantLot, "The empty plot", "vacant", vx, 0f, 1f, 3f, vx, 1.05f, -2.2f, 0f, 7f, SpotTags.OpenLot);

            // Walk graph: courtyard, through the gate, along the street; the open space and the vacant plot off the street.
            Node(l, "courtyard", 0f, 0f, 0.8f);
            Node(l, "rooftop", 0f, HouseGeometry.Eaves, -5.2f);
            Node(l, "balcony", 0f, HouseGeometry.UpperFloor, HouseGeometry.WorldFrontZ + 0.45f);
            Node(l, "gate_in", HouseGeometry.GateX, 0f, 2.9f);
            Node(l, "gate_out", HouseGeometry.GateX, 0f, StreetZ0 + 0.7f);
            Edge(l, "courtyard", "rooftop", EdgeKinds.Stairs);
            Edge(l, "courtyard", "balcony", EdgeKinds.Stairs);
            Edge(l, "courtyard", "gate_in", EdgeKinds.Walk);
            Edge(l, "gate_in", "gate_out", EdgeKinds.Gate);
            Edge(l, "gate_out", "house_front", EdgeKinds.Walk);
            Node(l, "house_front", HouseGeometry.GateX, 0f, StreetLine);
            Node(l, "street", sx, 0f, StreetLine);

            var line = new List<(float x, string id)> { (HouseGeometry.GateX, "house_front"), (sx, "street") };
            for (int k = minK; k <= maxK; k++)
            {
                if (k != 0 && Math.Abs(k * LotSpacing - sx) > 0.5f)
                {
                    string id = "st" + Num(k);
                    Node(l, id, k * LotSpacing, 0f, StreetLine);
                    line.Add((k * LotSpacing, id));
                }
            }

            line.Sort((a, b) => a.x.CompareTo(b.x));
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Edge(l, line[i].id, line[i + 1].id, EdgeKinds.Walk);
            }

            Node(l, "open", 8f, 0f, oz);
            Edge(l, Nearest(line, 8f), "open", EdgeKinds.Walk);
            Node(l, "vacant_lot", vx, 0f, 1f);
            Edge(l, Nearest(line, vx), "vacant_lot", EdgeKinds.Walk);
            return l;
        }

        private static SpotDef Spot(SettlementLayout l, string id, string kind, string name, string lot, float x, float y, float z, float radius,
            float cx, float cy, float cz, float yaw, float pitch, string requires = null)
        {
            var s = new SpotDef
            {
                id = id, kind = kind, name = name, lot = lot, x = x, y = y, z = z, radius = radius, camX = cx, camY = cy, camZ = cz, camYaw = yaw, camPitch = pitch,
                requires = requires != null ? new[] { requires } : new string[0],
            };
            l.spots.Add(s);
            return s;
        }

        private static void Node(SettlementLayout l, string id, float x, float y, float z) => l.nodes.Add(new WalkNode { id = id, x = x, y = y, z = z });

        private static void Edge(SettlementLayout l, string a, string b, string kind) => l.edges.Add(new WalkEdge { a = a, b = b, kind = kind });

        private static string Nearest(List<(float x, string id)> line, float x)
        {
            string best = line[0].id;
            float d = float.MaxValue;
            foreach ((float lx, string id) in line)
            {
                if (Math.Abs(lx - x) < d)
                {
                    d = Math.Abs(lx - x);
                    best = id;
                }
            }

            return best;
        }

        /// <summary>FNV-1a: string.GetHashCode differs between runtimes, and neighbours must look the same on every phone.</summary>
        private static int StableHash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s)
            {
                h = (h ^ c) * 16777619;
            }

            return (int)(h & 0x7fffffff);
        }

        private static string Num(int k) => (k < 0 ? "m" : "p") + Math.Abs(k).ToString(CultureInfo.InvariantCulture);
    }
}
