using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Home;

namespace UtsavNights.Tests
{
    /// <summary>Phase 10 step 4 (F23): a shared home carries no free text, fits the visitor's phone, and is kept for offline.</summary>
    public class HomeSnapshotTests
    {
        [Test]
        public void Snapshot_NeverCarriesTheAvatarsName_AndTheAvatarOnlyWhenChosen()
        {
            AvatarProfile me = AvatarPresets.Default;
            me.created = true;
            me.name = "Asha";
            HouseDesign design = HouseDesign.Default();
            design.decorations.Add(new PlacedDecor { uid = 1, item = "diya_clay" });

            HomeSnapshot with = HomeSnapshot.From(design, "riverside", me);
            Assert.IsTrue(with.hasAvatar);
            Assert.AreEqual(string.Empty, with.avatar.name, "never the name");
            Assert.AreEqual("Asha", me.name, "the player's own profile is untouched");
            CollectionAssert.IsEmpty(with.Problems());

            HomeSnapshot without = HomeSnapshot.From(design, "riverside", null);
            Assert.IsFalse(without.hasAvatar);
            Assert.AreEqual(1, without.design.decorations.Count);

            with.avatar.name = "Typed by someone";
            CollectionAssert.IsNotEmpty(with.Problems(), "a name off the network is refused");
            with.Clean();
            Assert.AreEqual(string.Empty, with.avatar.name);
        }

        [Test]
        public void Snapshot_RefusesNewerVersionsAndOddNeighbourhoods_AndRepairsTheRest()
        {
            var s = new HomeSnapshot { version = HomeSnapshot.CurrentVersion + 1 };
            CollectionAssert.IsNotEmpty(s.Problems());
            s = new HomeSnapshot { environment = "Somewhere Else!" };
            CollectionAssert.IsNotEmpty(s.Problems());
            s = new HomeSnapshot();
            s.design.decorations.Add(new PlacedDecor { uid = 1, item = "no_such_thing" });
            s.Clean();
            Assert.AreEqual(0, s.design.decorations.Count, "unknown items are dropped, as from a save file");
        }

        [Test]
        public void Visit_ShowsWhatTheVisitorsPhoneCanCarry_InTheOwnersOrder()
        {
            HouseDesign design = HouseDesign.Default();
            for (int i = 1; i <= 120; i++)
            {
                design.decorations.Add(new PlacedDecor { uid = i, item = "diya_clay" });
            }

            HouseDesign low = HomeSnapshot.ForTier(design, 0);
            HouseDesign high = HomeSnapshot.ForTier(design, 2);
            Assert.Less(low.decorations.Count, high.decorations.Count);
            Assert.AreEqual(1, low.decorations[0].uid, "the owner's first pieces come first");
            int units = 0;
            foreach (PlacedDecor d in low.decorations)
            {
                units += DecorBudget.CostOf(HomeCatalog.Decoration(d.item));
            }

            Assert.LessOrEqual(units, DecorBudget.Units[0]);
            Assert.AreEqual(120, design.decorations.Count, "the snapshot itself is untouched");
        }

        [Test]
        public void History_KeepsTheNewestTen_AndForgets()
        {
            var h = new VisitHistory();
            for (int i = 0; i < 12; i++)
            {
                h.Remember("CODE" + i, "{}", 100 + i);
            }

            Assert.AreEqual(VisitHistory.Kept, h.visits.Count);
            Assert.AreEqual("CODE11", h.visits[0].code, "newest first");
            Assert.IsNull(h.Find("CODE0"));
            h.Remember("CODE5", "{\"v\":2}", 500);
            Assert.AreEqual("CODE5", h.visits[0].code, "a revisit moves to the top");
            Assert.AreEqual(VisitHistory.Kept, h.visits.Count);
            Assert.IsTrue(h.Forget("CODE5"));
            Assert.IsNull(h.Find("CODE5"));
        }
    }
}
