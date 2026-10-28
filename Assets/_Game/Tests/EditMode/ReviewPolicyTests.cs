using NUnit.Framework;
using UtsavNights.Core.Engagement;

namespace UtsavNights.Tests
{
    /// <summary>Phase 5 step 4: ask for a rating after a good session, never after an ad, at most once a month.</summary>
    public class ReviewPolicyTests
    {
        private const long Now = 1_800_000_000L;

        private static ReviewState Returning() => new ReviewState { sessions = 2 };

        private static ReviewMoment GoodQuietMoment() => new ReviewMoment
        {
            NowUnix = Now,
            FinalesCompleted = 1,
            Payoffs = 4,
            SecondsSinceAd = float.PositiveInfinity,
            SecondsSincePayoff = 5f,
            SecondsSinceTouch = 4f,
        };

        [Test]
        public void GoodSession_QuietMoment_SecondLaunch_Asks()
        {
            Assert.AreEqual(ReviewBlock.None, ReviewPolicy.Check(Returning(), GoodQuietMoment()));
        }

        [Test]
        public void NeverOnTheFirstLaunch()
        {
            var state = new ReviewState();
            ReviewPolicy.StartSession(state);
            Assert.AreEqual(ReviewBlock.FirstSession, ReviewPolicy.Check(state, GoodQuietMoment()));
            ReviewPolicy.StartSession(state);
            Assert.AreEqual(ReviewBlock.None, ReviewPolicy.Check(state, GoodQuietMoment()));
            Assert.AreEqual(ReviewBlock.FirstSession, ReviewPolicy.Check(null, GoodQuietMoment()));
        }

        [Test]
        public void GoodSession_IsAFinaleOrTenPayoffs()
        {
            ReviewMoment m = GoodQuietMoment();
            m.FinalesCompleted = 0;
            m.Payoffs = ReviewPolicy.GoodSessionPayoffs - 1;
            Assert.AreEqual(ReviewBlock.NotAGoodSession, ReviewPolicy.Check(Returning(), m));
            m.Payoffs = ReviewPolicy.GoodSessionPayoffs;
            Assert.AreEqual(ReviewBlock.None, ReviewPolicy.Check(Returning(), m));
        }

        [Test]
        public void NeverWithinTwoMinutesOfAnAd()
        {
            ReviewMoment m = GoodQuietMoment();
            m.SecondsSinceAd = ReviewPolicy.AdQuietSeconds - 1f;
            Assert.AreEqual(ReviewBlock.TooSoonAfterAd, ReviewPolicy.Check(Returning(), m));
            m.SecondsSinceAd = ReviewPolicy.AdQuietSeconds + 1f;
            Assert.AreEqual(ReviewBlock.None, ReviewPolicy.Check(Returning(), m));
        }

        [Test]
        public void WaitsForAQuietMoment()
        {
            ReviewMoment busy = GoodQuietMoment();
            busy.Busy = true;
            Assert.AreEqual(ReviewBlock.Busy, ReviewPolicy.Check(Returning(), busy));

            ReviewMoment burst = GoodQuietMoment();
            burst.SecondsSincePayoff = 1f;
            Assert.AreEqual(ReviewBlock.Settling, ReviewPolicy.Check(Returning(), burst));

            ReviewMoment touching = GoodQuietMoment();
            touching.SecondsSinceTouch = 0.5f;
            Assert.AreEqual(ReviewBlock.Settling, ReviewPolicy.Check(Returning(), touching));
        }

        [Test]
        public void OncePerSessionAndOncePerThirtyDays()
        {
            ReviewState state = Returning();
            ReviewPolicy.RecordPrompt(state, Now);
            Assert.AreEqual(1, state.prompts);

            ReviewMoment same = GoodQuietMoment();
            same.AskedThisSession = true;
            Assert.AreEqual(ReviewBlock.AskedThisSession, ReviewPolicy.Check(state, same));

            ReviewMoment later = GoodQuietMoment();
            later.NowUnix = Now + ReviewPolicy.CooldownSeconds - 60;
            Assert.AreEqual(ReviewBlock.Cooldown, ReviewPolicy.Check(state, later));
            later.NowUnix = Now + ReviewPolicy.CooldownSeconds;
            Assert.AreEqual(ReviewBlock.None, ReviewPolicy.Check(state, later));
        }
    }
}
