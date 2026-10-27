# Play Console compliance answers (Phase 4 step 8)

PRD 16. Each answer below matches what the app actually does in this build: it was checked against
the code and the APK's permissions on 2026-10-27. If a later change adds data or permissions, update this
file and the privacy policy together. Google's form wording changes from time to time, so read each
question as you answer it. [Verify] marks answers that depend on current Play policy.

## 1. Before you start: fill in the placeholders

| Placeholder | Where | What to put |
|---|---|---|
| `[DEVELOPER NAME]`, `[CONTACT EMAIL]`, `[DATE]` | `site/privacy-policy.html` | Your developer name as shown on Play, a contact email you check, today's date |
| `pub-XXXXXXXXXXXXXXXX` | `site/app-ads.txt` | Your AdMob publisher id (AdMob, Settings, Account information) |
| Privacy policy URL | `Assets/_Game/Resources/AppLinks.asset` | The https address where you host the page. The in-app Settings row appears once this is set |

**Hosting:** the page and `app-ads.txt` must be public over https.
- **Simplest:** a GitHub Pages site from a small public repository. Put `privacy-policy.html` and
  `app-ads.txt` at its root.
- `app-ads.txt` must sit at the root of the **developer website** you enter in Play Console, for
  example `https://yourname.github.io/app-ads.txt`.

## 2. App content: Privacy policy

Enter the privacy policy URL.

## 3. App content: Ads

**Does your app contain ads? Yes.** Rewarded ads only for now. Interstitials are built but switched off.

## 4. App content: Advertising ID

The APK declares `com.google.android.gms.permission.AD_ID` (added by the Google Mobile Ads SDK).
- **Does your app use an advertising ID? Yes.**
- **Purposes:** Advertising or marketing, Analytics, Fraud prevention, security and compliance.

## 5. App content: Target audience and content

- **Target age groups:** 13–15, 16–17 and 18 and over. Not under 13, as decided in PRD 2. [Verify]
  - Fireworks can appeal to children. If Play asks whether the app could unintentionally appeal to
    children, answer honestly. The store listing must not target children: no cartoon mascots aimed at
    kids, and no "for kids" wording.
- **Appeals to children:** No, by design. The store listing is written for adults and teens.

## 6. App content: Content rating (IARC questionnaire)

- **Category:** Game.
- **Violence:** No. Fireworks are celebratory, with no people or creatures harmed and no weapons.
- **Fear, sexuality, language, controlled substances, crude humour:** No.
- **Gambling:** No. No real-money or simulated gambling, and no loot boxes.
- **Users can interact or exchange content:** No. Clip sharing (PRD 17 `clip_shared`) is not in this
  build. Revisit when it ships.
- **Shares location:** No.
- **Digital purchases:** Yes (Remove ads and unlock all).
- **Ads:** Yes.

Expected result: PEGI 3 / ESRB Everyone, with "In-app purchases" shown. [Verify]

## 7. App content: Data safety

### Overview

| Question | Answer | Why |
|---|---|---|
| Does your app collect or share any of the required user data types? | **Yes** | AdMob, and Firebase once connected |
| Is all user data encrypted in transit? | **Yes** | The Google SDKs use HTTPS |
| Do you provide a way for users to request that their data is deleted? | **Yes** | By email (privacy policy, "Deleting your data"); local data goes with "Clear data" or uninstall |
| Account creation | **No accounts** | |

### Data types

"Collected" means sent off the phone. Settings, progress and the local playtest and analytics logs never
leave the phone, so they are not declared.

| Data type | Collected | Shared | Optional | Purposes | Comes from |
|---|---|---|---|---|---|
| Device or other IDs (advertising ID, Firebase app-instance ID) | Yes | Yes (AdMob) | No for ads; yes for analytics (Settings switch) | Advertising or marketing; Analytics; Fraud prevention, security and compliance | AdMob, Firebase |
| Approximate location (from IP) | Yes | Yes (AdMob) | No | Advertising or marketing; Fraud prevention | AdMob |
| App interactions (events in PRD 17) | Yes | No | Yes (Settings switch) | Analytics | Firebase |
| Crash logs | Yes | No | Yes (Settings switch) | App functionality; Analytics | Crashlytics |
| Diagnostics (performance samples) | Yes | No | Yes (Settings switch) | Analytics | Firebase |
| Purchase history | Yes | No | Yes (only if bought) | App functionality | Google Play Billing |
| Personal info, financial info, messages, photos, audio, contacts, calendar, files, web browsing, health, precise location | No | | | | |

**Shared versus collected:** per Google's guidance, data sent to a service provider that processes it
for you (Firebase) counts as collected, not shared. AdMob uses data for its own advertising, so mark
AdMob data as shared. [Verify against the current Play help article "Data safety: SDKs".]

**Before Firebase is connected:** analytics and crash data only go to local log files that never leave the
phone. Until then you could leave out the Firebase rows, but connect Firebase before the closed test and
declare them, so the form stays correct.

## 8. App content: Other declarations

- **Government app:** No. **Financial features:** No. **Health:** No. **News:** No.
- **Foreground service:** the APK lists `FOREGROUND_SERVICE` because an SDK adds it. If Play Console
  asks for foreground service types, check which SDK adds it before answering. The game itself runs no
  foreground service. [Verify]
- **Families policy:** not applicable, because the app is not designed for children.

## 9. In-app items already in place

| Requirement | Where |
|---|---|
| Consent before ads, where required | `ConsentService` (Phase 4 step 4), Google's form on devices |
| Privacy options entry | Settings → Privacy options |
| Analytics opt-out | Settings → Share usage statistics |
| Privacy policy link | Settings → Privacy policy (visible once `AppLinks` has the URL) |
| No ads at launch, during fuses, payoffs, placement or finale | `AdPolicy` (step 1), audited in step 9 |
| Restore purchases | Settings → Restore |

## Checklist before the closed test

- [ ] Placeholders filled in, privacy page and `app-ads.txt` published over https
- [ ] `AppLinks.asset` has the policy URL; the build log no longer warns about it
- [ ] Firebase connected (`google-services.json`), Data safety rows match
- [ ] In Google Analytics (Firebase project, Admin, Data retention) event data retention set to 2 months, as the policy states
- [ ] AdMob live IDs in `AdsConfig.asset`, test devices registered (SDK_SETUP.md), GDPR message published
- [ ] `remove_ads` product active in Play Console, licence tester added
- [ ] Ads, Advertising ID, Target audience, Content rating and Data safety sections completed
