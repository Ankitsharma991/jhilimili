# Phase 1 spike report

Fill one column per phone. The app logs every measurement with the tag `[UTSAV_SPIKE]` and saves
them to `spike_results.json`; collect both with `tools/pull_spike_results.sh`.

Build used: `UtsavNights-release.apk` (release, IL2CPP, ARM64), unless a row says otherwise.

## Devices

| | Phone 1 (budget, 3 to 4 GB) | Phone 2 (mid) | Phone 3 (mid or flagship) |
|---|---|---|---|
| Model | | Xiaomi Redmi Pad Pro (2405CRPFDI), tablet | |
| Android version | | 16 (API 36) | |
| RAM | | 6 GB (5.6 GB usable) | |
| GPU (from report) | | Snapdragon 7s Gen 2 (SM7435), 1600x2560 | |

## Gate A: particles (target 30 fps or better on the budget phone)

Procedure: tier **Low**, then **Mid**. Tap **Start 5 effects**, wait 5 s, tap **Capture 30 s**.
Repeat with effects stopped for a baseline. Then run **Soak 15 min** with effects on while
`tools/thermal_soak.sh` runs on the computer.

| Metric | Phone 1 Low | Phone 1 Mid | Phone 2 Mid | Phone 3 High |
|---|---|---|---|---|
| Peak particles | | | | Pad Pro High: 2,943 |
| avg_fps | | | | Pad Pro High: 59.8 |
| p99 frame ms | | | | Pad Pro High: 16.7 |
| % frames over 66.7 ms (2x budget) | | | | Pad Pro High: 0.06 |
| Peak system memory MB | | | | Pad Pro High: 360 |
| Soak: fps at minute 1 / 15 | | | | Pad Pro: 59.5 / 59.9 (Low and High mixed) |
| Soak: battery temp start / end, thermal status | | | | Pad Pro: 31 / 31 °C, status 0 |

**Gate A pass?** Mid tier ✅ (Redmi Pad Pro holds 60 fps at about 2,900 particles, no throttling in 15 min). Budget 3 to 4 GB phone: ☐ pending, which is what the gate is judged on.

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
| Buffer latency at DSP 256 / 512 / 1024 (ms) | | Pad Pro at 48 kHz: 21.3 / 42.7 / 85.3 (before the fix, Unity chose 24 kHz: 42.7 / 85.3 / 170.7) | |
| Tap-to-play software time (ms) | | Pad Pro: 28 (64 taps); plus 21.3 buffer = about 49 software latency | |
| Perceived tap-to-sound, 10-tap average (ms) | | | |
| overlap_peak_voices | | Pad Pro: 16 | |
| Glitches heard with 16 voices? | | Pad Pro: none (owner, speaker) | |
| Chosen DSP buffer | | 256 at 48 kHz | |

**Gate B pass?** Provisional ✅ on the Pad Pro: about 49 ms software latency and 16 clean voices. The acoustic fingernail measurement is still open. If not, try DSP 512, then evaluate a native low-latency plugin [Verify].

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
| Cold start Phone 1, 5-run average (ms) | Redmi Pad Pro: activity 365 + scene ready about 500 = about 0.9 s |
| Cold start Phone 2 (ms) | |

**Gate C pass?** Size: ✅ pass (32.1 MB vs 60 MB target). Cold start: ✅ pass on the Redmi Pad Pro (about 0.9 s vs 5 s). Re-check on the budget phone.

## Spike D: touch

| Check | Result |
|---|---|
| Lighter follows the finger with an offset, fuse stays visible | Yes (owner test and injected touch on the Pad Pro) |
| Ignition after holding about 0.5 s near the fuse | Yes |
| first_ignition_after_scene_ready (s) | Not meaningful in this session (logged 343 s, after other tests) |
| Any input lag felt? | No (owner). Issue: the spike pot sits at the control panel's edge, leaving a narrow touch strip; fix with the Phase 2 camera and layout |

## Decision

☑ Owner decision 2026-10-26: proceed to Phase 2 on Unity. Gates B and C pass, and Gate A passes on the mid-tier tablet with large headroom. The budget-phone Gate A run and the acoustic latency check carry into Phase 2 as open items.
☐ A gate failed after two optimisation attempts: evaluate Godot 4 and write ADR 0003.

Notes:

- First clean IL2CPP build took about 27 min on the dev laptop (shader variants plus IL2CPP);
  incremental builds reuse Library/Bee and the shader cache.
- Unity started the mixer at 24 kHz on the Redmi Pad Pro although the hardware runs at 48 kHz,
  doubling buffer latency. AudioLatencySpike now requests 48 kHz; Phase 2's audio manager must keep this.
- The 16-voice test first peaked at 15 because a 0.15 s sound ended before the 16th voice started;
  the test now uses only sounds of 0.9 s or longer.
- The build needs roughly 4 GB of free disk for intermediates; keep 10 GB or more free.
