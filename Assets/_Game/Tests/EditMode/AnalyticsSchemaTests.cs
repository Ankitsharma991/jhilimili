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
            string[] r1 =
            {
                "avatar_creator_opened", "avatar_saved", "avatar_skipped", "decor_mode_opened", "decor_mode_closed", "decoration_placed",
                "house_module_changed", "layout_saved", "layout_loaded", "house_restored", "walk_started", "walk_ended", "fast_travel",
                "spot_chosen", "environment_chosen",
            };
            string[] r3 = { "pack_installed", "pack_failed", "pack_deleted", "show_saved", "show_played", "clip_recorded", "clip_failed" };
            CollectionAssert.AreEquivalent(prd.Concat(r1).Concat(r3), AnalyticsSchema.EventNames.ToArray());
        }

        [Test]
        public void AvatarEvents_CarryNoNameBodyOrSkin()
        {
            var saved = new EventParams().Add("from_preset", true).Add("garments", 4L).Add("top", "kurta").Add("bottom", "churidar").Add("full", "none").Add("headwear", "turban");
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("avatar_saved", saved));
            foreach (string banned in new[] { "name", "skin_tone", "body_style", "height" })
            {
                Assert.IsNotEmpty(AnalyticsSchema.Validate("avatar_saved", new EventParams().Add(banned, "x")), banned + " is not allowed");
            }

            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("session_end", new EventParams().Add("duration_s", 300L).Add("ignitions", 9L).Add("has_avatar", true)));
        }

        [Test]
        public void DecorationEvents_CarryIdsAndCountsOnly()
        {
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("decoration_placed", new EventParams().Add("id", "lantern_akash_kandil").Add("category", "lamps")));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("house_module_changed", new EventParams().Add("point", "window_ground_left").Add("module", "window_jali")));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("decor_mode_closed", new EventParams().Add("edits", 12L).Add("saved", true).Add("decorations", 9L)));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("session_end", new EventParams().Add("duration_s", 60L).Add("ignitions", 2L).Add("has_avatar", false).Add("decorations", 3L)));
            Assert.IsNotEmpty(AnalyticsSchema.Validate("layout_saved", new EventParams().Add("slot", 0L).Add("name", "diwali")), "layout names stay on the phone");
            Assert.IsNotEmpty(AnalyticsSchema.Validate("decoration_placed", new EventParams().Add("id", "diya_clay").Add("x", 1.5)), "no positions");
            foreach (Core.Home.DecorInfo info in Core.Home.HomeCatalog.Decorations)
            {
                Assert.IsTrue(AnalyticsSchema.IsSafeId(info.Id), info.Id);
            }

            foreach (Core.Home.ModuleInfo module in Core.Home.HomeCatalog.Modules)
            {
                Assert.IsTrue(AnalyticsSchema.IsSafeId(module.Id), module.Id);
            }

            foreach (Core.Home.AttachmentPoint point in Core.Home.HomeCatalog.Points)
            {
                Assert.IsTrue(AnalyticsSchema.IsSafeId(point.Id), point.Id);
            }
        }

        [Test]
        public void NeighbourhoodEvents_CarryIdsAndCountsOnly()
        {
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("spot_chosen", new EventParams().Add("spot", "vacant_lot").Add("kind", "vacant_lot").Add("environment", "hill_village")));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("walk_ended", new EventParams().Add("seconds", 95L).Add("metres", 120L)));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("session_end", new EventParams().Add("duration_s", 60L).Add("environment", "riverside").Add("walk_s", 30L)));
            Assert.IsNotEmpty(AnalyticsSchema.Validate("walk_ended", new EventParams().Add("x", 3.5)), "no positions");
            Assert.IsNotEmpty(AnalyticsSchema.Validate("fast_travel", new EventParams().Add("spot", "open").Add("route", "a")), "no routes");
            foreach (Core.Neighbourhood.EnvironmentInfo env in Core.Neighbourhood.Environments.All)
            {
                Assert.IsTrue(AnalyticsSchema.IsSafeId(env.Id), env.Id);
                foreach (Core.Neighbourhood.SpotDef spot in Core.Neighbourhood.SettlementLibrary.For(env.Id).spots)
                {
                    Assert.IsTrue(AnalyticsSchema.IsSafeId(spot.id) && AnalyticsSchema.IsSafeId(spot.kind), spot.id);
                }
            }
        }

        [Test]
        public void PackShowAndClipEvents_CarryIdsAndCountsOnly()
        {
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("pack_installed", new EventParams().Add("pack", Core.Packs.SamplePacks.BlueCityPack).Add("version", "1.2.0").Add("type", "environment")));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("pack_failed", new EventParams().Add("pack", "env_blue_city_lane").Add("reason", "network")));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("show_saved", new EventParams().Add("pads", 3L).Add("cues", 9L).Add("seconds", 28L)));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("show_played", new EventParams().Add("cues", 9L).Add("seconds", 28L).Add("replay", true)));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("clip_recorded", new EventParams().Add("seconds", 14L).Add("source", "show").Add("frames", 410L)));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("clip_failed", new EventParams().Add("reason", "encoder").Add("source", "free")));
            CollectionAssert.IsEmpty(AnalyticsSchema.Validate("clip_shared", new EventParams().Add("target", "whatsapp").Add("seconds", 12L).Add("source", "free").Add("fell_back", false)));
            Assert.IsNotEmpty(AnalyticsSchema.Validate("show_saved", new EventParams().Add("name", "dadi")), "show names stay on the phone");
            Assert.IsNotEmpty(AnalyticsSchema.Validate("clip_recorded", new EventParams().Add("path", "x")), "no file paths");
            Assert.IsNotEmpty(AnalyticsSchema.Validate("pack_failed", new EventParams().Add("pack", "env_a").Add("reason", "HTTP 404 at https://cdn")), "no messages or URLs");
            foreach (Core.Clips.ShareTarget target in System.Enum.GetValues(typeof(Core.Clips.ShareTarget)))
            {
                Assert.IsTrue(AnalyticsSchema.IsSafeId(target.ToString().ToLowerInvariant()), target.ToString());
            }

            foreach (string type in new[] { Core.Packs.PackTypes.Environment, Core.Packs.PackTypes.Wardrobe, Core.Packs.PackTypes.Shows })
            {
                Assert.IsTrue(AnalyticsSchema.IsSafeId(type), type);
            }
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
