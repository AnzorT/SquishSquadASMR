// The game intro ("Squish wakes the house", webgame/app/intro.js), taken apart for Unity: every piece the browser draws
// saved as a PNG on the Galaxy A15's pixel grid, plus intro.json saying where each goes; reference frames of the real
// intro (frozen clock, scripted taps) for the Reference Overlay; and a self-check that puts the pieces back together by
// Unity's rules (MenuIntro.cs) and compares them with the browser's own frames.
//
//   node webgame/build.mjs --out webgame/test/out/webgame
//   node tools/unity-port/export-intro.mjs                 -> unity/Assets/_Project/Resources/Screens/Intro/
//                                                             unity/Assets/_Project/Reference/game/intro-*.jpg
//   node tools/unity-port/export-intro.mjs --seasons rainy --no-refs     (only some of it)
//
// The pieces:
//   - World (in the scene's own picture grid: the 1000 x 460 drawing at the A15's scale, as scene-L1-rainy.png):
//     per weather, the night's house (level 1, every room's window open, no lock signs, no sun: intro.js's own
//     scene) and the morning's (sunny: with its sun; the others are the same picture); each room's windows cut out of
//     the night's house (the night veil's holes); the door's light; per room, its window halos and its warm panes as
//     they look over the house at night and in the morning. The moon, the box's face and shadow, the spark's glow,
//     each peeking creature.
//   - Overlay (on the A15's screen): the six captions, Skip, TAP TO START, the logo's eleven letters.
//   Not pictures: the night veil and the dawn's colour (gradients Unity draws: the veil as a mesh with the gradient's
//   colours, the dawn as a tint on the house's pictures, which is exactly mix-blend-mode multiply), the box's flaps
//   (their shape moves), the rain and snow, the tap rings, the sparkles and puffs (drawn by Unity from intro.js's
//   numbers).
//
// The blend modes Unity's UI can't do (the halos' screen, the panes' soft-light) are baked: a halo's picture holds,
// for each pixel, the colour screen-blending gives over the house it lies on, with the halo's own alpha, so laid on
// with normal blending at the halo's opacity it gives exactly what the page shows over that house. The house under it
// changes as the night turns to morning: each has a picture made over the house at five points of the dawn
// (DAWN_KEYS: 0 is the night with every room lit, 1 the morning), the two either side crossfaded. The panes the same,
// over the house and halos. And as they are (plain), for the end, when the fading back layer no longer blends.
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { PNG, REPO, BUILT, W, H, K, SCENE_SCALE, needBuild, launch, isolate, showAll, trim } from './lib/menu-page.mjs';
import { Canvas, readPng, diff } from './lib/compose.mjs';

const OUT = path.join(REPO, 'unity/Assets/_Project/Resources/Screens/Intro');
const REF = path.join(REPO, 'unity/Assets/_Project/Reference/game');
const CHECK = path.join(REPO, 'webgame/test/out/unity-intro-check');
const T0 = new Date('2026-11-13T12:00:00Z');
const SEED = `(() => { let s = 0x2f6b9a1; Math.random = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; })();`;
// window.SquadIntro.run wrapped: when it started (performance.now), its api and handle. And the CSS transitions and
// animations held on the page's fake clock: each is paused and set to (now - when it first showed up) at every step,
// so the frames are the same on every run (Playwright's clock moves timers and requestAnimationFrame, not CSS).
const HOOK = `(() => { let SI; Object.defineProperty(window, 'SquadIntro', { configurable: true, get() { return SI; }, set(v) {
    if (v && v.run && !v.__w) { const run = v.run; v = { v: v.v, __w: 1, run(api) { window.__introT0 = performance.now(); window.__introApi = api; const h = run(api); window.__introRun = h; return h; } }; }
    SI = v; } });
  window.__animSync = () => { const now = performance.now(); for (const a of document.getAnimations()) { if (a.__born == null) a.__born = now; try { a.pause(); a.currentTime = now - a.__born; } catch (e) { /* gone */ } } return now - (window.__introT0 || 0); };
})();`;

const ROOMS = ['living', 'bedroom', 'bathroom', 'kitchen', 'movie', 'dance'];
const DK = r => r === 'dance' ? 0.6 : 1;
const all = f => Object.fromEntries(['living', 'bedroom', 'bathroom', 'kitchen', 'movie', 'dance'].map(r => [r, f(r)]));
const fx3 = v => +v.toFixed(3);
const LIT = { h: r => fx3(0.9 * DK(r)), g: () => 0.85, p: r => fx3(0.75 * DK(r)) };           // setLit(r, 1)
// The dawn (intro.js's 2800 ms tween, linear) at t: the night veil, the moon, the dawn's colour, the morning's house
// fading in, the halos and panes dimming (the holes stay as lit)
const DAWN_KEYS = [0, 0.25, 0.45, 0.65, 1];
function dawnState(t) {
  const day = Math.max(0, Math.min(1, (t - 0.45) / 0.5)), L = 1 - 0.7 * day;
  return { h: all(LIT.h), g: all(() => fx3(Math.min(1.4, L * 0.85))), p: all(r => fx3(Math.min(1, L * 0.75) * DK(r))),
    night: fx3(0.8 * (1 - t)), moon: fx3(0.6 * (1 - Math.min(1, t * 2))), dawn: fx3(0.6 * Math.sin(Math.min(1, t * 1.1) * Math.PI)),
    sceneB: t > 0 ? fx3(Math.max(0, Math.min(1, (t - 0.2) / 0.6))) : 0, door: 0 };
}
const PEEK = [['biscuit', 'living', 0], ['berri', 'living', 1], ['nibbles', 'bedroom', 0], ['appy', 'bedroom', 1], ['peachy', 'bathroom', 0], ['scoop', 'bathroom', 1],
  ['truffle', 'kitchen', 0], ['boba', 'movie', 0, 628], ['bao', 'movie', 0, 672]];
const WIN = { living: [['r', 434, 218, 36, 40], ['r', 530, 218, 36, 40]], bedroom: [['a', 298, 164, 42, 48], ['r', 366, 176, 30, 30]], bathroom: [['c', 629, 185, 17], ['r', 668, 164, 46, 44]],
  kitchen: [['r', 282, 246, 62, 36], ['c', 385, 265, 13]], movie: [['r', 604, 242, 92, 40], ['r', 712, 248, 16, 34]], dance: [['d', 500, 170, 46]] };
