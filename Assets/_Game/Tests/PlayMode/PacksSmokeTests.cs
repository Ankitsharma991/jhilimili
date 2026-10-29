using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Packs;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>
    /// Phase 9 step 3: an environment pack downloads (from the sample folder in the hosting layout), verifies, installs,
    /// appears in the browser, builds its settlement and deletes (F21); a failed download changes nothing.
    /// </summary>
    public class PacksSmokeTests
    {
        private PackService _packs;

        private static string SampleRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Phase9", "sample_packs"));

        private IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out _packs), "the pack service in the Main scene");
            if (!File.Exists(Path.Combine(SampleRoot, "catalog.json")))
            {
                Assert.Inconclusive("Build the sample packs first (Utsav/Phase 9/Build Sample Packs; the EditMode tests do).");
            }

            _packs.Transport = new FolderPackTransport(SampleRoot);
            var refresh = _packs.RefreshCatalog();
            while (!refresh.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(refresh.Result, "the catalog arrives");
        }

        private IEnumerator WaitForJob()
        {
            float end = Time.realtimeSinceStartup + 20f;
            while (_packs.Busy && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.IsFalse(_packs.Busy, "the download finished");
        }

        [TearDown]
        public void TearDown()
        {
            if (_packs != null && _packs.Manager.IsInstalled(SamplePacks.BlueCityPack))
            {
                _packs.Delete(SamplePacks.BlueCityPack);
            }

            PlayerPrefs.DeleteKey(NeighbourhoodInstaller.EnvironmentPrefKey);

            // The downloaded catalog would show its packs in other tests' browsers.
            string cache = Path.Combine(PacksInstaller.StorageRoot, "packs", "catalog.json");
            if (File.Exists(cache))
            {
                File.Delete(cache);
            }
        }

        [UnityTest]
        public IEnumerator EnvironmentPack_DownloadsChoosesAndDeletes()
        {
            yield return Load();
            var browser = Object.FindFirstObjectByType<EnvironmentBrowser>();
            browser.Open();
            CollectionAssert.Contains(browser.Shown, SamplePacks.BlueCityPack, "a download card for the catalog's pack");

            _packs.InstallNow(SamplePacks.BlueCityPack);
            yield return WaitForJob();
            Assert.IsTrue(_packs.Manager.IsInstalled(SamplePacks.BlueCityPack));
            Assert.IsNotNull(Environments.Find(SamplePacks.BlueCity), "its environment is registered");
            CollectionAssert.Contains(browser.Shown, SamplePacks.BlueCity, "now a card with Choose");

            Assert.IsTrue(browser.Choose(SamplePacks.BlueCity));
            Assert.IsTrue(Game.Services.TryGet(out SettlementView settlement));
            Assert.AreEqual(SamplePacks.BlueCity, settlement.Layout.environment, "the downloaded neighbourhood is built");
            Assert.Greater(settlement.LotCount, 5);

            var storage = Object.FindFirstObjectByType<StorageManager>();
            storage.Open();
            CollectionAssert.Contains(storage.Listed, SamplePacks.BlueCityPack);
            Assert.IsTrue(storage.Delete(SamplePacks.BlueCityPack).Ok);
            Assert.IsNull(Environments.Find(SamplePacks.BlueCity));
            Assert.AreEqual(Environments.TownLane, settlement.Layout.environment, "deleting the place in use goes back to the town lane");
            storage.Close();
        }

        private sealed class DeadTransport : IPackTransport
        {
            public byte[] Fetch(string path) => throw new PackTransportException("connection lost");
        }

        [UnityTest]
        public IEnumerator FailedDownload_ChangesNothing()
        {
            yield return Load();
            _packs.Transport = new DeadTransport();
            PackResult? finished = null;
            _packs.Finished += (id, r) => finished = r;
            _packs.InstallNow(SamplePacks.BlueCityPack);
            yield return WaitForJob();
            yield return null;
            Assert.IsTrue(finished.HasValue);
            Assert.IsFalse(finished.Value.Ok);
            StringAssert.Contains("Nothing changed", finished.Value.Message);
            Assert.IsFalse(_packs.Manager.IsInstalled(SamplePacks.BlueCityPack));
            Assert.IsFalse(Directory.Exists(Path.Combine(PacksInstaller.StorageRoot, "packs", "staging", SamplePacks.BlueCityPack)));
        }
    }
}
