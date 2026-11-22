# The creature rig in Unity: port log

The game draws every creature with RiveRig (webgame `rive-rig.js` for the 20 hand-made rigs, `rive-generic.js` for
the other 98). It's being ported to C# **line for line** (the user's choice, 2026-11-22: native code, no JavaScript
engine and no browser, so it's fast on phones and drawn as vector shapes with no texture problems), and every step is
checked number for number against the game itself.

## How it's checked

`tools/unity-port/rig/record-rig.mjs <key>` makes a test copy of the built game where rive-rig.js is changed in
test-only ways (its tick() is reachable, its own frame loop is off, the random draws of tick() and of the particles
(emit, drawParts) come from a seeded generator, each frame's pose and every attribute it writes to the drawing are
reported), loads the real menu page (all move files, the remote-config filter), mounts the creature and drives it 60
frames a second for 40 s with scripted moves, touches and particles (`emit:<preset>:<where>` steps: every kind). It writes:

- `Art/Generated/Resources/Rig/rig-states.json`: every creature's moves and the particle presets (PRE, with the
  entries other files add; the game's own data; 12 MB, not in git)
- `Art/Generated/Resources/Rig/geo-<key>.json`: the creature's definition, the geometry mount() precomputes from it
  (parsed outlines, ear sections, fur rings: data, so not ported), and its drawing's element tree
- `tools/unity-port/rig/out/trace-<key>.json`: per frame, the pose, the drawing's attribute writes and the fx layer's
  markup (particles and the move's effect)

`tools/unity-port/rig/check` (.NET 8; `dotnet run -c Release -- <rig-states.json> <trace.json> [<geo.json>]`) compiles
the Unity rig files exactly as they are and replays the trace: every pose channel, and with the geometry, every
attribute the game wrote to the drawing (path numbers, opacities, stroke widths, ellipses), the parts' stacking order,
and the fx layer element by element (tags, attributes, numbers to a rounding step) against the game's markup.
Record with `--draw-frames 2400` to check the drawing over all 40 s (the trace is then ~44 MB, in `rig/out/`, not in git).
`HULL_FRAME=<n>` prints the near-ties in the ear outlines at that frame.

## Steps

| Step | What | Status |
|---|---|---|
| 1 | Pose engine: tick() up to render (moves, blends, fidgets, gaze, blinks, touch, shots, springs, soft body) | **Done**, matches exactly (2401 frames, largest difference 3e-16) |
| 2 | Built-in renderer for Mittens: render() -> every path / attribute of his drawing | **Done**: 2400 frames, 229k attributes, 6.2M numbers exact, stacking order the same every frame (see the ears note) |
| 3 | Unity drawing: the element tree (fills, gradients, clips, the fabric grain) on the GPU; live in the menu | **Done for Mittens**: the draw list, played in software, matches Chrome's screenshots (11 frames: mean 0.7/255, 2% of pixels more than 8/255 off, at outline edges); GPU drawer written, first run in Unity pending |
| 4 | The other 19 built-in rigs (their own branches of render(): web feet, crests, lobe ears, wings, …) | |
| 5 | The generic rig (rive-generic.js, ~835 KB of code) for the other 98 creatures | Recorder, survey, plan **done** (below); **Bao** ported (`RigGeneric`): every frame matches (drawing, I.W, particles), pixels as Mittens'; the other 99: to do |
| 6 | Walking gaits (house-walk.js), the light / look / outline layers, particles | Particles **done**: emit / drawParts (`RigParticles`, `RigFx`), all 13 kinds, every frame of the 20 rigs the same as the game's markup; the rest to do |

## The drawing (step 3)

- `RigTree` reads the drawing's element tree the game builds in mount() (exported by the recorder: tags, names, static
  attributes; styles cascade the SVG way), bound to RigRender's elements by name.
- `RigDisplay` walks it each frame in the game's painting order (the parts stacked as render() sorted them) into a
  draw list: Draw (a mesh and a paint), PushClip / PopClip, PushLayer / PopLayer (group opacity, the fabric filter).
  `RigTess` turns outlines into triangles: fills as fans (nonzero rule through the stencil), strokes as quads + joins +
  caps (a union, so nothing blends twice).
