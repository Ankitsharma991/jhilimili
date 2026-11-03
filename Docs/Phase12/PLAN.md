# Phase 12 plan: the open-world slice

The first phase of the open-world plan ([Docs/OpenWorld/PLAN.md](../OpenWorld/PLAN.md)). The owner asked on 2026-11-03
for the feel of an open-world city game, "character feel, surrounding communities, settings, profile and handlers,
everything", at that level of product. This phase builds one district that plays like that on the tablet. Started
2026-11-03.

## Working assumptions (the plan's recommended defaults, until the owner says otherwise)

- **Slice first:** one district made properly, judged on the tablet, before the whole town.
- **Family-safe, 13 and up:** no crime, weapons or harm; an original festival town, not any other game's map,
  characters or story.
- **Fireworks stay at the heart:** the town is the stage for them.
- **A light festival story** with jobs, not a pure sandbox.
- **Stylised and warm** art that budget phones can draw.
- **The town needs a 4 GB phone;** lower-tier phones keep today's neighbourhood.

## Definition of done

- The avatar moves like a game character: walk, jog, sprint, jump, land, turn, with a camera that follows and can
  be turned, on a thumbstick, in landscape.
- One district (about 300 x 300 m) to roam, with roads, lanes, buildings and landmarks, streamed in tiles.
- People with routines who react to the player and to fireworks; traffic on the roads.
- A scooter to ride.
- A minimap, a phone with jobs, and three jobs to do.
- Fireworks lit anywhere it's safe.
- A player profile (name, coins, reputation, stats) and settings for controls and camera.
- 30 fps or better on a mid-range phone; the GC guards hold.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | **The character** | Explore mode in landscape; a locomotion model (walk, jog, sprint, jump with coyote time and a jump buffer, landing, turning, acceleration); a follow camera (orbit, shoulder offset, zoom, collision, recentre); run and fall animation matched to speed; footsteps; a thumbstick, look-drag, Sprint, Jump and an Interact prompt; the neighbourhood as solid ground and walls | Locomotion and camera maths; Play Mode: run across the lane, jump, collide, interact |
| 2 | **Player profile and settings** | A profile (name, coins, reputation, play stats), save and load; settings for camera sensitivity, invert, auto-recentre, sprint toggle, HUD size, left-handed layout; a pause menu | Profile round trip; settings apply live |
| 3 | **The district** | A grey-box bazaar district joined to the player's lane: a road network, lanes, a square, a river front; buildings from a district kit; tile streaming around the player | District lint (reachable, no holes); streaming budgets |
| 4 | **People** | Residents with homes and routines, walking the lanes on a navigation graph; reactions (cheer, cover ears, step back, wave, gather at shows); level of detail for crowds | Routine and reaction logic; crowd GC guard |
| 5 | **Traffic and the scooter** | Scooters, rickshaws and cyclists on the road graph; the player's scooter (get on, ride, park), people stepping aside | Vehicle handling maths; Play Mode: ride and park |
| 6 | **Map, phone and jobs** | Minimap and map with a route line; the phone (map, messages, jobs, camera, home, settings); three jobs (deliver diyas, light up the lane, a rooftop show) paid in coins and reputation | Job state machine; Play Mode: take and finish a job |
| 7 | **Fireworks in the town** | Light crackers anywhere safe (away from people, roads in use, wires and doors), the crowd gathering, the district's own shows | Safety rules; Play Mode: light one in the square |
| 8 | **Measure** | Device runs, budgets, analytics, docs | GC guards, frame budgets |

## Status

| Step | Status |
|---|---|
| 1. The character | Done (to be felt on the tablet). `Core/Explore/Locomotion` (walk to 1.7 m/s and jog at 3.8 by how far the stick is pushed, sprint at 6.2; acceleration 11 m/s², stopping at 20 (a sprint stops in about 0.3 s); turning at 720°/s, 330 when sprinting; a 0.6 m jump with 0.12 s of coyote time and a 0.15 s jump buffer; a third of the control in the air; half speed for a moment after a hard landing), `OrbitCamera` (over the right shoulder, pitch from -35° to 65°, zoom 1.8 to 6 m, eases back behind a moving character 1.4 s after the player stops turning it, pulls in at walls at once and eases out, critically damped), `Footsteps` (a synthesised step in four variations). `AvatarMotion` gains a run (high knee, pumping arms, lean, bounce) and an in-air pose; `AvatarAnimator.SetLocomotion` picks idle, walk, run or fall from the speed and plays the walk and run at the pace of the feet, with a foot-down beat for the steps. `Gameplay/Explore/ExploreController` (the avatar on a CharacterController in the neighbourhood, its walls and houses as colliders, kept on walkable ground; stick input turned by the camera; the camera's sphere-cast against walls; footsteps; an Interact prompt for celebration spots and the front door; landscape on the phone while exploring; `ExploreEvent`). `UI/ExploreMode` (the Explore button bottom left; a floating thumbstick, drag to look and pinch to zoom anywhere else, Jump, Sprint, the Interact button with its label, Exit; WASD, Shift, Space, E and Esc on a keyboard), `VirtualStick`, `LookPad`. Tests: `LocomotionTests` (Mono: speeds, start and stop, turning, jump height, coyote time and the buffer, air control and landing, the camera's limits, recentring, wall pull-in and smoothing, footsteps), `ExploreSmokeTests` (Play Mode: jog, sprint, stop, jump and land, always on walkable ground, exit; walking up to a spot offers Celebrate) |

