// Records the creature rig (webgame rive-rig.js) frame by frame, so the Unity port of it (Scripts/Core/Rig) can be
// checked number for number. It also exports the rig's data (its moves: STATES, REST, FIDGETS, PRESS, REL, …) as JSON
// for Unity, straight from the running game, so nothing is retyped.
//
// How: a test copy of the built game where rive-rig.js is changed in three test-only ways (the app's files untouched):
//   - its tick() is reachable (window.RiveRig.__tick) and its own frame loop is off, so frames come exactly when asked;
//   - the random numbers tick() draws come from window.__rr(), a seeded generator the C# side has too;
//   - every frame's final pose (what tick() hands render()) is reported to window.__rigP.
// The real menu page is loaded (every move file, the remote-config filter, as in the app), a rig is mounted for a
// creature, and driven: 60 frames a second, with scripted moves and touches. Output: rig/trace-<key>.json.
//
//   node webgame/build.mjs --out webgame/test/out/webgame
//   node tools/unity-port/rig/record-rig.mjs [mittens] [--seconds 40]
import fs from 'node:fs';
import path from 'node:path';
import { REPO, BUILT, needBuild, launch } from '../lib/menu-page.mjs';
import { pathToFileURL } from 'node:url';

const args = process.argv.slice(2);
const key = args.find(a => !a.startsWith('--')) || 'mittens';
const SECONDS = +(args.includes('--seconds') ? args[args.indexOf('--seconds') + 1] : 40);
const TEST = path.join(REPO, 'webgame/test/out/rigtest');
const OUT_DATA = path.join(REPO, 'unity/Assets/_Project/Art/Generated/Resources/Rig'); // 12 MB: every creature's moves (not in git)
const OUT_TRACE = path.join(REPO, 'tools/unity-port/rig/out');
needBuild();

// ---- the test copy: every file linked, rive-rig.js patched ----
fs.rmSync(TEST, { recursive: true, force: true });
fs.mkdirSync(TEST, { recursive: true });
for (const e of fs.readdirSync(BUILT)) if (e !== 'rive-rig.js') fs.symlinkSync(path.join(BUILT, e), path.join(TEST, e));
let src = fs.readFileSync(path.join(BUILT, 'rive-rig.js'), 'utf8');
const must = (s, what) => { if (!src.includes(s)) throw new Error('rive-rig.js changed: ' + what); };
const a = src.indexOf('  function tick(I, now, dt) {'), b = src.indexOf('  function whipFx(I, P) {');
if (a < 0 || b < a) throw new Error('rive-rig.js changed: tick()');
src = src.slice(0, a) + src.slice(a, b).replace(/Math\.random\(\)/g, 'window.__rr()') + src.slice(b);
must('    I.propZ = st.propZ;', 'the render call');
src = src.replace('    I.propZ = st.propZ;', '    if (window.__rigP) window.__rigP(I, P, now, dt);\n    I.propZ = st.propZ;');
must('  const kick = () => {', 'kick');
src = src.replace('  const kick = () => {', '  const kick = () => { if (window.__noLoop) return;');
must('setInterval(() => { const n = performance.now(); if (ALL.size', 'the watchdog');
src = src.replace('setInterval(() => { const n = performance.now(); if (ALL.size', 'setInterval(() => { const n = performance.now(); if (window.__noLoop) return; if (ALL.size');
src = src.replace('window.RiveRig = { v: 2,', 'window.RiveRig = { __tick: (I, now, dt) => tick(I, now, dt), __RELEASE: RELEASE, v: 2,');
fs.writeFileSync(path.join(TEST, 'rive-rig.js'), src);

// the seeded generator, the same as RigRandom.cs (xorshift32, 2^32 divisor)
const RR = `(() => { let s = 0x1f2e3d4c; window.__rr = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; })();`;
const SEED = `(() => { let s = 0x2f6b9a1; Math.random = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; })();`;

