using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 4 step 2: the ad service in the real Main scene with the development ad card (F10).</summary>
    public class AdServiceSmokeTests
    {
        private readonly List<AdEvent> _events = new List<AdEvent>();

        [UnityTest]
        public IEnumerator Rewarded_BlockedWhileACrackerBurns_ShownWhenIdle()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out AdService ads), "ad service installed");
            Assert.IsTrue(Game.Services.TryGet(out ProgressService progress), "progress service installed");
            var card = (DevAdBackend)ads.Backend;
            ads.Policy.AdsRemoved = false;
            _events.Clear();
            Game.Events.Subscribe<AdEvent>(Collect);
            try
            {
                CrackerRuntime pot = SceneCrackers.Find("flowerpot_gold");
                Assert.IsNotNull(pot, "starting flowerpot");
                pot.Ignite();
                yield return null;

                AdOutcome? outcome = null;
                string message = null;
                ads.ShowRewarded("test_unlock", (o, m) => { outcome = o; message = m; });
                Assert.AreEqual(AdOutcome.Blocked, outcome, "no ad while a fuse burns (PRD 15.1)");
                StringAssert.Contains("fireworks", message);
                Assert.IsFalse(card.Visible);
                Assert.AreEqual(AdBlock.CrackerActive, _events[_events.Count - 1].Block);

                Object.FindFirstObjectByType<CrackerSpawner>().ClearAll();
                yield return null;
                outcome = null;
                ads.ShowRewarded("test_unlock", (o, m) => outcome = o);
                Assert.IsTrue(card.Visible, "test card shown");
                Assert.IsTrue(ads.IsShowing);
                Assert.IsTrue(InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "the card takes every touch");
                card.Finish(true);
                Assert.AreEqual(AdOutcome.Rewarded, outcome);
                Assert.IsFalse(ads.IsShowing);
                CollectionAssert.IsSubsetOf(new[] { "ad_shown", "ad_reward" }, _events.ConvertAll(e => e.Type));

                card.FailNext = true;
                ads.ShowRewarded("test_unlock", (o, m) => { outcome = o; message = m; });
                Assert.AreEqual(AdOutcome.Failed, outcome, "failures are reported, never thrown");
                Assert.IsFalse(ads.IsShowing);
            }
            finally
            {
                Game.Events.Unsubscribe<AdEvent>(Collect);
            }
        }

        [UnityTest]
        public IEnumerator Interstitial_NeverAtLaunchAndOffByDefault()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out AdService ads));
            Assert.IsFalse(ads.Policy.InterstitialsEnabled, "off for R0 by default");
            ads.Policy.InterstitialsEnabled = true;
            Assert.IsFalse(ads.MaybeShowInterstitial("test"), "never at launch");
            Assert.IsFalse(((DevAdBackend)ads.Backend).Visible);
            ads.Policy.InterstitialsEnabled = false;
        }

        private void Collect(AdEvent e) => _events.Add(e);
    }
}
