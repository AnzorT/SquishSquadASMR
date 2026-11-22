// Records a creature drawn by the GENERIC rig (webgame rive-generic.js: the 98 creatures rive-rig.js has no DEFS for)
// frame by frame, for the Unity port (Scripts/Core/Rig/RigGeneric*.cs), the way record-rig.mjs records the hand-made
// ones. The generic rig builds itself from the creature's own front art (export/tripo/<key>/<key>-front.svg) after an
// async fetch: setup() works out the roles of the art's shapes (head, body, ears, arms, feet, face...) and builds the
// rig, then render() draws every frame. setup() isn't ported (yet): everything it computed that render() reads (I.geo,
// I.items, the per-creature kits...) is exported here as DATA, with element references replaced by names.
//
// The test copy (generic-page.mjs; the app's files untouched) changes rive-rig.js as record-rig.mjs does (tick()
// reachable, no frame loop, its random draws seeded and counted, each frame's pose reported) and rive-generic.js in
// test-only ways: a hook right after setup(), before the first render (window.__rgSetup), where every element is named
// and the drawing's element tree and setup()'s data are taken; render()'s own Math.random() (Nimbo's rain) draws from
// the same seeded generator as tick().
//
// Outputs (the same places as record-rig.mjs):
//   - Art/Generated/Resources/Rig/geo-<key>.json: { generic: true, key, G: setup()'s data (I minus the engine's state),
//     RC: the creature's ROUND entry, fns: functions setup() left on I (ported as code), tree: the drawing's element tree
//     as setup() left it (paths keep their d: the art's own; every element named), svg }
//   - Art/Generated/Resources/Rig/rig-states-<key>.json: the moves build() adds for this creature (dance@<key>, PRE
//     entries...), which the shared rig-states.json misses (they're added lazily)
//   - rig/out/trace-<key>.json: per frame the pose and engine outputs (as record-rig.mjs), the drawing's attribute
//     writes (null = removed; 'display' = style.display), its DOM moves (a parent's children, by name, whenever render()
//     reorders them), I.W; 'pre': the ticks before frame 0 (mount runs one before the art has loaded: no drawing)
//
//   node webgame/build.mjs --out webgame/test/out/webgame
//   node tools/unity-port/rig/record-generic.mjs bao [--seconds 40] [--draw-frames 2400] [--shots 1,700,...]
//     [--more dance,eat_apple,... [--more-each 6]]: more moves after the 40 s, each for 6 s
//     [--coverage <file>]: V8 block coverage of rive-generic.js over the run (what render() runs for this creature)
import fs from 'node:fs';
import path from 'node:path';
import { REPO, BUILT, needBuild, launch } from '../lib/menu-page.mjs';
import { pathToFileURL } from 'node:url';
import { makeTestCopy, rgLib, RR, SEED } from './generic-page.mjs';

const args = process.argv.slice(2);
const opt = (k, d) => args.includes(k) ? args[args.indexOf(k) + 1] : d;
const key = args.find((a, i) => !a.startsWith('--') && !(i > 0 && args[i - 1].startsWith('--'))) || 'bao';
const SECONDS = +opt('--seconds', 40);
const TEST = path.join(REPO, 'webgame/test/out/gentest-' + key);   // one test copy per creature (record-rig.mjs uses rigtest-<key>)
const OUT_DATA = opt('--out-data', path.join(REPO, 'unity/Assets/_Project/Art/Generated/Resources/Rig'));
const OUT_TRACE = opt('--out-trace', path.join(REPO, 'tools/unity-port/rig/out'));
const COVERAGE = opt('--coverage', null);
needBuild();

makeTestCopy(TEST);

