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
| 2 | Cloud model and rules | `ICloud` (account, backups, shares), share codes (format, check character, no lookalikes, no rude words), moderation rules, quotas; the in-memory cloud enforcing the rules; the Firebase rules files and their emulator tests | Codes, rules, quotas; one player can't read or change another's data |
| 3 | Account and cloud backup | Sign in and out; back up now, keep the newest three; restore on a new phone; delete my cloud data | Back up on one "phone", restore on another; signed out or offline explains itself |
| 4 | Home visits | Publish a snapshot and get a code; visit by code (download, check, keep for offline); view-only visit of the house in its neighbourhood; recent visits; report; withdraw | Play Mode: publish, visit, leave; a hidden or withdrawn code; offline revisit |
| 5 | Shared shows | Share a show by code; a friend plays it at their own spot (checked against their phone's limits) and can keep a copy | Share, play, keep; a show too big for the friend's phone explains itself |
| 6 | Firebase and Google sign-in | Firebase Auth, Firestore and Storage backends; Google sign-in through Credential Manager (an Android library); rules deployed by the owner; compiled against the Firebase SDK | Compile check against the SDK; rules tests on the emulator if available |
| 7 | Measure and privacy review | Analytics (backup, restore, share, visit, report; ids and counts only); the privacy review; policy and Play data safety updates; the exit gate section in the metrics report | Schema; privacy review checklist |

## Status

| Step | Status |
|---|---|
| 1. Backup bundle and file | Done. `Core/Backup`: `BackupArchive` (named parts, versioned, read by name so newer games can add parts), `BackupCrypto` (AES-256-CBC with HMAC-SHA256 over header and ciphertext; a password through PBKDF2-SHA256 at 120,000 rounds, or a 32-byte account key; a fresh salt and IV each time), `BackupRules` (the parts; never the purchase; passwords of at least 8 characters; file names; plain reasons). `Gameplay/Backup`: `BackupService` (collects the save files after writing pending changes; checks every part as the game loads it, refusing newer versions and damaged text before writing anything; sets the current files aside, writes the backup, merges the timed unlocks into this phone's progress and keeps its purchase as it is; the quality tier goes back to automatic; the old house's edit journal is dropped; Undo puts everything back), `BackupFiles` and `BackupFileReceiver` over the `UtsavBackup.androidlib` library (`BackupFileActivity`: Android's own "save as" and "open" pickers, no storage permission, 8 MB cap). `UI/BackupScreen` (Settings → Backup and restore: a password, Save backup file, Restore from file, a confirm step with what the backup holds, Undo last restore; the password work runs off the main thread). A restore reloads the scene so every service reads the new files. Tests: `BackupTests` (Mono: round trips, a wrong password or key, any changed byte, cut short, other files, newer versions, rules), `BackupSmokeTests` (Play Mode: back up one phone and restore on another, purchase kept out, unlocks merged, undo; damaged and empty backups; the screen from Settings: save, wrong password, confirm, restore, undo) |

| 2. Cloud model and rules | Done (the rules' emulator run waits for Java 21, see notes). `Core/Cloud`: `ICloud` (Google or anonymous sign-in, the account's backup key, put, list, get and delete backups, publish, open, visit, report, withdraw, my shares, delete my data), `ShareCodes` (8 characters from 25 digits and consonants: no vowels or Y so no words, no 0/O, 1/I/L, S or Z; a weighted check character that catches every single wrong character and every swapped pair; shown as BCDF-2345), `CloudRules` (hidden at 3 reports from different accounts until reviewed, 10 shares a day per account, a year without visits expires a code, the newest 3 backups, 200,000-character payloads, 8 MB backups; plain words for every error). `MemoryCloud` and `MemoryCloudServer` (one server, many phones; the checks of the rules files, the moderation review). `Docs/Phase10/firebase`: `firestore.rules` (accounts and backups their owner's only; shares opened by code and never listed; a share's content in a separate document readable only while visible, so the game can still say why a code is closed; visits and reports change only their counters, one report per account; publishing within the day's count, which only moves by publishing; withdraw and delete by the owner), `storage.rules` (each account's own backups folder, sealed files under 8 MB, backup names only), `firebase.json`, `firestore.indexes.json`. `tools/rules-test` (the same cases on the Firestore and Storage emulators: `npm test`). The Mono test runner now honours SetUp and TearDown. Tests: `CloudModelTests` (Mono), `tools/rules-test/rules.test.mjs` |

| 3. Account and cloud backup | Done. `Gameplay/Cloud`: `CloudInstaller` (Firebase through `MonetisationProviders.Cloud` once installed; otherwise the memory cloud in the editor and development builds, one server for the whole run so sign-in outlives a restore's reload; in a release build `UnconfiguredCloud`, which says the online service isn't switched on yet rather than pretending), `CloudService` (sign in with Google, a visitor's anonymous account making way; sign out; Back up now: the same archive as a backup file, sealed with the account's own key on a worker thread, the newest three kept; list; fetch, open and check a backup before anything is written; restore with the same undo as a file; Delete my cloud data: backups, key and shares, then sign out, the phone's own saves untouched). `UI/BackupScreen` is now a scrolling page: Cloud backup (the account line, Sign in with Google or Sign out, Back up now, the backups with their date and counts and Restore, Delete my cloud data with a confirm step) and Backup file (as in step 1); one confirm step for both. Backups are made only when the player asks (PRD 13.5). Tests: `CloudBackupSmokeTests` (Play Mode: four backups on one phone, three kept; the same Google account on a new phone restores the newest; another account sees and fetches none; signed out and offline explain themselves; deleting cloud data keeps the phone's saves; the screen: sign in, back up, restore, undo, delete) |

| 4. Home visits | Done. `Core/Cloud/HomeSnapshot` (the house design, its neighbourhood, and the avatar's look only when the player turns on "Show my avatar"; never the avatar's name or the layouts' names; repaired like a save file; newer versions refused; `ForTier` shows the owner's decorations in their order while they fit the visitor's decoration budget), `VisitHistory` (the newest ten visits kept with their snapshot, for offline). `Gameplay/Cloud/HomeVisits` (share my home: Google sign-in needed, the account document made first; visit by code: no account needed, a silent anonymous one; the friend's house shown in place of the player's own, view only, in their neighbourhood when this phone has it, their avatar when they chose it, crackers cleared and walks ended; offline or server trouble falls back to the copy on the phone; hidden, taken down, expired or missing codes are forgotten; report; take down; my homes), `TextShare` (the share sheet with a line of text, also copied). `HomeService` shows a visited design without touching the player's own and refuses edits while visiting; decoration mode won't open; `CourtyardAvatar` shows the friend's look or nobody. `UI/HomeVisitScreen` (the Visit button above Shows; Visit a home: a code and recent visits with Visit again; Share my home: Show my avatar, Share, the new code with Send, your codes with their visits, Send and Take down; during a visit a see-through cover with a bar: whose code, notes such as fewer decorations on this phone, Report with four reasons, Leave). Tests: `HomeSnapshotTests` (Mono), `HomeVisitSmokeTests` (Play Mode: a friend's riverside home with their avatar, view only, the player's house untouched, back home after; offline copy; hidden after three reports and forgotten; taken down; the screen: sign-in needed to share, a bad code, share, send, visit with the bar, report, leave, take down) |

### Step 1 notes

- **Restoring replaces, part by part.** A part the backup has replaces this phone's file; a part it doesn't have
  leaves the file alone. Undo puts back exactly what was there, including files the phone didn't have before.
- **The purchase never travels in a backup.** It comes back with Restore purchases (Google Play), so a copied file
  can't unlock anything. Timed unlocks from rewarded ads do travel, never longer than 24 hours from now.
- **A forgotten password can't be recovered.** The screen says so when the file is saved. The cloud backup (step 3)
  needs no password.

### Step 2 notes

- **Rules tests need Java 21.** `firebase-tools` 15 refuses older Java, and this machine has Java 17 (Unity's bundled
  JDK is 17 too). With Java 21 installed: `cd tools/rules-test && npm test`. Until then the same cases run against
  the memory cloud.
- **What a visitor can see of a share:** its kind, counts, dates and the owner's Firebase account id (a random id that
  opens nothing: accounts and backups are owner-only). Never a name or email. The game itself doesn't show it.
- **Moderation review** is done in the Firebase console: open a hidden share's reports, then reset its count to keep it
  or delete it. A review tool can come with the feedback round.
- **Known limits without a server function:** deleting and recreating an account restarts its day count, and nothing
  stops a script from using many anonymous accounts to report. Turn on Firebase App Check (Play Integrity) before
  launch, and add a Cloud Function for reports if abuse shows up.
- **Expiry:** set a TTL policy on `shares.expireAt` in the Firestore console, so expired shares are deleted, not only
  refused.

### Step 3 notes

- **On the tablet nothing works online yet:** the release APK has no Firebase, so the Cloud backup section says the
  online service isn't switched on yet. The backup file works. Step 6 connects Firebase.
- **Signing in asks Google for the account only.** No name, email or profile picture is read or kept; the game uses
  the account id to find the player's backups.
- **Losing the account means losing the cloud backups.** Their key lives in the account. Backup files are the
  fallback that needs no account.

### Step 4 notes

- **A visit is view only.** Nothing can be placed, lit or decorated while visiting, and the player's own house and
  saves are never touched. Leaving brings back their house, avatar and neighbourhood.
- **Neighbourhoods:** a friend's home stands in their neighbourhood when this phone has it (built in or downloaded);
  otherwise in the visitor's own, and the bar says so.
- **Review list:**
  - Should a visitor be able to light a cracker in a friend's courtyard (on their own phone only)?
  - Should a visit let the visitor walk round the friend's neighbourhood?
  - A "copy this decoration idea" button?