const browser = await launch();
const ctx = await browser.newContext({ viewport: { width: 832, height: 384 } });
const page = await ctx.newPage();
await page.route(/^(https?|wss?):/, r => r.abort());
await page.addInitScript(SEED);
await page.addInitScript(RR);
await page.addInitScript('window.__noLoop = true;');
await page.clock.install({ time: new Date('2026-11-13T12:00:00Z') });
await page.clock.pauseAt(new Date('2026-11-13T12:00:00Z'));
await page.goto(pathToFileURL(path.join(TEST, 'App.dc.html')).href);
await page.waitForTimeout(8000);
for (let t = 0; t < 3000; t += 500) { await page.clock.runFor(500); await page.waitForTimeout(80); }

// ---- the rig's data, as the running game has it (functions, if any, are left out: listed so the port knows) ----
const data = await page.evaluate(() => {
  const R = window.RiveRig, fns = [];
  const clean = (o, at) => JSON.parse(JSON.stringify(o, (k, v) => { if (typeof v === 'function') { fns.push(at + '.' + k); return undefined; } return v; }));
  return { STATES: clean(R.STATES, 'STATES'), REST: R.REST, FIDGETS: R.FIDGETS, FIDK: clean(R.FIDK || {}, 'FIDK'), PRESS: clean(R.PRESS || {}, 'PRESS'),
    REL: clean(R.REL || {}, 'REL'), RELEASE: clean(R.__RELEASE, 'RELEASE'), SOFT: [...R.SOFT], HYAW: [...R.HYAW], functions: fns,
    config: window.SquishConfig ? { has: true } : null };
});
fs.mkdirSync(OUT_DATA, { recursive: true });
fs.writeFileSync(path.join(OUT_DATA, 'rig-states.json'), JSON.stringify(data));
console.log(`rig data: ${Object.keys(data.STATES).length} states, ${data.FIDGETS.length} fidgets, ${data.functions.length} functions left out`);