const SHOTS = args.includes('--shots') ? opt('--shots').split(',').map(Number).sort((a, b) => a - b) : [];
const DSF = 2.8125, HOST = { x: 128, y: 80, s: 169 }, CLIP = { x: 48, y: 0, width: 352, height: 336 };
const browser = await launch();
const ctx = await browser.newContext({ viewport: { width: 832, height: 384 }, deviceScaleFactor: SHOTS.length ? DSF : 1 });
const page = await ctx.newPage();
await page.route(/^(https?|wss?):/, r => r.abort());
await page.addInitScript(SEED);
await page.addInitScript(RR);
await page.addInitScript(rgLib);
await page.addInitScript('window.__noLoop = true;');
await page.clock.install({ time: new Date('2026-11-13T12:00:00Z') });
await page.clock.pauseAt(new Date('2026-11-13T12:00:00Z'));
let cdp = null;
if (COVERAGE) { cdp = await ctx.newCDPSession(page); await cdp.send('Profiler.enable'); await cdp.send('Debugger.enable'); }
await page.goto(pathToFileURL(path.join(TEST, 'App.dc.html')).href);
await page.waitForTimeout(8000);
for (let t = 0; t < 3000; t += 500) { await page.clock.runFor(500); await page.waitForTimeout(80); }
if (!(await page.evaluate(() => !!window.RiveGeneric && !!window.RiveRig))) throw new Error('the page has no RiveGeneric / RiveRig');
if (await page.evaluate(k => !!window.RiveRig.DEFS[k], key)) throw new Error(key + ' is a hand-made rig (rive-rig.js DEFS): use record-rig.mjs');

// the moves as they are before this creature is built (build() adds its own: rig-states-<key>.json has those)
await page.evaluate(() => { const R = window.RiveRig; window.__st0 = new Set(Object.keys(R.STATES)); window.__pre0 = new Set(Object.keys(R.PRE || {})); });
if (cdp) await cdp.send('Profiler.startPreciseCoverage', { callCount: true, detailed: true });

