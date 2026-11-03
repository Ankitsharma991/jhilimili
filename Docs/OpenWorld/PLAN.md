# From fireworks simulator to open-world festival city: plan

Owner's request (2026-11-03): "give this app a real 3D simulator feel, not just a single person, but a GTA Vice City
kind of feel". This plan says what that means for Utsav Nights, where the game stands today, everything needed to get
there, in what order, and what it will cost. Nothing here is built yet.

## 1. What "that kind of feel" means here

The parts of an open-world city game that make it feel alive, applied to a festival night:

| Pillar | In Utsav Nights |
|---|---|
| **A big world you roam freely** | A whole festival town, not a lane: districts, a bazaar, a river or lake front, a hill quarter, rooftops, alleys, open grounds. No loading between them. |
| **You are a character in it** | Third-person control: walk, run, climb stairs, sit, interact; the camera follows and you look round freely. |
| **A city that lives without you** | Crowds with their own routines, shopkeepers, families lighting crackers, kids with sparklers, traffic, animals, people reacting to what you do. |
| **Getting around** | Bicycle, scooter, auto-rickshaw: hop on, ride, park. Riding feels fast and fun. |
| **Things to do everywhere** | A light story through the festival days, side jobs and challenges from neighbours, shows and contests at landmarks, races, collectibles. |
| **The radio and the sounds** | Music on the scooter, bazaar chatter, horns, bells, distant fireworks from every direction. |
| **Night that changes** | Dusk to midnight, then the next festival day: crowds, lights and fireworks change through the night and across the days. |
| **Your phone as the menu** | A phone with the map and GPS, messages that bring jobs, the camera, your shows, your home. |

**What we do not take from those games:** crime, weapons, police chases, hurting people, or anything from their
maps, names, story, characters or art. This stays an original game. It also stays family-safe: the PRD keeps it
PEGI 3, fireworks apps that look like prank or weapon games lose store standing (PRD non-goal), and nobody is ever
harmed in the game. Vehicles nudge people aside; they never hit them. Fireworks can never be aimed at people.

## 2. Where the game is today

Built and working (Phases 0 to 11):
- **Fireworks:** 11 crackers with their own effects, sound and haptics; placement rules; a finale; the show designer;
  clips; New Year with a countdown.
- **Home:** the avatar creator (with the photo suggestion), the house with modules, decorations, layouts, two
  interior rooms.
- **Neighbourhood:** three environments of 6 to 12 house lots each, streamed in 16.8 m chunks; walking at 1.4 m/s by
  tap-to-move or a joystick; celebration spots; ambient bursts; up to 12 standing crowd members on the high tier.
- **Online and content:** packs, backups, home visits and shared shows by code; analytics; ads and the purchase.
- **Size:** 273 scripts, about 53,000 lines, 828 tests. Everything visual is generated from code (simple shapes, no
  modelled art).

What keeps it from feeling like a living open world:

| Today | Why it doesn't feel open-world |
|---|---|
| The main view is a fixed fireworks camera over the courtyard; walking is a side mode | You watch a scene; you don't live in a place. |
| The place is a lane a few minutes across (the PRD says: "a settlement is a few minutes' walk across, not an open world") | Nowhere to go. |
| Tap-to-move at walking pace, no running, jumping or climbing | It feels like a board game. |
| Crowd members stand in a ring; neighbours appear at their doors | Nobody goes anywhere or reacts. |
| No vehicles, roads or traffic | No sense of a town. |
| No jobs, story or progression beyond unlocking crackers | Nothing pulls you round the world. |
| Code-generated boxes and lathes for all art | Fine for a lane at night; thin for a city you walk through up close. |
| Portrait orientation | Open-world games play in landscape. |
| Budgets sized for a courtyard (12 people, 150,000 to 400,000 triangles for the whole lane) | A town needs far more, streamed. |

## 3. What needs doing

Grouped by area. Effort is rough person-weeks for one developer working with AI help, not counting art made by
artists [Hypothesis]. Priority: **P0** = needed for the feel at all, **P1** = makes it convincing, **P2** = depth
and polish.

### A. The player and the camera (P0)

1. **Landscape orientation**, with every screen laid out again for it (HUD, creator, designer, settings). 2 weeks.
2. **Third-person character controller:** walk, run, sprint, stop, turn, step up kerbs and stairs, slopes, jump
   (small), ladders to rooftops, sit and lean; collisions with the world; kept on the ground. 3 weeks.
