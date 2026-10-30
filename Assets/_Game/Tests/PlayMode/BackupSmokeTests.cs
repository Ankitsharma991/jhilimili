using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Backup;
using UtsavNights.Core.Home;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Settings;
using UtsavNights.Core.Shows;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 10 step 1 (F24): back up one phone, restore on another, undo; and the screen from Settings.</summary>
    public class BackupSmokeTests
    {
        private const long Now = 1_790_000_000;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Application.temporaryCachePath, "backup_test");
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }

            Directory.CreateDirectory(Path.Combine(_root, "old_phone"));
            Directory.CreateDirectory(Path.Combine(_root, "new_phone"));
        }

        [TearDown]
        public void TearDown()
        {
            BackupFiles.EditorPick = null;
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void BackUpOnePhone_RestoreOnAnother_ThenUndo()
        {
            string oldPhone = Path.Combine(_root, "old_phone");
            string newPhone = Path.Combine(_root, "new_phone");

            // The old phone: a decorated house with a layout, an avatar, a show, "Remove ads" bought and one unlock.
            HouseState house = HouseState.Fresh();
            house.design.decorations.Add(new PlacedDecor { uid = 1, item = "diya_clay" });
            house.design.nextUid = 2;
            Assert.IsTrue(house.SaveLayout(0, "Diwali"));
            Write(oldPhone, HouseStore.FileName, house);
            AvatarProfile avatar = AvatarPresets.Default;
            avatar.created = true;
            avatar.name = "Asha";
            Write(oldPhone, ProfileStore.FileName, avatar);
            var shows = new ShowLibrary();
            ShowDef show = ShowTemplates.All[0].Clone();
            show.name = "Gentle";
            Assert.IsTrue(shows.Put(show));
            Write(oldPhone, ShowStore.FileName, shows);
            Write(oldPhone, SettingsStore.FileName, new GameSettings { quietMode = true, qualityTier = 2 });
            var oldProgress = new PlayerProgress();
            oldProgress.SetPurchased("remove_ads");
            oldProgress.GrantRewardedUnlock("rocket_basic", Now - 60);
            Write(oldPhone, ProgressStore.FileName, oldProgress);

            byte[] file = new BackupService(oldPhone, () => Now).Export("tihar-lights");

            // The new phone already has a different house, a journal and no purchase.
            Write(newPhone, HouseStore.FileName, HouseState.Fresh());
            File.WriteAllText(Path.Combine(newPhone, HouseStore.JournalName), "{\"op\":\"stale\"}\n");
            Write(newPhone, ProgressStore.FileName, new PlayerProgress());
            var restore = new BackupService(newPhone, () => Now);
            Assert.AreEqual(BackupError.WrongKeyOrDamaged, BackupCrypto.OpenWithPassword(file, "tihar-light", out _));
            Assert.AreEqual(BackupError.None, BackupCrypto.OpenWithPassword(file, "tihar-lights", out BackupArchive archive));
            Assert.IsNull(restore.Check(archive, out BackupSummary summary));
            Assert.IsTrue(summary.Avatar);
            Assert.AreEqual(1, summary.Decorations);
            Assert.AreEqual(1, summary.Layouts);
            Assert.AreEqual(1, summary.Shows);
            Assert.AreEqual(1, summary.Unlocks);
            StringAssert.Contains("your avatar", summary.Describe());

            Assert.IsFalse(restore.CanUndo);
            Assert.IsTrue(restore.Apply(archive, out string error), error);
            HouseState restored = new HouseStore(Path.Combine(newPhone, HouseStore.FileName)).Load();
            Assert.AreEqual(house.design.Fingerprint(), restored.design.Fingerprint());
            Assert.AreEqual("Diwali", restored.layouts[0].name);
            Assert.IsFalse(File.Exists(Path.Combine(newPhone, HouseStore.JournalName)), "the old house's journal is gone");
            Assert.AreEqual("Asha", new ProfileStore(Path.Combine(newPhone, ProfileStore.FileName)).Load().name);
            Assert.AreEqual(1, new ShowStore(Path.Combine(newPhone, ShowStore.FileName)).Load().shows.Count);
            GameSettings settings = JsonUtility.FromJson<GameSettings>(File.ReadAllText(Path.Combine(newPhone, SettingsStore.FileName)));
            Assert.IsTrue(settings.quietMode);
            Assert.AreEqual(GameSettings.AutoTier, settings.qualityTier, "the new phone finds its own quality tier");
            PlayerProgress progress = new ProgressStore(Path.Combine(newPhone, ProgressStore.FileName)).Load(Now);
            Assert.IsFalse(progress.adsRemoved, "a backup never carries the purchase");
            Assert.IsTrue(progress.IsUnlocked("rocket_basic", Now), "timed unlocks come along");

            Assert.IsTrue(restore.CanUndo);
            Assert.IsTrue(restore.Undo(out error), error);
            Assert.AreEqual(0, new HouseStore(Path.Combine(newPhone, HouseStore.FileName)).Load().design.decorations.Count, "undo brings back the old house");
            Assert.IsFalse(File.Exists(Path.Combine(newPhone, ProfileStore.FileName)), "and removes files the phone didn't have");
            Assert.IsTrue(File.Exists(Path.Combine(newPhone, HouseStore.JournalName)), "and its journal");
            Assert.IsFalse(restore.CanUndo);
        }

        [Test]
        public void DamagedOrEmptyParts_AreRefusedBeforeAnythingIsWritten()
        {
            string phone = Path.Combine(_root, "new_phone");
            var service = new BackupService(phone, () => Now);
            var archive = new BackupArchive();
            archive.parts[BackupRules.House] = "{ this is not json";
            Assert.IsNotNull(service.Check(archive, out _));
            Assert.IsFalse(service.Apply(archive, out string error));
            StringAssert.Contains("damaged", error);
            Assert.IsFalse(service.CanUndo, "nothing was set aside or written");

            Assert.IsNotNull(service.Check(new BackupArchive(), out _), "an empty backup");
            var newer = new BackupArchive();
            newer.parts[BackupRules.Settings] = JsonUtility.ToJson(new GameSettings { version = GameSettings.CurrentVersion + 1 });
            StringAssert.Contains("newer version", service.Check(newer, out _));
        }

        [UnityTest]
        public IEnumerator Screen_SavesAFile_RestoresIt_AndUndoes()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var settings = Object.FindFirstObjectByType<SettingsSheet>();
            Assert.IsNotNull(settings);
            settings.OpenBackup();
            var screen = Object.FindFirstObjectByType<BackupScreen>();
            Assert.IsTrue(screen.IsOpen, "opened from Settings");

            screen.SetPassword("short");
            screen.SaveFile();
            StringAssert.Contains("password", screen.Status);
            screen.SetPassword("festival-night");
            screen.SaveFile();
            yield return WaitWhileBusy(screen);
            StringAssert.Contains("Backup saved", screen.Status);
            string exported = Path.Combine(BackupFiles.CacheFolder(), "exported");
            string[] files = Directory.GetFiles(exported, "*" + BackupRules.Extension);
            Assert.IsNotEmpty(files);
            System.Array.Sort(files);
            BackupFiles.EditorPick = files[^1];

            screen.SetPassword("festival-nights");
            screen.RestoreFromFile();
            yield return WaitWhileBusy(screen);
            StringAssert.Contains("Wrong password", screen.Status);
            Assert.IsFalse(screen.ConfirmVisible);

            screen.SetPassword("festival-night");
            screen.RestoreFromFile();
            yield return WaitWhileBusy(screen);
            Assert.IsTrue(screen.ConfirmVisible, screen.Status);
            StringAssert.Contains("settings", screen.Summary);
            screen.CancelConfirm();

            screen.RestoreFromFile();
            yield return WaitWhileBusy(screen);
            Assert.IsTrue(screen.Confirm());
            yield return null;
            yield return null;
            screen = Object.FindFirstObjectByType<BackupScreen>();
            screen.Open();
            Assert.IsTrue(screen.UndoVisible, "after a restore the screen offers undo");
            Assert.IsTrue(screen.Undo());
            yield return null;
            yield return null;
            Assert.IsFalse(new BackupService().CanUndo);
            foreach (string f in files)
            {
                File.Delete(f);
            }
        }

        private static IEnumerator WaitWhileBusy(BackupScreen screen)
        {
            float end = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                if (!screen.Busy)
                {
                    yield break;
                }
            }

            Assert.Fail("the backup took too long");
        }

        private static void Write(string folder, string file, object value) => File.WriteAllText(Path.Combine(folder, file), JsonUtility.ToJson(value));
    }
}
