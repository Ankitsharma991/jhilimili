using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Placement;

namespace UtsavNights.Tests
{
    /// <summary>Phase 8 step 4: which spots are open and why not, aerial rules, pads, areas and strands overhead (F20).</summary>
    public class CelebrationRulesTests
    {
        [Test]
        public void DefaultHouse_OpensEverySpotInEveryEnvironment()
        {
            foreach (EnvironmentInfo env in Environments.All)
            {
                SettlementLayout layout = SettlementLibrary.For(env.Id);
                foreach (SpotDef spot in layout.spots)
                {
                    SpotAvailability a = CelebrationRules.Availability(layout, spot, HouseDesign.Default());
                    Assert.IsTrue(a.Open, $"{env.Id} {spot.id}: {a.Reason}");
                    Assert.AreEqual(string.Empty, a.Reason);
                }
            }
        }

        [Test]
        public void HouseChoices_GreyTheRooftopAndBalconyWithAReason()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            HouseDesign d = HouseDesign.Default();
            d.ModuleAt("roof").module = "roof_tiled_slope";
            d.ModuleAt("balcony").module = string.Empty;
            SpotAvailability roof = CelebrationRules.Availability(layout, layout.Spot("rooftop"), d);
            Assert.IsFalse(roof.Open);
            StringAssert.Contains("roof slopes", roof.Reason);
            SpotAvailability balcony = CelebrationRules.Availability(layout, layout.Spot("balcony"), d);
            Assert.IsFalse(balcony.Open);
            StringAssert.Contains("no balcony", balcony.Reason);

            d.ModuleAt("balcony").module = "balcony_jharokha";
            StringAssert.Contains("canopy", CelebrationRules.Availability(layout, layout.Spot("balcony"), d).Reason);
            Assert.IsTrue(CelebrationRules.Availability(layout, layout.Spot("courtyard"), d).Open, "the courtyard is always open");
            Assert.IsFalse(CelebrationRules.Availability(layout, null, d).Open);
        }

        [Test]
        public void SettlementTags_ComeFromTheLayout()
        {
            Assert.IsTrue(CelebrationRules.Tags(SettlementLibrary.For(Environments.TownLane), null).Contains(SpotTags.Square));
            Assert.IsFalse(CelebrationRules.Tags(SettlementLibrary.For(Environments.HillVillage), null).Contains(SpotTags.Square), "a threshing ground, not a square");
            Assert.IsTrue(CelebrationRules.Tags(SettlementLibrary.For(Environments.Riverside), null).Contains(SpotTags.OpenLot));
        }

        [Test]
        public void Aerials_FollowTheSpotsRule()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            SpotDef street = layout.Spot("street");
            Assert.AreEqual(AerialRule.Pads, CelebrationRules.RuleOf(street));
            Assert.AreEqual(AerialRule.None, CelebrationRules.RuleOf(layout.Spot("balcony")));
            Assert.AreEqual(AerialRule.Open, CelebrationRules.RuleOf(layout.Spot("open")));
            LaunchPad pad = street.pads[0];
            Assert.IsTrue(CelebrationRules.OnPad(street, pad.x + 0.2f, pad.z));
            Assert.IsFalse(CelebrationRules.OnPad(street, street.x - 1.5f, street.z));

            var facts = new PlacementFacts { HitSurface = true, SurfaceFlags = 1, AllowedSurfaces = 1, InReach = true, FootprintClear = true, EffectVolumeClear = true, NearestItemDistance = 9f, SkyClear = true, IsAerial = true };
            Assert.AreEqual(PlacementBlock.None, PlacementRules.Evaluate(facts));
            facts.AerialRule = AerialRule.Pads;
            Assert.AreEqual(PlacementBlock.AerialPadsOnly, PlacementRules.Evaluate(facts));
            facts.OnPad = true;
            Assert.AreEqual(PlacementBlock.None, PlacementRules.Evaluate(facts));
            facts.SkyClear = false;
            Assert.AreEqual(PlacementBlock.NoSky, PlacementRules.Evaluate(facts));
            facts.AerialRule = AerialRule.None;
            Assert.AreEqual(PlacementBlock.AerialNotHere, PlacementRules.Evaluate(facts));
            facts.IsAerial = false;
            Assert.AreEqual(PlacementBlock.None, PlacementRules.Evaluate(facts), "ground crackers ignore the aerial rule");
            facts.OutsideArea = true;
            Assert.AreEqual(PlacementBlock.OutsideArea, PlacementRules.Evaluate(facts));
            foreach (PlacementBlock b in new[] { PlacementBlock.OutsideArea, PlacementBlock.AerialNotHere, PlacementBlock.AerialPadsOnly, PlacementBlock.NoSky })
            {
                Assert.IsNotEmpty(PlacementRules.Message(b));
            }
        }

        [Test]
        public void PlayArea_IsACircleAtTheSpotsHeight()
        {
            SpotDef roof = SettlementLibrary.For(Environments.TownLane).Spot("rooftop");
            Assert.IsTrue(CelebrationRules.InArea(roof, new V3(roof.x + 1f, roof.y, roof.z)));
            Assert.IsFalse(CelebrationRules.InArea(roof, new V3(roof.x + 1f, 0f, roof.z)), "the courtyard below is not the rooftop");
            Assert.IsFalse(CelebrationRules.InArea(roof, new V3(roof.x + roof.radius + 0.1f, roof.y, roof.z)));
        }

        [Test]
        public void Strands_AboveAPointBlockAerials()
        {
            HouseDesign d = HouseDesign.Default();
            d.decorations.Add(new PlacedDecor { uid = 1, item = "string_lights_warm", x = 3.4f, y = 1.5f, z = 2f, ex = -3.4f, ey = 1.5f, ez = 2f, sag = 0.2f });
            Assert.IsTrue(CelebrationRules.StrandOverhead(d, new V3(0f, 0f, 2.1f)), "under the strand");
            Assert.IsFalse(CelebrationRules.StrandOverhead(d, new V3(0f, 0f, 3.5f)), "clear of it");
            Assert.IsFalse(CelebrationRules.StrandOverhead(HouseDesign.Default(), new V3(0f, 0f, 2f)));
        }
    }
}
