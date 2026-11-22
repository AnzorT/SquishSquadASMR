// Records the real intro (webgame/app/intro.js) frame by frame for the timeline check: the scripted run of
// export-intro.mjs (intro.json's script: the box tapped, three squishes, TAP TO START) on the page's fake clock, and
// at every frame the values intro.js has written (the camera, the box, the flaps, the rooms' lights, the night and
// the dawn, Mittens' transform and filter, the peeks, the letters, the caption's classes...), plus Mittens' moves.
//
//   node tools/unity-port/intro-check/record-intro.mjs [weather]   -> tools/unity-port/intro-check/out/trace-<weather>.json
//   cd tools/unity-port/intro-check && dotnet run -c Release -- out/trace-rainy.json
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '../../..');
const BUILT = path.join(REPO, 'webgame/test/out/webgame');
const L = JSON.parse(fs.readFileSync(path.join(REPO, 'unity/Assets/_Project/Resources/Screens/Intro/intro.json'), 'utf8'));
const weather = process.argv[2] || 'rainy';
const T0 = new Date('2026-11-13T12:00:00Z');
const SEED = `(() => { let s = 0x2f6b9a1; Math.random = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; })();`;
const HOOK = `(() => { let SI; Object.defineProperty(window, 'SquadIntro', { configurable: true, get() { return SI; }, set(v) {
    if (v && v.run && !v.__w) { const run = v.run; v = { v: v.v, __w: 1, run(api) { window.__introT0 = performance.now(); window.__moves = [];
      const c = api.ctrl(); if (c) { const p = c.play; c.play = s => { window.__moves.push([performance.now() - window.__introT0, s]); return p.call(c, s); }; }
      return run(api); } }; }
    SI = v; } }); })();`;

const browser = await chromium.launch({ args: ['--allow-file-access-from-files'] });
const ctx = await browser.newContext({ viewport: { width: 832, height: 384 }, deviceScaleFactor: 1 });
const page = await ctx.newPage();
await page.route(/^(https?|wss?):/, r => r.abort());
await page.addInitScript(SEED); await page.addInitScript(HOOK);
await page.addInitScript(`window.__squishOutbox = []; window.__SQUISH_INIT__ = ${JSON.stringify({ intro: true, state: { weather } })};`);
await page.clock.install({ time: T0 }); await page.clock.pauseAt(T0);
await page.goto(pathToFileURL(path.join(BUILT, 'App.dc.html')).href);
await page.waitForTimeout(7000);
// the page's frames: a requestAnimationFrame loop on its clock
await page.evaluate(() => { window.__frames = []; const loop = () => { window.__frames.push(performance.now()); requestAnimationFrame(loop); }; requestAnimationFrame(loop); });
for (let i = 0; i < 2000 && !(await page.evaluate(() => window.__introT0 != null)); i++) await page.clock.runFor(1);
const t0 = await page.evaluate(() => window.__introT0);
// the next frame after the start, then one every 16 ms (checked as it goes)
await page.clock.runFor(16);
let f = await page.evaluate(t0 => window.__frames.filter(x => x >= t0).pop(), t0);
const period = 16;
const now = () => page.evaluate(() => performance.now());

