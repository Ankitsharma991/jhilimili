# Firebase for backup, home visits and shared shows

Phase 10 uses the same Firebase project as analytics ([Phase 5 FIREBASE.md](../Phase5/FIREBASE.md)). The code is ready.
It compiles against the Firebase Unity SDK 14.1.0 and waits for the project and the packages.

## What is already in place

| Piece | Where | Status |
|---|---|---|
| The cloud on Firebase: Auth, Firestore, Storage | `Assets/_Game/Scripts/Sdk/FirebaseCloud/FirebaseCloud.cs` | Compiled against SDK 14.1.0 (2026-11-03); not yet installed |
| Registration on devices only (the editor and tests keep the memory cloud) | `FirebaseCloudRegistration.cs` | Same |
| Assembly compiled only when `com.google.firebase.auth`, `firestore` and `storage` 14.0.0 or newer are installed | `UtsavNights.FirebaseCloud.asmdef` | Same pattern as Analytics and AdMob |
| Google sign-in through Android's Credential Manager | `Assets/Plugins/Android/UtsavSignIn.androidlib` and `GoogleSignInBridge.cs` | In every build; used once Firebase is installed |
| Rules, indexes and emulator config | `Docs/Phase10/firebase/` | Written; tests in `tools/rules-test` (need Java 21) |
| Compile check against the real SDK | `FIREBASE_SDK=<folder with the unpacked packages> tools/compile-check/check.sh` | Passes |

Until all of this is set up, a release build says "This needs the game's online service, which isn't switched on yet",
and the backup file keeps working.

## Your part

1. **Authentication:** in the Firebase console, open Authentication, then Sign-in method. Turn on:
   - **Google:** pick a support email. This creates the web OAuth client that the game signs in with.
   - **Anonymous:** visitors open shared homes and shows with it.
2. **SHA-1 fingerprints:** Google sign-in only works from apps whose signing certificate Firebase knows. Open Project
   settings, then Your apps, then Android, then Add fingerprint, and add both:
   - your upload key: `keytool -list -v -keystore <your upload keystore>` (see [Phase 5 RELEASE.md](../Phase5/RELEASE.md));
   - the Play app signing key: in Play Console, Test and release, then App integrity.
3. **Download `google-services.json` again** after step 2 (it now lists the OAuth clients) and send it to me.
4. **Firestore:** open Firestore Database, then Create database, in production mode. Pick the location closest to
   your players (it can't be changed later).
5. **Storage:** open Storage, then Get started, in production mode, in the same location. Storage needs the
   pay-as-you-go Blaze plan on new projects. Set a **budget alert** (Google Cloud console, Billing, then Budgets) at a
   small amount, for example $5 a month. [Verify the current free quota.]
6. **Rules and indexes:** with the Firebase CLI (`npm install -g firebase-tools`, then `firebase login`):

   ```
   cd Docs/Phase10/firebase
   firebase deploy --only firestore:rules,firestore:indexes,storage --project <your project id>
   ```

7. **TTL policy:** in Google Cloud console, open Firestore, then Time-to-live. Add a policy on collection group
   `shares`, field `expireAt`. Shares nobody has visited for a year are then deleted, not only refused.
8. **App Check:** in the Firebase console, open App Check, register the Android app with **Play Integrity**, and
   enforce it for Firestore and Storage once the closed test shows it working. This stops scripts that aren't the game
   from reporting or publishing.

## My part when the file arrives

1. Add `com.google.firebase.auth`, `firestore` and `storage` 14.1.0 as embedded packages, next to app, analytics and
   crashlytics. As with those, remove the `com.google.external-dependency-manager` dependency line, since the project
   uses Unity's own dependency manager.
2. Rebuild. The `UtsavNights.FirebaseCloud` assembly compiles, and on the phone the game uses Firebase.
3. On the tablet, with your permission to open the app:
   - Sign in with Google from Settings, then Backup and restore. Back up, then restore.
   - Share the home. Visit its code from a second phone, or the same phone signed out.
   - Share a show, then open its code.
   - Report a code from three accounts and check that it hides.
   - Delete the cloud data.
4. Check in the console that every document matches the shapes in `firestore.rules`.

## Moderation review

Hidden shares (three reports) wait for you:

1. In Firestore, filter `shares` where `reports >= 3`.
2. Open the share's `content/main` and look at it. A home's payload names the decorations and house parts; the game
   can show it if you visit it as its owner sees it.
3. To keep it, set `reports` to 0 and delete its `reports` documents. To remove it, delete the share and its
   `content/main`.

## Costs [Hypothesis]

- A backup is about 10 to 100 KB, and three are kept per account.
- A shared home is a few KB; a show is under 1 KB.
- Firestore reads: a visit is 2 reads and 1 write.
- At hundreds of players this stays within the free daily quotas, apart from Storage's Blaze requirement. The budget
  alert is there in case it doesn't.