const box = w => w[0] === 'r' || w[0] === 'a' ? [w[1], w[2], w[3], w[4]] : w[0] === 'c' ? [w[1] - w[3], w[2] - w[3], 2 * w[3], 2 * w[3]] : [w[1] - w[3], w[2] - w[3], 2 * w[3], w[3]];
const CAPTIONS = {
  house: 'A little house is waiting for its squad…',
  box: 'Something is moving in the box… <b>Tap it!</b>',
  squish0: 'Squish Mittens to wake up the house!', squish1: 'The living room is awake! <b>Again!</b>',
  squish2: 'The bedroom too! <b>One more!</b>', squish3: 'The whole house is waking up!',
};
const dots = n => '<span class="si-dots">' + [0, 1, 2].map(i => '<i class="' + (i < n ? 'on' : '') + '"></i>').join('') + '</span>';
const captionHtml = k => CAPTIONS[k] + (k.startsWith('squish') ? dots(+k.slice(6)) : '');

// The scripted run the reference frames come from (and Unity's PlushCrush > Intro > reference moments): intro ms.
const SCRIPT = { tapBox: 6400, squish: [[10400, 10550], [12400, 12550], [14400, 14550]], start: 25500 };
const REFS = {
  rainy: [[2400, 'night'], [6000, 'box'], [7300, 'popped'], [10300, 'squish'], [12000, 'living'], [13450, 'bedroom-lighting'], [15900, 'three-rooms'],
    [17100, 'house-glows'], [18400, 'dawn'], [19300, 'dawn-late'], [21000, 'peeks'], [24000, 'logo'], [25850, 'finish']],
  snowy: [[6000, 'box'], [17100, 'house-glows'], [21000, 'peeks']],
  sunny: [[6000, 'box'], [17100, 'house-glows'], [18400, 'dawn'], [21000, 'peeks']],
};
const SKIP = { at: 12200, shot: 12550 };   // a skip in the night (rainy): its fade

const args = process.argv.slice(2);
const opt = k => { const i = args.indexOf(k); return i >= 0 ? args[i + 1] : null; };
const SEASONS = (opt('--seasons') || 'rainy,snowy,sunny').split(',');
const NO_REFS = args.includes('--no-refs'), NO_PIECES = args.includes('--no-pieces');

needBuild();
fs.mkdirSync(OUT, { recursive: true }); fs.mkdirSync(CHECK, { recursive: true }); fs.mkdirSync(REF, { recursive: true });
const browser = await launch();
const layoutFile = path.join(OUT, 'intro.json');
const layout = fs.existsSync(layoutFile) ? JSON.parse(fs.readFileSync(layoutFile, 'utf8')) : {};
Object.assign(layout, {
  version: 1, screen: { width: W, height: H, scale: K },
  veil: [[0, '#090d2e'], [0.55, '#141a46'], [1, '#1b1838']], dawn: [[0, '#ff7a8a'], [0.5, '#ffaa66'], [1, '#ffd890']],
  script: SCRIPT,
  references: Object.entries(REFS).flatMap(([season, list]) => list.map(([t, label]) => ({ name: refName(season, t, label), season, t })))
    .concat([{ name: refName('rainy', SKIP.shot, 'skipped'), season: 'rainy', t: SKIP.shot, skipAt: SKIP.at }]),
});
layout.seasons = layout.seasons || {};
function refName(season, t, label) { return `intro-${season}-${String(t).padStart(5, '0')}-${label}`; }
const save = (rel, buf) => { const f = path.join(OUT, rel + '.png'); fs.mkdirSync(path.dirname(f), { recursive: true }); fs.writeFileSync(f, PNG.sync.write(PNG.sync.read(buf), { deflateLevel: 9, deflateStrategy: 0 })); return rel; };

// ---------------------------------------------------------------- pages

// The app's menu with the given weather. intro: start the intro at once (as the app's first menu does); otherwise the
// menu settles first (9 s, as the other exporters) and startIntro() starts it with the clock held, so its layers are
// there, untouched, to be set by hand. freeze: CSS animations held at their first frame (pieces); otherwise synced.
async function openPage({ season, viewport = { width: W, height: H }, scale = K, intro = false, freeze = true }) {
  const ctx = await browser.newContext({ viewport, deviceScaleFactor: scale, hasTouch: true });
  const page = await ctx.newPage();
  const errs = []; page.on('pageerror', e => errs.push(e.message));
  await page.route(/^(https?|wss?):/, r => r.abort());
  await page.addInitScript(SEED); await page.addInitScript(HOOK);
  await page.addInitScript(`window.__squishOutbox = []; window.__SQUISH_INIT__ = ${JSON.stringify({ intro, state: { weather: season } })};`);
  await page.clock.install({ time: T0 }); await page.clock.pauseAt(T0);
  if (freeze) { const cdp = await ctx.newCDPSession(page); await cdp.send('Animation.enable'); await cdp.send('Animation.setPlaybackRate', { playbackRate: 0 }); }
  await page.goto(pathToFileURL(path.join(BUILT, 'App.dc.html')).href);
  await page.waitForTimeout(7000);
  if (!intro) {
    for (let t = 0; t < 9000; t += 500) { await page.clock.runFor(500); await page.waitForTimeout(80); }
    await page.evaluate(() => document.fonts.ready);
    await page.evaluate(() => { const A = window.SquishApp; A.intro = true; A._introDone = false; window.SquishMenu.mounted(window.SquishMenu._x); });
    if (!(await page.evaluate(() => !!document.querySelector('.si-ov')))) throw new Error('the intro did not start');
  } else {
    for (let i = 0; i < 2000 && !(await page.evaluate(() => window.__introT0 != null)); i++) await page.clock.runFor(1);
  }
  await page.evaluate(() => document.fonts.ready);
  return { ctx, page, errs };
}

// Everything hidden but these (CSS selectors); hide: selectors hidden inside them.
async function showOnly(page, sels, hide = []) {
  await page.evaluate(({ sels, hide }) => {
    document.querySelectorAll('[data-x-show],[data-x-hide]').forEach(e => { e.removeAttribute('data-x-show'); e.removeAttribute('data-x-hide'); });
    let st = document.getElementById('x-iso');
    if (!st) { st = document.createElement('style'); st.id = 'x-iso'; document.head.appendChild(st); }
    st.textContent = 'html,body{background:transparent !important} *{visibility:hidden !important} [data-x-show],[data-x-show] *{visibility:visible !important} [data-x-hide],[data-x-hide] *{visibility:hidden !important}';
    sels.forEach(s => document.querySelectorAll(s).forEach(e => e.setAttribute('data-x-show', '')));
    hide.forEach(s => document.querySelectorAll(s).forEach(e => e.setAttribute('data-x-hide', '')));
  }, { sels, hide });
}
const shot = async page => readPng(await page.screenshot({ omitBackground: true }));

