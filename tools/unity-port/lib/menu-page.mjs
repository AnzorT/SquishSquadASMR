// Shared by the Main Menu exporters: open the game's menu frozen (fake clock, seeded random, animations held, offline),
// show one piece of it alone, and save it trimmed to its painted pixels on the Galaxy A15's pixel grid.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const require = createRequire(import.meta.url);
export const { chromium } = require('playwright');
const HERE = path.dirname(fileURLToPath(import.meta.url));
export const REPO = path.resolve(HERE, '../../..');
export const { PNG } = require(path.join(REPO, 'node_modules/pngjs'));
export const BUILT = path.join(REPO, 'webgame/test/out/webgame');
export const MENU_OUT = path.join(REPO, 'unity/Assets/_Project/Resources/Screens/MainMenu');
// big, rebuilt by the exporters, not in git (unity/.gitignore): one scene per level and season
export const SCENES_OUT = path.join(REPO, 'unity/Assets/_Project/Art/Generated/Resources/MenuScenes');

export const W = 832, H = 384, K = 2.8125;               // the A15, landscape: dp and px per dp
export const SCENE_SCALE = (H / 460) * K;                 // px per scene unit on the A15 (the slice is height-limited)
export const SEASONS = ['sunny', 'rainy', 'snowy'];
const T0 = new Date('2026-11-13T12:00:00Z');
const SEED = `(() => { let s = 0x2f6b9a1; Math.random = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; })();`;

export function needBuild() {
  if (!fs.existsSync(path.join(BUILT, 'App.dc.html'))) {
    console.error('Build the game pages first: node webgame/build.mjs --out webgame/test/out/webgame');
    process.exit(1);
  }
}

export const launch = () => chromium.launch({ args: ['--allow-file-access-from-files'] });

export async function openMenu(browser, viewport = { width: W, height: H }, scale = K) {
  const ctx = await browser.newContext({ viewport, deviceScaleFactor: scale });
  const page = await ctx.newPage();
  await page.route(/^(https?|wss?):/, r => r.abort());
  await page.addInitScript(SEED);
  await page.clock.install({ time: T0 });
  await page.clock.pauseAt(T0);
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Animation.enable');
  await cdp.send('Animation.setPlaybackRate', { playbackRate: 0 });
  await page.goto(pathToFileURL(path.join(BUILT, 'App.dc.html')).href);
  await page.waitForTimeout(8000);
  for (let t = 0; t < 9000; t += 500) { await page.clock.runFor(500); await page.waitForTimeout(100); }
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(800);
  await markCrestNumber(page);
  return { ctx, page };
}

// the level number's two <text>s carry no class of their own: mark them (digits only, inside the crest)
export const markCrestNumber = page =>
  page.evaluate(() => document.querySelectorAll('.sq-crest text').forEach(t => { if (/^\d+$/.test(t.textContent.trim())) t.classList.add('sq-num'); }));

// Show only one piece: everything else hidden, the page see-through. part: { sel, i, hide: [sel], only: sel }
export async function isolate(page, part) {
  await page.evaluate(part => {
    document.querySelectorAll('[data-x-show],[data-x-hide]').forEach(e => { e.removeAttribute('data-x-show'); e.removeAttribute('data-x-hide'); });
    let st = document.getElementById('x-iso');
    if (!st) {
      st = document.createElement('style'); st.id = 'x-iso';
      st.textContent = 'html,body{background:transparent !important} *{visibility:hidden !important}'
        + ' [data-x-show],[data-x-show] *{visibility:visible !important}'
        + ' [data-x-hide],[data-x-hide] *{visibility:hidden !important}'
        // a box that only holds what's kept: its own painting goes (background, border, shadow, ::before/::after)
        + ' [data-x-bare]{background:none !important;box-shadow:none !important;border-color:transparent !important}'
        + ' [data-x-bare]::before,[data-x-bare]::after{visibility:hidden !important}';
      document.head.appendChild(st);
    }
    document.querySelectorAll('[data-x-bare]').forEach(e => e.removeAttribute('data-x-bare'));
    document.querySelectorAll('[data-x-filter]').forEach(e => { e.style.removeProperty('filter'); e.removeAttribute('data-x-filter'); });
    const el = document.querySelectorAll(part.sel)[part.i || 0];
    if (!el) throw new Error('not found: ' + part.sel);
    el.setAttribute('data-x-show', '');
    (part.hide || []).forEach(s => el.querySelectorAll(s).forEach(h => h.setAttribute('data-x-hide', '')));
    if (part.only) {
      const keep = [...el.querySelectorAll(part.only)];
      el.querySelectorAll('*').forEach(e => {
        if (keep.some(k => k === e || k.contains(e))) return;            // kept, or inside something kept
        if (e.closest('defs') || /^(linearGradient|radialGradient|stop|filter|clipPath|mask|pattern|symbol)$/.test(e.tagName)) return; // what kept things are painted with
        if (keep.some(k => e.contains(k))) { bare(e); return; }          // holds something kept
        e.setAttribute('data-x-hide', '');
      });
      bare(el);
    }
    // A box holding the kept elements loses its own painting. A drop shadow on it is the whole group's shadow (in the
    // crest, ball and number together): the kept part's share of it falls under the rest of the group, so it goes too.
    function bare(e) {
      if (!(e instanceof SVGElement)) e.setAttribute('data-x-bare', '');
      const f = getComputedStyle(e).filter;
      if (f && f !== 'none' && f.replace(/drop-shadow\((?:[^()]|\([^()]*\))*\)/g, '').trim() === '') { e.style.setProperty('filter', 'none', 'important'); e.setAttribute('data-x-filter', ''); }
    }
  }, part);
}

