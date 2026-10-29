using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>Celebration spot kinds (PRD 10). Stored as their lowercase ids in layout JSON.</summary>
    public static class SpotKinds
    {
        public const string Courtyard = "courtyard";
        public const string Rooftop = "rooftop";
        public const string Balcony = "balcony";
        public const string HouseFront = "house_front";
        public const string Street = "street";
        public const string Square = "square";
        public const string VacantLot = "vacant_lot";
        public const string VillageArea = "village_area";
        public const string TownSpace = "town_space";

        public static readonly string[] All = { Courtyard, Rooftop, Balcony, HouseFront, Street, Square, VacantLot, VillageArea, TownSpace };
    }

    /// <summary>Availability tags (PRD 10). Home-lot tags come from the player's house design; the rest from the layout.</summary>
    public static class SpotTags
    {
        public const string HasCourtyard = "has_courtyard";
        public const string HasRooftop = "has_rooftop";
        public const string HasBalcony = "has_balcony";
        public const string Square = "square";
        public const string OpenLot = "open_lot";

        public static readonly string[] All = { HasCourtyard, HasRooftop, HasBalcony, Square, OpenLot };
    }

    /// <summary>Where aerials may launch at a spot (PRD 10: aerials stay in a sky corridor above their pad).</summary>
    public static class Aerials
    {
        /// <summary>From any valid ground point, if the sky above it is clear.</summary>
        public const string Open = "open";

        /// <summary>Only from the spot's launch pads (wires or eaves overhead elsewhere).</summary>
        public const string Pads = "pads";

        /// <summary>None here (a balcony): ground crackers only.</summary>
        public const string None = "none";
    }

    public static class EdgeKinds
    {
        public const string Walk = "walk";

        /// <summary>Through a gate: the gate opens, so the edge may cross the gate's solid.</summary>
        public const string Gate = "gate";

        /// <summary>Up to a roof or balcony inside the house: a short fade, not a walk.</summary>
        public const string Stairs = "stairs";
    }

    /// <summary>
    /// One lot (PRD 10): a house and its yard, placed by its house-space origin (the house front's centre at the ground) and
    /// a yaw of 0 (the front faces +z) or 180. The home lot is the player's own house in the Main scene. A vacant lot has no
    /// house: it is an open celebration ground.
    /// </summary>
    [Serializable]
    public sealed class LotDef
    {
        public string id = string.Empty;
        public float x;
        public float z;
        public float yaw;
        public bool home;
        public bool vacant;
        public int seed;

        public V3 ToWorld(V3 house)
        {
            bool turned = Math.Abs(yaw - 180f) < 1f;
            return new V3(x + (turned ? -house.X : house.X), house.Y, z + (turned ? -house.Z : house.Z));
        }

        public Box3 ToWorld(Box3 house)
        {
            V3 a = ToWorld(house.Min), b = ToWorld(house.Max);
            return new Box3(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
        }

        /// <summary>The ground the lot takes: the house and its courtyard to the boundary walls.</summary>
        public Box3 Footprint => ToWorld(new Box3(-HouseGeometry.HalfWidth, 0f, -HouseGeometry.Depth, HouseGeometry.HalfWidth, 9f,
            HouseGeometry.BackWallZ + HouseGeometry.BoundaryThickness / 2f));
    }

    [Serializable]
    public sealed class LaunchPad
    {
        public float x;
        public float y;
        public float z;
    }

    /// <summary>
    /// A celebration spot (PRD 10): its play area (centre and radius on the ground or deck), the fireworks camera for it,
    /// the tags it needs, and launch pads for aerials. Spots without pads allow no aerials there.
    /// </summary>
    [Serializable]
    public sealed class SpotDef
    {
        public string id = string.Empty;
        public string kind = SpotKinds.Courtyard;
        public string name = string.Empty;

        /// <summary>The lot the spot belongs to, or empty for public spaces.</summary>
        public string lot = string.Empty;
        public float x;
        public float y;
        public float z;
        public float radius = 2.5f;
        public float camX;
        public float camY;
        public float camZ;

        /// <summary>Camera yaw in degrees (0 looks along +z) and pitch (positive looks down).</summary>
        public float camYaw;
        public float camPitch = 7f;
        public string[] requires = new string[0];
        public string aerials = Aerials.Open;
        public List<LaunchPad> pads = new List<LaunchPad>();

        public V3 Centre => new V3(x, y, z);

        public V3 Camera => new V3(camX, camY, camZ);
    }

    [Serializable]
    public sealed class WalkNode
    {
        public string id = string.Empty;
        public float x;
        public float y;
        public float z;

        public V3 Position => new V3(x, y, z);
    }

    [Serializable]
    public sealed class WalkEdge
    {
        public string a = string.Empty;
        public string b = string.Empty;
        public string kind = EdgeKinds.Walk;
    }

    /// <summary>An axis-aligned area on the ground (x0..x1, z0..x1) with a kind, for the street and the open space.</summary>
    [Serializable]
    public sealed class GroundArea
    {
        public string kind = string.Empty;
        public float x0;
        public float z0;
        public float x1;
        public float z1;

        public bool Contains(float x, float z, float margin = 0f) => x >= x0 - margin && x <= x1 + margin && z >= z0 - margin && z <= z1 + margin;
    }

    /// <summary>
    /// A settlement's layout (PRD 10, layout JSON): the lots, the street, the open space, the celebration spots and the walk
    /// graph, in Main-scene world space. Chunks are street segments <see cref="ChunkSize"/> metres wide along x.
    /// </summary>
    [Serializable]
    public sealed class SettlementLayout
    {
        public const int CurrentVersion = 1;
        public const float ChunkSize = 16.8f;

        public int version = CurrentVersion;
        public string id = string.Empty;
        public string environment = string.Empty;
        public List<LotDef> lots = new List<LotDef>();
        public GroundArea street = new GroundArea { kind = "street" };
        public GroundArea open = new GroundArea { kind = "square" };
        public List<SpotDef> spots = new List<SpotDef>();
        public List<WalkNode> nodes = new List<WalkNode>();
        public List<WalkEdge> edges = new List<WalkEdge>();

        public LotDef Home => lots.Find(l => l.home);

        public LotDef Lot(string lotId) => lots.Find(l => l.id == lotId);

        public SpotDef Spot(string spotId) => spots.Find(s => s.id == spotId);

        public WalkNode Node(string nodeId) => nodes.Find(n => n.id == nodeId);

        public static int ChunkOf(float x) => (int)Math.Floor((x + ChunkSize / 2f) / ChunkSize);

        /// <summary>The chunks the layout spans (by lots, the street and the open space).</summary>
        public void ChunkRange(out int first, out int last)
        {
            first = Math.Min(ChunkOf(street.x0), ChunkOf(open.x0));
            last = Math.Max(ChunkOf(street.x1), ChunkOf(open.x1));
            foreach (LotDef l in lots)
            {
                first = Math.Min(first, ChunkOf(l.x));
                last = Math.Max(last, ChunkOf(l.x));
            }
        }
    }
}
