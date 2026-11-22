// The Main Menu, taken apart for Unity: every piece the browser draws, saved as its own PNG, plus layout.json saying
// where each goes. Unity puts them back together (MainMenuScreen.cs), so the screen matches the game pixel for pixel
// on the Galaxy A15 (832 x 384 dp at 2.8125 = 2340 x 1080 px).
//
//   - Fixed pieces (tabs, buttons, PLAY, the crest's medal) are pictures, text included.
//   - Pieces with changing text (coin and gem pills) are pictures with the text taken out; layout.json carries the
//     text's place and style, and Unity draws it.
//   - Pieces that depend on a value are a picture per value: the name plate per creature (its plaque, stars and
//     lettering follow the creature's rarity and name), the crest's number per level, the creature itself, the scene
//     per level and season. This script makes the default state's (level 1, rainy, Mittens);
//     export-menu-states.mjs makes all the others.
//   - The scene is the whole 1000 x 460 drawing at the A15's scale, so Unity can crop it like the page does
//     (preserveAspectRatio xMidYMax slice) on any screen shape.
//   - The weather is not a picture: Unity draws it the way menu.js does (MenuWeather.cs).
//
//   node webgame/build.mjs --out webgame/test/out/webgame
//   node tools/unity-port/export-menu.mjs        -> unity/Assets/_Project/Resources/Screens/MainMenu/
//   then it checks itself: the pieces put back together in the browser, compared with a screenshot of the real menu.
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { PNG, REPO, BUILT, MENU_OUT as OUT, W, H, K, SCENE_SCALE, needBuild, launch, openMenu, openScenePage, shootScene, shootVignette, shootPark, shootPart, showAll, sceneAt } from './lib/menu-page.mjs';

const CHECK = path.join(REPO, 'webgame/test/out/unity-menu-check');

// The pieces, bottom to top. sel + i: which element. hide: what's left out of its picture. only: keep just these
// inside it. anchor: which screen edges it keeps its distance to on other screen shapes (the page's own layout).
// layer: 'world' pieces are under the weather, the rest (the HUD) above it, as in the page.
// anchor 'scene': it stands in the scene (the creature on the path), so it moves with the scene's crop on other
// screen shapes; its size still follows the HUD's. Stored as its bottom-center point in scene units (viewBox).
// value: a picture per value, found through that value's index (see export-menu-states.mjs).
const PARTS = [
  { name: 'creature', sel: '.mm-stage', anchor: 'scene', layer: 'world', value: 'creature-still' },
  { name: 'nameplate', sel: '.mm-name', anchor: 'bottom-center', layer: 'world', value: 'nameplate', image: 'Names/nameplate-mittens' },
  { name: 'tab-house', sel: '.mm-tab', i: 0, anchor: 'top-left' },
  { name: 'tab-characters', sel: '.mm-tab', i: 1, anchor: 'top-left' },
  { name: 'tab-shop', sel: '.mm-tab', i: 2, anchor: 'top-left' },
  { name: 'pill-coins', sel: '.mm-pill', i: 0, hide: ['span'], anchor: 'top-right', text: { key: 'coins', sel: '.mm-pill span', i: 0 } },
  { name: 'pill-gems', sel: '.mm-pill', i: 1, hide: ['span'], anchor: 'top-right', text: { key: 'gems', sel: '.mm-pill span', i: 1 } },
  { name: 'orb-trophies', sel: '.mm-orb', i: 0, anchor: 'top-right' },
  { name: 'orb-settings', sel: '.mm-orb', i: 1, anchor: 'top-right' },
  { name: 'play', sel: '.mm-play', hide: ['.sq-glint'], anchor: 'bottom-left' },
  { name: 'crest', sel: '.sq-crest', hide: ['.sq-spark', 'text.sq-num'], anchor: 'bottom-right' },
  { name: 'crest-level', sel: '.sq-crest', only: 'text.sq-num', anchor: 'bottom-right', value: 'level', image: 'Levels/crest-level-1' },
  ...[0, 1, 2, 3].map(i => ({ name: `crest-spark-${i}`, sel: '.sq-spark', i, anchor: 'bottom-right' })),
];

needBuild();
fs.mkdirSync(OUT, { recursive: true });
fs.mkdirSync(CHECK, { recursive: true });
const browser = await launch();
// the state the default pictures show (the reference screenshots' state): Unity uses these pictures for it, exactly
const layout = { screen: { width: W, height: H, scale: K }, defaults: { creature: 'mittens', level: 1, season: 'rainy' }, scene: null, parts: [], texts: [] };

