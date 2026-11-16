# The web version

The whole app runs in a browser, shown inside a phone at the Galaxy A15's size (built 2026-11-14).

## Running it

```
npx expo start
```

Then open **http://localhost:8081** in Chrome, or press **w** in that terminal. `npm run web` does both. The same
Metro serves the Android app and the web version at once.

- On a computer you get a phone frame (Galaxy A15 landscape, 832 x 384 dp) scaled to fit the window. The app runs
  inside it at exactly that size, so it lays out as on the phone.
- Under the frame: **◁ Back** is the phone's back button (so is **Esc**), **Reload** reloads the app, and
  **Full window** opens the app without the frame.
- `?device=412x915` shows another phone size (portrait width x height, in dp).
- On a phone's own browser there is no frame: the app fills the screen.
- A desktop browser is far faster than a phone, so the web version tells you nothing about the A15's frame rate.

Log in with email and password. The Google, Facebook and TikTok buttons don't work on web yet (see below).

## How it works

Nothing in the app's screens changed. The Android-only pieces are swapped for web stand-ins, on web only.
`metro.config.js` swaps them, and the Android bundle doesn't contain them.

| On Android | On web | File |
|---|---|---|
| `@react-native-firebase/*` | Firebase's web SDK, compat API (same calls). Same project and key, read from `google-services.json`. | `firebase/*.js` |
| `react-native-webview` | An `<iframe>` with the same message link (`ReactNativeWebView.postMessage`, `injectJavaScript`, `injectedJavaScriptBeforeContentLoaded`) | `WebView.js` |
| Pages from the APK (`file:///android_asset/...`) | Served by Metro at `/android_asset/` from `android/app/src/main/assets`, with one start line added to each page | `metro.config.js` |
| Google Play Billing | Nothing to buy: buying says the store is unavailable | `src/billing/index.web.js` |
| AdMob (test ads) | Pretend ads: a rewarded ad grants its reward after a moment, as Google's test ads do | `ads.js` |
| `expo-file-system` | The app's small saved files in `localStorage`; downloads aren't cached | `fileSystem.js` |
| Turning the phone | Turns the frame | `orientation.js` |
| `SvgXml` (missing from react-native-svg's web build) | The browser draws the SVG itself | `svg.js` |
| Back button, `Alert` | Esc / the frame's Back button; the browser's alert and confirm dialogs | `setup.js` |
| Firebase Storage files in WebGL and fetch | Fetched through Metro at `/__fbstorage/`, because the bucket sends no CORS headers | `metro.config.js`, `setup.js` |

`App.web.js` is the web entry. Expo imports `./App`, and Metro picks `App.web.js` first on web. It shows the frame
(`PhoneFrame.js`), or inside the frame loads `setup.js` and then the real `App.js`.

The game pages come from `android/app/src/main/assets`. Run `node webgame/build.mjs` (and
`node tools/chest/build.mjs`) after changing them, as for Android.

## Testing helpers (development only)

- **`window.__app`**: in a development build, App.js puts a few of its switches on the page, so a test can open a
  screen or a card without playing up to it. Nothing here spends the account's coins or gems. From the browser
  console (inside the frame, or at `/?phone=1`):
  - `__app.setScreen('road')` opens a screen (`'stats'`, `'road'`, `'settings'`, `'shop'`, `'achievements'`,
    `'squishdoku'`, `'sort'`, `'stack'`, `'hop'`; `'home'` goes back).
  - `__app.setStreakIntro(true)` opens the streak screen.
  - `__app.setSpinState('show')` opens the daily spin. Don't tap SPIN: it spends the day's real spin.
  - `__app.setLevelUp({ level: 12 })` opens the level-up card.
  - `__app.setDailyOpen(true)` opens Daily Challenges.
  - `__app.play()` opens Squish Time with the selected creature.
  - `__app.flashApp('text')` shows a toast.
- **`/?phone=1&preview=1`** (`Preview.js`) shows the games' popups on their own, with made-up numbers, plus the
  email check screen a signed-in test account can't reach. `window.__preview('sd-result')` switches between them;
  `window.__previewNames` lists them.

## Not done yet

- **Google, Facebook and TikTok sign-in.** The `oauth` function sends the player back to
  `com.plushcrush.app://auth`, which a browser can't open. It needs a web return address: an allowed list of
  origins in `functions/oauth.js`, plus a deploy.
- **Putting it online.** `npx expo export -p web` builds the app into `dist/`. Hosting it also needs three things:
  - `android/app/src/main/assets` copied to `dist/android_asset/`, with the start line added to each page (Metro adds
    it today).
  - CORS on the Storage bucket, replacing the `/__fbstorage/` relay. The `gcloud` CLI isn't installed on this PC.
    With it:
    ```
    gcloud storage buckets update gs://squishy-app-61445.firebasestorage.app --cors-file=cors.json
    ```
    where `cors.json` is:
    ```json
    [{ "origin": ["https://your-site"], "method": ["GET"], "maxAgeSeconds": 3600 }]
    ```
  - The site's domain added in Firebase Console → Authentication → Settings → Authorized domains.
- **Purchases** can't work in a browser through Google Play.