// ---------------------------------------------------------------- world pieces (per weather)

// a picture with alpha `a` (its own alpha channel) whose colour turns B into R where it lies: q = (R - B (1 - a)) / a.
// Where a is small its colour counts for little (q a): it's rounded to steps that change the result by under 1/4 of a
// level, so the noise of R - B over a tiny a doesn't bloat the file.
function sprite(B, R, A) {
  const out = new PNG({ width: B.w, height: B.h });
  for (let i = 0; i < B.w * B.h; i++) {
    const a = A.data[i * 4 + 3] / 255;
    if (a <= 0) continue;
    const step = Math.min(64, Math.max(1, Math.floor(64 / A.data[i * 4 + 3])));
    for (let c = 0; c < 3; c++) {
      const b = B.data[i * 4 + c], r = R.data[i * 4 + c], q = (r - b * (1 - a)) / a;
      out.data[i * 4 + c] = Math.max(0, Math.min(255, step > 1 ? Math.round(q / step) * step : Math.round(q)));
    }
    out.data[i * 4 + 3] = A.data[i * 4 + 3];
  }
  return PNG.sync.write(out);
}
// a picture's RGB with another's alpha
function withAlpha(rgb, A) {
  const out = new PNG({ width: rgb.w, height: rgb.h });
  for (let i = 0; i < rgb.w * rgb.h; i++) { for (let c = 0; c < 3; c++) out.data[i * 4 + c] = rgb.data[i * 4 + c]; out.data[i * 4 + 3] = A.data[i * 4 + 3]; }
  return PNG.sync.write(out);
}
// trimmed to its painted pixels: { image, px } in the full picture's grid
function piece(rel, buf) {
  const t = trim(buf);
  if (!t) return null;
  save(rel, t.buf);
  return { image: rel, px: [t.x, t.y, t.w, t.h] };
}

// the intro's back layer and scene set to a state: h/g/p per room, night, moon, dawn, sceneB (0 or 1), door light
async function setState(page, s) {
  await page.evaluate(s => {
    const back = document.querySelector('.si-back'), q = c => back.querySelectorAll(c);
    for (const r of Object.keys(s.h || {})) q('.h-' + r).forEach(e => e.setAttribute('opacity', s.h[r]));
    for (const r of Object.keys(s.g || {})) q('.g-' + r).forEach(e => e.setAttribute('opacity', s.g[r]));
    for (const r of Object.keys(s.p || {})) q('.p-' + r).forEach(e => e.setAttribute('opacity', s.p[r]));
    const night = back.querySelector('.si-night'), moon = back.querySelector('.si-moon'), dawn = back.querySelector('.si-dawn');
    night.style.opacity = s.night; night.style.display = s.night > 0 ? '' : 'none';
    moon.style.opacity = s.moon; dawn.style.opacity = s.dawn || 0;
    const sb = document.querySelector('.si-sceneb');
    if (s.sceneB && !sb.innerHTML) { const W = window.__introApi; sb.innerHTML = W.sceneSvg({ level: 1, season: window.__xSeason }); sb.querySelectorAll('.mm-sign').forEach(e => e.remove()); if (window.__xShift) window.__xShift(); }
    sb.style.opacity = s.sceneB || 0;
    const dl = document.querySelector('.mm-scene .mm-doorlight'); if (dl) dl.setAttribute('opacity', s.door || 0);
    back.querySelector('.si-halo').style.mixBlendMode = s.haloNormal ? 'normal' : 'screen';
    back.querySelector('.si-pane').style.mixBlendMode = s.paneNormal ? 'normal' : 'soft-light';
  }, s);
}
const only = (room, v, rest = 0) => Object.fromEntries(ROOMS.map(r => [r, r === room ? v : rest]));

