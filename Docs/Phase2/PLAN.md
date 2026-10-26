# Phase 2 plan: core engine and first cracker

PRD section 19, Phase 2. Features F01 (instant scene, no login) and F02 (lighter and fuse ignition).
Goal: a reusable, data-driven engine, and one cracker (the flowerpot) that feels excellent.

## Definition of done (from the PRD)

- Launch to a lit flowerpot in under 10 s; first interactive scene in 6 s or less on a mid phone.
- 5 testers: 4 or more ignite unaided within 30 s.
- Definition schema v1 plus a linter that blocks bad content.
- Performance capture on 3 devices.
- Exit gate: side-by-side with a top competitor, a majority of 10 testers prefer our ignition and payoff.

## Carried over from Phase 1

- Gate A on a budget 3 to 4 GB phone (open).
- Acoustic tap-to-sound measurement (open).
- Request 48 kHz output in the audio manager (Unity chose 24 kHz on the Redmi Pad Pro).
- Keep the ignition target well clear of UI; the spike scene's pot sat at the panel edge.

## Build order (one commit or small group per step, tests with each)

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Cracker definitions | `CrackerDefinition` ScriptableObject matching PRD 7.1 (id, names, category, tags, placement, ignition, stages, per-tier quality, budget, credits) | Field defaults, stage ordering |
| 2 | State machine | Pure C# `CrackerStateMachine`: Idle, Placed, Lit, Burning, Payoff, Cooldown, driven by definition stages; emits stage events. No cracker-specific code | Transitions, timing, pause and resume, reset |
| 3 | Content linter | Editor validator: required fields, stage times, audio and emitter references, licence id, per-tier budgets; menu item plus a build preprocessor that fails the build on errors | Each rule, valid and invalid fixtures |
| 4 | Core services | Boot scene, service registry, event bus, central `GameLoop` ticker (one Update for all crackers and effects) | Registry and event bus |
| 5 | Effects | Keyed prefab pools, flash-light pool (cap 3 flashes per second, reduce-flash halves), smoke sprite, per-tier particle caps | Pool reuse, flash rate limiter |
| 6 | Audio manager v1 | Voice pool with priority and stealing, ducking through AudioMixer snapshots, 48 kHz request, pause on focus loss and resume | Priority and stealing logic |
| 7 | Haptics v1 | Tick, thud, rumble through Android `VibrationEffect` via AndroidJNI (no third-party plugin), master toggle and intensity | Pattern mapping; device check |
| 8 | Lighter input | Finger offset, proximity to fuse anchors, 0.5 s hold, flame out after 3 s away from a fuse, relight; ignores touches on UI | Hold, flame-out and relight logic |
| 9 | Camera rig | Cinemachine 3 ground and aerial presets with easing, touch-cancelable aerial follow, shake 100 to 150 ms (off in Quiet and reduce-motion) | Preset selection logic |
| 10 | Quality governor v1 | First-run tier from RAM and GPU plus a short benchmark; rolling frame time; step down particles, then bloom, then resolution after 2 s over budget; at most one step up per session | Step-down policy |
| 11 | Main scene | Courtyard blockout (ground, wall, diyas, emissive string lights), flowerpot definition and fountain, first-run hand hint, background pause and resume, loading behind a short splash | Play Mode smoke test: boot, ignite, payoff |
| 12 | Measure and playtest | Perf capture on 3 devices, time-to-first-ignition, 5-tester unaided test, competitor side-by-side | Reports in Docs/Phase2 |

The Phase 1 spike scene and scripts stay in the repo as a benchmark until the end of Phase 2,
then move to a `Spikes` folder excluded from release builds.

## Decisions (recommendations; the owner can override)

| Topic | Recommendation | Why |
|-------|----------------|-----|
| Haptics | Own small AndroidJNI wrapper | No dependency or licence; three patterns are simple |
| Camera | Cinemachine 3 (already installed) | Easing, presets and shake for free |
| Flowerpot sound for the feel test | Real licensed recordings | The exit gate is about feel; synthesised placeholders will not win a side-by-side. Sourcing and licences are the owner's task (log them in Docs/LICENCE_LOG.md); code works with placeholders until then |
| Ignition target size | At least 48 dp touch target around each fuse, kept above the tray | Fixes the Phase 1 narrow-strip issue |

## Out of scope for Phase 2

Other crackers, tray UI, settings sheet, finale, ads, analytics, store: Phases 3 and 4.
