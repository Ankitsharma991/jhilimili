# Phase 5 plan: closed test, optimisation and the R0 launch

PRD section 19, Phase 5. Started 2026-10-28. Several steps wait on the owner's accounts (Play Console,
AdMob, Firebase) and field tests. The steps without those dependencies come first, so the build is ready
when the accounts are.

## Definition of done (from the PRD)

- The Section 14 targets are met on lab devices (see the table below).
- Crash-free sessions are 99 percent or better in the closed test.
- Deliverables: R0 on Google Play, release notes and a rollback plan, and a metrics report.
- Exit gate: a decision meeting. Go on to R1 only if feel preference, retention, and ad plus purchase
  economics justify it. Otherwise pivot or stop.

## Section 14 targets

| Metric | Low tier | Mid tier | High tier | How it's measured |
|---|---|---|---|---|
| Frame rate in the main scene | 30 fps | 45 to 60 fps | 60 fps | Developer overlay, Phase 1 spike captures |
| Frame-time spikes | Under 5% of frames over 2× budget | same | same | Overlay p95; step 3 GC guard |
| Cold start to interactive | 6 s or less | 4 s | 3 s | `tools/measure_cold_start.sh` |
| Peak RAM | Under 700 MB | Under 900 MB | Under 1.2 GB | `perf_sample` in the playtest log |
| Install size | Under 150 MB, stretch 100 MB | | | Store build plus bundletool (step 1) |
| Thermal | No visible throttling in 10 min | | | `tools/thermal_soak.sh` |
| Crash-free sessions | 99% or better | | | Play Console Android vitals, Crashlytics |

## Build order

| # | Step | Main pieces | Needs the owner? |
|---|------|-------------|------------------|
| 1 | Store build | Upload key outside the repo, signed AAB, version name and code, debug symbols for Play, preflight checks, developer overlay and playtest log removed from store builds, local logs capped | Run `make_upload_key.sh` once |
| 2 | Asset import rules | Import rules for the licensed audio (Vorbis quality by sound type, mono, load type) and icons (compressed, size cap), checked by the content lint, so real assets arrive at the right size | No |
| 3 | Performance guards | Managed-allocation guard during a busy show (GC spikes), startup timeline marks from process start to interactive, fixes for what they find | No, then a device re-test |
| 4 | In-app review prompt | Play In-App Review after a good session (a finale or a strong run), never after an ad, at most once per 30 days. Rules in Core, a development card in the editor | No |
| 5 | Store listing kit | Short and full description in honest wording, a screenshot capture script (portrait), feature graphic and video brief, release notes template | Owner takes the screenshots |
| 6 | Closed test kit | Tester invite text, a feedback form, the device lab re-test checklist (Section 14), a metrics report script for day 1, 3 and 7 | Owner runs the test |
| 7 | Launch | Production rollout plan (staged), timing before the festival peak, review prompt on, post-launch reviews | Owner |

## Status

