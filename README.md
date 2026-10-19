# jhilimili — Utsav Nights (Festival Fireworks Simulator)

Offline 3D Diwali / Tihar fireworks simulator for Android, built with Unity 6.3 LTS and URP.
The full product spec is [Docs/PRD/Festival_Fireworks_Simulator_Development_PRD-1.pdf](Docs/PRD/Festival_Fireworks_Simulator_Development_PRD-1.pdf).
Work proceeds one phase at a time; a phase starts only after the previous exit gate passes.

| Phase | Status |
|-------|--------|
| 0 Research and validation | Not started (owner task) |
| 1 Project setup and rendering spikes | **In progress**: code in place, device measurements pending |

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
  Scripts/Core/      pure C# (no UnityEngine): stats, ignition hold, maths, procedural sounds
  Scripts/Spikes/    Phase 1 throwaway spike runtime (perf monitor, particles, audio, lighter, HUD)
  Scripts/Editor/    project setup as code, scene builder, effect factory, build script
  Scripts/{Gameplay,Content,Effects,Audio,Haptics,UI,Services}/   Phase 2+ (empty)
  Definitions/ Prefabs/ Scenes/ Art/ Audio/ Materials/ Shaders/ Addressables/ Settings/
  Tests/EditMode  Tests/PlayMode
Docs/   PRD, ADRs, Phase 1 setup and spike report, licence log
tools/  adb scripts for cold start, results pull and thermal soak
```

## Rules for contributors and AI agents

- Gameplay code never names a specific cracker; it reads definitions (PRD 7).
- No per-frame allocations in gameplay code, pooling for particles, audio and UI, central ticking (PRD 13.3).
- Any new library or package needs an ADR in `Docs/ADR`.
- Only original or clearly licensed assets; log every third-party asset in `Docs/LICENCE_LOG.md`.