3. **Controls:** a left-thumb virtual stick, right-side drag to look, Run, Jump and Interact buttons, a context
   prompt for nearby things ("Light", "Talk", "Ride", "Enter"). Controller support (Bluetooth gamepads). 2 weeks.
4. **Follow camera** with Cinemachine (already installed): collision with walls, shoulder offset, zoom, a fireworks
   look-up when something bursts overhead, and the current fireworks view as a mode at any spot. 2 weeks.
5. **Animation:** a proper locomotion set (idle, walk, jog, run, start, stop, turn, stairs), interact, light a
   cracker, celebrate, sit, ride. Bought motion-capture packs retargeted to the avatar, or commissioned.
   2 weeks plus the animation packs.
6. **Photo mode:** free camera, filters, share. 1 week.

### B. The world (P0)

1. **City design:** an original festival town of about 1 to 1.5 km across, in districts, each with its own look,
   landmark and festival life:
   - the old bazaar (narrow lanes, stalls, string lights overhead);
   - the river front with ghats;
   - the hill quarter with steps and terraces;
   - the new town with wide roads and a fairground;
   - the player's own neighbourhood (today's lane and house).

   A map design document, then a grey-box you can walk on the tablet. 3 weeks.
2. **World tiles and streaming:** the town split into tiles (for example 100 x 100 m), each an Addressables scene
   with three detail levels; tiles load and unload around the player and ahead of a vehicle; impostors for far tiles;
   occlusion culling in dense lanes; memory budgets per tier. Today's chunk planner is the starting point. 4 weeks.
3. **Roads and paths:** a road network (lanes, junctions, footpaths, crossings) that traffic and crowds both use, and
   a navigation mesh for people. 3 weeks.
