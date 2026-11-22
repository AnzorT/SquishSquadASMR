// The Main Menu's moving HUD pieces, taken apart so Unity can animate them as the page does (webgame/app/menu.js):
//   the crest: every layer of its SVG on its own (wings, star, medal, shine, banner), each with its own share of the
//     crest's drop shadow, plus its glow and the sparkle; the level number comes from export-menu-states.mjs;
//   PLAY: its glow (the card's ::before) and its glint band (.sq-glint::after, skew drawn in: Unity can't skew).
// Positions are in A15 screen pixels relative to the piece's own box (the crest's .sq-cin, the PLAY button), with
// each animation's pivot. -> Resources/Screens/MainMenu/Hud/ + hud.json
//
// The shadow: CSS drop-shadow(0 4px 4px rgba(20,0,50,.45)) on the crest's SVG, drawn by the browser per layer (see
// browserShadow), so a flapping wing's shadow flaps with it. Checked at the end against the real crest.
import fs from 'node:fs';
import path from 'node:path';
import { PNG, REPO, MENU_OUT, W, H, K, needBuild, launch, openMenu, isolate, showAll, trim } from './lib/menu-page.mjs';

const OUT = path.join(MENU_OUT, 'Hud');
const CHECK = path.join(REPO, 'webgame/test/out/unity-menu-check');
needBuild();
fs.mkdirSync(OUT, { recursive: true });
const browser = await launch();
const { ctx, page } = await openMenu(browser);
const hud = {};

// a reference of the crest region before anything is changed (the menu without its weather layer)
await page.addStyleTag({ content: '.sq-wx{visibility:hidden !important}' });
await page.waitForTimeout(200);

const box = (sel, i = 0) => page.evaluate(({ sel, i }) => { const b = document.querySelectorAll(sel)[i].getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; }, { sel, i });

// Shoot a piece inside a box (css px), trimmed; returns { buf, png, px: [x, y, w, h] } in screen pixels.
async function shoot(part, clipCss, pad = 24) {
  const [x, y, w, h] = clipCss;
  const px0 = Math.max(0, Math.floor((x - pad) * K)), py0 = Math.max(0, Math.floor((y - pad) * K));
  const px1 = Math.min(W * K, Math.ceil((x + w + pad) * K)), py1 = Math.min(H * K, Math.ceil((y + h + pad) * K));
  await isolate(page, part);
  const shot = await page.screenshot({ omitBackground: true, clip: { x: px0 / K, y: py0 / K, width: (px1 - px0) / K, height: (py1 - py0) / K } });
  const t = trim(shot);
  if (!t) return null;
  return { buf: t.buf, png: PNG.sync.read(t.buf), px: [px0 + t.x, py0 + t.y, t.w, t.h] };
}

// A layer's drop shadow, as the browser makes it: alpha blurred (Gaussian, sigma in px), moved (dx, dy), one colour.
function dropShadow(layer, sigma, dx, dy, rgb, alpha) {
  const r = Math.ceil(sigma * 3), ox = Math.floor(dx), oy = Math.floor(dy);
  const w = layer.png.width + 2 * r + 2, h = layer.png.height + 2 * r + 2;
  const a = new Float32Array(w * h);
  // the layer's alpha, placed with the sub-pixel part of the offset (bilinear)
  const fx = dx - ox, fy = dy - oy;
  for (let y = 0; y < layer.png.height; y++) for (let x = 0; x < layer.png.width; x++) {
    const v = layer.png.data[(y * layer.png.width + x) * 4 + 3] / 255;
    if (!v) continue;
    const X = x + r + 1, Y = y + r + 1;
    a[Y * w + X] += v * (1 - fx) * (1 - fy); a[Y * w + X + 1] += v * fx * (1 - fy);
    a[(Y + 1) * w + X] += v * (1 - fx) * fy; a[(Y + 1) * w + X + 1] += v * fx * fy;
  }
  const k = [], n = Math.ceil(sigma * 3);
  let sum = 0; for (let i = -n; i <= n; i++) { const g = Math.exp(-(i * i) / (2 * sigma * sigma)); k.push(g); sum += g; }
  for (let i = 0; i < k.length; i++) k[i] /= sum;
  const tmp = new Float32Array(w * h), out = new Float32Array(w * h);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { let s = 0; for (let i = -n; i <= n; i++) { const xx = x + i; if (xx >= 0 && xx < w) s += a[y * w + xx] * k[i + n]; } tmp[y * w + x] = s; }
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { let s = 0; for (let i = -n; i <= n; i++) { const yy = y + i; if (yy >= 0 && yy < h) s += tmp[yy * w + x] * k[i + n]; } out[y * w + x] = s; }
  const png = new PNG({ width: w, height: h });
  for (let i = 0; i < w * h; i++) { png.data[i * 4] = rgb[0]; png.data[i * 4 + 1] = rgb[1]; png.data[i * 4 + 2] = rgb[2]; png.data[i * 4 + 3] = Math.round(Math.min(1, out[i]) * alpha * 255); }
  const t = trim(PNG.sync.write(png));
  return { buf: t.buf, px: [layer.px[0] + ox - r - 1 + t.x, layer.px[1] + oy - r - 1 + t.y, t.w, t.h] };
}

