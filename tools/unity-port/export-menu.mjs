// The Main Menu, taken apart for Unity: every piece the browser draws, saved as its own PNG, plus layout.json saying
// where each goes. Unity puts them back together (MainMenuScreen.cs), so the screen matches the game pixel for pixel
// on the Galaxy A15 (832 x 384 dp at 2.8125 = 2340 x 1080 px).
//
//   - Fixed pieces (tabs, buttons, PLAY, the crest's medal) are pictures, text included.
//   - Pieces with changing text (coin and gem pills) are pictures with the text taken out; layout.json carries the
//     text's place and style, and Unity draws it.
//   - Lettering Unity can't draw (gradient-filled, three-layer outlined: the creature's name, the level number) is a
//     picture per value.
//   - The scene is the whole 1000 x 460 drawing at the A15's scale, so Unity can crop it like the page does
//     (preserveAspectRatio xMidYMax slice) on any screen shape.
//   - The weather is not a picture: Unity draws it the way menu.js does (MenuWeather.cs).
//
// Frozen like the reference screenshots (fake clock, seeded random, animations held), so pieces and references match.
//
//   node webgame/build.mjs --out webgame/test/out/webgame
//   node tools/unity-port/export-menu.mjs     -> unity/Assets/_Project/Resources/Screens/MainMenu/
//   then it checks itself: the pieces put back together in the browser, compared with a screenshot of the real menu.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const { PNG } = require(path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../node_modules/pngjs'));
const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(HERE, '../..');
const BUILT = path.join(REPO, 'webgame/test/out/webgame');
const OUT = path.join(REPO, 'unity/Assets/_Project/Resources/Screens/MainMenu');
const CHECK = path.join(REPO, 'webgame/test/out/unity-menu-check');

const W = 832, H = 384, K = 2.8125;              // the A15, landscape
const T0 = new Date('2026-11-13T12:00:00Z');
const SEED = `(() => { let s = 0x2f6b9a1; Math.random = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; })();`;

// The pieces, bottom to top. sel + i: which element. hide: what's left out of its picture. only: keep just these
// inside it. anchor: which screen edges it keeps its distance to on other screen shapes (the page's own layout).
// layer: 'world' pieces are under the weather, the rest (the HUD) above it, as in the page.
// anchor 'scene': it stands in the scene (the creature on the path), so it moves with the scene's crop on other
// screen shapes; its size still follows the HUD's. Stored as its bottom-center point in scene units (viewBox).
const PARTS = [
  { name: 'creature', sel: '.mm-stage', anchor: 'scene', layer: 'world' },
  { name: 'nameplate', sel: '.mm-name', hide: ['text'], anchor: 'bottom-center', layer: 'world' },
  { name: 'name-mittens', sel: '.mm-name', only: 'text', anchor: 'bottom-center', value: 'name', layer: 'world' },
  { name: 'tab-house', sel: '.mm-tab', i: 0, anchor: 'top-left' },
  { name: 'tab-characters', sel: '.mm-tab', i: 1, anchor: 'top-left' },
  { name: 'tab-shop', sel: '.mm-tab', i: 2, anchor: 'top-left' },
  { name: 'pill-coins', sel: '.mm-pill', i: 0, hide: ['span'], anchor: 'top-right', text: { key: 'coins', sel: '.mm-pill span', i: 0 } },
  { name: 'pill-gems', sel: '.mm-pill', i: 1, hide: ['span'], anchor: 'top-right', text: { key: 'gems', sel: '.mm-pill span', i: 1 } },
  { name: 'orb-trophies', sel: '.mm-orb', i: 0, anchor: 'top-right' },
  { name: 'orb-settings', sel: '.mm-orb', i: 1, anchor: 'top-right' },
  { name: 'play', sel: '.mm-play', hide: ['.sq-glint'], anchor: 'bottom-left' },
  { name: 'crest', sel: '.sq-crest', hide: ['.sq-spark', 'text.sq-num'], anchor: 'bottom-right' },
  { name: 'crest-level-1', sel: '.sq-crest', only: 'text.sq-num', anchor: 'bottom-right', value: 'level' },
  ...[0, 1, 2, 3].map(i => ({ name: `crest-spark-${i}`, sel: '.sq-spark', i, anchor: 'bottom-right' })),
];

