# Closed test kit (Phase 5 step 6)

How to run the PRD's closed test: who to invite, what testers do, what to watch every few days, and how to
decide at the end. [Verify] marks Play rules that change; Play Console's own dashboard shows the current
rule for your account.

## What the test must show (PRD 19, Phase 5)

| Question | Measured by | Pass |
|---|---|---|
| Does it crash? | Android vitals, Crashlytics, `metrics_report.py --crashes` | Crash-free sessions 99% or better |
| Do new players get to the fun fast? | Time to first ignition, first-cracker completion | Median 30 s or less; 80% or more reach a burst |
| Do they keep playing? | Ignitions per session, D1 retention | 8 or more; D1 around 20% (a hopeful target; 20 to 50 testers only give a rough signal) |
| Does it feel better than what exists? | Feedback form, question 4 (side-by-side) | Most testers prefer it, or name a clear reason |
| Are ads safe and tolerable? | Ads within 10 s of a payoff (must be 0), form question 7, reviews | 0 violations; ads not the top complaint |
| Does it run well? | Device re-test in [PERFORMANCE.md](PERFORMANCE.md), `perf_sample` in Firebase | Section 14 targets on the lab phones |

## Before day 0

- [ ] **Store build for the closed track** passes the preflight:
  ```
  tools/build_store.sh closed 0.1.0 3 --preflight
  ```
  This needs:
  - your live AdMob IDs;
  - the privacy policy URL;
  - the upload key.
  
  Then build without `--preflight` and upload ([RELEASE.md](RELEASE.md)).
- [ ] **Firebase connected** (send me `google-services.json`). Without it, the only data is Play Console's crash and install counts: no time to first ignition, retention or ad numbers.
- [ ] Firebase → BigQuery linked (free sandbox), so `metrics_query.sql` works. The export starts the day after linking.
- [ ] Your own phones are registered as AdMob test devices, so your own taps never count as invalid traffic ([SDK_SETUP.md](../Phase4/SDK_SETUP.md)).
- [ ] The `remove_ads` product is active. Add every tester's Google account under **Settings → License testing**, so their purchases are free test purchases.
- [ ] The store listing is filled in ([STORE_LISTING.md](STORE_LISTING.md)). Closed tests need the main listing too.
- [ ] Device re-test done on the lab phones ([PERFORMANCE.md](PERFORMANCE.md)), so testers don't find what you could have.

## Testers

- **How many:**
  - New personal developer accounts need at least **12 testers opted in for 14 days in a row** before production access [Verify].
  - The PRD asks for 30 to 100.
  - Invite about 25, because some never install.
- **Who:** adults and teens in your target group (PRD 3): students and workers away from family for the festival, people who used to light crackers at home. Include at least 3 people with budget phones (3 to 4 GB RAM).
- **How:**
  1. Create a Google Group, for example `utsav-nights-testers`, and add the group to the closed track's Testers tab.
  2. Share the opt-in link from that tab.
  3. People join the group, open the link, accept, and install from Play.

### Invite message

Copy, fill in the link, and send by WhatsApp or email:

```
Hi! I've made a small Diwali fireworks game for Android called Utsav Nights, and I need testers for two weeks
before it goes public.

What it takes: install it from the link below, play a few minutes on at least 3 different days, and fill in
a 2-minute form at the end. That's all.

1. Join the tester group: <Google Group link>
2. Open this link on your Android phone and tap "Become a tester": <opt-in link>
3. Install from the Play Store page it shows.

Please keep it installed for the full 14 days; Google counts testers who stay. Thank you!

(One request: please don't tap on ads to "support" me. Google treats that as fake clicks and can close the ad
account. Watch an ad only if you want what it offers.)
```

Hindi version of the key lines, if useful:

```
नमस्ते! मैंने Android के लिए दिवाली आतिशबाज़ी का एक छोटा गेम बनाया है, Utsav Nights। सार्वजनिक होने से पहले दो हफ़्ते के लिए टेस्टर चाहिए।
कम से कम 3 अलग-अलग दिन कुछ मिनट खेलें, और आख़िर में 2 मिनट का फ़ॉर्म भरें। 14 दिन तक ऐप इंस्टॉल रखें।
कृपया मदद के लिए विज्ञापनों पर टैप न करें — Google इसे नकली क्लिक मानता है।
```

