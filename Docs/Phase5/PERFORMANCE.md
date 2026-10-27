# Performance guards and device re-test (Phase 5 step 3)

The PRD Section 14 targets, how each is measured, and what the automatic guards check on every build.
Device numbers go in the tables at the end.

## Automatic guards

| Guard | Where | Result (2026-10-28, editor) |
|---|---|---|
| Managed memory allocated per frame, idle in Main | `PerformanceGuardTests`, limit 1 KB | **0.02 KB** |
| Same, during the six-cracker stress finale | `PerformanceGuardTests`, limit 4 KB | **0.95 KB** average; largest single frame 36 KB |
| Incremental garbage collector | Player settings | On, so even the larger frames are collected in small slices |

These are measured with Unity's profiler on the game's own frames only.

In the editor, the plain "GC Allocated In Frame" counter reports about 1 MB per frame, but that comes from
editor tools: Google's External Dependency Manager alone allocates about 300 KB per editor tick. Neither
ships in the app. A diagnostic that switched off every component type and object in Main one at a time
confirmed it: the number didn't move.

## Startup timeline

Every build logs one line to logcat when the scene first takes taps, and again as each SDK finishes in the
background:

```
[UTSAV_STARTUP] pre_engine_ms=420 first_scene_ms=455 main_loaded_ms=1180 scene_ready_ms=1270 interactive_ms=1270 consent_ms=1350 ads_sdk_ms=2100 store_ms=2400
```

| Mark | Meaning |
|---|---|
| `pre_engine_ms` | Android starting the process and the Unity player, before any game code |
| `first_scene_ms` | Boot scene loaded |
| `main_loaded_ms` | Main scene loaded |
| `scene_ready_ms`, `interactive_ms` | One frame of Main drawn: the player can tap. **This is the PRD 14 number** |
| `consent_ms`, `ads_sdk_ms`, `store_ms` | Consent answered, Google Mobile Ads ready, Play Billing products fetched. These happen after interactive and don't delay play |

All times count from the real process start, read from `android.os.Process`, so they're what a player waits.

**Measure:** install the release APK, connect the phone and run `tools/measure_cold_start.sh 5`. It
restarts the app five times and averages `interactive_ms`.

## Frame spikes and memory on the device

Every 30 s the playtest log's `perf_sample` now has:
- `spike_pct`: the share of frames over twice the frame budget (33 ms at 30 fps, 17 ms at 60 fps). PRD
  target: under 5.
- `gc_kb_per_frame`: managed allocation per frame. Only in development APKs; release players don't expose
  the counter.
- `memory_mb`, `avg_fps`, `p95_ms`: as before.

Run `tools/pull_playtest.sh` after a session to see them.

## Device re-test (definition of done)

At least four phones per the PRD: one budget phone with 3 to 4 GB, two mid-range, one flagship. Use the
release APK, with each phone's tier as auto-detected.

| Phone | Tier | Cold start, 5-run average (target 6 / 4 / 3 s) | Avg fps in a 10-min session (30 / 45 / 60) | spike_pct (under 5) | Peak memory_mb (700 / 900 / 1200) | Thermal after 10 min (`tools/thermal_soak.sh`) |
|---|---|---|---|---|---|---|
| Redmi Pad Pro | | | | | | |
| | | | | | | |
| | | | | | | |
| | | | | | | |

Also check the PRD 14 items that have no number:
- background the app mid-show and come back: sound and the show resume;
- take a phone call during a finale;
- play 15 minutes without a crash.