// The same shadow drawn by the browser itself: the layer shot with its drop shadow and without, in the same box; the
// one with it is the layer laid over its shadow, so the shadow is solved for, pixel by pixel (premultiplied colours).
// Exact wherever the shadow can be seen; under the layer it's covered by the layer anyway.
async function browserShadow(part, clipCss, svgSel, shadowCss) {
  const [x, y, w, h] = clipCss, pad = 30;
  const px0 = Math.max(0, Math.floor((x - pad) * K)), py0 = Math.max(0, Math.floor((y - pad) * K));
  const px1 = Math.min(W * K, Math.ceil((x + w + pad) * K)), py1 = Math.min(H * K, Math.ceil((y + h + pad) * K));
  const clip = { x: px0 / K, y: py0 / K, width: (px1 - px0) / K, height: (py1 - py0) / K };
  await isolate(page, part); // (drops the group's drop shadow: the layer alone)
  const bare = PNG.sync.read(await page.screenshot({ omitBackground: true, clip }));
  await page.evaluate(({ svgSel, f }) => { const e = document.querySelector(svgSel); e.style.setProperty('filter', f, 'important'); e.setAttribute('data-x-filter', ''); }, { svgSel, f: shadowCss });
  const lit = PNG.sync.read(await page.screenshot({ omitBackground: true, clip }));
  const out = new PNG({ width: bare.width, height: bare.height });
  for (let i = 0; i < bare.data.length; i += 4) {
    const a0 = bare.data[i + 3] / 255, a1 = lit.data[i + 3] / 255;
    if (a0 >= 0.995) continue;                                  // fully under the layer: never seen
    const sa = Math.min(1, Math.max(0, (a1 - a0) / (1 - a0)));
    if (sa <= 0) continue;
    for (let c = 0; c < 3; c++) {
      const p1 = lit.data[i + c] / 255 * a1, p0 = bare.data[i + c] / 255 * a0;
      out.data[i + c] = Math.round(Math.min(1, Math.max(0, (p1 - p0) / (1 - a0) / sa)) * 255);
    }
    out.data[i + 3] = Math.round(sa * 255);
  }
  const t = trim(PNG.sync.write(out));
  return t && { buf: t.buf, px: [px0 + t.x, py0 + t.y, t.w, t.h] };
}

const save = (name, buf) => fs.writeFileSync(path.join(OUT, name + '.png'), buf);
const rel = (px, origin) => [px[0] - origin[0], px[1] - origin[1], px[2], px[3]];

