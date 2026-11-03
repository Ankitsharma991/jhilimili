using NUnit.Framework;
using UtsavNights.Core.Analytics;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Settings;

namespace UtsavNights.Tests
{
    /// <summary>Phase 12 step 2: the player's record (coins, reputation, stats) and the Explore control settings.</summary>
    public class PlayerRecordTests
    {
        [Test]
        public void Coins_EarnUpToTheCap_AndSpendOnlyWhatIsThere()
        {
            var r = new PlayerRecord();
            Assert.AreEqual(50, r.Earn(50));
            Assert.AreEqual(0, r.Earn(-10), "nothing from a negative reward");
            Assert.IsFalse(r.Spend(80), "not enough");
            Assert.AreEqual(50, r.coins, "and nothing taken");
            Assert.IsTrue(r.Spend(30));
            Assert.AreEqual(20, r.coins);
            r.coins = PlayerRecord.MaxCoins - 5;
            Assert.AreEqual(5, r.Earn(100), "never past the cap");
            Assert.IsFalse(r.Spend(-1));
        }

        [Test]
        public void Sanitize_RepairsADamagedRecord()
        {
            var r = new PlayerRecord { coins = -40, reputation = int.MaxValue, metresExplored = float.NaN, jumps = -3, secondsExplored = -1f };
            Assert.IsTrue(r.Sanitize());
            Assert.AreEqual(0, r.coins);
            Assert.AreEqual(PlayerRecord.MaxReputation, r.reputation);
            Assert.AreEqual(0f, r.metresExplored);
            Assert.AreEqual(0, r.jumps);
            Assert.IsFalse(r.Sanitize(), "a good record is left as it is");
            Assert.IsFalse(new PlayerRecord().Migrate());
            Assert.IsTrue(new PlayerRecord { version = 0 }.Migrate());
        }

        [Test]
        public void Reputation_Levels_Titles_AndProgress()
        {
            Assert.AreEqual(1, Reputation.Level(0));
            Assert.AreEqual("Newcomer", Reputation.Title(0));
            Assert.AreEqual(2, Reputation.Level(100));
            Assert.AreEqual("Neighbour", Reputation.Title(150));
            Assert.AreEqual(0.25f, Reputation.Progress(150), 1e-4f);
            Assert.AreEqual(150, Reputation.ToNext(150));
            int top = Reputation.Thresholds[Reputation.Thresholds.Length - 1];
            Assert.AreEqual(Reputation.Thresholds.Length, Reputation.Level(top + 10));
            Assert.AreEqual(1f, Reputation.Progress(top + 10));
            Assert.AreEqual(0, Reputation.ToNext(top + 10));
            Assert.AreEqual(Reputation.Thresholds.Length, Reputation.Titles.Length);
            var r = new PlayerRecord();
            r.GainReputation(-50);
            Assert.AreEqual(0, r.reputation, "reputation is only gained");
        }

        [Test]
        public void ControlSettings_RangesMigrationAndReporting()
        {
            Assert.AreEqual(ControlRanges.MinLook, ControlRanges.Look(0f));
            Assert.AreEqual(ControlRanges.MaxLook, ControlRanges.Look(1f));
            Assert.AreEqual(0.5f, ControlRanges.LookSlider(ControlRanges.Look(0.5f)), 1e-4f);
            Assert.AreEqual(0.5f, ControlRanges.HudSlider(ControlRanges.Hud(0.5f)), 1e-4f);

            var fresh = new GameSettings();
            Assert.AreEqual(1f, fresh.lookSensitivity);
            Assert.IsTrue(fresh.autoRecentre);
            Assert.IsFalse(fresh.holdToSprint);
            var old = new GameSettings { version = 3 };
            Assert.IsTrue(old.Migrate());
            Assert.AreEqual(4, old.version);
            Assert.AreEqual(1f, old.lookSensitivity, "an older file keeps the defaults");

            var wild = new GameSettings { lookSensitivity = 50f, hudScale = float.NaN };
            Assert.IsTrue(wild.Sanitize());
            Assert.AreEqual(ControlRanges.MaxLook, wild.lookSensitivity);
            Assert.AreEqual(1f, wild.hudScale);

            var changed = new GameSettings { invertLook = true, leftHanded = true, lookSensitivity = 1.5f };
            var diff = SettingsDiff.Changes(new GameSettings(), changed);
            Assert.IsTrue(diff.Exists(c => c.Key == "invert_look" && c.Value == "on"));
            Assert.IsTrue(diff.Exists(c => c.Key == "left_handed" && c.Value == "on"));
            Assert.IsTrue(diff.Exists(c => c.Key == "look_sensitivity" && c.Value == "1.5"));
            Assert.AreNotEqual(new GameSettings(), changed);
        }
    }
}
