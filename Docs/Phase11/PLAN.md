# Phase 11 plan: expansion (R5)

PRD section 19, Phase 11: interiors (F25), new festivals, more regions with advisors, the photo-assisted avatar (F26), a
creator pack pipeline with review, and an iOS port. Started 2026-11-03, ahead of R4 data, by the owner's decision;
feedback and changes come after all PRD features are built.

## Definition of done (from the PRD)

- **F25 Interiors:** enter and exit with a fade; memory bounded.
- **F26 Photo-assisted avatar:** the photo never leaves the device and is deleted immediately.
- **Festivals:** per-feature criteria, below.
- **Exit gate:** revenue and retention justify the content cost (measured after launch).

## Decisions (owner, 2026-11-03)

- **iOS: skipped for now.** It needs a Mac, Xcode and an Apple Developer account. It stays a later project; nothing
  in Phase 11 depends on it.
- **Photo avatar: Android's built-in face detector** (`android.media.FaceDetector`). No extra library, nothing
  downloaded, fully offline. The game samples skin tone, hair colour and face proportions from the pixels and
  suggests the closest look; the player adjusts it.
- **New festival: New Year.** Eid and Lunar New Year wait for cultural advisors. The festival model is built so
  either can be added as data later, through the creator pipeline.

## Design

- **Festivals are data.** Each festival has an id, a name, which crackers and decorations belong to it, a show
  template, and its own night (New Year counts down to midnight). The player picks tonight's festival in Settings;
  decorations, the environment browser and the show designer filter by it. Diwali and Tihar stay the default.
- **New Year, low cultural risk:** a secular, worldwide night. Gold and silver string lights, star lanterns,
  balloons and a "Happy New Year" banner; two New Year crackers; a countdown to midnight with a finale and a cheer.
- **Interiors:** two rooms (the main room on the ground floor, an upper room), generated from code like the house.
  Tapping the door fades into the room while the exterior and neighbourhood unload, and leaving fades back.
  Decorations can be placed inside, within a per-tier budget, and are saved with the house. Only one of interior and
  exterior is loaded at a time, so memory stays bounded.
- **Photo avatar privacy (PRD 8, 16):**
  - offered only after the player confirms they are 18 or older;
  - the photo comes from the camera or the system photo picker (no permission), is read into memory, analysed, and
    the file and pixels are deleted at once;
  - only the suggested look (skin tone, hair colour, face shape) is kept, in the avatar like any manual choice;
  - no face is detected: the creator says so and falls back to presets.
- **Creator pack pipeline:** a command-line tool for outside creators (template, build, lint, a review checklist that
  an advisor signs, and a submission folder), using the Phase 9 pack format and linter unchanged. More regions arrive
  this way, each with its advisor's review.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Festivals and New Year | Festival registry; tonight's festival in Settings; filters; New Year decorations, crackers and show template; the countdown to midnight with its finale and cheer; cultural review note | Registry and filters; countdown timing; Play Mode: switch to New Year, count down, finale |
| 2 | Interiors | Room models (walls, floor, windows, furniture), the door and the fade, the exterior unloaded inside, decorations inside with budgets, saved with the house | Room lint and budgets; Play Mode: enter, decorate, leave; memory inside vs outside |
| 3 | Photo-assisted avatar | The 18+ confirmation; camera or picker; the face detector and colour sampling on the phone; the suggestion in the creator; deletion | Suggestion maths from test images; the file is deleted; no face and under-18 paths |
| 4 | Creator pack pipeline | `tools/creator`: template, build, lint, review checklist and sign-off, submission; docs for creators and advisors | Builds and lints the template; a pack without a signed review is refused |
| 5 | Measure and wrap-up | Analytics (festival chosen, countdown, interiors, photo suggestion used; ids and outcomes only); performance inside and during the midnight finale; privacy and compliance updates; review list | Schema; GC guard; budgets |

## Status

| Step | Status |
|---|---|
| 1. Festivals and New Year | Done. `Core/Festivals`: `FestivalInfo` and `Festivals` (Diwali and Tihar, the default, and New Year; crackers belong by their festival tags, decorations by their festival flags, everyday items every night), `MidnightCountdown` (ten seconds, then midnight; the clock shows 11:59 pm, then 12:00 am; the greeting, the surge), `CheerSound` (a crowd's cheer synthesised: voice-band noise with whistles, swelling and fading). `GameSettings` version 3 adds tonight's festival (unknown ones fall back to Diwali and Tihar; `settings_changed` reports it). New Year decorations: gold and silver lights, a silver star lantern, party bunting, a balloon bunch (`Festival.NewYear`; the light curtain joins too). Two New Year crackers: Golden crown (a sky shot that opens into gold and falls like a willow) and Confetti fountain (four colours in turn); sparklers, rockets, sky shots and cakes join the New Year tray. `ShowTemplates.NewYear` ("Midnight": confetti, crowns, a sky shot and a cake on six pads), first in the designer on a New Year night. The tray shows tonight's crackers only; Settings has "Festival tonight"; the decoration shelf has a New Year chip. `UI/NewYearCountdown` (a Countdown button left of Shows on a New Year night; ten big numbers with a tick each, then the greeting, the Midnight show where it fits the spot, the cheer, a thud, and `AmbientLife.Surge`: the neighbours' bursts rise to 40 a minute for 45 seconds and everyone out cheers; Quiet mode and the low tier keep their silence). LICENCE_LOG own-010. Tests: `FestivalTests` (Mono), `NewYearSmokeTests` (Play Mode: the tray, shelf and show switch with the festival; the countdown from ten to midnight, the show and the surge; back to Diwali) |

### Step 1 notes

- **New Year needs no cultural advisor** in the sense Eid and Lunar New Year do: no religious imagery, nothing
  regional. The names in Hindi and Nepali (Sunehra mukut, Sunaulo mukut; Rangeen fawwara, Rangin phohara) still want a
  native speaker's check with the rest of the names.
- **Adding a festival later** is data: a `FestivalInfo`, the festival's flag on its decorations, its tag on its
  crackers, and an optional show template, after the advisor's review.
- **Review list:**
  - Should the game pick New Year by itself around 31 December (and Diwali in its season)?
  - A real night clock on screen during the countdown, or only the small 11:59 pm line?
  - Should the countdown be longer (30 or 60 seconds) for a party at home?