async function worldPieces(season, shared) {
  console.log(`\n[${season}] the house's pieces`);
  // the scene page: the drawing's whole 1000 x 460 at the A15's scale for it, on the same pixel grid as the menu's
  // scene pictures (the page's 0.087 px offset drawn in: export-menu.mjs openScenePage)
  const { ctx, page } = await openPage({ season, viewport: { width: 1000, height: 460 }, scale: SCENE_SCALE });
  const pageOffset = (W * K - 1000 * SCENE_SCALE) / 2, imgW = Math.ceil(1000 * SCENE_SCALE - 1e-6), shift = pageOffset - (W * K - imgW) / 2;
  // (moved by their viewBox, not a CSS transform: a transform would make the back layer an isolated group, and its
  // blend modes would no longer reach the house; and a moved composited layer is resampled, not drawn at the offset)
  await page.addStyleTag({ content: '.si-black{display:none !important} .mm-world{transform:none !important}' });   // (the camera at rest)
  await page.evaluate(({ s, d }) => {
    window.__xSeason = s;
    window.__xShift = () => document.querySelectorAll('.mm-world svg').forEach(v => { if (v.__xs || v.getAttribute('viewBox') === '0 0 1000 460') { v.setAttribute('viewBox', (-d) + ' 0 1000 460'); v.__xs = 1; } });
    window.__xShift();
  }, { s: season, d: shift / SCENE_SCALE });
  const S = {};
  const wx = await page.evaluate(() => window.MainMenuScene.state({ level: 1 }).season);
  if (wx !== season) throw new Error(`the page's weather is ${wx}, not ${season}`);

  // the night's house (intro.js's scene, as run() drew it) and the morning's
  await setState(page, { h: all(() => 0), g: all(() => 0), p: all(() => 0), night: 0.8, moon: 0.6 });
  await showOnly(page, ['.mm-scene']);
  const Sa = await shot(page);
  S.night = save(`${season}/night`, PNG.sync.write(Object.assign(new PNG({ width: Sa.w, height: Sa.h }), { data: Sa.data })));
  S.pixels = [Sa.w, Sa.h];
  if (season === 'sunny') {
    await setState(page, { h: all(() => 0), g: all(() => 0), p: all(() => 0), night: 0.8, moon: 0.6, sceneB: 1 });
    await showOnly(page, ['.si-sceneb']);
    const Sb = await shot(page);
    S.morning = save(`${season}/morning`, PNG.sync.write(Object.assign(new PNG({ width: Sb.w, height: Sb.h }), { data: Sb.data })));
    await setState(page, { h: all(() => 0), g: all(() => 0), p: all(() => 0), night: 0.8, moon: 0.6, sceneB: 0 });
  } else S.morning = null;   // (the same picture as the night's)

  // the door's light (the scene's .mm-doorlight, 0 .. 0.95): what it changes on the night's house
  await page.evaluate(() => { const d = document.querySelector('.mm-scene .mm-doorlight'); d.setAttribute('opacity', 1); });
  await showOnly(page, ['.mm-scene']);
  const Rd = await shot(page);
  await showOnly(page, ['.mm-scene .mm-doorlight']);
  const Ad = await shot(page);
  await page.evaluate(() => document.querySelector('.mm-scene .mm-doorlight').setAttribute('opacity', 0));
  let changed = 0; for (let i = 0; i < Sa.data.length; i++) if (Sa.data[i] !== Rd.data[i]) changed++;
  S.doorlight = changed ? piece(`${season}/doorlight`, sprite(Sa, Rd, Ad)) : null;
  console.log(`  door light: ${changed ? 'changes ' + changed + ' values' : 'hidden behind the closed door: no picture'}`);

  // each room's windows cut out of the night's house (the veil's holes: the mask's shapes, drawn white)
  S.cuts = {};
  for (const r of ROOMS) {
    await page.evaluate(r => {
      const back = document.querySelector('.si-back'); let g = back.querySelector('#x-holes');
      if (!g) { g = document.createElementNS('http://www.w3.org/2000/svg', 'g'); g.id = 'x-holes'; back.appendChild(g); }
      g.innerHTML = ''; back.querySelectorAll('mask .h-' + r).forEach(e => { const c = e.cloneNode(); c.setAttribute('fill', '#fff'); c.setAttribute('opacity', 1); c.removeAttribute('class'); g.appendChild(c); });
    }, r);
    await showOnly(page, ['#x-holes']);
    S.cuts[r] = piece(`${season}/cut-${r}`, withAlpha(Sa, await shot(page)));
  }
  await page.evaluate(() => document.querySelector('#x-holes').remove());

  // the halos and panes, night: over the house with every room lit
  const nightLit = dawnState(0);
  // the dawn's keyframes: the house the halos and panes lie on at these points of the dawn (t: 0 the night with every
  // room lit, 1 the morning); Unity crossfades between the two either side of where the dawn is
  S.dawnKeys = DAWN_KEYS;
  S.halos = {}; S.panes = {};
  const layers = ['.mm-scene', '.si-sceneb', '.si-back'], alpha = {};
  for (const r of ROOMS) {
    // each room's halos and panes at opacity 1, alone (normal blending): their alpha
    await setState(page, { ...nightLit, g: only(r, 1), haloNormal: true });
    await showOnly(page, ['.si-back .si-halo']);
    const A = await shot(page);
    await setState(page, { ...nightLit, p: only(r, 1), paneNormal: true });
    await showOnly(page, ['.si-back .si-pane']);
    alpha[r] = { halo: A, pane: await shot(page) };
    // (as they are, for the end: when the intro's layers fade, the back layer is a group of its own, its blend modes
    // no longer reach the house, and the halos and panes show as plain colours)
    if (shared) for (const k of ['halo', 'pane']) { layout.plain = layout.plain || {}; (layout.plain[k] = layout.plain[k] || {})[r] = piece(`${k}-${r}-plain`, PNG.sync.write(Object.assign(new PNG({ width: alpha[r][k].w, height: alpha[r][k].h }), { data: alpha[r][k].data }))); }
  }
  for (const t of DAWN_KEYS) {
    const st = dawnState(t), tag = 'k' + String(Math.round(t * 100)).padStart(3, '0');
    await setState(page, st);
    await showOnly(page, layers, ['.si-halo', '.si-pane']);
    const Bh = await shot(page);
    await showOnly(page, layers, ['.si-pane']);
    const Bp = await shot(page);   // (all halos at their opacity)
    for (const r of ROOMS) {
      // halo: the room's ellipses at opacity 1 screened over the house, for the colour
      await setState(page, { ...st, g: only(r, 1) });
      await showOnly(page, layers, ['.si-pane']);
      const R = await shot(page);
      (S.halos[r] = S.halos[r] || []).push(piece(`${season}/halo-${r}-${tag}`, sprite(Bh, R, alpha[r].halo)));
      // pane: the room's panes at opacity 1, soft-light over the house and halos
      await setState(page, { ...st, p: only(r, 1) });
      await showOnly(page, layers);
      const Rp = await shot(page);
      (S.panes[r] = S.panes[r] || []).push(piece(`${season}/pane-${r}-${tag}`, sprite(Bp, Rp, alpha[r].pane)));
    }
    console.log(`  halos and panes over the house at dawn t = ${t}`);
  }
  await setState(page, nightLit);

  if (shared) {
    // the moon (its group at opacity 1)
    await setState(page, { ...nightLit, moon: 1 });
    await showOnly(page, ['.si-back .si-moon']);
    layout.moon = piece('moon', PNG.sync.write(Object.assign(new PNG({ width: Sa.w, height: Sa.h }), { data: (await shot(page)).data })));
    // the box: its face at rest (filter brightness(.62) saturate(.85) drawn in) and its shadow at opacity 1
    await page.evaluate(() => { const f = document.querySelector('.si-bxf'); f.removeAttribute('transform'); f.setAttribute('opacity', 1); document.querySelector('.si-bsh').setAttribute('opacity', 1); });
    await showOnly(page, ['.si-bxf']);
    const face = piece('box-face', PNG.sync.write(Object.assign(new PNG({ width: Sa.w, height: Sa.h }), { data: (await shot(page)).data })));
    await showOnly(page, ['.si-bsh']);
    const shadow = piece('box-shadow', PNG.sync.write(Object.assign(new PNG({ width: Sa.w, height: Sa.h }), { data: (await shot(page)).data })));
    // the flaps' colours under the box's filter (CSS filters run in sRGB, each step clamped: checked against Chrome)
    const filt = c => { const v = [1, 3, 5].map(i => parseInt(c.slice(i, i + 2), 16) / 255), m = mat(0.62, 0.85); return '#' + m.map(r => Math.round(Math.max(0, Math.min(1, r[0] * v[0] + r[1] * v[1] + r[2] * v[2])) * 255).toString(16).padStart(2, '0')).join(''); };
    layout.box = { face, shadow, inside: filt('#3e2412'), flapL: filt('#d9a066'), flapR: filt('#c98f55'), stroke: filt('#5a3418'), strokeWidth: 2 };
    // the spark's glow (siGlowF, r 18) around (500, 200)
    await page.evaluate(() => { const top = document.querySelector('.si-top'); const c = document.createElementNS('http://www.w3.org/2000/svg', 'circle'); c.id = 'x-glow'; c.setAttribute('cx', 500); c.setAttribute('cy', 200); c.setAttribute('r', 18); c.setAttribute('fill', 'url(#siGlowF)'); top.appendChild(c); });
    await showOnly(page, ['#x-glow']);
    layout.glow = { ...piece('spark-glow', PNG.sync.write(Object.assign(new PNG({ width: Sa.w, height: Sa.h }), { data: (await shot(page)).data }))), at: [500, 200] };
    // the squad's pictures, as the windows show them (sz x sz scene units), at a whole-pixel place
    layout.peeks = [];
    for (const [key, room, wi, pcx] of PEEK) {
      const w = WIN[room][wi], [x, y, ww, hh] = box(w), sz = Math.min(ww * 1.1, hh * 1.3), cx = pcx || x + ww / 2;
      const X = 100, Y = 100;   // a place on whole pixels: picture px = unit * SCENE_SCALE + shift
      await page.evaluate(({ key, X, Y, sz }) => {
        const L = document.querySelector('.si-peekl'); L.innerHTML = '<image id="x-peek" href="export/thumbs/c-' + key + '.webp" x="' + X + '" y="' + Y + '" width="' + sz + '" height="' + sz + '"/>';
      }, { key, X, Y, sz });
      await page.waitForFunction(() => { const i = document.querySelector('#x-peek'); return i && i.getBBox().width > 0; });
      await page.waitForTimeout(150);
      await showOnly(page, ['#x-peek']);
      const p = piece(`peek-${key}`, PNG.sync.write(Object.assign(new PNG({ width: Sa.w, height: Sa.h }), { data: (await shot(page)).data })));
      // the picture's box relative to the image's (x, y), in scene units
      layout.peeks.push({ key, room, wi, cx, sz, image: p.image, px: p.px, at: [X, Y] });
    }
    await page.evaluate(() => { document.querySelector('.si-peekl').innerHTML = ''; });
    layout.scene = { pixels: S.pixels, sceneScale: +SCENE_SCALE.toFixed(6), shift: +shift.toFixed(4) };
  }
  await ctx.close();
  layout.seasons[season] = S;
}