// ---- drive a rig: 60 frames a second, scripted moves and touches ----
const FPS = 60, N = SECONDS * FPS;
// [frame, what]: moves by name, a hold (press) and its release, a sideways drag (spin) and its release
const SCRIPT = [[600, 'play:hop'], [1100, 'play:look'], [1600, 'press'], [1750, 'release'], [1900, 'play:happy'], [2300, 'drag:40'], [2330, 'drag:120'], [2360, 'dragEnd']];
const DRAWN = +(args.includes('--draw-frames') ? args[args.indexOf('--draw-frames') + 1] : 600); // frames whose drawing is kept
const trace = await page.evaluate(({ key, N, FPS, SCRIPT, DRAWN }) => {
  const host = document.createElement('div'); host.style.cssText = 'position:fixed;left:0;top:0;width:169px;height:169px;'; document.body.appendChild(host);
  // the rig reads performance.now() itself (mount, play): it answers the frame's exact time
  let NOW = performance.now(); performance.now = () => NOW;
  const t0 = NOW, frames = [];
  // how many of the rig's own random numbers have been drawn (the C# check compares the count)
  // (from the seed again here: the menu's own creature, mounted with the page, has drawn from it already)
  let seed = 0x1f2e3d4c, draws = 0;
  window.__rr = () => { draws++; seed ^= seed << 13; seed >>>= 0; seed ^= seed >>> 17; seed ^= seed << 5; seed >>>= 0; return seed / 4294967296; };
  let cur = null, I = null;
  window.__rigP = (I2, P, now, dt) => { if (!I || I2 === I) cur = Object.assign({}, P); };
  // every attribute the rig writes to its drawing, per frame (the drawing's check), with each element named by its
  // role in the rig (I.E's keys: body, head, earL, cr.segs.3 …) or else its place in the drawing (svg/g.1/path.4)
  const writes = new Map(); let rec = false;
  const sa = Element.prototype.setAttribute;
  Element.prototype.setAttribute = function (a, v) { if (rec && host.contains(this)) { let m = writes.get(this); if (!m) writes.set(this, m = {}); m[a] = String(v); } return sa.call(this, a, v); };
  const names = new Map();
  const nameAll = I => {
    const walk = (o, at) => { if (!o) return; if (o instanceof Element) { if (!names.has(o)) names.set(o, at); return; }
      if (Array.isArray(o)) { o.forEach((v, i) => walk(v, at + '.' + i)); return; } if (typeof o === 'object') for (const k in o) walk(o[k], at ? at + '.' + k : k); };
    walk(I.E, ''); walk(I.pg, 'pg'); if (I.shadow) names.set(I.shadow, 'shadow'); if (I.root) names.set(I.root, 'root');
  };
  const domPath = e => { const p = []; while (e && e !== host) { const par = e.parentNode; p.unshift(e.tagName + '.' + (par ? [...par.children].indexOf(e) : 0)); e = par; } return p.join('/'); };
  const nameOf = e => names.get(e) || domPath(e);
  const drawn = () => { const o = {}; writes.forEach((m, e) => { o[nameOf(e)] = m; }); writes.clear(); return o; };
  rec = true;
  const c = window.RiveRig.mount(host, key, {}); I = c._I;   // (mounting runs one frame: dt 0.016 at t0)
  nameAll(I);
  const sc = new Map(SCRIPT.map(([f, w]) => [f, w]));
  const keep = I => ({ rr: draws, order: I.order, hs: I.hs, ed: I.ed, ld: I.ld, spTw: I.spTw, spF: I.spF, ms: I.ms, dent: I.dent, state: I.cur.name, spinOff: I.spinOff });
  frames.push({ f: 0, now: 0, P: cur, ...keep(I), draw: drawn() });
  // the creature's rig as mounted (data for the C# renderer: its definition D, the geometry mount() precomputes from it
  // (I.RG: parsed outlines, ear sections, fur rings…), a few derived numbers, and the drawing's element tree)
  const geo = JSON.parse(JSON.stringify({ D: I.D, RG: I.RG, hkx: I.hkx == null ? null : I.hkx, generic: !!I.generic,
    furH: I.furH || null, furB: I.furB || null, svg: host.innerHTML }, (k, v) => typeof v === 'function' ? undefined : v));
  for (let f = 1; f <= N; f++) {
    const now = t0 + f * 1000 / FPS, dt = 1 / FPS;
    NOW = now;
    const w = sc.get(f);
    if (w) {
      if (w.startsWith('play:')) c.play(w.slice(5));
      else if (w === 'press') { if (I.cur.name !== 'idle') c.play('idle'); I.down = { cx: 100, x: 50, y: 60 }; I.drag = false; I.press = true; I.pressT = now; }
      else if (w === 'release') { I.press = false; const RS = (I.key && window.RiveRig.REL[I.key]) || window.RiveRig.__RELEASE; I.shots.push({ S: RS, t0: now }); I.down = null; }
      else if (w.startsWith('drag:')) { if (!I.drag) { I.down = { cx: 100, x: 50, y: 60 }; I.drag = true; I.spinTw = null; I.spin0 = I.spinOff; } I.spinOff = I.spin0 + (+w.slice(5)) / 169 * 360; }
      else if (w === 'dragEnd') { I.spinTw = { from: I.spinOff, to: Math.round(I.spinOff / 360) * 360, t0: now, d: 650 }; I.down = null; I.drag = false; }
    }
    cur = null;
    window.RiveRig.__tick(I, now, dt);
    frames.push({ f, now: now - t0, P: cur, ...keep(I), draw: f <= DRAWN ? drawn() : (writes.clear(), undefined) });
  }
  return { key, t0, fps: FPS, script: SCRIPT, geo, frames };
}, { key, N, FPS, SCRIPT, DRAWN });
fs.writeFileSync(path.join(OUT_DATA, `geo-${key}.json`), JSON.stringify(trace.geo));
console.log(`geometry: ${path.relative(REPO, path.join(OUT_DATA, `geo-${key}.json`))}`);
delete trace.geo;
fs.mkdirSync(OUT_TRACE, { recursive: true });
fs.writeFileSync(path.join(OUT_TRACE, `trace-${key}.json`), JSON.stringify(trace));
const states = [...new Set(trace.frames.map(f => f.state))];
console.log(`trace: ${trace.frames.length} frames of ${key}, states seen: ${states.join(', ')}`);
await browser.close();
