using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Analytics;
using UtsavNights.Core.Settings;

namespace UtsavNights.Tests
{
    /// <summary>F12: only PRD 17 events, no personal data, consent gate, settings diff.</summary>
    public class AnalyticsSchemaTests
    {
        [Test]
        public void EveryPrdEventIsListed()
        {
            string[] prd =
            {
                "app_first_open", "scene_ready", "cracker_placed", "cracker_ignited", "payoff_complete", "session_end",
                "settings_changed", "ad_shown", "ad_failed", "ad_reward", "purchase_started", "purchase_done",
                "clip_shared", "perf_sample",
            };
            CollectionAssert.AreEquivalent(prd, AnalyticsSchema.EventNames.ToArray());
        }

        [Test]
        public void ValidEventsPass()
        {
            Assert.IsEmpty(AnalyticsSchema.Validate("cracker_ignited", new EventParams().Add("id", "peacock_fountain").Add("is_first", true)));
            Assert.IsEmpty(AnalyticsSchema.Validate("perf_sample", new EventParams().Add("avg_fps", 58.4).Add("tier", "mid").Add("downgrades", 1L)));
            Assert.IsEmpty(AnalyticsSchema.Validate("settings_changed", new EventParams().Add("key", "master_volume").Add("value", "0.8")));
        }

        [Test]
        public void UnknownEventsAndParametersAreRejected()
        {
            Assert.IsNotEmpty(AnalyticsSchema.Validate("user_profile", new EventParams()));
            Assert.IsNotEmpty(AnalyticsSchema.Validate("cracker_placed", new EventParams().Add("id", "bomb_single").Add("device_id", "x")));
            Assert.IsNotEmpty(AnalyticsSchema.Validate("cracker_placed", new EventParams().Add("id", "a").Add("id", "b")));
        }

        [TestCase("someone@example.com")]
        [TestCase("Hello there")]
        [TestCase("9876543210")]
        [TestCase("Peacock")]
        [TestCase("a_very_long_identifier_that_goes_past_forty_chars")]
        public void PersonalDataAndFreeTextAreRejected(string value)
        {
            Assert.IsFalse(AnalyticsSchema.IsSafeId(value));
            Assert.IsNotEmpty(AnalyticsSchema.Validate("cracker_placed", new EventParams().Add("id", value)));
        }

        [TestCase("flowerpot_gold")]
        [TestCase("unlock_premium")]
        [TestCase("0.8")]
        [TestCase("english")]
        public void ShortIdsPass(string value)
        {
            Assert.IsTrue(AnalyticsSchema.IsSafeId(value));
        }

        [Test]
        public void Gate_HoldsUntilDecided_ThenSendsOrDrops()
        {
            var gate = new AnalyticsGate();
            Assert.IsFalse(gate.Offer("scene_ready", new EventParams()));
            Assert.AreEqual(1, gate.HeldCount);
            var released = gate.Decide(true);
            Assert.AreEqual(1, released.Count, "held events go out once allowed");
            Assert.IsTrue(gate.Offer("cracker_placed", new EventParams()));

            gate.Decide(false);
            Assert.IsFalse(gate.Offer("cracker_placed", new EventParams()), "never while denied");
            Assert.AreEqual(0, gate.HeldCount, "and nothing is kept");

            gate.Hold();
            gate.Offer("a", null);
            Assert.IsEmpty(gate.Decide(false), "denied after holding drops what was held");
        }

        [Test]
        public void Gate_KeepsOnlyTheLatestFifty()
        {
            var gate = new AnalyticsGate();
            for (int i = 0; i < AnalyticsGate.MaxHeld + 10; i++)
            {
                gate.Offer("e" + i, null);
            }

            var released = gate.Decide(true);
            Assert.AreEqual(AnalyticsGate.MaxHeld, released.Count);
            Assert.AreEqual("e10", released[0].Key);
        }

        [Test]
        public void SettingsDiff_ReportsChangedKeysWithPlainValues()
        {
            var before = new GameSettings();
            GameSettings after = before.Clone();
            after.quietMode = true;
            after.masterVolume = 0.42f;
            after.qualityTier = 0;
            after.shareUsageStats = false;
            var changes = SettingsDiff.Changes(before, after).ToDictionary(c => c.Key, c => c.Value);
            Assert.AreEqual("on", changes["quiet_mode"]);
            Assert.AreEqual("0.4", changes["master_volume"]);
            Assert.AreEqual("low", changes["quality"]);
            Assert.AreEqual(3, changes.Count, "the usage-statistics switch itself is never reported");
            Assert.IsTrue(changes.Values.All(AnalyticsSchema.IsSafeId));
        }

        [Test]
        public void Params_SerialiseToOneJsonLine()
        {
            string json = new EventParams().Add("id", "bomb_single").Add("is_first", true).Add("avg_fps", 59.95).ToJson("cracker_ignited");
            Assert.AreEqual("{\"event\":\"cracker_ignited\",\"id\":\"bomb_single\",\"is_first\":1,\"avg_fps\":59.95}", json);
        }
    }
}
