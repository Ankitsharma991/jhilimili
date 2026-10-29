using System;
using System.Collections.Generic;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>
    /// An environment (PRD 10 "environment pack"): a settlement layout plus its look: the house styles its neighbours are
    /// built from, the ground and the festival it suits. The three built-in environments ship in the app (offline); later
    /// packs (Phase 9) download with the same fields.
    /// </summary>
    public sealed class EnvironmentInfo
    {
        public EnvironmentInfo(string id, string name, string region, Festival festival, string description, float sizeMb,
            string openKind, Rgb ground, int[] wallColours, (string module, int weight)[] roofs, (string module, int weight)[] balconies,
            (string module, int weight)[] windows, (string module, int weight)[] boundaries, int[] materials)
        {
            Id = id;
            Name = name;
            Region = region;
            Festival = festival;
            Description = description;
            SizeMb = sizeMb;
            OpenKind = openKind;
            Ground = ground;
            WallColours = wallColours;
            Roofs = roofs;
            Balconies = balconies;
            Windows = windows;
            Boundaries = boundaries;
            Materials = materials;
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>Region tag for the browser's filter chips (north_india, nepal, east_india, ...).</summary>
        public string Region { get; }

        public Festival Festival { get; }

        public string Description { get; }

        /// <summary>Download size for a pack; built-in environments report their in-app share.</summary>
        public float SizeMb { get; }

        public bool BuiltIn => true;

        /// <summary>The open space across the street: square, village area or town space.</summary>
        public string OpenKind { get; }

        public Rgb Ground { get; }

        public int[] WallColours { get; }

        public (string module, int weight)[] Roofs { get; }

        public (string module, int weight)[] Balconies { get; }

        public (string module, int weight)[] Windows { get; }

        public (string module, int weight)[] Boundaries { get; }

        public int[] Materials { get; }
    }

    /// <summary>The built-in environments. Each needs the cultural review listed in Docs/Phase8 before release.</summary>
    public static class Environments
    {
        public const string TownLane = "town_lane";
        public const string HillVillage = "hill_village";
        public const string Riverside = "riverside";

        public static readonly EnvironmentInfo[] All =
        {
            new EnvironmentInfo(TownLane, "Town lane", "north_india", Festival.Diwali,
                "A lane of two-storey houses opening onto the chowk.", 0f, SpotKinds.Square, new Rgb(0.42f, 0.33f, 0.27f),
                new[] { 0, 1, 2, 3, 5, 6, 8 },
                new[] { ("roof_flat_parapet", 5), ("roof_terrace_canopy", 4), ("roof_tiled_slope", 1) },
                new[] { ("balcony_iron_rail", 4), ("balcony_jharokha", 3), ("balcony_wood_lattice", 1), (string.Empty, 2) },
                new[] { ("window_shuttered", 4), ("window_arched", 3), ("window_plain", 2), ("window_jali", 1) },
                new[] { ("boundary_plaster", 5), ("boundary_grill", 3), ("boundary_brick", 2) },
                new[] { 0, 0, 0, 1, 2 }),
            new EnvironmentInfo(HillVillage, "Hill village", "nepal", Festival.Tihar,
                "Brick and timber houses with sloping roofs around the village threshing ground.", 0f, SpotKinds.VillageArea,
                new Rgb(0.36f, 0.3f, 0.24f),
                new[] { 2, 8, 1, 0 },
                new[] { ("roof_tiled_slope", 7), ("roof_flat_parapet", 2), ("roof_terrace_canopy", 1) },
                new[] { ("balcony_wood_lattice", 6), (string.Empty, 3), ("balcony_iron_rail", 1) },
                new[] { ("window_jali", 5), ("window_shuttered", 4), ("window_arched", 1) },
                new[] { ("boundary_brick", 6), ("boundary_plaster", 3), ("boundary_grill", 1) },
                new[] { 1, 1, 3, 0 }),
            new EnvironmentInfo(Riverside, "Riverside ghat", "east_india", Festival.Diwali,
                "Bright houses along a lane above the ghat steps and the river.", 0f, SpotKinds.TownSpace, new Rgb(0.5f, 0.42f, 0.34f),
                new[] { 3, 5, 6, 8, 4 },
                new[] { ("roof_terrace_canopy", 5), ("roof_flat_parapet", 5) },
                new[] { ("balcony_jharokha", 4), ("balcony_iron_rail", 4), (string.Empty, 2) },
                new[] { ("window_arched", 4), ("window_shuttered", 3), ("window_jali", 2), ("window_plain", 1) },
                new[] { ("boundary_plaster", 6), ("boundary_grill", 4) },
                new[] { 0, 0, 2 }),
        };

        public static EnvironmentInfo Find(string id) => Array.Find(All, e => e.Id == id);
    }

    /// <summary>
    /// Neighbour houses: a seeded <see cref="HouseDesign"/> in an environment's style, with a few decorations for its
    /// festival. Every decoration passes the Phase 7 placement rules, so neighbours follow the same rules as the player.
    /// The same seed always gives the same house.
    /// </summary>
    public static class NeighbourDesigns
    {
        /// <summary>Decoration units a neighbour may use: enough to look festive, a small share of the scene's budget.</summary>
        public const int Units = 20;

        public static HouseDesign Generate(EnvironmentInfo env, int seed)
        {
            var rng = new Random(seed);
            HouseDesign d = HouseDesign.Default();
            d.wallColour = env.WallColours[rng.Next(env.WallColours.Length)];
            d.trimColour = rng.Next(HomeCatalog.TrimColours.Length);
            d.material = env.Materials[rng.Next(env.Materials.Length)];
            Choose(d, "roof", env.Roofs, rng);
            Choose(d, "balcony", env.Balconies, rng);
            string windows = Pick(env.Windows, rng);
            foreach (string p in new[] { "window_ground_left", "window_ground_right" })
            {
                Set(d, p, windows, rng);
            }

            string upper = Pick(env.Windows, rng);
            foreach (string p in new[] { "window_upper_left", "window_upper_right" })
            {
                Set(d, p, upper, rng);
            }

            Choose(d, "boundary", env.Boundaries, rng);
            ModuleInfo[] doors = Array.FindAll(HomeCatalog.Modules, m => m.Type == ModuleType.Door);
            Set(d, "door", doors[rng.Next(doors.Length)].Id, rng);
            ModuleInfo[] gates = Array.FindAll(HomeCatalog.Modules, m => m.Type == ModuleType.Gate);
            Set(d, "gate", gates[rng.Next(gates.Length)].Id, rng);
            Decorate(d, env, rng);
            d.Sanitize();
            return d;
        }

        private static void Decorate(HouseDesign d, EnvironmentInfo env, Random rng)
        {
            PlacementContext ctx = DecorPlacement.For(d, 2);
            bool tihar = (env.Festival & Festival.Tihar) != 0;
            int units = 0;

            void Try(PlacedDecor pose)
            {
                DecorInfo info = HomeCatalog.Decoration(pose.item);
                int cost = DecorBudget.CostOf(info, (new V3(pose.ex, pose.ey, pose.ez) - new V3(pose.x, pose.y, pose.z)).Length);
                pose.uid = d.nextUid;
                if (units + cost <= Units && DecorPlacement.Check(ctx, pose).Ok)
                {
                    d.decorations.Add(pose);
                    d.nextUid++;
                    units += cost;
                }
            }

            // A rangoli by the gate, diyas along the plinth, a toran over the door, lights under the cornice.
            string rangoli = tihar ? (rng.Next(2) == 0 ? "rangoli_mandala_tihar" : "rangoli_diya_ring") : (rng.Next(2) == 0 ? "rangoli_lotus" : "rangoli_geometric");
            Try(new PlacedDecor { item = rangoli, x = 0f, z = 2.2f, surface = Surface.Ground, scale = 1f, variant = rng.Next(4) });
            for (int i = 0; i < 3; i++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    Try(new PlacedDecor { item = "diya_clay", x = side * (1.2f + i * 0.5f), y = HouseGeometry.Plinth, z = 0.04f, surface = Surface.Step, scale = 1f, variant = rng.Next(3) });
                }
            }

            Try(new PlacedDecor { item = "toran_mango", x = -0.7f, y = 2.6f, ex = 0.7f, ey = 2.6f, sag = 0.1f, surface = Surface.Wall, scale = 1f });
            string lights = rng.Next(2) == 0 ? "string_lights_warm" : "string_lights_colour";
            Try(new PlacedDecor { item = lights, x = -2.9f, y = 6.05f, ex = 2.9f, ey = 6.05f, sag = 0.08f, surface = Surface.Wall, scale = 1f });
            if (tihar)
            {
                Try(new PlacedDecor { item = "garland_marigold", x = -1.6f, y = 2.4f, ex = -2.8f, ey = 2.4f, sag = 0.3f, surface = Surface.Wall, scale = 1f });
            }
            else
            {
                Try(new PlacedDecor { item = "lantern_akash_kandil", x = 2.2f, y = 2.4f, z = 0.14f, surface = Surface.Wall, scale = 1f, variant = rng.Next(4) });
            }
        }

        private static void Choose(HouseDesign d, string point, (string module, int weight)[] options, Random rng) => Set(d, point, Pick(options, rng), rng);

        private static void Set(HouseDesign d, string point, string module, Random rng)
        {
            ModuleChoice slot = d.ModuleAt(point);
            ModuleInfo info = HomeCatalog.Module(module);
            slot.module = module ?? string.Empty;
            slot.variant = info != null ? rng.Next(info.Variants) : 0;
        }

        private static string Pick((string module, int weight)[] options, Random rng)
        {
            int total = 0;
            foreach ((string _, int w) in options)
            {
                total += w;
            }

            int roll = rng.Next(total);
            foreach ((string module, int w) in options)
            {
                if (roll < w)
                {
                    return module;
                }

                roll -= w;
            }

            return options[0].module;
        }
    }
}