function mat(b, s) {   // brightness(b) then saturate(s), as one matrix (no clamping needed in between for these)
  const B = [[b, 0, 0], [0, b, 0], [0, 0, b]], Sm = [[0.213 + 0.787 * s, 0.715 - 0.715 * s, 0.072 - 0.072 * s], [0.213 - 0.213 * s, 0.715 + 0.285 * s, 0.072 - 0.072 * s], [0.213 - 0.213 * s, 0.715 - 0.715 * s, 0.072 + 0.928 * s]];
  return Sm.map(r => [0, 1, 2].map(j => r[0] * B[0][j] + r[1] * B[1][j] + r[2] * B[2][j]));
}

// ---------------------------------------------------------------- the real intro, driven

const PXB = (r) => [Math.floor(r.x * K), Math.floor(r.y * K), 0, 0];
async function drive(season, { refs, skip, pieces }) {
  const { ctx, page, errs } = await openPage({ season, intro: true, freeze: false });
  const it = () => page.evaluate(() => performance.now() - window.__introT0);
  const runTo = async ms => { let t = await it(); while (t < ms - 1e-6) { await page.clock.runFor(Math.max(1, Math.min(4, Math.round(ms - t)))); t = await page.evaluate(() => window.__animSync()); } };
  const squish = up => page.evaluate(up => { const h = document.querySelector('.mm-stage .mm-hit'), r = h.getBoundingClientRect();
    h.dispatchEvent(new PointerEvent(up ? 'pointerup' : 'pointerdown', { bubbles: true, clientX: r.x + r.width / 2, clientY: r.y + r.height / 2, pointerId: 1, isPrimary: true })); }, up);
  const acts = skip
    ? [[skip.at, () => page.evaluate(() => document.querySelector('.si-skip').click())]]
    : [[SCRIPT.tapBox, () => page.evaluate(() => document.querySelector('.si-hit').click())],
      ...SCRIPT.squish.flatMap(([d, u]) => [[d, () => squish(false)], [u, () => squish(true)]]),
      [SCRIPT.start, () => page.evaluate(() => document.querySelector('.si-ov').click())]];
  const events = [...acts.map(([t, a]) => ({ t, a })), ...refs.map(([t, label]) => ({ t, label })), ...(pieces ? [{ t: SCRIPT.start - 300, pieces: true }] : [])].sort((a, b) => a.t - b.t);
  const checks = [];
  for (const e of events) {
    await runTo(e.t);
    if (e.a) { await e.a(); continue; }
    if (e.pieces) { await overlayPieces(page); continue; }
    await page.evaluate(() => document.fonts.ready);
    const name = refName(season, e.t, e.label);
    await page.screenshot({ path: path.join(REF, name + '.jpg'), type: 'jpeg', quality: 90 });
    // the check's frame: without what Unity draws itself (rain and snow, Mittens, the tap rings, the sparkles, the
    // box's flaps), and the page's live values
    const live = await liveValues(page);
    if (live.intro) {
      const hide = await page.addStyleTag({ content: '.si-rainl,.mm-stage,.si-ring,.si-fx,.si-boxb{visibility:hidden !important}' });
      const ref = readPng(await page.screenshot());
      await hide.evaluate(n => n.remove());
      checks.push({ name, t: e.t, ref, live });
    }
    console.log(`  ${name}.jpg`);
  }
  if (errs.length) console.log('  page errors:', [...new Set(errs)].slice(0, 3).join(' | '));
  await ctx.close();
  return checks;
}