| 2. Player profile and settings | Done. `Core/Explore/PlayerRecord` (coins up to a cap, spent only when there are enough; reputation, only gained; play stats: distance and time explored, jumps, crackers lit, shows, midnights, homes visited, jobs; repaired when damaged), `Reputation` (eight levels from Newcomer to Utsav legend, progress and what's left to the next), `ControlRanges`. `GameSettings` version 4: look speed (0.4 to 2.5), invert up and down, camera follows behind, hold to sprint, controls size (0.8 to 1.3), left-handed controls; all reported by `settings_changed`. `Gameplay/Explore/PlayerService` (player.json, counted from the game's events, `Reward` with a level-up announcement in `PlayerRewardEvent`, saved a few seconds after a change and on pause; carried in backups as their own part) and `PlayerInstaller`. Explore follows the settings (the camera's speed, inversion and recentring; Sprint held or tapped; the stick and buttons swapped for left hands; their size) and gains Pause: the game stops behind a menu with Resume, Your profile, the six control settings and Back to the courtyard. `UI/ProfileScreen` (the avatar's name with Edit avatar, the level and title with a progress bar, and eleven stats), from Settings and the pause menu; Settings has an Explore controls section. `HoldButton`. Tests: `PlayerRecordTests` (Mono: coins, repairs, levels, the control settings' ranges, migration and reporting), `ProfileSmokeTests` (Play Mode: events counted, a reward with a level up, saved, on the profile screen, in a backup; invert, sensitivity, left-handed, hold to sprint and pause) |

| 3. The district | Done. `Core/District`: `DistrictPlan` and `DistrictGenerator.Bazaar` (an original bazaar town of 300 x 300 m east of the lane: the Bazaar road carries the lane's street on, the Chowk road crosses it at a 50 m square with a clock tower, seven lanes divide the blocks, a river promenade, ghat steps and the river along the north; about 300 buildings lining every road with their fronts to it, 1 to 4 floors, taller on the main roads, shops on the ground floor, balconies and water tanks; 32 market stalls along the bazaar road; lamps along the main roads and the promenade; the same every time from its seed), `DistrictLint` (joined to the lane, every road reachable, nothing on a road, the square, the ghat or another building, every building facing a road, every tile within 30,000 triangles), `TilePlanner` (50 m tiles: full detail within 60 or 80 m by tier, simple out to 130 or 170 m, nothing beyond; the low tier keeps to the lane), `DistrictKit` (one mesh per tile: earth, roads, paving, ghat steps and water; full buildings with floor bands, lit and dark windows with sills, open shops with lit insides, half-rolled shutters, counters and awnings, balconies, parapets and tanks; simple buildings keep the walls and the lit windows; stalls with goods; lamps; the clock tower with four lit faces), `DistrictPalette` (washed walls in seven colours, warm windows, dark roads). `Gameplay/District/DistrictView` (while exploring: every tile built once when Explore opens, full and simple meshes and colliders, then streamed by switching, which allocates nothing; freed on leaving). Explore walks from the lane into the district without a seam, is stopped by buildings, stalls, the tower and the river (a move that would leave walkable ground is undone), and adds a night haze so the far town fades out. Measured in the editor: about 29,000 triangles drawn on the bazaar road (16 tiles), 127,000 held for the whole district; sprinting down the bazaar road 0.33 KB of garbage a frame. Tests: `DistrictTests` (Mono: lint, seed, walkability, tiles by tier and the simple detail), `DistrictSmokeTests` (Play Mode: from the lane into the bazaar, tiles and colliders round the player, a building and the river stop you, put away on leaving), `PerformanceGuardTests` (exploring the bazaar) |

### Step 1 notes

- **It needs feeling on the tablet.** The numbers above are a first tuning, from what makes mobile third-person games
  feel responsive. Speeds, acceleration, the camera's lag and recentring are all constants in two files, so feedback
  turns into quick changes.
- **Animation is still procedural.** The run and fall are hand-built poses on the avatar's skeleton. Bought or
  commissioned motion capture (open-world plan, section 3.A.5) is the step up; the animator already chooses clips
  by speed, so captured clips can replace these one by one.
- **The world is still the lane.** Step 3 builds the district to roam; until then, Explore covers today's
  neighbourhood.
- **Walk mode stays for now** (tap-to-move and the map's fast travel). It retires once Explore has the map (step 6).

### Step 3 notes

- **Grey-box quality.** The town is built from code like the house: clear shapes, lit windows and colour, no
  textures or modelled detail. It shows the layout, the scale and the night look; bought or commissioned art (the
  open-world plan's biggest cost) replaces it district by district.
- **Explore only, for now.** The district appears when Explore opens and goes when it closes (building it takes a
  moment at the start of Explore). The courtyard's fireworks view doesn't show it yet.
- **Next:** people (step 4) and traffic (step 5) bring it to life; the map (step 6) shows where things are.
- **Review list:** the district's place (east of the lane) and size; the river on its north side; how the clock tower
  and the square should look; more landmarks (a fairground, a temple's outside with an advisor).