const sample = () => page.evaluate(() => {
  const back = document.querySelector('.si-back'); if (!back) return null;
  const at = (e, a) => e ? e.getAttribute(a) : null, num = v => v == null || v === '' ? null : +v;
  const R = {}; ['living', 'bedroom', 'bathroom', 'kitchen', 'movie', 'dance'].forEach(r => { R[r] = [num(at(back.querySelector('.h-' + r), 'opacity')), num(at(back.querySelector('.g-' + r), 'opacity')), num(at(back.querySelector('.p-' + r), 'opacity'))]; });
  const night = back.querySelector('.si-night'), moon = back.querySelector('.si-moon'), dawn = back.querySelector('.si-dawn'), sb = document.querySelector('.si-sceneb');
  const stage = document.querySelector('.mm-stage'), cap = document.querySelector('.si-cap'), ring = document.querySelector('.si-ring'), rc = ring && ring.querySelector('circle');
  const bxf = document.querySelector('.si-bxf'), bxb = document.querySelector('.si-bxb'), flL = document.querySelector('.si-fl'), inP = document.querySelector('.si-in');
  const tap = document.querySelector('.si-tap');
  return {
    world: document.querySelector('.mm-world').style.transform,
    black: document.querySelector('.si-black') ? document.querySelector('.si-black').style.opacity : null,
    cap: cap ? [cap.textContent, cap.classList.contains('on'), cap.classList.contains('pop'), cap.querySelectorAll('.si-dots i.on').length] : null,
    box: [at(bxf, 'transform'), at(bxf, 'opacity'), bxf ? bxf.style.display : null, at(document.querySelector('.si-bsh'), 'opacity'), at(inP, 'opacity'), at(flL, 'd')],
    hit: (document.querySelector('.si-hit') || { style: {} }).style.display,
    ring: ring ? [at(ring, 'opacity'), at(rc, 'cx'), at(rc, 'cy'), at(rc, 'r')] : null,
    rooms: R, door: num(at(document.querySelector('.mm-scene .mm-doorlight'), 'opacity')),
    night: [night.style.opacity || at(night, 'opacity'), night.style.display], moon: moon.style.opacity || at(moon, 'opacity'), dawn: dawn.style.opacity || at(dawn, 'opacity'),
    sceneB: sb ? sb.style.opacity : null,
    stage: stage ? [stage.style.opacity, stage.style.transform, stage.style.filter] : null,
    peeks: [...document.querySelectorAll('.si-peekl image')].map(im => [num(at(im, 'y')), at(im, 'transform')]),
    letters: [...document.querySelectorAll('.si-logo > div')].map(d => d.style.transform),
    tap: tap ? tap.classList.contains('on') : null, skip: (document.querySelector('.si-skip') || { style: {} }).style.display,
  };
});

const squish = up => page.evaluate(up => { const h = document.querySelector('.mm-stage .mm-hit'), r = h.getBoundingClientRect();
  h.dispatchEvent(new PointerEvent(up ? 'pointerup' : 'pointerdown', { bubbles: true, clientX: r.x + r.width / 2, clientY: r.y + r.height / 2, pointerId: 1, isPrimary: true })); }, up);
const S = L.script;
const inputs = [[S.tapBox, 'tapBox', () => page.evaluate(() => document.querySelector('.si-hit').click())],
  ...S.squish.flatMap(([d, u]) => [[d, 'down', () => squish(false)], [u, 'up', () => squish(true)]]),
  [S.start, 'start', () => page.evaluate(() => document.querySelector('.si-ov').click())]];
const frames = [], done = [];
let next = 0;
while (f - t0 < S.start + 400) {
  // inputs due before the next frame: at their own time
  while (next < inputs.length && inputs[next][0] + t0 < f + period) {
    const [t, kind, act] = inputs[next++];
    const cur = await now(); if (t + t0 > cur) await page.clock.runFor(t + t0 - cur);
    await act(); done.push([t, kind]);
  }
  const cur = await now(); await page.clock.runFor(f + period - cur); f += period;
  const last = await page.evaluate(() => window.__frames[window.__frames.length - 1]);
  if (Math.abs(last - f) > 1e-6) throw new Error(`frame at ${last}, expected ${f}`);
  const v = await sample(); if (!v) break;
  frames.push({ t: +(f - t0).toFixed(6), v });
}
const moves = await page.evaluate(() => window.__moves);
fs.mkdirSync(path.join(HERE, 'out'), { recursive: true });
const out = path.join(HERE, 'out', `trace-${weather}.json`);
fs.writeFileSync(out, JSON.stringify({ weather, t0, period, inputs: done, moves, frames }));
console.log(`${frames.length} frames, ${done.length} inputs, ${moves.length} moves -> ${path.relative(REPO, out)}`);
await browser.close();
