using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Analytics;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Tests
{
    /// <summary>Phase 11 step 2 (F25): rooms, placing decorations inside with plain reasons, budgets, and the save format.</summary>
    public class InteriorTests
    {
        private static RoomDef Main => Rooms.Find(Rooms.Main);

        private static string Try(string item, Surface surface, V3 point, V3 normal, List<PlacedDecor> items = null, int tier = 1)
        {
            PlacedDecor pose = InteriorPlacement.PoseFor(Main, HomeCatalog.Decoration(item), new RoomHit(surface, point, normal));
            pose.uid = 99;
            return InteriorPlacement.Problem(Main, items ?? new List<PlacedDecor>(), pose, tier);
        }

        [Test]
        public void Rooms_AreTwo_WithinTheLowTiersBudget_WhenEmpty()
        {
            Assert.AreEqual(2, Rooms.All.Length);
            foreach (RoomDef room in Rooms.All)
            {
                Assert.IsTrue(AnalyticsSchema.IsSafeId(room.Id), room.Id);
                int tris = InteriorPlacement.Triangles(room, new PlacedDecor[0]);
                Assert.Less(tris, InteriorBudget.Triangles[0] / 4, room.Id + " leaves room for decorations");
                Assert.IsTrue(System.Array.Exists(room.Furniture, f => f.TopTakesItems), room.Id + " has somewhere for a diya");
            }

            Assert.IsNull(Rooms.Find("cellar"));
        }

        [Test]
        public void RoomColours_FollowTheHouse_PalerInside()
        {
            HouseDesign a = HouseDesign.Default();
            HouseDesign b = HouseDesign.Default();
            b.wallColour = (a.wallColour + 1) % HomeCatalog.WallColours.Length;
            Assert.AreNotEqual(InteriorGenerator.Colour(HousePaint.Wall, a), InteriorGenerator.Colour(HousePaint.Wall, b));
            Assert.Greater(InteriorGenerator.Colour(HousePaint.Wall, a).R, HomeCatalog.WallColours[a.wallColour].R - 1e-3f);
        }

        [Test]
        public void Placing_OnTops_Floors_AndWalls()
        {
            Assert.IsNull(Try("diya_clay", Surface.Step, new V3(0f, 0.42f, 2.25f), new V3(0, 1, 0)), "a diya on the low table");
            Assert.IsNull(Try("pot_marigold", Surface.Ground, new V3(1.6f, 0f, 1.5f), new V3(0, 1, 0)), "a pot on the floor");
            Assert.IsNull(Try("lantern_akash_kandil", Surface.Wall, new V3(-1f, 1.9f, Main.Depth), new V3(0, 0, -1)), "a lantern on the back wall");
            Assert.IsNull(Try("lantern_paper_round", Surface.Wall, new V3(-Main.HalfWidth, 1.8f, 2.4f), new V3(1, 0, 0)), "a lantern on the left wall");
            Assert.IsNotNull(Try("rangoli_lotus", Surface.Ground, new V3(0f, 0f, 2.2f), new V3(0, 1, 0)), "rangoli under the table is in the way");
            Assert.IsNull(Try("rangoli_lotus", Surface.Ground, new V3(-1f, 0f, 3.3f), new V3(0, 1, 0)), "rangoli on open floor");
        }

        [Test]
        public void Reasons_AreNamedPlainly()
        {
            StringAssert.Contains("doorway", Try("pot_marigold", Surface.Ground, new V3(0f, 0f, 0.5f), new V3(0, 1, 0)));
            StringAssert.Contains("low table", Try("pot_marigold", Surface.Ground, new V3(0f, 0f, 2.2f), new V3(0, 1, 0)));
            StringAssert.Contains("stays outside", Try("lamp_post_small", Surface.Ground, new V3(1.6f, 0f, 1.5f), new V3(0, 1, 0)));
            StringAssert.Contains("wall", Try("pot_marigold", Surface.Ground, new V3(Main.HalfWidth - 0.05f, 0f, 2f), new V3(0, 1, 0)));
            StringAssert.Contains("corner", Try("lantern_akash_kandil", Surface.Wall, new V3(Main.HalfWidth - 0.1f, 1.9f, Main.Depth), new V3(0, 0, -1)));
            StringAssert.Contains("lower", Try("lantern_akash_kandil", Surface.Wall, new V3(-1f, 2.85f, Main.Depth), new V3(0, 0, -1)));
            StringAssert.Contains("too big", Try("bench_wooden", Surface.Step, new V3(0f, 0.42f, 2.25f), new V3(0, 1, 0)));
            StringAssert.Contains("doesn't hang", Try("pot_palm", Surface.Wall, new V3(-1f, 1.5f, Main.Depth), new V3(0, 0, -1)));
            StringAssert.Contains("edge", Try("diya_brass_stand", Surface.Step, new V3(0.45f, 0.42f, 2.25f), new V3(0, 1, 0)));
            var pot = new PlacedDecor { uid = 1, item = "pot_marigold", room = Rooms.Main, x = 1.6f, y = 0f, z = 1.5f, yaw = 180f, scale = 1f };
            StringAssert.Contains("marigold pot", Try("pot_marigold", Surface.Ground, new V3(1.65f, 0f, 1.55f), new V3(0, 1, 0), new List<PlacedDecor> { pot }));
        }

        [Test]
        public void Budget_FillsByTier()
        {
            var items = new List<PlacedDecor>();
            int cost = DecorBudget.CostOf(HomeCatalog.Decoration("pot_marigold"));
            for (int i = 0; items.Count * cost + cost <= InteriorBudget.Units[0]; i++)
            {
                items.Add(new PlacedDecor { uid = i + 1, item = "pot_marigold", room = Rooms.Main, x = -2f + (i % 8) * 0.5f, z = 1.0f + i / 8 * 0.4f, yaw = 180f, scale = 1f });
            }

            StringAssert.Contains("full", Try("diya_clay", Surface.Step, new V3(0f, 0.42f, 2.25f), new V3(0, 1, 0), items, tier: 0));
            Assert.IsNull(Try("diya_clay", Surface.Step, new V3(0f, 0.42f, 2.25f), new V3(0, 1, 0), items, tier: 2), "a high-tier phone has room");
        }

        [Test]
        public void SaveFormat_KeepsTheRoom_AndOlderLinesReadAsOutdoors()
        {
            var inside = new PlacedDecor { uid = 7, item = "diya_clay", room = Rooms.Upper, x = 1.2f, y = 0.5f, z = 1.5f, surface = Surface.Step };
            IHouseCommand back = HouseCommandCodec.Decode(HouseCommandCodec.Encode(new PlaceDecor(inside)));
            var design = HouseDesign.Default();
            Assert.IsTrue(back.Apply(design));
            Assert.AreEqual(Rooms.Upper, design.Find(7).room);

            var outside = new PlacedDecor { uid = 8, item = "diya_clay", x = 0.2f, y = 0.08f, z = 1f };
            string line = HouseCommandCodec.Encode(new PlaceDecor(outside));
            StringAssert.DoesNotContain("@", line, "outdoor items are written as before");
            Assert.IsTrue(HouseCommandCodec.Decode(line).Apply(design));
            Assert.IsFalse(design.Find(8).Indoors);

            Assert.AreEqual(1, design.InRoom(Rooms.Upper).Count);
            Assert.AreEqual(1, design.Outdoor().Count);
            Assert.AreEqual(DecorBudget.CostOf(HomeCatalog.Decoration("diya_clay")), DecorPlacement.UnitsUsed(design), "indoor items don't use the courtyard's budget");

            design.decorations.Add(new PlacedDecor { uid = 9, item = "diya_clay", room = "cellar" });
            design.Sanitize();
            Assert.IsNull(design.Find(9), "a room that doesn't exist loses its items");
            Assert.IsNotNull(design.Find(7));
            Assert.AreNotEqual(inside.Fingerprint(), outside.Fingerprint());
        }
    }
}