// ---- 1. the scene: the whole drawing (viewBox 1000 x 460) at the A15's scale for it ----
// (the menu's own drawing, without its vignette: that's a layer of its own, the same for every scene, faded out while
//  the Game Park is beside the house; and the Game Park's picture, to the left of the house)
{
  const { ctx, page, shift } = await openScenePage(browser);
  const vig = await shootVignette(page);
  fs.writeFileSync(path.join(OUT, 'vignette.png'), vig);
  await page.reload(); await page.waitForTimeout(8000);
  for (let t = 0; t < 9000; t += 500) { await page.clock.runFor(500); await page.waitForTimeout(100); }
  await page.evaluate(() => { const x = window.SquishMenu._x; x.scene.classList.remove('sq-pic-on', 'sq-pic-hh'); x.scene.querySelectorAll('rect[fill$="Vig)"]').forEach(r => r.remove()); });
  const buf = await shootScene(page);
  const png = PNG.sync.read(buf);
  fs.writeFileSync(path.join(OUT, 'scene-L1-rainy.png'), buf);
  layout.scene = { image: 'scene-L1-rainy', vignette: 'vignette', viewBox: [1000, 460], align: 'xMidYMax slice', pixels: [png.width, png.height], subpixelShift: +shift.toFixed(4), level: 1, season: 'rainy' };
  console.log(`scene-L1-rainy.png  ${png.width} x ${png.height} (+ vignette.png)`);
  await ctx.close();
  const park = await shootPark(browser, 1, 'rainy');
  fs.writeFileSync(path.join(OUT, 'park-L1-rainy.png'), park.buf);
  layout.park = { image: 'park-L1-rainy', d: park.d, top: park.top, sceneScale: +SCENE_SCALE.toFixed(6) };
  console.log(`park-L1-rainy.png (left edge ${park.dPx} px from the park box)`);
}

// ---- 2. the pieces, at the A15's size ----
const { ctx, page } = await openMenu(browser);
const reference = await page.screenshot();
fs.writeFileSync(path.join(CHECK, 'reference.png'), reference);
// weather is drawn by Unity, so the self-check compares against the menu without it
const noWx = await page.addStyleTag({ content: '.sq-wx{visibility:hidden !important}' });
await page.waitForTimeout(300);
const refNoWx = PNG.sync.read(await page.screenshot());
await noWx.evaluate(n => n.remove());

for (const part of PARTS) {
  const shot = await shootPart(page, part);
  if (!shot) { console.log(`${part.name}: nothing painted (left out)`); continue; }
  const image = part.image || part.name;
  fs.mkdirSync(path.dirname(path.join(OUT, image + '.png')), { recursive: true });
  fs.writeFileSync(path.join(OUT, image + '.png'), shot.buf);
  const [ox, oy, w, h] = shot.px;
  const entry = { name: part.name, image, anchor: part.anchor, layer: part.layer || 'hud', value: part.value || null,
    x: +(ox / K).toFixed(4), y: +(oy / K).toFixed(4), w: +(w / K).toFixed(4), h: +(h / K).toFixed(4), px: shot.px };
  if (part.anchor === 'scene') entry.sceneAt = sceneAt(shot.px);
  layout.parts.push(entry);
  console.log(`${(image + '.png').padEnd(30)} ${String(w).padStart(4)} x ${String(h).padEnd(4)} at ${ox},${oy} px`);

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
await showAll(page);

// ---- 3. the weather's numbers ----
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
// composed in whole screen pixels (2340 x 1080 at 1x), as Unity's layout snaps to them: nothing is resampled
const PX = v => Math.round(v * K);
const sceneW = layout.scene.pixels[0];
const html = `<!doctype html><html><head><style>
  html,body{margin:0;width:${W * K}px;height:${H * K}px;overflow:hidden;background:#24123f}
  img{position:absolute;display:block}
  .t{position:absolute;white-space:nowrap}
</style></head><body>
<img src="scene-L1-rainy.png" style="left:${Math.round((W * K - sceneW) / 2)}px;top:0">
<img src="vignette.png" style="left:${Math.round((W * K - sceneW) / 2)}px;top:0">
${layout.parts.map(p => `<img src="${p.image}.png" style="left:${p.px[0]}px;top:${p.px[1]}px">`).join('\n')}
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