// ---- mount, wait for the art, name everything, export setup()'s data, drive 60 frames a second ----
const FPS = 60;
const SCRIPT = [[600, 'play:hop'], [1100, 'play:look'], [1600, 'press'], [1750, 'release'], [1900, 'play:happy'], [2300, 'drag:40'], [2330, 'drag:120'], [2360, 'dragEnd']];
// --more dance,eat_apple,...: more moves after the 40 s (each --more-each seconds, default 6), to see (and check) more of
// render() than the standard script reaches
const MORE = opt('--more', '') ? opt('--more').split(',') : [], EACH = +opt('--more-each', 6);
MORE.forEach((m, i) => SCRIPT.push([SECONDS * FPS + 1 + i * EACH * FPS, 'play:' + m]));
const N = (SECONDS + MORE.length * EACH) * FPS;
const DRAWN = +opt('--draw-frames', 600);
await page.evaluate(({ key, FPS, SCRIPT, DRAWN, HOST, SHOT }) => {
  const host = document.createElement('div'); host.id = 'rigHost'; host.style.cssText = `position:fixed;left:${HOST.x}px;top:${HOST.y}px;width:${HOST.s}px;height:${HOST.s}px;z-index:2147483647;`; document.body.appendChild(host);
  if (SHOT) { const st = document.createElement('style'); st.textContent = 'html,body{background:transparent!important} body>*:not(#rigHost){visibility:hidden!important}'; document.head.appendChild(st); }
  let NOW = performance.now(); performance.now = () => NOW;
  const t0 = NOW, frames = [], pre = [];
  let seed = 0x1f2e3d4c, draws = 0;
  window.__rr = () => { draws++; seed ^= seed << 13; seed >>>= 0; seed ^= seed >>> 17; seed ^= seed << 5; seed >>>= 0; return seed / 4294967296; };
  let cur = null, I = null;
  window.__rigP = (I2, P) => { if (!I || I2 === I) cur = Object.assign({}, P); };

  // every attribute written to the drawing (null: removed), per frame
  const writes = new Map(); let rec = false;
  const inHost = e => host.contains(e);
  const sa = Element.prototype.setAttribute, ra = Element.prototype.removeAttribute;
  Element.prototype.setAttribute = function (a, v) { if (rec && inHost(this)) { let m = writes.get(this); if (!m) writes.set(this, m = {}); m[a] = String(v); } return sa.call(this, a, v); };
  Element.prototype.removeAttribute = function (a) { if (rec && inHost(this) && this.hasAttribute(a)) { let m = writes.get(this); if (!m) writes.set(this, m = {}); m[a] = null; } return ra.call(this, a); };
  // the DOM moves render() makes (its depth sorts append / insert its parts): the parents whose children changed
  const dirty = new Set();
  const hook = (proto, name, parentOf) => { const f = proto[name]; proto[name] = function (...a) { if (rec) parentOf(this, a).forEach(p => { if (p && inHost(p)) dirty.add(p); }); return f.apply(this, a); }; };
  hook(Node.prototype, 'appendChild', (t, a) => [t, a[0] && a[0].parentNode]);
  hook(Node.prototype, 'insertBefore', (t, a) => [t, a[0] && a[0].parentNode]);
  hook(Node.prototype, 'replaceChild', (t, a) => [t, a[0] && a[0].parentNode]);
  hook(Node.prototype, 'removeChild', t => [t]);
  for (const n of ['before', 'after', 'replaceWith']) hook(Element.prototype, n, (t, a) => [t.parentNode].concat(a.map(x => x && x.parentNode)));
  for (const n of ['append', 'prepend']) hook(Element.prototype, n, (t, a) => [t].concat(a.map(x => x && x.parentNode)));
  hook(Element.prototype, 'remove', t => [t.parentNode]);

  // names: the generic rig's own (I.E, I.pg), the art's shapes by their place in I.items ('it.<i>'), anything else
  // setup() keeps on I by its path there ('bun.pl.3.el', 'tubes.0', 'faceG'), defs by their id (the mount's suffix
  // dropped: '#g', '#bkn'), the rest by their place in a named parent ('#g/stop.1', 'art/g.2/path.0'). Taken once,
  // after setup() and before the first render (render() moves things round; the names stay with the elements).
  // names, setup()'s data, the element tree (generic-page.mjs: rgLib)
  const { names, ENGINE, nameAll, nameOf, ser, tree } = window.__rgLib(host);

  // the parents' children as last reported (the trace has a parent's list whenever it changes)
  const lastKids = new Map(), lastDisp = new Map();
  const kidsOf = p => [...p.children].map(nameOf);
  const doms = () => { const o = {}; dirty.forEach(p => { const k = kidsOf(p), s = k.join('|'); if (lastKids.get(p) !== s) { lastKids.set(p, s); o[nameOf(p)] = k; } }); dirty.clear(); return o; };
  // style.display (render() hides and shows things that way): reported with the attributes, as 'display'
  const disp = () => { names.forEach((n, e) => { if (!e.style) return; const d = e.style.display || ''; const l = lastDisp.get(e); if (l !== undefined && l !== d) { let m = writes.get(e); if (!m) writes.set(e, m = {}); m.display = d; } lastDisp.set(e, d); }); };
  const drawn = () => { disp(); const o = {}; writes.forEach((m, e) => { o[nameOf(e)] = m; }); writes.clear(); return o; };

  let geo = null;
  window.__rgSetup = (I2, extra) => {
    if (I2 !== I && I) return;
    nameAll(I2);
    const { data, fns } = ser(I2, (o, k) => o === I2 && ENGINE.has(k));
    geo = { generic: true, key, u: I2.u, G: data, fns, RC: extra.RC, pet: extra.pet, hero: extra.hero, tree: tree(host.querySelector('svg')),
      svg: host.innerHTML.replace(/data:image\/png;base64,[A-Za-z0-9+/=]+/g, 'DATA') };
    names.forEach((n, e) => { if (e.parentNode) lastKids.set(e, kidsOf(e).join('|')); if (e.style) lastDisp.set(e, e.style.display || ''); });
    writes.clear(); dirty.clear(); rec = true;   // from here on: frame 0's drawing (setup() draws it at once: RiveRig.tick1)
  };
  const c = window.RiveRig.mount(host, key, {}); I = c._I || window.RiveRig._last._I;
  if (!I) { const all = window.__rgI; I = all && all[key]; }
  window.__R = { c, I: () => I, t0, ready: () => !!(I && I.ready) };
  const keep = I => ({ rr: draws, hs: I.hs, ed: I.ed, ld: I.ld, spTw: I.spTw, spF: I.spF, ms: I.ms, dent: I.dent, state: I.cur.name, spinOff: I.spinOff, W: I.W || null });
  pre.push({ now: 0, P: cur, ...keep(I) });   // mount's own tick (the art not loaded yet: nothing drawn)
  cur = null;
  window.__R.frame0 = () => { frames.push({ f: 0, now: 0, P: cur, ...keep(I), draw: drawn(), dom: doms() }); };
  const sc = new Map(SCRIPT.map(([f, w]) => [f, w]));
  let f = 0;
  window.__R.step = to => { for (f++; f <= to; f++) {
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
    const fr = { f, now: now - t0, P: cur, ...keep(I) };
    if (f <= DRAWN) { fr.draw = drawn(); fr.dom = doms(); } else { disp(); writes.clear(); doms(); }
    frames.push(fr);
  } f--; };
  window.__R.done = () => ({ key, t0, fps: FPS, script: SCRIPT, pre, frames, geo });
}, { key, FPS, SCRIPT, DRAWN, HOST, SHOT: SHOTS.length > 0 });

