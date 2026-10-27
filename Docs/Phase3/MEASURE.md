# Phase 3 measurement protocol (step 13)

Phase 3 is done when the definition of done and the exit gate in [PLAN.md](PLAN.md) pass. Use the
release APK (`Utsav > Build > Android Release APK`). Each section below says what it covers, how to
run it, and how to read the result.

## Tools in the build

- **Developer overlay:** triple-tap the top-left corner. It shows fps and budgets, plus two buttons:
  - **Sound test** opens the blind sound test.
  - **Stress finale** clears the scene, places six crackers (flowerpot, chakri, bomb, chain lar,
    rocket, cake) and starts a finale. It gives the same heavy load every time, for performance
    captures.
  
  Hide the overlay before usability sessions.
- **Playtest log:** every session records ignitions and the tray signals: picks, placements,
  rejected placements with the reason, cancelled picks and finales. It also takes a performance
  sample every 30 s.
- **Report:** `tools/phase3_report.py` reads everything under `captures/` and prints both reports,
  with gate verdicts. To pull from the phone: `tools/pull_playtest.sh tester-03` and
  `tools/pull_sound_test.sh`.
- **Fresh first run:** `adb shell pm clear com.ankitsharma.utsavnights`. This also deletes the
  logs on the phone, so always pull first.

## 1. Blind sound test (F03: 6 of 8 identified)

**Run this after the licensed recordings are in.** With the current synthesised placeholders the
report marks the result as provisional.

1. Find 5 or more testers who know Diwali crackers.
2. Turn the phone volume to about 70 %, in a quiet room, without headphones (PRD 11: phone speaker).
3. Show the tester the 8 names on screen first, so they know the choices.
4. Open the developer overlay, tap **Sound test**, then **Start**. The scene is cleared, and the
   screen covers it, so only the sound gives a clue. **Close** (top right) leaves at any time; a
   test stopped part way is saved as unfinished and the report leaves it out of the pass count.
5. Each sound plays once. The tester taps a name or **Not sure**. **Play again** is allowed, and
   replays are counted. Don't give any hints.
6. After the last answer the screen shows the score. Tap **Close**. The tester number counts the
   finished tests saved on the phone, so it goes up by itself for the next person.
7. When everyone is done, run `tools/pull_sound_test.sh --clear`. This saves the results to
   `captures/` and removes them from the phone, so the next round starts again at tester 1.

**How to read it:** each tester should name 6 or more of 8. The report also shows how often each
cracker was named correctly, and the most common mix-ups (for example, bomb named as chain lar). A
cracker that is often confused needs a more distinct sound: change its recording, not the test.

| Tester | Correct / 8 | Replays | Mix-ups noted |
|---|---|---|---|
| 1 | | | |
| 2 | | | |
| 3 | | | |
| 4 | | | |
| 5 | | | |

## 2. Free session (exit gate: average of 8+ ignitions, no major tray confusion)

Use 10 target testers (PRD 2 primary persona). For each tester:

1. Clear the app data. Hand over the phone with the app closed. Say only "This is a Diwali
   fireworks app, play with it as long as you like".
2. Observe without helping. Write down every moment the tester hesitates at the tray, or says
   something like "how do I...".
3. Stop when they hand the phone back, or after 10 minutes.
4. Run `tools/pull_playtest.sh tester-NN`, then clear data before the next person.

When all 10 are done: `tools/phase3_report.py --sessions captures --min-minutes 2`.

**How to read it:**
- **Ignitions** must average 8 or more across the 10 testers.
- **Kinds** is how many different crackers each tester lit. It shows whether people explored the
  tray.
- **Rejected placements** show placement rules people ran into. The report groups them by the
  message shown on screen.
- **Cancels** count picks dropped without placing anything.
- If rejections and cancels are high (more than about 3 each per session), that is likely tray
  confusion. Check those sessions against your observation notes.

| Tester | Minutes | Ignitions | Kinds | Rejections | Cancels | Used finale | Tray confusion (observed) |
|---|---|---|---|---|---|---|---|
| 1 | | | | | | | |
| 2 | | | | | | | |
| 3 | | | | | | | |
| 4 | | | | | | | |
| 5 | | | | | | | |
| 6 | | | | | | | |
| 7 | | | | | | | |
| 8 | | | | | | | |
| 9 | | | | | | | |
| 10 | | | | | | | |

**Major tray confusion:** 3 or more testers fail to place anything from the tray within 2
minutes, or 3 or more get stuck at the same step. Either one fails the gate.

## 3. Device performance

Run this on the same 3 devices as Phase 2 (mid tablet, budget phone, a phone with a vibrator).
On each:

1. Clear data, launch, open the overlay and leave it visible.
2. Tap **Stress finale** 5 times, waiting for each finale to end. Run `tools/thermal_soak.sh 10`
   alongside.
3. Run a 16-voice audio check: start the stress finale and, while it plays, light the flowerpot
   and the sparkler. Listen for any crackle or drop-out (F05).
4. Run a call or pause check: during a finale, pull down the notification shade for 5 s, or take a
   call. Sound and fuses must pause and then resume (F05).
5. Haptics, on the phone with a vibrator: check that the bomb gives a thud and the fuse a tick.
   Then switch vibration off in settings, and check it stays off after restarting the app (F06,
   F07).
6. Run `tools/pull_playtest.sh perf-<device>`.

| Device | Tier | Avg fps | Min 30 s fps | Governor steps | Peak memory MB | Temp start/end | Audio glitch? | Resume OK? | Haptics |
|---|---|---|---|---|---|---|---|---|---|
| Redmi Pad Pro (mid) | | | | | | | | | No vibrator |
| Budget phone (3 to 4 GB) | | | | | | | | | |
| Phone with a vibrator | | | | | | | | | |

**Targets:** 30 fps or more on Low (budget phone); 60 fps on mid (PRD 14); no audio glitches; and
no more than one governor step-down during a stress finale on the device's own tier.

## 4. Remaining definition-of-done items

| Item | Check | Result |
|---|---|---|
| Tray 20 % of screen or less (F04) | Auto: `TraySmokeTests` | Pass |
| One tap places (F04) | Free-session log: placements against picks | |
| Settings persist across restarts (F07) | Device step 3.5, plus `SettingsSmokeTests` | |
| Tier chosen on first run, with manual override (F08) | The report's tier column after `pm clear`; override in settings | |
| Works in airplane mode (F09) | [ACCESSIBILITY.md](ACCESSIBILITY.md) item 9 | |
| Finale chains 3 to 6 and replays (F13) | Auto: `FinaleSmokeTests`, plus the stress finale | Pass |

## Results summary

| Item | Target | Result |
|---|---|---|
| Blind sound test | each tester 6 of 8 or more | |
| Free-session ignitions | average 8 or more, 10 testers | |
| Tray confusion | none major | |
| Fps, budget phone on Low | 30 or more | |
| Audio, 16 voices | no glitch | |
