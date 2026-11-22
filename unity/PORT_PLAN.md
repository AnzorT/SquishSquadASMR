# PlushCrush → Unity: port plan

Started 2026-11-22. A **fresh** Unity project (not the earlier local one from `UNITY_MIGRATION_PLAN.md`), rebuilding the
game natively in Unity. The goal is a 1:1 match with the app as it is today: same screens, same look, same rules.

## Decisions so far

| Decision | Choice | Why |
|---|---|---|
| Starting point | Fresh Unity 6 project (6000.3) in `unity/` | User's choice (2026-11-22) |
| Prototype screens (menu, house, park, chest smash) | Rebuilt natively in Unity, not a WebView | User's choice; a WebView would keep the web performance cost |
| Still art | Rendered by Chromium from the game's own files → PNG | Pixel-identical. Unity's SVG importer can't draw the blurs, blend modes and the fabric-texture filter the art uses |
| UI | UI Toolkit, built in C# | Flexbox, like React Native; text-based, reviewable |
| UI units | 1 unit = 1 dp; 832 × 384 (Galaxy A15 landscape), scaled by screen height | Same numbers as the React Native styles; the game's pages scale their HUD by height too (measured) |
| Color space | **Gamma** | The browser blends semi-transparent edges, shadows and glows in gamma (sRGB); Unity must blend the same way to match |
| Screens from the prototype | Taken apart by the browser into pieces + `layout.json`, put back together by Unity | Every piece pixel-identical; positions measured in the game, anchored to the same edges as the page |
| App id | `com.plushcrush.app` | `google-services.json` is keyed to it. A Unity build replaces the React Native app on a phone |
| Backend | Unchanged: Firebase (Auth, Firestore, Storage, Functions) | Unity only replaces the client |

## How "1:1" is checked

1. **Reference screenshots** of the real game at the A15's exact size (2340 × 1080 px) live in
   `Assets/_Project/Reference/`. They're frozen (fake clock, seeded randomness, animations held) so they're the same on
   every run: `node tools/unity-port/capture-references.mjs`.
2. In Unity: set the Game view to **2340 × 1080**, press Play, open **PlushCrush > Reference Overlay**, pick the
   screenshot, and slide the opacity. A rebuilt screen is done when nothing moves as the overlay fades in and out.
3. Animations: the same frozen captures at several moments (0 s, 0.5 s, 1 s…) compared with Unity at the same moments.

Each exporter also checks itself: it puts its pieces back together in the browser and compares with the real screen.
Main Menu: 0.57% of pixels differ by more than 2/255, 0.095% clearly; almost all of the clear ones are the coin and gem
numbers (drawn as text, by Unity in the end), the rest blur-filter noise in the scene.

The one known source of difference left: text Unity draws itself (the coin and gem numbers) goes through Unity's
text engine, not the browser's. Lettering that can't be drawn natively (the creature's name, the level number) is
exported as pictures instead.

## What the app is (the port's scope)

**Drawn by React Native** (`src/screens`, `src/components`, mostly `react-native-svg` + gradients, no filters):
Splash, Auth, Verify email, Main (Home tabs), Lobby, Settings, Achievements, Stats, Shop (deals, chests), Daily spin,
Daily challenges, Streak, Level road, Level-up card, Create (custom creatures), Chest opener, Loading, toasts,
interstitial ad. Mini games: **Squishdoku** (600 levels), **Squish Sort**, **Squish Stack**, **Squish Hop**.
**Squish Time**: the 3D squish toy (three.js, Tripo `.glb` models).

**Drawn by the designer's prototype** (`webgame/`, HTML + SVG in a WebView): Main Menu (HUD, level crest, PLAY →
Game Park carousel), Level Pass, Squad House v6 (8 rooms with animated creatures), the 7 yard games, chest smash.

## The hard parts, and the approach

1. **The menu and house scenes are generated, not drawn.** `main-menu-scene.js` builds the scene as SVG from the
   level (1–30) and the season (sunny / rainy / snowy). Colors shift continuously with level, rooms open at set levels.
   - **Plan:** render each (level, season) as a few layered PNGs (back: sky, sea, ground, back trees, house; front:
     path, front trees, bushes), with weather, vignette, lock signs and the creature drawn natively on top. 90
     variants is too much to ship in the app, so only the player's current one is downloaded from Firebase Storage
     and cached. Pixel-identical, and small to ship.
   - Alternative: port the generator to C# and draw vectors at runtime. Continuous, but loses the blurs.
2. **The creatures are alive.** `plush-rig25.js` deforms each creature's SVG every frame (idle, look, hop, dance,
   squish) as one soft body.
   - **Plan:** port the rig's math to C#, deforming a mesh made from the same SVG paths, with the fabric texture as a
     texture in the creature's material. This is the biggest fidelity risk: it gets its own side-by-side check, frame
     by frame.
3. **Animations everywhere** (CSS keyframes, JS timers): rewritten as C# tweens, timed from the original's numbers.
4. **Squish Time**: glTFast loads the same `.glb` models; the squish physics (`src/engine`) is ported line by line.
5. **Fonts**: the app's three (Baloo 2, Nunito, Fredoka) are in. The prototype also uses about a dozen display fonts
   (Cherry Swash, Alfa Slab One, Pacifico…); their TTFs come from Google Fonts' GitHub repo when a screen needs them.
6. **App size**: the exported art is large (creatures alone: 119 MB as PNG). It doesn't go in `Resources`; it ships
   through Addressables, with most of it downloaded on demand.

## Build order