// The values the page shows at this moment, as MenuIntro.cs takes them from IntroScript (opacities, transforms).
async function liveValues(page) {
  return page.evaluate(ROOMS => {
    const back = document.querySelector('.si-back');
    if (!back || !document.querySelector('.si-black')) return { intro: false };   // (gone: the intro has finished)
    const num = (v, d = 0) => { const n = parseFloat(v); return isFinite(n) ? n : d; };
    const op = (e, attr = true) => !e ? 0 : getComputedStyle(e).display === 'none' ? 0 : e.style.opacity !== '' ? num(e.style.opacity, 1) : attr ? num(e.getAttribute('opacity'), 1) : 1;
    const rooms = {};
    for (const r of ROOMS) rooms[r] = { h: num(back.querySelector('.h-' + r).getAttribute('opacity')), g: Math.min(1, num(back.querySelector('.g-' + r).getAttribute('opacity'))), p: num(back.querySelector('.p-' + r).getAttribute('opacity')) };
    const night = back.querySelector('.si-night'), sb = document.querySelector('.si-sceneb');
    const bxf = document.querySelector('.si-bxf'), tr = (bxf.getAttribute('transform') || '').match(/-?[\d.]+/g) || [];
    const cap = document.querySelector('.si-cap'), cs = getComputedStyle(cap), cm = new DOMMatrix(cs.transform === 'none' ? undefined : cs.transform);
    const capKey = Object.entries({ house: 'A little house', box: 'Something is moving', squish0: 'Squish Mittens', squish1: 'The living room', squish2: 'The bedroom too', squish3: 'The whole house' }).find(([k, t]) => cap.textContent.startsWith(t));
    const tap = document.querySelector('.si-tap'), skip = document.querySelector('.si-skip');
    const letters = [...document.querySelectorAll('.si-logo > div')].map(d => { const m = d.style.transform.match(/translateY\((-?[\d.]+)px\) scale\((-?[\d.]+),\s*(-?[\d.]+)\) rotate\((-?[\d.]+)deg\)/) || d.style.transform.match(/translateY\((-?[\d.]+)px\)/);
      return m && m.length > 2 ? { y: +m[1], sx: +m[2], sy: +m[3], rot: +m[4] } : { y: m ? +m[1] : 0, sx: 1, sy: 1, rot: 0 }; });
    const peeks = [...document.querySelectorAll('.si-peekl image')].map(im => { const m = (im.getAttribute('transform') || '').match(/-?[\d.]+/g) || [0]; return { y: num(im.getAttribute('y')), rot: +m[0] }; });
    const w = document.querySelector('.mm-world').style.transform.match(/scale\(([\d.]+)\) translateY\((-?[\d.]+)%\)/);
    return {
      intro: true, rooms, night: op(night), moon: op(back.querySelector('.si-moon')), dawn: op(back.querySelector('.si-dawn')),
      nightShown: getComputedStyle(night).display !== 'none',
      sceneB: sb.innerHTML ? op(sb, false) : 0, door: num((document.querySelector('.mm-scene .mm-doorlight') || { getAttribute: () => 0 }).getAttribute('opacity')),
      world: w ? { s: +w[1], ty: +w[2] } : { s: 1, ty: 0 },
      box: { shown: getComputedStyle(bxf).display !== 'none', dy: tr.length >= 2 ? +tr[1] - 432 : 0, rot: +(tr[2] || 0), sx: +(tr[3] || 1), sy: +(tr[4] || 1), op: num(bxf.getAttribute('opacity'), 1), shadow: num(document.querySelector('.si-bsh').getAttribute('opacity')) },
      caption: { key: capKey ? capKey[0] : null, opacity: num(cs.opacity), scale: cm.a },
      skip: { shown: getComputedStyle(skip).display !== 'none' },
      tap: tap ? { opacity: num(getComputedStyle(tap).opacity), scale: new DOMMatrix(getComputedStyle(tap.firstChild).transform === 'none' ? undefined : getComputedStyle(tap.firstChild).transform).a } : null,
      black: document.querySelector('.si-black') ? num(getComputedStyle(document.querySelector('.si-black')).opacity) : 0,
      letters, peeks,
    };
  }, ROOMS);
}

// The overlay's pieces, from the real intro (rainy run, before TAP TO START is tapped): the letters at rest, the tap,
// Skip, and each caption put in the caption box with its own words.
async function overlayPieces(page) {
  console.log('  overlay pieces');
  const rect = sel => page.evaluate(s => { const b = document.querySelector(s).getBoundingClientRect(); return [b.x, b.y, b.width, b.height].map(v => +v.toFixed(4)); }, sel);
  const part = async (rel, sel, i = 0) => {
    const r = await page.evaluate(({ sel, i }) => { const b = document.querySelectorAll(sel)[i].getBoundingClientRect(); return { x: b.x, y: b.y, w: b.width, h: b.height }; }, { sel, i });
    const pad = 40, px0 = Math.max(0, Math.floor((r.x - pad) * K)), py0 = Math.max(0, Math.floor((r.y - pad) * K)), px1 = Math.min(W * K, Math.ceil((r.x + r.w + pad) * K)), py1 = Math.min(H * K, Math.ceil((r.y + r.h + pad) * K));
    await isolate(page, { sel, i });
    const t = trim(await page.screenshot({ omitBackground: true, clip: { x: px0 / K, y: py0 / K, width: (px1 - px0) / K, height: (py1 - py0) / K } }));
    save(rel, t.buf);
    return { image: rel, px: [px0 + t.x, py0 + t.y, t.w, t.h], box: [r.x, r.y, r.w, r.h].map(v => +v.toFixed(4)) };
  };
  // the letters (their own transforms off: at rest), each in its box (transform-origin 50% 96% of it)
  const n = await page.evaluate(() => { const L = [...document.querySelectorAll('.si-logo > div')]; L.forEach(d => { d.__t = d.style.transform; d.style.transform = 'none'; }); return L.length; });
  layout.logo = [];
  for (let i = 0; i < n; i++) layout.logo.push(await part(`logo-${i}`, '.si-logo > div', i));
  await page.evaluate(() => document.querySelectorAll('.si-logo > div').forEach(d => { d.style.transform = d.__t; }));
  // TAP TO START (its pulse off), its text's box (the pulse scales it about its middle)
  await page.evaluate(() => { const s = document.querySelector('.si-tap span'); s.style.animation = 'none'; });
  layout.tap = { ...(await part('tap', '.si-tap')), span: await rect('.si-tap span') };
  await page.evaluate(() => { document.querySelector('.si-tap span').style.animation = ''; });
  // Skip (hidden by now: shown for its picture)
  await page.evaluate(() => { document.querySelector('.si-skip').style.display = ''; });
  layout.skip = await part('skip', '.si-skip');
  await page.evaluate(() => { document.querySelector('.si-skip').style.display = 'none'; });
  // the captions
  layout.captions = {};
  for (const k of Object.keys(CAPTIONS)) {
    await page.evaluate(html => { const c = document.querySelector('.si-cap'); c.__h = c.__h == null ? c.innerHTML : c.__h; c.style.transition = 'none'; c.innerHTML = html; c.classList.add('on'); c.classList.remove('pop'); }, captionHtml(k));
    layout.captions[k] = await part(`caption-${k}`, '.si-cap');
  }
  await page.evaluate(() => { const c = document.querySelector('.si-cap'); c.classList.remove('on'); c.innerHTML = c.__h; c.style.transition = ''; });
  await showAll(page);
}

