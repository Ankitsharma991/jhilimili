using System.Collections.Generic;

namespace UtsavNights.Core.Avatar
{
    /// <summary>
    /// Layering (PRD 8): each slot holds one garment except accessories (up to <see cref="MaxAccessories"/>). Equipping
    /// a garment takes off whatever shares a slot with it, so an abaya replaces both the top and the bottom, and a sari
    /// replaces the bottom and the mid-layer drape. An avatar is always dressed: <see cref="Repair"/> adds a top and a
    /// bottom when nothing covers the torso or legs.
    /// </summary>
    public static class OutfitRules
    {
        public const int MaxAccessories = 3;
        public const string DefaultTop = "kurta";
        public const string DefaultBottom = "churidar";

        /// <summary>Top that goes with skirts and saris when one is needed.</summary>
        public const string BlouseTop = "choli";

        /// <summary>Puts on a garment. Returns false (and changes nothing) for an unknown id.</summary>
        public static bool Equip(AvatarProfile profile, WardrobeCatalog catalog, string id, int variant = 0)
        {
            GarmentInfo garment = catalog.Find(id);
            if (garment == null)
            {
                return false;
            }

            variant = garment.Variants <= 0 ? 0 : (variant % garment.Variants + garment.Variants) % garment.Variants;
            for (int i = profile.outfit.Count - 1; i >= 0; i--)
            {
                EquippedItem item = profile.outfit[i];
                if (item.id == id)
                {
                    item.variant = variant;
                    return true;
                }
            }

            if (garment.Slots == WardrobeSlot.Accessory)
            {
                int accessories = 0;
                foreach (EquippedItem item in profile.outfit)
                {
                    if (catalog.Find(item.id)?.Slots == WardrobeSlot.Accessory)
                    {
                        accessories++;
                    }
                }

                if (accessories >= MaxAccessories)
                {
                    RemoveFirst(profile, catalog, WardrobeSlot.Accessory);
                }
            }
            else
            {
                profile.outfit.RemoveAll(item => (catalog.Find(item.id)?.Slots & garment.Slots) != 0);
            }

            profile.outfit.Add(new EquippedItem(id, variant));
            return true;
        }

        /// <summary>Takes a garment off; the next <see cref="Repair"/> re-dresses the torso or legs if needed.</summary>
        public static bool Unequip(AvatarProfile profile, string id) => profile.outfit.RemoveAll(item => item.id == id) > 0;

        public static GarmentInfo InSlot(AvatarProfile profile, WardrobeCatalog catalog, WardrobeSlot slot)
        {
            foreach (EquippedItem item in profile.outfit)
            {
                GarmentInfo g = catalog.Find(item.id);
                if (g != null && (g.Slots & slot) != 0)
                {
                    return g;
                }
            }

            return null;
        }

        /// <summary>Body regions covered by everything worn: the skin mask for the body mesh.</summary>
        public static BodyRegion Covered(AvatarProfile profile, WardrobeCatalog catalog)
        {
            BodyRegion covered = BodyRegion.None;
            foreach (EquippedItem item in profile.outfit)
            {
                covered |= catalog.Find(item.id)?.Covers ?? BodyRegion.None;
            }

            return covered;
        }

        public static bool HairHidden(AvatarProfile profile, WardrobeCatalog catalog) =>
            InSlot(profile, catalog, WardrobeSlot.Headwear)?.HidesHair == true;

        /// <summary>Drops unknown and duplicate items, resolves slot clashes (first worn wins) and dresses the body.</summary>
        public static void Repair(AvatarProfile profile, WardrobeCatalog catalog)
        {
            var kept = new List<EquippedItem>();
            WardrobeSlot used = WardrobeSlot.None;
            int accessories = 0;
            var seen = new HashSet<string>();
            foreach (EquippedItem item in profile.outfit)
            {
                GarmentInfo g = catalog.Find(item?.id);
                if (g == null || !seen.Add(g.Id))
                {
                    continue;
                }

                if (g.Slots == WardrobeSlot.Accessory)
                {
                    if (accessories++ >= MaxAccessories)
                    {
                        continue;
                    }
                }
                else if ((used & g.Slots) != 0)
                {
                    continue;
                }

                used |= g.Slots == WardrobeSlot.Accessory ? WardrobeSlot.None : g.Slots;
                item.variant = g.Variants <= 0 ? 0 : (item.variant % g.Variants + g.Variants) % g.Variants;
                kept.Add(item);
            }

            profile.outfit = kept;
            if ((used & WardrobeSlot.BaseBottom) == 0)
            {
                profile.outfit.Add(new EquippedItem(DefaultBottom));
                used |= WardrobeSlot.BaseBottom;
            }

            if ((used & WardrobeSlot.BaseTop) == 0)
            {
                GarmentInfo bottom = InSlot(profile, catalog, WardrobeSlot.BaseBottom);
                bool skirt = bottom != null && (bottom.Id.StartsWith("sari") || bottom.Id == "lehenga" || bottom.Id == "long_skirt");
                profile.outfit.Add(new EquippedItem(skirt ? BlouseTop : DefaultTop));
            }
        }

        private static void RemoveFirst(AvatarProfile profile, WardrobeCatalog catalog, WardrobeSlot slot)
        {
            int index = profile.outfit.FindIndex(item => catalog.Find(item.id)?.Slots == slot);
            if (index >= 0)
            {
                profile.outfit.RemoveAt(index);
            }
        }
    }
}
