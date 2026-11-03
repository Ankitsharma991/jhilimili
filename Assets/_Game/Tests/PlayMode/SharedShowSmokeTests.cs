using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Shows;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 10 step 5: share a show by code; a friend plays it at their own spot, checked for their phone, and keeps a copy.</summary>
    public class SharedShowSmokeTests
    {
        private string _code;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Game.Services.TryGet(out SharedShows shared) && _code != null)
            {
                shared.Forget(_code);
            }

            if (Game.Services.TryGet(out ShowPlayer player))
            {
                player.Stop();
            }

            QualityManager quality = Object.FindFirstObjectByType<QualityManager>();
            if (quality != null)
            {
                quality.UseAutomaticTier();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ShareAShow_AFriendOpensPlaysAndKeepsIt()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out CloudService cloud));
            Assert.IsTrue(Game.Services.TryGet(out SharedShows shared));
            Assert.IsTrue(Game.Services.TryGet(out ShowPlayer player));
            ShowDef mine = ShowTemplates.All[0].Clone();
            mine.name = "Dadi show";

            cloud.SignOut();
            Task<(string code, string error)> early = shared.Share(mine);
            yield return Wait(early);
            Assert.IsNull(early.Result.code, "sharing needs the sign-in");

            yield return Wait(cloud.SignIn());
            Task<(string code, string error)> made = shared.Share(mine);
            yield return Wait(made);
            Assert.IsNotNull(made.Result.code, made.Result.error);
            _code = made.Result.code;
            StringAssert.DoesNotContain("Dadi", CloudInstaller.EditorServer.Peek(_code).payload, "the show's name stays on the sharer's phone");
            Assert.AreEqual(ShareKind.Show, CloudInstaller.EditorServer.Peek(_code).kind);

            // The friend: no account.
            cloud.SignOut();
            Task<(ShowDef show, string error)> opened = shared.Open(ShareCodes.Display(_code));
            yield return Wait(opened);
            ShowDef theirs = opened.Result.show;
            Assert.IsNotNull(theirs, opened.Result.error);
            Assert.AreEqual("From " + ShareCodes.Display(_code), theirs.name);
            Assert.AreEqual(mine.cues.Count, theirs.cues.Count);
            CollectionAssert.IsEmpty(player.Check(theirs), "it plays here");
            Assert.IsTrue(player.Play(theirs));
            player.Stop();

            // Kept for offline; a home code opened as a show is told apart.
            ((MemoryCloud)cloud.Cloud).Offline = true;
            opened = shared.Open(_code);
            yield return Wait(opened);
            ((MemoryCloud)cloud.Cloud).Offline = false;
            Assert.IsNotNull(opened.Result.show, "the copy on this phone");

            var friend = new MemoryCloud(CloudInstaller.EditorServer, "friend-dev");
            Assert.IsTrue(friend.SignInWithGoogle().Result.Ok);
            string home = friend.Publish(ShareKind.Home, HomeVisits.Serialise(HomeSnapshot.From(Core.Home.HouseDesign.Default(), "town_lane", null))).Result.Value.code;
            opened = shared.Open(home);
            yield return Wait(opened);
            StringAssert.Contains("home", opened.Result.error);
        }

        [UnityTest]
        public IEnumerator AShowTooBigForThisPhone_ExplainsItself_InTheDesigner()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Object.FindFirstObjectByType<QualityManager>().SetTierManually(0);
            yield return null;

            // A friend's show from a high-end phone: eight fountains lit together (the low tier allows six at once).
            var big = ShowTemplates.Blank(string.Empty, string.Empty, 8);
            for (int pad = 0; pad < 8; pad++)
            {
                big.cues.Add(new ShowCue { cracker = "flowerpot_gold", pad = pad, at = 0f });
            }

            var friend = new MemoryCloud(CloudInstaller.EditorServer, "friend-big");
            Assert.IsTrue(friend.SignInWithGoogle().Result.Ok);
            _code = friend.Publish(ShareKind.Show, SharedShows.Serialise(big)).Result.Value.code;

            var designer = Object.FindFirstObjectByType<ShowDesigner>();
            designer.Open();
            designer.OpenList();
            designer.OpenFriendShow(_code);
            yield return WaitWhileBusy(designer);
            Assert.IsFalse(designer.ListOpen, ListStatusOf(designer));
            Assert.AreEqual("From " + ShareCodes.Display(_code), designer.Draft.name);
            StringAssert.Contains("but here", designer.Status, "explains why it can't play on this phone");
            CollectionAssert.IsNotEmpty(designer.Problems());
            designer.Close();
        }

        [UnityTest]
        public IEnumerator Designer_SharesASavedShow_AndListsReceivedOnes()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out CloudService cloud));
            Assert.IsTrue(Game.Services.TryGet(out ShowPlayer player));
            yield return Wait(cloud.SignIn());
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "shows_share_test.json");
            player.UseStore(new ShowStore(path));
            try
            {
                ShowDef saved = ShowTemplates.All[2].Clone();
                saved.id = player.Library.NewId();
                saved.name = "Tihar for Nani";
                Assert.IsTrue(player.Save(saved));

                var designer = Object.FindFirstObjectByType<ShowDesigner>();
                designer.Open();
                designer.OpenList();
                designer.ShareShow(saved);
                yield return WaitWhileBusy(designer);
                Assert.IsNotNull(designer.LastSharedCode, designer.ListStatus);
                _code = designer.LastSharedCode;
                StringAssert.Contains(ShareCodes.Display(_code), GUIUtility.systemCopyBuffer);

                designer.OpenFriendShow(_code);
                yield return WaitWhileBusy(designer);
                Assert.IsTrue(designer.Save(), designer.Status);
                Assert.IsNotNull(player.Library.shows.Find(s => s.name == "From " + ShareCodes.Display(_code)), "kept as a show of the player's own");
                designer.OpenList();
                Assert.IsNotNull(GameObject.Find($"From {ShareCodes.Display(_code)} (received)"), "listed under received shows");
                designer.Close();
            }
            finally
            {
                player.UseStore(new ShowStore());
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
        }

        private static string ListStatusOf(ShowDesigner d) => d.ListStatus;

        private static IEnumerator Wait(Task task)
        {
            float end = Time.realtimeSinceStartup + 20f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.IsTrue(task.IsCompleted, "timed out");
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }

        private static IEnumerator WaitWhileBusy(ShowDesigner designer)
        {
            float end = Time.realtimeSinceStartup + 20f;
            do
            {
                yield return null;
            }
            while (designer.Busy && Time.realtimeSinceStartup < end);

            Assert.IsFalse(designer.Busy, "timed out");
        }
    }
}
