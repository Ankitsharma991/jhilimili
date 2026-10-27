using System.IO;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Core.Monetisation;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests
{
    /// <summary>progress.json on disk: round trip, corrupt file, and the service saving each change.</summary>
    public class ProgressStoreTests
    {
        private const long Now = 1_800_000_000;
        private string _dir;
        private string _path;

        [SetUp]
        public void CreateDir()
        {
            _dir = Path.Combine(Application.temporaryCachePath, "progress-tests-" + System.Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, ProgressStore.FileName);
        }

        [TearDown]
        public void RemoveDir()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Test]
        public void Service_SavesUnlocksAndPurchase_AndReloadsThem()
        {
            long clock = Now;
            var service = new ProgressService(new ProgressStore(_path), () => clock);
            int changes = 0;
            service.Changed += () => changes++;
            service.GrantRewardedUnlock("peacock_fountain");
            service.SetPurchased("remove_ads");
            service.SetPurchased("remove_ads");
            Assert.AreEqual(2, changes, "a repeated purchase confirmation is not a change");

            var reloaded = new ProgressService(new ProgressStore(_path), () => clock + 60);
            Assert.IsTrue(reloaded.AdsRemoved);
            Assert.AreEqual(PlayerProgress.RewardedUnlockSeconds - 60, reloaded.SecondsLeft("peacock_fountain"));
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void LockedOnlyMattersForCatalogLockedItems()
        {
            var service = new ProgressService(new ProgressStore(_path), () => Now);
            Assert.IsTrue(service.IsUnlocked("flowerpot_gold", false));
            Assert.IsFalse(service.IsUnlocked("peacock_fountain", true));
        }

        [Test]
        public void CorruptFile_StartsEmpty()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "{ nope");
            var service = new ProgressService(new ProgressStore(_path), () => Now);
            Assert.IsFalse(service.AdsRemoved);
            Assert.IsEmpty(service.Current.unlocks);
        }
    }
}
