using System;
using System.Collections.Generic;
using UtsavNights.Core.Art;

namespace UtsavNights.Core.Home
{
    /// <summary>What a facade module is (PRD 9: doors, windows, balconies, roofs, boundary walls, gates).</summary>
    public enum ModuleType
    {
        Door,
        Window,
        Balcony,
        Roof,
        Boundary,
        Gate,
    }

    public sealed class ModuleInfo
    {
        public ModuleInfo(string id, string name, ModuleType type, int variants)
        {
            Id = id;
            Name = name;
            Type = type;
            Variants = variants;
        }

        public string Id { get; }

        public string Name { get; }

        public ModuleType Type { get; }

        /// <summary>Colour variants of the module itself (the frame, the shutters).</summary>
        public int Variants { get; }
    }

    /// <summary>A place on the house where one module of a type fits. Optional points can stay empty.</summary>
    public sealed class AttachmentPoint
    {
        public AttachmentPoint(string id, ModuleType type, string defaultModule, bool optional = false)
        {
            Id = id;
            Type = type;
            DefaultModule = defaultModule;
            Optional = optional;
        }

        public string Id { get; }

        public ModuleType Type { get; }

        public string DefaultModule { get; }

        public bool Optional { get; }
    }

    public enum DecorCategory
    {
        Lamps,
        Lights,
        Garlands,
        Plants,
        Furniture,
        Rangoli,
    }

    /// <summary>Festivals an item belongs to, for browsing (PRD 9.1: browse by category and festival).</summary>
    [Flags]
    public enum Festival
    {
        None = 0,
        Everyday = 1,
        Diwali = 2,
        Tihar = 4,
        Wedding = 8,
    }

    /// <summary>How an item is placed (PRD 9): free with snapping, drawn between two points, or stamped on the floor.</summary>
    public enum PlacementMode
    {
        Free,
        Path,
        Decal,
    }

    /// <summary>Surfaces an item may stand on, hang from or be drawn along.</summary>
    [Flags]
    public enum Surface
    {
        None = 0,
        Ground = 1,
        Step = 2,
        Wall = 4,
        Railing = 8,
        Roof = 16,
    }

    public sealed class DecorInfo
    {
        public DecorInfo(string id, string name, DecorCategory category, Festival festivals, PlacementMode mode, Surface surfaces,
            float width, float depth, float height, int cost, int variants, float maxScale = 1.3f)
        {
            Id = id;
            Name = name;
            Category = category;
            Festivals = festivals;
            Mode = mode;
            Surfaces = surfaces;
            Width = width;
            Depth = depth;
            Height = height;
            Cost = cost;
            Variants = variants;
            MaxScale = maxScale;
        }

        public string Id { get; }

        public string Name { get; }

        public DecorCategory Category { get; }

        public Festival Festivals { get; }

        public PlacementMode Mode { get; }

        public Surface Surfaces { get; }

        /// <summary>Footprint in metres at scale 1 (path items: the strand's thickness).</summary>
        public float Width { get; }

        public float Depth { get; }

        public float Height { get; }

        /// <summary>Budget units (PRD 9.1: object budgets per category and in total, by quality tier).</summary>
        public int Cost { get; }

        public int Variants { get; }

        /// <summary>PRD 9.1: limited scale. The minimum is always <see cref="HomeCatalog.MinScale"/>.</summary>
        public float MaxScale { get; }
    }

    /// <summary>
    /// The house kit, facade modules, colours and the decoration catalogue (Phase 7 step 1). Shapes are generated in steps 2
    /// and 3 from these ids. Decoration items need the cultural review listed in PLAN.md before release.
    /// </summary>
    public static class HomeCatalog
    {
        public const string KitId = "courtyard_house";
        public const float MinScale = 0.8f;

