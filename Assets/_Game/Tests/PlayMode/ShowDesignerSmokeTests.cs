using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Shows;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 9 step 5 (F22, PRD 12): in the show designer, create a show on the pads, adjust a delay, save it, play it and replay it from the list.</summary>
    public class ShowDesignerSmokeTests
    {
        private string _path;

        [TearDown]
        public void TearDown()
        {
            if (_path != null && File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        [UnityTest]
        public IEnumerator Designer_Creates_Saves_AndReplays()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out ShowPlayer player));
            _path = Path.Combine(Application.temporaryCachePath, "shows_designer_test.json");
            player.UseStore(new ShowStore(_path));
            var designer = Object.FindFirstObjectByType<ShowDesigner>();
            Assert.IsNotNull(designer, "the show designer in the HUD");
            yield return null;
            Assert.IsTrue(designer.Launcher.gameObject.activeInHierarchy, "the Shows button is on screen");

            designer.Open();
            Assert.IsTrue(designer.IsOpen);
            CollectionAssert.Contains(designer.Palette, "flowerpot_gold");
            CollectionAssert.DoesNotContain(designer.Palette, "peacock_fountain", "locked crackers aren't offered");

            designer.New();
            Assert.AreEqual(4, designer.Draft.pads.Count);
            Assert.IsNotNull(designer.PadView(3), "every pad is drawn on the map");
            designer.SelectPad(0);
            Assert.IsNotNull(designer.AddCracker("flowerpot_gold"));
            Assert.IsNotNull(designer.Drop("sparkler_classic", 1));
            ShowCue chakri = designer.Drop("chakri_classic", 2);
            Assert.AreEqual(2 * ShowEditing.Rhythm, chakri.at, 1e-3f);
            designer.Nudge(-1f);
            designer.Nudge(0.1f);
            Assert.AreEqual(2 * ShowEditing.Rhythm - 0.9f, designer.SelectedCue.at, 1e-3f, "the delay controls move the selected cracker");
            designer.Rename("Our first show");
            CollectionAssert.IsEmpty(designer.Problems());
            StringAssert.StartsWith("Ready", designer.Status);

            Assert.IsTrue(designer.Save());
            ShowDef saved = new ShowStore(_path).Load().Find(designer.Draft.id);
            Assert.IsNotNull(saved, "saved to shows.json");
            Assert.AreEqual("Our first show", saved.name);
            Assert.AreEqual(3, saved.cues.Count);

            Assert.IsTrue(designer.Preview());
            Assert.IsTrue(designer.Previewing, "the panel steps aside while the show plays");
            float end = Time.realtimeSinceStartup + 40f;
            while (player.State == ShowPlayer.PlayerState.Running && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.AreEqual(ShowPlayer.PlayerState.Idle, player.State);
            Assert.AreEqual(3, player.Lit);

            // Replay from the list: the saved show loads as itself (same id), not a copy.
            designer.Close();
            designer.Open();
            designer.OpenList();
            Assert.IsTrue(designer.ListOpen);
            bool found = false;
            foreach ((string label, ShowDef show, bool isSaved) in designer.Choices())
            {
                if (isSaved && show.id == saved.id)
                {
                    designer.Load(show, asCopy: false);
                    found = true;
                }
            }

            Assert.IsTrue(found, "the saved show is in the list");
            designer.CloseList();
            Assert.AreEqual(saved.id, designer.Draft.id);
            Assert.IsTrue(designer.Preview(), "the saved show plays again");
            yield return new WaitForSeconds(0.3f);
            designer.StopPreview();
            yield return null;
            Assert.IsFalse(designer.Previewing);
            designer.Close();
            Game.Services.Get<CrackerSpawner>().ClearAll();
        }

        [UnityTest]
        public IEnumerator Designer_ExplainsWhyAShowCantPlay()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var designer = Object.FindFirstObjectByType<ShowDesigner>();
            yield return null;
            designer.Open();
            designer.New();
            Assert.IsFalse(designer.Preview(), "an empty show doesn't play");
            StringAssert.Contains("at least one cracker", designer.Status);
            designer.Rename("");
            Assert.IsFalse(designer.Save());
            StringAssert.Contains("name", designer.Status);
            designer.Close();
        }
    }
}
