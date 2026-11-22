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
| 2 | Main Menu, alive: every state, HUD motion, weather, PLAY → Game Park, Level Pass | **Written** except the creature rig (see below); Level Pass written by a helper (2026-11-22) |
| 2b | The creature rig (rive-rig.js + rive-generic.js) | Being ported to C#: see `RIG_PORT.md` |
| 2c | The game intro on the menu (app/intro.js) | **Written** (2026-11-22), timeline and sounds checked against the game; see "Game intro" |
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
- **The creature is live** where its rig is ported (Mittens so far, see `RIG_PORT.md`): the game's rig in C#
  (breathes, blinks, looks round, idle fidgets, the menu's moves and side-on turns), drawn on the GPU. Creatures not
  ported yet show their still picture, as does Mittens for the moment the rig's moves take to load. Walking, the box
  does the page's bob (mmBob). The menu's stage takes no taps, as in the page (.mm-stage: pointer-events none).

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

## Level Pass (the crest's page)

`webgame/app/menu.js` M.levels, opened by tapping the crest. Taken apart by `tools/unity-port/export-level-pass.mjs`
into `Resources/Screens/LevelPass/` (pictures + `layout.json`), put back by `Scripts/App/LevelPass/LevelPassScreen.cs`.

- **Pieces**: the page's wood background; header (back orb, "Level Pass" lettering, CLAIM ALL plaque, LV plaque with
  its pink bar's track and fill, coin plaque and its coin); track (rail and its fill, the level node in 8 looks, the
  ok / ! / lock badges, the "you" frame and pointer, the creature inside it per creature); panel (frame, "LEVEL n"
  gold and white for levels 1-50, CLAIM, the action's frame with its check and lock, the reward card, the glow behind
  the art, 6 tags, 55 group chests + 5 plain ones, coin and gem piles, 12 room pictures, 103 reward titles); the
  burst's coin and gem.
- **Layout**: `layout.json` keeps the page's own numbers (design px, × k = 384 / 393 in Unity) measured in the page,
  and each picture's place relative to its element's box in A15 px, so it lands on the page's pixel. Pieces whose
  width changes are cut in three: plain ones stretch a 4 px strip of their uniform middle; wooden ones (the LV and coin
  plaques, the panel) cut their left part to length from one drawn long, so the wood's stripes stay put. Pieces that
  move along the track (nodes, badges, the "you" frame) are drawn at 4 sub-pixel phases.
- **Behaviour**: the track drags sideways with menu.js dragRow's numbers (6 dp threshold, glide ×0.94 per 16 ms,
  a tap after a drag swallowed, the wheel); a tap picks a level, a second tap on one waiting claims it; CLAIM,
  CLAIM ALL, "Claiming…" while the caller's handler answers (`LevelPassClaimHandler`; none = claimed at once, as the
  page does outside the app); 9 coins or gems fly to the coin plaque (menu.js burst timings); waiting levels bob
  (sqClaim, CssAnimation); the track and the panel's content fade in over 0.18 s; back orb and phone back close it.
