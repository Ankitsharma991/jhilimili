using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Crackers;
using UtsavNights.Core.Placement;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Placement in the real Main scene: valid ground, blocked spots with reasons, spacing, limit, and recycling after use.</summary>
    public class PlacementSmokeTests
    {
        private PlacementController _placement;
        private CrackerSpawner _spawner;
        private CrackerRuntime _scenePot;
        private Camera _camera;

        private IEnumerator LoadMain()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out _placement), "placement installed");
            Assert.IsTrue(Game.Services.TryGet(out _spawner), "spawner installed");
            _scenePot = SceneCrackers.Find("flowerpot_gold");
            _camera = Camera.main;
        }

        private Vector2 Screen(Vector3 world) => _camera.WorldToScreenPoint(world);

        [UnityTest]
        public IEnumerator Placement_ValidAndBlockedSpots()
        {
            yield return LoadMain();
            _placement.BeginPlacing(_scenePot.Definition);
            Assert.IsTrue(_placement.IsPlacing);

            PlacementCheck ok = _placement.CheckAt(Screen(new Vector3(1.1f, 0f, 0.4f)));
            Assert.IsTrue(ok.Valid, $"open floor should be valid, got {ok.Block}");

            Assert.AreEqual(PlacementBlock.TooCloseToItem, _placement.CheckAt(Screen(_scenePot.transform.position + new Vector3(0.15f, 0f, 0f))).Block);
            Assert.AreEqual(PlacementBlock.WrongSurface, _placement.CheckAt(Screen(new Vector3(0f, 1.6f, 3.42f))).Block, "the back wall is not ground");
            Assert.AreEqual(PlacementBlock.NoSurface, _placement.CheckAt(new Vector2(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height - 2f)).Block, "sky");

            CrackerRuntime placed = _placement.PlaceAt(Screen(new Vector3(1.1f, 0f, 0.4f)));
            Assert.IsNotNull(placed);
            Assert.AreEqual(CrackerState.Placed, placed.StateMachine.State);
            Assert.IsFalse(_placement.IsPlacing, "placement ends after one item");
            Assert.AreEqual(2, _spawner.ActiveCount, "starting pot plus the new one");
            Assert.IsTrue(placed.CanIgnite, "placed crackers can be lit");
        }

        [UnityTest]
        public IEnumerator Placement_RespectsItemLimit()
        {
            yield return LoadMain();
            _placement.StayAfterPlacing = true;
            _placement.BeginPlacing(_scenePot.Definition);
            int limit = _spawner.ItemLimit;
            for (float x = -2.6f; x <= 2.6f && _spawner.ActiveCount < limit; x += 0.6f)
            {
                for (float z = -0.6f; z <= 1.9f && _spawner.ActiveCount < limit; z += 0.6f)
                {
                    _placement.PlaceAt(Screen(new Vector3(x, 0f, z)));
                }
            }

            Assert.AreEqual(limit, _spawner.ActiveCount, "enough open floor for the tier limit (starting pot included)");
            Assert.AreEqual(PlacementBlock.TooManyItems, _placement.CheckAt(Screen(new Vector3(0f, 0f, -1.2f))).Block);
            _placement.Cancel();
            _spawner.ClearAll();
            Assert.AreEqual(0, _spawner.ActiveCount);
        }

        [UnityTest]
        public IEnumerator SpentCracker_ReturnsToPool_AndIsReused()
        {
            yield return LoadMain();
            _placement.BeginPlacing(_scenePot.Definition);
            CrackerRuntime first = _placement.PlaceAt(Screen(new Vector3(-1.2f, 0f, 0.5f)));
            Assert.IsNotNull(first);
            first.Ignite();

            float end = Time.realtimeSinceStartup + 20f;
            while (first.gameObject.activeSelf && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.AreEqual(1, _spawner.ActiveCount, "spent cracker cleared after its cooldown; the starting pot remains");
            Assert.IsFalse(first.gameObject.activeSelf);

            _placement.BeginPlacing(_scenePot.Definition);
            CrackerRuntime second = _placement.PlaceAt(Screen(new Vector3(-1.2f, 0f, 0.5f)));
            Assert.AreSame(first, second, "the pooled instance is reused");
            Assert.AreEqual(2, _spawner.InstancesCreated, "starting pot plus one pooled flowerpot");
            Assert.AreEqual(CrackerState.Placed, second.StateMachine.State);
        }
    }
}