4. **Moving from code-built art to modelled art:**
   - a modular building kit per district (walls, doors, windows, balconies, roofs, shop fronts, signage without real
     brands);
   - props (stalls, carts, lamps, wires, temples' outsides with an advisor, trees, water);
   - ground and road materials.

   Today's generator keeps the player's own house and decorations. This is the biggest cost of the plan, in art
   rather than code: see section 5. 3 weeks of code for the kit pipeline, plus the art.
5. **Interiors across the town:** shops (the sweet shop, the cracker stall, a tailor), a friend's house, a community
   hall, using the Phase 11 room system. 2 weeks plus art.
6. **The new PRD line:** "a settlement is a few minutes' walk across, not an open world" changes. The PRD and the
   cultural review grow with the world.

### C. A living city (P0 for crowds, P1 for the rest)

1. **Crowds that go places:** hundreds of people over the town, with tens close by. Each has a home, a routine
   through the night (shop, visit, watch fireworks, go home), a look from the avatar system, and level of detail
   (full animation near, simple far, impostors farther). This needs the job system and Burst, or DOTS, plus GPU
   instancing for skinned people. 6 weeks.
2. **Reactions:** cheer at big bursts, cover ears at bangs, step back from a lit cracker, gather round a show, wave,
   chat, look at the player's decorations, kids following sparklers. 3 weeks.
3. **People with jobs:** shopkeepers, a cracker-stall seller, a sweets vendor, musicians, a priest at a temple's
   outside (advisor), families on rooftops lighting their own crackers (today's distant bursts become real ones
   nearby). 3 weeks.
4. **Traffic:** scooters, rickshaws, bicycles, a few cars and a bus, on lanes with junction rules, slowing for people,
   honking, parking; dense in the new town, none in the bazaar's lanes. 5 weeks.
5. **Animals:** street dogs (that dislike bangs: Quiet mode matters), cows in lanes, birds. 2 weeks.
6. **Time and days:** dusk to after midnight each night, and the festival's days in order (Dhanteras to Bhai Dooj; Tihar's
   five days; New Year's Eve), with the town's decorations, crowds and events changing per day. Weather (a light
   haze, smoke that builds after big fireworks). 3 weeks.

### D. Vehicles (P1)

1. **Ride and drive:** bicycle, scooter and auto-rickshaw first; arcade handling that feels good on a phone
   (raycast wheels, drift on corners, a boost), simple damage-free bumps, get on and off animations, the camera
   for riding. 5 weeks.
2. **Mobile driving controls:** steer with tilt or buttons, accelerate and brake buttons, a horn. 1 week.
3. **Safety by design:** people step out of the way; a scooter bumping someone stops and they shout. Nobody falls or
   is hurt. Fireworks can't be carried lit while riding. 1 week.

### E. Things to do (P0 for a first set, then ongoing)

1. **A festival story:** a short, warm campaign over the festival days:
   - help the neighbourhood get ready (lights up the lane, sweets delivered, the old fireworks maker's last show);
   - meet the town's characters;
   - end with the big night.

   Written with the cultural advisor. 6 weeks for the system, plus writing.
2. **Jobs from the phone:** neighbours message for help (deliver diyas, fetch crackers, decorate a stall, find a lost
   kid's kite, set up a rooftop show), paid in coins and reputation. 3 weeks for the system, then content.
3. **Challenges and events:**
   - scooter time trials through the lanes;
   - rangoli contests;
   - fireworks show contests judged by the crowd's cheers;
   - a lantern release;
   - photo hunts;
   - collectibles (a hidden diya in every district).

   3 weeks, then content.
4. **Progress and money:**
   - coins earned in the world buy crackers, decorations and clothes;
   - a neighbourhood reputation unlocks districts' events;
   - the town gets more festive as you help.

   This changes today's "unlock by ad or purchase". The economy needs a careful design, with no loot boxes (PRD 15).
   3 weeks.
5. **Fireworks in the open world:** light anywhere that's safe (not near people, not under wires, not indoors or on
   roads in traffic: today's rules grow); big public shows at landmarks on a schedule; the show designer plays at any
   rooftop or ground you own or rent. 3 weeks.

### F. The phone and the HUD (P0)

1. **A minimap** and a full map with districts, jobs, shops, shows, your home and a GPS line on the roads.
   3 weeks.
2. **The in-game phone:** map, messages (jobs), contacts, camera and clips, shows, home, settings. It replaces most
   of today's buttons. 3 weeks.
3. **A landscape HUD:** coins, time of night, the current job, context prompts, the fireworks tray when holding
   crackers. 2 weeks.

### G. Sound and music (P1)

1. **City ambience in 3D:** bazaar chatter, vendors calling, horns, bells, dogs, music from houses, fireworks from all
   around with distance and echo in lanes; a mixer that keeps hundreds of sources to a phone's voice budget.
   3 weeks.
2. **Radio on vehicles and at home:** a few stations of festival, folk and film-style instrumental music. It must be
   original or properly licensed: commissioned or royalty-free, never film songs. 1 week for code, plus music.
3. **Voices:** short barks for characters ("Happy Diwali!", "Watch out!"), in Hindi, Nepali and English, recorded.
   1 week plus recording.

### H. Looks (P1)

1. **Night lighting at city scale:** URP Forward+ for many lights, baked lighting per tile for the static town,
   emissive windows and string lights everywhere, fireworks lighting the streets, bloom and colour grading per tier.
   3 weeks.
2. **Effects:** smoke that drifts and gathers, sparks bouncing off streets, wet-looking reflections near the river,
   haze. 2 weeks.
3. **Art direction:** one clear stylised look the art can reach on budget phones (bright, warm, readable at night).
   It stays stylised, not photoreal (PRD non-goal). It should be decided before any art is bought.

### I. Technology and performance (P0, all through)

1. **Device targets:** the PRD's budget phone (3 to 4 GB RAM) is unlikely to carry a whole town with traffic and
   crowds at 30 fps [Hypothesis, to measure]. Make 4 GB RAM and a recent mid-range chip the minimum for the town,
   keep the low tier as a "your neighbourhood only" mode, and measure on real phones from the first grey-box. 1 week
   to set, then every phase.
2. **Simulation at scale:** the job system, Burst and probably DOTS (Entities) for crowds and traffic; pooling;
   no per-frame garbage (the GC guards carry on). 4 weeks within C and D.
3. **Download size:** the town won't fit in today's 44 MB. Use Play Asset Delivery: the start area installs with the
   app, districts download on first visit (Phase 9's pack system and Addressables already point this way). 2 weeks.
4. **Saving the world:** player position, jobs, story progress, the town's state per day, vehicles parked;
   crash-safe like the house journal; backups carry it. 2 weeks.
5. **Load and heat:** fast start (first fireworks within 10 seconds is a PRD goal: keep a quick "light one in the
   courtyard" start while the town loads), a thermal-aware quality governor (exists; extend it). 2 weeks.
6. **Testing:** automated play-throughs of routes, soak tests for streaming, frame-time capture on devices.
   Ongoing.

### J. What stays and what changes in today's code

| Keep as it is | Rework | Retire or demote |
|---|---|---|
| Fireworks engine, crackers, effects, audio, haptics | Walk controller → character controller | Tap-to-move as the main control |
| Avatar, wardrobe, photo suggestion | Settlement generator and chunk planner → world tiles and streaming | The fixed fireworks camera as the main view (stays as a mode) |
| House, decorations, interiors, layouts | Ambient life → crowd and traffic systems | Portrait layout |
| Show designer, clips, New Year | HUD buttons → phone and landscape HUD | Code-generated town buildings (the player's house can stay generated) |
| Packs, backups, visits, analytics, ads, purchase, privacy | Placement rules → town-wide safety rules | |
| Quality tiers and GC guards | Progress → coins, reputation and story | |

## 4. Suggested order

Build one district properly first, put it on the tablet, and only then grow the town. That shows early whether the
feel, the performance and the art cost all work.

| Phase | What | Rough length | Proves |
|---|---|---|---|
| **12. Vertical slice** | Landscape; the character controller and camera; one district (about 300 x 300 m: the bazaar and the player's lane) in grey-box, then with a first art kit; 30 people with routines and reactions; the scooter; a minimap; three jobs; fireworks anywhere safe; streaming for that district | 3 to 4 months | It feels like a living place on a mid-range phone at 30 fps |
| **13. The town** | All districts, roads and traffic, more crowd, animals, interiors, Play Asset Delivery | 4 to 5 months | It scales, downloads and stays smooth |
| **14. Life and story** | The festival story, the phone, jobs, challenges, economy, radio and voices, day by day | 3 to 4 months | There's a reason to come back each night |
| **15. Polish and launch** | Looks, sound, performance on many devices, cultural review of the whole town, store pages, the closed test | 2 to 3 months | It's ready for players |

That is about a year to a year and a half of development with AI help, plus the art and audio bought or commissioned
alongside [Hypothesis]. Each phase ends with something you can play on the tablet.

## 5. What it will cost besides code [Hypothesis, to check against real quotes]

| Item | Why | Rough range |
|---|---|---|
| 3D environment art (modular kits for 4 to 5 districts, props, vehicles) | The biggest step up in the feel; South Asian street kits are rare off the shelf | $8,000 to $40,000 commissioned, less with asset-store kits adapted |
| Character and animation (locomotion and interaction mocap packs, NPC variety) | Movement is most of the feel | $500 to $3,000 for packs |
| Music (radio stations, themes) and voice barks | Original, licensed audio | $2,000 to $10,000 |
| Devices for testing (two or three mid-range and one low-end Android phones) | Performance can only be judged on phones | $500 to $1,200 |
| Cultural advisors for the whole town and the story | The PRD's review, now much larger | Per agreement |
| Unity plan | Revenue thresholds may require a paid plan | Check current Unity terms |

## 6. Decisions only you can make

1. **Scope:** go straight for the whole plan, or the vertical slice first and decide after playing it? (Recommended:
   the slice first.)
2. **Who the game is for:** still everyone 13 and up, family-safe (recommended), or an older audience?
3. **What sits at the centre:** fireworks stay the heart and the town is the stage (recommended), or the town comes
   first and fireworks are one activity among many?
4. **Story or sandbox:** a light festival story with jobs (recommended), or a pure sandbox?
5. **Art:** the budget for bought or commissioned art, and one art direction (stylised and warm is recommended for
   phones).
6. **Phones:** raise the minimum to 4 GB RAM, keeping today's neighbourhood as the low-end mode (recommended)?
7. **Money:** keep free with ads and the one-time purchase, move to premium, or add cosmetic items for coins?
8. **The PRD:** update it (scope, non-goals, budgets, phases) before Phase 12 starts, so the plan and the documents
   agree.

## 7. Risks

| Risk | Why it matters | What helps |
|---|---|---|
| Art cost and time | A city needs far more art than a lane | Slice first; modular kits; reuse across districts; packs |
| Performance on Android | Crowds, traffic, lights and fireworks together are heavy | Raise the minimum spec; DOTS; measure from the grey-box on |
| Scope creep | Open worlds grow without end | Fixed phase goals; each phase playable; cut lists |
| Cultural errors at scale | A whole town, a story and many characters | Advisors from the start of the map design, not the end |
| Store rating and standing | Anything resembling violence or prank weapons | Keep it family-safe by design (section 1) |
| Download size | Players drop large downloads | Play Asset Delivery; districts on demand |
| Lost focus | The fireworks magic could get buried | Every district and job leads back to lighting the sky |