        public static readonly AttachmentPoint[] Points =
        {
            new AttachmentPoint("door", ModuleType.Door, "door_wooden_panel"),
            new AttachmentPoint("window_ground_left", ModuleType.Window, "window_shuttered"),
            new AttachmentPoint("window_ground_right", ModuleType.Window, "window_shuttered"),
            new AttachmentPoint("window_upper_left", ModuleType.Window, "window_arched"),
            new AttachmentPoint("window_upper_right", ModuleType.Window, "window_arched"),
            new AttachmentPoint("balcony", ModuleType.Balcony, "balcony_iron_rail", optional: true),
            new AttachmentPoint("roof", ModuleType.Roof, "roof_flat_parapet"),
            new AttachmentPoint("boundary", ModuleType.Boundary, "boundary_plaster"),
            new AttachmentPoint("gate", ModuleType.Gate, "gate_iron"),
        };

        public static readonly ModuleInfo[] Modules =
        {
            new ModuleInfo("door_wooden_panel", "Panelled wooden door", ModuleType.Door, 4),
            new ModuleInfo("door_carved_arch", "Carved arched door", ModuleType.Door, 3),
            new ModuleInfo("door_double_teal", "Painted double door", ModuleType.Door, 4),
            new ModuleInfo("door_steel_modern", "Modern steel door", ModuleType.Door, 3),
            new ModuleInfo("window_shuttered", "Shuttered window", ModuleType.Window, 4),
            new ModuleInfo("window_arched", "Arched window", ModuleType.Window, 3),
            new ModuleInfo("window_jali", "Jali lattice window", ModuleType.Window, 3),
            new ModuleInfo("window_plain", "Plain glass window", ModuleType.Window, 3),
            new ModuleInfo("balcony_iron_rail", "Iron railing balcony", ModuleType.Balcony, 3),
            new ModuleInfo("balcony_jharokha", "Jharokha (covered balcony)", ModuleType.Balcony, 3),
            new ModuleInfo("balcony_wood_lattice", "Carved wooden balcony", ModuleType.Balcony, 3),
            new ModuleInfo("roof_flat_parapet", "Flat roof with parapet", ModuleType.Roof, 3),
            new ModuleInfo("roof_tiled_slope", "Sloping tiled roof", ModuleType.Roof, 3),
            new ModuleInfo("roof_terrace_canopy", "Roof terrace with canopy", ModuleType.Roof, 3),
            new ModuleInfo("boundary_plaster", "Plastered wall", ModuleType.Boundary, 1),
            new ModuleInfo("boundary_brick", "Exposed brick wall", ModuleType.Boundary, 1),
            new ModuleInfo("boundary_grill", "Low wall with grille", ModuleType.Boundary, 2),
            new ModuleInfo("gate_iron", "Iron gate", ModuleType.Gate, 3),
            new ModuleInfo("gate_wooden", "Wooden gate", ModuleType.Gate, 3),
            new ModuleInfo("gate_arch", "Arched gateway", ModuleType.Gate, 3),
        };

        /// <summary>
        /// Wall colours: warm lime (the courtyard's plaster before Phase 7), ochre, terracotta, sky blue (the blue houses of
        /// Jodhpur), mint, pink, saffron, grey and lime white.
        /// </summary>
        public static readonly Rgb[] WallColours =
        {
            new Rgb(0.74f, 0.54f, 0.4f), new Rgb(0.82f, 0.62f, 0.32f), new Rgb(0.68f, 0.36f, 0.24f), new Rgb(0.42f, 0.6f, 0.8f),
            new Rgb(0.6f, 0.78f, 0.66f), new Rgb(0.88f, 0.62f, 0.66f), new Rgb(0.95f, 0.66f, 0.28f), new Rgb(0.6f, 0.6f, 0.62f),
            new Rgb(0.92f, 0.89f, 0.82f),
        };

