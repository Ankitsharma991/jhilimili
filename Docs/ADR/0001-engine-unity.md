# ADR 0001: Engine is Unity 6.3 LTS with URP

- Status: Accepted, provisional until the Phase 1 gates pass
- Date: 2026-10-19

## Context

The product is a particle-heavy, audio-critical, touch-driven 3D app that must run on budget Android
phones (PRD 0.2). Later stages need avatars, modular houses, chunked neighbourhoods and downloadable packs.

## Decision

- Engine: Unity **6000.3.26f1** (Unity 6.3 LTS, newest LTS patch on 2026-10-08), C#, IL2CPP, ARM64, AAB.
  Unity 6.0 LTS support ends in October 2026, so 6.3 LTS (supported to December 2027) is the right base.
- Rendering: Universal Render Pipeline 17.3 with three quality tiers (Low / Mid / High).
- Particles: built-in Particle System (Shuriken), pooled. No VFX Graph on low-end Android.

## Gates (decide at the gate, not later)

| Gate | Pass condition |
|------|----------------|
| A | Five simultaneous effects, about 3,000 particles, hold 30 fps or better on a 3 to 4 GB RAM phone |
| B | Perceived audio latency under about 100 ms; 16 simultaneous voices without glitches |
| C | Release AAB of the spike scene under about 60 MB; cold start to interactive under 5 s |

If any gate fails after two optimisation attempts, evaluate Godot 4 before Phase 2 and record the
outcome in a new ADR that supersedes this one.

## Consequences

- C# and the Unity editor are new skills; the ramp-up is budgeted inside Phase 1.
- Unity licence terms must be checked against expected revenue [Verify].
