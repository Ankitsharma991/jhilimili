using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>
    /// Phase 7 step 6: a courtyard decorated to each tier's full budget stays within that tier's triangles and shares a
    /// small set of materials (PRD 9.1 budgets, PRD 14 performance); the house's lights are on only in decoration mode.
    /// </summary>
    public class HomeBudgetSmokeTests
    {
        [TearDown]
        public void TearDown() => new HouseStore().Delete();

        [UnityTest]
        public IEnumerator FullBudget_StaysWithinTrianglesAndSharesMaterials()
        {
            new HouseStore().Delete();
            SceneManager.LoadScene("Main");
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out HomeService home));
            for (int tier = 0; tier < DecorBudget.Units.Length; tier++)
            {
                home.Design.decorations.Clear();
                int units = HomeLoad.FillToBudget(home.Design, tier);
                home.Refresh();
                yield return null;
                long triangles = 0;
                foreach (MeshFilter f in home.Decor.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh m = f.sharedMesh;
                    for (int s = 0; s < m.subMeshCount; s++)
                    {
                        triangles += m.GetIndexCount(s) / 3;
                    }
                }

                Debug.Log($"[UTSAV_PERF] tier {tier}: {home.Design.decorations.Count} decorations, {units} units, {triangles} triangles, {home.Decor.MaterialCount} materials");
                Assert.Greater(units, DecorBudget.Units[tier] - 7, "filled to the budget");
                Assert.LessOrEqual(triangles, DecorBudget.Triangles[tier], $"tier {tier} triangles");
                Assert.LessOrEqual(home.Decor.MaterialCount, 60, "materials are shared per paint and colour, not per item");
            }
        }

        [UnityTest]
        public IEnumerator HouseLights_AreOnlyOnInDecorationMode()
        {
            new HouseStore().Delete();
            SceneManager.LoadScene("Main");
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out HomeService home));
            Light[] lights = home.House.GetComponentsInChildren<Light>(true);
            Assert.AreEqual(2, lights.Length);
            Assert.IsTrue(System.Array.TrueForAll(lights, l => !l.enabled), "off in the fireworks view");
            UI.DecorationMode mode = Object.FindFirstObjectByType<UI.DecorationMode>();
            mode.Open();
            Assert.IsTrue(System.Array.TrueForAll(lights, l => l.enabled), "on while decorating");
            mode.Done();
            Assert.IsTrue(System.Array.TrueForAll(lights, l => !l.enabled), "off again");
        }
    }
}
