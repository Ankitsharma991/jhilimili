using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Core.Packs
{
    /// <summary>
    /// The sample environment pack (Phase 9 step 1): a blue-washed lane in the spirit of Jodhpur, for proving the pack format,
    /// the linter and the manager end to end. Its cultural review is a placeholder until a reviewer signs it (the linter
    /// only checks that the fields are filled; the owner fills the real ones before publishing).
    /// </summary>
    public static class SamplePacks
    {
        public const string BlueCityPack = "env_blue_city_lane";
        public const string BlueCity = "blue_city_lane";

        public static EnvironmentDef BlueCityEnvironment() => new EnvironmentDef
        {
            id = BlueCity,
            name = "Blue city lane",
            region = "west_india",
            festivals = new[] { "Diwali" },
            description = "Blue-washed houses with jharokhas round a small chowk.",
            openKind = SpotKinds.Square,
            ground = new[] { 0.5f, 0.42f, 0.33f },
            wallColours = new[] { 3, 3, 3, 8, 0 },
            materials = new[] { 0, 0, 2 },
            roofs = new[] { "roof_flat_parapet", "roof_terrace_canopy" },
            roofWeights = new[] { 6, 4 },
            balconies = new[] { "balcony_jharokha", "balcony_iron_rail", string.Empty },
            balconyWeights = new[] { 6, 2, 2 },
            windows = new[] { "window_arched", "window_jali", "window_shuttered" },
            windowWeights = new[] { 4, 4, 2 },
            boundaries = new[] { "boundary_plaster", "boundary_grill" },
            boundaryWeights = new[] { 7, 3 },
        };

        public static SettlementLayout BlueCityLayout() =>
            SettlementLibrary.Compose(BlueCity, new[] { -2, -1, 1, 2 }, 3, new[] { -12f, 0f, 12f }, 32f, SpotKinds.Square, 25.6f, EnvironmentFeatures.Features.Platform);

        public static PackManifest BlueCityManifest() => new PackManifest
        {
            packId = BlueCityPack,
            version = "1.0.0",
            type = PackTypes.Environment,
            title = "Blue city lane",
            description = "A blue-washed lane with jharokhas round a small chowk.",
            tags = new PackTags { geo = new[] { "west_india" }, style = new[] { "old_city" }, festival = new[] { "diwali" } },
            budget = new PackBudget { maxTriangles = 400000, maxTextureMB = 1f, drawCalls = 140 },
            review = new PackReview { advisor = "placeholder (owner to fill before publishing)", date = "2026-10-29" },
        };
    }
}
