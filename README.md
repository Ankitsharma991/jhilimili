# jhilimili — Utsav Nights (Festival Fireworks Simulator)

Offline 3D Diwali / Tihar fireworks simulator for Android, built with Unity 6.3 LTS and URP.
The full product spec is [Docs/PRD/Festival_Fireworks_Simulator_Development_PRD-1.pdf](Docs/PRD/Festival_Fireworks_Simulator_Development_PRD-1.pdf).
Work proceeds one phase at a time; a phase starts only after the previous exit gate passes.

| Phase | Status |
|-------|--------|
| 0 Research and validation | Not started (owner task) |
| 1 Project setup and rendering spikes | **Done** 2026-10-26: gates B and C pass; A passes on mid tier, budget phone open |
| 2 Core engine and first cracker | **Built** 2026-10-26: playable flowerpot; playtests and exit gate pending (Docs/Phase2/PLAYTEST.md) |

## Requirements

- Ubuntu 24.04 (or Windows / macOS), 30 GB free disk
- Unity Hub and **Unity 6000.3.26f1** with Android Build Support (OpenJDK, Android SDK and NDK)
- Git and Git LFS
- An Android phone with USB debugging for the gates

Full install steps: [Docs/Phase1/SETUP.md](Docs/Phase1/SETUP.md).

## Clean clone to running spike

```bash
git clone https://github.com/Ankitsharma991/jhilimili.git && cd jhilimili
git lfs install && git lfs pull
```

1. Unity Hub, then **Add project from disk**, then pick this folder. Open it with 6000.3.26f1.
2. If the editor asks to enable the new Input System backend, accept and let it restart.
3. Menu **Utsav > Setup > Run All Phase 1 Setup**. This applies Android player settings, creates the
   Low / Mid / High URP tiers, generates the spike scene and prefabs, and bakes lighting.
4. Press Play to try it in the editor (mouse acts as touch), or build:
   **Utsav > Build > Android Release APK** and install with `adb install -r Builds/Android/UtsavNights-release.apk`.

Compile check without a licence (about 20 s): `tools/compile-check/check.sh`.

Batch mode equivalent:

```bash
UNITY=~/Unity/Hub/Editor/6000.3.26f1/Editor/Unity
$UNITY -batchmode -projectPath . -executeMethod UtsavNights.EditorTools.ProjectSetup.RunAllBatch -logFile -
$UNITY -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile -
$UNITY -batchmode -projectPath . -buildTarget Android -executeMethod UtsavNights.EditorTools.BuildScript.CiReleaseAab -logFile -
```

## Layout

```
Assets/_Game/
  Scripts/Core/          engine-free C# (no UnityEngine): crackers, services, effects, audio, haptics,
                         camera timing, quality, ignition, diagnostics; unit-tested under Mono too
  Scripts/Core/Runtime/  Game, GameLoop, Bootstrap (thin Unity layer)
  Scripts/Content/       CrackerDefinition and content enums
  Scripts/Effects|Audio|Haptics/   services and installers
  Scripts/Gameplay/      CrackerRuntime, LighterController, CameraRig, QualityManager, hint, diagnostics
  Scripts/Editor/        project setup, content linter, build script, Main scene and flowerpot builders
  Spikes/                archived Phase 1 spikes; compile only with the UTSAV_SPIKES define
  Definitions/ Prefabs/ Scenes/ Audio/ Materials/ Settings/
  Tests/EditMode  Tests/PlayMode
Docs/   PRD, ADRs, phase plans, spike report, playtest protocol, licence log
tools/  adb scripts (cold start, spike results, playtest log, thermal soak) and the compile check
```

## Phase 2 quick start

1. `Utsav > Phase 2 > Build Boot and Main Scenes` (regenerates the flowerpot assets and scenes).
2. Press Play on `Assets/_Game/Scenes/Boot.unity`, or build `Utsav > Build > Android Release APK`.
3. In the app, triple-tap the top-left corner for the performance overlay.
4. After a session, `tools/pull_playtest.sh` prints time to first ignition, ignitions and fps.

## Rules for contributors and AI agents

- Gameplay code never names a specific cracker; it reads definitions (PRD 7).
- No per-frame allocations in gameplay code, pooling for particles, audio and UI, central ticking (PRD 13.3).
- Any new library or package needs an ADR in `Docs/ADR`.
- Only original or clearly licensed assets; log every third-party asset in `Docs/LICENCE_LOG.md`.
