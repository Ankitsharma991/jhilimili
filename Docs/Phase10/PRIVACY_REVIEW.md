# Phase 10 privacy review

Phase 10's definition of done asks for a privacy review of backup, home visits and shared shows (F23, F24). It was done
on 2026-11-03, against the code and rules as built, by the developer with Claude. A second reader should repeat it
before launch, ideally someone outside the project. [Owner: name and date.]

## What leaves the phone, and where it goes

| Data | When | Where | Who can read it | How long | Deleted by |
|---|---|---|---|---|---|
| Google account ID, email, name | Google sign-in (optional) | Firebase Authentication | The developer (console) | Until the account is deleted | Delete my cloud data |
| The account's backup key | First cloud backup or share | Firestore `users/{uid}` | The account; the developer (console) | Until deleted | Delete my cloud data |
| Cloud backups (sealed: AES-256 + HMAC) | Back up now | Cloud Storage `backups/{uid}` | Ciphertext only; the key is in Firestore | Newest three | Restore list delete; Delete my cloud data |
| Backup list (date, size, counts) | With each backup | Firestore `users/{uid}/backups` | The account | As the backup | As the backup |
| Shared home (design, neighbourhood, avatar's look if chosen) | Share my home | Firestore `shares/{code}` | Anyone signed in with the code | A year after the last visit, or take-down | Take down; Delete my cloud data; TTL |
| Shared show (crackers and timing) | Share a show | Same | Same | Same | Same |
| Share counts (visits, reports) and owner's account ID | With each share | Same | Anyone with the code (the ID opens nothing) | As the share | As the share |
| Reports (reason id, time) | Report | `shares/{code}/reports/{uid}` | The developer only | Until reviewed | The review |
| Anonymous account ID | Visiting or opening without signing in | Firebase Authentication | The developer (console) | Until the app is cleared [Verify Firebase's cleanup of unused anonymous accounts] | Clear app data |
| Backup file | Save backup file | Wherever the player saves it | Whoever has the file **and** the password | The player's choice | The player |
| Analytics outcomes (backup made, home shared or visited, worked or not) | With consent and the usage switch | Firebase Analytics | The developer | 2 months | Usage switch off; email request |

Nothing else is new: the game still reads no contacts, photos, location or microphone, and needs no new permission.

## Checks

| # | Check | Result |
|---|---|---|
| 1 | Playing needs no account; sign-in is offered only for backup and sharing | Pass (Settings → Backup and restore; sharing asks for it with the reason) |
| 2 | The game never reads the Google name, email or picture | Pass (`GoogleSignIn.java` returns the ID token only; `FirebaseCloud` uses `UserId` only) |
| 3 | No free text leaves the phone in a share | Pass (`HomeSnapshot` has no name fields and refuses an avatar name; shows travel without their name; reports use a fixed reason list) |
| 4 | The avatar's look is shared only by choice | Pass ("Show my avatar" is off each time the game starts, and only applies to the share made with it) |
| 5 | A share can't be found without its code | Pass (rules: get by code, never list; codes are random from 6 billion) |
| 6 | A share can be withdrawn and then reads as gone | Pass (content deleted; rules refuse withdrawn; visitors' offline copies are dropped when the server says so) |
| 7 | Players can't read each other's backups, keys or reports | Pass (rules owner-only; emulator tests written, run pending Java 21; memory-cloud tests pass) |
| 8 | Cloud backups are useless without the account | Pass (sealed with the account key; Storage holds ciphertext) |
| 9 | The purchase can't be copied through a backup | Pass (`BackupRules`: no progress part; tests) |
| 10 | Everything in the cloud can be deleted from the app | Pass (Delete my cloud data: backups, shares, account document, the Firebase account where allowed) |
| 11 | Analytics carry no code, account ID or content | Pass (schema; `AnalyticsSmokeTests` searches the log for the code and ID) |
| 12 | The privacy policy and Play data safety say all this | Updated (`privacy-policy.html`, `COMPLIANCE.md`); the owner fills the placeholders and the console |
| 13 | Children | The app is not for under-13s (Phase 4). Sharing has no chat or messages, so no contact between strangers beyond viewing a code someone chose to send |

## Risks kept open

- **Codes travel outside the game.** A code posted publicly lets anyone visit that home. The house and its decorations
  are what's visible: no name, no location, no photo. The bar during a visit says "view only". Acceptable; take-down
  is one tap.
- **The developer can read backups in principle**, because the key is in the account's Firestore document and the
  developer can read Firestore. True end-to-end encryption would need a password the player keeps (the backup file
  already offers that). Stated honestly in the policy ("a key that belongs to your account"); revisit if players ask.
- **Abuse without App Check:** scripts could report or publish. Turn on App Check (Play Integrity) before launch
  (`FIREBASE_SETUP.md`).
- **Anonymous accounts pile up.** Firebase can delete unused anonymous accounts automatically [Verify the setting and
  its default]; turn it on.

## Before launch

- [ ] Owner's second read of this review (name and date above).
- [ ] Rules emulator tests run (`tools/rules-test`, Java 21).
- [ ] App Check enforced; TTL policy on `shares.expireAt`; unused anonymous accounts cleaned up.
- [ ] Privacy policy placeholders filled and published; Data safety and content rating answered as in `COMPLIANCE.md`.
- [ ] Device run of every online flow (`FIREBASE_SETUP.md`, "My part").
