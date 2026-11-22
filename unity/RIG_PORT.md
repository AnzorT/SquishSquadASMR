# The creature rig in Unity: port log

The game draws every creature with RiveRig (webgame `rive-rig.js` for the 20 hand-made rigs, `rive-generic.js` for
the other 98). It's being ported to C# **line for line** (the user's choice, 2026-11-22: native code, no JavaScript
engine and no browser, so it's fast on phones and drawn as vector shapes with no texture problems), and every step is
checked number for number against the game itself.

## How it's checked

`tools/unity-port/rig/record-rig.mjs <key>` makes a test copy of the built game where rive-rig.js is changed in
test-only ways (its tick() is reachable, its own frame loop is off, its random draws come from a seeded generator,
each frame's pose and every attribute it writes to the drawing are reported), loads the real menu page (all move
files, the remote-config filter), mounts the creature and drives it 60 frames a second for 40 s with scripted moves
and touches. It writes:

- `Art/Generated/Resources/Rig/rig-states.json`: every creature's moves (the game's own data; 12 MB, not in git)
- `Art/Generated/Resources/Rig/geo-<key>.json`: the creature's definition, the geometry mount() precomputes from it
  (parsed outlines, ear sections, fur rings: data, so not ported), and its drawing's element tree
- `tools/unity-port/rig/out/trace-<key>.json`: per frame, the pose and the drawing's attribute writes

`tools/unity-port/rig/check` (.NET 8; `dotnet run -c Release -- <rig-states.json> <trace.json> [<geo.json>]`) compiles
the Unity rig files exactly as they are and replays the trace: every pose channel, and with the geometry, every
attribute the game wrote to the drawing (path numbers, opacities, stroke widths, ellipses) and the parts' stacking order.
Record with `--draw-frames 2400` to check the drawing over all 40 s (the trace is then ~44 MB, in `rig/out/`, not in git).
`HULL_FRAME=<n>` prints the near-ties in the ear outlines at that frame.

## Steps

| Step | What | Status |
|---|---|---|
| 1 | Pose engine: tick() up to render (moves, blends, fidgets, gaze, blinks, touch, shots, springs, soft body) | **Done**, matches exactly (2401 frames, largest difference 3e-16) |
| 2 | Built-in renderer for Mittens: render() -> every path / attribute of his drawing | **Done**: 2400 frames, 229k attributes, 6.2M numbers exact, stacking order the same every frame (see the ears note) |
| 3 | Unity drawing: the element tree (fills, gradients, clips, the fabric grain) on the GPU; live in the menu | **Done for Mittens**: the draw list, played in software, matches Chrome's screenshots (11 frames: mean 0.7/255, 2% of pixels more than 8/255 off, at outline edges); GPU drawer written, first run in Unity pending |
| 4 | The other 19 built-in rigs (their own branches of render(): web feet, crests, lobe ears, wings, …) | |
| 5 | The generic rig (rive-generic.js, ~835 KB of code) for the other 98 creatures | |
| 6 | Walking gaits (house-walk.js), the light / look / outline layers, particles | |

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
  dp): what's left is anti-aliasing at outline edges, and the sparkles of the happy move (particles: step 6).
- Cost (desktop, one core): tick 0.01 ms, outlines 0.12 ms, draw list 0.10 ms a frame; ~50 commands, ~5,500 vertices.
- To do: the moves file is 12 MB of JSON parsed into objects at start (a worker thread); a compact per-creature export
  would save memory and time on phones.

## Notes

- Quirks of the game are kept (1:1 with what the app does): the soft body's two spring steps are commented out in
  the game (they never move); a quick tap on a creature does nothing (the poke branch is inside a comment).
- The rig's own random draws come through a delegate: the game's Math.random in the app, a seeded generator in tests.
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
