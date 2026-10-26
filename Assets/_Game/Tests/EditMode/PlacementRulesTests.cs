using NUnit.Framework;
using UtsavNights.Core.Placement;

namespace UtsavNights.Tests
{
    public class PlacementRulesTests
    {
        private const int Ground = 1;
        private const int Rooftop = 2;

        private static PlacementFacts Good() => new PlacementFacts
        {
            HitSurface = true,
            SurfaceFlags = Ground,
            AllowedSurfaces = Ground | Rooftop,
            SlopeDegrees = 2f,
            InReach = true,
            ItemsPlaced = 1,
            ItemLimit = 10,
            FootprintClear = true,
            NearestItemDistance = 2f,
            MinimumSpacing = 0.45f,
            EffectVolumeClear = true,
        };

        [Test]
        public void GoodSpot_IsValid_WithNoMessage()
        {
            PlacementFacts f = Good();
            Assert.AreEqual(PlacementBlock.None, PlacementRules.Evaluate(f));
            Assert.AreEqual(string.Empty, PlacementRules.Message(PlacementBlock.None));
        }

        [Test]
        public void EachRule_Blocks()
        {
            PlacementFacts f = Good(); f.HitSurface = false;
            Assert.AreEqual(PlacementBlock.NoSurface, PlacementRules.Evaluate(f));
            f = Good(); f.SurfaceFlags = 0;
            Assert.AreEqual(PlacementBlock.WrongSurface, PlacementRules.Evaluate(f), "unmarked surfaces (walls) are not ground");
            f = Good(); f.AllowedSurfaces = Rooftop;
            Assert.AreEqual(PlacementBlock.WrongSurface, PlacementRules.Evaluate(f));
            f = Good(); f.SlopeDegrees = 25f;
            Assert.AreEqual(PlacementBlock.TooSteep, PlacementRules.Evaluate(f));
            f = Good(); f.InReach = false;
            Assert.AreEqual(PlacementBlock.OutOfReach, PlacementRules.Evaluate(f));
            f = Good(); f.ItemsPlaced = 10;
            Assert.AreEqual(PlacementBlock.TooManyItems, PlacementRules.Evaluate(f));
            f = Good(); f.FootprintClear = false;
            Assert.AreEqual(PlacementBlock.FootprintBlocked, PlacementRules.Evaluate(f));
            f = Good(); f.NearestItemDistance = 0.2f;
            Assert.AreEqual(PlacementBlock.TooCloseToItem, PlacementRules.Evaluate(f));
            f = Good(); f.EffectVolumeClear = false;
            Assert.AreEqual(PlacementBlock.NoRoomForEffect, PlacementRules.Evaluate(f));
        }

        [Test]
        public void SlopeLimit_IsInclusive20Degrees()
        {
            PlacementFacts f = Good();
            f.SlopeDegrees = PlacementRules.MaxSlopeDegrees;
            Assert.AreEqual(PlacementBlock.None, PlacementRules.Evaluate(f));
        }

        [Test]
        public void ReasonsComeInUsefulOrder()
        {
            PlacementFacts f = Good();
            f.FootprintClear = false;
            f.ItemsPlaced = 99;
            Assert.AreEqual(PlacementBlock.TooManyItems, PlacementRules.Evaluate(f), "the limit is more useful than a blocked spot");
        }

        [Test]
        public void ZeroLimit_MeansUnlimited()
        {
            PlacementFacts f = Good();
            f.ItemLimit = 0;
            f.ItemsPlaced = 500;
            Assert.AreEqual(PlacementBlock.None, PlacementRules.Evaluate(f));
        }

        [Test]
        public void EveryBlock_HasAMessage_AndLimitsGrowWithTier()
        {
            foreach (PlacementBlock b in System.Enum.GetValues(typeof(PlacementBlock)))
            {
                if (b != PlacementBlock.None)
                {
                    Assert.IsNotEmpty(PlacementRules.Message(b), b.ToString());
                }
            }

            Assert.Less(PlacementRules.ItemLimit(0), PlacementRules.ItemLimit(1));
            Assert.Less(PlacementRules.ItemLimit(1), PlacementRules.ItemLimit(2));
        }
    }
}
