using NUnit.Framework;
using UtsavNights.Core.Settings;

namespace UtsavNights.Tests
{
    public class GameSettingsTests
    {
        [Test]
        public void Defaults_FollowPrd()
        {
            var s = new GameSettings();
            Assert.AreEqual(GameSettings.CurrentVersion, s.version);
            Assert.Less(s.effectsVolume, 1f, "effects start at a moderate level (PRD 11)");
            Assert.IsFalse(s.musicEnabled, "music off by default (PRD 11)");
            Assert.IsFalse(s.quietMode);
            Assert.IsTrue(s.hapticsEnabled);
            Assert.AreEqual(GameSettings.AutoTier, s.qualityTier);
            Assert.IsFalse(s.Sanitize(), "defaults are already valid");
        }

        [Test]
        public void Sanitize_ClampsEverything()
        {
            var s = new GameSettings
            {
                masterVolume = 3f,
                effectsVolume = -1f,
                hapticsIntensity = float.NaN,
                qualityTier = 9,
            };
            Assert.IsTrue(s.Sanitize());
            Assert.AreEqual(1f, s.masterVolume);
            Assert.AreEqual(0f, s.effectsVolume);
            Assert.AreEqual(1f, s.hapticsIntensity);
            Assert.AreEqual(GameSettings.AutoTier, s.qualityTier);
        }

        [Test]
        public void Migrate_StampsOldFiles_AndKeepsValues()
        {
            var s = new GameSettings { version = 0, quietMode = true };
            Assert.IsTrue(s.Migrate());
            Assert.AreEqual(GameSettings.CurrentVersion, s.version);
            Assert.IsTrue(s.quietMode);
            Assert.IsFalse(s.Migrate());
        }

        [Test]
        public void Clone_IsIndependent_AndEqualityIsByValue()
        {
            var a = new GameSettings();
            GameSettings b = a.Clone();
            Assert.AreEqual(a, b);
            b.reduceFlash = true;
            Assert.AreNotEqual(a, b);
            Assert.IsFalse(a.reduceFlash);
        }

        [Test]
        public void QuietMode_RemovesFlashesAndShake()
        {
            var s = new GameSettings { quietMode = true };
            Assert.IsFalse(SettingsRules.FlashesAllowed(true, s));
            Assert.IsTrue(SettingsRules.ReduceFlash(s));
            Assert.IsFalse(SettingsRules.ShakeAllowed(s));
        }

        [Test]
        public void IndividualAccessibilityToggles()
        {
            var s = new GameSettings();
            Assert.IsTrue(SettingsRules.FlashesAllowed(true, s));
            Assert.IsFalse(SettingsRules.FlashesAllowed(false, s), "tier without lights");
            s.reduceShake = true;
            Assert.IsFalse(SettingsRules.ShakeAllowed(s));
            s.reduceFlash = true;
            Assert.IsTrue(SettingsRules.ReduceFlash(s));
            s.hapticsIntensity = 0f;
            Assert.IsFalse(SettingsRules.HapticsOn(s));
        }
    }
}