| Step | Status |
|---|---|
| 1 Store build | Done 2026-10-28. A test build signed with a throwaway key: AAB 64.6 MB including debug symbols, **download 43.3 MB** on arm64 (bundletool), target API 36, store flag confirmed in the compiled code. The internal track is ready once you create the upload key. The closed track is blocked until live ad IDs and the privacy policy URL are in |
| 2 Asset import rules | Done 2026-10-28. New sounds and art get their rule on first import; the content lint checks 18 sounds and 9 icons before every build: 0 errors, 4 warnings on placeholders. Delivery spec for licensed audio in [ASSETS.md](ASSETS.md) |
| 3 Performance guards | Done 2026-10-28. The game allocates 0.02 KB per frame idle and 0.95 KB during the stress finale (guarded in tests). A startup timeline from process start to interactive is logged on every launch, and `measure_cold_start.sh` reads it. Frame-spike share and GC are in the 30 s perf sample. Device re-test table in [PERFORMANCE.md](PERFORMANCE.md) |
| 4 In-app review prompt | Done 2026-10-28. Rules in Core (second launch on, a finale or 10 bursts, quiet moment, 2 min clear of ads, once a session and per 30 days). Play In-App Review through JNI in store builds, a test card elsewhere. How to test it: [RELEASE.md](RELEASE.md), last section |
| 5 Store listing kit | Done 2026-10-28. App icon (the launcher icon was Unity's default until now), Play icon and feature graphic as own procedural art; honest short and full descriptions; screenshot script with Play's format checks; shot list and video brief in [STORE_LISTING.md](STORE_LISTING.md) |
| 6 Closed test kit | Done 2026-10-28. [CLOSED_TEST.md](CLOSED_TEST.md): setup checklist, tester invite (English and Hindi), feedback form with the feel-preference question, 14-day schedule, monitoring thresholds, decision table. `tools/metrics_report.py` computes the PRD 17 metrics from Firebase's BigQuery export ([metrics_query.sql](metrics_query.sql)); self-tested |
| 7 Launch plan | Done 2026-10-28. Diwali 2026 (8 Nov) is too close for production: run the closed test through Diwali week for festival data, launch staged in late November, New Year's Eve as a second peak, Diwali 2027 as the full launch. Master launch checklist, rollout stages, release notes and day 1/3/7 template in [LAUNCH.md](LAUNCH.md) |

All Phase 5 code and documents are in place. What remains needs the owner: the accounts and setup in the [launch checklist](LAUNCH.md#launch-checklist), the device tests and the closed test.

## Visual pass (2026-10-28)

The scene was blockout art. Changes, all own procedural art (LICENCE_LOG own-006):

| Before | After |
|---|---|
| A 2.8 m wall filled about 75% of the portrait view; a flat-colour sky strip | 1.5 m compound wall with coping, a gate between pillars; a painted night-sky panorama (stars, moon, city-light haze); houses with lit windows and roof lights, a temple spire, a water tank beyond the wall |
| String lights along the top of the wall | Strung between bamboo poles and the lamp post above the wall |
| Contour blotches on the walls (64 indirect samples, no filtering) | 256 samples with Gaussian filtering; plaster texture |
| Flat dark floor | Sandstone tiles with grout |
| Plain coloured cracker bodies | Printed wrappers: spiral stripes (rocket), stars (skyshot), sunburst (cake), zigzag (single bang), bunting (anar band), feather eyes (peacock), dots (chakri, chain) |

Review renders without a device: **Utsav → Phase 2 → Capture Scene Shots** (or `SceneShots.CiCapture`) writes
`captures/visual/`. **Check on the low-tier phone:** the skyline adds about 100 small static objects (batched),
and the sky is one 2048 × 1024 texture, so confirm fps in the device re-test ([PERFORMANCE.md](PERFORMANCE.md)).

### Effects review (2026-10-28)

**Utsav → Phase 2 → Capture Effect Shots** (`SceneShots.CiCaptureEffects`) renders each cracker mid-show, plus the
sky as the aerial follow sees it. It found one real defect: when the camera looked up at an aerial burst, the
sky showed **olive and red rings**. The cause was post-processing, not the texture: the neutral tonemapper's toe
stretched each 8-bit step of the very dark sky into a visible band (mid and high tiers only, which run
post-processing). Fixed by:
- lifting the zenith colour out of the crushed range;
- dithering the sky texture by about one level;
- turning on camera dithering;
- using high-quality compression (BC7, or ASTC 4×4 on Android) for the sky;
- widening stars near the zenith, so they stay round in the panorama.

The shared spark glow is now uncompressed (16 KB). The fireworks effects themselves were tuned in the Phase 2
feel tests and are unchanged.

## Firebase ready to switch on (2026-10-28)

Analytics and Crashlytics backends written against Firebase Unity SDK 14.1.0 and compile-checked against the real
DLLs. Not installed yet: they compile only once the packages are in. The "collection off at launch" manifest flags
are already in every APK. What remains is your `google-services.json`; the steps for both sides are in
[FIREBASE.md](FIREBASE.md).
