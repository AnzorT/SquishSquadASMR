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

`tools/unity-port/rig/check` (.NET 8; `dotnet run -c Release -- <rig-states.json> <trace.json>`) compiles the Unity
rig files exactly as they are and replays the trace.

## Steps

| Step | What | Status |
|---|---|---|
| 1 | Pose engine: tick() up to render (moves, blends, fidgets, gaze, blinks, touch, shots, springs, soft body) | **Done**, matches exactly (2401 frames, largest difference 3e-16) |
| 2 | Built-in renderer for Mittens: render() -> every path / attribute of his drawing | In progress |
| 3 | Unity drawing: the element tree (fills, gradients, clips, masks, the fabric texture, outline) as vector meshes | |
| 4 | The other 19 built-in rigs (their own branches of render(): web feet, crests, lobe ears, wings, …) | |
| 5 | The generic rig (rive-generic.js, ~835 KB of code) for the other 98 creatures | |
| 6 | Walking gaits (house-walk.js), the light / look / outline layers, particles | |

## Notes

- Quirks of the game are kept (1:1 with what the app does): the soft body's two spring steps are commented out in
  the game (they never move); a quick tap on a creature does nothing (the poke branch is inside a comment).
- The rig's own random draws come through a delegate: the game's Math.random in the app, a seeded generator in tests.
- Number formatting matters for the drawing check: the game writes coordinates rounded to 2 decimals (r2) as JS
  number strings; the C# formats them the same way ("12.5", "0", never "-0").
