# ADR 0003: Phase 4 SDKs (ads, consent, purchases, analytics)

- Status: Accepted
- Date: 2026-10-27

## Packages

| Package | Version | Source | Why |
|---------|---------|--------|-----|
| com.google.ads.mobile | 11.6.0 | OpenUPM | AdMob with Google's User Messaging Platform (consent) built in (PRD 13.1, 15.2) |
| com.unity.external-dependency-manager | 2.1.1 | Unity registry | Resolves the Android libraries AdMob needs. Replaces `com.google.external-dependency-manager` 1.2.190 from ADR 0002, which is deprecated in favour of this one; AdMob 11 depends on it |
| com.unity.purchasing | 5.4.4 | Unity registry | Google Play Billing for the one-time Remove-ads purchase (PRD 13.1) |
| Firebase Analytics and Crashlytics | not added yet | Google | Added once the owner's `google-services.json` exists; the app cannot initialise Firebase without it |

## How the SDKs connect

- Each SDK has its own assembly: `UtsavNights.Ads` (AdMob and UMP) and `UtsavNights.Purchasing` (Unity
  IAP). Unity compiles each one only while its package is installed, through a version define
  (`UTSAV_ADMOB`, `UTSAV_IAP`) used as a define constraint. Removing a package removes its assembly;
  nothing else breaks.
- On a device, each assembly registers itself in `MonetisationProviders` before the first scene loads.
  The installers use those providers, or fall back to the development providers when none is set.
- The editor and the automated tests always use the development providers. Tests stay deterministic,
  with no network and no SDK pop-ups. Real SDK behaviour is checked on the device (Phase 4 step 9).
- Ad identifiers live in `Assets/_Game/Resources/AdsConfig.asset`. Before every build, the build script
  copies the app ID into the plugin's settings asset (`Assets/GoogleMobileAds/Resources/`).
- AdMob's own build step switches on the custom main Gradle template, and the dependency manager adds
  the Play Services libraries to it. Gradle downloads them at build time, so the first build needs
  network access.

## Consequences

- The INTERNET permission is now really used (ads, store). Gameplay still never touches the network,
  and `OfflineGuardTests` still scans game scripts. The SDK assemblies live in `Scripts/Sdk` and use no
  network APIs directly.
- Purchases are confirmed on the device. There is no game server, and PRD 16 keeps no accounts. That is
  acceptable for a one-time unlock. Server-side receipt validation is out of scope for R0.
