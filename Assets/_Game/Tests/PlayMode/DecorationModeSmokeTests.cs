using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>
    /// Phase 7 step 5: decoration mode in the real Main scene (F17, F18: place, undo, redo, save; the camera turns to the
    /// house; the lighter is blocked) and step 6's journal recovery (an edit survives a restart without saving).
    /// </summary>
    public class DecorationModeSmokeTests
    {
        private DecorationMode _mode;
        private DecorationController _controller;
        private HomeService _home;

        private IEnumerator Load(bool fresh = true)
        {
            if (fresh)
            {
                new HouseStore().Delete();
            }

            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _mode = Object.FindFirstObjectByType<DecorationMode>();
            Assert.IsNotNull(_mode, "decoration mode in the HUD");
            _controller = _mode.Controller;
            Assert.IsTrue(Game.Services.TryGet(out _home), "the home service");
        }

        private IEnumerator OpenAndSettle(DecorationController.View view = DecorationController.View.Courtyard)
        {
            _mode.Open();
            Assert.IsTrue(_mode.IsOpen);
            _controller.SetView(view);
            yield return new WaitForSeconds(CameraRig.OverrideSeconds + 0.2f);
        }

        /// <summary>A screen point in the open middle of the view (clear of the top bar and the drawer) that hits a surface.</summary>
        private Vector2 Aim(float x = 0.5f, float y = 0.48f)
        {
            var p = new Vector2(Screen.width * x, Screen.height * y);
            Assert.IsTrue(_controller.Raycast(p).HasValue, $"the view at {x},{y} looks at the house or courtyard");
            return p;
        }

        private void Tap(Vector2 p)
        {
            _controller.PointerDown(p);
            _controller.PointerUp(p);
        }

        [TearDown]
        public void TearDown() => new HouseStore().Delete();

        [UnityTest]
        public IEnumerator Open_TurnsTheCameraToTheHouseAndBlocksTheLighter()
        {
            yield return Load();
            CameraRig rig = Object.FindFirstObjectByType<CameraRig>();
            Assert.Greater(Camera.main.transform.forward.z, 0.5f, "the fireworks view looks out over the courtyard");
            yield return OpenAndSettle(DecorationController.View.House);
            Assert.AreEqual(1f, rig.OverrideWeight, 1e-3f);
            Assert.Less(Camera.main.transform.forward.z, -0.5f, "decoration mode looks back at the house");
            Assert.IsTrue(InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "the lighter ignores touches");
            Assert.AreEqual(HomeCatalog.Points.Length + 1, _home.House.PieceCount, "the house is built");

            _mode.Done();
            yield return new WaitForSeconds(CameraRig.OverrideSeconds + 0.2f);
            Assert.AreEqual(0f, rig.OverrideWeight, 1e-3f);
            Assert.Greater(Camera.main.transform.forward.z, 0.5f, "back to the fireworks view");
            Assert.IsFalse(InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)));
        }

        [UnityTest]
        public IEnumerator Place_UndoRedoAndDoneSaves()
        {
            yield return Load();
            yield return OpenAndSettle();
            _controller.Arm("pot_marigold");
            Vector2 at = Aim();
            _controller.PointerDown(at);
            Assert.IsNotNull(_controller.Preview);
            Assert.IsTrue(_controller.Preview.Ok, _controller.Preview.Reason);
            Assert.IsTrue(_controller.Ghost.activeSelf, "the ghost shows where it will go");
            _controller.PointerUp(at);
            Assert.AreEqual(1, _home.Design.decorations.Count);
            yield return null;
            Assert.IsNotNull(_home.Decor.ObjectFor(_home.Design.decorations[0].uid), "placed in the scene");

            Assert.IsTrue(_controller.Undo());
            Assert.AreEqual(0, _home.Design.decorations.Count);
            Assert.IsTrue(_controller.Redo());
            Assert.AreEqual(1, _home.Design.decorations.Count);

            _mode.Done();
            HouseState saved = new HouseStore().Load();
            Assert.AreEqual(1, saved.design.decorations.Count, "Done writes house.json");
            Assert.AreEqual("pot_marigold", saved.design.decorations[0].item);
        }

        [UnityTest]
        public IEnumerator WrongSpot_ShowsARedGhostWithAReason()
        {
            yield return Load();
            yield return OpenAndSettle(DecorationController.View.House);
            _controller.Arm("charpai");
            Vector2 at = Aim(0.5f, 0.68f);
            _controller.PointerDown(at);
            Assert.IsNotNull(_controller.Preview);
            Assert.IsFalse(_controller.Preview.Ok, "a charpai cannot go on the house front");
            Assert.IsNotEmpty(_controller.Message);
            _controller.PointerUp(at);
            Assert.AreEqual(0, _home.Design.decorations.Count, "nothing placed");
        }

        [UnityTest]
        public IEnumerator SelectMoveRecolourAndDelete()
        {
            yield return Load();
            yield return OpenAndSettle();
            _controller.Arm("tulsi_planter");
            Vector2 at = Aim(0.5f, 0.48f);
            Tap(at);
            _controller.Disarm();
            Assert.AreEqual(1, _home.Design.decorations.Count);
            int uid = _home.Design.decorations[0].uid;
            yield return null;
            Physics.SyncTransforms();

            Vector3 screen = Camera.main.WorldToScreenPoint(_home.Decor.ObjectFor(uid).transform.position + Vector3.up * 0.4f);
            Tap(screen);
            Assert.AreEqual(uid, _controller.Selected, "tapping an item selects it");

            float x0 = _home.Design.Find(uid).x;
            Vector2 to = Aim(0.3f, 0.48f);
            _controller.PointerDown(screen);
            _controller.PointerMove(Vector2.Lerp(screen, to, 0.5f));
            _controller.PointerMove(to);
            _controller.PointerUp(to);
            Assert.AreNotEqual(x0, _home.Design.Find(uid).x, "dragging moves it");

            Assert.IsTrue(_controller.RecolourSelected());
            Assert.AreEqual(1, _home.Design.Find(uid).variant);
            Assert.IsTrue(_controller.DuplicateSelected());
            Assert.AreEqual(2, _home.Design.decorations.Count);
            Assert.IsTrue(_controller.DeleteSelected());
            Assert.AreEqual(1, _home.Design.decorations.Count);
            Assert.AreEqual(5, _home.Editor.UndoCount, "place, move, colour, copy and delete: one undo step each");
        }

        [UnityTest]
        public IEnumerator HouseTab_SwapsAModuleAndColours()
        {
            yield return Load();
            yield return OpenAndSettle(DecorationController.View.House);
            _mode.ShowTab(1);
            MeshRenderer door = _home.House.RendererAt("door");
            Assert.IsTrue(_controller.Apply(new SetModule("door", "door_carved_arch")));
            Assert.AreNotSame(door, _home.House.RendererAt("door"));
            Assert.IsTrue(_controller.Apply(new SetPaint(3, 1, 0)));
            Assert.AreEqual(3, _home.Design.wallColour);
            Assert.IsTrue(_controller.Undo());
            Assert.IsTrue(_controller.Undo());
            Assert.AreEqual("door_wooden_panel", _home.Design.ModuleAt("door").module);
            _mode.Done();
        }

        [UnityTest]
        public IEnumerator Shelf_FiltersByCategoryAndFestival()
        {
            yield return Load();
            yield return OpenAndSettle();
            _mode.FilterShelf(0, (int)DecorCategory.Rangoli);
            Assert.AreEqual(5, _mode.Shelf.Count);
            _mode.FilterShelf(2, (int)DecorCategory.Rangoli);
            CollectionAssert.AreEquivalent(new[] { "rangoli_diya_ring", "rangoli_mandala_tihar" }, _mode.Shelf, "Tihar rangoli");
            _mode.Done();
        }

        [UnityTest]
        public IEnumerator Layouts_SaveAndLoad()
        {
            yield return Load();
            yield return OpenAndSettle();
            _controller.Apply(new SetPaint(4, 0, 0));
            _mode.SaveLayout(0);
            _controller.Apply(new SetPaint(6, 0, 0));
            _mode.LoadLayout(0);
            Assert.AreEqual(4, _home.Design.wallColour);
            Assert.AreEqual(1, new HouseStore().Load().layouts.Count, "a saved layout is written straight away");
            _mode.Done();
        }

        [UnityTest]
        public IEnumerator KilledMidEdit_TheEditComesBackOnRestart()
        {
            yield return Load();
            yield return OpenAndSettle();
            _controller.Arm("diya_brass_stand");
            Tap(Aim());
            Assert.AreEqual(1, _home.Design.decorations.Count);
            Assert.IsTrue(_controller.Apply(new SetModule("roof", "roof_tiled_slope")));

            // No Done: the scene is torn down as if the app were killed, then started again.
            yield return Load(fresh: false);
            Assert.Greater(_home.Recovered, 0, "the journal was replayed");
            Assert.AreEqual(1, _home.Design.decorations.Count, "the placed lamp is back");
            Assert.AreEqual("roof_tiled_slope", _home.Design.ModuleAt("roof").module);
            Assert.IsTrue(_home.Editor.CanUndo, "and can still be undone");
            Assert.IsNotNull(_home.Decor.ObjectFor(_home.Design.decorations[0].uid), "shown in the courtyard");
        }
    }
}