// the art arrives by fetch (a real file read) and setup() may wait for layout (setTimeout, on the page's fake clock)
for (let i = 0; i < 200 && !(await page.evaluate(() => window.__R.ready())); i++) { await page.waitForTimeout(50); if (i % 10 === 9) await page.clock.runFor(100); }
if (!(await page.evaluate(() => window.__R.ready()))) throw new Error(key + ': the generic rig never got ready');
await page.evaluate(() => window.__R.frame0());
const pend = await page.evaluate(() => window.__R.I().cur.t0 - window.__R.t0);
if (pend) console.warn('warning: the clock moved while the art loaded (' + pend + ' ms)');

fs.mkdirSync(OUT_TRACE, { recursive: true });
for (const s of SHOTS) {
  await page.evaluate(to => window.__R.step(to), s);
  await page.screenshot({ path: path.join(OUT_TRACE, `shot-${key}-${s}.png`), clip: CLIP, omitBackground: true });
}
await page.evaluate(to => window.__R.step(to), N);
const trace = await page.evaluate(() => window.__R.done());
trace.shots = SHOTS.length ? { frames: SHOTS, dsf: DSF, host: HOST, clip: CLIP } : null;

// this creature's own moves ('<move>@<key>': the ones build() and the house add when it's mounted, which the shared
// rig-states.json misses, and the rest, as they are now), functions left out (listed)
const extra = await page.evaluate(key => {
  const R = window.RiveRig, fns = [];
  const clean = (o, at) => JSON.parse(JSON.stringify(o, (k, v) => { if (typeof v === 'function') { fns.push(at + '.' + k); return undefined; } return v; }));
  const st = {}, pre = {};
  Object.keys(R.STATES).forEach(k => { if (!window.__st0.has(k) || k.endsWith('@' + key)) st[k] = clean(R.STATES[k], 'STATES.' + k); });
  Object.keys(R.PRE || {}).forEach(k => { if (!window.__pre0.has(k)) pre[k] = clean(R.PRE[k], 'PRE.' + k); });
  return { STATES: st, PRE: pre, added: Object.keys(R.STATES).filter(k => !window.__st0.has(k)), functions: fns };
}, key);

if (cdp) {
  const { result } = await cdp.send('Profiler.takePreciseCoverage');
  const sc = result.find(s => /\/rive-generic\.js(\?|$)/.test(s.url));
  fs.writeFileSync(COVERAGE, JSON.stringify({ key, url: sc && sc.url, functions: sc ? sc.functions : [] }));
  console.log('coverage: ' + COVERAGE);
}

const geo = trace.geo; delete trace.geo;
if (!geo) throw new Error('setup() never reached the export hook');
fs.mkdirSync(OUT_DATA, { recursive: true });
fs.writeFileSync(path.join(OUT_DATA, `geo-${key}.json`), JSON.stringify(geo));
fs.writeFileSync(path.join(OUT_DATA, `rig-states-${key}.json`), JSON.stringify(extra));
fs.writeFileSync(path.join(OUT_TRACE, `trace-${key}.json`), JSON.stringify(trace));
const states = [...new Set(trace.frames.map(f => f.state))];
const unnamed = new Set(); trace.frames.forEach(f => Object.keys(f.draw || {}).forEach(n => { if (n[0] === '?') unnamed.add(n); }));
console.log(`geometry + setup data: ${path.relative(REPO, path.join(OUT_DATA, `geo-${key}.json`))} (${(fs.statSync(path.join(OUT_DATA, `geo-${key}.json`)).size / 1024).toFixed(0)} KB; functions on I: ${geo.fns.length ? geo.fns.join(', ') : 'none'})`);
console.log(`own moves: ${Object.keys(extra.STATES).length} (${extra.added.length} added by the mount: ${extra.added.slice(0, 6).join(', ')}${extra.added.length > 6 ? '…' : ''}); particle presets: ${Object.keys(extra.PRE).join(', ') || 'none'}${extra.functions.length ? '; functions left out: ' + extra.functions.join(', ') : ''}`);
console.log(`trace: ${trace.frames.length} frames of ${key} (+${trace.pre.length} before the art), states seen: ${states.join(', ')}${unnamed.size ? '; unnamed elements drawn: ' + [...unnamed].slice(0, 8).join(', ') : ''}`);
await browser.close();
