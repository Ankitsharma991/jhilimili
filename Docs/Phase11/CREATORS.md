# Making packs for Utsav Nights

This guide is for people who make content for the game (creators) and the people who check it before it ships
(advisors). A pack adds one of three things:

- **a neighbourhood** (`environment`): a new place to celebrate, with its houses, streets and landmark;
- **clothes** (`wardrobe`): garments for the avatar;
- **shows** (`shows`): fireworks shows players can start from in the show designer.

Packs are data files, not code. Players download them from inside the game, and the game checks every pack before it
installs it.

## What you need

- A Linux or macOS computer with **Unity 6000.3.26f1** installed through Unity Hub. The tool uses the Mono runtime that
  comes with Unity. You never open the Unity editor. If Unity is installed somewhere unusual, set `UNITY_DATA` to its
  `Editor/Data` folder.
- A copy of this project (the tool reuses the game's own pack checks, so what passes here passes in the game).

## The steps

All commands run from the project folder.

### 1. Start from a template

```
tools/creator/pack.sh new shows my_festival_shows packs/my_festival_shows
tools/creator/pack.sh new wardrobe festive_kurtas packs/festive_kurtas
tools/creator/pack.sh new environment env_lake_town packs/lake_town
```

The pack id is 3 to 40 lowercase letters, digits or `_`, starting with a letter. Neighbourhood packs start with `env_`.

Each pack folder has:

| File | What it is |
|---|---|
| `pack.json` | The pack's title, description, version and tags (`geo`, `style`, `festival`). |
| `content/` | The pack's files: `shows.json`; or `wardrobe.json`; or `environment.json`, `layout.json` and `preview.png`. |
| `REVIEW.md` | The checklist the advisor works through. |

The templates already pass every check, so you can change them a little at a time.

### 2. Check as you go

```
tools/creator/pack.sh lint packs/lake_town
```

It prints every problem in plain words, or "No problems: the game will accept this pack." The checks are the
game's own:
- sizes and the budget per type (triangles, texture memory, draw calls; 40 MB at most);
- file names and types;
- a neighbourhood's layout: the home lot, no overlaps, open sky for fireworks, every spot reachable on foot;
- garments open to every body;
- shows that fit every phone, at most as many crackers at once as a low-end phone allows.

### 3. The review

The advisor reads `REVIEW.md`, checks the pack in the game (see below) and ticks every box (`- [x]`). Then they sign:

```
tools/creator/pack.sh review packs/lake_town --advisor "Full Name" --date 2026-11-03
```

The tool refuses while a box is unticked. Signing writes the advisor's name and the date into the pack's manifest
(neighbourhood packs must carry them, PRD 16) and adds a signed line to `REVIEW.md`.

**Advisors** are people who know the culture, region or festival the pack shows, and who don't work on the pack
themselves. The game's owner agrees on who reviews what before work starts.

### 4. Submit

```
tools/creator/pack.sh submit packs/lake_town
```

This checks everything again, seals the pack (every file's size and SHA-256), and makes
`packs/lake_town/submission/env_lake_town-1.0.0.zip` with the pack and its signed review. Send the zip to the game's
owner. To change a pack after it ships, raise `version` in `pack.json` (for example to `1.1.0`) and submit again;
players get Update in the game.

## Trying a pack in the game

Build into a hosting folder, then point a development build at it the way the tests do (`FolderPackTransport`), or
ask the owner to put it on the test hosting:

```
tools/creator/pack.sh build packs/lake_town --out test_hosting
tools/creator/pack.sh catalog test_hosting
```

## For the owner: publishing

1. Unzip the submission. Read `REVIEW.md`: every box ticked, and a named advisor you agreed with.
2. Copy its `packs/<id>/<version>/` folder into the hosting folder, next to the packs already there.
3. Run `tools/creator/pack.sh catalog <hosting folder>` to rebuild `catalog.json` (the newest version of each pack).
4. Upload the hosting folder as it is (Phase 9: `PacksConfig.asset` has its address). Players see the pack the next
   time the game reads the catalog.
5. Add the pack's content to `Docs/LICENCE_LOG.md`, with the creator and the licence named in `REVIEW.md`.

## What packs can't do

- Run code. Packs are data only, and every file is hash-pinned.
- Ask for anything about the player. Packs never see or send personal data.
- Use sacred imagery as decoration or effect art. Optional religious items need the advisor's review and the
  game's hide filter.
- Use real brand names of crackers, shops or products.