- `RigDrawer` (App/Rig) plays the list on the GPU with `Resources/Shaders/RigFill.shader`: stencil fills and nested
  clips, radial / linear gradients on the shape's box (stops interpolated premultiplied, as Chrome), layers in their
  own multisampled textures (8x where the phone has it), the fabric grain multiplied in linear RGB (as Chrome runs
  filters), the result in straight alpha for the UI. `RigView` is the live creature in a VisualElement.
- The check plays the same list in software (`rig/check/RigRaster.cs`: the shader's steps, 4 samples a pixel) and
  compares with Chrome's screenshots of the same frames (`record-rig.mjs --shots 1,450,...`, at the A15's 2.8125 px per
  dp): what's left is anti-aliasing at outline edges (most where particles are: small shapes, thin strokes).
- Cost (desktop, one core): tick 0.01 ms, outlines 0.12 ms, draw list 0.10 ms a frame; ~50 commands, ~5,500 vertices.
- To do: the moves file is 12 MB of JSON parsed into objects at start (a worker thread); a compact per-creature export
  would save memory and time on phones.

## The generic rig (step 5)

### What it is

100 creatures (CREATURES_V2 minus rive-rig.js's DEFS) are drawn by `rive-generic.js` (847 KB). rive-rig.js mounts
them as any rig (same tick(): moves, gaze, touch, springs), but `RiveGeneric.build` fetches the creature's front art
(`export/tripo/<key>/<key>-front.svg`), applies per-creature fixes to it, and then:

- `setup()` (lines 2095-3284, 228 KB) works out the roles of the art's shapes (head, body, feet, arms, cheeks, shine,
  face, top, side...) from their sizes, places and colours, turns them all into paths, and builds the rig: the face
  (eyes, mouth), the paw tubes, and ~40 per-creature kits (a 3D fruit, a bakery cake, a whale's fins, a turtle's
  shell...), each a few elements plus the numbers render() needs;
- `render()` (lines 3285-4898, 302 KB) draws each frame;
- also: 64 dance builders (per-creature dances, added to `STATES` when that creature is built, as `dance@<key>`), the house's moves
  (`<move>@<key>`, added at mount), the ROUND / PETS / HERO tables, `heroFX` (24 KB, the 6 heroes; render() calls it),
  and the SN3D models in `snack-3d.js` + `fruit-3d.js` (130 KB, 22 snack / fruit creatures: render() calls
  `SN3D.draw`).

### Recording and checking

- `tools/unity-port/rig/record-generic.mjs <key>` records a generic creature as record-rig.mjs does the hand-made ones
  (same test-copy patches, same script with every kind of particle, `--draw-frames`, `--shots`), plus: it waits for the
  async build; names every element at setup time (the rig's own `E.*` / `pg.*`, the art's shapes `it.<i>` by their
  place in I.items, what setup() keeps on I by its path there: `bun.pl.3.el`, `tubes.0`, `faceG`; defs by id: `#g`,
  `#bkn`; the rest by place: `#g/stop.1`); exports **setup()'s result as data** (`geo-<key>.json`: `G` = I minus the
  engine's state, element references as names, shared objects as `$ref`, NaN as `$num`; `RC` = the creature's ROUND
  flags; the drawing tree as setup() left it, paths with the art's own `d`), the creature's own moves
  (`rig-states-<key>.json`: every `<move>@<key>` and particle preset its mount adds, which the shared rig-states.json
  misses), and per frame: the pose, every attribute write (removals as null, `style.display` as `display`), the DOM
  moves render() makes (a parent's children whenever their order changes), `I.W`, the fx layer's markup; `pre`: the
  mount's own tick before the art has loaded. `--more dance,eat_apple,...` adds moves after the 40 s (more of render()
  checked); `--coverage <file>` writes V8 block coverage of rive-generic.js (what render() ran for this creature).
