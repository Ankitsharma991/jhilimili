using System.IO;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests
{
    /// <summary>Phase 6 step 1: profile.json survives restarts, old versions and damage (PRD 13.5).</summary>
    public class ProfileStoreTests
    {
        private string _dir;
        private ProfileStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "utsav-profile-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _store = new ProfileStore(Path.Combine(_dir, ProfileStore.FileName));
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_dir, true);

        [Test]
        public void NoFile_GivesTheDefaultPresetNotYetCreated()
        {
            AvatarProfile p = _store.Load();
            Assert.IsFalse(p.created, "the creator still offers itself once");
            Assert.Greater(p.outfit.Count, 1);
        }

        [Test]
        public void SaveThenLoad_KeepsEverything()
        {
            AvatarProfile p = AvatarPresets.All[3].Clone();
            p.name = "आशा";
            p.skinTone = 0.37f;
            OutfitRules.Equip(p, WardrobeCatalog.Default, "hijab", 2);
            Assert.IsTrue(_store.Save(p));

            AvatarProfile back = _store.Load();
            Assert.AreEqual("आशा", back.name);
            Assert.AreEqual(0.37f, back.skinTone, 1e-5f);
            Assert.IsTrue(back.created);
            CollectionAssert.AreEquivalent(p.outfit.Select(i => i.id + ":" + i.variant), back.outfit.Select(i => i.id + ":" + i.variant));
        }

        [Test]
        public void CorruptFile_FallsBackWithoutThrowing()
        {
            File.WriteAllText(_store.Path, "{ not json");
            Assert.IsFalse(_store.Load().created);
        }

        [Test]
        public void OldOrEditedFile_IsRepairedAndRewritten()
        {
            File.WriteAllText(_store.Path, "{\"version\":0,\"created\":true,\"bodyStyle\":5,\"outfit\":[{\"id\":\"jeans\",\"variant\":1},{\"id\":\"ghost\"}]}");
            AvatarProfile p = _store.Load();
            Assert.AreEqual(AvatarProfile.CurrentVersion, p.version);
            Assert.AreEqual(2f, p.bodyStyle);
            CollectionAssert.AreEquivalent(new[] { "jeans", OutfitRules.DefaultTop }, p.outfit.Select(i => i.id));
            StringAssert.Contains("\"version\": " + AvatarProfile.CurrentVersion, File.ReadAllText(_store.Path), "the repaired profile was saved");
        }
    }
}
