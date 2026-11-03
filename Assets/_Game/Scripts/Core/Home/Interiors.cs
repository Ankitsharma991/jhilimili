using System;
using System.Collections.Generic;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Core.Home
{
    /// <summary>A piece of furniture in a room: a solid box, and whether its top takes small decorations.</summary>
    public sealed class RoomFurniture
    {
        public RoomFurniture(string name, Box3 box, HousePaint paint, bool topTakesItems, bool solid = true)
        {
            Name = name;
            Box = box;
            Paint = paint;
            TopTakesItems = topTakesItems;
            Solid = solid;
        }

        public string Name { get; }

        public Box3 Box { get; }

        public HousePaint Paint { get; }

        /// <summary>Diyas and small lamps can stand on it (a shelf, a table, a chest).</summary>
        public bool TopTakesItems { get; }

        /// <summary>False for a rug: things stand on it.</summary>
        public bool Solid { get; }
    }

    /// <summary>A window on the back wall: from inside, the night outside.</summary>
    public readonly struct RoomWindow
    {
        public RoomWindow(float x0, float x1, float y0, float y1)
        {
            X0 = x0;
            X1 = x1;
            Y0 = y0;
            Y1 = y1;
        }

        public float X0 { get; }

        public float X1 { get; }

        public float Y0 { get; }

        public float Y1 { get; }
    }

    /// <summary>
    /// An interior room (Phase 11 step 2, F25). Room space: the floor at y = 0, x across from -width/2 to width/2, z from
    /// the door (0, where the camera stands) to the back wall (depth). Rooms are generated from code like the house.
    /// </summary>
    public sealed class RoomDef
    {
        public RoomDef(string id, string name, float width, float depth, float height, RoomWindow[] windows, RoomFurniture[] furniture)
        {
            Id = id;
            Name = name;
            Width = width;
            Depth = depth;
            Height = height;
            Windows = windows;
            Furniture = furniture;
        }

        public string Id { get; }

        public string Name { get; }

        public float Width { get; }

        public float Depth { get; }

        public float Height { get; }

        public RoomWindow[] Windows { get; }

        public RoomFurniture[] Furniture { get; }

        public float HalfWidth => Width / 2f;
    }

    public static class Rooms
    {
        public const string Main = "main_room";
        public const string Upper = "upper_room";

        public static readonly RoomDef[] All =
        {
            new RoomDef(Main, "Main room", 4.6f, 4.2f, 2.9f, new[] { new RoomWindow(0.3f, 1.7f, 0.9f, 2.1f) }, new[]
            {
                new RoomFurniture("Rug", new Box3(-1.2f, 0f, 1.4f, 1.2f, 0.01f, 3.1f), HousePaint.Fabric, false, solid: false),
                new RoomFurniture("Low table", new Box3(-0.5f, 0f, 1.9f, 0.5f, 0.42f, 2.6f), HousePaint.Wood, true),
                new RoomFurniture("Cushion left", new Box3(-1.15f, 0f, 1.9f, -0.65f, 0.18f, 2.6f), HousePaint.Accent, false),
                new RoomFurniture("Cushion right", new Box3(0.65f, 0f, 1.9f, 1.15f, 0.18f, 2.6f), HousePaint.Accent, false),
                new RoomFurniture("Wall shelf", new Box3(-1.7f, 1.1f, 3.9f, -0.3f, 1.14f, 4.2f), HousePaint.Wood, true),
                new RoomFurniture("Chest", new Box3(1.55f, 0f, 3.4f, 2.2f, 0.55f, 4.05f), HousePaint.Wood, true),
            }),
            new RoomDef(Upper, "Upper room", 4.0f, 3.6f, 2.6f, new[] { new RoomWindow(-0.6f, 0.6f, 0.9f, 2f) }, new[]
            {
                new RoomFurniture("Rug", new Box3(-0.9f, 0f, 1.2f, 0.9f, 0.01f, 2.6f), HousePaint.Fabric, false, solid: false),
                new RoomFurniture("Divan", new Box3(-1.95f, 0f, 1.0f, -0.85f, 0.45f, 3.0f), HousePaint.Accent, true),
                new RoomFurniture("Side table", new Box3(1.05f, 0f, 1.3f, 1.55f, 0.5f, 1.8f), HousePaint.Wood, true),
                new RoomFurniture("Wall shelf", new Box3(0.8f, 1.2f, 3.3f, 1.9f, 1.24f, 3.6f), HousePaint.Wood, true),
            }),
        };

        public static RoomDef Find(string id) => Array.Find(All, r => r.Id == id);
    }

    /// <summary>Budgets indoors, by tier (low, mid, high): decoration units, and the room's triangles with its decorations.</summary>
    public static class InteriorBudget
    {
        public static readonly int[] Units = { 14, 22, 32 };
        public static readonly int[] Triangles = { 12000, 20000, 32000 };

        /// <summary>Real point lights in the room: the low tier relies on ambient light and glowing items.</summary>
        public static readonly int[] Lights = { 0, 1, 2 };

        public const float WallMargin = 0.08f;
        public const float DoorClear = 0.9f;
        public const float MinHang = 0.5f;
        public const float MaxHangBelowCeiling = 0.15f;

        public static int For(int[] byTier, int tier) => byTier[Math.Max(0, Math.Min(byTier.Length - 1, tier))];
    }

    /// <summary>The room as a mesh: inward-facing walls in the house's colours, floor, ceiling, windows and furniture.</summary>
    public static class InteriorGenerator
    {
        public static HouseMesh Room(RoomDef room)
        {
            var m = new HouseMesh();
            float hw = room.HalfWidth, d = room.Depth, h = room.Height;
            const float t = 0.1f;
            m.Box(-hw - t, -t, -0.2f, hw + t, 0f, d + t, HousePaint.Stone);                  // floor
            m.Box(-hw - t, h, -0.2f, hw + t, h + t, d + t, HousePaint.Trim);                 // ceiling
            m.Box(-hw - t, 0f, -0.2f, -hw, h, d, HousePaint.Wall);                           // left wall
            m.Box(hw, 0f, -0.2f, hw + t, h, d, HousePaint.Wall);                             // right wall
            m.Box(-hw, 0f, d, hw, h, d + t, HousePaint.Wall);                                // back wall
            m.Box(-hw, 0f, -0.2f - t, hw, h, -0.2f, HousePaint.Wall);                        // the door's wall, behind the camera
            m.Box(-hw, 0f, d - 0.02f, hw, 0.12f, d, HousePaint.Trim);                        // skirting
            m.Box(-hw, h - 0.08f, d - 0.03f, hw, h, d, HousePaint.Trim);                     // cornice
            foreach (RoomWindow w in room.Windows)
            {
                m.Box(w.X0, w.Y0, d - 0.01f, w.X1, w.Y1, d, HousePaint.Dark);                // the night, through the glass
                m.Box(w.X0 - 0.06f, w.Y0 - 0.06f, d - 0.05f, w.X1 + 0.06f, w.Y0, d, HousePaint.Wood);
                m.Box(w.X0 - 0.06f, w.Y1, d - 0.05f, w.X1 + 0.06f, w.Y1 + 0.06f, d, HousePaint.Wood);
                m.Box(w.X0 - 0.06f, w.Y0, d - 0.05f, w.X0, w.Y1, d, HousePaint.Wood);
                m.Box(w.X1, w.Y0, d - 0.05f, w.X1 + 0.06f, w.Y1, d, HousePaint.Wood);
                float mid = (w.X0 + w.X1) / 2f;
                m.Box(mid - 0.02f, w.Y0, d - 0.04f, mid + 0.02f, w.Y1, d, HousePaint.Wood);
            }

            foreach (RoomFurniture f in room.Furniture)
            {
                m.Box(f.Box.Min.X, f.Box.Min.Y, f.Box.Min.Z, f.Box.Max.X, f.Box.Max.Y, f.Box.Max.Z, f.Paint);
            }

            return m;
        }

        /// <summary>The colour of a room paint: the walls and trim follow the house's own colours, so outside and inside match.</summary>
        public static Rgb Colour(HousePaint paint, HouseDesign design)
        {
            switch (paint)
            {
                case HousePaint.Wall:
                {
                    Rgb w = HomeCatalog.WallColours[Math.Max(0, Math.Min(HomeCatalog.WallColours.Length - 1, design.wallColour))];
                    return new Rgb(0.35f + w.R * 0.6f, 0.33f + w.G * 0.6f, 0.3f + w.B * 0.6f); // paler indoors
                }

                case HousePaint.Trim:
                    return HomeCatalog.TrimColours[Math.Max(0, Math.Min(HomeCatalog.TrimColours.Length - 1, design.trimColour))];
                case HousePaint.Accent:
                    return new Rgb(0.62f, 0.14f, 0.18f);
                case HousePaint.Fabric:
                    return new Rgb(0.7f, 0.42f, 0.16f);
                case HousePaint.Wood:
                    return HousePalette.Wood;
                case HousePaint.Stone:
                    return new Rgb(0.55f, 0.45f, 0.36f);
                case HousePaint.Dark:
                    return new Rgb(0.04f, 0.05f, 0.12f);
                default:
                    return HousePalette.Dark;
            }
        }
    }

    /// <summary>Where a tap inside landed: the floor, a wall (with its inward normal) or a furniture top.</summary>
    public readonly struct RoomHit
    {
        public RoomHit(Surface surface, V3 point, V3 normal)
        {
            Surface = surface;
            Point = point;
            Normal = normal;
        }

        /// <summary>Ground (the floor), Wall, or Step (a furniture top).</summary>
        public Surface Surface { get; }

        public V3 Point { get; }

        public V3 Normal { get; }
    }

    /// <summary>
    /// Placing decorations indoors (Phase 11 step 2): which items may come in, their pose from a tap, and the rules (inside
    /// the room, clear of the door, on a surface the item allows, not inside furniture or another item, within the
    /// room's budget for the tier), each with a plain reason.
    /// </summary>
    public static class InteriorPlacement
    {
        /// <summary>Items that belong indoors: lamps, plants, furniture, rangoli and the balloons; no path items, garden lamps or tulsi.</summary>
        public static bool Allowed(DecorInfo info) =>
            info != null && info.Mode != PlacementMode.Path && info.Id != "lamp_post_small" && info.Id != "tulsi_planter" && info.Id != "charpai";

        public static List<DecorInfo> Items()
        {
            var list = new List<DecorInfo>();
            foreach (DecorInfo d in HomeCatalog.Decorations)
            {
                if (Allowed(d))
                {
                    list.Add(d);
                }
            }

            return list;
        }

        /// <summary>The item's pose for a tap: standing on the floor or a top facing the door, or hanging on a wall facing in.</summary>
        public static PlacedDecor PoseFor(RoomDef room, DecorInfo info, RoomHit hit)
        {
            var p = new PlacedDecor { item = info.Id, room = room.Id, surface = hit.Surface, scale = 1f };
            if (hit.Surface == Surface.Wall)
            {
                V3 at = hit.Point + hit.Normal * (info.Depth / 2f);
                p.x = at.X;
                p.z = at.Z;
                p.y = Math.Max(InteriorBudget.MinHang, hit.Point.Y - info.Height / 2f);
                p.yaw = (float)(Math.Atan2(hit.Normal.X, hit.Normal.Z) * 180.0 / Math.PI);
            }
            else
            {
                p.x = hit.Point.X;
                p.y = hit.Point.Y;
                p.z = hit.Point.Z;
                p.yaw = 180f; // face the door, where the camera stands
            }

            return p;
        }

        /// <summary>Why the pose can't stay, or null when it can.</summary>
        public static string Problem(RoomDef room, IReadOnlyList<PlacedDecor> roomItems, PlacedDecor pose, int tier)
        {
            DecorInfo info = HomeCatalog.Decoration(pose.item);
            if (room == null || !Allowed(info))
            {
                return "That one stays outside.";
            }

            Surface wanted = pose.surface;
            Surface allows = info.Surfaces;
            bool surfaceOk = wanted == Surface.Wall ? (allows & (Surface.Wall | Surface.Railing)) != 0
                : wanted == Surface.Step ? (allows & (Surface.Step | Surface.Railing | Surface.Ground)) != 0 && info.Width <= 0.5f
                : (allows & Surface.Ground) != 0;
            if (!surfaceOk)
            {
                return wanted == Surface.Wall ? $"The {Name(info)} doesn't hang on a wall." : wanted == Surface.Step ? $"The {Name(info)} is too big for that." : $"The {Name(info)} doesn't stand on the floor.";
            }

            float hw = info.Width / 2f * pose.scale, hd = info.Depth / 2f * pose.scale;
            Box3 foot = Footprint(pose, info);
            float m = InteriorBudget.WallMargin;
            if (wanted == Surface.Wall)
            {
                // Against its wall by design: only the run along the wall is checked.
                float along = info.Width / 2f * pose.scale;
                bool sideWall = Math.Abs(Math.Sin(pose.yaw * Math.PI / 180.0)) > 0.7;
                bool corner = sideWall ? pose.z - along < InteriorBudget.DoorClear || pose.z + along > room.Depth - m : Math.Abs(pose.x) + along > room.HalfWidth - m;
                if (corner)
                {
                    return "That's too close to the corner.";
                }

                if (pose.y < InteriorBudget.MinHang - 1e-3f || pose.y + info.Height * pose.scale > room.Height - InteriorBudget.MaxHangBelowCeiling)
                {
                    return "Hang it a little lower.";
                }
            }
            else if (foot.Min.X < -room.HalfWidth + m - 1e-3f || foot.Max.X > room.HalfWidth - m + 1e-3f || foot.Max.Z > room.Depth - m + 1e-3f || foot.Min.Z < m)
            {
                return "That's too close to the wall.";
            }

            if (wanted != Surface.Wall && pose.z - hd < InteriorBudget.DoorClear)
            {
                return "Keep the doorway clear.";
            }

            foreach (RoomFurniture f in room.Furniture)
            {
                if (!f.Solid)
                {
                    continue;
                }

                bool onTop = wanted == Surface.Step && f.TopTakesItems && Math.Abs(pose.y - f.Box.Max.Y) < 0.02f;
                if (onTop)
                {
                    if (pose.x - hw < f.Box.Min.X - 1e-3f || pose.x + hw > f.Box.Max.X + 1e-3f || pose.z - hd < f.Box.Min.Z - 1e-3f || pose.z + hd > f.Box.Max.Z + 1e-3f)
                    {
                        return $"The {Name(info)} would hang over the edge.";
                    }

                    continue;
                }

                if (Overlaps(foot, f.Box))
                {
                    return $"That's in the way of the {f.Name.ToLowerInvariant()}.";
                }
            }

            if (wanted == Surface.Step && !OnSomeTop(room, pose))
            {
                return "Put it on a table, a shelf or the chest.";
            }

            int units = DecorBudget.CostOf(info);
            foreach (PlacedDecor other in roomItems)
            {
                DecorInfo oi = HomeCatalog.Decoration(other.item);
                if (oi == null || other.uid == pose.uid)
                {
                    continue;
                }

                units += DecorBudget.CostOf(oi);
                bool flat = info.Mode == PlacementMode.Decal || oi.Mode == PlacementMode.Decal;
                if (!flat && Overlaps(foot, Footprint(other, oi)))
                {
                    return $"That's on top of the {Name(oi)}.";
                }
            }

            int budget = InteriorBudget.For(InteriorBudget.Units, tier);
            return units > budget ? $"This room is full for this phone ({budget} units). Remove something first." : null;
        }

        /// <summary>The space an item takes, as a box in room space (turned with its yaw in quarter turns).</summary>
        public static Box3 Footprint(PlacedDecor d, DecorInfo info)
        {
            bool turned = Math.Abs(Math.Sin(d.yaw * Math.PI / 180.0)) > 0.7;
            float hw = (turned ? info.Depth : info.Width) / 2f * d.scale;
            float hd = (turned ? info.Width : info.Depth) / 2f * d.scale;
            float height = Math.Max(0.01f, info.Height * d.scale);
            return new Box3(d.x - hw, d.y, d.z - hd, d.x + hw, d.y + height, d.z + hd);
        }

        /// <summary>The room's triangles with its decorations (the memory and draw budget inside).</summary>
        public static int Triangles(RoomDef room, IEnumerable<PlacedDecor> items)
        {
            int n = InteriorGenerator.Room(room).TriangleCount;
            foreach (PlacedDecor d in items)
            {
                n += DecorGenerator.Item(d.item)?.TriangleCount ?? 0;
            }

            return n;
        }

        private static bool OnSomeTop(RoomDef room, PlacedDecor pose)
        {
            foreach (RoomFurniture f in room.Furniture)
            {
                if (f.TopTakesItems && Math.Abs(pose.y - f.Box.Max.Y) < 0.02f && pose.x >= f.Box.Min.X && pose.x <= f.Box.Max.X && pose.z >= f.Box.Min.Z && pose.z <= f.Box.Max.Z)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Overlaps(Box3 a, Box3 b) =>
            a.Min.X < b.Max.X - 1e-3f && a.Max.X > b.Min.X + 1e-3f && a.Min.Y < b.Max.Y - 1e-3f && a.Max.Y > b.Min.Y + 1e-3f &&
            a.Min.Z < b.Max.Z - 1e-3f && a.Max.Z > b.Min.Z + 1e-3f;

        private static string Name(DecorInfo info) => info.Name.ToLowerInvariant();
    }
}
