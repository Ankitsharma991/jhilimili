using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 11 step 2 (F25): go inside with a fade, decorate, go upstairs, come out; only one of inside and outside is drawn.</summary>
    public class InteriorSmokeTests
    {
        [UnityTest]
        public IEnumerator GoInside_Decorate_Upstairs_ComeOut()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out HomeService home));
            Assert.IsTrue(Game.Services.TryGet(out SettlementView settlement));
            var mode = Object.FindFirstObjectByType<InteriorMode>();
            var tray = Object.FindFirstObjectByType<TrayView>();
            Assert.IsTrue(mode.Launcher.gameObject.activeInHierarchy, "Go inside is on screen");
            int outdoor = home.Decor.Count;
            int before = home.Design.decorations.Count;

            Assert.IsTrue(mode.GoInside());
            yield return WaitIdle(mode.Controller);
            Assert.IsTrue(mode.Inside);
            Assert.AreEqual(Rooms.Main, mode.Controller.Room.Id);
            Assert.IsFalse(settlement.gameObject.activeInHierarchy, "the neighbourhood is put away");
            Assert.IsFalse(home.House.gameObject.activeInHierarchy, "and the house front");
            Assert.IsFalse(tray.gameObject.activeInHierarchy, "the HUD steps aside");
            Assert.Greater(mode.Offered.Count, 4);
            Assert.IsFalse(mode.Offered.Contains("lamp_post_small"));
            Assert.LessOrEqual(mode.Controller.Triangles, InteriorBudget.Triangles[0]);

            // Place on the low table, then by a tap on the open floor.
            string problem = mode.Controller.Place("diya_clay", 0, new RoomHit(Surface.Step, new V3(0f, 0.42f, 2.25f), new V3(0, 1, 0)));
            Assert.IsNull(problem, problem);
            Assert.AreEqual(1, home.Design.InRoom(Rooms.Main).Count);
            Assert.AreEqual(outdoor, home.Decor.Count, "outdoor decorations are untouched");
            yield return new WaitForSecondsRealtime(CameraRig.OverrideSeconds + 0.2f);
            mode.Choose("pot_marigold");
            Vector3 floor = InteriorController.Origin + new Vector3(1.4f, 0f, 1.7f);
            mode.TapAt(Camera.main.WorldToScreenPoint(floor));
            Assert.AreEqual("Placed.", mode.Status);
            Assert.AreEqual(2, home.Design.InRoom(Rooms.Main).Count);
            mode.TapAt(Camera.main.WorldToScreenPoint(InteriorController.Origin + new Vector3(0f, 0f, 0.4f)));
            StringAssert.Contains("doorway", mode.Status);

            // Pick the pot and remove it.
            PlacedDecor pot = home.Design.InRoom(Rooms.Main).Find(d => d.item == "pot_marigold");
            mode.Choose(null);
            mode.TapAt(Camera.main.WorldToScreenPoint(InteriorController.Origin + new Vector3(pot.x, 0.18f, pot.z)));
            Assert.AreEqual(pot.uid, mode.SelectedUid, mode.Status);
            Assert.IsTrue(mode.RemoveSelected());
            Assert.AreEqual(1, home.Design.InRoom(Rooms.Main).Count);

            Assert.IsTrue(mode.OtherFloor());
            yield return WaitIdle(mode.Controller);
            Assert.AreEqual(Rooms.Upper, mode.Controller.Room.Id);
            Assert.IsFalse(settlement.gameObject.activeInHierarchy, "still inside");

            Assert.IsTrue(mode.Leave());
            yield return WaitIdle(mode.Controller);
            Assert.IsFalse(mode.Inside);
            Assert.IsTrue(settlement.gameObject.activeInHierarchy, "the neighbourhood is back");
            Assert.IsTrue(home.House.gameObject.activeInHierarchy);
            Assert.IsTrue(tray.gameObject.activeInHierarchy, "and the HUD");
            Assert.AreEqual(outdoor, home.Decor.Count, "the indoor diya isn't drawn outside");
            Assert.IsNull(GameObject.Find("Interior_" + Rooms.Main), "the room is gone from memory");

            // Saved with the house: a fresh load still has it.
            HouseState saved = new HouseStore().Load();
            Assert.AreEqual(1, saved.design.InRoom(Rooms.Main).Count);
            foreach (PlacedDecor d in home.Design.InRoom(Rooms.Main))
            {
                home.Do(new RemoveDecor(d.uid));
            }

            home.Save();
            Assert.AreEqual(before, home.Design.decorations.Count);
        }

        [UnityTest]
        public IEnumerator NoGoingInside_DuringAVisitOrAWalk()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var mode = Object.FindFirstObjectByType<InteriorMode>();
            var walk = Object.FindFirstObjectByType<WalkMode>();
            walk.Open();
            yield return null;
            Assert.IsFalse(mode.GoInside());
            StringAssert.Contains("walking", mode.Status);
            walk.Home();
        }

        private static IEnumerator WaitIdle(InteriorController c)
        {
            float end = Time.realtimeSinceStartup + 5f;
            do
            {
                yield return null;
            }
            while (c.Busy && Time.realtimeSinceStartup < end);

            Assert.IsFalse(c.Busy, "the fade finished");
        }
    }
}
