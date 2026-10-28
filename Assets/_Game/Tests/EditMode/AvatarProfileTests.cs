using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Tests
{
    /// <summary>Phase 6 step 1: the avatar and wardrobe rules from PRD 8.</summary>
    public class AvatarProfileTests
    {
        private static WardrobeCatalog Catalog => WardrobeCatalog.Default;

        private static string[] Worn(AvatarProfile p) => p.outfit.Select(i => i.id).OrderBy(s => s).ToArray();

        [Test]
        public void Catalogue_MeetsTheDeliverable_TwentyGarmentsAcrossFiveTags()
        {
            Assert.GreaterOrEqual(Catalog.All.Count, 20);
            foreach (Occasion tag in new[] { Occasion.Everyday, Occasion.Festive, Occasion.Formal, Occasion.Wedding, Occasion.Street })
            {
                Assert.GreaterOrEqual(Catalog.All.Count(g => (g.Occasion & tag) != 0), 4, $"{tag} has a real choice");
            }

            foreach (string prdItem in new[] { "daura", "suruwal", "dhaka_topi", "sari_nivi", "lehenga", "kurta", "sherwani", "abaya", "thobe", "blazer", "jeans" })
            {
                Assert.IsNotNull(Catalog.Find(prdItem), $"{prdItem} from PRD 8");
            }
        }

        [Test]
        public void EveryGarment_HasASlotATagAndVariants()
        {
            foreach (GarmentInfo g in Catalog.All)
            {
                Assert.AreNotEqual(WardrobeSlot.None, g.Slots, g.Id);
                Assert.AreNotEqual(Occasion.None, g.Occasion, g.Id);
                Assert.AreNotEqual(Inspiration.None, g.Inspiration, g.Id);
                Assert.GreaterOrEqual(g.Variants, 3, $"{g.Id} colour variants");
                Assert.AreEqual(g.Id.ToLowerInvariant(), g.Id, "ids are lowercase");
            }

            Assert.AreEqual(Catalog.All.Count, Catalog.All.Select(g => g.Id).Distinct().Count(), "ids are unique");
        }

        [Test]
        public void NoGenderOrRegionAnywhere_EveryItemIsOpenToEveryBody()
        {
            string[] banned = { "gender", "sex", "male", "female", "region", "country", "nation", "religion" };
            foreach (Type type in new[] { typeof(AvatarProfile), typeof(GarmentInfo), typeof(EquippedItem) })
            {
                foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    Assert.IsFalse(banned.Any(b => member.Name.ToLowerInvariant().Contains(b)), $"{type.Name}.{member.Name}");
                }
            }

            // Every garment can be worn by every body style.
            foreach (float body in new[] { 0f, 1f, 2f })
            {
                foreach (GarmentInfo g in Catalog.All)
                {
                    var p = new AvatarProfile { bodyStyle = body };
                    Assert.IsTrue(OutfitRules.Equip(p, Catalog, g.Id));
                    p.Sanitize(Catalog);
                    CollectionAssert.Contains(Worn(p), g.Id, $"{g.Id} on body {body}");
                }
            }
        }

        [Test]
        public void Equip_ReplacesWhateverSharesASlot()
        {
            var p = new AvatarProfile();
            OutfitRules.Equip(p, Catalog, "kurta");
            OutfitRules.Equip(p, Catalog, "churidar");
            OutfitRules.Equip(p, Catalog, "nehru_jacket");
            OutfitRules.Equip(p, Catalog, "abaya");
            CollectionAssert.AreEquivalent(new[] { "abaya", "nehru_jacket" }, Worn(p), "a full-length robe replaces top and bottom");

            OutfitRules.Equip(p, Catalog, "sari_nivi");
            CollectionAssert.DoesNotContain(Worn(p), "abaya");
            CollectionAssert.DoesNotContain(Worn(p), "nehru_jacket", "the sari's drape takes the mid layer");
        }

        [Test]
        public void Accessories_StackUpToThree()
        {
            var p = new AvatarProfile();
            foreach (string id in new[] { "bangles", "necklace", "earrings", "watch" })
            {
                OutfitRules.Equip(p, Catalog, id);
            }

            CollectionAssert.AreEquivalent(new[] { "necklace", "earrings", "watch" }, Worn(p), "the oldest makes room");
        }

        [Test]
        public void Equip_WrapsTheVariantAndRejectsUnknownIds()
        {
            var p = new AvatarProfile();
            Assert.IsFalse(OutfitRules.Equip(p, Catalog, "space_suit"));
            OutfitRules.Equip(p, Catalog, "jeans", 7);
            Assert.AreEqual(7 % Catalog.Find("jeans").Variants, p.outfit.Single().variant);
            OutfitRules.Equip(p, Catalog, "jeans", -1);
            Assert.AreEqual(Catalog.Find("jeans").Variants - 1, p.outfit.Single().variant);
        }

        [Test]
        public void Repair_AlwaysDressesTheBody()
        {
            var bare = new AvatarProfile();
            bare.Sanitize(Catalog);
            CollectionAssert.AreEquivalent(new[] { OutfitRules.DefaultBottom, OutfitRules.DefaultTop }, Worn(bare));

            var sari = new AvatarProfile();
            OutfitRules.Equip(sari, Catalog, "sari_seedha");
            sari.Sanitize(Catalog);
            CollectionAssert.Contains(Worn(sari), OutfitRules.BlouseTop, "a sari gets a blouse, not a kurta");
        }

        [Test]
        public void Sanitize_RepairsAnEditedOrCorruptSave()
        {
            var p = new AvatarProfile
            {
                bodyStyle = 9f, shoulders = -4f, skinTone = float.NaN, hairStyle = 99, glasses = -3, height = (HeightPreset)7,
                name = "  Asha\n<script>   ",
            };
            p.outfit.Add(null);
            p.outfit.Add(new EquippedItem("jeans"));
            p.outfit.Add(new EquippedItem("trousers"));
            p.outfit.Add(new EquippedItem("unknown"));
            p.outfit.Add(new EquippedItem("jeans"));
            Assert.IsTrue(p.Sanitize(Catalog));
            Assert.AreEqual(2f, p.bodyStyle);
            Assert.AreEqual(-1f, p.shoulders);
            Assert.AreEqual(0.5f, p.skinTone);
            Assert.AreEqual(AvatarOptions.HairStyles - 1, p.hairStyle);
            Assert.AreEqual(0, p.glasses);
            Assert.AreEqual(HeightPreset.Tall, p.height);
            Assert.AreEqual("Ashascript", p.name);
            CollectionAssert.AreEquivalent(new[] { "jeans", OutfitRules.DefaultTop }, Worn(p), "first bottom wins; duplicates and unknowns go");
            Assert.IsFalse(p.Sanitize(Catalog), "sanitising twice changes nothing");
        }

        [TestCase("आशा", "आशा")]
        [TestCase("Rōshan K", "Rōshan K")]
        [TestCase("O'Neil-Shah", "O'Neil-Shah")]
        [TestCase("averyveryverylongname123", "averyveryverylongnam")]
        [TestCase("   ", "")]
        public void Names_KeepLettersInAnyScriptAndStayShort(string raw, string expected)
        {
            Assert.AreEqual(expected, AvatarProfile.CleanName(raw));
        }

        [Test]
        public void SkinTone_IsContinuousFromLightToDeep()
        {
            float previous = float.MaxValue;
            for (int i = 0; i <= 100; i++)
            {
                var c = SkinTones.Evaluate(i / 100f);
                float luma = 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;
                Assert.Less(luma, previous + 1e-4f, "darkens steadily");
                previous = luma;
            }

            Assert.GreaterOrEqual(SkinTones.AnchorCount, 10);
        }

        [Test]
        public void Presets_AreValidVariedAndCovered()
        {
            Assert.GreaterOrEqual(AvatarPresets.All.Count, 10);
            foreach (AvatarProfile p in AvatarPresets.All)
            {
                Assert.IsFalse(p.Clone().Sanitize(Catalog), "presets need no repair");
                Assert.IsTrue(p.created);
            }

            Assert.AreEqual(3, AvatarPresets.All.Select(p => p.height).Distinct().Count());
            Assert.Greater(AvatarPresets.All.Max(p => p.skinTone) - AvatarPresets.All.Min(p => p.skinTone), 0.8f, "light to deep");
            foreach (Inspiration i in new[] { Inspiration.SouthAsian, Inspiration.Nepali, Inspiration.WestAsian, Inspiration.Western })
            {
                Assert.IsTrue(AvatarPresets.All.Any(p => p.outfit.Any(e => (Catalog.Find(e.id).Inspiration & i) != 0)), $"{i} in the presets");
            }
        }

        [Test]
        public void Headscarves_HideHairAndTheirRegionsAreMasked()
        {
            var p = new AvatarProfile();
            OutfitRules.Equip(p, Catalog, "hijab");
            p.Sanitize(Catalog);
            Assert.IsTrue(OutfitRules.HairHidden(p, Catalog));
            Assert.IsTrue((OutfitRules.Covered(p, Catalog) & BodyRegion.Neck) != 0);
            OutfitRules.Equip(p, Catalog, "dhaka_topi");
            Assert.IsFalse(OutfitRules.HairHidden(p, Catalog), "a topi sits on top of the hair");
        }

        [Test]
        public void Clone_IsIndependent()
        {
            AvatarProfile a = AvatarPresets.Default;
            AvatarProfile b = a.Clone();
            OutfitRules.Equip(b, Catalog, "cap");
            Assert.AreNotEqual(Worn(a).Length, Worn(b).Length);
        }
    }
}
