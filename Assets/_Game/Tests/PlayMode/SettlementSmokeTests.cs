using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 8 step 2: the settlement builds around the home lot and its chunks follow the player (F19).</summary>
    public class SettlementSmokeTests
    {
        private SettlementView _view;

        private IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out _view), "the settlement in the Main scene");
        }

        [UnityTest]
        public IEnumerator Settlement_BuildsEveryNeighbourAroundTheHome()
        {
            yield return Load();
            SettlementLayout layout = _view.Layout;
            Assert.AreEqual(layout.lots.Count - 1, _view.LotCount, "every lot but the player's own");
            int full = 0;
            foreach (LotDef lot in layout.lots)
            {
                if (lot.home)
                {
                    continue;
                }

                ChunkDetail d = _view.DetailOf(lot.id);
                full += d == ChunkDetail.Full ? 1 : 0;
                Assert.AreNotEqual(ChunkDetail.Unloaded, d, $"{lot.id} is visible from the courtyard");
            }

            Assert.Greater(full, 0, "the lots across the open space near the home are fully built");
            Assert.LessOrEqual(_view.Shared.Materials.Count, 80, "materials are shared across the settlement");
            Assert.IsTrue(Physics.Raycast(new Vector3(0f, 5f, 6.8f), Vector3.down, out RaycastHit hit, 10f), "the street is walkable ground");
            Assert.AreEqual(0f, hit.point.y, 0.05f);
        }

        [UnityTest]
        public IEnumerator WalkingAway_LowersTheDetailBehind()
        {
            yield return Load();
            Vector3 at = Vector3.zero;
            _view.Focus = () => at;
            at = new Vector3(SettlementLibrary.LotSpacing * 4.3f, 0f, 6.8f); // past the high tier's simple radius for the far end
            yield return new WaitForSecondsRealtime(SettlementView.PlanInterval * 2f + 0.05f);
            Assert.That(_view.DetailOf("lotm3"), Is.EqualTo(ChunkDetail.Impostor).Or.EqualTo(ChunkDetail.Unloaded), "the far end of the street is cheap");
            Assert.AreNotEqual(ChunkDetail.Unloaded, _view.DetailOf("lotp2"), "the lots beside the player are built");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Environments_CanBeSwapped()
        {
            yield return Load();
            foreach (EnvironmentInfo env in Environments.All)
            {
                _view.Load(env.Id);
                yield return null;
                Assert.AreEqual(env.Id, _view.Layout.environment);
                Assert.AreEqual(_view.Layout.lots.Count - 1, _view.LotCount);
            }
        }
    }
}