| Phase | What | Status |
|---|---|---|
| 0 | Project, first-open setup, reference screenshots, art export | **Done** (this session) |
| 1 | Main Menu, still: scene at level 1 + HUD, checked 1:1 against `menu.jpg` | **Written** (2026-11-22), waiting for its first run in Unity |
| 2 | Main Menu, alive: every state, HUD motion, weather, PLAY → Game Park, Level Pass | **Written** except the creature rig (see below) |
| 2b | The creature rig (rive-rig.js + rive-generic.js) | Not started: a project of its own |
| 3 | Characters, Shop, Settings, Achievements, Level road | |
| 4 | Squish Time (3D) | |
| 5 | Squad House (8 rooms) | |
| 6 | Mini games: Squishdoku, Sort, Stack, Hop; yard games | |
| 7 | Firebase, ads, billing wired in (can start any time once the SDKs are imported) | |

Game logic (economy, XP, levels, achievements) is ported into `PlushCrush.Core` as plain C# with tests, alongside
whichever screen first needs it.

## Phase 1 notes (Main Menu, still)

- Pieces: the scene (whole 1000 × 460 drawing at the A15's scale, with the page's 0.087 px offset drawn in so it lands
  on whole pixels), the creature, name plate, the name (a picture per creature), 3 tabs, 2 pills (numbers drawn by
  Unity), 2 round buttons, PLAY, the crest, its level number (a picture per level), a sparkle.
- Rain is drawn by Unity from menu.js's own numbers (`MenuWeather.cs`); snow too. Birds (sunny, later levels): phase 2.
- Only one state exported so far: level 1, rainy, Mittens. Other creatures' names, other levels' numbers and scenes
  come when the exporter loops over them (phase 2), then the delivery plan above.
- Buttons answer with a "comes in phase N" message until their screens exist.

## Phase 2 notes (Main Menu, alive)

- **Every state is a picture made by the game:** the scene for levels 1-30 x sunny/rainy/snowy (without its vignette,
  which is a layer of its own), the Game Park's picture likewise, every creature's name plate (118, plaque and stars by
  rarity), every crest number (1-200), the XP ring per percent, the prize badge (1-9, 9+), each creature at rest.
  Big ones (scenes, parks, creatures: ~280 MB) are generated into `Art/Generated` (not in git): run
  `export-menu-states.mjs` before opening the project. The default state's pictures are in git.
- **HUD motion** (`MenuHud.cs`, `CssAnimation.cs`): the flourish every 5.2 s exactly as menu.js (crest float, wing
  flap, star flip, shine, sparkles; PLAY glow and glint 1.3 s later), the press effects (only PLAY and the crest have
  them in the page), the XP ring's fill-in on the first menu, the badge, sunny-day birds, rain and snow. The crest is
  taken apart layer by layer with each layer's own share of the drop shadow (solved from the browser's drawing); put
  back together it differs from the real crest on 0.07% of its pixels.
- **PLAY -> Game Park** (`MainMenuScreen.cs` EnterPark / LeavePark): the HUD fades (.35 s), the vignette fades (.45 s),
  turn (380 ms), walk with the camera following (2300 ms, EOUT / EIOC exactly as menu.js), turn back, the games; back:
  340 / 2100 / 340 ms. The park's picture lands on whole pixels like the house's. The games carousel and the Level
  Pass are separate pieces (GameParkOverlay, LevelPassScreen).
- **The creature is its still picture** for now: it doesn't breathe, blink, look around, play moves or turn side-on
  as in the game; walking it does the page's own fallback bob (mmBob). Taps on it do nothing (in the game a quick tap
  does nothing either, a bug there: rive-rig.js:1373; holding squeezes it, dragging spins it).

## The creature rig: what porting it means

The game draws every creature with RiveRig: hand-built rigs for 20 creatures (rive-rig.js, pet-defs.js,
pet2-defs.js) and a generic one for the other 98 (rive-generic.js: it tags each shape of the creature's front art
with a role and rigs it). Every frame it rebuilds every path of the creature from a pose of ~50 numbers: idle loop,
fidgets, glances, blinks, the showcase moves, a 2.5D head sphere the face slides around, touch (squeeze, spin),
the walking gaits (house-walk.js), plus the fabric texture and outline effects. About 1.1 MB of JavaScript.

A 1:1 port needs: the rig logic in C# (pose layers, keyframe tracks, springs, the warps), a vector renderer in Unity
that redraws the warped paths with their gradients every frame (the Vector Graphics package's tessellator, or a
custom mesh builder), and shaders for the fabric texture and outlines. Checked frame by frame against the browser,
like the rest. It's the biggest single piece of the port, and it's what makes the creatures feel alive everywhere in
the game (menu, house, Characters), so it's worth doing as its own focused piece of work.

## What needs you

- [ ] **Open the project** in Unity 6000.3 (with the Android module) and send a screenshot of the first screen
      (the setup check). See `README.md`.
- [ ] **Firebase and AdMob SDKs** (phase 7): imported in the Editor by hand, once.
- [ ] **A test account** for capturing the signed-in React Native screens as references. Best given as an environment
      secret, not in chat.

## Regenerating

```bash
node webgame/build.mjs --out webgame/test/out/webgame   # the game pages, as the phone has them
node tools/unity-port/capture-references.mjs            # reference screenshots -> Assets/_Project/Reference/game
node tools/unity-port/export-art.mjs                    # creature art -> Assets/_Project/Art/Generated (not in git)
node tools/unity-port/export-menu.mjs                   # Main Menu pieces + layout.json -> Resources/Screens/MainMenu
```
