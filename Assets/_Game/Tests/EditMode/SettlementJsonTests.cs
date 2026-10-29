using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests
{
    /// <summary>Phase 8 step 1: layout JSON round trips, and a broken layout is refused with reasons.</summary>
    public class SettlementJsonTests
    {
        [Test]
        public void EveryBuiltInLayout_RoundTrips()
        {
            foreach (EnvironmentInfo env in Environments.All)
            {
                SettlementLayout layout = SettlementLibrary.For(env.Id);
                string json = SettlementJson.Write(layout);
                SettlementLayout back = SettlementJson.Read(json, out List<string> problems);
                Assert.IsNotNull(back, string.Join("\n", problems));
                Assert.AreEqual(json, SettlementJson.Write(back), env.Id);
                Assert.AreEqual(layout.Spot("street").pads.Count, back.Spot("street").pads.Count);
                CollectionAssert.AreEqual(layout.Spot("rooftop").requires, back.Spot("rooftop").requires);
            }
        }

        [Test]
        public void BrokenLayouts_AreRefused()
        {
            Assert.IsNull(SettlementJson.Read("{ nope", out List<string> problems));
            Assert.IsNotEmpty(problems);
            Assert.IsNull(SettlementJson.Read("{\"version\":99}", out problems));
            StringAssert.Contains("newer", problems[0]);
            string json = SettlementJson.Write(SettlementLibrary.For(Environments.TownLane)).Replace("\"vacant_lot\"", "\"nowhere\"");
            Assert.IsNull(SettlementJson.Read(json, out problems));
            Assert.IsNotEmpty(problems);
        }
    }
}
