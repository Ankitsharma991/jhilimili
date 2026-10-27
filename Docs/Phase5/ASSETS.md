# Audio and art import rules (Phase 5 step 2)

Rules for the licensed sounds and any new art, so they arrive at the right size and sound right on a phone.
New files under `Assets/_Game/Audio` and `Assets/_Game/Art` get these settings automatically when first
imported. The content lint checks every file before each build: errors stop the build, warnings are for a
person to judge. If someone changes a file's settings by hand, **Utsav → Content → Apply Import Rules** puts
them back. Every licensed file also needs its row in [LICENCE_LOG.md](../LICENCE_LOG.md) before it's
committed.

## What to ask for or export

| | Ask for |
|---|---|
| Format | WAV, 48 kHz (44.1 kHz is fine), 16 or 24 bit. Higher rates are resampled to 48 kHz on import and only cost download time |
| Channels | Mono for crackers. Stereo only for ambience beds |
| Start | The sound starts at the first sample: no silence, no fade-in. Every 10 ms of silence is 10 ms of extra delay after the tap |
| Level | Peaks around -1 dBFS, never touching full scale. Keep the crackers' relative loudness as recorded, so a bomb is louder than a pop; the mixer balances the rest |
| Loops | Seamless: the end flows into the start with no click. Named with `_loop` at the end |
| Length | One-shots 4 s or less, including the tail. Loops 10 s or less. Anything longer is ambience |

## Roles

The role comes from the folder and the file name.

| Role | How it's recognised | Loaded as | Vorbis quality | Mono | Length limit |
|---|---|---|---|---|---|
| One-shot (bursts, pops, clicks) | Anything else | Decompressed on load, so it starts on the frame it's triggered | 0.6 (about 130 kbps) | Yes | 4 s |
| Loop (fuse hiss, fountain roar, chakri whirr) | Name ends in `_loop` | Decompressed on load | 0.6 | Yes | 10 s |
| Long (ambience, music) | In `Audio/Ambience/` or `Audio/Music/`, or named `amb_…` or `music_…` | Streamed from storage | 0.45 | No | 10 min |

All decompressed sounds together must fit in **32 MB of RAM** (today: 18 placeholder sounds, 1.5 MB). That's
about 80 one-shots of 4 s at 48 kHz mono, far more than R0 needs.

Vorbis quality 0.6 is a starting point [Hypothesis]. When the licensed sounds arrive, compare 0.5, 0.6 and 0.8
by ear on the tablet speaker and on headphones with the blind sound test. Raise the number in
`ImportRules.SpecFor` only if there's an audible difference.

## Art

| Folder | Rule |
|---|---|
| `Art/Icons/` (tray icons) | Sprite, no mipmaps, at most 256 px, compressed (ASTC on Android). Today's 9 icons are 256 px, about 64 KB each |
| Rest of `Art/` | At most 1024 px, compressed |
| `Art/Spike/` | Archived Phase 1 test art, not checked |

## Warnings on today's placeholders

The lint flags these, as expected. They go away when the licensed sounds replace the placeholders:
- `bomb_boom`, `fuse_hiss_loop` and `rocket_burst` peak at full scale;
- `fountain_ignite` starts with 21 ms of near-silence.
