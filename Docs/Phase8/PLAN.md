# Phase 8 plan: neighbourhood and celebration locations (R2)

PRD section 19, Phase 8: features F19 (neighbourhood settlement, chunked) and F20 (celebration locations and placement
validation). The design is in PRD 10, the environment browser in PRD 12. Started 2026-10-28, ahead of R0 data, by the
owner's decision; feedback and changes come after all PRD features are built.

## Definition of done (from the PRD)

- **F19:** walk through 6 to 12 lots at 30 fps on the low tier.
- **F20:** no clipping or floating; reasons shown.
- **Deliverables:** a walkable settlement, a location picker, and an environment browser (the preview of F21 packs).
- **Exit gate:** session length and retention improve against the R0 cohort.

## Shape of the settlement

The fireworks need open sky, so nothing tall stands in front of the fireworks camera.

```
            far lots (low on screen, like today's skyline)
   ┌──────────────────────────────────────────────────────┐
   │                the square / field / ghat              │   open: the sky above it is clear
   └──────────────────────────────────────────────────────┘
   ═══════════════════════ street ════════════════════════   (walkable, past every gate)
   [lot -3][lot -2][lot -1][ HOME lot 0 ][lot 1][lot 2][vacant lot]
                              ^ house behind the fireworks camera
```

- **The player's house and courtyard** are lot 0. Phase 7 already puts them in world space (house front at z -2.4,
  back wall and gate at z 3.6).
- **Neighbour lots** line the same side of the street, left and right, so they never stand in front of the camera.
- **Across the street** is an open space: a chowk in the town, a threshing ground in the hill village, a ghat by the
  river. The far lots stand at its far edge.
- **Hierarchy (PRD 10):** environment, settlement, chunk, lot, house and yard, celebration spot. The settlement is about
  a two-minute walk across, not an open world.
- **Neighbour houses** reuse the Phase 7 house generator with seeded designs, plus seeded decorations. Module meshes are
  shared across lots (instancing), simplified at distance (LOD) and shown as impostor boxes with lit windows far away.
  There are no real lights in the neighbourhood: windows, string lights and diyas are emissive.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Settlement model | Layout JSON (environment, lots, street, open space, chunks, spots with tags, launch pads, ambient anchors), seeded neighbour house designs, three built-in layouts (town lane, hill village for Tihar, riverside), lint | Lint: 6 to 12 lots, no overlaps, every spot reachable, tags consistent; JSON round trip |
| 2 | Chunks and LOD | Chunk planner (which chunks are loaded and at what detail, for a position; memory bound; hysteresis); the settlement view builds and frees chunks; shared module meshes; LOD and impostors | Bounded chunk count; no thrash at a boundary; triangle budget per tier |
| 3 | Walking | Walk graph and A* from the street and spots; tap-to-move with drag-to-look; optional joystick; follow camera; map fast travel; the avatar walks | A path to every spot; arrival; camera never inside a wall |
| 4 | Celebration locations | Location picker with tags and plain reasons for greyed spots; moving the celebration (camera and placement area) to a spot; effect volumes and launch pads (aerials need a clear sky corridor; nothing into walls, roofs or wires) | Every spot in every environment; no clipping or floating |
| 5 | Ambient life and Night Timeline | Lit windows, string lights, distant bursts (always); looped neighbours (mid and high); crowds (high); all off in Quiet mode; a night timeline (dusk to late night) | Per tier and Quiet mode; timeline order |
| 6 | Environments and browser | Two more environments with their palettes and house styles; the environment browser (filter chips, preview, size, offline badge, download progress for later packs); cultural review list | Every environment passes lint and placement |
| 7 | Measure | Analytics (spot chosen, walk, fast travel, environment; ids only); performance while walking (30 fps target on the low tier; GC; triangles and draw calls per tier); memory pressure unloads far chunks | Schema; GC guard; budgets |

## Status