## Feedback form

Create it in Google Forms with these questions. Send the link on day 10.

1. Which phone do you use? (short text)
2. How many days did you play? (1, 2, 3, 4 or more)
3. Did the app ever close by itself or freeze? (No / Once / More than once) and when? (short text)
4. **Have you played another fireworks app? If so, which felt better to light and watch, and why?** (Utsav Nights / The other app / About the same / Haven't played another) plus why (paragraph). *This is the PRD's feel-preference question.*
5. Was anything confusing in the first minute? (paragraph)
6. How was the sound? (Too quiet / Just right / Too loud / I played muted)
7. Did the ads bother you? (Didn't see any / Fine / A bit / A lot) plus when (short text)
8. Would you pay ₹99 to remove ads and unlock all crackers? (Yes / Maybe / No / Already did)
9. Which cracker did you like most, and which least? (short text)
10. Anything else? (paragraph)

The form asks for no name or email. Google Forms can collect emails, so switch that off: Settings → Responses → Collect email addresses: Do not collect.

## Schedule

| Day | Do |
|---|---|
| 0 | Roll out to the closed track. Send invites. Check that your own install from Play works end to end: purchase, Restore, an ad, the consent form if you test EEA |
| 1 | Android vitals: any crash cluster? Firebase DebugView or realtime: are events arriving? `metrics_report.py` with `--days 1` |
| 3 | Day 3 report. Read the pre-launch report (Play Console → Testing → Pre-launch report): crashes and accessibility warnings on Google's test devices. Fix anything serious |
| 5 to 7 | **Fix build:** top crashes and confusion points. Upload with a higher version code to the same track; testers update automatically. Day 7 report |
| 10 | Send the feedback form. Rerun the usability test (Phase 3 [MEASURE.md](../Phase3/MEASURE.md)) with 3 to 5 new people if confusion points were fixed |
| 14 | Final report (`--days 14`) and the decision meeting below. If 12 or more testers stayed 14 days, apply for production access in Play Console [Verify] |

### Report commands

```
# BigQuery: run metrics_query.sql for the test dates, save the result as events.csv
tools/metrics_report.py events.csv --since 2026-11-01 --days 7 \
    --crashes <crashes from Crashlytics or Android vitals> \
    --installs <Play Console installs CSV> \
    --ecpm <AdMob eCPM in USD> \
    --out Docs/Phase5/reports/day7.md
```

Play Console CSVs: **Download reports → Statistics** (installs, by month, UTF-16; the script reads it as is).

### Watch every few days

| Signal | Where | Act when |
|---|---|---|
| User-perceived crash rate | Android vitals | Above 1%. Play's "bad behaviour" threshold is about 1.09% [Verify] |
| User-perceived ANR rate (app not responding) | Android vitals | Above 0.47% [Verify] |
| Crash clusters | Crashlytics or vitals → Crashes | Any cluster hitting more than one tester |
| Interstitials within 10 s of a payoff | `metrics_report.py` | Any (must be 0; interstitials are off for R0 anyway) |
| AdMob policy centre | AdMob → Policy centre | Any warning: pause the ad units first ([RELEASE.md](RELEASE.md) rollback plan) |

## Decision meeting (exit gate)

Hold it on day 14 with the final report and the form results. The PRD says go on to R1 only if feel preference,
retention and ad plus purchase economics justify it; otherwise pivot (a Tihar angle, clips) or stop.

| | Result | Verdict |
|---|---|---|
| Crash-free sessions (99%+) | | |
| Time to first ignition (≤ 30 s) / completion (≥ 80%) | | |
| Ignitions per session (≥ 8) | | |
| D1 retention (about 20%, rough at this size) | | |
| Feel preference (form question 4) | | |
| Ads: violations 0, complaints | | |
| Revenue check: sessions per user × ads per session × eCPM / 1000 | | |
| Would pay to remove ads (form question 8) | | |
| **Decision** | Launch R0 as is / fix then launch / pivot / stop | |

Write it down in `Docs/Phase5/reports/decision.md`, so the next phase starts from the facts.
