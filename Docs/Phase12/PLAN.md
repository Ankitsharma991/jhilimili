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

| 4. People | Done (to be seen on the tablet). `Core/District/Crowd`: `WalkGraph` (points every 25 m along every road's footpaths, a ring round the square and the promenade, joined where roads meet; shortest routes with preallocated arrays), `CrowdSim` (140 residents, each with a home, walking errands to anywhere on the paths and home now and then, standing a while at each, on their own side of the footpath at their own pace, rounding stall corners; a shopkeeper behind each of the 32 stalls facing the road; they cheer at a burst within 32 m, cover their ears at a loud one within 9 m, step aside for the player, wave when the player stops close by, and `Gather` brings people round a show to watch for a while; the same every run from its seed; structs in arrays, allocation-free per tick). `AvatarMotion` gains two one-shot clips: covering the ears with a flinch, and a wave. `PayoffStartedEvent` now says whether the cracker is loud. `Gameplay/District/CrowdView` (while the district is active: the simulation for everyone, and a pool of avatars, 24 on the high tier and 14 on mid, lent to the nearest people within 30 m; a person keeps their avatar while near, so nobody changes look in front of you; built three a frame when Explore opens, then only moved and switched; each with a body the player bumps into; bursts reach the crowd). Measured in the editor: 172 people with 10 drawn round a stall; sprinting down the bazaar road with the crowd 0.25 KB of garbage a frame. Tests: `CrowdTests` (Mono: the graph joins every road, people stay on the footpaths and get about, cheer, cover ears, wave and gather, step aside, the same every run), `AvatarMotionTests` (the new clips and the run and fall within joint limits, one-shots start and end at rest), `CrowdSmokeTests` (Play Mode: people round the player, the stall's shopkeeper, a loud bang and the ears covered, put away on leaving), `PerformanceGuardTests` (the bazaar now with its crowd) |