| Step | Status |
|---|---|
| 1. Settlement model | Done. `Core/Neighbourhood`: `SettlementLayout` (layout JSON: lots, street, open space, spots with tags, aerials rule and launch pads, walk graph; chunks are 16.8 m street segments), `Environments` (town lane for Diwali, hill village for Tihar, riverside ghat; house styles per environment), `NeighbourDesigns` (seeded houses with festival decorations that pass the Phase 7 placement rules), `SettlementLibrary` (the three layouts: 11, 8 and 7 lots), `SettlementLint`. `Gameplay/Neighbourhood/SettlementJson` reads layouts through the lint. Balcony placement solids are now the slab and rails (the floor stays free). Tests: `SettlementTests` (Mono), `SettlementJsonTests` |
| 2. Chunks and LOD | Done. `Core/Neighbourhood/ChunkPlanner` (full, simple and impostor detail by distance and tier, 2 m hysteresis, low-memory drop to impostors; no allocation per update) and `ImpostorBuilder` (a house as boxes with lit windows, under 160 triangles). `Gameplay/Neighbourhood`: `SettlementView` (street, open space and land; every neighbour lot at its chunk's detail; rebuilds only changed chunks; meshes and materials shared across lots through `HomeShared`; no real lights; colliders for the houses' solid parts), `NeighbourhoodInstaller`. The Main scene's floor is now just the courtyard; the old skyline houses are replaced by the settlement (the temple spire and water tank stay as far landmarks); the camera's far clip is 150 m. Captures: `Utsav/Phase 8/Capture Neighbourhood Shots`. Tests: `ChunkPlannerTests` (Mono, including the whole settlement's triangles per tier at every spot), `SettlementSmokeTests` (Play Mode) |
| 3. Walking | Done. `Core/Neighbourhood/WalkNavigator` (walkable areas: courtyard, gate opening, street and open space, vacant plot, rooftop and balcony by stairs when the house has them; straight walks inside the areas, otherwise A* over the walk graph; joystick sliding along edges; home availability tags) and `WalkAgent` (walking pace 1.4 m/s, turning, stairs fades). Gates open: the gate module has an open variant (leaves swung into the courtyard) and the gate's solid is split into pillars and leaves. `Gameplay/Neighbourhood/WalkController` (the player's avatar walks; follow camera with drag-to-look and limited zoom, kept in front of walls; tap-to-move; joystick; fast travel with a fade; the settlement's detail follows the avatar). `UI/WalkMode` (walking-figure button; Home, Map and Joystick; a map with a pin per spot and the avatar's dot), `UI/StickInput`. Walk and decoration modes never open together. Tests: `WalkNavigatorTests` (Mono), `WalkModeSmokeTests` (Play Mode) |
| 4. Celebration locations | Done. `Core/Neighbourhood/CelebrationRules` (spot availability from the house's and the settlement's tags with plain reasons: a sloping roof, no balcony, a jharokha's low canopy; aerials rule, launch pads, play areas, the player's strands overhead). `PlacementRules`: four new blocks with messages (outside the area, no aerials here, pads only, nothing overhead). `PlacementValidator` gathers them (sky clear 40 m up by raycast and the player's strands). `Gameplay/Neighbourhood/CelebrationService` (choose a spot: the fireworks view and the placement area move there; crackers left at the old spot are cleared; a house change that closes the spot falls back to the courtyard; every session starts at home). Ground markers on the street, open space, empty plot, roof deck and balcony floor. Walk screen: map pins greyed when closed, a card with the reason, Go there and Celebrate here; a Celebrate here button when standing in a spot. Tests: `CelebrationRulesTests` (Mono), `CelebrationSmokeTests` (Play Mode: every spot, every aerial rule, closed spots, celebrate here) |
| 5. Ambient life and Night Timeline | Done. `Core/Neighbourhood/Ambient`: `NightTimeline` (dusk, evening, night, late over a session's first 24 minutes of play, then holding; sky exposure, ambient light, distant-burst rate and how many people are out; a 6:30 pm to 1 am clock), `AmbientPlan` (bursts on every tier; up to 3 looped neighbours on mid and 6 on high; a 12-person crowd on high only; nothing moving in Quiet mode; low memory keeps 2 neighbours and no crowd), `DistantBursts` (seeded, far beyond the settlement, above every roof), `AmbientAnchors` (a neighbour in each built lot's courtyard; a crowd ring on the open space, clear of every spot's play area). `Gameplay/Neighbourhood/AmbientLife`: street lamps and strings of lights along the street (emissive only), the bursts from one pooled particle system (no allocation), neighbours and crowd as avatars with idle and celebrate loops, the night's sky and ambient on an instance of the sky material. Tests: `AmbientTests` (Mono), `AmbientLifeSmokeTests` (Play Mode: people by tier, Quiet mode through the real setting, the sky darkening) |
| 6. Environments and browser | Done. `Core/Neighbourhood/EnvironmentFeatures` (riverside: ghat steps, the river, 60 floating diyas, the far bank; hill village: a chautari with a pipal tree and diyas, layered hills; town lane: a chowk platform with diyas), counted by the lint and walked round by the navigator. `EnvironmentPreviews` (painted 512 x 288 cards). `UI/EnvironmentBrowser` (filter chips for festival and region; cards with preview, region and festival, description, size, an "On this phone" badge, a progress bar kept for Phase 9 packs; Choose ends the walk, builds the place and remembers it), opened from the walk map's Neighbourhoods button. By the river the land stops at the ghat and the far landmarks are hidden. `Docs/Phase8/CULTURAL_REVIEW.md`. LICENCE_LOG own-009. Tests: `EnvironmentFeatureTests` (Mono), `EnvironmentBrowserSmokeTests` (Play Mode) |
| 7. Measure | Done. Analytics: `walk_started` (environment), `walk_ended` (seconds, metres), `fast_travel` (spot), `spot_chosen` (spot, kind, environment), `environment_chosen`, and `environment` and `walk_s` on `session_end`. Never a position or a route (schema test; the privacy policy says so). The metrics report gains a neighbourhood section for the exit gate: share of users who walk, walks and walk length, median session and D1 and D7 return of walkers against everyone else, spots and environments chosen; compare with an R0-only export for the R0 cohort. The BigQuery query has the new columns. Performance: a walking guard (along the whole street on the high tier, chunks changing detail on the way) and the triangle budgets of step 2. Tests: `AnalyticsSchemaTests`, `AnalyticsSmokeTests` (neighbourhood events), `PerformanceGuardTests` (walking), metrics report self-test |

### Step 1 notes

- **Lint rules:**
  - 6 to 12 lots, with the home lot exactly where the Main scene's house stands.
  - No overlaps between lots, the street and the open space.
  - Spot play areas, pads and cameras are clear of walls.
  - The sky is clear 18° and 30° up, straight ahead and 12° either side, from every fireworks camera.
  - Every spot is reachable on foot from the courtyard, with no walk through a wall (gates open; stairs are a fade).
- **Home spots depend on the house:** the rooftop needs a flat roof or terrace (`has_rooftop`), and the balcony needs a
  balcony (`has_balcony`). The lint is checked with every roof and balcony module. A jharokha's canopy blocks the
  balcony's sky, so that combination greys the balcony spot (step 4 gives the reason).
- **Neighbours** use up to 20 decoration units each, a rangoli first, so the far lots look festive at a glance.

### Step 2 notes

- **Triangle budgets for the whole settlement** (houses, modules, decorations; fireworks not included): 150,000 on the
  low tier, 250,000 on the mid tier and 400,000 on the high tier. Every environment fits standing at every spot.
- **Detail radii in chunks** (low, mid, high): full 0, 1, 1; simple 1, 1, 2; impostors 3, 3, 4; beyond that nothing is
  loaded. Simple detail keeps only the decorations that glow (lamps and lights).
- **House backs are never modelled.** No camera sees them: the lots beside the home show their fronts to the street,
  and the far lots face back across the open space.
- **Lighting:** the neighbourhood is lit by ambient light, the moon and its own glowing windows and lamps (no real
  lights). The street and open ground are tinted lighter so they read at night. Step 5 adds street lamps and string
  lights.

### Step 3 notes

- **Walkable ground** is a set of flat rectangles, each kept 0.3 m (the avatar's radius) from the walls around it.
  Straight walks need every point on the line inside the areas; other routes follow the walk graph, which the lint keeps
  out of walls. A tap on a wall or in a neighbour's yard walks to the nearest walkable point.
- **Stairs** (courtyard to rooftop, courtyard to balcony) are a short fade, not a walk.
- **The joystick** is off by default (PRD 10: tap-to-move is the default); the Joystick button turns it on and the choice
  is remembered.
- **Review list:** whether the gate leaves should swing with an animation, the walk speed, the camera distance on a phone
  screen, and the map's look.

### Step 4 notes

- **Rules by spot:**
  - Courtyard: bounded by its walls.
  - Rooftop and balcony: bounded by their parapet and rails.
  - Everywhere else: a circle around the spot (the play area).
- **Aerials (rockets, sky shots, cakes):**
  - The street allows them only from its two launch pads (string lights and eaves overhead).
  - The balcony allows none.
  - Everywhere else they need 40 m of clear sky and no strand of the player's own lights within 0.5 m above.
- **The balcony is a sparkler spot.** A fountain's spray (1.2 m wide) does not fit between its rails, and the effect
  volume check refuses it with "Not enough room for the sparks".
- **Review list:**
  - Should the courtyard avatar stand at the chosen spot?
  - Should each spot get a starting cracker?
  - Do the launch pads need a visible marker on the ground?

### Step 5 notes

- **Quiet mode** stops the distant bursts and sends the neighbours and crowd in. Lamps, strings of lights and lit windows
  stay on: they are part of the place, not activity.
- **Garbage per frame** stays at about 0.01 KB idle with the ambient life running.
- **Review list:**
  - Distant bursts are silent; a faint far-off boom would add life. Mix it under the Quiet mode rule.
  - The crowd stands in a ring around nothing; a shared fountain in the middle would explain it.
  - Should the night clock show on screen?
  - The crowd and neighbours' look (presets) need the cultural review.

## Phase 8 result

The neighbourhood and celebration locations (F19, F20) are built.
- **The settlement:**
  - three environments with their own landmarks, 7 to 11 lots each;
  - chunked detail within triangle budgets per tier;
  - walking with tap-to-move, a joystick and the map's fast travel.
- **Celebration spots and placement:**
  - celebration spots with plain reasons when closed;
  - placement that keeps aerials under open sky;
  - ambient life by tier and Quiet mode, and the Night Timeline.
- **The environment browser.**
- **Exit gate data:** the neighbourhood section of the metrics report (walkers' session length and return against
  everyone else, and against an R0-only export).
- **Measured in the editor:** walking the whole street on the high tier, with chunks changing detail, allocates 0.26 KB
  per frame on average (0.90 KB at most); idle stays at 0.01 KB.
- **Open items** (with the feedback round after all PRD features):
  - the cultural review (`CULTURAL_REVIEW.md`);
  - 30 fps while walking on a low-tier phone (measure on the device with the playtest log);
  - the review lists in the step notes above.
