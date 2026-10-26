# Phase 1 spike report

Fill one column per phone. The app logs every measurement with the tag `[UTSAV_SPIKE]` and saves
them to `spike_results.json`; collect both with `tools/pull_spike_results.sh`.

Build used: `UtsavNights-release.apk` (release, IL2CPP, ARM64), unless a row says otherwise.

## Devices

| | Phone 1 (budget, 3 to 4 GB) | Phone 2 (mid) | Phone 3 (mid or flagship) |
|---|---|---|---|
| Model | | | |
| Android version | | | |
| RAM | | | |
| GPU (from report) | | | |

## Gate A: particles (target 30 fps or better on the budget phone)

Procedure: tier **Low**, then **Mid**. Tap **Start 5 effects**, wait 5 s, tap **Capture 30 s**.
Repeat with effects stopped for a baseline. Then run **Soak 15 min** with effects on while
`tools/thermal_soak.sh` runs on the computer.

| Metric | Phone 1 Low | Phone 1 Mid | Phone 2 Mid | Phone 3 High |
|---|---|---|---|---|
| Peak particles | | | | |
| avg_fps | | | | |
| p99 frame ms | | | | |
| % frames over 66.7 ms (2x budget) | | | | |
| Peak system memory MB | | | | |
| Soak: fps at minute 1 / 15 | | | | |
| Soak: battery temp start / end, thermal status | | | | |

**Gate A pass?** ☐ yes ☐ no

## Gate B: audio (target under about 100 ms perceived, 16 voices clean)

Software numbers: switch **Mode** to Audio tap, tap the 3D area 20 times per DSP setting.
The panel shows the mixer buffer latency and tap-to-play time.

Perceived latency (the gate): put a second phone running a voice recorder next to the test phone.
Tap the screen firmly with a fingernail so the tap itself makes a sound. Open the recording in an
audio editor (Audacity works). The gap between the fingernail transient and the app's click is the
tap-to-sound latency. Average 10 taps. The white square flashes on the same frame the sound starts,
so a 240 fps slow-motion video can also give touch-to-photon latency if needed.

Overlap: tap **16 voices**, listen on the phone speaker and on headphones for crackles or dropouts.

| Metric | Phone 1 | Phone 2 | Phone 3 |
|---|---|---|---|
| Buffer latency at DSP 256 / 512 / 1024 (ms) | | | |
| Tap-to-play software time (ms) | | | |
| Perceived tap-to-sound, 10-tap average (ms) | | | |
| overlap_peak_voices | | | |
| Glitches heard with 16 voices? | | | |
| Chosen DSP buffer | | | |

**Gate B pass?** ☐ yes ☐ no. If not, try DSP 512, then evaluate a native low-latency plugin [Verify].

## Gate C: build size and cold start (targets under about 60 MB, under 5 s)

Size comes from `Builds/Android/build_log.txt`. For the real download size per device, run
bundletool on the AAB:

```bash
bundletool build-apks --bundle=Builds/Android/UtsavNights-release.aab --output=/tmp/un.apks
bundletool get-size total --apks=/tmp/un.apks
```

Cold start: install the release APK, then run `tools/measure_cold_start.sh 5`.

| Metric | Value |
|---|---|
| Release AAB size (MB) | 32.1 (2026-10-26, Unity 6000.3.26f1, IL2CPP ARM64, spike scene + URP + all Phase 1 packages) |
| Max device download size from bundletool (MB) | 31.4 (arm64-v8a; bundletool 1.17.2 `get-size total`) |
| Release APK size (MB) | |
| Cold start Phone 1, 5-run average (ms) | |
| Cold start Phone 2 (ms) | |

**Gate C pass?** Size: ✅ pass (32.1 MB vs 60 MB target). Cold start: pending device.

## Spike D: touch

| Check | Result |
|---|---|
| Lighter follows the finger with an offset, fuse stays visible | |
| Ignition after holding about 0.5 s near the fuse | |
| first_ignition_after_scene_ready (s) | |
| Any input lag felt? | |

## Decision

☐ All gates pass: confirm Unity in ADR 0001 and start Phase 2.
☐ A gate failed after two optimisation attempts: evaluate Godot 4 and write ADR 0003.

Notes:

- First clean IL2CPP build took about 27 min on the dev laptop (shader variants plus IL2CPP);
  incremental builds reuse Library/Bee and the shader cache.
- The build needs roughly 4 GB of free disk for intermediates; keep 10 GB or more free.
