# Measuring the open world on phones (Phase 12 step 8)

The open-world plan's gate for the slice: **it feels like a living place on a mid-range phone at 30 fps.** This page
says how to measure that on real phones and where the numbers go.

## The budgets

| What | Budget | Where it comes from |
|---|---|---|
| Frame rate exploring | 30 fps or more on every tier that has the town (mid and high) | PRD 14, the open-world plan |
| Spikes | Under 5% of frames over 66.7 ms (twice the 30 fps budget) | PRD 14 |
| Garbage | 4 KB a frame at most (the GC guard's show limit) | Phase 5 step 3 |
| Memory | 700 / 900 / 1200 MB on low / mid / high | Phase 5 device table |
| Crackers on the scene | 6 / 10 / 14 by tier, shared by the courtyard and the town | Phase 3 |

The low tier keeps to the player's lane (no bazaar), so the town's budgets apply to mid and high phones.

## The Explore bench

A scripted tour, the same every time, in five legs:

| Leg | Seconds | What it loads |
|---|---|---|
| `courtyard` | 10 | The courtyard at rest: the baseline |
| `bazaar_walk` | 14 | Sprinting east down the bazaar road: tiles streaming, the crowd and the stalls going by |
| `bazaar_stand` | 10 | Standing by the stalls turning the camera all the way round: the most of the town in view |
| `bazaar_ride` | 12 | The scooter at full speed through the traffic |
| `chowk_show` | 26 | The district's show on the chowk, the crowd gathering: the heaviest moment |

Each leg records the average fps and frame time, p50, p95, p99, the longest frame, the share of spikes, garbage a frame
(a development APK only; a release APK reports -1), the most memory, the most crackers on the scene and any the limit
turned away, the people simulated and drawn, and the vehicles drawn. Each leg is checked against the budgets above.

### Running it

- **From the phone:** triple-tap the top-left corner for the developer overlay, then **Explore bench**. Keep hands off
  for about 90 seconds; it ends back in the courtyard.
- **From the computer** (phone on USB, app installed): `tools/explore_bench.sh [runs]`. It starts the app with the
  bench, waits for it to finish, and saves the results, peak memory (PSS) and the battery temperature in
  `captures/bench-<phone>-<time>/`, with a summary a leg:

  ```
  bazaar_walk    41.2 fps  p95  29.8 ms  max   61.0 ms  spikes  0.0%  mem    812 MB  crackers 0 refused 0  ok
  ```

- **Garbage on the phone** needs the development APK (Utsav → Build → Android Development APK); the release APK
  measures everything else.
- **In the editor** the bench runs (its Play Mode test tours every leg) but garbage and memory count the editor too
  (about 600 KB a frame and 2.3 GB); the GC guard tests measure the game's own garbage there.
- **Heat:** for a long run, `tools/thermal_soak.sh 15` alongside three or four bench runs back to back.

Results are in `explore_bench.jsonl` in the app's files, one JSON line a leg and a summary line, and in logcat with the
tag `[UTSAV_BENCH]`. No personal data: the phone's model, its tier, and timings.

## Results

Fill in a row a phone. A cell that misses its budget needs a fix or a decision before the whole town is built.

| Phone | RAM | Tier | Walk fps / p95 | Stand fps / p95 | Ride fps / p95 | Show fps / p95 / refused | Peak memory | Spikes (worst leg) | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Xiaomi 2405CRPFDI (the test tablet) | | | | | | | | | |
| A 4 GB mid-range phone | | | | | | | | | |
| A budget phone (low tier: the lane only) | | | | | | | | | |

## In the field

Once Firebase is connected, `perf_sample` says whether the player was exploring (`mode`), and
`tools/metrics_report.py` gives the median fps exploring by tier next to the courtyard's. The open-world events
(`explore_started`, `explore_ended` with the time in the bazaar and on the scooter, `job_started`, `job_ended`,
`level_up`, `phone_app`, `town_cracker_placed` with the safety verdict, `town_cracker_lit`, `town_show` with whether the
player was near) feed the report's open-world section: who explores, for how long, which jobs get finished, which
safety rules turn players away most, and whether explorers come back more than others. Like every event, they carry
seconds, counts, catalogue ids and short reason ids only: never a position, a route or anything typed.

## If a budget is missed

The levers, cheapest first; all are constants:

1. **People drawn:** `CrowdView.HighPool` and `MidPool` (24 and 14), and `DrawRange` (30 m).
2. **Detail range:** `TilePlanner`'s full-detail and simple ranges by tier (60 or 80 m, 130 or 170 m).
3. **Traffic drawn:** `TrafficView.DrawRange` (100 m).
4. **The night haze** can come in closer, hiding a shorter draw distance.
5. **Show crackers:** the scene's limit by tier; a town show refusing many on a phone can use fewer pads.
6. **The town's tier gate:** raising the town to the high tier only, as the open-world plan allows.