- **Data**: `LevelPassState` (xp, road, coins, creature), the app's own shapes. The menu builds it from `MenuState`
  (new: `XpPer`, `Road`; null road = the page with no app data: levels, no prizes). **PlushCrush > Screens > Level
  Pass** shows the stand-in player of the pictures and of `Reference/game/level-pass.jpg` / `level-pass-L8.jpg`
  (level 6, 420 / 700 XP, levels 1, 2, 4 claimed, 1,250 coins, Mittens, today's Forest chests).
- **Self-check** (the pieces put back together by Unity's rules, in the browser, against the page): opening state
  0.88% of the screen's pixels differ by more than 2/255 outside the text Unity draws, 0.016% clearly (>40/255);
  level 8 picked (three cards) 4.05% and 0.56%: the cards' pieces are drawn at the one-card position, so with three
  cards their edges are up to half a pixel off.

Known differences:
- Text drawn by Unity (LV n, the XP, the coins, CLAIM ALL (n), level numbers, the lines under the levels and the
  rewards, the action's words) goes through Unity's text engine; the header's plaques take their widths from it.
  Descriptions keep the browser's own line breaks.
- Wooden plaques drawn long differ from the page's by Chrome's gradient noise (single pixels, up to ~10/255).
- Other screen shapes: the background picture is stretched (its stripes widen); the rest follows the page's layout.
- Not ported: sounds (tap, score), the page's gold toast for claim errors (Unity's toast instead), the menu creature's
  "happy" when the pass closes (no rig yet). The fade starts on the first frame (the page waits for a steady frame
  rate, up to 1.5 s). A mouse wheel step is a guess (~100 px, as a browser's notch).
- Levels past 50 and titles not exported fall back to Unity text with an outline.
- 21 MB of pictures in Resources (chests 7.8 MB): to move to Addressables with the rest of the art.

## Game Park overlay (phase 2, written 2026-11-22, waiting for its first run in Unity)

The games carousel shown in the park after PLAY (menu.js `showGames`): the dim, the header (back orb + "Game Park"),
the rays and halo, a card per game, the arrows, the dots, the locked toast. Not the walk to the park, the park's picture
or the creature: the menu screen lays the overlay over itself once the walk is done.

- **Exporter**: `tools/unity-port/export-game-park.mjs` opens the park the instant way (no walk) with stand-in games
  (Squish Time + the app's four, as `src/game/player.js` sends them; Mittens), every transition finished, and shoots
  every card while it's the centre one → `Resources/Screens/GamePark/` (55 pictures, ~20 MB, the dim alone 3 MB:
  Chrome dithers its gradients) + `layout.json`. Per card: its frame (Squish Time's title drawn in), its window picture
  (a poster; Squish Time's pink background; Frybo's clip's first frame, the clip copied next to it), the window's inner
  shading, PLAY / LOCKED. Variants UI Toolkit can't make: `brightness(1.35)` twins of everything (sqCen, the centre
  card's flash), `saturate(.7)` buttons (the cards that aren't the centre one), locked windows
  (`saturate(.2) brightness(.7)`) and the lock. With `creatures`, every creature's Squish Time picture →
  `Art/Generated/Resources/ParkCreatures` (not in git; `creatures.json` says where each goes).
- **Unity**: `Scripts/App/MainMenu/GameParkOverlay.cs` (a VisualElement: `new GameParkOverlay(games, creatureKey,
  creatureName)`; events `Back`, `Play(id)`, `CreatureMove(name)`, `Sound(name)`; `AllowedMoves` for the rig's
  filter; `HandleBack()` for the phone's back button), `GameParkLayout.cs`, and `Motion/CssTransition.cs` (CSS
  transitions with the browser's rules, reversing included). Every timing and curve is menu.js's own (listed at the top
  of GameParkOverlay.cs). Check it with **PlushCrush > Game Park > Show alone...** against
  `Reference/game/park-overlay-alone*.jpg` (Freeze animations on); `park-overlay.jpg` is the whole park page.
- **Self-check** (each card centred, and a locked one, put back together in the browser and compared with the real
  overlay over the same park): 0.73 – 0.80% of the screen's pixels differ by more than 2/255,
  0.17 – 0.22% clearly (> 40/255), almost all of those the words (drawn by the browser in the check, as Unity will
  draw them: their exact sub-pixel place differs). Leaving the words out: 0.19 – 0.25%, 0.002 – 0.007% clearly: the
  browser's gradient dithering (±3/255 on the rims, rays and orbs) and the window's top edge row
  (`webgame/test/out/unity-park-check/`).
- **Words** Unity draws itself (each game's line, the progress chip, the toast): same font, size, colour, letter
  spacing and shadow; the browser's line breaks for the stand-in lines, broken the same greedy way by Unity for other
  creatures' names (two lines, then …). Nunito and Fredoka have no ★ (Squish Hop's line, a chip's stars): it's the
  browser's own picture of it, set in the row.

Known differences:
- **Fades**: CSS fades a card as one picture, UI Toolkit multiplies each piece's opacity, so while a card fades
  (.35 – .45 s) its overlapping pieces show slightly through each other. The frame is cut away under the window and the
  button to keep this small.
- **sqCen**: the brightened twins crossfade out, which is the same as `brightness()` except where 1.35× clips; Frybo's
  clip itself isn't brightened.
- **Dots**: 18-wide pills 9-sliced to their width; while one changes (.3 s) its colour is a crossfade of the two
  pictures (the same at rest, slightly different halfway); put on whole A15 pixels.
- **The squishing creature** is clipped by the window with UI Toolkit's rounded clip (not anti-aliased). Its picture
  is the design's thumbnail: the app's border looks (`state.looks`) aren't exported.
- **Other screen shapes**: the carousel keeps its place from the right edge (the page's carL formula), but the cards
  keep the A15's width (the page widens them with the screen); the dim stretches.

## Game intro (written 2026-11-22, waiting for its first run in Unity)

The designer's "Squish wakes the house" (`webgame/app/intro.js`, started by menu.js `startIntro`): night and today's
weather on the menu's own house, the box drops and wobbles, Mittens pops out, three squishes light three rooms, the
house glows, dawn, the squad peeks out and waves, the SQUISH SQUAD logo drops in, TAP TO START. The app's first menu of
a launch plays it (`MenuState.Intro`, the start data's `intro`: `AppRoot` asks, as App.js does, once a launch); Skip
and the phone's back end it.

- **Timeline** (`Scripts/App/MainMenu/IntroScript.cs`, no Unity in it): intro.js's async show as a coroutine stepped by
  a clock, with its waits, tweens, easings (easeIO, backOut, bounceOut...), rounding (r1, toFixed) and numbers as they
  are; timers run at their own time and tweens and loops from the next frame, as on the page. What it does to the page
  comes out as state (camera, box, flaps, rooms' holes / halos / panes, night, moon, dawn, morning, Mittens' transform
  and night light, peeks, letters, caption...) and events (sounds, Mittens' moves, bursts, puffs, sparks, the app's
  phases `start` / `awake` / `done`). **Checked** against the real intro by `tools/unity-port/intro-check`: the page
  driven frame by frame on its fake clock with the reference run's taps (`record-intro.mjs`), IntroScript compiled as
  Unity has it and stepped at the same frames: 1,619 frames, 90,839 values, no differences (2 near ties in a rounded
  last digit); the moves the same, each in its frame.
- **Drawing** (`MenuIntro.cs`, `IntroParts.cs`, `IntroLayout.cs`): the pictures `tools/unity-port/export-intro.mjs`
  takes from the game (`Resources/Screens/Intro`, 19 MB for the three weathers) put where `intro.json` says, on the
  A15's pixel grid; the night's veil a gradient mesh (its colours at its stops); the dawn's `mix-blend-mode: multiply`
  a vertex tint on the house's pictures (a multiply at opacity d is a tint of 1 - d + d x colour: exact); the box's
  flaps, the rings, sparkles, puffs and sparks, and intro.js's own rain / snow drawn with Painter2D from intro.js's
  numbers; the box's face, the captions, Skip, TAP TO START, the letters, the peeking creatures (clipped by their
  window) as pictures with intro.js's transforms; CSS transitions (caption .45 s / pop .3 s, the black 1.4 s, TAP TO
  START .6 s) and animations (rings, pulse) by `CssTransition` / `CssAnimation`.
- **Blend modes baked**: the window halos (`screen`) and warm panes (`soft-light`) can't blend in UI Toolkit. Each
  room's halo picture holds the colour screen-blending gives over the house it lies on, with the halo's own alpha
  (solved from the browser's own drawing: the house with and without that halo, and the halo alone); laid on with
  normal blending at the halo's opacity it is exactly what the page shows over that house, for any opacity. Panes the
  same, over the house and the halos. The house under them changes in the dawn: pictures made at five points of it
  (night with every room lit, t = .25, .45, .65, morning), the two either side crossfaded. The veil's holes (the lit
  windows) are the night's house cut to the windows, at the hole's opacity over the veil: exact.
- **Mittens** is the menu's live rig (`LiveCreature`: his moves; the night's light, menu.js `light()`, as a colour
  matrix in the rig's shader, `CssFilter`, matched to Chrome's sRGB filter functions; his touch, `RigTouch`: the rig's
  own handlers (hold 170 ms = squeeze, drag = spin, finger = gaze), then the intro's (the squeeze starts at once).
  When the player's creature isn't Mittens he has his own spot for the intro, and the player's comes back at the end.
- **Sounds** (`IntroSynth.cs`, `IntroSound.cs`): each WebAudio graph intro.js builds (tone, sweep, chime) worked out
  sample by sample into an AudioClip (made ahead, one a frame). **Checked** against Chrome rendering the same voices
  in an OfflineAudioContext (`intro-check/sound-check.mjs`): 34 sounds, 42-124 dB signal / difference, largest sample
  difference 0.001 (including Chrome's quirk of reading 440 Hz in an oscillator's first render quantum).
- **Self-check** (export-intro.mjs: the pieces put back by MenuIntro's rules, in Node, against the browser's frames
  without what Unity draws itself: rain, Mittens, rings, sparkles, flaps; 20 frames, three weathers): at rest (the box,
  Mittens out, every room lit, the peeks) 0.10 – 0.72% of the pixels differ by more than 2/255 and 0.000 – 0.009%
  clearly (> 40/255); a room lighting up 0.96%, 0.053%; the dawn 0.70 – 1.60%, 0.000%; a caption caught mid-pop, the
  logo with TAP TO START mid-pulse 0.74 – 2.64%, 0.13 – 0.15% (pictures scaled where the browser redraws the text); the
  camera's drift (scale 1.08) 10.8%, 0.005% (the same: resampled, not redrawn). Frames in
  `webgame/test/out/unity-intro-check/`.
- **References**: `Reference/game/intro-<weather>-<ms>-<moment>.jpg` (13 rainy moments, 3 snowy, 4 sunny, a skip), the
  real intro on the page's fake clock with its CSS transitions held to it, and the reference run's taps (`intro.json`
  `script`). **PlushCrush > Intro > Reference moments...** plays that run in Unity held at each moment (Mittens, the
  rain and the sparkles keep moving: compare the house, the box, the captions, the logo). **PlushCrush > Intro > Play**
  (and per weather, the reference run, Skip) for the rest.

Known differences:
- Halos and panes between the dawn's points and while a room lights up (500 ms): their pictures were made over the
  house at rest (measured: at most 0.05% of the screen clearly off).
- The end: the intro's layers fade as groups in the page (an opacity under 1 also isolates the back layer: its halos
  and panes show as plain colours, which Unity does too); UI Toolkit multiplies each piece's opacity instead (0.7 s).
  If skipped at night the lit windows show the intro's open windows while the veil fades, where the page shows the
  menu's own scene through the veil's holes.
- Peeks are clipped without anti-aliasing (UI Toolkit's stencil clip); scaled pictures (camera, caption pop, pulse,
  letters' squash, the box) are resampled where the browser redraws vectors and text.
- setTimeout(fn, 0) is taken as 1 ms, as the page's recorded timers run it.
- Not wired yet: the app's music (`IntroPhase` "awake") and what App.js holds until "done" (daily spin, streak, level-up
  card): those screens aren't ported. Bursts and the peek order use Unity's random (Math.random in the page).

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
node tools/unity-port/export-level-pass.mjs             # Level Pass pieces + layout.json + its reference screenshots

node tools/unity-port/export-game-park.mjs creatures    # Game Park overlay -> Resources/Screens/GamePark (+ Art/Generated)
node tools/unity-port/export-intro.mjs                  # the game intro -> Resources/Screens/Intro, Reference/game/intro-*.jpg
node tools/unity-port/intro-check/record-intro.mjs      # its timeline check (then: cd tools/unity-port/intro-check &&
                                                        #   dotnet run -c Release -- out/trace-rainy.json)
```
