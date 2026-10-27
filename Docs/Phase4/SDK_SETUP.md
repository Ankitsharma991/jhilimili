# Connecting your AdMob, Play Console and Firebase accounts

The app ships with Google's **test** ad IDs. They always show test ads, and tapping them is safe.
Purchases need the product to exist in Play Console. Follow these steps when your accounts are ready.
No code changes are needed.

## 1. AdMob (ads)

1. Create an AdMob account at admob.google.com and add an Android app. If the app is not on Play yet,
   choose "not published".
2. Create two ad units: one **Rewarded** (used for the Peacock unlock) and one **Interstitial** (built,
   but switched off for now).
3. In Unity, select `Assets/_Game/Resources/AdsConfig.asset` and fill in:
   - **Android App Id**: `ca-app-pub-XXXXXXXX~YYYYYYYY`
   - **Rewarded Unit Id** and **Interstitial Unit Id**: `ca-app-pub-XXXXXXXX/ZZZZZZZZ`
4. **Before you use live IDs, register your test devices.** Run the app once. In logcat, find
   the line from the Mobile Ads SDK that starts with `Use RequestConfiguration.Builder().setTestDeviceIds`
   and copy the hashed id into **Test Device Ids**. Never tap a live ad on your own phone (PRD 15.2).
5. **To test the consent form:** also tick **Debug Geography Eea**. It only affects the devices listed in
   Test Device Ids.
6. In AdMob, open Privacy and messaging, create a **GDPR** message for the app, and publish it. Google's
   consent form only appears once that message exists.

Until you go live, keep the test IDs. The build log warns whenever a release build still has them.

## 2. Google Play Console (purchase)

1. Create the app with package name `com.ankitsharma.utsavnights`.
2. Upload one build to a testing track. In-app products can only be created after the first upload.
3. Under **Monetize > Products > In-app products**, create:
   - **Product ID** `remove_ads` (it must match exactly)
   - Name "Remove ads and unlock all", one-time purchase, set a price, then **Activate** it.
4. Under **Settings > License testing**, add your Google account. Test purchases are then free and can be
   refunded from the Order management page. That is how you test the refund path.

## 3. Firebase (analytics and crash reports)

1. Create a project at console.firebase.google.com and add an Android app with the same package name.
2. Download `google-services.json` and give it to me. I'll add the Firebase Analytics and Crashlytics
   packages and connect them, the same way as AdMob.
3. In AdMob, link the app to Firebase (PRD 15.2 reporting).

## What stays the same

- The editor and the automated tests always use the development ad card, consent form and store.
  Only phone builds use the real SDKs.
- All ad rules (no ads during fireworks, consent first, and so on) apply in exactly the same way to
  real ads.
