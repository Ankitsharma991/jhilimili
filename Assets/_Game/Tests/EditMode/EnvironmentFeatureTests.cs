using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Tests
{
    /// <summary>Phase 8 step 6: each environment's landmarks and its painted preview card.</summary>
    public class EnvironmentFeatureTests
    {
        [Test]
        public void EveryEnvironment_HasItsOwnLandmarks()
        {
            foreach (EnvironmentInfo env in Environments.All)
            {
                HouseMesh m = EnvironmentFeatures.Build(SettlementLibrary.For(env.Id));
                Assert.Greater(m.TriangleCount, 0, env.Id);
                Assert.LessOrEqual(m.TriangleCount, 12000, $"{env.Id}: landmarks stay cheap");
                HouseGeneratorTests.AssertWellFormed(m, env.Id);
            }

            HouseMesh river = EnvironmentFeatures.Build(SettlementLibrary.For(Environments.Riverside));
            Assert.Greater(river.Triangles[(int)HousePaint.Flame].Count, 0, "floating diyas on the river");
            Assert.Less(river.Bounds().Min.Y, -1f, "steps down to the water");
        }

        [Test]
        public void Landmarks_AreWalkedRound()
        {
            SettlementLayout village = SettlementLibrary.For(Environments.HillVillage);
            var nav = new WalkNavigator(village, HouseDesign.Default());
            V3 c = EnvironmentFeatures.ChautariCentre(village);
            Assert.IsFalse(nav.IsWalkable(c), "nobody walks through the chautari");
            var west = new V3(c.X - 2.5f, 0f, c.Z);
            var east = new V3(c.X + 2.5f, 0f, c.Z);
            Assert.IsTrue(nav.IsWalkable(west) && nav.IsWalkable(east));
            Assert.IsFalse(nav.Straight(west, east));
            Assert.IsTrue(nav.Slide(west, new V3(0.2f, 0f, 0.2f)).X >= west.X, "the joystick slides along it");
        }

        [Test]
        public void Previews_AreSeededAndDistinct()
        {
            var fingerprints = new HashSet<string>();
            foreach (EnvironmentInfo env in Environments.All)
            {
                PaintCanvas a = EnvironmentPreviews.Paint(env, 128, 72), b = EnvironmentPreviews.Paint(env, 128, 72);
                CollectionAssert.AreEqual(a.Pixels, b.Pixels, $"{env.Id}: the same card every build");
                Assert.AreEqual(128 * 72 * 4, a.Pixels.Length);
                Rgb sky = a.Get(64, 4, out float _);
                Assert.Less(sky.R + sky.G + sky.B, 0.9f, "a night sky");
                Assert.IsTrue(fingerprints.Add(string.Join(",", a.Pixels.Where((_, i) => i % 97 == 0).Select(v => v.ToString("0.00")))), env.Id);
            }

            PaintCanvas river = EnvironmentPreviews.Paint(Environments.Find(Environments.Riverside), 128, 72);
            float bright = 0f;
            for (int x = 0; x < 128; x++)
            {
                Rgb p = river.Get(x, 68, out float _);
                bright = System.Math.Max(bright, p.R);
            }

            Assert.Greater(bright, 0.5f, "diyas glow on the river");
        }
    }
}
