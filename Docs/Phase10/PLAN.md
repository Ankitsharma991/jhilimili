# Phase 10 plan: backup, home visits and shared shows (R4)

PRD section 19, Phase 10: features F23 (async home visits by code) and F24 (optional account and cloud backup), plus
the PRD task "shared fireworks show (asynchronous first)". Backups follow PRD 13.5: optional, started by the player,
encrypted at rest, with an export and import file as the offline fallback. Started 2026-10-30, ahead of R3 data, by
the owner's decision; feedback and changes come after all PRD features are built.

## Definition of done (from the PRD)

- **F23:** view a friend's saved home, offline first.
- **F24:** restore on a new device.
- **Privacy review** completed.
- **Exit gate:** evidence that players share and visit homes before anything real-time is built.

## Decisions (owner, 2026-10-30)

- **Backend: Firebase.** Firebase Auth, Firestore for share codes and reports, Cloud Storage for backups and
  snapshots. The same Google project as the analytics. Rules files keep each player's data their own.
- **Sign-in: Google**, through Android's Credential Manager, only for cloud backup and for owning share codes. The game
  never requires it and never stores the player's name or email.
- **Shared shows: yes, asynchronous, by code.** No real-time play.

## Design

- **Offline first.** Everything works without a connection. Online features say plainly when they need one.
- **Behind interfaces.** Accounts, documents and files sit behind `ICloud`, with an in-memory cloud that enforces the
  same rules as the Firebase rules files. The tests use it, and so does the game until Firebase is installed. The
  Firebase backends compile only once the Firebase packages are present, like Analytics and Crashlytics in Phase 5.
- **What a backup holds:** settings, the avatar, the house (with its layouts), saved shows, and the timed cracker
  unlocks. Never the purchase: "Remove ads" comes back through Google Play's Restore, so a copied backup can't unlock it.
- **Encryption:** AES-256 (CBC) with an HMAC-SHA256 over everything (encrypt then MAC), so a damaged or altered file is
  refused before anything is read.
  - **Export file:** the key comes from a password the player picks (PBKDF2-SHA256).
  - **Cloud backup:** a random key per account, kept in the account's own Firestore document. The backup file and its
    key are in different services.
- **Restore** checks every part (versions, catalogue ids, the house lint, the show rules) before it writes anything.
  It keeps the current files aside, so the player can undo.
- **Home snapshots carry no free text:** the house design, the neighbourhood, and the avatar's look only if the player
  ticks "Show my avatar" (off by default; never the name). Shows carry crackers, pads and times; their name is
  replaced by the code. So there's nothing to moderate but the arrangement itself.
- **Moderation and reporting:**
  - A visitor can report a code with a reason (from a fixed list).
  - Three reports from different players hide a code until reviewed.
  - The owner can withdraw their code at any time.
  - Codes expire after a year without visits.
  - Publishing is limited per account per day.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Backup bundle and file | The bundle (versioned, each part checked), encryption with integrity, restore with undo; export to a file through the share sheet and import from a file picker; Settings → Backup and restore | Round trip; a wrong password, a damaged or altered file and a newer version are refused; purchases never restored; undo |
| 2 | Cloud model and rules | `ICloud` (account, documents, files), share codes (format, check character, no lookalikes, no rude words), moderation rules, snapshot and show lint, quotas; the in-memory cloud enforcing the rules; the Firebase rules files | Codes, rules, quotas; one player can't read or change another's data |
| 3 | Account and cloud backup | Sign in and out; back up now, keep the newest three; restore on a new phone; delete my cloud data | Back up on one "phone", restore on another; signed out or offline explains itself |
| 4 | Home visits | Publish a snapshot and get a code; visit by code (download, check, keep for offline); view-only visit of the house in its neighbourhood; recent visits; report; withdraw | Play Mode: publish, visit, leave; a hidden or withdrawn code; offline revisit |
| 5 | Shared shows | Share a show by code; a friend plays it at their own spot (checked against their phone's limits) and can keep a copy | Share, play, keep; a show too big for the friend's phone explains itself |
| 6 | Firebase and Google sign-in | Firebase Auth, Firestore and Storage backends; Google sign-in through Credential Manager (an Android library); rules deployed by the owner; compiled against the Firebase SDK | Compile check against the SDK; rules tests on the emulator if available |
| 7 | Measure and privacy review | Analytics (backup, restore, share, visit, report; ids and counts only); the privacy review; policy and Play data safety updates; the exit gate section in the metrics report | Schema; privacy review checklist |

## Status

| Step | Status |
|---|---|
| 1. Backup bundle and file | Done. `Core/Backup`: `BackupArchive` (named parts, versioned, read by name so newer games can add parts), `BackupCrypto` (AES-256-CBC with HMAC-SHA256 over header and ciphertext; a password through PBKDF2-SHA256 at 120,000 rounds, or a 32-byte account key; a fresh salt and IV each time), `BackupRules` (the parts; never the purchase; passwords of at least 8 characters; file names; plain reasons). `Gameplay/Backup`: `BackupService` (collects the save files after writing pending changes; checks every part as the game loads it, refusing newer versions and damaged text before writing anything; sets the current files aside, writes the backup, merges the timed unlocks into this phone's progress and keeps its purchase as it is; the quality tier goes back to automatic; the old house's edit journal is dropped; Undo puts everything back), `BackupFiles` and `BackupFileReceiver` over the `UtsavBackup.androidlib` library (`BackupFileActivity`: Android's own "save as" and "open" pickers, no storage permission, 8 MB cap). `UI/BackupScreen` (Settings → Backup and restore: a password, Save backup file, Restore from file, a confirm step with what the backup holds, Undo last restore; the password work runs off the main thread). A restore reloads the scene so every service reads the new files. Tests: `BackupTests` (Mono: round trips, a wrong password or key, any changed byte, cut short, other files, newer versions, rules), `BackupSmokeTests` (Play Mode: back up one phone and restore on another, purchase kept out, unlocks merged, undo; damaged and empty backups; the screen from Settings: save, wrong password, confirm, restore, undo) |

### Step 1 notes

- **Restoring replaces, part by part.** A part the backup has replaces this phone's file; a part it doesn't have
  leaves the file alone. Undo puts back exactly what was there, including files the phone didn't have before.
- **The purchase never travels in a backup.** It comes back with Restore purchases (Google Play), so a copied file
  can't unlock anything. Timed unlocks from rewarded ads do travel, never longer than 24 hours from now.
- **A forgotten password can't be recovered.** The screen says so when the file is saved. The cloud backup (step 3)
  needs no password.
