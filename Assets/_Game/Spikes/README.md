# Phase 1 spikes (archived)

Rendering, particle, audio-latency and touch spikes used to pass the Phase 1 gates
(Docs/Phase1/SPIKE_REPORT.md). Kept as a benchmark, excluded from normal builds.

The assemblies here compile only when the scripting define `UTSAV_SPIKES` is set:
Project Settings > Player > Android > Script Compilation > Scripting Define Symbols.
With it set, `Utsav/Setup/3. Build Spike Scene` and `4. Bake Spike Lighting` reappear, and
`Assets/_Game/Scenes/Spike_Phase1.unity` works again. Remove the define before release builds.
