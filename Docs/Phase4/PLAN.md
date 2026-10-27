# Phase 4 plan: monetisation, analytics, crash reporting and compliance

PRD section 19, Phase 4. Features F10 (ads and consent), F11 (Remove-ads purchase) and F12
(analytics and crash reporting), plus PRD 15 (ad rules), 16 (privacy) and 17 (events).
Started 2026-10-27, in parallel with the owner's Phase 2 and 3 field tests.

## Definition of done (from the PRD)

- F10: no ad at launch, during a fuse or during a payoff. The consent flow passes with debug geography.
- F11: the purchase persists, and Restore works.
- F12: the Section 17 events appear in the dashboard, with no personal data.
- An ad placement audit shows zero ads at forbidden moments over 50 test sessions.
- Deliverables: ads and purchases working with test IDs, a consent flow, a dashboard of core events and
  a privacy policy URL.
- Exit gate: switch to live ad units only on the closed-test build, and check AdMob shows no policy
  warnings.

## Approach

The rules live in engine-free Core code that is fully unit-tested: when an ad may show, what
the player owns, and which events are allowed. Each SDK sits behind a small interface, with two
versions of each:

- **Development version**, the default. Works with no accounts, no network and no SDK, so
  every build and test keeps working. Ads appear as an in-game "Test ad" card. Purchases succeed
  locally. Events go to the local playtest log.
- **SDK version**, compiled only when its scripting define is set (`UTSAV_ADMOB`, `UTSAV_IAP`,
  `UTSAV_FIREBASE`). It is switched on once the owner's account IDs exist. Game code does not change.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Rules core | `AdPolicy`. Interstitial only after 6 or more ignitions and 4 minutes or more since the last ad. Never at launch, never within 10 s of a payoff. Never while a fuse burns, an effect plays, something launches, the player is placing, or a finale runs. `progress.json` holds unlocks, the purchase cache and rewarded unlocks that expire (one session or 24 h) | Every rule and edge case; save, load and migrate |
| 2 | Ad service | `AdService.ShowRewarded(placement)` and `MaybeShowInterstitial(trigger)`, with preloading, frequency caps, a feature flag and silent failures. Development backend shows a "Test ad" card. Every show and every refusal goes to the log with its reason and `ms_since_payoff` | Policy wiring; failures never block play |
| 3 | Premium cracker and rewarded unlock | One premium cracker in the tray, shown locked. Tapping it opens a sheet: "Watch an ad: unlock for 24 h" or "Remove ads and unlock all". The unlock persists until it expires | Unlock flow; expiry; Play Mode |
| 4 | Consent | `ConsentService` over UMP: asked before ads start where required. Privacy options entry in Settings (replaces the placeholder). Ads and analytics follow the consent state. Development backend simulates EEA and non-EEA | Gating rules; debug geographies |
| 5 | Purchases | `PurchaseService`: Remove ads plus unlock all (one non-consumable). Purchase, Restore, an entitlement cache in `progress.json`, pending and failed states. Settings gets "Remove ads" and "Restore purchases" | Entitlement logic; Play Mode |
| 6 | Analytics and crashes | Typed Section 17 events (`app_first_open`, `scene_ready`, `cracker_placed`, `cracker_ignited`, `payoff_complete`, `session_end`, `settings_changed`, `ad_*`, `purchase_*`, `perf_sample`). A PII guard rejects free text and identifiers. Respects consent. Local sink plus a Firebase adapter. Crash reporter adapter | Schema; PII guard; consent |
| 7 | SDK integration | Google Mobile Ads Unity plugin with UMP, Unity IAP (Google Play Billing), Firebase Analytics and Crashlytics. Each behind its define, using Google's test ad unit IDs. Offline guard allowlist updated with reasons | Compile both ways; device test APK |
| 8 | Compliance | Privacy policy page, Data safety form answers, ads declaration, content rating answers, `app-ads.txt` | Docs/Phase4 |
| 9 | Audit and measure | 50 simulated sessions checked against the forbidden-moment rules; a device run with test ads; a consent run with debug geography; a purchase and restore run with a licence tester | Reports |

## Decisions (recommendations; the owner can override)

| Topic | Recommendation | Why |
|---|---|---|
| Premium cracker | Add a 9th cracker, "Peacock fountain" (a colour-changing fountain), as the premium item. Keep all 8 Phase 3 crackers free | PRD 15.1: the free app has every core cracker. Locking one of the tested 8 would weaken the free loop |
| Rewarded unlock length | 24 h (the PRD allows a session or 24 h) | One choice keeps the sheet simple; 24 h feels generous |
| Banner | None in R0 | PRD lists it as optional. A banner on the tray would sit next to controls (PRD 15.2 policy hygiene) |
| Interstitial | Built and rules-tested, off by default via the feature flag | Lets the first closed test compare with and without |
| Purchases | Unity IAP (`com.unity.purchasing`) | PRD 13.1. Official, and handles Google Play Billing updates |
| Analytics | Firebase Analytics and Crashlytics (PRD 13.1). Remote Config deferred | Remote Config is not needed for R0 |
| Audience | General audience, not designed for children under 13 (PRD 2). Under-age-of-consent tag only where known | PRD decision for R0 |

## Owner tasks (needed for steps 7 to 9; development continues without them)

1. **AdMob:** create an account, add the Android app, and send the app ID and two ad unit IDs
   (rewarded and interstitial). Register the tablet as a test device. Never tap your own live ads.
2. **Google Play Console:** create the app with package `com.ankitsharma.utsavnights`, create the
   managed product `remove_ads` (one-time), and add a licence tester account.
3. **Firebase:** create a project, add the Android app with the same package, download
   `google-services.json` and give it to me. Link AdMob to Firebase.
4. **Privacy policy:** a public URL and a contact email. I'll draft the text in step 8; it can be hosted
   on GitHub Pages or any simple site.
