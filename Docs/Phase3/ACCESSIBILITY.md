# Phase 3 step 12: accessibility and offline checklist

PRD section 14 (accessibility) and F09 (works in airplane mode). "Auto" means a test covers the item and
runs with every build. "Device" means someone has to check it by hand on a phone or tablet.

## Checklist

| # | Item | How | Check | Status |
|---|------|-----|-------|--------|
| 1 | Touch targets of 48 dp or more | Switches, sliders and the quality control have a 48 dp hit area around the smaller visible part | Auto: `AccessibilitySmokeTests.EveryControlIsAtLeast48Dp` measures every control, with the settings sheet open and closed | Pass |
| 2 | Text contrast meets WCAG AA (4.5:1) | Theme colour pairs checked. Selected tray items and quality segments now use indigo text on saffron. Off-white on saffron was only 2:1 | Auto: `UiContrastTests` | Pass |
| 3 | System font size | Text follows the Android font size setting, up to 130 %. Labels shrink to fit their space with a minimum size, so nothing is cut off | Device: set the largest font in Android settings, then open the tray and settings | To check |
| 4 | Screen reader (TalkBack) | `ScreenReaderSupport` lists the settings button, the 8 tray items (with their selected or locked state), the hint and the finale button. With settings open it lists every switch and slider with its value, the 4 quality choices and Done. Double-tap activates; swipe up or down changes sliders by 10 % | Auto: `AccessibilitySmokeTests.ScreenReaderListsMainAndSettingsControls`. Device: turn TalkBack on and move through each item by swiping | To check |
| 5 | Reduce motion | The settings switch turns off camera shake and push-in. On first launch it is switched on when Android "Remove animations" is on | Device: turn on Remove animations, clear the app's data, then launch | To check |
| 6 | Reduce flashes | Already in place: halves bright flashes and caps them at 3 per second (Phase 2) | Auto: `EffectsTests` | Pass |
| 7 | Quiet mode | Already in place: lower volume, no shake, softer haptics (step 8) | Auto: `GameSettingsTests` | Pass |
| 8 | No network code | Game scripts must not call network APIs, and the INTERNET permission is not forced on. The APK still lists INTERNET because Unity adds it for the Addressables and Localization packages, and Phase 4 ads will need it. Nothing in the game uses it | Auto: `OfflineGuardTests` | Pass |
| 9 | Airplane mode | Turn on airplane mode, start the app from cold, place and light all 8 crackers, run a finale, change settings, then restart and confirm the settings were kept | Device | To check |

## Known limits

- Lighting a fuse with TalkBack on is still a gesture on the 3D scene, and TalkBack captures single taps.
  Players can still pick and place crackers from the tray and start the finale with TalkBack. A
  "light selected cracker" action is a candidate for Phase 4, if testers who rely on TalkBack ask for it.
- Font scaling stops at 130 %. Above that, the tray would take more than 20 % of the screen (F04).
- The contrast check assumes a dark scene behind the translucent panels. That holds for the night
  courtyard. A bright burst behind the tray can briefly lower contrast.

## Device run log

| Date | Device | Items | Result | Notes |
|------|--------|-------|--------|-------|
| | | | | |