// ---------------- the crest ----------------
{
  const cin = await box('.sq-crest .sq-cin');           // the box that floats (sqFloat), css px
  const svgB = await box('.sq-crest svg.mm-crest');
  const crest = await box('.sq-crest');
  const unit = svgB[2] / 120;                          // css px per SVG unit (viewBox 0 0 120 104)
  const cinPx = [Math.round(cin[0] * K), Math.round(cin[1] * K), Math.round(cin[2] * K), Math.round(cin[3] * K)];
  const svgOrigin = [(svgB[0] - cin[0]) * K, (svgB[1] - cin[1]) * K];     // the SVG's corner inside the crest box, px
  const P = (ux, uy) => [+(svgOrigin[0] + ux * unit * K).toFixed(3), +(svgOrigin[1] + uy * unit * K).toFixed(3)]; // SVG units -> px in the crest box

  // the crest's layers, bottom to top, each alone (sel inside the crest SVG); the shine is shot without its clip, centred
  await page.evaluate(() => {
    const g = document.querySelector('.sq-crest g.sq-sweep');
    g.parentNode.setAttribute('data-clip', g.parentNode.getAttribute('clip-path')); g.parentNode.removeAttribute('clip-path');
    g.style.setProperty('transform', 'translateX(0)', 'important'); g.style.setProperty('animation', 'none', 'important');
  });
  const LAYERS = [
    { name: 'wingL', only: 'g.sq-wing1', pivot: [44, 50], shadow: true },
    { name: 'wingR', only: 'g.sq-wing-1', pivot: [76, 50], shadow: true },
    { name: 'star', only: 'g.sq-cstar', pivot: [60, 18], shadow: true },
    { name: 'medalBack', only: 'svg.mm-crest > circle[r="31"], svg.mm-crest > circle[r="1.7"]', shadow: true },
    { name: 'medalFace', only: 'svg.mm-crest > circle[r="23"], svg.mm-crest > ellipse', shadow: false },
    { name: 'shine', only: 'g.sq-sweep', shadow: false },
    { name: 'banner', only: 'svg.mm-crest > path, svg.mm-crest > text:not(.sq-num)', shadow: true },
  ];
  const layers = [];
  for (const L of LAYERS) {
    const s = await shoot({ sel: '.sq-crest .sq-cin', only: L.only }, cin, 40);
    if (!s) { console.log(`crest ${L.name}: nothing drawn`); continue; }
    save('crest-' + L.name, s.buf);
    const entry = { name: L.name, image: 'Hud/crest-' + L.name, px: rel(s.px, cinPx) };
    if (L.pivot) entry.pivot = P(...L.pivot);
    if (L.shadow) {
      const sh = await browserShadow({ sel: '.sq-crest .sq-cin', only: L.only }, cin, '.sq-crest svg.mm-crest', 'drop-shadow(0px 4px 4px rgba(20,0,50,.45))');
      save('crest-' + L.name + '-shadow', sh.buf);
      entry.shadow = { image: 'Hud/crest-' + L.name + '-shadow', px: rel(sh.px, cinPx) };
    }
    layers.push(entry);
    console.log(`crest-${L.name}.png ${s.px[2]} x ${s.px[3]}${L.shadow ? ' + shadow' : ''}`);
  }
  await page.evaluate(() => {
    const g = document.querySelector('.sq-crest g.sq-sweep');
    g.parentNode.setAttribute('clip-path', g.parentNode.getAttribute('data-clip'));
    g.style.removeProperty('transform'); g.style.removeProperty('animation');
  });

  // the glow behind it (.sq-crest::before) and one sparkle (all four are the same picture)
  const glow = await shoot({ sel: '.sq-crest', hide: ['.sq-cin', '.sq-spark', '.sq-badge'] }, crest, 8);
  save('crest-glow', glow.buf);
  await page.addStyleTag({ content: '.sq-spark{animation:none !important;opacity:1 !important}' });
  const sparks = await page.evaluate(() => [...document.querySelectorAll('.sq-spark')].map(s => {
    const b = s.getBoundingClientRect(); return { box: [b.x, b.y, b.width, b.height], delay: parseFloat(s.style.animationDelay) || 0 };
  }));
  const sp = await shoot({ sel: '.sq-spark', i: 0 }, sparks[0].box, 4);
  save('crest-spark', sp.buf);
  const sx = sp.px[0] - Math.round(sparks[0].box[0] * K), sy = sp.px[1] - Math.round(sparks[0].box[1] * K);

  hud.crest = {
    // the crest's own box (anchored bottom-right like the crest), its floating part inside it, the SVG scale
    box: cinPx, unit: +(unit * K).toFixed(5),
    float: { origin: [+(0.5 * cin[2] * K).toFixed(3), +(0.62 * cin[3] * K).toFixed(3)] },  // .sq-cin transform-origin 50% 62%
    layers,
    // the shine slides inside the medal's clip circle (60, 58, r 30), from -80 to 80 SVG units (sqShine)
    shine: { clipCenter: P(60, 58), clipRadius: +(30 * unit * K).toFixed(3), from: -80 * unit * K, to: 80 * unit * K },
    glow: { image: 'Hud/crest-glow', px: rel(glow.px, cinPx), opacity: 1 },   // shot at its own opacity (.45): it never changes
    sparks: sparks.map(s => ({ image: 'Hud/crest-spark', px: [Math.round(s.box[0] * K) + sx - cinPx[0], Math.round(s.box[1] * K) + sy - cinPx[1], sp.px[2], sp.px[3]], delay: s.delay })),
  };
}

