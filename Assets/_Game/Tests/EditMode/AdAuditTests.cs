using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Tests
{
    /// <summary>
    /// Phase 4 definition of done: zero ads at forbidden moments over 50 sessions. Each session is ten
    /// minutes of random play (taps, placing, lighting, finales, rewarded requests) driving the real
    /// <see cref="AdPolicy"/> with interstitials on; the independent <see cref="AdAudit"/> checks the result.
    /// </summary>
    public class AdAuditTests
    {
        private const int Sessions = 50;
        private const float SessionSeconds = 600f;
        private const float Step = 0.1f;

        private sealed class Session
        {
            public readonly List<AuditEvent> Events = new List<AuditEvent>();
            public int Interstitials;
            public int Rewarded;
            public int Ignitions;
        }

        [Test]
        public void FiftySessions_NoAdAtAForbiddenMoment()
        {
            var violations = new List<string>();
            int interstitials = 0, rewarded = 0, ignitions = 0;
            for (int seed = 1; seed <= Sessions; seed++)
            {
                Session s = Simulate(seed, followRules: true);
                violations.AddRange(AdAudit.Check(s.Events).Select(v => $"session {seed}: {v}"));
                interstitials += s.Interstitials;
                rewarded += s.Rewarded;
                ignitions += s.Ignitions;
            }

            string totals = $"{Sessions} sessions: {ignitions} ignitions, {interstitials} interstitials, {rewarded} rewarded ads";
            Assert.IsEmpty(violations, totals + "\n" + string.Join("\n", violations.Take(20)));

            // The audit means something only if ads were actually shown.
            // At most two fit in ten minutes (4 minutes apart, never at launch); quiet moments decide the rest.
            Assert.GreaterOrEqual(interstitials, Sessions / 2, totals + ": interstitials must actually be exercised");
            Assert.GreaterOrEqual(rewarded, Sessions, totals + ": rewarded ads shown when asked at quiet moments");
        }

        [Test]
        public void TheAuditCatchesAnAdServiceThatIgnoresTheRules()
        {
            int caught = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                caught += AdAudit.Check(Simulate(seed, followRules: false).Events).Count;
            }

            Assert.Greater(caught, 0, "showing ads whenever asked must be flagged");
        }

        [Test]
        public void EachRuleIsDetected()
        {
            AssertFlags("during a fuse", new AuditEvent(0f, AuditEventKind.Ignition), Ad(300f, AdKind.Rewarded));
            AssertFlags("placing", new AuditEvent(10f, AuditEventKind.PlacingStart), Ad(300f, AdKind.Rewarded));
            AssertFlags("finale", new AuditEvent(10f, AuditEventKind.FinaleStart), Ad(300f, AdKind.Rewarded));
            AssertFlags("10 s of a payoff", SixIgnitionsAndPayoffs(295f), Ad(300f, AdKind.Interstitial));
            AssertFlags("4 minutes", SixIgnitionsAndPayoffs(50f), Ad(100f, AdKind.Interstitial));
            AssertFlags("only 0 ignitions", Ad(300f, AdKind.Interstitial));
            AssertFlags("tap", SixIgnitionsAndPayoffs(50f), new AuditEvent(299f, AuditEventKind.Touch), Ad(300f, AdKind.Interstitial));

            var clean = new List<AuditEvent> { new AuditEvent(0f, AuditEventKind.SessionStart) };
            clean.AddRange(SixIgnitionsAndPayoffs(50f));
            clean.Add(Ad(300f, AdKind.Interstitial));
            Assert.IsEmpty(AdAudit.Check(clean), "a well-placed interstitial passes");
        }

        private static void AssertFlags(string expected, params object[] parts)
        {
            var events = new List<AuditEvent> { new AuditEvent(0f, AuditEventKind.SessionStart) };
            foreach (object part in parts)
            {
                if (part is AuditEvent e)
                {
                    events.Add(e);
                }
                else
                {
                    events.AddRange((IEnumerable<AuditEvent>)part);
                }
            }

            List<string> found = AdAudit.Check(events.OrderBy(e => e.Time).ToList());
            Assert.IsTrue(found.Any(v => v.Contains(expected)), $"expected '{expected}' in: {string.Join("; ", found)}");
        }

        private static IEnumerable<AuditEvent> SixIgnitionsAndPayoffs(float payoffTime)
        {
            for (int i = 0; i < 6; i++)
            {
                yield return new AuditEvent(1f + i, AuditEventKind.Ignition);
            }

            for (int i = 0; i < 6; i++)
            {
                yield return new AuditEvent(payoffTime, AuditEventKind.PayoffComplete);
            }
        }

        private static AuditEvent Ad(float time, AdKind kind) => new AuditEvent(time, AuditEventKind.AdShown, kind);

        private static Session Simulate(int seed, bool followRules)
        {
            var rng = new Random(seed);
            var session = new Session();
            var policy = new AdPolicy(0f) { InterstitialsEnabled = true };
            var payoffAt = new List<float>();
            bool placing = false, finale = false;
            float placingUntil = 0f, finaleUntil = 0f;
            float lastTouch = float.NegativeInfinity;
            float nextInterstitialCheck = 1f;
            float idleUntil = 0f;
            session.Events.Add(new AuditEvent(0f, AuditEventKind.SessionStart));

            void Ignite(float t, float fuseAndEffect)
            {
                payoffAt.Add(t + fuseAndEffect);
                policy.RecordIgnition();
                session.Ignitions++;
                session.Events.Add(new AuditEvent(t, AuditEventKind.Ignition));
            }

            void TryShow(float t, AdKind kind)
            {
                var scene = new AdScene(payoffAt.Count > 0, placing, finale, t - lastTouch);
                if (followRules && policy.Check(kind, t, scene) != AdBlock.None)
                {
                    return;
                }

                policy.RecordAdShown(t);
                session.Events.Add(new AuditEvent(t, AuditEventKind.AdShown, kind));
                if (kind == AdKind.Interstitial)
                {
                    session.Interstitials++;
                }
                else
                {
                    session.Rewarded++;
                }
            }

            int steps = (int)(SessionSeconds / Step);
            for (int i = 1; i <= steps; i++)
            {
                float t = i * Step;
                for (int p = payoffAt.Count - 1; p >= 0; p--)
                {
                    if (payoffAt[p] <= t)
                    {
                        payoffAt.RemoveAt(p);
                        policy.RecordPayoff(t);
                        session.Events.Add(new AuditEvent(t, AuditEventKind.PayoffComplete));
                    }
                }

                if (placing && t >= placingUntil)
                {
                    placing = false;
                    session.Events.Add(new AuditEvent(t, AuditEventKind.PlacingEnd));
                }

                if (finale && t >= finaleUntil)
                {
                    finale = false;
                    session.Events.Add(new AuditEvent(t, AuditEventKind.FinaleEnd));
                }

                // Players play in bursts (a tap about every five seconds, each starting one action) and
                // pause in between to watch, chat or look away (15 to 60 s without touching).
                if (t >= idleUntil && rng.NextDouble() < 0.002)
                {
                    idleUntil = t + 15f + (float)rng.NextDouble() * 45f;
                }

                if (t >= idleUntil && rng.NextDouble() < 0.02)
                {
                    lastTouch = t;
                    session.Events.Add(new AuditEvent(t, AuditEventKind.Touch));
                    double action = rng.NextDouble();
                    if (action < 0.35 && !placing)
                    {
                        placing = true;
                        placingUntil = t + 1f + (float)rng.NextDouble() * 2f;
                        session.Events.Add(new AuditEvent(t, AuditEventKind.PlacingStart));
                    }
                    else if (action < 0.8)
                    {
                        Ignite(t, 3f + (float)rng.NextDouble() * 9f);
                    }
                    else if (action < 0.88 && !finale)
                    {
                        finale = true;
                        finaleUntil = t + 10f + (float)rng.NextDouble() * 10f;
                        session.Events.Add(new AuditEvent(t, AuditEventKind.FinaleStart));
                        int items = 3 + rng.Next(4);
                        for (int k = 0; k < items; k++)
                        {
                            Ignite(t, 2f + k * 1.4f + 3f);
                        }
                    }
                    else
                    {
                        TryShow(t, AdKind.Rewarded);
                    }
                }

                if (t >= nextInterstitialCheck)
                {
                    nextInterstitialCheck += 1f;
                    TryShow(t, AdKind.Interstitial);
                }
            }

            return session;
        }
    }
}
