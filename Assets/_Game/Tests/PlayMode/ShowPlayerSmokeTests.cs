using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Shows;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 9 step 4 (F22): a show plays at the celebration spot, lights each cracker on its cue and pad, ends, saves, loads and replays.</summary>
    public class ShowPlayerSmokeTests
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
        public IEnumerator Show_LightsOnTime_Saves_AndReplays()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out ShowPlayer player), "the show player in the Main scene");
            _path = Path.Combine(Application.temporaryCachePath, "shows_test.json");
            player.UseStore(new ShowStore(_path));

            ShowDef show = ShowTemplates.All[0].Clone();
            show.id = player.Library.NewId();
            CollectionAssert.IsEmpty(player.Check(show), "the gentle template plays at the courtyard");

            var lit = new List<(ShowCue cue, Vector3? pos, float at)>();
            player.CueLit += (cue, cracker, at) => lit.Add((cue, cracker != null ? cracker.transform.position : (Vector3?)null, at));
            Assert.IsTrue(player.Play(show));
            Assert.AreEqual(ShowPlayer.PlayerState.Running, player.State);

            float end = Time.realtimeSinceStartup + 40f;
            while (player.State == ShowPlayer.PlayerState.Running && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.AreEqual(ShowPlayer.PlayerState.Idle, player.State, "the show ends once every cracker is cleared");
            Assert.AreEqual(show.cues.Count, lit.Count, "every cue was lit");
            Assert.AreEqual(0, player.Missed);
            CheckCues(player, lit);

            Assert.IsTrue(player.Save(show));
            var reloaded = new ShowStore(_path).Load();
            Assert.IsNotNull(reloaded.Find(show.id), "the show is in shows.json");
            Assert.AreEqual(show.cues.Count, reloaded.Find(show.id).cues.Count);

            Assert.IsTrue(player.CanReplay);
            lit.Clear();
            Assert.IsTrue(player.Replay(), "the same show plays again");
            yield return new WaitForSeconds(1.3f);
            Assert.GreaterOrEqual(lit.Count, 1, "the first cue lights after its second on the pad, as before");
            player.Stop();
            Assert.AreEqual(ShowPlayer.PlayerState.Idle, player.State);
            Game.Services.Get<CrackerSpawner>().ClearAll();
        }

        private static void CheckCues(ShowPlayer player, List<(ShowCue cue, Vector3? pos, float at)> lit)
        {
            Core.Neighbourhood.SpotDef spot = ShowPlayer.CurrentSpot();
            foreach ((ShowCue cue, Vector3? pos, float at) in lit)
            {
                Assert.AreEqual(cue.at, at, 0.25f, $"{cue.cracker} lit on its cue");
                Assert.IsTrue(pos.HasValue, $"{cue.cracker} was placed");
                Vector3 pad = ShowPlayer.PadPosition(spot, player.Last.pads[cue.pad]);
                Assert.Less(Vector3.Distance(pad, pos.Value), 0.05f, $"{cue.cracker} stood on pad {cue.pad + 1}");
            }
        }

        [UnityTest]
        public IEnumerator InvalidShow_DoesNotPlay()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out ShowPlayer player));
            ShowDef show = ShowTemplates.All[0].Clone();
            show.pads[1].dx = 10f;
            Assert.IsFalse(player.Play(show));
            Assert.AreEqual(ShowPlayer.PlayerState.Idle, player.State);
        }
    }
}
