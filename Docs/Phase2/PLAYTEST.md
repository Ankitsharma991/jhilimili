# Phase 2 playtest and measurement protocol

Phase 2 is done when the definition of done and the exit gate below pass (PRD 19, Phase 2).
Use the release APK from `Utsav > Build > Android Release APK`.

## Tools in the build

- **Playtest log:** every session appends JSON lines (time to first ignition, ignitions, payoffs,
  flame-outs, quality changes, a performance sample every 30 s) to the app's files folder.
  Collect with `tools/pull_playtest.sh`; it prints one summary row per session.
- **Developer overlay:** triple-tap the top-left corner to show fps, p95 frame time, tier,
  governor steps, particles and voices. Triple-tap again to hide. Hide it before usability tests.
- **Fresh first run:** `adb shell pm clear com.ankitsharma.utsavnights` resets the hint, the
  first-ignition flag and the detected tier.

## 1. Performance capture on 3 devices (definition of done)

Per device: clear data, launch, light the flowerpot 10 times in a row, keep the overlay visible,
then pull the log. Run `tools/thermal_soak.sh 10` alongside for heat.

| Device | RAM | Detected tier | Avg fps | Min 30 s fps | Governor steps | Peak memory MB | Battery temp start/end | Notes |
|---|---|---|---|---|---|---|---|---|
| Redmi Pad Pro (mid) | 6 GB | | | | | | | No vibrator: haptics untestable |
| Budget phone (3 to 4 GB) | | | | | | | | Gate A carry-over |
| Third phone | | | | | | | | Test vibration here |

Launch to lit flowerpot under 10 s: measure with a stopwatch from tapping the icon to the fountain.

## 2. Unaided first-ignition test (F02 acceptance)

5 people who have not seen the app. Clear data before each person. Hand them the phone with the
app closed, say only "This is a Diwali fireworks app, try it". Do not help or point.

| Tester | Ignited unaided? | Time to first ignition (log) | Confusion notes |
|---|---|---|---|
| 1 | | | |
| 2 | | | |
| 3 | | | |
| 4 | | | |
| 5 | | | |

**Pass:** 4 or more of 5 ignite within 30 s.

## 3. Side-by-side feel test (exit gate)

10 target users (PRD 2 primary persona: adults who miss home celebrations). Competitor: the top
Android result from Phase 0, for example *Diwali Crackers Simulator Game* (gl.com.diwalisimulator).
Alternate which app goes first. Each person lights one fountain-type cracker in both apps, then
answers: "Which one felt more like lighting a real anar?" and "Why?".

| Tester | First app | Prefers | Reason (their words) |
|---|---|---|---|
| 1 | | | |
| 2 | | | |
| 3 | | | |
| 4 | | | |
| 5 | | | |
| 6 | | | |
| 7 | | | |
| 8 | | | |
| 9 | | | |
| 10 | | | |

**Exit gate:** a majority (6 or more) prefer our ignition and payoff. If not, iterate before Phase 3.

The fountain uses synthesised placeholder audio. Sound is half of "feel" (PRD 4, 11), so run this
test after licensed flowerpot recordings replace the placeholders, or record the result as
provisional.

## Results summary

| Item | Target | Result |
|---|---|---|
| Launch to lit flowerpot | under 10 s | |
| Unaided ignition | 4 of 5 within 30 s | |
| Side-by-side preference | 6 of 10 or more | |
| Fps on budget phone, Low tier | 30 or more | |
