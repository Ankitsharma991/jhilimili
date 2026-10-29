using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 8 step 3: walking out of the courtyard, the follow camera, fast travel and coming home (F19).</summary>
    public class WalkModeSmokeTests
    {
        private WalkMode _mode;
        private WalkController _walk;

        private IEnumerator Load()
        {
            new HouseStore().Delete();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _mode = Object.FindFirstObjectByType<WalkMode>();
            Assert.IsNotNull(_mode, "walk mode in the HUD");
            _walk = _mode.Controller;
        }

        [TearDown]
        public void TearDown() => new HouseStore().Delete();

        [UnityTest]
        public IEnumerator Walk_OpensTheGateFollowsTheAvatarAndComesHome()
        {
            yield return Load();
            Assert.IsTrue(Game.Services.TryGet(out HomeService home));
            CameraRig rig = Object.FindFirstObjectByType<CameraRig>();
            _mode.Open();
            Assert.IsTrue(_mode.IsOpen);
            Assert.IsNotNull(_walk.Avatar, "the player's avatar walks");
            Assert.IsTrue(home.House.GateOpen, "the gate swings open");
            Assert.IsTrue(InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "the lighter ignores touches");
            yield return new WaitForSeconds(CameraRig.OverrideSeconds + 0.1f);
            Assert.AreEqual(1f, rig.OverrideWeight, 1e-3f);
            float toAvatar = Vector3.Distance(Camera.main.transform.position, _walk.Avatar.transform.position);
            Assert.That(toAvatar, Is.InRange(1f, WalkController.MaxDistance + 2f), "the camera follows close behind");

            // Out through the gate to the street.
            Assert.IsTrue(_walk.WalkTo(new Vector3(-6f, 0f, 6.8f)));
            for (int i = 0; i < 400 && _walk.Agent.Walking; i++)
            {
                _walk.Advance(0.05f);
            }

            Assert.IsFalse(_walk.Agent.Walking, "arrived");
            yield return null; // the camera rig applies the new follow pose on its next tick
            yield return null;
            Assert.AreEqual(6.8f, _walk.Avatar.transform.position.z, 0.05f, "on the street");
            Assert.Greater(Camera.main.transform.position.z, 3.8f - 1f, "the camera came out with the avatar");

            _mode.Home();
            yield return null;
            Assert.IsFalse(home.House.GateOpen, "the gate shuts behind");
            Assert.IsTrue(_walk.Avatar == null, "the walking avatar is gone");
            yield return new WaitForSeconds(CameraRig.OverrideSeconds + 0.1f);
            Assert.AreEqual(0f, rig.OverrideWeight, 1e-3f, "back to the fireworks view");
        }

        [UnityTest]
        public IEnumerator Map_FastTravelsToEverySpotThatIsOpen()
        {
            yield return Load();
            _mode.Open();
            _mode.ToggleMap();
            Assert.IsTrue(_mode.MapOpen);
            Assert.AreEqual(_walk.Layout.spots.Count, _mode.PinCount, "a pin per spot");
            Assert.IsTrue(Game.Services.TryGet(out SettlementView settlement));
            foreach (SpotDef spot in _walk.Layout.spots)
            {
                bool ok = _walk.FastTravel(spot.id);
                Assert.IsTrue(ok, spot.id);
                Assert.AreEqual(1f, _walk.Fade, 1e-3f, "a fade");
                Assert.IsTrue(_walk.Navigator.IsWalkable(_walk.Agent.Position), spot.id);
            }

            _walk.FastTravel("open");
            yield return new WaitForSecondsRealtime(SettlementView.PlanInterval * 2f + 0.05f);
            Assert.AreEqual(_walk.Agent.Position.X, settlement.Focus().x, 1e-3f, "the settlement's detail follows the avatar");
            _mode.Home();
        }

        [UnityTest]
        public IEnumerator Tap_WalksThere_AndDragLooksAround()
        {
            yield return Load();
            _mode.Open();
            yield return new WaitForSeconds(CameraRig.OverrideSeconds + 0.1f);
            Vector3 target = _walk.Avatar.transform.position + _walk.Avatar.transform.forward * 1.5f;
            Vector3 screen = Camera.main.WorldToScreenPoint(target);
            Assert.Greater(screen.z, 0f, "the courtyard floor ahead is in view");
            _walk.PointerDown(screen);
            _walk.PointerUp(screen);
            Assert.IsTrue(_walk.Agent.Walking, "a tap on the ground walks there");

            Quaternion before = Camera.main.transform.rotation;
            _walk.PointerDown(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            _walk.PointerMove(new Vector2(Screen.width * 0.7f, Screen.height * 0.5f));
            _walk.PointerUp(new Vector2(Screen.width * 0.7f, Screen.height * 0.5f));
            yield return null;
            yield return null;
            Assert.Greater(Quaternion.Angle(before, Camera.main.transform.rotation), 5f, "a drag turns the view");
            _mode.Home();
        }

        [UnityTest]
        public IEnumerator Joystick_MovesAndCannotLeaveTheWalkableGround()
        {
            yield return Load();
            _mode.Open();
            if (!_mode.JoystickOn)
            {
                _mode.ToggleJoystick();
            }

            _walk.Stick = new Vector2(0f, -1f);
            for (int i = 0; i < 200; i++)
            {
                _walk.Advance(0.05f);
            }

            Assert.IsTrue(_walk.Navigator.IsWalkable(_walk.Agent.Position), "stopped at the edge, not in the house");
            _walk.Stick = Vector2.zero;
            _mode.ToggleJoystick();
            _mode.Home();
        }
    }
}
