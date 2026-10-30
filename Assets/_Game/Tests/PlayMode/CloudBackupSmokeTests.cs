using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 10 step 3 (F24): back up to the cloud on one phone, restore on a new one; the screen; deleting cloud data.</summary>
    public class CloudBackupSmokeTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Application.temporaryCachePath, "cloud_backup_test");
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }

            Directory.CreateDirectory(Path.Combine(_root, "old"));
            Directory.CreateDirectory(Path.Combine(_root, "new"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [UnityTest]
        public IEnumerator BackUpOnOnePhone_RestoreOnANewOne()
        {
            var server = new MemoryCloudServer();
            HouseState house = HouseState.Fresh();
            house.design.decorations.Add(new PlacedDecor { uid = 1, item = "diya_clay" });
            house.design.decorations.Add(new PlacedDecor { uid = 2, item = "rangoli_lotus" });
            house.design.nextUid = 3;
            File.WriteAllText(Path.Combine(_root, "old", HouseStore.FileName), JsonUtility.ToJson(house));
            var oldPhone = new CloudService(new MemoryCloud(server, "asha"), new BackupService(Path.Combine(_root, "old")));
            var newPhone = new CloudService(new MemoryCloud(server, "asha"), new BackupService(Path.Combine(_root, "new")));
            var stranger = new CloudService(new MemoryCloud(server, "bina"), new BackupService(Path.Combine(_root, "new")));

            Task<string> early = oldPhone.BackUpNow();
            yield return Wait(early);
            Assert.AreEqual(CloudRules.Explain(CloudError.SignedOut), early.Result, "signed out explains itself");

            yield return Wait(oldPhone.SignIn());
            Assert.IsTrue(oldPhone.SignedIn);
            for (int i = 0; i < 4; i++)
            {
                Task<string> made = oldPhone.BackUpNow();
                yield return Wait(made);
                Assert.AreEqual(string.Empty, made.Result);
            }

            yield return Wait(newPhone.SignIn());
            var listed = newPhone.List();
            yield return Wait(listed);
            Assert.AreEqual(CloudRules.KeptBackups, listed.Result.backups.Count, "the newest three are kept");
            Assert.AreEqual(2, listed.Result.backups[0].decorations);

            yield return Wait(stranger.SignIn());
            var theirs = stranger.List();
            yield return Wait(theirs);
            Assert.AreEqual(0, theirs.Result.backups.Count, "another account sees none of them");

            var fetched = newPhone.Fetch(listed.Result.backups[0].id);
            yield return Wait(fetched);
            Assert.IsNotNull(fetched.Result.archive, fetched.Result.error);
            Assert.AreEqual(2, fetched.Result.summary.Decorations);
            Assert.AreEqual(string.Empty, newPhone.Restore(fetched.Result.archive));
            Assert.AreEqual(2, new HouseStore(Path.Combine(_root, "new", HouseStore.FileName)).Load().design.decorations.Count);

            var refused = stranger.Fetch(listed.Result.backups[0].id);
            yield return Wait(refused);
            Assert.IsNull(refused.Result.archive, "a stranger can't fetch it");

            ((MemoryCloud)newPhone.Cloud).Offline = true;
            Task<string> offline = newPhone.BackUpNow();
            yield return Wait(offline);
            Assert.AreEqual(CloudRules.Explain(CloudError.Offline), offline.Result);
            ((MemoryCloud)newPhone.Cloud).Offline = false;

            Task<string> deleted = newPhone.DeleteMyData();
            yield return Wait(deleted);
            Assert.AreEqual(string.Empty, deleted.Result);
            Assert.IsFalse(newPhone.SignedIn);
            Assert.AreEqual(0, server.BackupCount("g_asha"));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "new", HouseStore.FileName)), "the phone keeps its own saves");
        }

        [UnityTest]
        public IEnumerator Screen_SignIn_BackUp_Restore_Undo_Delete()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out CloudService cloud));
            Assert.IsTrue(cloud.Configured, "the editor uses the memory cloud");
            cloud.SignOut();
            Object.FindFirstObjectByType<SettingsSheet>().OpenBackup();
            var screen = Object.FindFirstObjectByType<BackupScreen>();
            yield return WaitWhileBusy(screen);
            StringAssert.Contains("Sign in with Google", screen.Account);

            screen.SignInOrOut();
            yield return WaitWhileBusy(screen);
            Assert.IsTrue(cloud.SignedIn, screen.Status);
            StringAssert.Contains("Signed in", screen.Account);
            int before = screen.CloudBackups.Count;
            screen.BackUpNow();
            yield return WaitWhileBusy(screen);
            Assert.AreEqual("Backed up.", screen.Status);
            Assert.AreEqual(Mathf.Min(before + 1, CloudRules.KeptBackups), screen.CloudBackups.Count);

            screen.RestoreCloud(screen.CloudBackups[0].id);
            yield return WaitWhileBusy(screen);
            Assert.IsTrue(screen.ConfirmVisible, screen.Status);
            Assert.IsTrue(screen.Confirm());
            yield return null;
            yield return null;

            Assert.IsTrue(Game.Services.TryGet(out cloud));
            Assert.IsTrue(cloud.SignedIn, "still signed in after the restore's reload");
            screen = Object.FindFirstObjectByType<BackupScreen>();
            screen.Open();
            yield return WaitWhileBusy(screen);
            Assert.IsTrue(screen.UndoVisible);
            Assert.IsTrue(screen.Undo());
            yield return null;
            yield return null;

            screen = Object.FindFirstObjectByType<BackupScreen>();
            screen.Open();
            yield return WaitWhileBusy(screen);
            screen.DeleteCloudData();
            Assert.IsTrue(screen.ConfirmVisible, "deleting asks first");
            StringAssert.Contains("stays", screen.Summary);
            Assert.IsTrue(screen.Confirm());
            yield return WaitWhileBusy(screen);
            Assert.IsTrue(Game.Services.TryGet(out cloud));
            Assert.IsFalse(cloud.SignedIn);
            Assert.AreEqual("Your cloud data is deleted.", screen.Status);
            Assert.AreEqual(0, screen.CloudBackups.Count);
            screen.Close();
        }

        private static IEnumerator Wait(Task task)
        {
            float end = Time.realtimeSinceStartup + 20f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.IsTrue(task.IsCompleted, "timed out");
            if (task.IsFaulted)
            {
                Assert.Fail(task.Exception?.ToString());
            }
        }

        private static IEnumerator WaitWhileBusy(BackupScreen screen)
        {
            float end = Time.realtimeSinceStartup + 20f;
            do
            {
                yield return null;
            }
            while (screen.Busy && Time.realtimeSinceStartup < end);

            Assert.IsFalse(screen.Busy, "timed out");
        }
    }
}