export async function showAll(page) {
  await page.evaluate(() => { const s = document.getElementById('x-iso'); if (s) s.remove(); });
}

// Crop a PNG to its painted pixels. Returns the new PNG and its offset, or null when nothing is painted.
export function trim(buf) {
  const png = PNG.sync.read(buf);
  let x0 = png.width, y0 = png.height, x1 = -1, y1 = -1;
  for (let y = 0; y < png.height; y++) for (let x = 0; x < png.width; x++) {
    if (png.data[(y * png.width + x) * 4 + 3] > 0) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  }
  if (x1 < 0) return null;
  const w = x1 - x0 + 1, h = y1 - y0 + 1, out = new PNG({ width: w, height: h });
  PNG.bitblt(png, out, x0, y0, w, h, 0, 0);
  return { buf: PNG.sync.write(out), x: x0, y: y0, w, h };
}

// One piece alone, trimmed: { buf, px: [x, y, w, h] } in A15 screen pixels, or null. The box is the element's, grown
// by 32 dp for shadows and glows and snapped out to whole pixels, so the piece lands exactly where the page has it.
export async function shootPart(page, part) {
  const r = await page.evaluate(p => { const b = document.querySelectorAll(p.sel)[p.i || 0].getBoundingClientRect(); return { x: b.x, y: b.y, w: b.width, h: b.height }; }, part);
  const pad = 32;
  const px0 = Math.max(0, Math.floor((r.x - pad) * K)), py0 = Math.max(0, Math.floor((r.y - pad) * K));
  const px1 = Math.min(W * K, Math.ceil((r.x + r.w + pad) * K)), py1 = Math.min(H * K, Math.ceil((r.y + r.h + pad) * K));
  await isolate(page, part);
  const shot = await page.screenshot({ omitBackground: true, clip: { x: px0 / K, y: py0 / K, width: (px1 - px0) / K, height: (py1 - py0) / K } });
  const t = trim(shot);
  if (!t) return null;
  return { buf: t.buf, px: [px0 + t.x, py0 + t.y, t.w, t.h] };
}

// A piece standing in the scene: its bottom-center in scene units (the scene on the A15: xMidYMax slice of 1000 x 460).
export function sceneAt(px) {
  const s = Math.max(W / 1000, H / 460), sx0 = (W - 1000 * s) / 2, sy0 = H - 460 * s;
  return [+(((px[0] + px[2] / 2) / K - sx0) / s).toFixed(4), +(((px[1] + px[3]) / K - sy0) / s).toFixed(4)];
}

// ---- the scene ----

// A page set up to shoot the whole scene drawing (1000 x 460) at the A15's scale. The page draws the scene
// (W*K - 1000*s)/2 px off the left edge (-3.913 on the A15); Unity can only place a picture on whole pixels (-4).
// The difference is drawn into the picture, so on the A15 it lands where the page has it.
export async function openScenePage(browser) {
  const { ctx, page } = await openMenu(browser, { width: 1000, height: 460 }, SCENE_SCALE);
  const pageOffset = (W * K - 1000 * SCENE_SCALE) / 2, imgW = Math.ceil(1000 * SCENE_SCALE - 1e-6);
  const shift = pageOffset - (W * K - imgW) / 2;
  await page.addStyleTag({ content: `.mm-scene{transform:translateX(${shift / SCENE_SCALE}px) !important}` });
  return { ctx, page, shift };
}