if (!fs.existsSync(path.join(BUILT, 'App.dc.html'))) {
  console.error('Build the game pages first: node webgame/build.mjs --out webgame/test/out/webgame');
  process.exit(1);
}
fs.mkdirSync(OUT, { recursive: true });
fs.mkdirSync(CHECK, { recursive: true });
const browser = await chromium.launch({ args: ['--allow-file-access-from-files'] });

async function openMenu(viewport, scale) {
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
  // the level number's two <text>s carry no class of their own: mark them (digits only, inside the crest)
  await page.evaluate(() => document.querySelectorAll('.sq-crest text').forEach(t => { if (/^\d+$/.test(t.textContent.trim())) t.classList.add('sq-num'); }));
  return { ctx, page };
}

// Show only one piece: everything else hidden, the page see-through.
async function isolate(page, part) {
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

// Crop a PNG to its painted pixels. Returns the new PNG and its offset.
function trim(buf) {
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

const layout = { screen: { width: W, height: H, scale: K }, scene: null, parts: [], texts: [] };

// ---- 1. the scene: the whole drawing (viewBox 1000 x 460) at the A15's scale for it ----
{
  const sceneScale = (H / 460) * K; // the slice scale on the A15 (height decides), in px per viewBox unit
  const { ctx, page } = await openMenu({ width: 1000, height: 460 }, sceneScale);
  // The page draws the scene (W*K - 1000*s)/2 px off the left edge (-3.913 on the A15); Unity can only place a picture
  // on whole pixels (-4). Draw the difference into the picture, so on the A15 it lands where the page has it.
  const pageOffset = (W * K - 1000 * sceneScale) / 2, imgW = Math.ceil(1000 * sceneScale - 1e-6);
  const unityOffset = (W * K - imgW) / 2, shift = pageOffset - unityOffset;
  await page.addStyleTag({ content: `.mm-scene{transform:translateX(${shift / sceneScale}px) !important}` });
  await isolate(page, { sel: '.mm-scene' });
  const buf = await page.screenshot({ omitBackground: true });
  const png = PNG.sync.read(buf);
  fs.writeFileSync(path.join(OUT, 'scene-L1-rainy.png'), buf);
  layout.scene = { image: 'scene-L1-rainy', viewBox: [1000, 460], align: 'xMidYMax slice', pixels: [png.width, png.height], subpixelShift: +shift.toFixed(4), level: 1, season: 'rainy' };
  console.log(`scene-L1-rainy.png  ${png.width} x ${png.height}`);
  await ctx.close();
}

// ---- 2. the pieces, at the A15's size ----
const { ctx, page } = await openMenu({ width: W, height: H }, K);
await page.evaluate(() => document.getElementById('x-iso') && document.getElementById('x-iso').remove());
const reference = await page.screenshot();
fs.writeFileSync(path.join(CHECK, 'reference.png'), reference);
// weather is drawn by Unity, so the self-check compares against the menu without it
const noWx = await page.addStyleTag({ content: '.sq-wx{visibility:hidden !important}' });
await page.waitForTimeout(300);
const refNoWx = PNG.sync.read(await page.screenshot());
await noWx.evaluate(n => n.remove());

for (const part of PARTS) {
  // the element's box, grown by 32 dp for shadows and glows, snapped out to whole screen pixels
  const r = await page.evaluate(p => { const b = document.querySelectorAll(p.sel)[p.i || 0].getBoundingClientRect(); return { x: b.x, y: b.y, w: b.width, h: b.height }; }, part);
  const pad = 32;
  const px0 = Math.max(0, Math.floor((r.x - pad) * K)), py0 = Math.max(0, Math.floor((r.y - pad) * K));
  const px1 = Math.min(W * K, Math.ceil((r.x + r.w + pad) * K)), py1 = Math.min(H * K, Math.ceil((r.y + r.h + pad) * K));
  await isolate(page, part);
  const shot = await page.screenshot({ omitBackground: true, clip: { x: px0 / K, y: py0 / K, width: (px1 - px0) / K, height: (py1 - py0) / K } });
  const t = trim(shot);
  if (!t) { console.log(`${part.name}: nothing painted (left out)`); continue; }
  fs.writeFileSync(path.join(OUT, part.name + '.png'), t.buf);
  const ox = px0 + t.x, oy = py0 + t.y;
  const entry = { name: part.name, image: part.name, anchor: part.anchor, layer: part.layer || 'hud', value: part.value || null,
    x: +(ox / K).toFixed(4), y: +(oy / K).toFixed(4), w: +(t.w / K).toFixed(4), h: +(t.h / K).toFixed(4), px: [ox, oy, t.w, t.h] };
  if (part.anchor === 'scene') {
    // the scene on the A15: xMidYMax slice of 1000 x 460
    const s = Math.max(W / 1000, H / 460), sx0 = (W - 1000 * s) / 2, sy0 = H - 460 * s;
    entry.sceneAt = [+(((ox + t.w / 2) / K - sx0) / s).toFixed(4), +(((oy + t.h) / K - sy0) / s).toFixed(4)];
  }
  layout.parts.push(entry);
  console.log(`${(part.name + '.png').padEnd(24)} ${String(t.w).padStart(4)} x ${String(t.h).padEnd(4)} at ${ox},${oy} px`);

  if (part.text) {
    const tx = await page.evaluate(q => {
      const e = document.querySelectorAll(q.sel)[q.i || 0], b = e.getBoundingClientRect(), c = getComputedStyle(e);
      const shadows = c.textShadow === 'none' ? [] : c.textShadow.split(/,(?![^(]*\))/).map(s => {
        const col = (s.match(/rgba?\([^)]*\)/) || ['rgb(0,0,0)'])[0], nums = s.replace(col, '').trim().split(/\s+/).map(parseFloat);
        return { color: col, x: nums[0] || 0, y: nums[1] || 0, blur: nums[2] || 0 };
      });
      return { text: e.textContent.trim(), x: b.x, y: b.y, w: b.width, h: b.height, font: c.fontFamily.split(',')[0].replace(/["']/g, '').trim(),
        weight: +c.fontWeight, size: parseFloat(c.fontSize), color: c.color, letterSpacing: parseFloat(c.letterSpacing) || 0, shadows };
    }, part.text);
    layout.texts.push({ key: part.text.key, parent: part.name, anchor: part.anchor, ...tx });
  }
}
await page.evaluate(() => document.getElementById('x-iso') && document.getElementById('x-iso').remove());

// ---- 3. the weather's numbers, for MenuWeather.cs to check against ----
// The drops come from menu.js's own formulas (the same code, the same engine), so Unity draws exactly these.
layout.weather = await page.evaluate(() => {
  const st = window.MainMenuScene.state({});
  const rndS = (i, k) => { const v = Math.sin(i * 127.1 + k * 311.7) * 43758.5453; return v - Math.floor(v); };
  const r1 = x => Math.round(x * 10) / 10;
  const rain = [], snow = [];
  for (let i = 0; i < 28; i++) rain.push({ x: r1(rndS(i, 21) * 1100 - 50), y: r1(rndS(i, 22) * 115), opacity: r1(0.35 + rndS(i, 23) * 0.35) });
  for (let i = 0; i < 23; i++) snow.push({ x: r1(rndS(i, 31) * 1000), y: r1(rndS(i, 32) * 115), r: r1(1 + rndS(i, 33) * 2), opacity: r1(0.6 + rndS(i, 34) * 0.4) });
  // where the moving layer stood when the screenshot was taken (Unity's "freeze like the reference" uses it)
  const m = document.querySelector('.sq-wxm');
  const t = m ? new DOMMatrix(getComputedStyle(m).transform) : new DOMMatrix();
  return {
    season: st.season, f: st.f, level: st.L, bandHeight: 115, bands: [-1, 0, 1, 2, 3],
    rain: { drops: rain, line: [-4, 14], color: '#e8f2ff', width: 1, from: [4, -115], to: [0, 0], ms: 225 },
    snow: { flakes: snow, color: '#ffffff', from: [0, -115], to: [3.5, 0], ms: 3500 },
    frozenOffset: [+t.e.toFixed(3), +t.f.toFixed(3)],
  };
});

fs.writeFileSync(path.join(OUT, 'layout.json'), JSON.stringify(layout, null, 1));
console.log(`layout.json: ${layout.parts.length} pieces, ${layout.texts.length} texts`);

// ---- 4. self-check: put the pieces back together in the browser, compare with the real menu ----
const parts = layout.parts.filter(p => p.value !== 'level' || p.name === 'crest-level-1');
// composed in whole screen pixels (2340 x 1080 at 1x), as Unity's layout snaps to them: nothing is resampled
const PX = v => Math.round(v * K);
const sceneW = layout.scene.pixels[0];
const html = `<!doctype html><html><head><style>
  html,body{margin:0;width:${W * K}px;height:${H * K}px;overflow:hidden;background:#24123f}
  img{position:absolute;display:block}
  .t{position:absolute;white-space:nowrap}
</style></head><body>
<img src="scene-L1-rainy.png" style="left:${Math.round((W * K - sceneW) / 2)}px;top:0">
${parts.map(p => `<img src="${p.image}.png" style="left:${p.px[0]}px;top:${p.px[1]}px">`).join('\n')}
${layout.texts.map(t => `<div class="t" style="left:${PX(t.x)}px;top:${PX(t.y)}px;font:${t.weight} ${t.size * K}px ${t.font};letter-spacing:${t.letterSpacing * K}px;line-height:${t.h * K}px;color:${t.color};text-shadow:${t.shadows.map(s => `${s.color} ${s.x * K}px ${s.y * K}px ${s.blur * K}px`).join(',')}">${t.text}</div>`).join('\n')}
</body></html>`;
// the game's own Fredoka for the drawn texts
const fontCss = fs.readdirSync(path.join(BUILT, 'vendor/css')).map(f => fs.readFileSync(path.join(BUILT, 'vendor/css', f), 'utf8')).join('\n')
  .replace(/url\(([^)]+)\)/g, (m, u) => `url(${pathToFileURL(path.join(BUILT, 'vendor', u.replace(/["']/g, '').replace(/^\.\.\//, ''))).href})`);
fs.writeFileSync(path.join(OUT, '_check.html'), html.replace('</style>', fontCss + '</style>'));
const cctx = await browser.newContext({ viewport: { width: W * K, height: H * K }, deviceScaleFactor: 1 });
const cpage = await cctx.newPage();
await cpage.goto(pathToFileURL(path.join(OUT, '_check.html')).href);
await cpage.evaluate(() => document.fonts.ready);
await cpage.waitForTimeout(500);
const composed = PNG.sync.read(await cpage.screenshot());
fs.unlinkSync(path.join(OUT, '_check.html'));
let diff = 0, big = 0;
const dpng = new PNG({ width: refNoWx.width, height: refNoWx.height });
for (let i = 0; i < refNoWx.data.length; i += 4) {
  const d = Math.max(Math.abs(refNoWx.data[i] - composed.data[i]), Math.abs(refNoWx.data[i + 1] - composed.data[i + 1]), Math.abs(refNoWx.data[i + 2] - composed.data[i + 2]));
  if (d > 2) diff++;
  if (d > 40) big++;
  dpng.data[i] = dpng.data[i + 1] = dpng.data[i + 2] = d > 2 ? Math.min(255, 60 + d * 3) : 0; dpng.data[i + 3] = 255;
}
fs.writeFileSync(path.join(CHECK, 'reference-no-weather.png'), PNG.sync.write(refNoWx));
fs.writeFileSync(path.join(CHECK, 'composed.png'), PNG.sync.write(composed));
fs.writeFileSync(path.join(CHECK, 'diff.png'), PNG.sync.write(dpng));
const n = refNoWx.width * refNoWx.height;
console.log(`self-check: ${(100 * diff / n).toFixed(3)}% of pixels differ (>2/255), ${(100 * big / n).toFixed(3)}% clearly (>40/255)  -> ${path.relative(REPO, CHECK)}/diff.png`);
await browser.close();
