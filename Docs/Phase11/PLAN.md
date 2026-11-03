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

| 2. Interiors | Done. `Core/Home/Interiors`: `RoomDef` and `Rooms` (the main room: a rug, a low table, two floor cushions, a wall shelf, a chest and a window; the upper room: a rug, a divan, a side table, a shelf and a window), `InteriorBudget` (decoration units 14, 22 and 32 by tier; triangles 12,000, 20,000 and 32,000; real lights 0, 1 and 2), `InteriorGenerator` (walls, floor, ceiling, skirting, cornice, windows with the night behind them and the furniture, in the house's own colours, paler inside), `InteriorPlacement` (which items come in: lamps, plants, furniture, rangoli and balloons, not strings, garden lamps, tulsi or the charpai; the pose from a tap, standing facing the door or hanging facing into the room; the rules with plain reasons: the doorway, walls and corners, hanging height, what an item stands on, the edge of a table, in the way of furniture or another item, the room's budget for the tier). A decoration's `room` marks it as inside (saved in house.json and the journal as `item@room`, so older lines read as outdoors); everything outdoors (the placement rules and budget, the neighbourhood's detail, aerials and strands, decoration mode, visits) skips indoor items, and a room that doesn't exist loses its items. `Gameplay/Interiors/InteriorController` (going in fades out, puts away the house, its decorations and colliders, the neighbourhood with its ambient life, the courtyard avatar and the moon, builds the room 60 m below the courtyard with its colliders and lights, and fades back; Upstairs and Downstairs swap rooms; coming out destroys the room and its materials and brings everything back; not during a visit, a walk or decoration mode), `RoomSurfaceMarker`. `UI/InteriorMode` ("Go inside" above Countdown; inside, the HUD steps aside for a bar with the room, Upstairs or Downstairs and Outside, a strip of tonight's decorations that suit a room, Remove for a tapped item and the reason line; drag to look), `InteriorTouch`. Tests: `InteriorTests` (Mono: rooms, colours, placing on tops, floors and walls, every reason, budgets by tier, the save format), `InteriorSmokeTests` (Play Mode: go in, place by API and by a tap, a refused tap, pick and remove, upstairs, come out with the outside and HUD back and the room gone, saved with the house; not during a walk) |

| 3. Photo-assisted avatar | Done (not yet tried with a real photo on the tablet). `Core/Avatar/PhotoAnalysis` (from a small RGB copy and the face the detector found: the cheeks and forehead give the skin colour, matched to the skin ramp by brightness and warmth separately; a patch above the hairline gives the hair colour when it is near one of the creator's hair colours, otherwise the avatar keeps its own; widths at the forehead, cheeks and jaw and the face's height give round, oval, square, heart or long; too small or at the edge gives nothing), `PhotoSuggestion.ApplyTo`. `UtsavPhoto.androidlib` (`PhotoFaceActivity`: the camera app through a FileProvider or the system photo picker, no CAMERA or storage permission; decodes at most 480 pixels, upright from the photo's orientation, finds the face with `android.media.FaceDetector`, deletes the camera's file at once, and holds only the small copy's bytes until the game takes them). `Gameplay/Avatar/PhotoAvatar` (18+ only, the answer kept on the phone; the pixels are cleared as soon as they are read; `PhotoAvatarEvent` outcomes only) and `PhotoAvatarReceiver`. The avatar creator's Looks tab starts with "Suggest a look from a photo": the age question once, what happens to the photo, Take a photo or Choose a photo, then the suggestion applied as a normal change the player can adjust, or a plain reason (no face; cancelled goes back). Saying under 18 removes the button for good. Tests: `PhotoAnalysisTests` (Mono: skin tones, hair colours, no hair, five face shapes, too small or off the edge), `PhotoAvatarSmokeTests` (Play Mode: an adult's photo suggests tone, hair and shape and its pixels are cleared; no face; cancel; under 18 never sees it) |

| 4. Creator pack pipeline | Done. `tools/creator/pack.sh` (builds itself from the game's Core code with the Mono inside Unity, no editor): `new` (a template of each type that already passes: shows from a built-in template, a wardrobe garment, the sample neighbourhood renamed; with `pack.json`, `content/` and `REVIEW.md`), `lint` (the game's own `PackLint`, with shows checked against the real cracker timings exported by the scene build to `tools/creator/crackers.json`), `build` (sealed in the hosting layout), `review` (the advisor signs only when every box in `REVIEW.md` is ticked; the name and date go into the manifest), `submit` (checks, seals and zips the pack with its signed review; refused before the review), `catalog` (the owner rebuilds catalog.json, the newest version of each pack), `selftest` (one pack of each type through the whole flow, an unknown cracker caught; run by the compile check). `tools/creator/Json.cs` (a JSON reader and writer with JsonUtility's shapes). `ShowPlayer.TimingOf` is shared by the game and the export. `Docs/Phase11/CREATORS.md` (for creators, advisors and the owner). `Docs/Phase11/creator_sample` (the self-test's packs in the hosting layout). Tests: the tool's self-test (compile check), `CreatorPackSmokeTests` (Play Mode: the tool-made catalog reads and all three packs install through the game's pack manager; the neighbourhood registers and the show is offered in the designer) |

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

### Step 2 notes

- **Memory bounded:** only one of inside and outside is drawn at a time. The outside is put away rather than
  destroyed, so coming out is instant; its meshes stay in memory, which the low tier's budget already allows for. The
  room is destroyed when the player comes out.
- **Visits show the outside only.** A friend's interior isn't shared (the snapshot carries it, but the visit doesn't
  open it).
- **Review list:**
  - Should the courtyard avatar walk in with the player?
  - A third room (a kitchen or a puja corner) needs the cultural review first.
  - Strings of lights across the ceiling indoors (path items inside).
  - Crackers are never lit indoors; should "Go inside" say so when the player has some placed?

### Step 3 notes

- **Not yet tried on a real face.** The tests use drawn faces. The first photos on the tablet will show how close the
  skin tone and face shape come; the thresholds in `PhotoAnalysis` are the knobs.
- **Privacy, as the PRD asks:** on the phone only, the photo deleted immediately, 18+ only, failure falls back to
  presets. The suggestion is stored like any manual choice and goes with the avatar's other settings (and Delete
  clears it with the avatar). The privacy policy and the Play forms are updated in step 5.
- **Review list:**
  - Lighting shifts skin tone. Should the result screen offer "lighter / deeper" nudges right there?
  - Suggest eye colour and brows too?

### Step 4 notes

- **More regions arrive this way:** a creator builds a neighbourhood pack, an advisor from that region signs the
  review, and the owner publishes it. The tool enforces the review; choosing the advisors is the owner's.
- **Windows:** the wrapper is a bash script. Creators on Windows can run it in WSL or Git Bash; a `.cmd` wrapper can
  come with the feedback round if needed.
