using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Content;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Placement;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>
    /// Phase 8 step 4: celebration spots in the real Main scene (F20): the fireworks view and placement move to a spot;
    /// ground crackers go anywhere in its area; aerials follow its rule (pads in the street, none on the balcony, open sky
    /// elsewhere); a closed spot is refused with a reason.
    /// </summary>
    public class CelebrationSmokeTests
    {
        private CelebrationService _celebration;
        private PlacementController _placement;
        private CameraRig _rig;
        private CrackerDefinition _fountain;
        private CrackerDefinition _rocket;
        private CrackerDefinition _sparkler;

        private IEnumerator Load()
        {
            new HouseStore().Delete();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out _celebration), "celebration spots in the Main scene");
            Assert.IsTrue(Game.Services.TryGet(out _placement));
            _rig = Object.FindFirstObjectByType<CameraRig>();
            _fountain = SceneCrackers.Definition("flowerpot_gold");
            _rocket = SceneCrackers.Definition("rocket_basic");
            _sparkler = SceneCrackers.Definition("sparkler_classic");
            Assert.IsNotNull(_fountain);
            Assert.IsNotNull(_rocket);
            Assert.IsTrue(PlacementValidator.IsAerial(_rocket));
            Assert.IsFalse(PlacementValidator.IsAerial(_fountain));
        }

        [TearDown]
        public void TearDown() => new HouseStore().Delete();

        private PlacementBlock CheckAt(CrackerDefinition d, Vector3 world)
        {
            Ray ray = new Ray(world + Vector3.up * 2.5f, Vector3.down);
            _placement.Validator.MaxReach = 50f;
            PlacementCheck c = _placement.Validator.Check(d, ray, 0, 0);
            return c.Block;
        }

        [UnityTest]
        public IEnumerator EverySpot_MovesTheViewAndTakesGroundCrackers()
        {
            yield return Load();
            Assert.AreEqual("courtyard", _celebration.Current.id, "every session starts at home");
            foreach (SpotDef spot in _celebration.Layout.spots)
            {
                Assert.IsTrue(_celebration.Choose(spot.id), spot.id);
                yield return null;
                Assert.AreEqual(0f, Vector3.Distance(_rig.GroundShot.position, new Vector3(spot.camX, spot.camY, spot.camZ)), 1e-3f, spot.id);
                var centre = new Vector3(spot.x, spot.y, spot.z);
                Vector3 inArea = centre + new Vector3(spot.radius * 0.4f, 0f, 0f);
                // The balcony is small: a sparkler fits, a fountain's spray would hit the rails.
                CrackerDefinition ground = spot.kind == SpotKinds.Balcony ? _sparkler : _fountain;
                Assert.AreEqual(PlacementBlock.None, CheckAt(ground, inArea), $"{spot.id}: {ground.Id} in the play area");
                if (spot.kind == SpotKinds.Balcony)
                {
                    Assert.AreEqual(PlacementBlock.NoRoomForEffect, CheckAt(_fountain, inArea), "a fountain is too big for the balcony");
                }

                if (spot.kind != SpotKinds.Courtyard && spot.kind != SpotKinds.Balcony && spot.kind != SpotKinds.Rooftop)
                {
                    Vector3 outside = centre + new Vector3(0f, 0f, spot.radius + 0.8f); // toward the street: still ground, just outside
                    Assert.AreEqual(PlacementBlock.OutsideArea, CheckAt(_fountain, outside), $"{spot.id}: outside its area");
                }
            }
        }

        [UnityTest]
        public IEnumerator Aerials_FollowEachSpotsRule()
        {
            yield return Load();
            SettlementLayout layout = _celebration.Layout;

            Assert.IsTrue(_celebration.Choose("street"));
            SpotDef street = layout.Spot("street");
            Assert.AreEqual(PlacementBlock.AerialPadsOnly, CheckAt(_rocket, new Vector3(street.x - 1f, 0f, street.z)));
            Assert.AreEqual(PlacementBlock.None, CheckAt(_rocket, new Vector3(street.pads[0].x, 0f, street.pads[0].z)), "from a pad");

            Assert.IsTrue(_celebration.Choose("balcony"));
            SpotDef balcony = layout.Spot("balcony");
            Assert.AreEqual(PlacementBlock.AerialNotHere, CheckAt(_rocket, new Vector3(balcony.x, balcony.y, balcony.z)));

            Assert.IsTrue(_celebration.Choose("open"));
            SpotDef open = layout.Spot("open");
            Assert.AreEqual(PlacementBlock.None, CheckAt(_rocket, new Vector3(open.x, 0f, open.z)), "open sky in the open space");

            // The courtyard under the player's own string lights: the rocket would fly into them.
            Assert.IsTrue(Game.Services.TryGet(out HomeService home));
            home.Do(new PlaceDecor(new PlacedDecor
            {
                uid = home.Design.nextUid, item = "string_lights_warm", x = 3.425f, y = 1.4f, z = 3.5f, ex = -3.425f, ey = 1.4f, ez = 3.5f, sag = 0.1f, surface = Surface.Wall,
            }));
            Assert.IsTrue(_celebration.Choose("courtyard"));
            Assert.AreEqual(PlacementBlock.NoSky, CheckAt(_rocket, new Vector3(0f, 0f, HouseGeometry.WorldFrontZ + 3.5f)), "under the strand");
            Assert.AreEqual(PlacementBlock.None, CheckAt(_rocket, new Vector3(0f, 0f, 0.4f)), "clear of it");
            Assert.AreEqual(PlacementBlock.NoSky, CheckAt(_rocket, new Vector3(0f, 0f, HouseGeometry.WorldFrontZ + 0.7f)), "under the balcony");
        }

        [UnityTest]
        public IEnumerator ClosedSpots_AreRefusedWithAReason_AndAHouseChangeFallsBack()
        {
            yield return Load();
            Assert.IsTrue(Game.Services.TryGet(out HomeService home));
            Assert.IsTrue(_celebration.Choose("rooftop"));
            Assert.IsTrue(home.Do(new SetModule("roof", "roof_tiled_slope")));
            Assert.AreEqual("courtyard", _celebration.Current.id, "a sloping roof sends the celebration back to the courtyard");
            Assert.IsFalse(_celebration.Choose("rooftop"));
            StringAssert.Contains("roof slopes", _celebration.Availability("rooftop").Reason);

            var walk = Object.FindFirstObjectByType<WalkMode>();
            walk.Open();
            walk.ToggleMap();
            walk.ShowCard("rooftop");
            StringAssert.Contains("roof slopes", walk.CardReason, "the map's card says why");
            walk.Home();
        }

        [UnityTest]
        public IEnumerator CelebrateHere_EndsTheWalkAtTheSpot()
        {
            yield return Load();
            var walk = Object.FindFirstObjectByType<WalkMode>();
            walk.Open();
            Assert.IsTrue(walk.Controller.FastTravel("open"));
            yield return null;
            Assert.AreEqual("open", walk.HereSpot, "standing in the open space");
            Assert.IsTrue(walk.Celebrate(walk.HereSpot));
            Assert.IsFalse(walk.IsOpen, "the walk ends");
            Assert.AreEqual("open", _celebration.Current.id);
            yield return new WaitForSeconds(CameraRig.OverrideSeconds + 0.1f);
            SpotDef open = _celebration.Current;
            Assert.AreEqual(0f, Vector3.Distance(Camera.main.transform.position, new Vector3(open.camX, open.camY, open.camZ)), 0.05f, "the fireworks view at the spot");
        }
    }
}