        /// <summary>Trim (door and window frames, cornices): white, deep green, maroon, teal, indigo, gold.</summary>
        public static readonly Rgb[] TrimColours =
        {
            new Rgb(0.95f, 0.95f, 0.92f), new Rgb(0.1f, 0.35f, 0.22f), new Rgb(0.45f, 0.08f, 0.1f), new Rgb(0.05f, 0.42f, 0.45f),
            new Rgb(0.16f, 0.12f, 0.4f), new Rgb(0.82f, 0.62f, 0.2f),
        };

        public static readonly string[] Materials = { "Plaster", "Brick", "Stone", "Wood" };

        public static readonly DecorInfo[] Decorations =
        {
            // Lamps
            new DecorInfo("diya_clay", "Clay diya", DecorCategory.Lamps, Festival.Diwali | Festival.Tihar, PlacementMode.Free,
                Surface.Ground | Surface.Step | Surface.Railing | Surface.Roof, 0.1f, 0.1f, 0.06f, 1, 3, 1.5f),
            new DecorInfo("diya_brass_stand", "Brass lamp stand", DecorCategory.Lamps, Festival.Diwali | Festival.Tihar | Festival.Wedding,
                PlacementMode.Free, Surface.Ground | Surface.Step, 0.22f, 0.22f, 0.7f, 3, 2),
            new DecorInfo("lantern_akash_kandil", "Star lantern (akash kandil)", DecorCategory.Lamps, Festival.Diwali, PlacementMode.Free,
                Surface.Wall | Surface.Railing, 0.45f, 0.28f, 0.6f, 4, 4),
            new DecorInfo("lantern_paper_round", "Round paper lantern", DecorCategory.Lamps, Festival.Diwali | Festival.Tihar | Festival.Wedding,
                PlacementMode.Free, Surface.Wall | Surface.Railing, 0.32f, 0.32f, 0.4f, 3, 5),
            new DecorInfo("lamp_post_small", "Garden lamp", DecorCategory.Lamps, Festival.Everyday, PlacementMode.Free,
                Surface.Ground, 0.25f, 0.25f, 1.2f, 4, 2),

            // Lights (drawn between two points)
            new DecorInfo("string_lights_warm", "Warm string lights", DecorCategory.Lights, Festival.Diwali | Festival.Tihar | Festival.Wedding | Festival.Everyday,
                PlacementMode.Path, Surface.Wall | Surface.Railing | Surface.Roof, 0.03f, 0.03f, 0.03f, 4, 1),
            new DecorInfo("string_lights_colour", "Coloured string lights", DecorCategory.Lights, Festival.Diwali | Festival.Tihar | Festival.Wedding,
                PlacementMode.Path, Surface.Wall | Surface.Railing | Surface.Roof, 0.03f, 0.03f, 0.03f, 4, 1),
            new DecorInfo("light_curtain", "Light curtain", DecorCategory.Lights, Festival.Diwali | Festival.Wedding, PlacementMode.Path,
                Surface.Wall | Surface.Railing, 0.03f, 0.03f, 1.2f, 6, 2),

            // Garlands (drawn between two points)
            new DecorInfo("toran_mango", "Mango-leaf toran", DecorCategory.Garlands, Festival.Diwali | Festival.Tihar | Festival.Wedding | Festival.Everyday,
                PlacementMode.Path, Surface.Wall, 0.08f, 0.04f, 0.15f, 3, 1),
            new DecorInfo("garland_marigold", "Marigold garland", DecorCategory.Garlands, Festival.Diwali | Festival.Tihar | Festival.Wedding,
                PlacementMode.Path, Surface.Wall | Surface.Railing, 0.07f, 0.07f, 0.07f, 3, 2),
            new DecorInfo("garland_marigold_strands", "Hanging marigold strands", DecorCategory.Garlands, Festival.Diwali | Festival.Tihar | Festival.Wedding,
                PlacementMode.Path, Surface.Wall | Surface.Railing, 0.07f, 0.07f, 0.8f, 5, 2),

            // Plants
            new DecorInfo("tulsi_planter", "Tulsi planter", DecorCategory.Plants, Festival.Everyday | Festival.Diwali, PlacementMode.Free,
                Surface.Ground | Surface.Step | Surface.Roof, 0.35f, 0.35f, 0.8f, 3, 2),
            new DecorInfo("pot_marigold", "Marigold pot", DecorCategory.Plants, Festival.Everyday | Festival.Diwali | Festival.Tihar, PlacementMode.Free,
                Surface.Ground | Surface.Step | Surface.Railing | Surface.Roof, 0.25f, 0.25f, 0.35f, 2, 2),
            new DecorInfo("pot_palm", "Potted palm", DecorCategory.Plants, Festival.Everyday, PlacementMode.Free,
                Surface.Ground | Surface.Roof, 0.4f, 0.4f, 1.3f, 3, 1),

            // Furniture
            new DecorInfo("bench_wooden", "Wooden bench", DecorCategory.Furniture, Festival.Everyday, PlacementMode.Free,
                Surface.Ground | Surface.Roof, 1.2f, 0.4f, 0.45f, 4, 3),
            new DecorInfo("chair_cane", "Cane chair", DecorCategory.Furniture, Festival.Everyday, PlacementMode.Free,
                Surface.Ground | Surface.Roof, 0.55f, 0.55f, 0.8f, 3, 2),
            new DecorInfo("charpai", "Charpai (woven cot)", DecorCategory.Furniture, Festival.Everyday, PlacementMode.Free,
                Surface.Ground | Surface.Roof, 1.8f, 0.9f, 0.45f, 6, 2, 1.1f),

            // Rangoli (stamped templates on the floor)
            new DecorInfo("rangoli_lotus", "Lotus rangoli", DecorCategory.Rangoli, Festival.Diwali | Festival.Everyday, PlacementMode.Decal,
                Surface.Ground | Surface.Step, 1.0f, 1.0f, 0f, 2, 4, 1.6f),
            new DecorInfo("rangoli_peacock", "Peacock rangoli", DecorCategory.Rangoli, Festival.Diwali, PlacementMode.Decal,
                Surface.Ground | Surface.Step, 1.2f, 1.2f, 0f, 2, 3, 1.6f),
            new DecorInfo("rangoli_geometric", "Geometric rangoli", DecorCategory.Rangoli, Festival.Diwali | Festival.Wedding | Festival.Everyday,
                PlacementMode.Decal, Surface.Ground | Surface.Step, 0.9f, 0.9f, 0f, 2, 4, 1.6f),
            new DecorInfo("rangoli_diya_ring", "Diya-ring rangoli", DecorCategory.Rangoli, Festival.Diwali | Festival.Tihar, PlacementMode.Decal,
                Surface.Ground | Surface.Step, 1.0f, 1.0f, 0f, 2, 3, 1.6f),
            new DecorInfo("rangoli_mandala_tihar", "Mandala floor art (Tihar)", DecorCategory.Rangoli, Festival.Tihar, PlacementMode.Decal,
                Surface.Ground | Surface.Step, 1.1f, 1.1f, 0f, 2, 3, 1.6f),
        };

        private static readonly Dictionary<string, ModuleInfo> ModuleById = Index(Modules, m => m.Id);
        private static readonly Dictionary<string, DecorInfo> DecorById = Index(Decorations, d => d.Id);

        public static ModuleInfo Module(string id) => id != null && ModuleById.TryGetValue(id, out ModuleInfo m) ? m : null;

        public static DecorInfo Decoration(string id) => id != null && DecorById.TryGetValue(id, out DecorInfo d) ? d : null;

        public static AttachmentPoint Point(string id) => Array.Find(Points, p => p.Id == id);

        private static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key)
        {
            var d = new Dictionary<string, T>();
            foreach (T item in items)
            {
                d.Add(key(item), item);
            }

            return d;
        }
    }
}
