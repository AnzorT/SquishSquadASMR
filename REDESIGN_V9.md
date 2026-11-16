# Redesign v9 (2026-11-16)

The user's list from 2026-11-16, with the Desktop zip `Squish-Squad-assets.zip` (exported 16 Nov). The zip holds:

- Three loading screens, each as an MP4 recording and three stills: *Heading home*, *To the treasure vault* and
  *Gathering your squad*.
- 117 creatures in four border looks, as 512 px PNGs:
  - look-1: shading only
  - look-2: thin ink line
  - look-3: bold ink line
  - look-4: bold colour line, the game's default

## The user's answers (AskUserQuestion)

- **Use the zip as it is.** I rebuild the three loading screens from the recordings as live animations. The four
  looks become still pictures, chosen in Firebase, and used wherever the app shows a creature that doesn't move. The
  animated creatures stay as they are.
- **The backgrounds** of the Level Pass, Achievements, Characters and Shop become the wood planks of the Game Park's
  game card: the menu's `--vel` and `--dia` planks.
- ***Gathering your squad*** plays when Characters opens. *Heading home* plays when entering the house. *To the treasure
  vault* plays after buying a chest, before it opens.

## Steps

1. **Crib back button:** the house page's back button becomes the app's wood back orb.
2. **Achievements bottom bar:** the title in the middle, how to unlock it below. No LOCKED or UNLOCKED.
3. **Characters:** the creature stands in the ring, not above it.
4. **Wood plank backgrounds:**
   - In the menu page: Characters and the Level Pass.
   - In the app: Achievements and the Shop.
5. **Level Pass redesign** (menu.js):
   - One row of levels, each one tappable. The tapped level's rewards show in one panel at the bottom.
   - There's no reward tile under every level and no big card for the next reward. This fixes the overlap while
     scrolling.
   - Rewards are drawn as what you get:
     - A chest shows that group's own chest, for example the Pet Shop Common chest.
     - A room shows a picture of the room.
6. **Rewards set in Firebase:** a chest prize in `config/levels` road can name its group:
   `{ k: 'chest', t: 'common', g: 'pet' }`. Without `g` it stays today's first HOT group. This needs a functions
   deploy.
7. **Loading screens:**
   - *Heading home*: menu → house.
   - *To the treasure vault*: buy a chest → the chest smash.
   - *Gathering your squad*: opening Characters.
   Each plays its entry, waits for the real load, then plays its exit.
8. **Creature looks:** `config/roster` gets `look` (1–4; 4 when unset) and an optional `looks: { <creature id>: n }`.
   The still pictures come from the zip's PNGs.

## Status (2026-11-16): all 8 steps built

Checked in the web version only: not on a phone, nothing committed.

1. **House back button:** `.mm-hback` is now the wood orb (CSS in `webgame/app/menu.js`).
2. **Achievements:** the bottom bar has the title in the middle, how to unlock it below, and the progress bar when the
   achievement counts something. No LOCKED or UNLOCKED. (`src/screens/AchievementsScreen.js` WideDetails)
3. **Characters:** the creature stands on the plate (`.sq-c2 .rig` moved down 28 px and made 150 px).
4. **Wood:**
   - In the menu, `WOOD_BG` is the page background (Level Pass, Characters).
   - In the app, `WoodBg` (`src/royal/parts.js`) shows a picture of the same CSS (`webgame/tools/wood-bg.mjs` ->
     `assets/royal/wood-bg.jpg`). Used on Achievements, the Shop and the RN Level Road.
5. **Level Pass** (`M.levels` in menu.js):
   - One row of 46 px level discs. Each is gold once reached, with a ! when it can be claimed, a check when done and a
     lock when not reached. The selected level has a cream ring and a pointer.
   - The selected level's rewards show in one wood panel along the bottom: LEVEL n, its state, CLAIM / CLAIMED /
     "n XP to go", and a card for each reward.
   - A chest card shows its group's own chest (`app/gchests/<g>-<t>-grow.webp`, copied in by `webgame/build.mjs`) and
     name.
   - Rooms and Yard Games show pictures from the house map; the house's next look shows the house
     (`webgame/tools/room-art.mjs` -> `webgame/app/art/rooms/*.jpg`).
6. **Rewards set in Firebase:** a road chest's `g` in `config/levels` (`functions/squad.js` prizeGroup; documented in
   `tools/admin/levels.mjs`; covered by tests).
   - The app sends each chest prize's group and names (`src/game/player.js` roadPrizes).
   - **Deployed 2026-11-16.** The first try failed because Google retired the Node.js 20 runtime on 2026-10-30.
     `functions/package.json` now asks for Node 22 and every function was redeployed on it. That also created the
     not-yet-deployed `gameCreatureWritten`, `gameItemWritten`, `gameAnimationsWritten` and `rebuildGameConfig`.