// ---------------------------------------------------------------- the self-check: Unity's rules, in the browser's frames

const hex = c => [1, 3, 5].map(i => parseInt(c.slice(i, i + 2), 16) / 255);
const stopsAt = (stops, f) => { f = Math.max(0, Math.min(1, f)); for (let i = 1; i < stops.length; i++) if (f <= stops[i][0]) { const a = hex(stops[i - 1][1]), b = hex(stops[i][1]), k = (f - stops[i - 1][0]) / (stops[i][0] - stops[i - 1][0]); return a.map((v, j) => v + (b[j] - v) * k); } return hex(stops[stops.length - 1][1]); };
const pics = new Map();
const pic = rel => { if (!pics.has(rel)) pics.set(rel, readPng(fs.readFileSync(path.join(OUT, rel + '.png')))); return pics.get(rel); };

// MenuIntro.cs's halo / pane crossfade between the night's picture and the morning's: s = how far the night has gone
// (1 - night / 0.8; 1 once it's gone); the morning's at g s, the night's at g (1 - s) / (1 - g s a), a = the layer's
// typical alpha (halos 0.5, panes 1), so the pair adds up to about g.
export const pair = (g, s, a) => { g = Math.min(1, g); const o2 = g * s; return [g * (1 - s) / Math.max(1e-6, 1 - o2 * a), o2]; };

