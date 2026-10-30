# Phase 9 plan: content packs, show designer and clips (R3)

PRD section 19, Phase 9: features F21 (content packs and offline manager), F22 (show designer) and F14 (clip recording
and sharing). The pack format is in PRD 13.4; the screens are in PRD 12 (Finale designer, Storage manager). Started
2026-10-29, ahead of R0 data, by the owner's decision; feedback and changes come after all PRD features are built.

## Definition of done (from the PRD)

- **F21:** download, verify, update and delete packs.
- **F22:** create, save and replay a show, and export a clip of it.
- **F14:** a 10 to 20 second clip shares to WhatsApp and Instagram.
- **Exit gate:** a pack update with a simulated network failure leaves the app consistent.

## Decision: data packs on our own manager, Addressables kept for authored assets

The PRD plans packs as Addressables remote bundles. Everything in this game so far is generated from code and data:
houses, settlements, garments and decorations. A pack is therefore data: an environment's layout and styles, a wardrobe
list, show templates, and a few PNG previews. That is a few kilobytes to a few megabytes, with no asset bundles.

So packs use:
- the PRD 13.4 manifest, with one change: `files` lists every file with its size and SHA-256, so each file is
  hash-pinned (PRD 16);
- a catalog JSON on static HTTPS storage;
- staged installs: a new version downloads beside the old, is verified file by file, and only then replaces it. Any
  failure keeps the old version (the PRD's automatic rollback).

Addressables (already installed) stays for the day packs carry authored models or audio. The manifest's `type` and
`files` already allow it.

**Hosting is the owner's step:** a static HTTPS location for the catalog and packs. Until then the app shows only its
built-in environments, and the tests use an in-memory server that can fail on purpose.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Pack format, lint and budgets | Manifest, versions, dependencies, per-file SHA-256; pack types (environment, wardrobe, shows); the linter (ids, versions, sizes, hashes, budgets per type, cultural review for environment packs, the content's own lint); a pack builder for the editor and CI; sample packs | Every rule; a sample pack passes; tampered or oversize packs fail |
| 2 | Pack manager | Catalog, dependency order, staged download and verify, atomic swap, rollback, delete, update; transport and storage behind interfaces | Install, update, delete; **simulated network failure leaves the app consistent** (the exit gate); a corrupt file is refused |
| 3 | Packs in the game | HTTPS transport, Wi-Fi-only by default, packs on the phone at start; environment packs in the browser (download progress, badge); wardrobe and show packs; the storage manager (sizes, delete, update, Wi-Fi-only toggle) | Play Mode: install an environment pack and walk in it; delete it |
| 4 | Show model and player | A show: launch pads and a timeline of crackers with start times; validation (pads, spacing, budget per tier, aerials on pads); templates; save and load; the player spawns and lights the crackers on time and replays | Timing, validation, save and load, replay |
| 5 | Show designer screen | Timeline strip, drag items onto launch pads, delay controls, preview, save (PRD 12) | Play Mode: create, save, replay |
| 6 | Clips | Record 10 to 20 seconds of the screen and the game's sound to MP4 on the phone (MediaCodec and MediaMuxer); share to WhatsApp and Instagram through the share sheet; export a show's clip | Encoder on the device; share intent; editor fallback |
| 7 | Measure | Analytics (pack installed or failed, show saved or played, `clip_shared` with its target); performance; the review list | Schema; GC guard |

## Status

| Step | Status |
|---|---|
| 1. Pack format, lint and budgets | Done. `Core/Packs`: `PackManifest` (PRD 13.4 fields; files hash-pinned with size and SHA-256; the pack digest), `PackCatalog`, `SemVer`, `PackHash`, `PackBuilder` (seals a manifest from its files), `PackLint`: ids, versions, types, requirements, safe file paths, sizes, hashes, the digest, a 40 MB cap; budget caps per type (environment 400,000 triangles, 48 MB of textures, 140 draw calls; wardrobe 60,000, 16 MB, 40); textures measured from PNG headers; for environment packs the cultural reviewer and date, a valid `EnvironmentDef`, a new id, a layout that passes the settlement lint, and the settlement's real worst-case triangles inside the budget; wardrobe packs through the Phase 6 validator; shows through step 4's check. `EnvironmentDef` and a runtime registry of installed environments; the layout names its landmark (`feature`). `SamplePacks` (a blue-washed "Blue city lane"). `Gameplay/Packs/JsonPackReader`; `Editor/PackTool` (Utsav/Phase 9/Build Sample Packs; CI entry) writes `Docs/Phase9/sample_packs` in the hosting layout. Tests: `PackLintTests` (Mono), `PackJsonTests` |
| 2. Pack manager | Done. `Core/Packs/PackManager`: plans requirements first, downloads each file into a staging folder checking its size and SHA-256 as it arrives, lints the staged pack whole, then swaps (old version aside, new one in, index written, old deleted); any failure deletes the staging folder and leaves the installed version untouched; `Recover` finishes or undoes a swap the app was killed in; delete refuses while another pack needs it; updates come from the catalog's newer versions. Storage, transport and JSON behind interfaces. Tests: `PackManagerTests` (Mono: install, update, delete, requirements, a damaged file, a network failure during an update leaving the old version byte for byte, and a kill after each swap step recovering to one whole version: the exit gate) |
| 3. Packs in the game | Done. `Gameplay/Packs`: `FilePackStorage` (atomic writes and directory renames), `JsonPackJson`, `HttpPackTransport` (HTTPS only, on a worker thread), `FolderPackTransport` (editor and tests), `PacksConfig` (Resources; the catalog address, empty until the owner hosts packs), `PackService` (recovers at start, registers installed environment packs, catalog with an offline copy, installs on a worker thread with progress, Wi-Fi only by default, deletes and leaves a deleted environment), `PacksInstaller`. The environment browser lists installed packs ("On this phone", their preview) and the catalog's packs to download (size, Download, progress bar). `UI/StorageManager` (from Settings, Downloads and storage: each pack's version and size, Update when newer, Delete, Wi-Fi only switch, Check for updates, total). The offline guard allows the pack download file by name. Tests: `PacksSmokeTests` (Play Mode: download the sample pack, choose its neighbourhood, delete it; a failed download changes nothing) |
| 4. Show model and player | Done. `Core/Shows`: `ShowDef` (launch pads as offsets from the spot's centre, to the camera's right and away from it; cues of cracker, pad and time), `ShowLibrary` (up to 12 saved shows, copies, replace by id), `ShowRules.Check`: names, 1 to 8 pads inside the spot's area and 0.6 m apart, known crackers, existing pads, times 0 to 120 s, a pad free only once its last cracker is cleared, aerials refused where the spot allows none, at most 40 crackers and never more out at once than the phone's item limit (6, 10 or 14 by tier); `Order`, `Shift` (delays in tenths of a second); `ShowTemplates` (Gentle start, Diwali night, Tihar lights; free crackers only; pads in rows of three that fit the rooftop). `Gameplay/Shows`: `ShowStore` (shows.json, atomic writes, a corrupt file gives an empty library), `ShowPlayer` (at the current celebration spot: clears free play, sets each cracker on its pad a second before its cue, lights it on the cue, moves aerials onto the spot's launch pads where the spot requires them, ends once every cracker is cleared; replay, stop; templates include installed shows packs; checks shows packs for the pack linter). Tests: `ShowRulesTests` (Mono), `ShowPlayerSmokeTests` (Play Mode: lights on time and pad, saves, loads, replays; an invalid show doesn't play) |
| 5. Show designer screen | Done. `UI/ShowDesigner` (PRD 12 "Finale designer"), opened from the Shows button left of Finale. The lower part of the screen holds the editor and the celebration spot stays in view above it: a header (Close, Shows, the show's name, Save, Play); the pad map (the spot's area to scale, the pads numbered with their cracker count, + Pad and − Pad); the palette of unlocked crackers (tap to add to the selected pad, or drag a chip onto a pad or its timeline lane; `ShowChipDrag`); the timeline strip (a lane per pad, each cracker a block as long as it burns, aerials in magenta, sideways scrolling); delay controls for the selected cracker (−1 s, −0.1, +0.1, +1 s, Remove); a status line with the first reason the show can't play here, or "Ready". A new cracker lands at its pad's next free moment on a 1.5 s rhythm (`Core/Shows/ShowEditing`), so what the designer adds always passes the rules. Play hides the panel and shows a slim bar with the show clock and Stop. The Shows list: New show, the templates and shows packs (each opens as a new show of the player's own), and saved shows (open, Delete). Tests: `ShowRulesTests` (editing), `ShowDesignerSmokeTests` (Play Mode: create on pads, adjust a delay, name, save, play, reopen from the list and replay; an empty or unnamed show explains itself) |
| 6. Clips | Built; on-device check pending (see notes). `Core/Clips/ClipRules` (10 to 20 seconds, the clip's size: the screen's shape with the long side at most 1280 in multiples of 16, 30 fps, about 8 Mbit/s at 720p; file names by time, the newest five kept; float to 16-bit PCM; WhatsApp and Instagram package names) and `I420` (BT.601 conversion, the CPU reference for the shader, thumbnails). `Shaders/ClipYuv.shader` packs each frame into one I420 image on the GPU, read back asynchronously. `Gameplay/Clips`: `ClipRecorder` (copies the screen, UI included, once URP has drawn the frame; the game's sound from the AudioListener through `ClipAudioTap`; stops itself at the limit or when the phone turns; a thumbnail of the first frame), `AndroidClipEncoder` (JNI direct buffers, no managed copy per frame) over the `UtsavClips.androidlib` Android library (`ClipEncoder.java`: MediaCodec H.264 and AAC, MediaMuxer, a worker thread, frames dropped rather than queued when the encoder falls behind; `ClipShare.java`: a FileProvider over the cache's clips folder, straight to WhatsApp or Instagram when installed, else the share sheet), `FfmpegClipEncoder` (the editor's stand-in), `ClipSharer` (`ClipSharedEvent` for step 7). `UI/ClipButton`: Record above Finale (Stop from 10 seconds, stops by itself at 20), a share sheet with the first frame (WhatsApp, Instagram, More, Delete, Close). The show designer's Clip button plays the show and records it (the show's length plus a second, 10 to 20 seconds). No new permissions. Tests: `ClipRulesTests` (Mono), `ClipSmokeTests` (Play Mode: GPU packing the right way up and in the right colours from a test card; a real MP4 with H.264 and AAC checked by ffprobe; sharing; the Record and Clip buttons) |
| 7. Measure | Done. Analytics: `pack_installed` (pack, version, type), `pack_failed` (pack, a reason id: network, damaged, lint or other), `pack_deleted`, `show_saved` (pads, crackers, seconds), `show_played` (crackers, seconds, replay), `clip_recorded` (seconds, source, frames), `clip_failed` (a reason id: unsupported, start or encoder), and `clip_shared` gains seconds, source and whether the share sheet stood in for a missing app. Never a show's name, a file path, a URL or an error message (schema test; the privacy policy says so and now covers clips and downloads; the Play compliance notes too). The metrics report gains a packs, shows and clips section (installs and failure rate by reason, show designers, plays and replays, clips recorded, failed and shared, share targets); the BigQuery query has the new columns. Performance: the recorder no longer allocates per frame (one cached readback callback with the frame times queued; on Android, reused JNI arguments and a native audio buffer instead of a Java array per block). GC guards for recording and for a show. Tests: `AnalyticsSchemaTests`, `AnalyticsSmokeTests` (packs, shows and clips reach analytics without names or paths), `PerformanceGuardTests` (recording, a show), metrics report self-test |

### Step 1 notes

- **Hosting layout:** `catalog.json` at the root; each pack's files and `manifest.json` under `packs/{packId}/{version}/`.
  Copy the folder to static HTTPS storage as it is.
- **The sample pack's cultural review is a placeholder.** The linter checks that the reviewer and date are filled; the
  owner fills the real ones before publishing any environment pack.

### Steps 2 and 3 notes

- **Offline first.** Nothing downloads until the player taps Download or Update; built-in and installed neighbourhoods
  work without a connection (F09). The catalog is cached so the browser can show what exists while offline.
- **Wardrobe and show packs** install and verify like any pack. Garments from a wardrobe pack reach the avatar creator
  once garment meshes can come from packs (they need Addressables bundles or new generator recipes): an open item.
  Show packs feed the show designer's templates (step 4).
- **To go live:**
  - Host `Docs/Phase9/sample_packs` (or real packs built the same way) on static HTTPS storage.
  - Put its address in `Assets/_Game/Resources/PacksConfig.asset`.
  - Fill the cultural reviewer and date before publishing an environment pack.

### Step 6 notes

- **Checked in the editor, not yet on the phone.** The tests run the real GPU packing and a real encoder (ffmpeg),
  but batch mode draws nothing to the screen, so they feed a test card instead of the screen. Two things only the
  tablet can confirm: the screen copy is the right way up on its graphics API (Vulkan or OpenGL ES), and its
  MediaCodec accepts the frames. To check: record one clip on the tablet, then
  `adb shell run-as com.ankitsharma.utsavnights ls cache/clips` and pull it for a look.
- **The clip shows the screen as the player sees it**, buttons included (PRD: "record the screen"). A show's clip
  hides the designer and shows only the slim play bar.

### Step 7 notes

- **Garbage per frame, measured in the editor:**
  - recording a clip: 0.01 KB on average (0.11 KB at most), the same as idle;
  - playing a show: 0.50 KB on average, within the finale's 4 KB limit. Its peaks (about 32 KB) come when a cracker
    is set on its pad, as in the stress finale (36 KB).
- **On the phone:** `gc_kb_per_frame` in a development APK's playtest log while recording gives the real number.
- **Review list:**
  - The Record button sits above Finale on every screen. Should it hide during decoration mode and walks?
  - Clips include the buttons on screen. A clean picture would need a second camera pass without the UI.
  - Should a clip carry a small "Utsav Nights" watermark for shares?
  - Should Stop be allowed before 10 seconds (and the clip thrown away)?
  - Show designer: the 1.5 s rhythm for new crackers, the timeline's scale, and whether templates should be
    editable in place or always copied.
  - Pack browser: a "new" badge for packs added to the catalog since the last visit.

## Phase 9 result

Content packs, the show designer and clips (F21, F22, F14) are built.
- **Packs (F21):**
  - hash-pinned manifests, a linter with budgets per type and the cultural review for environments;
  - staged installs that verify every file, an atomic swap, and recovery from a kill at any step;
  - environment, wardrobe and show packs in the game, and the storage manager (update, delete, Wi-Fi only).
  - **Exit gate:** a network failure during an update leaves the old version byte for byte (`PackManagerTests`),
    and a failed download in the game changes nothing (`PacksSmokeTests`).
- **Shows (F22):** create on launch pads, adjust delays, save, play and replay, templates and show packs, and
  export a clip of a show.
- **Clips (F14):** 10 to 20 seconds of the screen and the game's sound, H.264 and AAC in an MP4, shared straight to
  WhatsApp or Instagram, or through the share sheet.
- **Measured:** recording adds no per-frame garbage; a show stays within the finale's limit.
- **Open items** (with the feedback round after all PRD features):
  - **Hosting:** a static HTTPS location for the catalog and packs, and its address in `PacksConfig.asset`.
  - **On the tablet:** record one clip to confirm it is the right way up and that the hardware encoder accepts
    the frames (step 6 notes).
  - Garments from wardrobe packs reaching the avatar creator (steps 2 and 3 notes).
  - The cultural reviewer and date for any environment pack before publishing.
  - The review lists in the step notes above.