7. **Loading screens:**
   - *Heading home* (menu.js `headingHome`; three patches in `webgame/patches.mjs`):
     - After the walk to the door, night falls on the real house. Stars and the moon come out, and the open rooms
       light up one by one as the house loads, the door last. Then the camera flies in.
     - The house now counts as loaded when it says so (`state.ready`). Before, it waited for more than 12 pictures,
       and a house with 11 always waited the full 12 s.
   - *Gathering your squad* (menu.js `gatherSquad`, when Characters opens), as in the recording:
     - The deck deals five of your creatures' cards left to right, each turning over as it lands. The deck shrinks
       and is gone once the last card has left it (the user's correction).
     - While still loading, the cards turn back, gather into the deck and the next five are dealt (up to 15).
     - Once the row's pictures in view have loaded, the cards fade away up the table.
   - *To the treasure vault* (`src/components/VaultLoader.js`, art by `webgame/tools/vault-art.mjs`): plays over every
     chest smash until it has painted (ChestSmash `onShown`).
     - It covers the Shop's buys, free, ad, deal and bundle chests, the Deals claims, and the Level Road (and so the
       Level Pass and LEVEL UP chest claims).
     - Deals claims no longer use the falling ChestDrop.
     - Preview: `/?phone=1&preview=1&card=vault`.
8. **Looks:**
   - `tools/looks/encode.py <zip>` writes `android/app/src/main/assets/looks/<n>/<key>.webp` (384 px, about 6 MB for
     all) and `src/squad/lookKeys.js`.
   - `src/squad/looks.js` withLooks: App.js's `creatures` carry the chosen look in `thumb.url` (`asset:/looks/...`;
     on web `/android_asset/looks/...`).
   - The menu's still pictures (character cards, the Level Pass avatar, the loaders) use it through `state.looks`.
   - Set it with `tools/admin/roster.mjs --look n [creatures]`, or `look` / `looks` in `config/roster`. It is 4 when
     unset; 0 means the old pictures.
   - Prickle and Wisecrack aren't in the zip and keep their pictures.

## Round 2 (2026-11-16)

- **The vault room** (the user: "where is the vault room?"; their pick: the place every chest is opened). The chest
  smash page (`tools/chest/host.html`) stands in an underground treasure vault, where *To the treasure vault* flies in:
  - A stone wall with a glowing arch full of gold behind the chest, two wall torches that flicker, a stone floor, and
    gold, gems, goblets and old chests in the corners. It's a picture drawn once by `tools/chest/vault-room.mjs`
    (`tools/chest/vault-room.jpg`), which `tools/chest/build.mjs` copies next to host.html.
  - Two banners on the wall in the chest's group colour.
  - The page's purple (HP bar, hint text, reveal backdrop) is now brown and gold.
- ***Gathering your squad*** now follows the recording: the deck is gone once the last card has been dealt (the user's
  correction). See step 7.
- **Functions deployed** on Node 22 (see step 6).

## Round 3 (2026-11-16): the Squishdoku hub and BLITZ MODE

- **The hub layout**, from the user's marked-up screenshot:
  - The title is at the top in the middle.
  - The two modes are side by side in the middle.
  - HEAD START and HOW TO PLAY are along the bottom.
  - The page is on the wood.
- **The cards in the new design**: wood plaques like the Game Park's game card, with no PLAY or GO buttons. The card is
  tapped.
  - REGULAR MODE has the progress bar and "25/600" on one row.
  - BLITZ MODE shows only "BEST: n".
- **HEAD START** has no creature picture, says what it does plainly, and is for Regular mode only.
- **HOW TO PLAY** has a ? where the X was.
- **BLITZ MODE is endless:**
  - Each board has a short clock of its own.
  - A mistake or the clock running out is GAME OVER, unless the run is saved with an ad (once) or gems (10, 20, 40 …).
  - Each board cleared pays.
  - This needs a functions deploy for the gem save (`blitzSave`).
- Details in SQUISHDOKU.md, "Hub and Blitz redesign (2026-11-16)".

## Round 4 (2026-11-16): the same for Squish Stack and Squish Hop

- The user: "I want the same thing to be done with squish stack, squish hop". Both got the hub in the new design and
  an endless BLITZ MODE with saves, as Squishdoku's Round 3.
  - The hubs share Squishdoku's pieces (`GameHub` in `src/squishdoku/Hub.js`). Each card's picture is its game's Game
    Park clip (`tools/games/mode-art.mjs`).
  - Squish Hop had no hub or Blitz before; its Blitz deals fresh small boards (`src/hop/blitz.js`).
  - The save and GAME OVER cards take a `unit` (puzzle / board) and a line on what went wrong.
- Squish Hop's ★ now opens only once every other cushion is cleared, and its stars count UNDOs and RESTARTs.
- Details in SQUISH_STACK.md and SQUISH_HOP.md, "Hub and BLITZ MODE (2026-11-16)" / "Hub, BLITZ MODE and the ★ rule".
- The gem save needs `blitzSave` deployed (Round 3's functions deploy). The `xp.blitz.hop` default is read by the app
  itself, so it needs no deploy.
- The STUCK card and every mini-game's result card (the stars, or Squishdoku's score creatures, and Blitz's champion
  card) are one column in landscape too, as upright: the title and picture on top, the text, coins, XP and buttons
  under them (the user's marked-up screenshot and pick, 2026-11-16). `Overlay`'s `column`
  (`src/squishdoku/Overlays.js`): set tighter, with the stars and score creatures at 72% and the XP bar and LEVEL n!
  side by side (`useCompact`), so a result with a level-up fits the 384 dp height. The other popups keep two columns.
