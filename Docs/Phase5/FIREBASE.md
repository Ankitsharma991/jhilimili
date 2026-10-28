# Firebase Analytics and Crashlytics

The closed test's numbers come from Firebase: time to first ignition, retention, ads, crashes
([CLOSED_TEST.md](CLOSED_TEST.md)). The code is ready and waits for your `google-services.json`.

## What is already in place

| Piece | Where | Status |
|---|---|---|
| Analytics backend: PRD 17 events, consent mode (analytics storage follows the setting; ad signals always denied) | `Assets/_Game/Scripts/Sdk/Firebase/FirebaseAnalyticsBackend.cs` | Compiled and checked against Firebase Unity SDK 14.1.0 (2026-10-23); not yet installed |
| Crashlytics backend: uncaught exceptions as fatal, non-fatal exceptions, on/off with the same switch | `FirebaseCrashReporter.cs` | Same |
| Startup: checks Google Play services once; events wait (at most 64) until Firebase is ready, or are dropped if it never is | `FirebaseStartup.cs` | Same; adds a `firebase` mark to the startup timeline |
| Registration on devices only (editor and tests keep the local log) | `FirebaseRegistration.cs` | Same |
| Assembly compiled only when `com.google.firebase.crashlytics` 14.0.0 or newer is installed | `UtsavNights.Firebase.asmdef` | Same pattern as AdMob and IAP |
| **Collection off at launch**: manifest flags, so Firebase sends nothing before the game's consent check runs | `Assets/Plugins/Android/UtsavFirebaseConfig.androidlib` | In every build now; inert until Firebase is installed |
| Compile check against the real SDK | `FIREBASE_SDK=<folder with the unpacked packages> tools/compile-check/check.sh` | Passes |
| Store preflight warns until Firebase is installed | `ReleaseChecks` (`analytics_sdk`) | Passes the warning today |

## Your part

1. [console.firebase.google.com](https://console.firebase.google.com) → **Add project** ("Utsav Nights"). Turn Google
   Analytics on when asked. Pick or create an Analytics account; set the data location to your country.
2. In the project: **Add app → Android**:
   - Android package name: `com.ankitsharma.utsavnights`;
   - App nickname: "Utsav Nights";
   - skip the SHA-1.
3. Download **`google-services.json`** and send it to me. It holds project identifiers, not secrets, but keep it out
   of public repositories anyway. [Verify Firebase's current advice before committing it.]
4. **Analytics → Admin → Data retention: 2 months**, as the privacy policy says.
5. **Project settings → Integrations → BigQuery → Link** (free sandbox), for the metrics report.

## My part when the file arrives

1. Put `google-services.json` in `Assets/`.
2. Add the three Firebase packages (`com.google.firebase.app`, `analytics`, `crashlytics`, 14.1.0 or the newest
   version by then) as embedded packages. Their `package.json` depends on Google's older
   `com.google.external-dependency-manager`, but this project uses Unity's `com.unity.external-dependency-manager`
   (AdMob depends on it too), so that one dependency line is removed. Two dependency managers in one project
   would clash.
3. Rebuild. The preflight's Firebase warning goes away. Check that the merged manifest still has the collection-off
   flags.
4. Device test with a release APK:
   - Firebase DebugView (`adb shell setprop debug.firebase.analytics.app com.ankitsharma.utsavnights`): no events
     before the consent choice; `scene_ready`, `cracker_ignited` and the rest after it; none after **Share usage
     statistics** is switched off.
   - Crashlytics: a test crash from a development build shows in the console within minutes.
5. Update the data safety answers if needed ([COMPLIANCE.md](../Phase4/COMPLIANCE.md); the Firebase rows are
   already written).

## Crash symbols

Store builds write `UtsavNights-<version>-<code>-<track>-…symbols.zip` next to the AAB. Play reads the symbols
inside the AAB for Android vitals. Crashlytics needs them uploaded separately:

```
firebase crashlytics:symbols:upload --app=<Firebase Android app id> <unzipped symbols folder>
```

The Firebase CLI comes from `npm install -g firebase-tools`. The app id is in Project settings → Your apps
(`1:…:android:…`).