- `tools/unity-port/rig/survey-generic.mjs` runs every generic creature through hop, look, a hold, happy, a spin,
  dance, eat_apple, poke_head, sleep, cry (24 s each, one page, ~8 min) with block coverage taken per creature:
  `out/generic-survey.txt` (the summary below), `out/generic-cov/render-<key>.txt` (render() with the code that
  creature never ran elided: **the porter's to-do list for that creature**), `out/generic-survey.json`.
- The check: a generic geometry (`"generic": true`) is drawn with `RigGeneric` (`check/Generic.cs`): the creature's
  own moves are merged in, the `pre` tick is compared, every recorded frame's attributes (frame 0 too: setup() draws it
  at once), I.W to 1e-9, the particles (they start at I.W), the DOM moves (none ported yet: any is a difference),
  frames that needed a branch not ported (`RigGeneric.Missing`), pixels. `RIG_PERF=1` prints the cost.
- Bao (`ROUND.bun`): 2401 frames, 243,523 attributes, 2,981,589 numbers exact, 0 differences (2 near-ties in the
  knot's outline, a hull round 84 points: curves 0.16 apart, measured as for the ears); I.W 38,416 points, 0
  differences; particles 22,782 fx elements, 0 differences; pixels mean 0.43-0.91/255, 1.1-3.4% more than 8/255 off
  (outline edges, as Mittens). With `--more dance,eat_pepper,cry,stinky,poke_nose,poke_head,surprised,squish,sitdown,
  idle_yawn,eat_apple,poke_sideL,eat_lollipop,sleep` (7441 frames: the dent, squeeze eyes, tongue, the dance mouth,
  paws reaching, sitting, the green tint): drawing and I.W 0 differences (the fx layer differs only in `stinky`'s own
  effect, the flies, which RigFx doesn't draw yet: step 6). Cost: outlines 0.06 ms, draw list 0.14 ms a frame, 38
  commands, 2,600 vertices.

### setup() stays data

setup() isn't ported: it reads the art through the browser (getBBox of every shape, the DOM) and its result depends
only on the art, which is fixed in a build. Its result ships as data, one `geo-<key>.json` per creature (51 KB on
average, 5 MB for all 100; Corny 1.2 MB: 1,123 popcorn elements), regenerated by the recorder when the art or
rive-generic.js changes. What can't be data is the **functions** setup() leaves on I (the survey lists them): the
`mapHand` / `toM` / `fromM` chain (ported: its inputs are on `I.geo`, the wrappers follow `RC` flags), `I.zE` (48
creatures: the body's depth), the kits' profiles (`rT`, `zAt`, `rB`, `dR`...), `bk.M.draw` / `bk.S.*` (the SN3D
models). They're ported as code; **what they close over is often setup()'s local variables, not on I**: the porter
adds a test-only line to the test copy (generic-page.mjs) that stashes those on I (e.g. `I.__zE = { HY0, prof }`) so
they're exported, as data, with the rest.

### The survey (100 creatures)

- render(): 228,556 non-blank characters. Run by >=90% of the creatures: 5.9% (the shared core), by 2-90%: 12.9%
  (shared kits and roles), by exactly one: 77.0% (one creature's own code), by none: 4.1% (moves the survey doesn't
  play, dead branches). Each creature runs 19-37 KB of it (Bao 24.8 KB, of which 1.1 KB his own).
- Plus SN3D (22 creatures, 7-17 KB of snack-3d.js / fruit-3d.js each) and heroFX (6 heroes, 4-6 KB each).
- Elements per creature: 65-1,123 (median 105).

render(), top to bottom (KB; who):

| Lines | What | KB | Who |
|---|---|---|---|
| 3286-3326 | the pose adjusted per creature (eating reach, the heroes' hover, Puff's bloat, the turtle's hide, wings, floats, jelly wobbles, `bk.pre`) | 6.6 | ~25 (kits) |
| 3327-3418 | **core frame**: yaw / twist, `glob` (squash, lean, bend, tilt, dent, motion stretch), `headT`, `sz` / `sph` / `sphZ` (the head sphere), `face`, `zc`, `byw` | 16.2 | all (inside: hooks for Jelli, Octo, Scoop, Bunbun's burger, Mallow's cylinder: 6.9) |
| 3419-4458 | per-creature kits before the shapes: chefbun hat, pn pina, av avo, lm lemmy, kw kiwi, pe peachy, pt pitaya, tu tutti, mo melo, **dl** (Dreamland, 16 creatures), hn hana, gb boo, px pixie, **bk** (bakery, 9 + the 22 SN3D), br berri, bb boba, orb bubbly, star twinkle, dn dunkie, bun bao, BG bunbun, ck choco, wings dusty, **wh** (aurora, finn, sealy, pearl), jl jelli, oc octo, pq pip | 214 | 1-31 each |
| 4459-4553 | the dizzy halo (any creature, channel `dz`: no move of the survey uses it), the turtle (shelly, seaturtle) | 17.3 | 2 |
| 4554-4632 | **the art's shapes by role** (45 roles) | 17.2 | all (roles: below) |
| 4633-4680 | **paws**: `mitArm` (armStr: 85), Pearl's flippers, the plain arms; the paw tubes | 10.9 | 92 |
| 4681-4788 | marina, Ember's fire, lumi's lantern, nimbo's rain, plip's ripples, bubbly's shimmer | 19.9 | 1 each |
| 4789-4829 | **face**: tints, eyes, mouth (+ per-creature mouths), I.W | 7.6 | all |
| 4830-4897 | breeze's kite, feet W, zappi's zaps, heroFX, jinni's lamp, **shadow** | 13.6 | all / 1 each |

Roles of the art's shapes (creatures): head 100, cheek 97, arm 92, foot 76, shine 54, bnop 39 (hidden, a kit redraws
it), xHide 37, body 29, face 28, patch 19, top 17, bsurf 11, wshine 4, side 4, and 31 roles of one or two creatures.
setup() keys render() reads (creatures): rk 100, sky 93, armStr 85, armOut 82, whole / hb 79, zE 48, bk 31, foodK 23,
dl 16, bkH 9, armReach / hx / ash 6, mReach 5, wh / zc0 / petRoles 4, and ~50 kit keys of one to three creatures. With no
kit at all: frybo, gourdy, batty, kernel, hexie, mumbles, hocus, countsquishula.

Ported for Bao: the core frame without the creature hooks, roles head / shine / wsurf / cheek / foot, `mitArm` and
the tubes, the face, I.W, the shadow, the bun (~25 KB of render()).

### The C# structure (for every porter)

- `Core/Rig/RigGeneric.cs`: the core (setup data read into typed fields once, the frame's locals, `Glob` / `HeadT` /
  `Sph` / `SphZ` / `Byw` / `BywZ` / `Zc` as cached delegates, `Render`), `RigGeneric.Parts.cs`: shapes by role,
  paws, face, I.W, shadow; **one partial file per kit** (`RigGeneric.Bun.cs`: its `BunKeys` (setup keys it draws), an
  `InitBun()` reading its data, a `Bun()` called from Render where the game's block is). A new kit adds its keys to
  `Ported`, its init to the constructor and its call to Render, in the game's order.
- `RigGeneric.Unported(geo)` refuses a creature using anything not ported, by name: a truthy setup key (`I.pn`), an
  item role or item key, a ROUND flag that changes mapHand. Branches reached only at run time (the dizzy halo) set
  `Missing` (the check counts those frames). `RIG_PARTIAL=1` draws what's ported.
- Elements: a `RigEl` per named element of the tree (`El(name)`); attributes as RigRender's (`Dp`, `Op`, `Sw`, `Cx`...:
  numbers already rounded the game's way, no strings). Line for line: `r2`, `JsRound`, `Hypot`, JS `||` (`Or0`,
  `Or1`), NaN for missing channels, `(a.rot || 0)`, string truthiness of `q.op`; no per-frame allocations (tuples,
  cached delegates, pooled lists).
- `RigView` shows a generic creature live (`RigView.Generic.cs`: its geometry, its own moves merged, the mount's two
  ticks), e.g. Bao in the menu state L10-snowy-bao.

### What the drawing side needs (the trees setup() leaves, all 100)

Already handled: paths (written, or the art's own `d` until written), ellipses, circles, groups, radial / linear
gradients (bbox or user space, stop-opacity), clip paths (one path), masks, opacity, stroke caps / joins / dashes,
`filter="none"` (every generic art's body group: **no fabric grain** on the generic creatures), Gaussian blur.
To add (creatures):

- **DOM moves**: render() reorders elements (appendChild / insertBefore, 71 call sites: paws round the back, kits'
  front / back pieces, depth sorts). RigGeneric needs the parents' child order as the game's DOM has it (a list of
  RigEls per parent it reorders, moved as the game moves them) and RigDisplay.Children to follow it; the recorder
  already gives each change (`dom`), the check compares them.
- Elements render() **creates** (the dizzy halo, Choco's / the kits' back groups `armB`, new paths in some kits):
  not in the exported tree; the recorder names them only by DOM path (`?svg/g.1/...`). Name them when first seen
  (recorder) and pre-create them in C#.
- `style.display` toggled per frame (7 sites): RigDisplay must read `El.Str["display"]`; attribute removal (4 sites).
- Attributes render() writes as strings: `fill` / `stroke` (17 + 5 sites, some `url(#...)`: RigDisplay.Dyn reads only
  colours), `stop-color` (6) and linear-gradient `x1 / x2 / y1 / y2`, `r` (9, circles), `transform` (3), `offset`.
  RigGradient.Refresh reads cx / cy / r / offset / gradientTransform only; a numeric slot on RigEl for `r` (and the
  gradient ends) keeps them allocation-free.
- `<use>` (shelly, seaturtle, chronoshade), `fill-rule="evenodd"` (chip; RigTess fills nonzero), filters:
  feTurbulence + feDisplacementMap (shelly, seaturtle: the water wobble), feColorMatrix (chronoshade), masks (dunkie,
  choco, zappi), `<rect>` in masks. Each needs RigTree / RigDisplay, the software rasteriser (RigRaster) and the GPU
  drawer + shader, kept in step.

### Splitting the work (porters in parallel)

The core first (one porter, ~1 day), then kits in parallel; each porter records his creatures (`record-generic.mjs
<key> --draw-frames 2400 --shots ...`, plus `--more` for the creature's own moves: its dance), ports from
`out/generic-cov/render-<key>.txt`, and keeps Bao and the 20 hand-made rigs passing.

1. **Core** (shared by most): the remaining roles of the shapes loop (body, face, top, side, patch, tail, bsurf, bnop,
   earP / inner / pad for the pets...), the plain arms, the creature hooks inside the core frame (Jelli, Octo, Scoop,
   burger, cylinder: or leave them to their kits), `I.zE`, the dizzy halo, DOM moves + created elements + display /
   removal + string attributes (with the drawing porter). Creatures done with it alone: the 8 with no kit (frybo,
   gourdy, batty, kernel, hexie, mumbles, hocus, countsquishula), then nimbo (+ rain), plip (+ ripples)...
2. **Fruit** (render 58 KB + their setup functions): pn pina, av avo, lm lemmy, kw kiwi, pe peachy, pt pitaya, tu
   tutti, mo melo, br berri: 9 creatures, each its own kit (similar code: a turned profile `rT`).
3. **Dreamland** (`dl`, 28 KB + Ember's fire 5 KB + jinni's lamp 9 KB + the pose hooks): jinni, ember, drako, floof,
   unibun, gumdrop, wisecrack, leo, kiko, teddy, panko, hoot, fawn, acorn, moss, sprout: 16 creatures.
4. **Bakery + snacks** (`bk` 33 KB, + SN3D: snack-3d.js 100 KB / fruit-3d.js 30 KB): cuppy, chip, twisty, crois,
   maca, puddi, mochi, cakey, sugarplum + the 22 SN3D creatures (nori, corny, noodle, khinki, razzy...). The biggest;
   two porters (the bk block; the SN3D models).
5. **Ocean** (wh 14 KB + Pearl's flippers, tt 17 KB, oc 8 KB, jl 5 KB, pq 4 KB, mm 11 KB, Puff's bloat): aurora,
   finn, sealy, pearl, shelly, seaturtle, octo, jelli, pip, marina, puff: 11 creatures.
6. **The rest** (each small): hana, boo, pixie, twinkle (star), choco, bunbun (burger), boba, dunkie, chefbun, bubbly,
   breeze, zappi, lumi, dusty, scoop, mallow, bao (done), + the 6 heroes (heroFX 24 KB outside render()).
7. **Drawing** (one porter, alongside 1): the features above, in RigTree / RigDisplay / RigTess, RigRaster and
   RigDrawer + RigFill.shader.

### Risks

- Hulls round many points (Bao's knot: 84) keep near-tie points by the last bit of sin / pow, as the ears do:
  measured as curves (limit 0.4), not numbers.
- Functions setup() leaves close over its locals (above): each kit needs a stash line in the test copy; forgetting one
  shows as wrong numbers, not as a refusal.
- 77% of render() is one creature's own code: coverage of a 40 s script is partial (a kit's dance channels, eat
  moves). Record each creature with `--more` its own moves (its dance, eats, pokes) and read its coverage file.
- The mount: in the game the art loads asynchronously and the rig ticks without drawing meanwhile (the recorder
  freezes time: one tick); Unity has the data at once and ticks twice at the mount (mount + setup's tick1), as the
  recording.
- render()'s own Math.random (Nimbo's rain) goes through the rig's random delegate (the recorder seeds it).
- Per-creature data in Unity: `geo-<key>.json` + `rig-states-<key>.json` (Bao's 1 MB: 152 moves, mostly the house's,
  parsed when he's first shown) per creature in Resources; a compact export would help phones.
- Moves added at build by other files (house moves, eats) carry functions (`prop`, `fx`): not data; props and the
  move effects are step 6.

## Notes

- Quirks of the game are kept (1:1 with what the app does): the soft body's two spring steps are commented out in
  the game (they never move); a quick tap on a creature does nothing (the poke branch is inside a comment).
- The rig's own random draws come through a delegate: the game's Math.random in the app, a seeded generator in tests.
- Particles (rive-rig.js PRE, emit, drawParts): a move fires them (an `emit` event, an emit window, a let-go
  reaction's `em`); each starts where the rig drew that part the frame before (I.W: RigRender.W, handed to the
  engine as `RigEngine.W`). They live in the engine (pooled) and move at the end of Tick (the game moves them in
  drawParts, after render, which draws no random numbers, so the order of draws is the same). `RigFx` turns them into
  the fx layer's shapes (paths, circles, ellipses, groups with a transform and opacity; the dust clouds use the tree's
  `phz` gradient), before the move's own effect; `RigDisplay` draws them like the tree (a group's opacity on more than
  one paint is a layer).
- The renderer (RigRender) writes paths as commands plus numbers already rounded the game's way (r2, JS Math.round),
  not as strings, so a phone isn't making garbage every frame; RigPath.ToString() prints the game's string form for checks.
- Math.hypot is V8's algorithm (scaled, Kahan-summed), so lengths match to the bit.
- The ears' outlines are convex hulls round ~170 points on the ear's surface, and many pairs of those points lie a hair
  (1e-15) apart. Which one of a pair the hull keeps depends on the last bit of sin / pow, and that differs between maths
  libraries (Chrome's and .NET's, and in the game itself between phones' WebViews). In 118 of 4800 ear outlines the
  C# keeps a different one than this Chrome did; the drawn curves are then at most 0.22 rig units apart (about one
  phone pixel, at the ear's root, for a frame). The check measures those curves' distance (limit 0.25) instead of
  comparing numbers.
- Rigs using a branch of render() not ported yet are refused by RigRender's constructor with the feature's name
  (RigRender.Unported), so nothing is drawn wrong by accident.
