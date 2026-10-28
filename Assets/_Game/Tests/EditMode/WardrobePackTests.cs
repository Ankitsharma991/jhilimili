using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Tests
{
    /// <summary>Phase 6 step 7: the wardrobe pack format (feeds Phase 9) enforces PRD 8.</summary>
    public class WardrobePackTests
    {
        private static WardrobePackManifest Good() => new WardrobePackManifest
        {
            id = "festive_two", version = 1, title = "Festive two", licence = "own-009",
            garments = new[]
            {
                new PackGarment
                {
                    id = "angrakha", name = "Angrakha", slots = new[] { "BaseTop" }, covers = new[] { "Chest", "Belly", "UpperArms", "LowerArms" },
                    occasion = new[] { "Festive", "Wedding" }, inspiration = new[] { "SouthAsian" }, variants = 4, mesh = "generator:kurta",
                    vertices = 1800, culturalReview = "2026-11-02, reviewer AB",
                },
            },
        };

        [Test]
        public void AValidPack_Passes_AndBecomesCatalogueEntries()
        {
            CollectionAssert.IsEmpty(WardrobePacks.Validate(Good(), WardrobeCatalog.Default.All.Select(g => g.Id)));
            GarmentInfo g = WardrobePacks.ToGarments(Good()).Single();
            Assert.AreEqual(WardrobeSlot.BaseTop, g.Slots);
            Assert.AreEqual(BodyRegion.Torso | BodyRegion.Arms, g.Covers);
            Assert.AreEqual(Occasion.Festive | Occasion.Wedding, g.Occasion);
        }

        [Test]
        public void TheBuiltInWardrobe_IsAValidPack()
        {
            WardrobePackManifest pack = WardrobePacks.FromCatalog(WardrobeCatalog.Default, "builtin", "own-007");
            foreach (PackGarment g in pack.garments)
            {
                g.culturalReview = "pending";
            }

            CollectionAssert.IsEmpty(WardrobePacks.Validate(pack, null));
            Assert.AreEqual(WardrobeCatalog.Default.All.Count, WardrobePacks.ToGarments(pack).Count);
            var roundTrip = WardrobePacks.ToGarments(pack).ToDictionary(g => g.Id);
            foreach (GarmentInfo g in WardrobeCatalog.Default.All)
            {
                Assert.AreEqual(g.Slots, roundTrip[g.Id].Slots, g.Id);
                Assert.AreEqual(g.Covers, roundTrip[g.Id].Covers, g.Id);
                Assert.AreEqual(g.Occasion, roundTrip[g.Id].Occasion, g.Id);
            }
        }

        [TestCase("Women's kurta", "women's")]
        [TestCase("Kurta (men only)", "men")]
        public void GenderLockedNames_AreRejected(string name, string word)
        {
            WardrobePackManifest pack = Good();
            pack.garments[0].name = name;
            Assert.IsTrue(WardrobePacks.Validate(pack, null).Any(e => e.Contains("every body")));
        }

        [Test]
        public void MissingOrUnknownFields_AreRejected()
        {
            WardrobePackManifest pack = Good();
            PackGarment g = pack.garments[0];
            g.slots = new[] { "Pocket" };
            g.occasion = new string[0];
            g.mesh = "kurta.fbx";
            g.vertices = 12000;
            g.culturalReview = "";
            pack.licence = "";
            var errors = WardrobePacks.Validate(pack, null);
            foreach (string expected in new[] { "unknown slot", "occasion tag", "mesh must be", "vertices", "cultural review", "licence" })
            {
                Assert.IsTrue(errors.Any(e => e.Contains(expected)), expected + "\n" + string.Join("\n", errors));
            }
        }

        [Test]
        public void IdsMustBeNewAndWellFormed()
        {
            WardrobePackManifest pack = Good();
            pack.garments[0].id = "kurta";
            Assert.IsTrue(WardrobePacks.Validate(pack, WardrobeCatalog.Default.All.Select(g => g.Id)).Any(e => e.Contains("already used")));
            pack.garments[0].id = "Bad Id";
            Assert.IsTrue(WardrobePacks.Validate(pack, null).Any(e => e.Contains("lowercase")));
        }
    }
}
