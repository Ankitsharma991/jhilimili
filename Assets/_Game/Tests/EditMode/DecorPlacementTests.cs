using System;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Tests
{
    /// <summary>Phase 7 step 4: every placement rule, with its plain-language reason (PRD 9.1).</summary>
    public class DecorPlacementTests
    {
        private static readonly V3 Down = new V3(0, -1, 0);

        [Test]
        public void Raycast_FindsTheFloorStepFrontAndRail()
        {
            PlacementContext ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            Assert.AreEqual("courtyard floor", Hit(ctx, new V3(1.5f, 5f, 3f), Down).Patch.Name);
            SurfaceHit step = Hit(ctx, new V3(0f, 5f, 0.3f), Down);
            Assert.AreEqual(Surface.Step, step.Patch.Kind);
            Assert.AreEqual(0.15f, step.Point.Y, 1e-4f);
            Assert.AreEqual("house front", Hit(ctx, new V3(2f, 2f, 4f), new V3(0, 0, -1)).Patch.Name);
            Assert.AreEqual(4.3f, Hit(ctx, new V3(0f, 6f, 0.87f), Down).Point.Y, 1e-4f, "the balcony rail");
            Assert.IsNull(DecorPlacement.Raycast(ctx, new V3(0f, 5f, 3f), new V3(0, 1, 0)), "the sky");
        }

        [Test]
        public void FreeItem_OnTheFloorIsSnappedAndAccepted()
        {
            PlacementContext ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            PlacementResult r = DecorPlacement.PlaceAt(ctx, "pot_marigold", Hit(ctx, new V3(2.234f, 3f, 3.012f), Down), 37f, 9f, 1);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(string.Empty, r.Reason);
            Assert.AreEqual(2.25f, r.Pose.x, 1e-4f);
            Assert.AreEqual(3.0f, r.Pose.z, 1e-4f);
            Assert.AreEqual(30f, r.Pose.yaw, 1e-4f, "15 degree steps");
            Assert.AreEqual(HomeCatalog.Decoration("pot_marigold").MaxScale, r.Pose.scale, 1e-5f, "limited scale");
            Assert.AreEqual(Surface.Ground, r.Pose.surface);
        }

        [Test]
        public void WrongSurface_SaysWhereTheItemCanGo()
        {
            PlacementContext ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            PlacementResult r = DecorPlacement.PlaceAt(ctx, "bench_wooden", Hit(ctx, new V3(2f, 2f, 4f), new V3(0, 0, -1)), 0f, 1f, 0);
            Assert.AreEqual(PlacementProblem.WrongSurface, r.Problem);
            Assert.AreEqual("The wooden bench can only go on the ground or on the roof.", r.Reason);
        }

        [Test]
        public void TooBigForItsSurface_HangsOverTheEdge()
        {
            PlacementContext ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            PlacementResult r = DecorPlacement.PlaceAt(ctx, "charpai", Hit(ctx, new V3(0f, 3f, 3.0f), Down), 0f, 1f, 0);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.IsTrue(DecorPlacement.PlaceAt(ctx, "pot_marigold", Hit(ctx, new V3(0.5f, 6f, 0.87f), Down), 0f, 1f, 0).Ok, "a pot on the balcony rail");
            r = DecorPlacement.PlaceAt(ctx, "pot_marigold", Hit(ctx, new V3(0.95f, 6f, 0.87f), Down), 0f, 1f, 0);
            Assert.AreEqual(PlacementProblem.OverEdge, r.Problem, "past the end of the rail");
            StringAssert.Contains("over the edge", r.Reason);
        }

        [Test]
        public void SteepRoof_IsRefusedAndAFlatRoofAccepted()
        {
            HouseDesign tiled = HouseDesign.Default();
            tiled.ModuleAt("roof").module = "roof_tiled_slope";
            PlacementContext ctx = DecorPlacement.For(tiled, 2);
            PlacementResult r = DecorPlacement.PlaceAt(ctx, "pot_marigold", Hit(ctx, new V3(1f, 12f, -1f), Down), 0f, 1f, 0);
            Assert.AreEqual(PlacementProblem.TooSteep, r.Problem);
            StringAssert.Contains("too steep", r.Reason);

            ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            r = DecorPlacement.PlaceAt(ctx, "pot_marigold", Hit(ctx, new V3(1f, 12f, -3f), Down), 0f, 1f, 0);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(Surface.Roof, r.Pose.surface);
        }

        [Test]
        public void IntoTheHouse_IsRefused()
        {
            PlacementContext ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            PlacementResult r = DecorPlacement.Check(ctx, Pose("pot_marigold", 2f, 0f, 0.15f));
            Assert.AreEqual(PlacementProblem.HitsHouse, r.Problem, "it would stand in the plinth");
            StringAssert.Contains("into the wall", r.Reason);
            Assert.IsTrue(DecorPlacement.Check(ctx, Pose("pot_marigold", 2f, 0f, 0.25f)).Ok);
            Assert.AreEqual(PlacementProblem.HitsHouse, DecorPlacement.Check(ctx, Pose("pot_marigold", 3.35f, 0f, 2f)).Problem, "the side wall");
        }

        [Test]
        public void Overlaps_AreRefusedExceptOnRangoliOrTheItemItself()
        {
            HouseDesign d = HouseDesign.Default();
            d.decorations.Add(Pose("pot_marigold", 2f, 0f, 3f, uid: 1));
            d.decorations.Add(Pose("rangoli_lotus", -1.5f, 0f, 3f, uid: 2));
            PlacementContext ctx = DecorPlacement.For(d, 2);
            PlacementResult r = DecorPlacement.Check(ctx, Pose("pot_palm", 2.2f, 0f, 3.1f, uid: 3));
            Assert.AreEqual(PlacementProblem.Overlaps, r.Problem);
            Assert.AreEqual("Another decoration is already here.", r.Reason);
            Assert.IsTrue(DecorPlacement.Check(ctx, Pose("pot_marigold", 2.05f, 0f, 3f, uid: 1)).Ok, "moving an item a little");
            Assert.IsTrue(DecorPlacement.Check(ctx, Pose("diya_clay", -1.5f, 0f, 3f, uid: 4)).Ok, "a diya on the rangoli");
            Assert.AreEqual(PlacementProblem.Overlaps, DecorPlacement.Check(ctx, Pose("rangoli_peacock", -1.2f, 0f, 3.3f, uid: 5)).Problem, "rangoli on rangoli");
            Assert.IsTrue(DecorPlacement.Check(ctx, Pose("pot_palm", 2.4f, 0f, 3f, uid: 3)).Ok, "side by side");
        }

        [Test]
        public void DoorAndGate_StayClearButRangoliMayGoBeforeTheDoor()
        {
            PlacementContext ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            PlacementResult r = DecorPlacement.Check(ctx, Pose("pot_marigold", 0f, 0f, 0.9f));
            Assert.AreEqual(PlacementProblem.BlocksDoor, r.Problem);
            Assert.AreEqual("Keep the doorway clear so people can come and go.", r.Reason);
            Assert.IsTrue(DecorPlacement.Check(ctx, Pose("rangoli_lotus", 0f, 0f, 1.2f)).Ok, "rangoli belongs at the door");
            Assert.IsTrue(DecorPlacement.Check(ctx, Pose("diya_clay", 0.5f, 0.15f, 0.3f, surface: Surface.Step)).Ok, "diyas on the step");
            Assert.AreEqual(PlacementProblem.BlocksGate, DecorPlacement.Check(ctx, Pose("pot_marigold", HouseGeometry.GateX, 0f, 5.4f)).Problem);
        }

        [Test]
        public void HungItems_HangFromWallsAndRailsFacingOut()
        {
            PlacementContext ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            PlacementResult r = DecorPlacement.PlaceAt(ctx, "lantern_akash_kandil", Hit(ctx, new V3(2.2f, 2.9f, 4f), new V3(0, 0, -1)), 90f, 1f, 0);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(0f, r.Pose.yaw, 1e-3f, "faces the courtyard, whatever yaw was asked");
            Assert.AreEqual(2.9f - 0.6f, r.Pose.y, 1e-3f, "hangs below its hook");
            Assert.AreEqual(0.14f, r.Pose.z, 1e-3f, "clear of the wall");

            r = DecorPlacement.PlaceAt(ctx, "lantern_paper_round", Hit(ctx, new V3(0.5f, 6f, 0.87f), Down), 0f, 1f, 2);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.Greater(r.Pose.z, 0.9f, "outside the balcony rail");

            r = DecorPlacement.PlaceAt(ctx, "lantern_akash_kandil", Hit(ctx, new V3(3.6f, 5f, 3f), Down), 0f, 1f, 0);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(270f, r.Pose.yaw, 1e-3f, "the left wall faces -x");

            r = DecorPlacement.PlaceAt(ctx, "lantern_akash_kandil", Hit(ctx, new V3(2.2f, 0.8f, 4f), new V3(0, 0, -1)), 0f, 1f, 0);
            Assert.AreEqual(PlacementProblem.TooLow, r.Problem);
            StringAssert.Contains("higher", r.Reason);
            r = DecorPlacement.PlaceAt(ctx, "lantern_akash_kandil", Hit(ctx, new V3(1f, 5f, 3f), Down), 0f, 1f, 0);
            Assert.AreEqual(PlacementProblem.WrongSurface, r.Problem);
            Assert.AreEqual("The star lantern (akash kandil) can only go on a wall or from a railing.", r.Reason);
        }

        [Test]
        public void Paths_SpanBetweenHooksWithinLimits()
        {
            PlacementContext ctx = DecorPlacement.For(HouseDesign.Default(), 2);
            var back = new V3(0, 0, -1);
            PlacementResult r = DecorPlacement.PathBetween(ctx, "toran_mango", Hit(ctx, new V3(-0.7f, 2.62f, 3f), back), Hit(ctx, new V3(0.7f, 2.62f, 3f), back), 0.1f, 0);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual(0f, r.Pose.z, 1e-4f);

            r = DecorPlacement.PathBetween(ctx, "toran_mango", Hit(ctx, new V3(-0.1f, 2.6f, 3f), back), Hit(ctx, new V3(0.1f, 2.6f, 3f), back), 0.1f, 0);
            Assert.AreEqual(PlacementProblem.PathTooShort, r.Problem);
            StringAssert.Contains("further", r.Reason);

            r = DecorPlacement.PathBetween(ctx, "string_lights_warm", Hit(ctx, new V3(3.6f, 5f, 0.5f), Down), Hit(ctx, new V3(-3.6f, 5f, 5f), Down), 0.1f, 0);
            Assert.AreEqual(PlacementProblem.PathTooLong, r.Problem);
            StringAssert.Contains("6 metres", r.Reason);

            r = DecorPlacement.PathBetween(ctx, "string_lights_warm", Hit(ctx, new V3(-1f, 5f, 6f), Down), Hit(ctx, new V3(3f, 5f, 6f), Down), 0.3f, 0);
            Assert.AreEqual(PlacementProblem.PathBlocked, r.Problem, "through the gate");
            StringAssert.Contains("through a wall", r.Reason);

            r = DecorPlacement.PathBetween(ctx, "string_lights_warm", Hit(ctx, new V3(0f, 1.0f, 3f), new V3(0, 0, 1)), Hit(ctx, new V3(0f, 1.2f, 3f), back), 0.1f, 0);
            Assert.AreEqual(PlacementProblem.BlocksDoor, r.Problem, "a strand down the middle of the courtyard at waist height");

            r = DecorPlacement.PathBetween(ctx, "garland_marigold", Hit(ctx, new V3(1f, 5f, 3f), Down), Hit(ctx, new V3(2f, 5f, 3f), Down), 0.2f, 0);
            Assert.AreEqual(PlacementProblem.WrongSurface, r.Problem, "garlands do not lie on the floor");
        }

        [Test]
        public void Budgets_PerTierAndPerCategory()
        {
            HouseDesign d = HouseDesign.Default();
            int uid = 1;
            for (int i = 0; i < 30; i++)
            {
                d.decorations.Add(Pose("diya_clay", -3f + i * 0.1f, 0f, 5f, uid: uid++));
            }

            PlacementContext low = DecorPlacement.For(d, 0);
            Assert.AreEqual(30, DecorPlacement.CategoryCap(0, DecorCategory.Lamps));
            PlacementResult r = DecorPlacement.Check(low, Pose("diya_clay", 2f, 0f, 2f, uid: 99));
            Assert.AreEqual(PlacementProblem.CategoryFull, r.Problem);
            Assert.AreEqual("There are as many lamps as this phone can show. Remove one to add another.", r.Reason);
            Assert.IsTrue(DecorPlacement.Check(DecorPlacement.For(d, 2), Pose("diya_clay", 2f, 0f, 2f, uid: 99)).Ok, "a high tier has room");

            for (int i = 0; i < 7; i++)
            {
                d.decorations.Add(Pose("pot_palm", -3f + i * 0.5f, 0f, 4f, uid: uid++));
            }

            d.decorations.Add(Pose("charpai", 2f, 0f, 1.5f, uid: uid++));
            d.decorations.Add(Pose("rangoli_lotus", -2f, 0f, 2f, uid: uid++));
            Assert.AreEqual(59, DecorPlacement.UnitsUsed(d));
            r = DecorPlacement.Check(DecorPlacement.For(d, 0), Pose("rangoli_geometric", 0.5f, 0f, 2.6f, uid: 99));
            Assert.AreEqual(PlacementProblem.OverBudget, r.Problem);
            StringAssert.Contains("Remove one", r.Reason);
            Assert.AreEqual(57, DecorPlacement.UnitsUsed(d, exceptUid: uid - 1), "moving an item does not count it twice");
        }

        [Test]
        public void Audit_FindsItemsAModuleSwapLeftUnsupported()
        {
            HouseDesign d = HouseDesign.Default();
            PlacementContext ctx = DecorPlacement.For(d, 2);
            PlacementResult onRail = DecorPlacement.PlaceAt(ctx, "pot_marigold", Hit(ctx, new V3(0.4f, 6f, 0.87f), Down), 0f, 1f, 0, uid: 1);
            Assert.IsTrue(onRail.Ok, onRail.Reason);
            d.decorations.Add(onRail.Pose);
            d.decorations.Add(Pose("pot_marigold", 2f, 0f, 3f, uid: 2));
            Assert.IsEmpty(DecorPlacement.Audit(DecorPlacement.For(d, 2)));

            d.ModuleAt("balcony").module = "balcony_jharokha";
            var failing = DecorPlacement.Audit(DecorPlacement.For(d, 2));
            Assert.AreEqual(1, failing.Count);
            Assert.AreEqual(1, failing[0].Pose.uid);
            Assert.AreEqual(PlacementProblem.NoSurface, failing[0].Problem, "the jharokha parapet is lower than the iron rail");
        }

        [Test]
        public void EveryProblem_HasAPlainReason()
        {
            DecorInfo info = HomeCatalog.Decoration("garland_marigold");
            foreach (PlacementProblem p in Enum.GetValues(typeof(PlacementProblem)))
            {
                string reason = DecorPlacement.Reason(p, info);
                if (p == PlacementProblem.None)
                {
                    Assert.AreEqual(string.Empty, reason);
                    continue;
                }

                Assert.IsNotEmpty(reason, p.ToString());
                Assert.IsTrue(reason.EndsWith("."), p.ToString());
                StringAssert.DoesNotContain("_", reason, "no ids in reasons");
                Assert.IsTrue(char.IsUpper(reason[0]), p.ToString());
            }
        }

        private static SurfaceHit Hit(PlacementContext ctx, V3 origin, V3 dir)
        {
            SurfaceHit? hit = DecorPlacement.Raycast(ctx, origin, dir);
            Assert.IsTrue(hit.HasValue, $"nothing hit from {origin}");
            return hit.Value;
        }

        private static PlacedDecor Pose(string item, float x, float y, float z, int uid = 50, Surface surface = Surface.Ground) =>
            new PlacedDecor { uid = uid, item = item, x = x, y = y, z = z, scale = 1f, surface = surface };
    }
}
