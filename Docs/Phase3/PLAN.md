# Phase 3 plan: cracker catalogue, audio, haptics and UI

PRD section 19, Phase 3. Features F03 to F09, plus F13 (finale mode v1).
Started 2026-10-26 in parallel with the Phase 2 playtests (owner decision): if the Phase 2 feel
test sends the flowerpot back for changes, the shared engine work below is not affected.

## Definition of done (from the PRD)

- 8 crackers with distinct audio and visual identity; blind sound test identifies 6 of 8 (F03).
- Tray: one tap places; tray 20 % of the screen or less (F04).
- Audio: no glitch with 16 voices; resumes after a call (F05). Haptics on 3 devices with an off switch (F06).
- Settings persist across restarts (F07); tier chosen on first run with manual override (F08).
- Works in airplane mode (F09). Finale chains 3 to 6 items and is replayable (F13).
- Exit gate: 10 target testers average 8 or more ignitions in a free session, no major tray confusion.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Settings | Versioned `settings.json` (atomic write, migration), audio and effects volumes, Quiet mode, reduce flash, reduce shake, haptics toggle and intensity, manual tier; applied live to every service | Load, save, migrate, defaults, apply rules |
| 2 | Catalogue | `CrackerCatalog` asset: ordered definitions for the tray, lock flags for later unlocks | Order, lookup, lint |
| 3 | Placement | Ground raycast, footprint and effect-volume clearance, max items in scene, ghost preview with green or red tint and a plain reason (PRD 6.3) | Validation rules |
| 4 | Spawner | Pooled `CrackerRuntime` per definition; place, ignite, spent, clear; scene limit by tier | Pool reuse, limits |
| 5 | Motion and repeats | Data-driven stage motion: spin (chakri), launch to a height with burst at apex (rocket, skyshot), repeats with interval and offset (chain lar, cake); hand-held attach (sparkler) | Motion timing, repeat schedule |
| 6 | UI foundation | TextMeshPro (essentials imported by the setup script), safe-area root, 48 dp touch targets, palette | Layout checks in Play Mode |
| 7 | Tray | Bottom horizontal tray (20 % of screen or less), icons with cost dot, tap to select, tap ground to place, auto-focus after a payoff (PRD 6.3) | Selection flow |
| 8 | Settings sheet | Gear icon, sliders and toggles from step 1, tier override, privacy placeholder for Phase 4 | Bindings |
| 9 | Seven crackers | Sparkler, ground spinner, chain crackers, single bang, rocket, skyshot, multi-shot cake: definitions, effect prefabs, models, placeholder synthesised sound sets (licensed audio is the owner's task) | Linter clean, smoke test each |
| 10 | Aerial camera and sky corridor | Aerial items launch inside a vertical corridor above their pad; camera follow per PRD 6.2 | Corridor validation |
| 11 | Finale v1 | Chain 3 to 6 placed items with fixed delays; replay | Schedule logic, Play Mode |
| 12 | Accessibility and offline | Font scale, contrast, touch targets, reduce motion; airplane-mode run | Checklist in Docs/Phase3 |
| 13 | Measure | Blind sound test, 10-tester free session (ignitions per session from the playtest log), device perf | Reports |

## Decisions (recommendations; the owner can override)

| Topic | Recommendation | Why |
|-------|----------------|-----|
| UI toolkit | uGUI with TextMeshPro (PRD 13.1) | Mature on mobile; TMP is part of uGUI 2.0 in Unity 6 |
| Settings storage | JSON in persistentDataPath, atomic temp-then-rename, schema version | PRD 13.5; PlayerPrefs only for tiny flags already in use |
| New cracker behaviour | Extend the definition (motion, repeats, attach) instead of per-cracker scripts | PRD 7.1 rule: no cracker-specific gameplay code |
| Audio for 7 crackers | Synthesised placeholders now, licensed recordings before the blind sound test | The blind test needs real, distinct sounds |
| Scene limit | 6 placed items on Low, 10 on Mid, 14 on High | Keeps particle budgets meaningful; tune after device tests |

## Owner tasks in parallel

- Licensed audio for all 8 crackers (lighter click, fuse, payoff, tail per PRD 11), logged in Docs/LICENCE_LOG.md.
- Phase 2 playtests (Docs/Phase2/PLAYTEST.md) and a budget phone plus a phone with a vibrator.
