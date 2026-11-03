using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.District;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 12 step 3: from the lane into the bazaar; tiles stream round the player; buildings and the river stop you.</summary>
    public class DistrictSmokeTests
    {
        [UnityTest]
        public IEnumerator FromTheLaneIntoTheBazaar_TilesStream_WallsHold()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(1);
            try
            {
                Assert.IsTrue(Game.Services.TryGet(out DistrictView district));
                var mode = Object.FindFirstObjectByType<ExploreMode>();
                Assert.IsTrue(mode.Open());
                yield return null;
                ExploreController c = mode.Controller;

                // The east end of the lane's street, running east: into the district.
                c.Teleport(new Vector3(24f, 0f, 6.8f), 90f);
                district.Refresh();
                Assert.IsTrue(district.Active, "the bazaar is beside the town lane");
                mode.ToggleSprint();
                mode.Stick.Set(new Vector2(0f, 1f));
                yield return Seconds(2.2f);
                Assert.Greater(c.Player.position.x, 32f, "through the join, onto the bazaar road");
                Assert.IsTrue(district.IsWalkable(c.Player.position));

                // Tiles round the player: full here, with colliders.
                (int tx, int tz) = district.Plan.TileOf(c.Player.position.x, c.Player.position.z);
                district.Refresh();
                Assert.AreEqual(TileDetail.Full, district.DetailOf(tx, tz));
                Assert.IsNotNull(GameObject.Find($"DistrictTile_{tx}_{tz}"));
                Assert.IsNotNull(GameObject.Find($"DistrictSolids_{tx}_{tz}"), "with its colliders");
                Assert.Less(district.Triangles, 400000, "inside a mid phone's budget");
                Debug.Log($"[UTSAV_PERF] bazaar triangles drawn={district.Triangles} held={district.HeldTriangles} tiles={district.BuiltTiles}");

                // A building's front: pushing into it, the character stays outside.
                DistrictBuilding b = district.Plan.Buildings.Find(x => x.Front == Facing.South && x.Footprint.CentreX > 40f && x.Footprint.CentreX < 140f);
                c.Teleport(new Vector3(b.Footprint.CentreX, 0f, b.Footprint.Z0 - 1.5f), 0f);
                district.Refresh();
                yield return null;
                mode.Stick.Set(new Vector2(0f, 1f));
                yield return Seconds(1f);
                Assert.IsFalse(b.Footprint.Contains(c.Player.position.x, c.Player.position.z, -0.05f), "not inside the building");

                // The ghat: walking north into the river stops at the water.
                c.Teleport(new Vector3(200f, 0f, 112f), 0f);
                district.Refresh();
                yield return null;
                yield return Seconds(1.5f);
                Assert.Less(c.Player.position.z, district.Plan.River.Z0, "the river stops you");

                mode.Stick.Set(Vector2.zero);
                mode.Exit();
                yield return null;
                district.Refresh();
                Assert.IsFalse(district.Active, "back in the courtyard, the bazaar is put away");
                Assert.AreEqual(0, district.HeldTriangles, "and freed");
            }
            finally
            {
                quality.UseAutomaticTier();
            }
        }

        private static IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }
    }
}