// ---------------- PLAY ----------------
{
  const btn = await box('.mm-play');
  const btnPx = [Math.round(btn[0] * K), Math.round(btn[1] * K), Math.round(btn[2] * K), Math.round(btn[3] * K)];
  const radius = await page.evaluate(() => parseFloat(getComputedStyle(document.querySelector('.mm-play')).borderTopLeftRadius) || 0);
  // the glow: the card's ::before, at full strength (Unity sets its opacity: .4, and .15 -> .85 -> .15 in a flourish)
  await page.addStyleTag({ content: '.mm-card.sq-solo::before{animation:none !important;opacity:1 !important}' });
  const card = await box('.mm-card.sq-solo');
  const glow = await shoot({ sel: '.mm-card.sq-solo', hide: ['.mm-play'] }, card, 24);
  save('play-glow', glow.buf);
  // the glint band, unclipped, at translateX(0) with its skew
  await page.addStyleTag({ content: '.sq-glint{overflow:visible !important} .sq-glint::after{animation:none !important;transform:translateX(0) skewX(-18deg) !important}' });
  const band = await page.evaluate(() => { const g = document.querySelector('.sq-glint'), b = g.getBoundingClientRect(); return { w: b.width * 0.26, box: [b.x, b.y, b.width, b.height] }; });
  const glint = await shoot({ sel: '.sq-glint' }, band.box, 20);
  save('play-glint', glint.buf);
  hud.play = {
    box: btnPx, radius: +(radius * K).toFixed(3),
    glow: { image: 'Hud/play-glow', px: rel(glow.px, btnPx), opacity: 0.4 },
    // sqSweep: translateX(-160% -> 490%) of the band's own width
    glint: { image: 'Hud/play-glint', px: rel(glint.px, btnPx), from: -1.6 * band.w * K, to: 4.9 * band.w * K },
  };
}

// ---------------- the XP ring and the prize badge (they need the player's data: stand-in data here) ----------------
// The ring (menu.js SquishMenu.crest): a brown track and a cream-to-gold arc of (into / need) of the circle, from 12
// o'clock clockwise, between the medal's rim and its face. A picture per percent. The badge: the number of prizes
// waiting on the Level Pass, 1..9 or "9+", over the crest's top right corner (it doesn't float).
{
  const cinPx = hud.crest.box;
  const draw = (into, claimable) => page.evaluate(({ into, claimable }) => {
    const A = window.SquishApp;
    A.state.xp = { level: 1, into, need: 100, per: 100 };
    A.state.road = Array.from({ length: Math.max(1, claimable) }, (_, i) => ({ lv: i + 1, k: 'coins', n: 10, t: null, got: false, claimable: i < claimable }));
    document.querySelector('.sq-crest').innerHTML = window.SquishMenu.crest(1, window.MainMenu.crest);
  }, { into, claimable });
  await showAll(page);
  await draw(50, 0); await draw(50, 0); // (the ring's first drawing animates its fill: the second is still)
  const cin = await box('.sq-crest .sq-cin');
  const track = await shoot({ sel: '.sq-crest .sq-cin', only: 'circle[r="27.2"]:not([stroke-dasharray])' }, cin, 10);
  save('crest-ring-track', track.buf);
  const fills = [];
  for (let p = 1; p <= 100; p++) {
    await draw(p, 0);
    const s = await shoot({ sel: '.sq-crest .sq-cin', only: 'circle[stroke-dasharray]' }, cin, 10);
    if (!s) continue;
    save(`crest-ring-${p}`, s.buf);
    fills.push({ key: String(p), px: rel(s.px, cinPx) });
  }
  const badges = [];
  for (let n = 1; n <= 10; n++) {
    await draw(50, n);
    const b = await box('.sq-crest .sq-badge');
    const s = await shoot({ sel: '.sq-crest .sq-badge' }, b, 10);
    const key = n > 9 ? '9+' : String(n);
    save(`crest-badge-${n > 9 ? '9plus' : n}`, s.buf);
    badges.push({ key, image: `Hud/crest-badge-${n > 9 ? '9plus' : n}`, px: rel(s.px, cinPx) });
  }
  hud.crest.ring = { track: { image: 'Hud/crest-ring-track', px: rel(track.px, cinPx) }, fills, fillIn: { delay: 0.3, duration: 1.4, easing: 'cubic-bezier(.2,.9,.25,1)' } };
  hud.crest.badges = badges;
  console.log(`crest ring: track + ${fills.length} fills; badges: ${badges.length}`);
}

