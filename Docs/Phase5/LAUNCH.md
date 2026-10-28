# R0 launch plan (Phase 5 step 7)

When and how R0 goes public, what must be true first, and the reviews after launch. Written 2026-10-28.
[Verify] marks dates and Play rules to confirm.

## Timing: Diwali 2026 is too close

The PRD wants R0 live about 2 to 3 weeks before the festival peak. Diwali 2026 falls on **Sunday 8 November**
(Lakshmi Puja that evening; Govardhan Puja on the 9th and Bhai Dooj on the 10th) [Verify: a few calendars show
other dates]. That's 11 days from today.

A new personal developer account cannot reach production by then:
- it needs a closed test with 12 or more testers opted in for 14 days in a row first [Verify in Play Console];
- the accounts, Firebase and live ad IDs aren't set up yet;
- Play's review adds a few days more.

**Recommended plan:**

| When | What | Why |
|---|---|---|
| Now to about 3 Nov | Accounts, Firebase, privacy policy, live ad IDs, upload key, device re-test ([checklist below](#launch-checklist)) | Everything the closed test needs |
| As early as possible, **running through Diwali week** | Closed test ([CLOSED_TEST.md](CLOSED_TEST.md)), 14 days or more | Testers play during the real festival, so the closed-test data (retention, sessions, feel) is festival data, which is exactly what the PRD's D1 hypothesis needs |
| Late November | Production, staged rollout | After the 14 days and the fix build. Catches the late-season tail: Dev Deepawali (about 24 November [Verify]) and wedding-season fireworks |
| 31 December | Second peak: New Year's Eve | Fireworks without a specific festival; the game needs no change to suit it |
| **Diwali 2027** (about late October 2027 [Verify]) | The real launch moment, with R1 if the exit gate says go | Live 2 to 3 weeks before, as the PRD intends, with a year of ratings and fixes behind it |

Small tests can't measure retention and revenue well (PRD 17), so the late-November launch is the real demand
test. The data from it feeds the R1 decision well before Diwali 2027.

## Launch checklist

Everything from Phases 4 and 5 that has to be done once, in order. The owner is you unless it says "Claude".

**Accounts and legal**
- [ ] Play Console developer account; app created ([RELEASE.md](RELEASE.md))
- [ ] Privacy policy placeholders filled in, page and `app-ads.txt` hosted, URL in `AppLinks.asset` ([COMPLIANCE.md](../Phase4/COMPLIANCE.md))
- [ ] AdMob app and ad units created, live IDs in `AdsConfig.asset`, test devices registered, GDPR message published ([SDK_SETUP.md](../Phase4/SDK_SETUP.md))
- [ ] Firebase project, `google-services.json` sent to Claude → Claude adds Firebase Analytics and Crashlytics
- [ ] BigQuery linked to Firebase; event data retention set to 2 months
- [ ] `remove_ads` product active; licence testers added
- [ ] App content forms: ads, advertising ID, target audience, content rating, data safety ([COMPLIANCE.md](../Phase4/COMPLIANCE.md))

**Build**
- [ ] Upload key made and backed up (`tools/make_upload_key.sh`)
- [ ] Licensed sounds in, passing the import lint ([ASSETS.md](ASSETS.md)); blind sound test 6 of 8 or better
- [ ] Device re-test on four phones meets Section 14 ([PERFORMANCE.md](PERFORMANCE.md))
- [ ] Device ad audit, 50 sessions, 0 violations ([AUDIT.md](../Phase4/AUDIT.md))
- [ ] `tools/build_store.sh closed …` preflight all clear

**Listing**
- [ ] Text, icon, feature graphic, 4 or more screenshots ([STORE_LISTING.md](STORE_LISTING.md))

**Closed test, then production**
- [ ] Closed test run and decision meeting held ([CLOSED_TEST.md](CLOSED_TEST.md))
- [ ] Production access granted (Play Console dashboard)
- [ ] `tools/build_store.sh production …`; staged rollout as below

## Production rollout

| Stage | Share of users | Move on when |
|---|---|---|
| 1 | 10% | 2 days with crash rate under 1%, ANR under 0.47% [Verify], no new crash cluster, no ad policy warning |
| 2 | 50% | 2 more days, same checks, ratings not trending down |
| 3 | 100% | |

Halt or roll back as in [RELEASE.md](RELEASE.md), "Rollback plan". Play's review still applies to each update,
so keep a fix build ready rather than waiting for problems.

### Production release notes

```
<en-IN>
Utsav Nights is here. Hold the lighter to the fuse and light eight festival crackers: anars, chakris,
rockets, sparklers and more, then set off a finale. Works offline. Happy Diwali!
</en-IN>
```

Change "Happy Diwali!" to fit the date, for example "Happy New Year!" at the end of December.

## After launch

### Day 1, 3 and 7 reviews

Run `tools/metrics_report.py` ([CLOSED_TEST.md](CLOSED_TEST.md), "Report commands") with `--days 1`, `3` and `7`
from the launch date, and add:

| | Day 1 | Day 3 | Day 7 |
|---|---|---|---|
| Installs / uninstalls (Play Console) | | | |
| Crash rate / ANR rate (Android vitals) | | | |
| Time to first ignition, completion | | | |
| Ignitions per session | | | |
| D1 retention | | | |
| Ads per session, ad revenue per user (AdMob) | | | |
| Purchases | | | |
| Rating and the top review themes | | | |
| Action | | | |

Save each as `Docs/Phase5/reports/launch-dayN.md`. The day 7 report is the PRD's metrics report deliverable.

### Reviews and replies

- Reply to every 1 to 3 star review in the first two weeks: thank them, say what you'll change, and update
  the reply when the fix ships. Play lets the reviewer change their rating.
- Never ask for a rating in a reply, and never offer anything for one (Play policy).
- The in-app prompt (step 4) already asks happy players at the right moment.

### Getting seen (PRD 19)

- Seed 3 to 5 **short vertical clips**, real screen recordings ([STORE_LISTING.md](STORE_LISTING.md), video brief), on
  Instagram Reels, YouTube Shorts and WhatsApp status in the week of launch. Show the lighter at the fuse
  and the first second of the burst.
- Share with the closed testers; ask them, if they liked it, to rate it on Play. Asking is fine; rewarding
  is not.
- No paid ads for R0: the point is to measure organic demand (PRD 1.4).
