# Squish Hop: the cushion-hopping mini-game

The fourth mini-game. It began as a web prototype made in a cloud session (`prototypes/squish-hop/`, branch `claude/thin-ice-game-repo-i9424j`, merged into develop on 2026-11-07). The user then asked: "take the idea and implement it on our app". The prototype's README has the design history: the rooms, the twists, and how the levels were made.

## Playing
- **Hop:** the squishy hops one cushion at a time. Swipe (a long drag can turn corners: one hop per half a tile of drag), or tap a cushion next to it. A quick flick that ends before a hop's worth of drag still hops once.
- **Puff:** the cushion it hops off puffs away, leaving a dashed dent. It can never go back there.
- **The ★ cushion** ends the level. **Since 2026-11-16 (the user) it opens only once every other cushion is cleared**,
  so every win is a full clear. Until then it's grey with how many are left (the LEFT pill says the same); hopping at
  it bumps with "Clear every other cushion first. Then the ★ opens!". A pile counts until both its layers are gone.
- **Stuck:** with nowhere left to hop, the STUCK card comes up with UNDO and RETRY.
- **The hopper** is the Lobby's squishy (if it has a cut-out). The friends are other squishies, the player's own first. A level always has the same friends, like its board (`roster.js`).

## The twists, room by room (100 levels)
| Levels | Room | New twist |
|---|---|---|
| 1-14 | Living Room | friends (8): pick them all up (they sit on cushions, so clearing every cushion picks them up) |
| 15-30 | Kitchen | arrows (15): you can only hop off an arrow cushion the way it points |
| 31-46 | Bathroom | bubbles (31): land on one and it bounces you one further |
| 47-62 | Bedroom | piles (47): two cushions, two hops; friends in order (55): 1, 2, 3… |
| 63-80 | Yard | everything mixed, bigger boards |
| 81-100 | Dance Room | beat tiles (81): lit every third hop, landed on only when lit |

