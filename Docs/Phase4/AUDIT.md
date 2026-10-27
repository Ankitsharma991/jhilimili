# Phase 4 audit and measurement (step 9)

This is the Phase 4 definition of done from [PLAN.md](PLAN.md), with how each item is checked and its
current status. "Auto" means a test runs it on every build. "Device" means someone checks it on a
phone with the release APK.

## Status

| Item | Check | Status |
|---|---|---|
| No ad at launch, during a fuse or during a payoff (F10) | Auto: `AdPolicyTests`, `AdServiceSmokeTests` | Pass |
| Zero ads at forbidden moments over 50 sessions | Auto: `AdAuditTests`, 50 simulated 10-minute sessions with interstitials on, checked by an independent auditor. Device: `tools/ad_audit.py` on real sessions | Simulated: pass (0 violations; about 45 interstitials and 150 rewarded ads per run). Device: to do |
| Consent flow passes with debug geography (F10) | Auto: `ConsentSmokeTests` (development form). Device: Google's form, section 2 | Simulated: pass. Device: to do |
| Purchase persists; Restore works (F11) | Auto: `PurchaseSmokeTests` (development store). Device: Google Play with a licence tester, section 3 | Simulated: pass. Device: needs Play Console |
| Section 17 events appear; no personal data (F12) | Auto: `AnalyticsSchemaTests`, `AnalyticsSmokeTests`. Device: `tools/pull_analytics.sh`; Firebase DebugView once connected | Local log: pass. Firebase: needs `google-services.json` |
| Ads and purchases working with test IDs | Device: section 1 | Ads: to do. Purchases: needs Play Console |
| Privacy policy URL | [COMPLIANCE.md](COMPLIANCE.md) | Needs your name, email and hosting |
| Exit gate: live ad units only on the closed-test build; no policy warnings in AdMob | At the closed test (Phase 5) | Later |

## 1. Device ad audit (definition of done)

Use the release APK on the tablet or any phone, with internet on. Each session is a normal play
session of 3 to 10 minutes. Sessions can be spread over several days and several people. Fifty in
total are needed.

1. Open the app. Triple-tap the top-left corner and tap **Interstitials on/off** once. The log
   records "interstitials on". This lasts for that session only, so repeat it each session.
2. Play normally: place and light crackers, run a finale, change a setting.
3. In about half the sessions, also try to break the rules:
   - Tap the Peacock fountain → **Watch a short ad** while something is burning. Expected: "Wait for the
     fireworks to finish".
   - Tap it while a finale runs, and right after a big payoff.
4. Leave the phone untouched for a minute now and then. After 4 minutes and 6 ignitions, a full-screen
   test ad may appear at that quiet moment. That's expected.
5. Close the app fully at the end of each session.
6. Every few sessions, run `tools/pull_playtest.sh audit-NN`, then `tools/ad_audit.py`.

**Pass:** `ad_audit.py` reports 50 or more sessions, 0 sessions with violations, and ads actually shown
in many of them.

| Date | Sessions | Ads shown | Violations | Notes |
|---|---|---|---|---|
| | | | | |

Never tap a live ad on your own phone. These are Google's test IDs until the closed-test build, so tapping
them is fine for now.

## 2. Consent with Google's form (debug geography)

1. Run the app once and find the test device id in logcat. The steps are in
   [SDK_SETUP.md](SDK_SETUP.md), step 1.4.
2. In `AdsConfig.asset`, add the id to **Test Device Ids**, tick **Debug Geography Eea**, rebuild and
   install.
3. Clear the app's data, then open the app. **Expected:** Google's consent form appears before any ad.
   This needs a GDPR message published in AdMob.
4. Choose **Do not consent**. Tap the Peacock fountain → **Watch a short ad**. **Expected:** an ad still
   shows. It's non-personalised, which is allowed.
5. Open **Settings → Privacy options → Open**. **Expected:** Google's privacy options form opens again.
6. Untick Debug Geography Eea for normal builds.

## 3. Purchase and restore with Google Play

This needs the `remove_ads` product and a licence tester ([SDK_SETUP.md](SDK_SETUP.md), section 2),
and the app installed **from the Play testing track**. Sideloaded APKs can't buy.

| Check | Expected |
|---|---|
| Settings shows the price | The real price from Play, for example "₹99.00", in the local currency |
| Buy, then cancel in Google's sheet | Nothing changes, no error message |
| Buy with the test card "always approves" | "Thank you", the Premium badge is gone, Watch-ad options disappear |
| Restart the app offline | Still purchased |
| Clear app data, open, Settings → Restore | "Purchase restored" |
| Refund in Play Console (Order management), then reopen the app | Ads and the Premium badge come back |
| Buy with "slow test card, approves after a few minutes" | "Payment pending", then unlocked on its own once it approves |

The ₹ sign needs checking at this step, because the UI font may not include it. If it shows as a box, a
font fallback gets added.

## 4. Analytics

1. With **Share usage statistics** on, play for a minute, close the app, and run
   `tools/pull_analytics.sh`. **Expected:** `app_first_open` (fresh install only), `scene_ready`,
   `cracker_placed`, `cracker_ignited`, `payoff_complete`, `session_end`.
2. Switch it off, play again, and pull again. **Expected:** no new events.
3. Once Firebase is connected, turn on DebugView, as described in Firebase's Android documentation
   (`adb shell setprop debug.firebase.analytics.app <package>`). The same events should appear live.
