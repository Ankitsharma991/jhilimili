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
    /// <summary>Phase 8 step 6: the environment browser filters, switches the settlement and remembers the choice (PRD 12).</summary>
    public class EnvironmentBrowserSmokeTests
    {
        private EnvironmentBrowser _browser;
        private SettlementView _settlement;

        private IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _browser = Object.FindFirstObjectByType<EnvironmentBrowser>();
            Assert.IsNotNull(_browser, "the browser in the HUD");
            Assert.IsTrue(Game.Services.TryGet(out _settlement));
        }

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(NeighbourhoodInstaller.EnvironmentPrefKey);

        [UnityTest]
        public IEnumerator Filters_ShowTheMatchingPlaces()
        {
            PlayerPrefs.DeleteKey(NeighbourhoodInstaller.EnvironmentPrefKey);
            yield return Load();
            Assert.AreEqual(Environments.TownLane, _settlement.Layout.environment, "the town lane at first");
            _browser.Open();
            Assert.IsTrue(_browser.IsOpen);
            foreach (EnvironmentInfo env in Environments.All)
            {
                CollectionAssert.Contains(_browser.Shown, env.Id, "every built-in environment has a card");
            }

            _browser.Filter(2);
            CollectionAssert.AreEqual(new[] { Environments.HillVillage }, _browser.Shown, "Tihar");
            _browser.Filter(5);
            CollectionAssert.AreEqual(new[] { Environments.Riverside }, _browser.Shown, "East India");
            _browser.Close();
        }

        [UnityTest]
        public IEnumerator Choosing_BuildsThePlaceEndsTheWalkAndIsRemembered()
        {
            PlayerPrefs.DeleteKey(NeighbourhoodInstaller.EnvironmentPrefKey);
            yield return Load();
            var walk = Object.FindFirstObjectByType<WalkMode>();
            walk.Open();
            Assert.IsTrue(_browser.Choose(Environments.Riverside));
            Assert.IsFalse(walk.IsOpen, "the walk ends");
            Assert.AreEqual(Environments.Riverside, _settlement.Layout.environment);
            Assert.IsNotNull(_settlement.transform.Find("Ground/Features"), "the ghat and river");
            Assert.IsTrue(Game.Services.TryGet(out CelebrationService celebration));
            Assert.IsTrue(celebration.Availability(celebration.Current.id).Open, "the celebration spot is valid there");
            yield return null;

            Assert.IsTrue(_browser.Choose(Environments.HillVillage));
            Assert.AreEqual(Environments.HillVillage, _settlement.Layout.environment);

            yield return Load();
            Assert.AreEqual(Environments.HillVillage, _settlement.Layout.environment, "remembered on the next start");
        }
    }
}
