# Phase 7 plan: house customisation and decoration mode (R1)

PRD section 19, Phase 7: features F17 (house exterior customisation) and F18 (decoration mode with undo and redo). PRD 9
has the design, PRD 12 the decoration screen, and PRD 13.5 `house.json`. Started 2026-10-28, ahead of R0 data, by the
owner's decision. Feedback and changes come after all PRD features are built.

## Definition of done (from the PRD)

- **F17:** swap modules, change colours, place decorations, save.
- **F18:** 50-step undo; a crash-safe journal.
- Killing the app during an edit restores the state on restart.
- **Deliverables:** the house editor, saved layouts and a journal recovery test.
- **Exit gate:** decoration use per user and the return rate justify going on to neighbourhoods.

## Where the house is

The fireworks need the sky. A two-storey house front facing the camera would fill the upper half of the screen. So the
player's house stands **behind the fireworks camera, facing the courtyard**, and decoration mode turns the camera
around to face it. The lot is the house front, its step and door, the courtyard floor and the boundary walls.
Decorations in the courtyard (rangoli, diyas, lights along the walls) are also seen during fireworks.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Model, history and journal | House kit (attachment points that accept module types), module and decoration catalogues (category, festival tags, placement mode, surfaces, footprint, cost), `HouseState` (`house.json`) with versions and migration, edit commands with 50-step undo and redo, an append-only journal that replays an interrupted session, named layouts (3 free) | Every command undoes exactly; journal replay after a simulated kill; save, load and migrate |
| 2 | House generator | Procedural house front: shell, doors, windows, balconies, roofs, boundary walls and gates as modules at attachment points; wall colours and materials | Every module fits every point it accepts |
| 3 | Decoration items | Procedural diyas, lanterns, plants, furniture; path items (string lights, torans, marigold garlands) between two points; stamped rangoli templates | Catalogue lint: budgets, footprints |
| 4 | Placement validation | Raycast to surfaces, footprint overlap with walls and items, support angle within 20°, door clearance volumes, budgets per category and per tier, plain-language reasons | Every rule, with reasons |
| 5 | Decoration mode | Camera turns to the house; a catalogue drawer by category and festival; ghost preview tinted green or red; place, move, two-finger rotate, limited scale, duplicate, delete; undo and redo buttons; layouts; a House tab for modules and colours | Play Mode: place, undo, redo, save |
| 6 | Courtyard and recovery | Decorations shown during fireworks; restore on start; **journal recovery test** (kill mid-edit) | Play Mode recovery |
| 7 | Measure and review | Analytics (`decoration_placed` and others; no free text), performance budget per tier, a cultural review list for the decoration items | Schema; GC guard |

## Status