| 5. Traffic and the scooter | Done (to be ridden on the tablet). `Core/District/Traffic`: `TrafficArm` (each main road split at the chowk, which stays for people, into two arms; vehicles keep left, 1.6 m from the middle, and turn round at each end; the west arm turns before the player's lane), `TrafficSim` (about one vehicle per 50 m: scooters at 25 to 31 km/h, autos at 19 to 24, cyclists at 12 to 15; each slows for its turn, follows whoever is ahead and stops short for anyone on the carriageway, a person crossing or the player, never for the footpaths; a short honk when the player stays in the way; nobody is ever touched; allocation-free), `VehicleKit` (box-built scooter, auto rickshaw and bicycle with their riders, four paints each, lit headlamps). `Core/Explore/Scooter`: `ScooterModel` (point the stick where to go: up to 40 km/h in about 3 s, steering no tighter than a 2.4 m circle or 100°/s, easing off in sharp turns; pull back to brake, from 40 km/h in under 7 m, and hold back to roll backwards slowly; it coasts when let go and leans into turns as a two-wheeler must; a knock against something solid takes most of the speed, nothing breaks), `VehicleSounds` (an engine putter that loops without a seam, a two-tone horn). The crowd steps aside for vehicles, gives the player more room on the scooter, and the horn moves people ahead to the side. `AvatarMotion` gains a seated riding pose. `AudioService.SetPitch`. `Gameplay/District/TrafficView` (while the district is active: every vehicle within 100 m drawn, each with a body to bump into; shares the crowd both ways; honks heard within 40 m). Explore: the player's scooter waits where the lane meets the bazaar; Ride the scooter, Get off when slow (it stays parked where you left it for the session), Jump becomes Horn and Sprint goes while riding, a km/h readout, the camera sits farther back, the engine's pitch follows the speed, the scooter's nose stays out of walls, stalls and the river. The lane behind keeps its detail while the player is in the bazaar (it had rebuilt lots as the player rode off: an 810 KB hitch). Measured in the editor: riding the bazaar road at 40 km/h through the traffic and the crowd, under 0.2 KB of garbage a frame. Tests: `TrafficTests` (Mono: arms keep left and turn smoothly, clear of buildings and stalls; vehicles get about and keep their distance; stop short of the player, honk, then go on; stop for someone crossing, not for the footpath; the same every run; the horn; the scooter's speed, braking, coasting, rolling back, turning, leaning, bumps and sounds; the vehicle meshes), `ScooterSmokeTests` (Play Mode: get on, ride, brake, honk, park, walk off, parked there next time; traffic flows, waits behind the player, honks and never touches), `PerformanceGuardTests` (riding the bazaar) |

| 6. Map, phone and jobs | Done (to be tried on the tablet). `Core/Explore/Jobs`: `JobDef` and `JobStep` (reach a place, or Interact there; steps in a group in any order, groups in turn; a step can move the player, up to a roof, or set off a finale), `JobRun` (the job's state: steps done, the group open, the clock, the objective line, which changes only with progress; the nearest step left for the route), `JobPay` (in full the first time, a quarter after), `JobCatalog` (three jobs placed from the district and the lane: Light up the lane, eight diyas by the gates and across the street, for Kamla Aunty; Diyas for the bazaar, a box from Meena's clay stall to a sweet shop, a cloth shop and a tea shop by the river in five minutes; A rooftop show for Joshi-ji, from level 2: the crate from the fireworks stall, up to his roof by the chowk, three racks on the edge over the square, then light it). `JobKit` (a diya on a brick, lit or not; the waypoint beacon). `PlayerRecord` version 2 keeps the jobs finished. `DistrictPlan.IsWalkableAt` and `RoofAt` (flat roofs walkable inside the parapet; the buildings' colliders now stop at the roof). `Core/District/WorldMap`: `MapFrame` (the lane and the bazaar, north up, 512 pixels wide), `MapRaster` (the town drawn from the plan: roads, lanes, paving, the river, buildings, home in saffron), `MapRoute` (along the lane's street to the bazaar, then the footpath graph; written into the caller's buffers, allocation-free). `Gameplay/Explore/JobService` (offers jobs as phone messages, new ones when the player levels up; runs the one taken from the player's position and Interact; beacons at every step left; the lane's diyas, lit for good once the lane's been lit; pays through `PlayerService`, with a thank-you message; gives up on leaving Explore; the rooftop show: twenty-odd seconds of rockets from the racks bursting over the chowk with whooshes and bangs, the square filling with people who cheer at each burst). Explore: Interact does the job's step where it is, and "Go down to the street" from any roof. `UI/Minimap` (top left: the map round the player turned to the camera, the route as dots, home, the scooter, the target, and the target on the rim when it's off the map). `UI/PhoneScreen` (Phone button with a count of new messages, or P: Map with home, the chowk, the ghat, the scooter, the job's places and you, tap to set a waypoint; Messages; Jobs with what each pays and Take the job or Give up; Camera, a photo of the view without the controls, saved and shared through the share sheet; Home, back to your gate; Settings). The HUD shows the job's objective with the time left, and notes for pay, levels and photos. Tests: `JobTests` (Mono: the catalogue's places can be stood on, the lane in any order, the diyas in order against the clock, the roof and the finale, reach steps and giving up, pay and the record, roofs walkable only inside the parapet, the map's frame and picture, routes from home to the roof and back), `JobSmokeTests` (Play Mode: take the lane job on the phone, follow the minimap, light all eight, paid and lit; the rooftop job up to the show and back down; the map's waypoint, the stick held while the phone is up, a photo, home) |

| 7. Fireworks in the town | Done (to be tried on the tablet). `Core/District/TownFireworks`: `TownSafety` (where a cracker may go: open ground or a flat roof; never on a main road's carriageway, which the traffic uses; 3 m from doors, the lane's gates and stalls with their awnings, 2.5 m from the lamp posts' wires, 1.5 m from walls; people kept back 3, 5 or 8 m by how loud it is, 6 m at least for anything that flies; rockets, sky shots and cakes need open sky, away from walls, the clock tower and the narrow lanes; three set down at a time; each refusal explained in plain words), `TownShows` (the district's own shows, at the ghat over the river and on the chowk in turn: the first two and a half minutes into exploring, then every six, announced 45 seconds before; a 26-second program building to a finale of big ones from every pad, the same every time). `CrowdSim.GatherAround` (people come to watch and stand in a ring, keeping back from the fireworks). `Gameplay/District/TownFireworks` (the player's crackers in the town: the tray's unlocked ones, sparklers aside; set down in front of the player if the spot is safe, Light the fuse when there, the crowd gathers round; the district's shows run on their pads with the game's own crackers, with a phone message and a note on screen before; a show stops and crackers set down are picked up when the player leaves the bazaar). The rooftop job's finale now lights real rockets and sky shots on its racks (the sky bursts stay as a stand-in when the scene is full). Explore: a Crackers button and a strip of crackers to set down, Pick up; the phone's map shows where and when the next show is. `ShowPlayer.Catalog`. Measured in the editor: a show on the chowk with the crowd watching, 1.02 KB of garbage a frame. Tests: `TownFireworksTests` (Mono: the square, the ghat and the show pads are fine for anything; not on the roads, by doors, stalls or wires, or the lane's gates; people kept back by loudness; aerials need open sky; roofs are fine; every refusal explained; the crowd's ring keeps back; the timetable, turns and the program), `TownFireworksSmokeTests` (Play Mode: refused on the bazaar road, then lit in the square and it goes off; the district's show at the ghat with people coming, stopped on leaving; the cracker strip), `JobSmokeTests` (the rooftop show lights real rockets), `PerformanceGuardTests` (a show on the chowk) |

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

### Step 4 notes

- **Look and feel need the tablet.** Walking speed (1.05 to 1.5 m/s), how often people wave (every 25 to 45 s at
  most) and how many are drawn are constants in `Crowd.cs` and `CrowdView.cs`.
- **Looks are the twelve presets,** each avatar in the pool with its own skin tone and hair. Commissioned resident
  outfits and faces would widen the variety; the pool takes any profile.
- **Far people aren't drawn.** Past 30 m the town's people are simulated but not shown (the night haze hides the
  gap). A cheap far crowd (simple figures, no skeleton) is a later step if the streets look empty at a distance.
- **Gathering is ready, not yet used:** step 7 (fireworks in the town) calls it to bring people to shows.
- **Review list:** how busy the streets should be; whether people should talk (speech bubbles or sounds); the
  shopkeepers' calls; families walking together.

### Step 5 notes

- **It needs riding on the tablet.** The scooter's numbers (top speed, acceleration, how tight it turns, how much it
  leans, the camera's distance) are constants in `Scooter.cs` and `ExploreController.cs`.
- **Point-to-go steering.** The stick says where to go, relative to the camera, and the scooter gets there as a scooter
  can; pulling back brakes. It's the usual thumb-friendly scheme on phones. Separate throttle, brake and steer buttons
  are an option if it feels loose.
- **Family-safe by design.** Traffic never touches anyone, there's no damage or falling off, and riding into people or
  walls only stops you. No helmet is drawn yet; the review list has it.
- **Traffic stays on the main roads.** The lanes are for people, and the chowk is closed to traffic for the festival,
  so vehicles turn round at its edge. Junctions with turns and more vehicle kinds (cars, a bus, a tempo) are later work.
- **The lane behind is held at the detail it has at its east end** while the player is in the bazaar, so it doesn't
  rebuild as you ride off. It is small and in the haze, so the extra drawing is minor; step 8 measures it on the phone.
- **Review list:** a helmet on the scooter; whether the player's scooter should be bought, chosen or customised (step 6
  jobs could pay for one); the horn's sound; whether traffic should also use the lanes.

### Step 6 notes

- **The rooftop show is a stand-in.** It uses the neighbourhood's sky bursts and the synthesised sounds. Step 7 brings
  real crackers into the town, and the job's finale will light them on the racks instead.
- **Jobs don't carry over.** Leaving Explore gives up the job in hand; what's finished is kept (and paid for once in
  full). Saving a job half done is a small addition if the tablet test shows people leave mid-job.
- **Phone photos** are the camera's view without the controls, at up to 1920 pixels wide, saved in the app's cache and
  shared through the share sheet. Saving them to the gallery needs a storage permission or the media store, left out for
  now.
- **Messages are this session's** (offers when Explore opens, thanks after a job). A message history kept between
  sessions, and new jobs over time, come with the content plan.
- **Review list:** the givers' names and words; what the jobs pay; whether the minimap should turn with the camera or
  stay north up (a setting); more jobs (a delivery by scooter across town, a puja shopping list, help at the ghat).


### Step 7 notes

- **The safety rules are a first set** from what the PRD asks (away from people, roads in use, wires and doors) and
  common sense for a crowded bazaar. The distances are constants in `TownFireworks.cs`; the cultural and safety review
  should look at them.
- **No handheld sparklers in the town yet.** In the courtyard the sparkler is held; holding one while walking about
  needs its own animation and rules (step 8's review list).
- **District shows use three aerial crackers** (green sky shots, rockets and gold crowns) on fixed pads. Shows chosen
  by the festival (Diwali, Tihar, New Year) and bigger shows on the big nights would build on this.
- **The town shares the courtyard's cracker limit** (6, 10 or 14 on the scene by tier), so a busy show on a low phone
  fires fewer crackers. Step 8 measures it on the phone.
- **Review list:** the distances; quiet hours in the town; whether the player can join a district show (light from its
  pads); a show the player designs, played on the chowk.
