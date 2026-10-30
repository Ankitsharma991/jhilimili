using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 10 step 4 (F23): share a home, visit a friend's by code (view only, offline too), reports, take-downs, the screen.</summary>
    public class HomeVisitSmokeTests
    {
        private string _friendCode;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Game.Services.TryGet(out HomeVisits visits))
            {
                visits.Leave();
                if (_friendCode != null)
                {
                    visits.Forget(_friendCode);
                }
            }

            yield return null;
        }

        /// <summary>A friend on another phone shares a riverside home with two decorations and their avatar.</summary>
        private static IEnumerator FriendShares(MemoryCloud friend, System.Action<string> code)
        {
            Assert.IsTrue(friend.SignInWithGoogle().Result.Ok);
            HouseDesign design = HouseDesign.Default();
            design.decorations.Add(new PlacedDecor { uid = 1, item = "diya_clay" });
            design.decorations.Add(new PlacedDecor { uid = 2, item = "rangoli_lotus" });
            design.nextUid = 3;
            AvatarProfile look = AvatarPresets.All[2].Clone();
            look.created = true;
            look.name = "Bina";
            Task<CloudResult<ShareInfo>> published = friend.Publish(ShareKind.Home, HomeVisits.Serialise(HomeSnapshot.From(design, Environments.Riverside, look)));
            yield return Wait(published);
            Assert.IsTrue(published.Result.Ok);
            code(published.Result.Value.code);
        }

        [UnityTest]
        public IEnumerator VisitAFriend_ViewOnly_InTheirNeighbourhood_ThenLeave()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out CloudService cloud));
            Assert.IsTrue(Game.Services.TryGet(out HomeVisits visits));
            Assert.IsTrue(Game.Services.TryGet(out HomeService home));
            Assert.IsTrue(Game.Services.TryGet(out SettlementView settlement));
            var friend = new MemoryCloud(CloudInstaller.EditorServer, "friend-bina");
            yield return FriendShares(friend, c => _friendCode = c);

            cloud.SignOut();
            string ownEnvironment = settlement.Layout.environment;
            string ownDesign = home.Design.Fingerprint();
            Task<string> visit = visits.Visit(ShareCodes.Display(_friendCode).ToLowerInvariant());
            yield return Wait(visit);
            Assert.AreEqual(string.Empty, visit.Result, "visiting needs no Google account");
            Assert.IsTrue(home.Visiting);
            Assert.AreEqual(2, home.Shown.decorations.Count);
            Assert.AreEqual(2, home.Decor.Count, "the friend's decorations are on screen");
            Assert.AreEqual(ownDesign, home.Design.Fingerprint(), "the player's own house is untouched");
            Assert.AreEqual(Environments.Riverside, settlement.Layout.environment, "in their neighbourhood");
            Assert.IsTrue(Object.FindFirstObjectByType<CourtyardAvatar>().ShowingVisitor, "their avatar, because they chose to show it");
            Assert.IsFalse(Object.FindFirstObjectByType<DecorationController>().Open(), "view only: no decorating");
            Assert.IsFalse(home.Do(new PlaceDecor(new PlacedDecor { item = "diya_clay" })), "no edits while visiting");
            Assert.AreEqual(1, CloudInstaller.EditorServer.Peek(_friendCode).visits);

            visits.Leave();
            Assert.IsFalse(home.Visiting);
            Assert.AreEqual(ownEnvironment, settlement.Layout.environment, "back home");
            Assert.IsFalse(Object.FindFirstObjectByType<CourtyardAvatar>().ShowingVisitor);

            // Offline: the copy on this phone.
            ((MemoryCloud)cloud.Cloud).Offline = true;
            visit = visits.Visit(_friendCode);
            yield return Wait(visit);
            ((MemoryCloud)cloud.Cloud).Offline = false;
            Assert.AreEqual(string.Empty, visit.Result);
            Assert.IsTrue(visits.OfflineCopy);
            visits.Leave();

            // Three reports hide it; a take-down removes it, and the phone forgets it.
            for (int i = 0; i < CloudRules.HideAtReports; i++)
            {
                var reporter = new MemoryCloud(CloudInstaller.EditorServer, null);
                Assert.IsTrue(reporter.SignInAnonymously().Result.Ok);
                Assert.IsTrue(reporter.Report(_friendCode, ReportReason.Offensive).Result.Ok);
            }

            visit = visits.Visit(_friendCode);
            yield return Wait(visit);
            Assert.AreEqual(CloudRules.Explain(CloudError.Hidden), visit.Result);
            Assert.IsNull(visits.History.Find(_friendCode), "a hidden home isn't kept for offline either");
            CloudInstaller.EditorServer.Review(_friendCode, keep: true);
            Assert.IsTrue(friend.Withdraw(_friendCode).Result.Ok);
            visit = visits.Visit(_friendCode);
            yield return Wait(visit);
            Assert.AreEqual(CloudRules.Explain(CloudError.Withdrawn), visit.Result);
        }

        [UnityTest]
        public IEnumerator Screen_ShareMyHome_VisitWithTheBar_Report_Leave()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out CloudService cloud));
            cloud.SignOut();
            var screen = Object.FindFirstObjectByType<HomeVisitScreen>();
            Assert.IsTrue(screen.Launcher.gameObject.activeInHierarchy, "the Visit button is on screen");
            screen.Open();
            yield return WaitWhileBusy(screen);

            screen.ShareMyHome();
            StringAssert.Contains("Google sign-in", screen.Status, "sharing asks for the sign-in");
            screen.SetCode("BCDF-234");
            screen.VisitCode();
            yield return WaitWhileBusy(screen);
            StringAssert.Contains("8 letters", screen.Status);

            yield return Wait(cloud.SignIn());
            screen.ToggleAvatar();
            screen.ShareMyHome();
            yield return WaitWhileBusy(screen);
            Assert.IsNotNull(screen.LastCode, screen.Status);
            Assert.IsTrue(screen.MyHomes.Exists(s => s.code == screen.LastCode), "listed under your codes");
            screen.SendCode(screen.LastCode);
            StringAssert.Contains(ShareCodes.Display(screen.LastCode), GUIUtility.systemCopyBuffer);
            string mine = screen.LastCode;

            var friend = new MemoryCloud(CloudInstaller.EditorServer, "friend-chandra");
            yield return FriendShares(friend, c => _friendCode = c);
            screen.SetCode(_friendCode);
            screen.VisitCode();
            yield return WaitWhileBusy(screen);
            Assert.IsFalse(screen.IsOpen, "the page closes for the visit");
            Assert.IsTrue(screen.VisitBarVisible);
            StringAssert.Contains(ShareCodes.Display(_friendCode), screen.VisitTitle);
            Assert.IsFalse(screen.Launcher.gameObject.activeSelf, "no Visit button during a visit");
            screen.OpenReport();
            Assert.IsTrue(screen.ReasonsVisible);
            screen.Report(ReportReason.Spam);
            yield return WaitWhileBusy(screen);
            StringAssert.Contains("Thanks", screen.VisitNotes);
            Assert.AreEqual(1, CloudInstaller.EditorServer.Peek(_friendCode).reports);
            screen.Leave();
            Assert.IsFalse(screen.VisitBarVisible);
            Assert.IsTrue(screen.Launcher.gameObject.activeSelf);

            screen.Open();
            yield return WaitWhileBusy(screen);
            screen.TakeDown(mine);
            yield return WaitWhileBusy(screen);
            StringAssert.Contains("taken down", screen.Status);
            Assert.IsFalse(screen.MyHomes.Exists(s => s.code == mine));
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
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }

        private static IEnumerator WaitWhileBusy(HomeVisitScreen screen)
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
