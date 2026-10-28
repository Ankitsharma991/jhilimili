# Store builds, uploads and rollback (Phase 5 step 1)

How to build the signed Google Play upload, put it on a testing track, and what to do if a release
goes wrong. [Verify] marks Play rules that change over time.

## Store build or device APK?

| | Device APK (`tools/run_all.sh`) | Store build (`tools/build_store.sh`) |
|---|---|---|
| Format | APK, sideloaded with adb | AAB, uploaded to Play Console |
| Signed with | Unity's debug key | Your upload key |
| Developer overlay (triple-tap the corner), sound test, "Store: refund" and the other tools | Yes | No |
| Local playtest log | Yes | No |
| Native debug symbols | No | In the bundle, for Play's crash reports, plus a zip next to it |
| Use for | Your own device tests and audits | Internal testing, the closed test, production |

Both use the same game code, ads, consent and purchases.

## One-time setup

1. **Create the upload key:** run `tools/make_upload_key.sh`. It asks for a password and your name, then
   saves `~/.utsav/upload.keystore`, outside the repository.
   - **Back up the file and its password now**, for example on a USB stick and in a password manager.
   - Never commit it, and never send it to anyone.
2. **Create the app in Play Console:** All apps → Create app. Use the name "Utsav Nights", type Game, free,
   and accept the declarations.
3. **Play App Signing** is on by default for new apps. Google keeps the key that signs what players
   download, and your key only proves uploads come from you. If you lose your upload key, Play support can
   reset it, but that takes a few days.
4. **New personal developer accounts** must run a closed test with at least 12 testers, opted in for 14 days
   in a row, before production access. [Verify] in Play Console's dashboard, which shows the current rule
   for your account.

## Versions

- **Version name**, for example `0.1.0`, is what players see. Raise the last number for fixes, the middle
  one for new content, and the first one for big releases.
- **Version code** is a whole number that must go up with every upload, on any track. Play rejects a code
  it has seen before, even if that upload was deleted.
- Each successful store build adds a row to [store_builds.tsv](store_builds.tsv) (created by the first
  build). The preflight refuses a code that isn't higher than every code in it.

## Building

```
tools/build_store.sh internal 0.1.0 2 --preflight   # checks only, about a minute, no key needed
tools/build_store.sh internal 0.1.0 2               # tests, then the signed AAB
```

The full build:
1. asks for the keystore password;
2. runs every EditMode and PlayMode test (about 10 minutes);
3. runs the preflight;
4. builds `Builds/Android/store/UtsavNights-0.1.0-2-internal.aab`.

If `BUNDLETOOL` points at a bundletool jar, it also prints the download size a phone will get.

**After the build:** commit `ProjectSettings/ProjectSettings.asset` (new version) and
`Docs/Phase5/store_builds.tsv`. Tag the commit, for example `git tag v0.1.0-2`, so the exact source of every
upload can be rebuilt.

### What the preflight checks

| Check | Internal track | Closed test and production |
|---|---|---|
| Version name is three numbers; code higher than every earlier store build | Blocks | Blocks |
| Upload key found and passwords given | Blocks | Blocks |
| Google Mobile Ads and Unity IAP installed (otherwise the build would show fake ads and give Remove ads away) | Blocks | Blocks |
| Debug Geography Eea ticked in AdsConfig | Blocks | Blocks |
| Target API at least 35 | Blocks | Blocks |
| Google's test ad IDs still in AdsConfig | Warning | Blocks |
| No privacy policy URL in AppLinks | Warning | Blocks |
| Firebase not connected | Warning | Warning |

The internal track is fine with test ads, so you can try the store build on your own phones now. From the
closed test on, the PRD needs your live AdMob IDs (Phase 4 exit gate) and the privacy policy.

## Uploading to a track

1. Play Console → your app → **Testing → Internal testing** (or Closed testing) → **Create new release**.
2. Upload the `.aab`. The first upload registers your upload key automatically.
3. **Release name:** leave Play's default (`2 (0.1.0)`).
4. **Release notes:** use the template below.
5. **Next → Save → Send for review** (closed and production) or **Start rollout** (internal).
6. **Testers:** add tester emails in the track's Testers tab, then share the opt-in link. Testers install
   from Play, which is also the only way purchases can be tested.

The `remove_ads` product and licence testers are in [SDK_SETUP.md](../Phase4/SDK_SETUP.md), section 2.

### Release notes template

Plain words, under 500 characters per language, no promises of future features.

```
<en-IN>
First test build of Utsav Nights. Light crackers with a lighter, place up to six, and run a finale.
Please tell us: did anything feel slow, confusing or too loud? Did the app close by itself?
</en-IN>
```

For later releases, say what changed for the player, for example "Rockets go higher. Fixed a rare
freeze when a call came in during a finale."

## Rollback plan

Play cannot switch players back to an older version. Every fix is a new upload with a higher version code.
What you can do, fastest first:

| Problem | Do this |
|---|---|
| A production rollout is going badly (crashes in Android vitals, bad reviews) | **Halt the staged rollout** (Production → Releases → Halt rollout). Players who haven't updated stay on the old version |
| The new version itself is broken | Check out the last good tag, raise only the version code, build and upload it as the "fix". For example, the good tag is `v0.1.0-2` and the bad upload was code 3: build `0.1.0` with code 4 |
| Ads misbehave, for example an ad at a bad moment or a policy warning | In AdMob, pause the ad units (Apps → Ad units). The game keeps working without ads, with no update needed. Fix, then re-enable |
| Purchases fail | Players keep anything already bought (Play holds the entitlement and Restore brings it back). Fix and upload; never deactivate the product, because that would stop Restore |
| Consent form problem in the EEA | Pause the ad units, as above. Analytics already waits for consent |

**Always use staged rollouts for production:** 10%, then 50% after 2 days with no new crash cluster, then
100%. Watch Android vitals (crash rate, ANR rate) on day 1, 3 and 7, as in the PRD.

**There is no remote switch:** the app works offline and has no server. Anything that needs changing
without an update must go through AdMob or Play Console, as in the table above. Interstitials ship
switched off, so the riskiest ad format is never live by accident.

## In-app review prompt (step 4)

The game asks Google Play to show its rating dialog when all of these are true:
- it's the second launch or later;
- this session went well: a finale finished, or 10 or more bursts;
- the moment is quiet: nothing burning, no finale running, no sheet open, no touch for 2 s, and 3 s since
  the last burst;
- no ad in the last 2 minutes;
- it hasn't asked this session, or in the last 30 days.

Play adds its own limit on top and may show nothing. The game never learns whether the player rated.

| Build | What appears |
|---|---|
| Editor and device APK | A white "Test review prompt" card with "Not now". **Developer overlay → Review: reset** makes it eligible again; **Review: ask now** shows it at once |
| Store build | Google Play's real dialog. It only appears when the app was installed from Play: use the internal testing track. With internal app sharing the dialog shows, but Submit is greyed out [Verify] |

Every request is in the playtest log as `review_prompt`, with the trigger (`finale` or `payoffs`).
