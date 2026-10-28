using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Avatar
{
    /// <summary>
    /// Where a garment sits (PRD 8: base, mid, outer, headwear, footwear, accessories). The base layer is split into
    /// top and bottom; full-length garments (abaya, thobe) take both.
    /// </summary>
    [Flags]
    public enum WardrobeSlot
    {
        None = 0,
        BaseTop = 1,
        BaseBottom = 2,
        Mid = 4,
        Outer = 8,
        Headwear = 16,
        Footwear = 32,

        /// <summary>Jewellery and small items; up to <see cref="OutfitRules.MaxAccessories"/> at once.</summary>
        Accessory = 64,
    }

    /// <summary>Body regions a garment covers: the skin there is hidden (skin mask), so nothing pokes through.</summary>
    [Flags]
    public enum BodyRegion
    {
        None = 0,
        Chest = 1,
        Belly = 2,
        UpperArms = 4,
        LowerArms = 8,
        Hips = 16,
        UpperLegs = 32,
        LowerLegs = 64,
        Feet = 128,
        Hair = 256,
        Neck = 512,
        Torso = Chest | Belly,
        Arms = UpperArms | LowerArms,
        Legs = UpperLegs | LowerLegs,
    }

    /// <summary>When people wear it. Five tags (the PRD's "5 style tags").</summary>
    [Flags]
    public enum Occasion
    {
        None = 0,
        Everyday = 1,
        Festive = 2,
        Formal = 4,
        Wedding = 8,
        Street = 16,
    }

    /// <summary>Where the style comes from. Never a lock: every garment is open to every avatar.</summary>
    [Flags]
    public enum Inspiration
    {
        None = 0,
        SouthAsian = 1,
        Nepali = 2,
        WestAsian = 4,
        Western = 8,
    }

    /// <summary>One wardrobe item. The visual is generated from <see cref="Id"/> (Phase 6 step 3).</summary>
    public sealed class GarmentInfo
    {
        public GarmentInfo(string id, string name, WardrobeSlot slots, BodyRegion covers, Occasion occasion, Inspiration inspiration,
            int variants, bool hidesHair = false)
        {
            Id = id;
            Name = name;
            Slots = slots;
            Covers = covers;
            Occasion = occasion;
            Inspiration = inspiration;
            Variants = variants;
            HidesHair = hidesHair;
        }

        public string Id { get; }

        public string Name { get; }

        public WardrobeSlot Slots { get; }

        public BodyRegion Covers { get; }

        public Occasion Occasion { get; }

        public Inspiration Inspiration { get; }

        /// <summary>Colour or draping variants (0 to Variants - 1).</summary>
        public int Variants { get; }

        /// <summary>Turbans, hijabs and headscarves hide the hairstyle.</summary>
        public bool HidesHair { get; }
    }

    public sealed class WardrobeCatalog
    {
        private readonly Dictionary<string, GarmentInfo> _byId = new Dictionary<string, GarmentInfo>();
        private readonly List<GarmentInfo> _all = new List<GarmentInfo>();

        public WardrobeCatalog(IEnumerable<GarmentInfo> garments)
        {
            foreach (GarmentInfo g in garments)
            {
                _byId.Add(g.Id, g);
                _all.Add(g);
            }
        }

        public IReadOnlyList<GarmentInfo> All => _all;

        public GarmentInfo Find(string id) => id != null && _byId.TryGetValue(id, out GarmentInfo g) ? g : null;

        /// <summary>
        /// The v1 wardrobe: 38 garments across the five occasion tags. Researched starting points from PRD 8 (Daura-Suruwal,
        /// Dhaka topi, sari draping styles, lehenga, kurta sets, sherwani, Western casual and formal, abaya, thobe); none is
        /// presented as a single national dress. Needs the cultural review listed in PLAN.md before release.
        /// </summary>
        public static readonly WardrobeCatalog Default = new WardrobeCatalog(new[]
        {
            // Base tops
            new GarmentInfo("tshirt", "T-shirt", WardrobeSlot.BaseTop, BodyRegion.Torso | BodyRegion.UpperArms, Occasion.Everyday | Occasion.Street, Inspiration.Western, 6),
            new GarmentInfo("shirt", "Shirt", WardrobeSlot.BaseTop, BodyRegion.Torso | BodyRegion.Arms, Occasion.Formal | Occasion.Everyday, Inspiration.Western, 5),
            new GarmentInfo("kurta", "Kurta", WardrobeSlot.BaseTop, BodyRegion.Torso | BodyRegion.Arms | BodyRegion.Hips | BodyRegion.UpperLegs, Occasion.Festive | Occasion.Everyday, Inspiration.SouthAsian, 6),
            new GarmentInfo("short_kurta", "Short kurta", WardrobeSlot.BaseTop, BodyRegion.Torso | BodyRegion.UpperArms | BodyRegion.Hips, Occasion.Everyday | Occasion.Street, Inspiration.SouthAsian, 6),
            new GarmentInfo("choli", "Choli blouse", WardrobeSlot.BaseTop, BodyRegion.Chest | BodyRegion.UpperArms, Occasion.Festive | Occasion.Wedding, Inspiration.SouthAsian, 5),
            new GarmentInfo("daura", "Daura", WardrobeSlot.BaseTop, BodyRegion.Torso | BodyRegion.Arms | BodyRegion.Hips | BodyRegion.UpperLegs, Occasion.Festive | Occasion.Formal, Inspiration.Nepali, 4),

            // Base bottoms
            new GarmentInfo("jeans", "Jeans", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.Legs, Occasion.Everyday | Occasion.Street, Inspiration.Western, 3),
            new GarmentInfo("trousers", "Trousers", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.Legs, Occasion.Formal | Occasion.Everyday, Inspiration.Western, 4),
            new GarmentInfo("churidar", "Churidar", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.Legs, Occasion.Festive | Occasion.Everyday, Inspiration.SouthAsian, 5),
            new GarmentInfo("salwar", "Salwar", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.Legs, Occasion.Everyday | Occasion.Festive, Inspiration.SouthAsian, 5),
            new GarmentInfo("suruwal", "Suruwal", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.Legs, Occasion.Festive | Occasion.Formal, Inspiration.Nepali, 3),
            new GarmentInfo("dhoti", "Dhoti", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.UpperLegs, Occasion.Festive | Occasion.Wedding, Inspiration.SouthAsian, 3),
            new GarmentInfo("lehenga", "Lehenga skirt", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.Legs, Occasion.Festive | Occasion.Wedding, Inspiration.SouthAsian, 6),
            new GarmentInfo("long_skirt", "Long skirt", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.Legs, Occasion.Everyday | Occasion.Festive, Inspiration.Western | Inspiration.SouthAsian, 5),
            new GarmentInfo("shorts", "Shorts", WardrobeSlot.BaseBottom, BodyRegion.Hips | BodyRegion.UpperLegs, Occasion.Street, Inspiration.Western, 4),

            // Full length (top and bottom)
            new GarmentInfo("sari_nivi", "Sari (pleated front)", WardrobeSlot.BaseBottom | WardrobeSlot.Mid, BodyRegion.Hips | BodyRegion.Legs, Occasion.Festive | Occasion.Wedding | Occasion.Everyday, Inspiration.SouthAsian, 6),
            new GarmentInfo("sari_seedha", "Sari (front pallu)", WardrobeSlot.BaseBottom | WardrobeSlot.Mid, BodyRegion.Hips | BodyRegion.Legs, Occasion.Festive | Occasion.Wedding, Inspiration.SouthAsian, 6),
            new GarmentInfo("abaya", "Abaya", WardrobeSlot.BaseTop | WardrobeSlot.BaseBottom, BodyRegion.Torso | BodyRegion.Arms | BodyRegion.Hips | BodyRegion.Legs, Occasion.Everyday | Occasion.Formal, Inspiration.WestAsian, 4),
            new GarmentInfo("thobe", "Thobe", WardrobeSlot.BaseTop | WardrobeSlot.BaseBottom, BodyRegion.Torso | BodyRegion.Arms | BodyRegion.Hips | BodyRegion.Legs, Occasion.Everyday | Occasion.Festive | Occasion.Formal, Inspiration.WestAsian, 4),

            // Mid
            new GarmentInfo("nehru_jacket", "Nehru jacket", WardrobeSlot.Mid, BodyRegion.Torso, Occasion.Festive | Occasion.Formal, Inspiration.SouthAsian, 5),
            new GarmentInfo("hoodie", "Hoodie", WardrobeSlot.Mid, BodyRegion.Torso | BodyRegion.Arms, Occasion.Street | Occasion.Everyday, Inspiration.Western, 5),
            new GarmentInfo("dupatta", "Dupatta", WardrobeSlot.Mid, BodyRegion.None, Occasion.Festive | Occasion.Everyday | Occasion.Wedding, Inspiration.SouthAsian, 6),

            // Outer
            new GarmentInfo("blazer", "Blazer", WardrobeSlot.Outer, BodyRegion.Torso | BodyRegion.Arms, Occasion.Formal | Occasion.Wedding, Inspiration.Western, 4),
            new GarmentInfo("sherwani", "Sherwani", WardrobeSlot.Outer, BodyRegion.Torso | BodyRegion.Arms | BodyRegion.Hips | BodyRegion.UpperLegs, Occasion.Wedding | Occasion.Festive | Occasion.Formal, Inspiration.SouthAsian, 5),
            new GarmentInfo("shawl", "Shawl", WardrobeSlot.Outer, BodyRegion.None, Occasion.Everyday | Occasion.Festive, Inspiration.SouthAsian | Inspiration.Nepali, 5),

            // Headwear
            new GarmentInfo("dhaka_topi", "Dhaka topi", WardrobeSlot.Headwear, BodyRegion.None, Occasion.Festive | Occasion.Everyday | Occasion.Formal, Inspiration.Nepali, 3),
            new GarmentInfo("turban", "Turban", WardrobeSlot.Headwear, BodyRegion.Hair, Occasion.Everyday | Occasion.Festive | Occasion.Wedding, Inspiration.SouthAsian, 6, hidesHair: true),
            new GarmentInfo("hijab", "Hijab", WardrobeSlot.Headwear, BodyRegion.Hair | BodyRegion.Neck, Occasion.Everyday | Occasion.Formal | Occasion.Festive, Inspiration.WestAsian | Inspiration.SouthAsian, 6, hidesHair: true),
            new GarmentInfo("ghutra", "Headscarf with agal", WardrobeSlot.Headwear, BodyRegion.Hair | BodyRegion.Neck, Occasion.Everyday | Occasion.Formal, Inspiration.WestAsian, 3, hidesHair: true),
            new GarmentInfo("cap", "Cap", WardrobeSlot.Headwear, BodyRegion.None, Occasion.Street, Inspiration.Western, 5),

            // Footwear
            new GarmentInfo("sneakers", "Sneakers", WardrobeSlot.Footwear, BodyRegion.Feet, Occasion.Street | Occasion.Everyday, Inspiration.Western, 5),
            new GarmentInfo("juttis", "Juttis", WardrobeSlot.Footwear, BodyRegion.Feet, Occasion.Festive | Occasion.Wedding, Inspiration.SouthAsian, 4),
            new GarmentInfo("chappals", "Chappals", WardrobeSlot.Footwear, BodyRegion.None, Occasion.Everyday, Inspiration.SouthAsian | Inspiration.Nepali, 4),
            new GarmentInfo("formal_shoes", "Formal shoes", WardrobeSlot.Footwear, BodyRegion.Feet, Occasion.Formal | Occasion.Wedding, Inspiration.Western, 3),

            // Accessories
            new GarmentInfo("bangles", "Bangles", WardrobeSlot.Accessory, BodyRegion.None, Occasion.Festive | Occasion.Wedding, Inspiration.SouthAsian, 4),
            new GarmentInfo("necklace", "Necklace", WardrobeSlot.Accessory, BodyRegion.None, Occasion.Festive | Occasion.Wedding | Occasion.Formal, Inspiration.SouthAsian, 4),
            new GarmentInfo("earrings", "Earrings", WardrobeSlot.Accessory, BodyRegion.None, Occasion.Festive | Occasion.Everyday, Inspiration.SouthAsian, 4),
            new GarmentInfo("watch", "Watch", WardrobeSlot.Accessory, BodyRegion.None, Occasion.Everyday | Occasion.Formal, Inspiration.Western, 3),
        });
    }
}