The first time a level brings a twist in, a banner says so (until that level has a result, like Squish Stack's).

## Stars, hint, rewards
- **Stars** (since 2026-11-16, the user's pick once every win became a full clear): **3 with no UNDO or RESTART, 2 with
  up to 3, 1 with more.** UNDO, RESTART (the button or the pause card's, once a hop is made) and RETRY after getting
  stuck each count as one take-back; NEXT and REPLAY start the count again. The result card says "n TAKE-BACKS".
  (Before: 3 for a full clear, 2 for up to 3 cushions left, 1 otherwise.)
- **The meter** shows the stars still possible: off the full clear it's one fewer, since an UNDO is needed. Levels 5-100 have exactly one full clear, so this is exact: you're on it or you're not. Levels 1-4 ask the solver after every hop. Off it, the tip says "You can't clear every cushion from here. UNDO and try another way!".
- **Hint:** Squish Stack's rules. Every hint costs a rewarded ad (free with Remove Ads). The first one in a level asks first, and a hinted level gives one star less.
  - It shows the next cushion of the full clear, glowing gold.
  - If you've already left the full clear, it first takes you back to the last spot that was still on it ("Went back 2 hops").
- **UNDO** takes back one hop. **RESTART** plays the same board from the start (a taken hint stays taken).
- **Coins:** (8 + 0.6 per cushion + 4 per twist + 2 per friend) × stars (1 / 1.5 / 2) × tier (+15% each). That's 13 / 20 / 27 on level 1, 134 for 3 stars on level 90, and 198 on level 100.
- **XP:** Squish Sort's and Stack's formula, (10 + ½ × level + 5 × tier) × (0.6 + 0.2 × stars). Half on a replay, through playerXp.

## Every level is fixed
Unlike the other three mini-games, a level is the same board every time: played, replayed or retried. There's no random deal and no hidden skill score moving the tier. `src/hop/levelData.js` holds the 100 boards, made and checked offline by the prototype's `tools/gen.mjs` (unique full clear, effort score). A level's tier (EASY…EXPERT) is how hard its board measured against the others.

## Code (src/hop/)
- `engine.js`: the rules and the solver, an ES-module copy of `prototypes/squish-hop/engine.js` (keep the two the same, but for one rule: here the ★ stays shut until every other cushion is cleared, the prototype's opens once the friends are picked up; the solver finds the same full clears either way).
- `levelData.js`: the boards (written from the prototype's `levels.js`; don't edit by hand).
- `levels.js`: rooms, the time of day through each room, tiers, the map's list, stars, coins, XP, and the profile's `hop` record.
- `board.js`: the board's perspective, worked out in JS: leaned back 38°, seen from 13 tiles away, as the prototype's CSS.
  - It gives where a squishy stands, which cell a tap is on, and the cushions' outlines on screen.
  - Doing it in JS rather than with an RN 3D transform means drawing, standing and touching all use the same numbers. Only the rug uses RN's own `perspective` + `rotateX` (it's decoration).
- `Pieces.js`: the drawing.
  - Cushions and dance tiles as tilted SVG shapes, one view per layer so each can puff away and back.
  - Arrows, bubbles, the ★, the hint glow, and the flat cushion used in the goal card and the picker.
  - The squishies: Stack's cut-outs. Every hop is one native timing (`freshPose`), so nothing steps from JS.
- `Room.js`: the Crib's room layers scaled so the floor line sits about a third of the way down, the floor repeated below, the time-of-day tint; and `RoomCard`, the map's room picture.
- `Game.js`: a level (input, hops, puffs, friends flying to the goal card, undo, hint, stuck, win), and the next ones after it, with the Crib's room-switch flash between rooms.
- `Cards.js`: the result, STUCK, pause, hint and how-to-play cards.
- `roster.js`: the hopper and each level's friends.
- `src/screens/HopScreen.js`: the map (Squish Sort's `LevelMap`, with a band per room and its picture) and the game.
- **Hooked into the app:** the PICK A GAME card (`GamePicker.js` `HopArt`: a squishy hopping along three cushions that puff away), `LobbyScreen` `onOpenHop`, `App.js` stage `hop` (music: cozy).
- **Saved on the profile** (written by the client): `hop.{lv, best, plays, wins, seen}` through `saveHop`. firestore.rules doesn't block `hop`, so there's nothing to deploy.
- **Sounds:** all existing ones. Hop: `st_lift`. Puff: `fs_pop`. Bubble: `tap` (the bubble). Bump: `bonk`. Friend: `sd_squeak`. ★ opens: `sd_unlock`. Stuck: `sd_wrong`. Win: `st_land`, `sd_solve`, `sd_confetti`. Room change: `swoosh`.
- **Tests:** `node src/hop/engine.test.js`.
  - Every level's stored clear replays to a full clear, and the first eight have the number of clears the data says.
  - A tap anywhere in any cell finds that cell, on every board, at three area sizes, and the board fits.
  - The rooms, times of day, stars, coins and XP are right.
  - The solver itself is checked against brute force by `prototypes/squish-hop/tools/check.mjs`.

## Status (2026-11-07)
- Played on the Pixel_8_Pro debug build:
  - the picker card and its animation, the map (room pictures and banners), the how-to-play card on a first visit
  - level 1 (taps and swipes, puffs, the win, ALL CLEAR with +27 coins and +12 XP, level 2 unlocked and saved), NEXT to level 2
  - level 90 (Dance Room: beats, bubbles, piles, arrows), its whole 45-hop clear
  - level 58 (Bedroom: four friends in order, picked up and landing in the goal card in order), its whole clear
  - getting stuck (STUCK card, UNDO from it), the hint through a Google test ad (back 2 hops, the glow, one star less)
  - level 70 (Yard).
- **Fixed from the prototype:**
  - The goal card lists friends in pick-up order, and it grows to fit them.
  - Friends are smaller, and numbered chips are drawn over everything, so a friend behind another can still be read.
  - HOPS counts hops, not cells entered.
- **Not yet checked:**
  - the room-switch flash on NEXT into a new room
  - the friends' flight to the goal card, frame by frame
  - speed on a release build on the A15
- The levels are still the prototype's 100. The 600-levels-per-game plan (fixed, measured difficulty for every mini-game) is next, waiting on the user's decisions.

## Hub, BLITZ MODE and the ★ rule (2026-11-16)

The user, after Squishdoku's hub and Blitz redesign: "I want the same thing to be done with squish stack, squish hop.
also, in squish hop, change the rule so that the block that moves us to the next round is unlocked only after we
visited all the other blocks". They chose a hub **and** a new endless Blitz for Hop, and stars by take-backs.

**The ★ rule** (`engine.js` blockOf and the solver): above, under Playing. The ★ opening has its pop, sparkle and
`sd_unlock` on the hop that clears the last other cushion (it used to be the last friend landing in the goal card).
Tests: the ★ is `locked` with a cushion (or a pile's second layer) left, and random play on all 100 levels never ends
with cushions left.

**The hub** (`src/hop/Hub.js`, Squishdoku's `GameHub`; `HopScreen` now opens on it)
- The title alone, **REGULAR MODE** (the 100-level map, "37/100") and **BLITZ MODE** ("BEST: n") as wood plaques with a
  picture each, tapped as a whole, and HOW TO PLAY with a ?.
- The pictures are the Game Park's Squish Hop clip at its start, as is and in Blitz blue with bolts
  (`tools/games/mode-art.mjs` -> `assets/hop/mode-*.jpg`).
- The map's header keeps SQUISH HOP, the levels done and the ★ pill; its back goes to the hub. The ? moved to the hub.

**BLITZ MODE** (`src/hop/blitz.js`, `Game.js` `blitz`, `levels.js` blitzPrize / afterBlitz)
- The 100 levels are fixed and big (up to 54 hops, most with only one full clear): too much for a short clock, and
  easy to learn by heart. So Blitz deals **fresh small boards**, made on the phone in a couple of milliseconds the
  way `tools/gen.mjs` starts a level: a random walk covering most of a grid (skipped cells are holes), its start the
  squishy's cushion and its end the ★, friends on the walk and arrows pointing the way it leaves.
  - The climb: 3×3, 4×3 ×2, 4×4 ×2, 5×4 ×3, 5×5 ×3, then 6×5 for good. Friends from the 3rd board, arrows from the 6th.
  - Each board's clock: 5 s to look plus 1 s a hop, in whole seconds; past the climb 3% shorter each board, down to
    three quarters.
  - Every board is played in the Dance Room (the party room, like Squishdoku's Blitz).
- **The mistake** is a hop after which no full clear is left: the solver checks after every hop (at most ~30 ms on a
  computer for a 6×5 board). Getting stuck counts too, in case the solver ever gives up. A board's clock running out
  stops the run as well.
- Saving works as in Squishdoku (an ad once a run, free with Remove Ads; or gems 10, 20, 40 … through `blitzSave`).
  After a mistake the hops back to the last spot a full clear was possible from are taken back and the clock runs on;
  after the clock ran out, the board's clock starts again. END RUN is GAME OVER (BOARDS CLEARED).
- Each board cleared pays coins by its grid (3×3: 25, 4×3: 35, 4×4: 45, 5×4: 60, 5×5: 75, 6×5: 95) and a tenth of
  config/levels `xp.blitz.hop` in XP (a new key, 200 by default in `functions/squad.js`: 20 a board). Quitting or
  starting over mid-run pays too.
- No UNDO, RESTART or hints in Blitz. In their place, the glass with "1 MISTAKE = GAME OVER" and the board's clock.
  The strip shows CLEARED, BEST (NEW BEST in gold) and LEFT.
- Saved as `hop.blitz.{top, runs}`. The PICK A GAME card shows "BLITZ BEST n" once there is one.
- **Checked** in the web version's preview with the bundled cut-outs (`?phone=1&preview=1&card=hop-blitz`): the
  countdown with the board hidden until GO, board 1 cleared (the ★ opening at LEFT 0), board 2 with its own clock, a
  wrong hop opening the save card ("That hop left tiles you can't clear."), the free save taking the hop back,
  TIME'S UP without the used ad button, END RUN with +25 coins and +20 XP. Regular level 8: the ★ counting down
  from 19, the meter and tip off the clear, UNDO as a take-back, the result card with 2 stars and "1 TAKE-BACK". The
  gem save wasn't tried (it calls the server). Not checked on a phone.
