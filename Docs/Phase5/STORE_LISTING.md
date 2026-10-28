# Play Store listing kit (Phase 5 step 5)

Everything for Play Console → **Grow users → Store presence → Main store listing**, written in honest
wording (PRD 19): every claim matches what the game does today. If a feature changes, update the text in
[store/](store/) in the same commit.

## Text

| Field | Limit | Where | Notes |
|---|---|---|---|
| App name | 30 | `Utsav Nights: Fireworks` (23) | The word "Fireworks" helps people find it; no other keywords |
| Short description | 80 | [store/short-description.txt](store/short-description.txt) (77) | |
| Full description | 4000 | [store/full-description.txt](store/full-description.txt) (about 1,260) | Ends with a real-fireworks safety line |

Play's metadata policy forbids:
- keyword lists, or other apps' names;
- "best", "#1" or "free" in the title;
- emoji in the title;
- claims the game doesn't meet, for example "realistic physics".

The text above avoids all of these. Play's machine translation can cover other languages; a Hindi listing
is worth writing by hand later if the closed test shows demand.

## Graphics

| Asset | Play's rule | File |
|---|---|---|
| App icon | 512 × 512 PNG, 32-bit, full square (Play rounds the corners) | [store/play-icon-512.png](store/play-icon-512.png) |
| Feature graphic | 1024 × 500 PNG or JPEG, no transparency | [store/feature-graphic-1024x500.png](store/feature-graphic-1024x500.png) |
| Phone screenshots | 2 to 8; 320 to 3840 px per side; long side at most twice the short side; no alpha | `tools/store_screenshots.sh` |
| Tablet screenshots (7" and 10") | Same rules; optional, but needed to be listed as tablet-optimised | Same script on the Redmi Pad Pro |

The icon and feature graphic are our own procedural art, painted by `StoreArtPainter`. To repaint them, use
**Utsav → Store → Generate App Icon and Store Art**. The same tool sets the Android launcher icon, so the
tablet shows the anar icon instead of Unity's default.

The feature graphic has no text, so it works in every language. Play shows the app name next to it anyway.

### Screenshot shot list (portrait)

Use a release APK, so there is no developer overlay. Run `tools/store_screenshots.sh` and press Enter at each
moment:

1. **The lighter at the fuse:** the flame touching an anar's fuse, sparks starting. This is the hook.
2. **A big anar fountain** at full height, with string lights behind.
3. **Rockets and skyshots** bursting high, the camera tilted up.
4. **The tray open,** with a cracker selected and its placement ghost on the ground.
5. **A finale** with three or more crackers going off at once.
6. **The Peacock fountain** (premium) mid-show.
7. **Settings:** Quiet mode, reduce flashes and vibration, showing it's comfortable to use.

Tips:
- Only real game frames. No added text or device frames: Play allows them, but plain frames read more
  honestly here.
- Never capture a test-ad card or the test review card.

## Promo video (optional)

- A YouTube link, public or unlisted, with ads off on the video. Play shows it first in the listing.
- **Brief:** 20 to 30 s, portrait screen recording of real play, no voice-over, the game's own sounds.
  1. Hands-free opening: the lighter reaches the fuse, 2 s of hiss, then the anar erupts.
  2. Quick cuts: chakri, rocket, sparkler.
  3. Placing three crackers and starting a finale.
  4. Ends on the finale's last burst over the courtyard.
- **Record:** `adb shell screenrecord --size 1080x1920 /sdcard/utsav.mp4` (stops at 3 minutes), then
  `adb pull /sdcard/utsav.mp4`.

## Other store settings

| Setting | Value |
|---|---|
| App category | Game → Simulation (Casual is the alternative; Simulation has fewer giant competitors) [Hypothesis] |
| Tags | Pick up to 5 from Play's list that describe it, for example Simulation, Casual, Relaxing [Verify] |
| Contact email | Your public support email; the same one as in the privacy policy |
| Website | The GitHub Pages address hosting the privacy policy and `app-ads.txt` |
| Privacy policy | The hosted `privacy-policy.html` ([COMPLIANCE.md](../Phase4/COMPLIANCE.md)) |

## Checklist

- [ ] App name, short and full description pasted
- [ ] Icon and feature graphic uploaded
- [ ] 4 or more phone screenshots, plus tablet screenshots
- [ ] Category, tags, contact email, website, privacy policy
- [ ] Promo video (optional)
- [ ] Listing preview checked on a phone (Play Console → Preview)
