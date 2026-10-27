using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Finale;

namespace UtsavNights.Tests
{
    public class FinaleScheduleTests
    {
        [Test]
        public void FewerThanThree_GivesNothing()
        {
            Assert.IsEmpty(FinaleSchedule.Build(new[] { new FinaleItem(0, 2f, 0), new FinaleItem(1, 2f, 0) }));
            Assert.IsEmpty(FinaleSchedule.Build(null));
        }

        [Test]
        public void PayoffsLandOnAFixedGap_InClimaxOrder()
        {
            var items = new[]
            {
                new FinaleItem(0, 2f, 7),   // cake, ranked last
                new FinaleItem(1, 3f, 4),   // single bang, long fuse
                new FinaleItem(2, 1.2f, 0), // sparkler first
                new FinaleItem(3, 2.5f, 1), // flowerpot
            };
            List<FinaleSlot> slots = FinaleSchedule.Build(items, 1.5f);
            Assert.AreEqual(4, slots.Count);

            var byPayoff = new List<FinaleSlot>(slots);
            byPayoff.Sort((a, b) => a.PayoffAt.CompareTo(b.PayoffAt));
            CollectionAssert.AreEqual(new[] { 2, 3, 1, 0 }, byPayoff.ConvertAll(s => s.Index), "sparkler, flowerpot, bang, cake");
            for (int i = 1; i < byPayoff.Count; i++)
            {
                Assert.AreEqual(1.5f, byPayoff[i].PayoffAt - byPayoff[i - 1].PayoffAt, 1e-4f, "fixed gap between payoffs");
            }

            foreach (FinaleSlot s in slots)
            {
                Assert.GreaterOrEqual(s.IgniteAt, -1e-4f, "nothing is lit before the finale starts");
            }

            Assert.AreEqual(0f, slots[0].IgniteAt, 1e-4f, "the first ignition starts the finale");
            FinaleSlot bang = slots.Find(s => s.Index == 1);
            Assert.AreEqual(3f, bang.PayoffAt - bang.IgniteAt, 1e-4f, "lit one fuse-length before its payoff");
        }

        [Test]
        public void MoreThanSix_UsesTheFirstSixByRank()
        {
            var items = new List<FinaleItem>();
            for (int i = 0; i < 9; i++)
            {
                items.Add(new FinaleItem(i, 2f, 9 - i));
            }

            List<FinaleSlot> slots = FinaleSchedule.Build(items);
            Assert.AreEqual(FinaleSchedule.MaxItems, slots.Count);
            Assert.IsFalse(slots.Exists(s => s.Index < 3), "the three highest-ranked (latest) items are left out");
        }

        [Test]
        public void EqualRanks_KeepSceneOrder_AndSlotsAreSortedByIgnition()
        {
            List<FinaleSlot> slots = FinaleSchedule.Build(new[] { new FinaleItem(5, 2f, 1), new FinaleItem(2, 2f, 1), new FinaleItem(9, 2f, 1) }, 1f);
            CollectionAssert.AreEqual(new[] { 2, 5, 9 }, slots.ConvertAll(s => s.Index));
            Assert.AreEqual(0f, slots[0].IgniteAt, 1e-4f);
            Assert.AreEqual(2f, slots[2].IgniteAt, 1e-4f);
        }
    }
}
