using System.Collections.Generic;
using System.Globalization;

namespace UtsavNights.Core.Monetisation
{
    public enum AuditEventKind
    {
        SessionStart,
        Ignition,
        PayoffComplete,
        PlacingStart,
        PlacingEnd,
        FinaleStart,
        FinaleEnd,
        Touch,
        AdShown,
    }

    public readonly struct AuditEvent
    {
        public AuditEvent(float time, AuditEventKind kind, AdKind ad = AdKind.Rewarded)
        {
            Time = time;
            Kind = kind;
            Ad = ad;
        }

        public float Time { get; }

        public AuditEventKind Kind { get; }

        /// <summary>Which ad, for <see cref="AuditEventKind.AdShown"/>.</summary>
        public AdKind Ad { get; }
    }

    /// <summary>
    /// Phase 4 ad placement audit (definition of done: zero ads at forbidden moments over 50 sessions).
    /// Replays a session's timeline and checks every shown ad against PRD 15.1 on its own; it shares no
    /// code with <see cref="AdPolicy"/>, so a mistake in the rules cannot hide itself. Events must be in
    /// time order. Touch rules are checked only when the timeline has touch events.
    /// </summary>
    public static class AdAudit
    {
        public static List<string> Check(IReadOnlyList<AuditEvent> events)
        {
            var violations = new List<string>();
            int active = 0;
            bool placing = false;
            bool finale = false;
            bool hasTouches = false;
            float lastPayoff = float.NegativeInfinity;
            float lastTouch = float.NegativeInfinity;
            float lastAdOrStart = 0f;
            int ignitionsSinceAd = 0;

            foreach (AuditEvent e in events)
            {
                switch (e.Kind)
                {
                    case AuditEventKind.SessionStart:
                        lastAdOrStart = e.Time;
                        ignitionsSinceAd = 0;
                        break;
                    case AuditEventKind.Ignition:
                        active++;
                        ignitionsSinceAd++;
                        break;
                    case AuditEventKind.PayoffComplete:
                        active = active > 0 ? active - 1 : 0;
                        lastPayoff = e.Time;
                        break;
                    case AuditEventKind.PlacingStart:
                        placing = true;
                        break;
                    case AuditEventKind.PlacingEnd:
                        placing = false;
                        break;
                    case AuditEventKind.FinaleStart:
                        finale = true;
                        break;
                    case AuditEventKind.FinaleEnd:
                        finale = false;
                        break;
                    case AuditEventKind.Touch:
                        hasTouches = true;
                        lastTouch = e.Time;
                        break;
                    case AuditEventKind.AdShown:
                        string at = $"{e.Ad} ad at {e.Time.ToString("0.0", CultureInfo.InvariantCulture)} s: ";
                        if (active > 0)
                        {
                            violations.Add(at + "during a fuse, payoff or launch");
                        }

                        if (placing)
                        {
                            violations.Add(at + "while placing a cracker");
                        }

                        if (finale)
                        {
                            violations.Add(at + "during a finale");
                        }

                        if (e.Ad == AdKind.Interstitial)
                        {
                            if (e.Time - lastPayoff < 10f)
                            {
                                violations.Add(at + "within 10 s of a payoff");
                            }

                            if (e.Time - lastAdOrStart < 240f)
                            {
                                violations.Add(at + "within 4 minutes of launch or the last ad");
                            }

                            if (ignitionsSinceAd < 6)
                            {
                                violations.Add(at + $"after only {ignitionsSinceAd} ignitions");
                            }

                            if (hasTouches && e.Time - lastTouch < 3f)
                            {
                                violations.Add(at + "within 3 s of a tap");
                            }
                        }

                        lastAdOrStart = e.Time;
                        ignitionsSinceAd = 0;
                        break;
                }
            }

            return violations;
        }
    }
}
