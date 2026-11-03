using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.District;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 12 step 4: the bazaar has people round the player, a shopkeeper at the stall, and they react to a bang.</summary>
    public class CrowdSmokeTests
    {
        [UnityTest]
        public IEnumerator PeopleRoundThePlayer_AShopkeeper_AndTheyReactToABang()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(2);
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out DistrictView district));
                Assert.IsTrue(Game.Services.TryGet(out CrowdView crowd));
                Assert.IsTrue(mode.Open());
                yield return null;

                // In front of a stall, on the road.
                DistrictProp stall = district.Plan.Props.Find(p => p.Kind == DistrictProp.Stall);
                float road = stall.Front == Facing.North ? 2.6f : -2.6f;
                mode.Controller.Teleport(new Vector3(stall.X, 0f, stall.Z + road), stall.Front == Facing.North ? 180f : 0f);
                district.Refresh();
                for (int f = 0; f < 120 && (crowd.PoolSize == 0 || crowd.Built < crowd.PoolSize); f++)
                {
                    yield return null;
                }

                Assert.AreEqual(CrowdView.HighPool, crowd.PoolSize, "a full pool on the high tier");
                Assert.AreEqual(crowd.PoolSize, crowd.Built);
                Vector3 player = mode.Controller.Player.position;
                crowd.Assign(player);
                yield return null;
                Assert.Greater(crowd.Drawn, 3, "people round the player");
                Debug.Log($"[UTSAV_PERF] bazaar crowd drawn={crowd.Drawn} pool={crowd.PoolSize} people={crowd.Sim.People.Length}");

                Person[] people = crowd.Sim.People;
                int keeper = -1;
                for (int i = 0; i < people.Length; i++)
                {
                    AvatarView v = crowd.ViewOf(i);
                    if (v == null)
                    {
                        continue;
                    }

                    Assert.Less(Vector3.Distance(v.transform.position, player), CrowdView.DrawRange + 4f, "drawn only near the player");
                    Assert.IsTrue(district.Plan.IsWalkable(people[i].X, people[i].Z, 0.2f) || people[i].Shopkeeper, "on the footpaths");
                    if (people[i].Shopkeeper && Mathf.Abs(people[i].X - stall.X) < 0.5f && Mathf.Abs(people[i].Z - stall.Z) < 2f)
                    {
                        keeper = i;
                    }
                }

                Assert.GreaterOrEqual(keeper, 0, "the stall has its shopkeeper, drawn");

                // A loud cracker right by the stall: the shopkeeper covers their ears.
                Game.Events.Publish(new PayoffStartedEvent("bomb_test", new Vector3(stall.X, 0f, stall.Z + road), loud: true));
                Assert.AreEqual(PersonState.CoveringEars, people[keeper].State);
                yield return null;
                yield return null;
                Assert.AreEqual(AvatarClip.CoverEars, crowd.ViewOf(keeper).GetComponent<AvatarAnimator>().Clip, "and the avatar shows it");

                mode.Exit();
                yield return null;
                district.Refresh();
                yield return null;
                Assert.AreEqual(0, crowd.PoolSize, "out of the bazaar, the crowd is put away");
                Assert.IsNull(GameObject.Find("BazaarPerson0"));
            }
            finally
            {
                if (mode.Controller != null && mode.Controller.IsOpen)
                {
                    mode.Exit();
                }

                quality.UseAutomaticTier();
            }
        }
    }
}
