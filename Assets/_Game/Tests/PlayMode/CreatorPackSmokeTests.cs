using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>
    /// Phase 11 step 4: packs made with the creator tool (tools/creator, outside Unity) install in the game through its own
    /// pack manager, reader and linter. The sample in Docs/Phase11/creator_sample is the tool's self-test output.
    /// </summary>
    public class CreatorPackSmokeTests
    {
        private static readonly string[] Packs = { "sample_shows", "sample_wardrobe", "env_sample_lakeside" };
        private PackService _packs;

        private static string SampleRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Phase11", "creator_sample"));

        [TearDown]
        public void TearDown()
        {
            foreach (string id in Packs)
            {
                if (_packs != null && _packs.Manager.IsInstalled(id))
                {
                    _packs.Delete(id);
                }
            }

            string cache = Path.Combine(PacksInstaller.StorageRoot, "packs", "catalog.json");
            if (File.Exists(cache))
            {
                File.Delete(cache);
            }
        }

        [UnityTest]
        public IEnumerator ToolMadePacks_InstallInTheGame()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out _packs));
            Assert.IsTrue(File.Exists(Path.Combine(SampleRoot, "catalog.json")), "run tools/creator/pack.sh selftest Docs/Phase11/creator_sample");
            _packs.Transport = new FolderPackTransport(SampleRoot);
            var refresh = _packs.RefreshCatalog();
            while (!refresh.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(refresh.Result, "the tool's catalog reads in the game");
            foreach (string id in Packs)
            {
                Core.Packs.PackResult? finished = null;
                void OnFinished(string pack, Core.Packs.PackResult r) => finished = pack == id ? r : finished;
                _packs.Finished += OnFinished;
                _packs.InstallNow(id);
                float end = Time.realtimeSinceStartup + 20f;
                while ((_packs.Busy || finished == null) && Time.realtimeSinceStartup < end)
                {
                    yield return null;
                }

                _packs.Finished -= OnFinished;
                Assert.IsTrue(finished.HasValue && finished.Value.Ok, $"{id}: {finished?.Message}");
                Assert.IsTrue(_packs.Manager.IsInstalled(id), id);
            }

            Assert.IsNotNull(Environments.Find("sample_lakeside"), "the tool-made neighbourhood is registered");
            Assert.IsTrue(Game.Services.TryGet(out ShowPlayer player));
            Assert.IsTrue(player.Templates().Exists(s => s.id == "sample_shows_1"), "the tool-made show is offered in the designer");
        }
    }
}