function compose(season, v) {
  const L = layout, S = L.seasons[season], A = { w: Math.round(W * K), h: Math.round(H * K) };
  const cv = new Canvas(A.w, A.h, [0x24, 0x12, 0x3f]);
  const SS = L.scene.sceneScale, shiftPx = L.scene.shift, sceneLeft = Math.round((A.w - L.scene.pixels[0]) / 2), sceneTop = A.h - L.scene.pixels[1];
  // the world (camera: scale s about 50% 64% of the screen, then translateY(ty% of its height) scaled)
  const ox = A.w * 0.5, oy = A.h * 0.64, sc = v.world.s, ty = v.world.ty / 100 * A.h * sc;
  const world = (x, y) => [ox + sc * (x - ox), oy + sc * (y - oy) + ty];
  const M = (px, py, k = 1) => { const [e, f] = world(sceneLeft + px, sceneTop + py); return [sc * k, 0, 0, sc * k, e, f]; };
  const unit = (x, y) => [x * SS + shiftPx, y * SS];   // scene units -> scene picture px
  // the dawn's tint (multiply), over the veil's span (screen y from -s to H + s in dp, as veilFit)
  const s = Math.max(W / 1000, H / 460), vTop = (-s) * K, vBot = (H + s) * K;
  const d = v.nightShown ? v.dawn : 0;
  const tint = d > 0 ? y => stopsAt(L.dawn, (y - vTop) / (vBot - vTop)).map(c => 1 - d + d * c) : null;
  const P = (rel, px, o = {}) => cv.draw(pic(rel), M(px[0], px[1]), o);
  P(S.night, [0, 0], { tint });
  if (S.doorlight && v.door > 0) P(S.doorlight.image, S.doorlight.px, { opacity: v.door, tint });
  if (v.sceneB > 0) P(S.morning || S.night, [0, 0], { opacity: v.sceneB, tint });
  const n = v.nightShown ? v.night : 0;
  if (n > 0) { const [, f0] = world(0, vTop), [, f1] = world(0, vBot); cv.fillRows(0, f0, A.w, f1, y => stopsAt(L.veil, (y - f0) / (f1 - f0)), n); }
  for (const r of ROOMS) if (v.rooms[r].h > 0 && S.cuts[r]) P(S.cuts[r].image, S.cuts[r].px, { opacity: v.rooms[r].h, tint });
  if (v.nightShown && v.moon > 0) P(L.moon.image, L.moon.px, { opacity: v.moon });
  const tDawn = v.nightShown ? 1 - n / 0.8 : 1, keys = S.dawnKeys;
  let ka = 0; while (ka < keys.length - 2 && tDawn > keys[ka + 1]) ka++;
  const wk = Math.max(0, Math.min(1, (tDawn - keys[ka]) / (keys[ka + 1] - keys[ka])));
  for (const [kind, a] of [['halos', 0.5], ['panes', 1]]) for (const r of ROOMS) {
    const g = kind === 'halos' ? v.rooms[r].g : v.rooms[r].p; if (g <= 0) continue;
    const [o1, o2] = pair(g, wk, a), A = S[kind][r][ka], B = S[kind][r][ka + 1];
    if (o1 > 0 && A) P(A.image, A.px, { opacity: o1 });
    if (o2 > 0 && B) P(B.image, B.px, { opacity: o2 });
  }
  // the peeks: clipped by their window (hard clip), rotated about (cx, base)
  v.peeks.forEach((pk, i) => {
    const p = L.peeks[i], w = WIN[p.room][p.wi], [bx, by, bw, bh] = box(w), base = by + bh + p.sz * 0.3;
    const img = pic(p.image), x0 = p.cx - p.sz / 2, dx = (p.px[0] - unit(p.at[0], 0)[0]) / SS, dy = (p.px[1] - unit(0, p.at[1])[1]) / SS;
    // the picture's top-left in scene units, before the rotation
    const tx = x0 + dx, tyU = pk.y + dy, a = pk.rot * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
    // scene units -> canvas px: rotate about (cx, base) in units, then picture grid, then world
    const rot = (x, y) => [p.cx + ca * (x - p.cx) - sa * (y - base), base + sa * (x - p.cx) + ca * (y - base)];
    const at = (x, y) => { const [ux, uy] = rot(x, y), [px, py] = unit(ux, uy); return world(sceneLeft + px, sceneTop + py); };
    const o = at(tx, tyU), ex = at(tx + 1 / SS, tyU), ey = at(tx, tyU + 1 / SS);
    const m = [ex[0] - o[0], ex[1] - o[1], ey[0] - o[0], ey[1] - o[1], o[0], o[1]];
    const inv = (X, Y) => { const ux = ((X - (sceneLeft)) - shiftPx) / SS, uy = (Y - sceneTop) / SS; return [ux, uy]; };
    const clip = (X, Y) => { const [x, y] = inv(X, Y);
      if (w[0] === 'c') return Math.hypot(x - w[1], y - w[2]) <= w[3];
      if (w[0] === 'a') { if (x < bx || x > bx + bw || y > by + bh) return false; const r = bw / 2; return y >= by + r || Math.hypot(x - (bx + r), y - (by + r)) <= r; }
      return x >= bx && x <= bx + bw && y >= by && y <= by + bh; };
    cv.draw(img, m, { clip });
  });
  // the box's face and shadow (boxT about (BX, BY))
  if (v.box.shown) {
    if (v.box.shadow > 0) P(L.box.shadow.image, L.box.shadow.px, { opacity: v.box.shadow });
    const [Bx, By] = unit(500, 432), a = v.box.rot * Math.PI / 180, img = pic(L.box.face.image);
    const loc = (x, y) => { const X = x - Bx, Y = y - By; return [Bx + Math.cos(a) * v.box.sx * X - Math.sin(a) * v.box.sy * Y, By + v.box.dy * SS + Math.sin(a) * v.box.sx * X + Math.cos(a) * v.box.sy * Y]; };
    const o = loc(L.box.face.px[0], L.box.face.px[1]), ex = loc(L.box.face.px[0] + 1, L.box.face.px[1]), ey = loc(L.box.face.px[0], L.box.face.px[1] + 1);
    const wo = world(sceneLeft + o[0], sceneTop + o[1]), wx = world(sceneLeft + ex[0], sceneTop + ex[1]), wy = world(sceneLeft + ey[0], sceneTop + ey[1]);
    cv.draw(img, [wx[0] - wo[0], wx[1] - wo[1], wy[0] - wo[0], wy[1] - wo[1], wo[0], wo[1]], { opacity: v.box.op });
  }
  // the overlay (screen px; the page's 393-tall design scaled by k = H / 393)
  const k = H / 393;
  const about = (img, px, cx, cy, sx, sy, rot, dyDp, opacity) => {
    const a = rot * Math.PI / 180, c = Math.cos(a), sn = Math.sin(a);
    // CSS: translate(0, dy) scale(sx, sy) rotate(a) about (cx, cy): p' = o + T + S R (p - o)
    const f = (x, y) => { const X = x - cx, Y = y - cy, rx = c * X - sn * Y, ry = sn * X + c * Y; return [cx + sx * rx, cy + sy * ry + dyDp * K]; };
    const o = f(px[0], px[1]), ex = f(px[0] + 1, px[1]), ey = f(px[0], px[1] + 1);
    cv.draw(img, [ex[0] - o[0], ex[1] - o[1], ey[0] - o[0], ey[1] - o[1], o[0], o[1]], { opacity });
  };
  if (v.caption.key && v.caption.opacity > 0) { const c = L.captions[v.caption.key], b = c.box; about(pic(c.image), c.px, (b[0] + b[2] / 2) * K, (b[1] + b[3] / 2) * K, v.caption.scale, v.caption.scale, 0, 0, v.caption.opacity); }
  if (v.skip.shown) cv.draw(pic(L.skip.image), [1, 0, 0, 1, L.skip.px[0], L.skip.px[1]]);
  v.letters.forEach((l, j) => { const p = L.logo[j], b = p.box; about(pic(p.image), p.px, (b[0] + b[2] * 0.5) * K, (b[1] + b[3] * 0.96) * K, l.sx, l.sy, l.rot, l.y * k, 1); });
  if (v.tap && v.tap.opacity > 0) { const b = L.tap.span; about(pic(L.tap.image), L.tap.px, (b[0] + b[2] / 2) * K, (b[1] + b[3] / 2) * K, v.tap.scale, v.tap.scale, 0, 0, v.tap.opacity); }
  if (v.black > 0) cv.fillRows(0, 0, A.w, A.h, () => [5 / 255, 6 / 255, 15 / 255], v.black);
  return cv.png();
}

// ---------------------------------------------------------------- run

if (!NO_PIECES) {
  for (const season of SEASONS) await worldPieces(season, season === SEASONS[0] || !layout.moon);
}
const results = [];
if (!NO_REFS) {
  for (const season of SEASONS) {
    console.log(`\n[${season}] the real intro, driven (reference frames)`);
    const checks = await drive(season, { refs: REFS[season], pieces: season === 'rainy' && !NO_PIECES });
    fs.writeFileSync(layoutFile, JSON.stringify(layout, null, 1));
    for (const c of checks) {
      const got = compose(season, c.live), gotP = { w: got.width, h: got.height, data: got.data };
      const r = diff(c.ref, gotP);
      fs.writeFileSync(path.join(CHECK, c.name + '-ref.png'), PNG.sync.write(Object.assign(new PNG({ width: c.ref.w, height: c.ref.h }), { data: c.ref.data })));
      fs.writeFileSync(path.join(CHECK, c.name + '-unity.png'), PNG.sync.write(got));
      fs.writeFileSync(path.join(CHECK, c.name + '-diff.png'), PNG.sync.write(r.png));
      fs.writeFileSync(path.join(CHECK, c.name + '-values.json'), JSON.stringify(c.live, null, 1));
      results.push({ name: c.name, over2: r.over2, over40: r.over40 });
      console.log(`  self-check ${c.name}: ${(100 * r.over2).toFixed(3)}% of pixels differ (>2/255), ${(100 * r.over40).toFixed(3)}% clearly (>40/255)`);
    }
    if (season === 'rainy') { console.log('\n[rainy] skipped in the night'); await drive('rainy', { refs: [[SKIP.shot, 'skipped']], skip: SKIP }); }
  }
  fs.writeFileSync(path.join(CHECK, 'results.json'), JSON.stringify(results, null, 1));
}
fs.writeFileSync(layoutFile, JSON.stringify(layout, null, 1));
console.log(`\nintro.json written (${path.relative(REPO, layoutFile)})`);
await browser.close();