// Draw the scene for a level and season the way the menu does (main-menu-scene.js svg(), animations stripped as
// menu.js still() does), into the page's own scene box. Without its vignette (the darkened edges: the last rect,
// fill url(#…Vig)), which Unity lays over it as a layer of its own: it fades out while the Game Park is beside the
// house (menu.js vignette()), and it's the same for every level and season.
export async function drawScene(page, level, season, { vignette = false } = {}) {
  await page.evaluate(({ level, season, vignette }) => {
    const x = window.SquishMenu._x, Sc = window.MainMenuScene;
    const st = Sc.state({ level, season });
    const svg = Sc.svg(st).replace(/<animate(?:Transform|Motion)?\b[^>]*\/>/g, '');
    x.scene.classList.remove('sq-pic-on', 'sq-pic-hh');
    x.scene.innerHTML = '<div class="sq-live">' + svg + '</div>';
    if (!vignette) x.scene.querySelectorAll('rect[fill$="Vig)"]').forEach(r => r.remove());
  }, { level, season, vignette });
  await page.waitForTimeout(150);
}

// The vignette alone, as it lies over the scene (same size and place as a scene picture).
export async function shootVignette(page) {
  await drawScene(page, 1, 'rainy', { vignette: true });
  await isolate(page, { sel: '.mm-scene', only: 'rect[fill$="Vig)"]' });
  return page.screenshot({ omitBackground: true });
}

// The Game Park's picture for a level and season (main-menu-scene.js svg({ ...st, park: true }), menu.js parkLayer()):
// drawn in a box the size of the screen, to the left of the house, overflowing it (a tree stands on the seam). Shot in
// a page three scenes wide at the A15's scene scale. On the A15 the page draws its drawing from x = -W*K + offset
// (-2343.913 px); the picture is shifted by a fraction of a pixel so that Unity, placing it on whole pixels, lands it
// there. Returns { buf, d, top }: where the picture's left and top edges are, in scene units, from the house
// picture's left edge minus the screen's width (the park box's place in Unity) and from the top.
export async function shootPark(browser, level, season) {
  const { ctx, page } = await openMenu(browser, { width: 3000, height: 460 }, SCENE_SCALE);
  const imgW = Math.ceil(1000 * SCENE_SCALE - 1e-6), houseLeft = (W * K - imgW) / 2;      // -4 px on the A15
  const origin = -W * K + (W * K - 1000 * SCENE_SCALE) / 2;                              // the page's: -2343.913 px
  const B = 1000 * SCENE_SCALE, frac = v => v - Math.floor(v);
  const sigma = frac(frac(origin) - frac(B));                                            // px, so B + sigma lands like origin
  await page.evaluate(({ level, season, sh }) => {
    const Sc = window.MainMenuScene, st = Sc.state({ level, season });
    const svg = Sc.svg(Object.assign({}, st, { park: true })).replace(/<animate(?:Transform|Motion)?\b[^>]*\/>/g, '');
    const d = document.createElement('div'); d.id = 'x-park';
    d.style.cssText = 'position:fixed;left:1000px;top:0;width:1000px;height:460px;z-index:99999;transform:translateX(' + sh + 'px)';
    d.innerHTML = svg; document.body.appendChild(d);
    const sv = d.firstChild; if (sv && sv.style) sv.style.overflow = 'visible';
  }, { level, season, sh: sigma / SCENE_SCALE });
  await page.waitForTimeout(200);
  await isolate(page, { sel: '#x-park' });
  const t = trim(await page.screenshot({ omitBackground: true }));
  await ctx.close();
  const L = origin - (B + sigma) + t.x;                    // where Unity puts the picture's left edge on the A15 (whole px)
  const d = L - (houseLeft - W * K);
  return { buf: t.buf, d: +(d / SCENE_SCALE).toFixed(5), top: +(t.y / SCENE_SCALE).toFixed(5), dPx: +d.toFixed(3) };
}

export async function shootScene(page) {
  await isolate(page, { sel: '.mm-scene' });
  return page.screenshot({ omitBackground: true });
}