hud.flourish = { every: 5.2, crestAt: 0, playAt: 1.3, duration: 0.9, easing: 'ease-in-out' };
fs.writeFileSync(path.join(MENU_OUT, 'hud.json'), JSON.stringify(hud, null, 1));
console.log('hud.json written');

// ---- self-check: the crest put back together at rest (layers + shadows), against the real crest ----
await showAll(page);
await page.reload(); // a fresh, untouched menu
await page.waitForTimeout(8000);
for (let t = 0; t < 9000; t += 500) { await page.clock.runFor(500); await page.waitForTimeout(100); }
await isolate(page, { sel: '.sq-crest', hide: ['.sq-spark'] });   // the crest alone, see-through (sparkles: checked apart)
await page.waitForTimeout(300);
const C = hud.crest, [bx, by, bw, bh] = C.box, pad = 60;
const ref = PNG.sync.read(await page.screenshot({ omitBackground: true, clip: { x: (bx - pad) / K, y: (by - pad) / K, width: (bw + 2 * pad) / K, height: (bh + 2 * pad) / K } }));
const comp = new PNG({ width: ref.width, height: ref.height });
const over = (file, px) => {
  const im = PNG.sync.read(fs.readFileSync(path.join(MENU_OUT, file + '.png')));
  for (let y = 0; y < im.height; y++) for (let x = 0; x < im.width; x++) {
    const X = px[0] + pad + x, Y = px[1] + pad + y; if (X < 0 || Y < 0 || X >= comp.width || Y >= comp.height) continue;
    const s = (y * im.width + x) * 4, d = (Y * comp.width + X) * 4, a = im.data[s + 3] / 255, da = comp.data[d + 3] / 255, oa = a + da * (1 - a);
    for (let c = 0; c < 3; c++) comp.data[d + c] = oa ? Math.round((im.data[s + c] * a + comp.data[d + c] * da * (1 - a)) / oa) : 0;
    comp.data[d + 3] = Math.round(oa * 255);
  }
};
over(C.glow.image, C.glow.px);
for (const L of C.layers) if (L.shadow) over(L.shadow.image, L.shadow.px);
for (const L of C.layers) if (L.name !== 'shine') over(L.image, L.px);
over('Levels/crest-level-1', (() => { const it = JSON.parse(fs.readFileSync(path.join(MENU_OUT, 'Levels/index.json'))).items[0]; return [it.px[0] - bx, it.px[1] - by]; })());
// compared as they'd look on screen: each laid over the same backgrounds (a see-through pixel's colour means nothing)
let diff = 0, big = 0, n = 0;
for (const bg of [[128, 128, 128], [40, 20, 70], [230, 210, 160]]) for (let i = 0; i < ref.data.length; i += 4) {
  if (!ref.data[i + 3] && !comp.data[i + 3]) continue; n++;
  const ra = ref.data[i + 3] / 255, ca = comp.data[i + 3] / 255;
  const d = Math.max(...[0, 1, 2].map(c => Math.abs((ref.data[i + c] * ra + bg[c] * (1 - ra)) - (comp.data[i + c] * ca + bg[c] * (1 - ca)))));
  if (d > 2) diff++; if (d > 40) big++;
}
fs.writeFileSync(path.join(CHECK, 'crest-reference.png'), PNG.sync.write(ref));
fs.writeFileSync(path.join(CHECK, 'crest-composed.png'), PNG.sync.write(comp));
console.log(`crest self-check: ${(100 * diff / n).toFixed(2)}% of its pixels differ (>2/255), ${(100 * big / n).toFixed(2)}% clearly (>40/255)`);
await browser.close();