| Step | Status |
|---|---|
| 1. Model, history and journal | Done. `Core/Home`: `HomeCatalog` (9 attachment points, 20 modules, 8 wall and 6 trim colours, 4 materials, 22 decorations in 6 categories with festival tags, placement mode, surfaces, footprint, cost, scale limit), `HouseState` (`house.json` v1: design, 3 free named layouts, save serial; migration from v0; repair of edited saves), commands (place, remove, pose, module, paint, load layout) with exact 50-step undo and redo, `HouseJournal` and `HouseEditor`. `Gameplay/Home/HouseStore` writes `house.json` atomically and keeps `house.journal` beside it. Tests: `HouseModelTests` (Mono and Unity), `HouseStoreTests` |
| 2. House generator | Done. `Core/Home`: `HouseMesh` (engine-free static mesh: boxes, arches, spandrels, domes, a wall with reveal-lined holes; Unity winding), `HouseGeometry` (house space, each point's frame, opening and allowed volumes, shell solids, palette), `HouseGenerator` (the shell and all 20 modules, built from each point's opening size). `Core/Art/HouseTexturePainter` paints neutral plaster, brick, sandstone and plank textures that the wall colour tints. `Gameplay/Home/HouseView` builds one object per point, rebuilds only changed modules, shares materials per paint and colour, and adds a porch lamp and a facade uplight. Captures: `Utsav/Phase 7/Capture House Shots`. Tests: `HouseGeneratorTests` (every module at every point it accepts: inside its volume, clear of the shell, fills the opening, well formed; volumes never overlap), `HouseViewTests` |
| 3. Decoration items | Done. `Core/Home/DecorGenerator`: 11 free items (clay diya, brass samai, akash kandil, paper lantern, garden lamp, tulsi vrindavan, marigold pot, palm, bench, cane chair, charpai) and 5 rangoli templates (lotus, peacock, geometric, diya ring, Tihar mandala), each built to its catalogue footprint; 5 path items (warm and coloured string lights, light curtain, mango-leaf toran, marigold garland, hanging marigold strands) hung between two points with sag. `DecorBudget`: units and triangles per tier, cost per 3 m of path. `HousePalette.Decor`: colours per item and variant, glowing paints. `HouseMesh` gained lathe, ball, disc, strut, two-sided and flat shapes. `Gameplay/Home/DecorView` (one shared mesh per item, path meshes per item, updates only what changed) and `HomeRendering` (mesh conversion and shared materials for the house and decorations). Captures: `Utsav/Phase 7/Capture Decoration Shots`. Tests: `DecorGeneratorTests` (catalogue lint: footprints, triangles per budget unit, path ends and sag, variants, light), `DecorViewTests` |
| 4. Placement validation | Done. `Core/Home/DecorPlacement`: the house's surfaces for a design (floor, step, plinth, house front, boundary wall faces and tops, balcony rail, roof terrace and parapet, or the tiled slopes), its solid parts, and the door and gate clearances; a raycast to the surfaces; snapping to a 5 cm grid and 15 degree turns; hung items hang from walls and rail edges facing out; rules for surface type, support within 20 degrees, footprint on its surface, the house, other items (items may stand on rangoli), door and gate clearance, hanging height, path length and route; budgets per tier and per category; a plain-language reason for every refusal; `Audit` lists items a module swap left unsupported. Tests: `DecorPlacementTests` (every rule with its reason) |
| 5. Decoration mode | Done. `CameraRig` override (eases to the house and back through a point above the courtyard; cuts with reduce-motion). `Gameplay/Home`: `HomeService` (house.json and journal, the editor, views, placement for the current tier, colliders for the house's solid parts so crackers keep out of walls), `HomeInstaller`, `DecorationController` (house and floor views with pan and pinch zoom; an armed item's ghost tinted green or red with the reason; place, pick, drag to move, re-hang a strand by an end, two-finger rotate and pinch scale, turn, size, colour, copy and delete buttons; undo and redo). `UI/DecorationMode` (house button in the HUD; top bar: view, layouts, undo, redo, Done; Decorate tab: festival filter, categories, item shelf; House tab: modules, colours, wall finish; selection bar; layouts sheet with 3 free slots). `CommandBatch` (both windows in one undo step). The scene's walls and gate are now the house's boundary and gate modules. Tests: `DecorationModeSmokeTests` (Play Mode: open, place, undo, redo, save, red ghost, select and move, house tab, shelf filter, layouts, journal recovery after a kill) |

### Step 1 notes

- **Journal format.** Line one is `B|serial` (the save the session started from). Then `D|command`, `U` or `R` per action.
  Every line ends with `#` and an FNV-1a checksum. Replay stops at the first torn or damaged line. A journal with
  another serial is discarded, so a kill between writing `house.json` and resetting the journal never applies an edit
  twice.
- **Undo after a save.** A save clears the undo list. A replayed journal therefore always starts from the save on disk.
  Decoration mode saves when the player taps Done (step 5), so undo covers the whole visit.
- **Layouts** are saved straight to `house.json` (the caller saves after `SaveLayout` or `DeleteLayout`). Loading a layout
  is an undoable command that carries the whole design, so the journal replays it even if the slot changes later.
