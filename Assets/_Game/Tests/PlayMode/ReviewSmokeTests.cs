using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Engagement;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 5 step 4 in the real Main scene: the review card comes after a good session, not after an ad.</summary>
    public class ReviewSmokeTests
    {
        private static IEnumerator Load(System.Action<ReviewService, DevReviewBackend> got)
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out ReviewService review));
            var dev = review.Backend as DevReviewBackend;
            Assert.IsNotNull(dev, "the editor uses the development card");
            review.ResetForTesting();
            got(review, dev);
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AfterTenPayoffs_TheCardAppearsOnceAtAQuietMoment()
        {
            ReviewService review = null;
            DevReviewBackend dev = null;
            yield return Load((r, d) => { review = r; dev = d; });
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsFalse(dev.Visible, "no prompt before the session has gone well");
            Assert.AreEqual(ReviewBlock.NotAGoodSession, review.LastBlock);

            for (int i = 0; i < ReviewPolicy.GoodSessionPayoffs; i++)
            {
                Game.Events.Publish(new PayoffCompleteEvent("test"));
            }

            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsFalse(dev.Visible, "waits for the last burst to settle");
            yield return WaitFor(() => dev.Visible, 5f);
            Assert.IsTrue(dev.Visible, "asked once the moment is quiet");
            Assert.IsTrue(review.AskedThisSession);
            Assert.Greater(review.State.lastPromptUnix, 0);

            dev.Close();
            Game.Events.Publish(new PayoffCompleteEvent("test"));
            yield return new WaitForSecondsRealtime(4.5f);
            Assert.IsFalse(dev.Visible, "never twice in a session");
        }

        [UnityTest]
        public IEnumerator RightAfterAnAd_TheCardWaits()
        {
            ReviewService review = null;
            DevReviewBackend dev = null;
            yield return Load((r, d) => { review = r; dev = d; });
            Game.Events.Publish(new AdEvent("ad_closed", AdKind.Rewarded, "unlock_peacock_fountain", -1));
            for (int i = 0; i < ReviewPolicy.GoodSessionPayoffs; i++)
            {
                Game.Events.Publish(new PayoffCompleteEvent("test"));
            }

            yield return new WaitForSecondsRealtime(5f);
            Assert.IsFalse(dev.Visible);
            Assert.AreEqual(ReviewBlock.TooSoonAfterAd, review.LastBlock);
        }
    }
}
