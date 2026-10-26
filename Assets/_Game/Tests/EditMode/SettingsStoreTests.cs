using System.IO;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Audio;
using UtsavNights.Content;
using UtsavNights.Core.Settings;
using UtsavNights.Effects;
using UtsavNights.Gameplay;
using UtsavNights.Haptics;

namespace UtsavNights.Tests
{
    public class SettingsStoreTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "utsav-settings-" + System.Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, SettingsStore.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Test]
        public void MissingFile_GivesDefaults()
        {
            var store = new SettingsStore(_path);
            GameSettings s = store.Load();
            Assert.AreEqual(new GameSettings(), s);
            Assert.AreEqual("missing", store.LastLoadIssue);
        }

        [Test]
        public void SaveThenLoad_RoundTrips_AndLeavesNoTempFile()
        {
            var store = new SettingsStore(_path);
            var s = new GameSettings { quietMode = true, effectsVolume = 0.3f, qualityTier = 0 };
            Assert.IsTrue(store.Save(s));
            Assert.IsTrue(store.Save(s), "overwrite works");
            Assert.IsFalse(File.Exists(_path + ".tmp"));
            Assert.AreEqual(s, store.Load());
            Assert.IsNull(store.LastLoadIssue);
        }

        [Test]
        public void CorruptFile_GivesDefaults()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "{ this is not json");
            var store = new SettingsStore(_path);
            Assert.AreEqual(new GameSettings(), store.Load());
            Assert.AreEqual("corrupt", store.LastLoadIssue);
        }

        [Test]
        public void OutOfRangeFile_IsClampedAndRewritten()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "{\"version\":0,\"masterVolume\":5,\"quietMode\":true}");
            var store = new SettingsStore(_path);
            GameSettings s = store.Load();
            Assert.AreEqual(1f, s.masterVolume);
            Assert.IsTrue(s.quietMode);
            Assert.AreEqual(GameSettings.CurrentVersion, s.version);
            StringAssert.Contains("\"version\": " + GameSettings.CurrentVersion, File.ReadAllText(_path));
        }
    }

    public class SettingsApplyTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("settings-root");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void Apply_ReachesEveryService()
        {
            var audio = new AudioService(_root.transform, 4);
            var effects = new EffectsService(_root.transform, QualityTier.High);
            var haptics = new HapticsService(new NullHapticsBackend());
            var rig = _root.AddComponent<CameraRig>();
            var s = new GameSettings { masterVolume = 0.5f, effectsVolume = 0.4f, quietMode = true, hapticsEnabled = false, hapticsIntensity = 0.3f };

            SettingsService.Apply(s, audio, effects, haptics, rig, null);

            Assert.AreEqual(0.5f, audio.MasterVolume);
            Assert.AreEqual(0.4f, audio.EffectsVolume);
            Assert.IsTrue(audio.QuietMode);
            Assert.IsFalse(effects.Flashes.Enabled, "quiet mode removes flashes even on High");
            Assert.IsTrue(effects.Flashes.ReduceFlash);
            Assert.IsFalse(haptics.Enabled);
            Assert.AreEqual(0.3f, haptics.Intensity, 1e-5f);
            Assert.IsFalse(rig.Shake(1f), "quiet mode removes shake");
        }

        [Test]
        public void TierChange_KeepsQuietModeFlashRule()
        {
            var effects = new EffectsService(_root.transform, QualityTier.High);
            SettingsService.Apply(new GameSettings { quietMode = true }, null, effects, null, null, null);
            effects.SetTier(QualityTier.Mid);
            Assert.IsFalse(effects.Flashes.Enabled, "a tier change must not bring flashes back in quiet mode");
            SettingsService.Apply(new GameSettings(), null, effects, null, null, null);
            Assert.IsTrue(effects.Flashes.Enabled);
        }

        [Test]
        public void ReduceShake_AloneBlocksShake()
        {
            var rig = _root.AddComponent<CameraRig>();
            SettingsService.Apply(new GameSettings { reduceShake = true }, null, null, null, rig, null);
            Assert.IsFalse(rig.Shake(1f));
            SettingsService.Apply(new GameSettings(), null, null, null, rig, null);
            Assert.IsTrue(rig.Shake(1f));
        }
    }
}