- **Numbers** in the journal use the invariant culture, so a phone set to a decimal-comma locale reads its own journal.

### Step 2 notes

- **House space.** The door is at the origin and the front faces +z, toward the courtyard. In the Main scene the house stands at
  world z -2.4, 0.4 m behind the fireworks camera. The boundary walls (x = +-3.6) and the back wall with the gate (z = 6) land
  exactly on the courtyard walls they replace in step 6.
- **Volumes.** Each point has boxes its module may fill, and no two points' boxes overlap. So any mix of modules is free of
  collisions by construction, and the test checks every vertex of every module.
- **Lighting.** The moon shines toward +z, so the front was lit only by ambient light. The view adds a warm porch lamp and a
  cool uplight aimed up at the front from the far end of the courtyard. Both are realtime with no shadows: two additional
  lights, within the low tier's per-object limit of 2. Window glass is emissive (lit rooms).
- **Wall colour 0** is the warm lime of today's courtyard plaster, so the fireworks view does not change when step 6 swaps the
  scene's walls for the boundary module. Lime white moved to the end of the list.
- **Review list** (with the cultural review): the jharokha, jali, chhajja and chhatri shapes, and the door panel depth (the
  raised panels read faintly at night).

### Step 3 notes

- **Budgets.** Units per tier: 60 (low), 100 (mid) and 160 (high). Every item stays within 500 triangles per unit it costs,
  so a full budget is at most 30,000, 50,000 or 80,000 triangles. Path items cost their units per started 3 m, up to 6 m.
  Step 4 enforces the budgets.
- **Item space.** The base centre is at the origin and the front faces +z. Width is on x, depth on z and height on y. Hanging
  items (kandil, paper lantern) are built standing on their bottom; step 4 hangs them from a hook at their height.
- **Glow.** Flames, bulbs, lantern paper and lamp glass are emissive. They do not light the scene, which keeps the cost
  flat however many diyas are placed.
- **Review list** (with the cultural review): the samai, kandil, tulsi vrindavan and toran shapes, the rangoli templates
  (the peacock's default colours read poorly; blue and green would suit it), and the palm fronds.

### Step 4 notes

- **Engine-free raycast.** Surfaces are flat patches built from the design, so decoration mode (step 5) does not need
  colliders on the house. The tests drive the same raycast a touch would.
- **Order of rules.** Surface type, then slope, hanging height, the house's solid parts, door and gate clearance, the
  footprint's edge, other items, then the budget. The player sees the first rule broken. A pot pushed into a wall says
  "into the wall", not "over the edge".
- **Category shares** of the tier's units: lamps 50%, lights 50%, garlands 40%, plants 35%, furniture 30%, rangoli 25%.
  Tune these with the cultural review and the device performance pass (step 7).
- **Rangoli before the door** is allowed (it is where rangoli goes); anything standing there is not.

### Step 5 notes

- **Where the house is.** The courtyard's old walls and gate are gone from the Main scene. The `HomeInstaller` builds the
  house's boundary and gate modules in their place (with colliders), so the fireworks view keeps its walls, now in the
  player's chosen style.
- **Touches.** Decoration mode registers a full-screen input blocker, so the lighter, cracker placement and waves ignore
  touches while it is open. Its own controller reads the touches and ignores those over its bars and drawer.
- **Saving.** Done (or Android back with nothing selected) saves `house.json`. Closing any other way (scene unload, app
  killed) does not save; the journal replays the edits on the next start. The Play Mode test checks this.
- **Step 6 is mostly done here:** decorations show in the courtyard during fireworks, and the journal recovery test
  passes. What remains for step 6: decorations and the fireworks together (performance with a full budget), and whether
  the old scene diyas and lamp post stay as fixed scenery.
- **Review list:** drawer height on a phone and on the tablet, the house button icon, and whether "Floor view" is clear.
