using System;
using System.Collections.Generic;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Core.Home
{
    /// <summary>
    /// Where an attachment point is: its origin (modules are built around it), the opening it fills (doors and windows;
    /// local to the origin, x across, y up from the sill) and the volumes a module may occupy (house space). Volumes of
    /// different points never overlap, so no choice of modules can collide.
    /// </summary>
    public sealed class PointFrame
    {
        public PointFrame(string pointId, V3 origin, float openingWidth, float openingHeight, params Box3[] volumes)
        {
            PointId = pointId;
            Origin = origin;
            OpeningWidth = openingWidth;
            OpeningHeight = openingHeight;
            Volumes = volumes;
        }

        public string PointId { get; }

        public V3 Origin { get; }

        /// <summary>Zero for points that are not openings (balcony, roof, boundary, gate).</summary>
        public float OpeningWidth { get; }

        public float OpeningHeight { get; }

        public Box3[] Volumes { get; }

        public bool IsOpening => OpeningWidth > 0f;

        /// <summary>The opening in house space.</summary>
        public Rect2 Opening => new Rect2(Origin.X - OpeningWidth / 2f, Origin.Y, Origin.X + OpeningWidth / 2f, Origin.Y + OpeningHeight);
    }

    /// <summary>
    /// The courtyard house in house space (metres): the door at the origin, the front in the plane z = 0 facing +z (the
    /// courtyard), x to the left as seen from the courtyard. Two storeys, 7.6 m wide and 6 m deep; the courtyard boundary
    /// walls run from the front corners to the back wall at z = 6 with the gate in it. In the Main scene the house sits
    /// at world z = <see cref="WorldFrontZ"/>, behind the fireworks camera.
    /// </summary>
    public static class HouseGeometry
    {
        public const float WorldFrontZ = -2.4f;
        public const float HalfWidth = 3.8f;
        public const float Depth = 6f;
        public const float Plinth = 0.3f;
        public const float UpperFloor = 3.3f;
        public const float Eaves = 6.3f;
        public const float WallThickness = 0.25f;

        public const float BoundaryHeight = 1.5f;
        public const float BoundaryCentre = 3.6f;
        public const float BoundaryThickness = 0.35f;
        public const float BackWallZ = 6f;
        public const float GateX = 1.4f;

        /// <summary>The glazed door behind the balcony. The shell owns it, so the balcony point can stay empty.</summary>
        public static readonly Rect2 UpperDoor = new Rect2(-0.6f, UpperFloor, 0.6f, 5.5f);

        public static readonly PointFrame[] Frames =
        {
            new PointFrame("door", new V3(0f, Plinth, 0f), 1.2f, 2.2f, new Box3(-0.75f, Plinth, -0.3f, 0.75f, 2.85f, 0.7f)),
            new PointFrame("window_ground_left", new V3(2.2f, 1.0f, 0f), 1.1f, 1.3f, new Box3(1.1f, 0.75f, -0.3f, 3.3f, 2.65f, 0.4f)),
            new PointFrame("window_ground_right", new V3(-2.2f, 1.0f, 0f), 1.1f, 1.3f, new Box3(-3.3f, 0.75f, -0.3f, -1.1f, 2.65f, 0.4f)),
            new PointFrame("window_upper_left", new V3(2.2f, 3.9f, 0f), 1.1f, 1.4f, new Box3(1.1f, 3.65f, -0.3f, 3.3f, 5.65f, 0.4f)),
            new PointFrame("window_upper_right", new V3(-2.2f, 3.9f, 0f), 1.1f, 1.4f, new Box3(-3.3f, 3.65f, -0.3f, -1.1f, 5.65f, 0.4f)),
            new PointFrame("balcony", new V3(0f, 3.15f, 0f), 0f, 0f, new Box3(-1.05f, 2.95f, 0f, 1.05f, 6.0f, 1.0f)),
            new PointFrame("roof", new V3(0f, Eaves, 0f), 0f, 0f, new Box3(-4.05f, Eaves, -6.45f, 4.05f, 8.5f, 0.45f)),
            new PointFrame("boundary", new V3(0f, 0f, 0f), 0f, 0f,
                new Box3(3.4f, 0f, 0f, 3.8f, 1.7f, 6.2f), new Box3(-3.8f, 0f, 0f, -3.4f, 1.7f, 6.2f),
                new Box3(-3.8f, 0f, 5.8f, GateX - 0.61f, 1.7f, 6.2f), new Box3(GateX + 0.61f, 0f, 5.8f, 3.8f, 1.7f, 6.2f)),
            new PointFrame("gate", new V3(GateX, 0f, BackWallZ), 0f, 0f, new Box3(GateX - 0.61f, 0f, 5.6f, GateX + 0.61f, 2.6f, 6.4f)),
        };

        /// <summary>Solid parts of the shell that stand out of the front wall; no module volume may enter them.</summary>
        public static readonly Box3[] ShellSolids =
        {
            new Box3(-3.4f, 0f, -0.02f, 3.4f, Plinth, 0.1f), // plinth
            new Box3(-0.85f, 0f, 0.1f, 0.85f, 0.15f, 0.5f), // front step
            new Box3(-HalfWidth, 3.15f, 0f, -1.05f, UpperFloor, 0.1f), // floor band
            new Box3(1.05f, 3.15f, 0f, HalfWidth, UpperFloor, 0.1f),
            new Box3(-HalfWidth - 0.05f, 6.12f, 0f, HalfWidth + 0.05f, Eaves, 0.12f), // cornice under the roof
        };

        /// <summary>
        /// The Main scene's own courtyard fittings, in house space: the low step along the back wall, the lamp post, the
        /// bamboo poles that carry the string lights, and the avatar's spot. Decorations keep out of them.
        /// </summary>
        public static readonly Box3[] CourtyardFixtures =
        {
            new Box3(-3f, 0f, 4.65f, 3f, CourtyardStepTop, 5.75f),
            new Box3(-2.0f, 0f, 5.5f, -1.8f, 2.4f, 5.7f),
            new Box3(-3.38f, 0f, 5.67f, -3.32f, 2.4f, 5.73f),
            new Box3(2.27f, 0f, 5.67f, 2.33f, 2.35f, 5.73f),
            new Box3(3.32f, 0f, 5.67f, 3.38f, 2.45f, 5.73f),

            // Where the player's avatar stands in the courtyard (CourtyardAvatar.Spot, world (-0.7, 0, 2.12)).
            new Box3(-0.95f, 0f, 4.27f, -0.45f, 1.8f, 4.77f),
        };

        public const float CourtyardStepTop = 0.08f;

        /// <summary>The gate's leaves across its opening (house space). They swing open for the walking avatar.</summary>
        public static Box3 GateLeaves => new Box3(GateX - 0.39f, 0f, BackWallZ - 0.2f, GateX + 0.39f, 2.2f, BackWallZ + 0.2f);

        public static PointFrame Frame(string pointId) => Array.Find(Frames, f => f.PointId == pointId);

        /// <summary>The openings cut in the front wall: every door and window point, and the upper door.</summary>
        public static List<Rect2> FrontOpenings()
        {
            var list = new List<Rect2>();
            foreach (PointFrame f in Frames)
            {
                if (f.IsOpening)
                {
                    list.Add(f.Opening);
                }
            }

            list.Add(UpperDoor);
            return list;
        }

        /// <summary>House space to Main-scene world space (no rotation).</summary>
        public static V3 ToWorld(V3 house) => new V3(house.X, house.Y, house.Z + WorldFrontZ);
    }

    /// <summary>Colours for each <see cref="HousePaint"/> (linear-ish sRGB, as the materials take them).</summary>
    public static class HousePalette
    {
        /// <summary>Module colour variants: natural wood, teal, maroon, deep green.</summary>
        public static readonly Rgb[] Accents =
        {
            new Rgb(0.36f, 0.2f, 0.1f), new Rgb(0.05f, 0.4f, 0.42f), new Rgb(0.45f, 0.08f, 0.1f), new Rgb(0.1f, 0.32f, 0.16f),
        };

        public static readonly Rgb Wood = new Rgb(0.34f, 0.21f, 0.12f);
        public static readonly Rgb Iron = new Rgb(0.07f, 0.07f, 0.08f);
        public static readonly Rgb Glass = new Rgb(1f, 0.6f, 0.26f);
        public static readonly Rgb RoofTile = new Rgb(0.6f, 0.26f, 0.16f);
        public static readonly Rgb Stone = new Rgb(0.6f, 0.49f, 0.38f);
        public static readonly Rgb Brick = new Rgb(0.62f, 0.3f, 0.2f);
        public static readonly Rgb Dark = new Rgb(0.03f, 0.025f, 0.03f);

        /// <summary>The colour of a paint on a piece: the shell (module null) or a module with its variant.</summary>
        public static Rgb Resolve(HousePaint paint, HouseDesign design, ModuleChoice module)
        {
            switch (paint)
            {
                case HousePaint.Wall:
                    return HomeCatalog.WallColours[Clamp(design.wallColour, HomeCatalog.WallColours.Length)];
                case HousePaint.Trim:
                    return HomeCatalog.TrimColours[Clamp(design.trimColour, HomeCatalog.TrimColours.Length)];
                case HousePaint.Accent:
                    return Accents[Clamp(module?.variant ?? 0, Accents.Length)];
                case HousePaint.Wood:
                    return Wood;
                case HousePaint.Iron:
                    return Iron;
                case HousePaint.Glass:
                    return Glass;
                case HousePaint.RoofTile:
                    return RoofTile;
                case HousePaint.Stone:
                    return Stone;
                case HousePaint.Brick:
                    return Brick;
                default:
                    return Dark;
            }
        }

        public static readonly Rgb[] ClayColours = { new Rgb(0.56f, 0.24f, 0.13f), new Rgb(0.62f, 0.1f, 0.08f), new Rgb(0.82f, 0.56f, 0.16f) };
        public static readonly Rgb[] BrassColours = { new Rgb(0.8f, 0.6f, 0.22f), new Rgb(0.74f, 0.4f, 0.24f) };
        public static readonly Rgb[] PaperColours =
        {
            new Rgb(1f, 0.55f, 0.12f), new Rgb(0.92f, 0.2f, 0.55f), new Rgb(0.9f, 0.14f, 0.12f), new Rgb(1f, 0.82f, 0.2f), new Rgb(0.3f, 0.8f, 0.35f),
        };

        public static readonly Rgb[] FabricColours = { new Rgb(0.78f, 0.12f, 0.12f), new Rgb(0.9f, 0.55f, 0.1f), new Rgb(0.2f, 0.3f, 0.72f), new Rgb(0.1f, 0.5f, 0.3f) };
        public static readonly Rgb[] CaneColours = { new Rgb(0.78f, 0.62f, 0.36f), new Rgb(0.45f, 0.3f, 0.16f) };
        public static readonly Rgb[] JuteColours = { new Rgb(0.72f, 0.6f, 0.42f), new Rgb(0.85f, 0.2f, 0.2f) };
        public static readonly Rgb[] FurnitureWood = { new Rgb(0.44f, 0.27f, 0.14f), new Rgb(0.22f, 0.13f, 0.08f), new Rgb(0.08f, 0.4f, 0.42f) };
        public static readonly Rgb Flame = new Rgb(1f, 0.68f, 0.22f);
        public static readonly Rgb Leaf = new Rgb(0.15f, 0.42f, 0.12f);
        public static readonly Rgb MarigoldOrange = new Rgb(1f, 0.48f, 0.04f);
        public static readonly Rgb MarigoldYellow = new Rgb(1f, 0.76f, 0.08f);
        public static readonly Rgb WarmBulb = new Rgb(1f, 0.76f, 0.42f);

        /// <summary>Rangoli powders: four colours per variant.</summary>
        public static readonly Rgb[][] Powders =
        {
            new[] { new Rgb(0.86f, 0.1f, 0.15f), new Rgb(1f, 0.78f, 0.1f), new Rgb(0.95f, 0.95f, 0.9f), new Rgb(0.1f, 0.6f, 0.25f) },
            new[] { new Rgb(0.86f, 0.15f, 0.55f), new Rgb(1f, 0.5f, 0.05f), new Rgb(0.15f, 0.35f, 0.86f), new Rgb(0.95f, 0.95f, 0.9f) },
            new[] { new Rgb(1f, 0.6f, 0.1f), new Rgb(1f, 0.5f, 0.6f), new Rgb(0.1f, 0.7f, 0.7f), new Rgb(1f, 0.85f, 0.15f) },
            new[] { new Rgb(0.5f, 0.2f, 0.75f), new Rgb(0.6f, 0.86f, 0.15f), new Rgb(0.86f, 0.1f, 0.15f), new Rgb(0.95f, 0.95f, 0.9f) },
        };

        /// <summary>Paints that glow: lit glass, flames, paper lanterns and bulbs.</summary>
        public static bool IsEmissive(HousePaint paint) =>
            paint == HousePaint.Glass || paint == HousePaint.Flame || paint == HousePaint.Paper ||
            paint == HousePaint.BulbA || paint == HousePaint.BulbB || paint == HousePaint.BulbC;

        /// <summary>The colour of a paint on a decoration item in a colour variant.</summary>
        public static Rgb Decor(HousePaint paint, string itemId, int variant)
        {
            int v = Math.Max(0, variant);
            switch (paint)
            {
                case HousePaint.Clay:
                    return ClayColours[v % ClayColours.Length];
                case HousePaint.Brass:
                    return BrassColours[v % BrassColours.Length];
                case HousePaint.Flame:
                    return Flame;
                case HousePaint.Leaf:
                    return Leaf;
                case HousePaint.Marigold:
                    return v % 2 == 1 ? MarigoldYellow : MarigoldOrange;
                case HousePaint.MarigoldAlt:
                    return v % 2 == 1 ? MarigoldOrange : MarigoldYellow;
                case HousePaint.Paper:
                    return PaperColours[v % PaperColours.Length];
                case HousePaint.BulbA:
                case HousePaint.BulbB:
                case HousePaint.BulbC:
                    if (itemId == "string_lights_colour")
                    {
                        return paint == HousePaint.BulbA ? new Rgb(1f, 0.2f, 0.25f) : paint == HousePaint.BulbB ? new Rgb(0.3f, 1f, 0.35f) : new Rgb(0.35f, 0.5f, 1f);
                    }

                    return itemId == "light_curtain" && v % 2 == 1 ? new Rgb(0.78f, 0.86f, 1f) : WarmBulb;
                case HousePaint.Fabric:
                    return itemId == "chair_cane" ? CaneColours[v % CaneColours.Length] :
                        itemId == "charpai" ? JuteColours[v % JuteColours.Length] : FabricColours[v % FabricColours.Length];
                case HousePaint.Wood:
                    return FurnitureWood[v % FurnitureWood.Length];
                case HousePaint.Powder1:
                case HousePaint.Powder2:
                case HousePaint.Powder3:
                case HousePaint.Powder4:
                    return Powders[v % Powders.Length][paint - HousePaint.Powder1];
                case HousePaint.Glass:
                    return WarmBulb;
                case HousePaint.Iron:
                    return itemId == "lamp_post_small" && v % 2 == 1 ? new Rgb(0.09f, 0.24f, 0.14f) : Iron;
                case HousePaint.Stone:
                    return Stone;
                default:
                    return Dark;
            }
        }

        private static int Clamp(int v, int count) => Math.Max(0, Math.Min(count - 1, v));
    }
}
