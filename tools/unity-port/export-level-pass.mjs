// The Main Menu's Level Pass (webgame/app/menu.js M.levels, opened by tapping the crest), taken apart for Unity:
// every piece the browser draws, saved as its own PNG, plus layout.json saying where each goes and the page's own
// layout numbers, measured in the page on the Galaxy A15 (832 x 384 dp at 2.8125 = 2340 x 1080 px). Unity puts them
// back together (Scripts/App/LevelPass/LevelPassScreen.cs) for any player's state.
//
//   - The page is laid out in "design px" (an 852 x 393 box scaled by k = height / 393). layout.json keeps the page's
//     numbers in design px; every picture's place is in A15 px, relative to the box of the element it belongs to
//     (`off`), so Unity puts it on the same whole pixel as the page.
//   - Fixed pieces are pictures, their fixed text included (the back orb, the "Level Pass" lettering, CLAIM, the tags).
//   - Lettering (gradient-filled, outlined: ft()) is a picture per string: "LEVEL n" gold and white for every level
//     on the road, every reward title the road, the chests and the rooms can produce.
//   - Plain text that changes (LV n, the XP, the coins, CLAIM ALL (n), level numbers, the lines under the levels and
//     rewards) is drawn by Unity: its font, size, colour, spacing and hard shadows are in layout.json. The rewards'
//     descriptions also carry the browser's own line breaks.
//   - Pieces whose width changes are cut in three: a left part, a middle, a right cap. Plain ones ("slice": the bars,
//     the rail, CLAIM ALL, the reward card) stretch a 4 px strip of their uniform middle. Wooden ones ("crop": the LV
//     and coin pills, the panel) have the wood's stripes, which can't stretch: their left part is cut to length from
//     one drawn long, so every stripe stays where the page draws it.
//   - Pieces that move along the track (the level nodes, their badges, the "you" frame) are drawn at 4 sub-pixel
//     phases; Unity picks the one nearest to where the piece lands, so edges stay as crisp as the page's.
//
//   node webgame/build.mjs --out webgame/test/out/webgame
//   node tools/unity-port/export-level-pass.mjs     -> unity/Assets/_Project/Resources/Screens/LevelPass/
//   It also writes the reference screenshots (Reference/game/level-pass*.jpg) with the same stand-in player, and
//   checks itself: the pieces put back together in the browser by the same rules Unity uses, against the real page.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import { PNG, REPO, BUILT, W, H, K, needBuild, launch, openMenu, isolate, showAll, trim } from './lib/menu-page.mjs';

const require = createRequire(import.meta.url);
const squad = require(path.join(REPO, 'functions/squad.js'));
const OUT = path.join(REPO, 'unity/Assets/_Project/Resources/Screens/LevelPass');
const REF = path.join(REPO, 'unity/Assets/_Project/Reference/game');
const CHECK = path.join(REPO, 'webgame/test/out/unity-level-pass-check');
const DESIGN_H = 393;
const WIDE = 1440;                  // dp: the viewport the long pieces are drawn in (panel <= 4096 px)
const PHASES = [0, 0.25, 0.5, 0.75];

// ---------------- the stand-in player (the app's data, src/game/player.js gamePlayer: xp, road) ----------------
// Level 6, 420 of 700 XP into it; the default road (functions/squad.js DEFAULT_LEVELS) with levels 1, 2 and 4
// claimed, so 3, 5 and 6 wait; the chests are today's first HOT group's (prizeGroup) on the frozen clock's day.
const CHEST_NAMES = JSON.parse(fs.readFileSync(path.join(REPO, 'src/squad/groupChestArt.js'), 'utf8').match(/export const CHEST_NAMES = (\{.*?\});/)[1]);
const SIZES = ['common', 'rare', 'epic', 'legendary', 'set'];
const CHEST_NAME = { common: 'Common', rare: 'Rare', epic: 'Epic', legendary: 'Legendary', set: 'Rainbow' };
const DAY = '2026-11-13';   // lib/menu-page.mjs T0
function roadFor(level, got) {   // src/game/player.js roadPrizes + src/playerXp.js roadView
  return squad.DEFAULT_LEVELS.road.map((r, i) => {
    const lv = i + 1, reached = level >= lv, t = r.t ? squad.givenSize(r.t) : null;
    const g = r.k === 'chest' ? squad.prizeGroup(r, DAY) : null, grp = g && squad.GROUPS.find(x => x.key === g);
    return { lv, k: r.k, n: r.n || 0, t, g, gn: grp ? grp.name : null, cn: g && t ? (CHEST_NAMES[g] || [])[squad.SIZE_I[t]] || null : null,
      got: reached && got.includes(lv), claimable: reached && !got.includes(lv) };
  });
}
const STAND_IN = { xp: { level: 6, into: 420, need: 700, per: 100 }, road: roadFor(6, [1, 2, 4]), coins: 1250, creature: 'mittens' };

needBuild();
fs.mkdirSync(OUT, { recursive: true });
fs.mkdirSync(CHECK, { recursive: true });
fs.mkdirSync(REF, { recursive: true });
for (const f of fs.readdirSync(OUT)) if (f.endsWith('.png') || f.endsWith('.json')) fs.unlinkSync(path.join(OUT, f));
for (const d of ['Nodes', 'Lettering', 'Titles', 'Chests', 'Rooms', 'You', 'Tags']) fs.rmSync(path.join(OUT, d), { recursive: true, force: true });

const browser = await launch();
const { ctx, page } = await openMenu(browser);
// (still pictures: the 0.18 s fade-in is shown finished; the claim bob stays at its first frame, as in the references)
await page.addStyleTag({ content: '.sq-pass,.sq-pass *{transition:none !important}' });
const k = await page.evaluate(() => window.SquishMenu._x.k);
const S = k * K;   // A15 px per design px
const r3 = v => +v.toFixed(3);
const dpx = v => r3(v / k);   // viewport dp -> design px

await page.evaluate(() => {
  // a text's look as the browser computes it (sizes in design px: the element's own CSS px)
  window.__rt = (e, box) => {
    const b = box || (() => { const r = e.getBoundingClientRect(); return [r.x, r.y, r.width, r.height]; })(), c = getComputedStyle(e);
    const shadows = c.textShadow === 'none' ? [] : c.textShadow.split(/,(?![^(]*\))/).map(s => {
      const col = (s.match(/rgba?\([^)]*\)/) || ['rgb(0, 0, 0)'])[0], n = s.replace(col, '').trim().split(/\s+/).map(parseFloat);
      return { color: col, x: n[0] || 0, y: n[1] || 0, blur: n[2] || 0 };
    });
    return { text: (box ? [...e.childNodes].filter(n => n.nodeType === 3).map(n => n.textContent).join('') : e.textContent).trim(), box: b, spec: { font: c.fontFamily.split(',')[0].replace(/["']/g, '').trim(), weight: +c.fontWeight,
      size: parseFloat(c.fontSize), color: c.color, letterSpacing: parseFloat(c.letterSpacing) || 0, lineHeight: c.lineHeight === 'normal' ? 0 : parseFloat(c.lineHeight), shadows } };
  };
  // the box of an element's own text (a button's: its text node)
  window.__tn = e => { const n = [...e.childNodes].find(n => n.nodeType === 3 && n.textContent.trim()); if (!n) return null; const r = document.createRange(); r.selectNodeContents(n); const b = r.getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; };
});

async function settle(ms = 1200) {
  for (let t = 0; t < ms; t += 300) { await page.clock.runFor(300); await page.waitForTimeout(60); }
  await page.evaluate(() => Promise.all([...document.querySelectorAll('.sq-pass img')].map(i => (i.complete && i.naturalWidth ? Promise.resolve() : new Promise(r => { i.onload = i.onerror = r; })).then(() => i.decode ? i.decode().catch(() => {}) : null))));
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(150);
}

async function openPass(st) {
  await page.evaluate(st => {
    const A = window.SquishApp, x = window.SquishMenu._x;
    if (document.querySelector('.sq-pass [data-x]')) document.querySelector('.sq-pass [data-x]').click();
    A.state.xp = st.xp; A.state.road = st.road;
    x.Q.setWallet({ coins: st.coins });
    window.SquishMenu.levels(x);
  }, st);
  await settle(3000);
}
// the app's new player data while the pass is open (App.on('state'): the page redraws, keeping the selection)
async function sendState(st) { await page.evaluate(st => window.SquishApp.receive({ type: 'state', state: { xp: st.xp, road: st.road } }), st); await settle(); }
// a tap on a level picks it; never a second tap (that claims a level waiting to be claimed)
async function select(L) {
  await showAll(page);
  if (await page.evaluate(L => document.querySelector(`.sq-pass .sq-ln[data-l="${L}"]`).classList.contains('sel'), L)) return;
  await page.click(`.sq-pass .sq-ln[data-l="${L}"]`); await settle();
}

const box = (sel, i = 0) => page.evaluate(({ sel, i }) => { const e = document.querySelectorAll(sel)[i]; if (!e) return null; const b = e.getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; }, { sel, i });

// Shoot a piece within a box (dp) grown by pad; untrimmed: { png, x0, y0 } (the clip's corner in A15 px)
async function shotRaw(part, clip, pad = 34) {
  const [x, y, w, h] = clip, VW = page.viewportSize().width;
  const px0 = Math.max(0, Math.floor((x - pad) * K)), py0 = Math.max(0, Math.floor((y - pad) * K));
  const px1 = Math.min(VW * K, Math.ceil((x + w + pad) * K)), py1 = Math.min(H * K, Math.ceil((y + h + pad) * K));
  await isolate(page, part);
  const buf = await page.screenshot({ omitBackground: true, clip: { x: px0 / K, y: py0 / K, width: (px1 - px0) / K, height: (py1 - py0) / K } });
  return { png: PNG.sync.read(buf), x0: px0, y0: py0 };
}
// ... trimmed to its painted pixels: { buf, png, px: [x, y, w, h] } in A15 px
async function shot(part, clip, pad) {
  const r = await shotRaw(part, clip, pad);
  const t = trim(PNG.sync.write(r.png));
  if (!t) return null;
  return { buf: t.buf, png: PNG.sync.read(t.buf), px: [r.x0 + t.x, r.y0 + t.y, t.w, t.h] };
}
function cut(png, x, y, w, h) { const o = new PNG({ width: w, height: h }); PNG.bitblt(png, o, x, y, w, h, 0, 0); return o; }
function save(name, pngOrBuf) {
  const f = path.join(OUT, name + '.png');
  fs.mkdirSync(path.dirname(f), { recursive: true });
  fs.writeFileSync(f, Buffer.isBuffer(pngOrBuf) ? pngOrBuf : PNG.sync.write(pngOrBuf));
}
// a picture's entry: its place relative to its element's box (A15 px, fractional: the box isn't on the pixel grid)
const picEntry = (image, s, boxDp) => ({ image, off: [r3(s.px[0] - boxDp[0] * K), r3(s.px[1] - boxDp[1] * K)], size: [s.px[2], s.px[3]] });

// One piece, alone, in place.
async function piece(name, part, boxDp, pad) {
  const s = await shot(part, boxDp, pad);
  if (!s) throw new Error('nothing drawn: ' + name);
  save(name, s.buf);
  return picEntry(name, s, boxDp);
}

// A piece that stretches: cut into left cap, a 4 px middle strip, right cap. cap: design px kept whole at each end
// (its rounding and shadow); the middle between them must be the same in every column (checked).
function sliceOf(name, s, boxDp, cap) {
  const bx = boxDp[0] * K, bw = boxDp[2] * K, pw = s.px[2];
  const dx = s.px[0] - bx, ext = s.px[0] + pw - (bx + bw);
  let lw = Math.round(-dx + cap * S), rw = Math.round(ext + cap * S);
  if (lw + rw + 4 > pw) throw new Error(name + ': too short to cut');
  const mx = Math.round((lw + pw - rw) / 2) - 2;
  // the middle must be uniform: every column between the caps like the strip's
  let worst = 0;
  for (let x = lw; x < pw - rw; x++) for (let y = 0; y < s.png.height; y++) for (let c = 0; c < 4; c++)
    worst = Math.max(worst, Math.abs(s.png.data[(y * pw + x) * 4 + c] - s.png.data[(y * pw + mx) * 4 + c]));
  if (worst > 2) console.log(`  ${name}: its middle isn't uniform (${worst}/255): caps too small?`);
  save(name + '-l', cut(s.png, 0, 0, lw, s.png.height));
  save(name + '-m', cut(s.png, mx, 0, 4, s.png.height));
  save(name + '-r', cut(s.png, pw - rw, 0, rw, s.png.height));
  return { mode: 'slice', left: { image: name + '-l', size: [lw, s.png.height] }, mid: { image: name + '-m', size: [4, s.png.height] },
    right: { image: name + '-r', size: [rw, s.png.height] }, off: [r3(dx), r3(s.px[1] - boxDp[1] * K)], ext: r3(ext) };
}
async function slice(name, part, boxDp, cap) { const s = await shot(part, boxDp); return sliceOf(name, s, boxDp, cap); }

// ---------------------------------------------------------------------------------------------------------------
console.log('stand-in: level 6, levels 1, 2, 4 claimed; chests:', STAND_IN.road.find(r => r.k === 'chest').g);
await openPass(STAND_IN);

// ---- the reference screenshots and the measurements for the self-check ----
async function measure() {
  return page.evaluate(() => {
    const R = e => { if (!e) return null; const b = e.getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; };
    const T = window.__rt, TN = window.__tn, q = s => document.querySelector('.sq-pass ' + s), qa = s => [...document.querySelectorAll('.sq-pass ' + s)];
    const texts = [];
    const add = (key, e, useNode) => { if (!e) return; const b = useNode ? TN(e) : null; if (useNode && !b) return; texts.push({ key, ...T(e, b) }); };
    add('claimAll', q('.sq-lvall'), true); add('lvLevel', q('.sq-lvx b')); add('lvXp', q('.sq-lvx small')); add('coins', q('.sq-coins span'));
    qa('.sq-ln').forEach(e => add('node-' + e.dataset.l, e, true));
    add('sub', q('.sq-lvpan .sub')); add('act', q('.sq-lvpan .act span'));
    qa('.sq-it .d').forEach((e, i) => add('desc-' + i, e));
    const art = c => { const a = c.querySelector('.ar > *'); return a ? { kind: a.tagName.toLowerCase() + (a.className && a.className.baseVal === undefined ? '.' + a.className : ''), src: a.getAttribute('src'), box: R(a) } : null; };
    return {
      scroll: q('.sq-lvtrack').scrollLeft,
      pgh: R(q('.mm-pgh')), orb: R(q('.mm-pgh .mm-orb')), title: R(q('.mm-pgh > svg.mm-ft')),
      all: R(q('.sq-lvall')), lv: R(q('.sq-lvx')), lvB: R(q('.sq-lvx b')), lvBar: R(q('.sq-lvbar')), lvFill: R(q('.sq-lvbar i')), lvSmall: R(q('.sq-lvx small')),
      coins: R(q('.sq-coins')), coinsImg: R(q('.sq-coins img')), coinsSpan: R(q('.sq-coins span')),
      track: R(q('.sq-lvtrack')), inner: R(q('.sq-lvin')), rail: R(q('.sq-rail')), railFill: R(q('.sq-rail i')), you: R(q('.sq-you')), youImg: R(q('.sq-you img')), youp: R(q('.sq-youp')),
      nodes: qa('.sq-ln').map(e => ({ l: +e.dataset.l, cls: e.className, box: R(e), st: e.querySelector('.st') ? e.querySelector('.st').className : null })),
      panel: R(q('.sq-lvpan')), col: R(q('.sq-lvpan .lv')), lettering: R(q('.sq-lvpan .lv > svg.mm-ft')), sub: R(q('.sq-lvpan .sub')),
      act: R(q('.sq-lvpan .act')), actCls: q('.sq-lvpan .act') ? q('.sq-lvpan .act').className : null, actIcon: R(q('.sq-lvpan .act > svg:not(.mm-ft)')), actSpan: R(q('.sq-lvpan .act span')),
      items: R(q('.sq-lvpan .items')),
      cards: qa('.sq-it').map(c => ({ box: R(c), none: c.classList.contains('none'), tag: c.querySelector('.tag') ? { text: c.querySelector('.tag').textContent, box: R(c.querySelector('.tag')) } : null,
        ar: R(c.querySelector('.ar')), art: art(c), t: R(c.querySelector('.t svg')), title: c.querySelector('.t svg') ? c.querySelector('.t svg').getAttribute('aria-label') : null, d: R(c.querySelector('.d')) })),
      texts,
    };
  });
}
const refs = {};
async function reference(name) {
  await showAll(page);
  await page.waitForTimeout(200);
  const buf = await page.screenshot();
  fs.writeFileSync(path.join(CHECK, name + '-reference.png'), buf);
  await page.screenshot({ path: path.join(REF, name + '.jpg'), type: 'jpeg', quality: 90 });
  refs[name] = { png: PNG.sync.read(buf), m: await measure() };
  console.log(`reference ${name}: Reference/game/${name}.jpg`);
}
await reference('level-pass');
await select(8);
await reference('level-pass-L8');
await select(3);

const m = refs['level-pass'].m;
const L = { screen: { width: W, height: H, scale: K }, page: { k: r3(k), designHeight: DESIGN_H }, standIn: { ...STAND_IN, select: 0 } };
const rel = (b, o) => [dpx(b[0] - o[0]), dpx(b[1] - o[1]), dpx(b[2]), dpx(b[3])];

// ---------------- the page's background (its wood and rays; stretched on other screen shapes) ----------------
{
  await isolate(page, { sel: '.sq-pass', hide: ['.mm-pg'] });
  const buf = await page.screenshot({ omitBackground: true });
  save('background', buf);
  L.background = { image: 'background', off: [0, 0], size: [Math.round(W * K), Math.round(H * K)] };
}

// ---------------- the header ----------------
console.log('header');
{
  const hd = m.pgh;
  L.header = { left: dpx(hd[0]), top: dpx(hd[1]), right: dpx(W - hd[0] - hd[2]), height: dpx(hd[3]), gap: dpx(m.lv[0] - (m.all[0] + m.all[2])) };
  L.header.back = { box: rel(m.orb, hd), pic: await piece('back', { sel: '.sq-pass .mm-pgh .mm-orb' }, m.orb) };
  L.header.title = { box: rel(m.title, hd), pic: await piece('title', { sel: '.sq-pass .mm-pgh > svg.mm-ft' }, m.title) };

  const textOf = key => { const t = m.texts.find(t => t.key === key); return t && t.spec; };
  // CLAIM ALL (n): a gold plaque (no stripes: it stretches), its text drawn by Unity
  const hideAll = await page.addStyleTag({ content: '.sq-lvall{color:transparent !important}' });
  const allText = m.texts.find(t => t.key === 'claimAll');
  L.header.claimAll = { height: dpx(m.all[3]), padLeft: dpx(allText.box[0] - m.all[0]), padRight: dpx(m.all[0] + m.all[2] - allText.box[0] - allText.box[2]),
    text: allText.spec, frame: await slice('claimall', { sel: '.sq-pass .sq-lvall' }, m.all, 34) };
  await hideAll.evaluate(n => n.remove());

  // LV n [bar] into / need XP: a wooden plaque; the bar's track and fill stretch
  L.header.lv = { height: dpx(m.lv[3]), padLeft: dpx(m.lvB[0] - m.lv[0]), padRight: dpx(m.lv[0] + m.lv[2] - m.lvSmall[0] - m.lvSmall[2]),
    gap: dpx(m.lvBar[0] - m.lvB[0] - m.lvB[2]), text: textOf('lvLevel'), text2: textOf('lvXp'),
    bar: { width: dpx(m.lvBar[2]), height: dpx(m.lvBar[3]),
      track: await slice('lv-bar', { sel: '.sq-pass .sq-lvbar', hide: ['i'] }, m.lvBar, 8),
      fill: await slice('lv-fill', { sel: '.sq-pass .sq-lvbar i' }, m.lvFill, 7) } };
  // the coins: a wooden plaque, the coin over its left end, the number drawn by Unity
  L.header.coins = { height: dpx(m.coins[3]), padLeft: dpx(m.coinsSpan[0] - m.coins[0]), padRight: dpx(m.coins[0] + m.coins[2] - m.coinsSpan[0] - m.coinsSpan[2]),
    text: textOf('coins'), icon: { box: rel(m.coinsImg, m.coins), pic: await piece('coin-icon', { sel: '.sq-pass .sq-coins img' }, m.coinsImg) } };
  // (their frames' right caps here, at the A15's width; their long left parts at the end, in a wide page)
  L.header.lv.capShot = await shot({ sel: '.sq-pass .sq-lvx', hide: ['*'] }, m.lv);
  L.header.coins.capShot = await shot({ sel: '.sq-pass .sq-coins', hide: ['*'] }, m.coins);
}

// ---------------- the track ----------------
console.log('track');
{
  const tr = m.track, inner = m.inner;
  const N = m.nodes.length, n1 = m.nodes[0].box, n2 = m.nodes[1].box;
  const col = dpx(n2[0] - n1[0]), x0 = dpx(n1[0] + n1[2] / 2 - inner[0]);
  L.track = { top: dpx(tr[1]), height: dpx(tr[3]), col, x0, endPad: dpx(inner[2]) - (x0 + (N - 1) * col),
    railTop: dpx(m.rail[1] - inner[1]), railHeight: dpx(m.rail[3]),
    nodeTop: dpx(n1[1] - inner[1]), nodeSize: dpx(n1[2]),
    youTop: dpx(m.you[1] - inner[1]), youSize: dpx(m.you[2]), pointerBox: rel(m.youp, [m.you[0] + m.you[2] / 2, m.you[1]]) };
  // the rail: drawn short in place (its right end is off the screen at full length) and cut
  await page.evaluate(() => { document.querySelector('.sq-pass .sq-rail').style.width = '300px'; });
  const railBox = await box('.sq-pass .sq-rail');
  L.track.rail = await slice('rail', { sel: '.sq-pass .sq-rail', hide: ['i'] }, railBox, 8);
  await page.evaluate(() => { document.querySelector('.sq-pass .sq-rail').style.width = ''; });
  L.track.fill = await slice('rail-fill', { sel: '.sq-pass .sq-rail i' }, m.railFill, 18);

  // pieces that move along the track: drawn at 4 sub-pixel phases of their box (translate moves them, exactly)
  const atPhase = async (sel, p, boxNow) => {
    const fx = boxNow[0] * K - Math.floor(boxNow[0] * K), d = ((p - fx) % 1 + 1) % 1;
    await page.evaluate(({ sel, d }) => { document.querySelector(sel).style.translate = d + 'px 0'; }, { sel, d: d / S });
    return box(sel);
  };
  const unPhase = sel => page.evaluate(sel => { document.querySelector(sel).style.translate = ''; }, sel);

  // the "you" frame and its pointer (the pointer with it: one phase for both, the frame's)
  const you = { phases: PHASES, frame: [], pointer: [] };
  for (const [i, p] of PHASES.entries()) {
    const yb = await atPhase('.sq-pass .sq-you', p, m.you);
    await page.evaluate(d => { document.querySelector('.sq-pass .sq-youp').style.translate = d; }, await page.evaluate(() => document.querySelector('.sq-pass .sq-you').style.translate));
    const f = await shot({ sel: '.sq-pass .sq-you', hide: ['img'] }, yb);
    save(`You/you-frame-${i}`, f.buf); you.frame.push(picEntry(`You/you-frame-${i}`, f, yb));
    const pt = await shot({ sel: '.sq-pass .sq-youp' }, await box('.sq-pass .sq-youp'));
    save(`You/you-pointer-${i}`, pt.buf); you.pointer.push(picEntry(`You/you-pointer-${i}`, pt, yb));
  }
  await unPhase('.sq-pass .sq-you'); await unPhase('.sq-pass .sq-youp');
  // the creature inside it (clipped by the round frame): one per creature, as still() picks its picture offline
  const roster = await page.evaluate(() => { const Q = window.SquishMenu._x.Q, T = window.SQ_THUMBS || {};
    return Q.ROSTER.map((c, id) => c && { id, key: c.key, src: (s => (s ? T[s] || s : null))(Q.src(id)) }).filter(c => c && c.src); });
  you.creatures = [];
  for (const c of roster) {
    await page.evaluate(src => new Promise(r => { const im = document.querySelector('.sq-pass .sq-you img'); im.onload = im.onerror = () => r(); im.src = src; if (im.complete) r(); }).then(() => document.querySelector('.sq-pass .sq-you img').decode().catch(() => {})), c.src);
    const s = await shot({ sel: '.sq-pass .sq-you', only: 'img' }, m.you, 4);
    if (!s) continue;
    save(`You/${c.key}`, s.buf);
    you.creatures.push({ key: c.key, ...picEntry(`You/${c.key}`, s, m.you) });
  }
  L.track.you = you;
  console.log(`  you: ${you.creatures.length} creatures`);

  // the level nodes: every look, drawn on level 4's node (inside the screen, its glow uncut), number and badge hidden
  const NODE = '.sq-pass .sq-ln[data-l="4"]';
  const LOOKS = { brown: 'sq-ln', 'brown-glow': 'sq-ln cl', 'brown-sel': 'sq-ln sel', gold: 'sq-ln on', 'gold-glow': 'sq-ln on cl', 'gold-sel': 'sq-ln on sel',
    'gold-cur': 'sq-ln on cur', 'gold-cur-sel': 'sq-ln on cur sel' };
  const badgeHtml = await page.evaluate(() => {
    const st = c => { const e = document.querySelector('.sq-pass .sq-ln .st.' + c); return e && e.outerHTML; };
    return { ok: st('ok'), ex: st('ex'), lock: st('lkb') };
  });
  const nb = (await box(NODE));
  const textStyle = await page.addStyleTag({ content: '.sq-pass .sq-ln{color:transparent !important;text-shadow:none !important}' });
  const nodes = { phases: PHASES, looks: [], badges: [] };
  for (const look of Object.keys(LOOKS)) nodes.looks.push({ key: look, pics: [] });
  for (const b of ['ok', 'ex', 'lock', 'ok-cur']) nodes.badges.push({ key: b, pics: [] });
  for (const [i, p] of PHASES.entries()) {
    await page.evaluate(sel => { document.querySelector(sel).className = 'sq-ln on'; }, NODE);   // (its box unscaled)
    const pb = await atPhase(NODE, p, nb);
    for (const look of nodes.looks) {
      await page.evaluate(({ sel, cls }) => { document.querySelector(sel).className = cls; }, { sel: NODE, cls: LOOKS[look.key] });
      const s = await shot({ sel: NODE, hide: ['.st'] }, pb, 40);
      save(`Nodes/node-${look.key}-${i}`, s.buf); look.pics.push(picEntry(`Nodes/node-${look.key}-${i}`, s, pb));
    }
    for (const b of nodes.badges) {
      const cur = b.key === 'ok-cur';
      await page.evaluate(({ sel, cls, html }) => { const e = document.querySelector(sel); e.className = cls; e.querySelectorAll('.st').forEach(s => s.remove()); e.insertAdjacentHTML('beforeend', html); },
        { sel: NODE, cls: cur ? LOOKS['gold-cur'] : LOOKS.gold, html: badgeHtml[cur ? 'ok' : b.key] });
      const s = await shot({ sel: NODE + ' .st' }, await box(NODE + ' .st'), 8);
      save(`Nodes/badge-${b.key}-${i}`, s.buf); b.pics.push(picEntry(`Nodes/badge-${b.key}-${i}`, s, pb));
    }
  }
  await unPhase(NODE);
  await textStyle.evaluate(n => n.remove());
  await page.evaluate(({ sel, cls, html }) => { const e = document.querySelector(sel); e.className = cls; e.querySelectorAll('.st').forEach(s => s.remove()); e.insertAdjacentHTML('beforeend', html); },
    { sel: NODE, cls: 'sq-ln on', html: badgeHtml.ok });
  // the number's look: brown and gold (text by Unity), the current level's node grown (.cur scale)
  const nodeText = async cls => { await page.evaluate(({ sel, cls }) => { document.querySelector(sel).className = cls; }, { sel: NODE, cls }); return page.evaluate(sel => window.__rt(document.querySelector(sel)).spec, NODE); };
  nodes.brownText = await nodeText('sq-ln'); nodes.goldText = await nodeText('sq-ln on');
  await page.evaluate(sel => { document.querySelector(sel).className = 'sq-ln on'; }, NODE);
  nodes.curScale = 1.12;   // .sq-ln.cur{transform:scale(1.12)} (an animation's transform wins over it: a claimable one isn't grown)
  nodes.bob = { name: 'sqClaim', duration: 1.2, easing: 'ease-in-out', y: -4 };   // @keyframes sqClaim 50%{translateY(-4px)}
  L.track.nodes = nodes;
}

// ---------------- the panel ----------------
console.log('panel');
{
  const pan = m.panel, colB = m.col, items = m.items, card = m.cards[0];
  L.panel = { left: dpx(pan[0]), right: dpx(W - pan[0] - pan[2]), top: dpx(pan[1]), height: dpx(pan[3]),
    padLeft: dpx(colB[0] - pan[0]), padTop: dpx(items[1] - pan[1]), padRight: dpx(pan[0] + pan[2] - items[0] - items[2]),
    gap: dpx(items[0] - colB[0] - colB[2]), itemsHeight: dpx(items[3]) };
  L.panel.capShot = await shot({ sel: '.sq-pass .sq-lvpan', hide: ['*'] }, pan);

  // the left column: LEVEL n, the line under it, the action
  const textOf = key => m.texts.find(t => t.key === key).spec;
  L.panel.column = { width: dpx(colB[2]), letteringHeight: dpx(m.lettering[3]), subTop: dpx(m.sub[1] - colB[1]), subHeight: dpx(m.sub[3]),
    actTop: dpx(m.act[1] - colB[1]), actWidth: dpx(m.act[2]), actHeight: dpx(m.act[3]), sub: textOf('sub') };
  L.panel.column.claim = await piece('claim', { sel: '.sq-pass .sq-lvpan .act' }, m.act);
  // "LEVEL n": gold once reached, white before; every level on the road
  const letters = [];
  const N = STAND_IN.road.length;
  for (let n = 1; n <= N; n++) for (const style of ['gold', 'white']) {
    const lb = await page.evaluate(({ n, style }) => {
      const svg = document.querySelector('.sq-pass .sq-lvpan .lv > svg.mm-ft');
      svg.outerHTML = window.MainMenu.ft('LEVEL ' + n, style, 24);
      const s2 = document.querySelector('.sq-pass .sq-lvpan .lv > svg.mm-ft'); window.SquishMenu._x.fitFT(s2.parentNode);
      const b = s2.getBoundingClientRect(); return [b.x, b.y, b.width, b.height];
    }, { n, style });
    const s = await shot({ sel: '.sq-pass .sq-lvpan .lv > svg.mm-ft' }, lb, 12);
    save(`Lettering/level-${n}-${style}`, s.buf);
    letters.push({ key: `${n}-${style}`, w: dpx(lb[2]), h: dpx(lb[3]), ...picEntry(`Lettering/level-${n}-${style}`, s, lb) });
  }
  L.panel.column.lettering = letters;
  console.log(`  lettering: ${letters.length}`);

  // a reward card: its background (stretches), the glow behind the art, the tag, the art, the title, the line
  const cb = card.box;
  L.panel.card = { basis: 196, gap: 12, padTop: dpx(card.ar[1] - cb[1]), padX: dpx(card.ar[0] - cb[0]), artHeight: dpx(card.ar[3]),
    titleTop: dpx(card.t[1] - cb[1]), titleHeight: dpx(card.t[3]), descTop: dpx(card.d[1] - cb[1]),
    tagTop: dpx(card.tag.box[1] - cb[1]), desc: m.texts.find(t => t.key === 'desc-0').spec };
  L.panel.card.bg = await slice('card', { sel: '.sq-pass .sq-it', hide: ['*'] }, cb, 18);
  // the glow behind the art (.ar::before, 120 x 100, centred in the art's box): placed by its own box
  const gb = [card.ar[0] + card.ar[2] / 2 - 60 * k, card.ar[1] + card.ar[3] / 2 - 50 * k, 120 * k, 100 * k];
  L.panel.card.glow = { w: 120, h: 100, ...(await piece('art-glow', { sel: '.sq-pass .sq-it .ar', hide: ['*'] }, gb)) };
  // the tags: centred over the card's top edge
  const tags = [];
  for (const t of ['CHEST', 'COINS', 'GEMS', 'NEW ROOM', 'GAMES', 'HOUSE']) {
    const tb = await page.evaluate(t => { const e = document.querySelector('.sq-pass .sq-it .tag'); e.textContent = t; const b = e.getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; }, t);
    const name = 'Tags/tag-' + t.toLowerCase().replace(/\s+/g, '');
    const p = await piece(name, { sel: '.sq-pass .sq-it .tag' }, tb, 6);
    tags.push({ key: t, w: dpx(tb[2]), h: dpx(tb[3]), ...p });
  }
  await page.evaluate(() => { document.querySelector('.sq-pass .sq-it .tag').textContent = 'CHEST'; });
  L.panel.card.tags = tags;

  // the chests: every group's every size, as the card draws them (150% of the art's height, its own drop shadow)
  const chests = [];
  for (const g of squad.GROUPS.map(x => x.key)) for (const t of SIZES) {
    const cbx = await page.evaluate(src => new Promise(r => { const im = document.querySelector('.sq-pass .sq-it .ar img.chest'); im.onload = im.onerror = () => r(); im.src = src; })
      .then(() => document.querySelector('.sq-pass .sq-it .ar img.chest').decode().catch(() => {}))
      .then(() => { const b = document.querySelector('.sq-pass .sq-it .ar img.chest').getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; }), `app/gchests/${g}-${t}-grow.webp`);
    const p = await piece(`Chests/${g}-${t}`, { sel: '.sq-pass .sq-it .ar img.chest' }, cbx, 8);
    chests.push({ key: `${g}-${t}`, w: dpx(cbx[2]), h: dpx(cbx[3]), ...p });
  }
  L.panel.card.chests = chests;
  // the chest's box: height 150% of the art's, margins -22% / -20% of the art's width (CSS % margins); checked here
  const ar = card.ar, ci = card.art.box, aw = dpx(ar[2]), ah = dpx(ar[3]), ch = 1.5 * ah;
  const top = (ah - (ch - 0.42 * aw)) / 2 - 0.22 * aw;
  if (Math.abs(top - dpx(ci[1] - ar[1])) > 0.01) console.log(`  chest top: formula ${top.toFixed(3)}, page ${dpx(ci[1] - ar[1])}`);
  L.panel.card.chestFit = { heightPct: 1.5, marginTopPct: -0.22, marginBottomPct: -0.2 };
  console.log(`  chests: ${chests.length}`);

  // the reward titles: every title the road, the chests and the rooms can make (ft white 15)
  const fmt = n => Number(n).toLocaleString('en-US');
  const rooms = await page.evaluate(() => window.MainMenuScene.ROOMS);
  const titleSet = new Set();
  for (const r of squad.DEFAULT_LEVELS.road) if (r.k !== 'chest') titleSet.add(fmt(r.n) + (r.k === 'gems' ? ' Gems' : ' Coins'));
  for (const g of Object.keys(CHEST_NAMES)) for (const n of CHEST_NAMES[g]) titleSet.add(n);
  for (const t of SIZES) titleSet.add(CHEST_NAME[t] + ' Chest');
  for (const key of Object.keys(rooms)) titleSet.add(rooms[key].n);
  ['Yard Games', 'Cozy home', 'Lovely home', 'Royal palace'].forEach(t => titleSet.add(t));
  const titles = [];
  const slug = t => t.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  for (const t of titleSet) {
    const tb = await page.evaluate(t => {
      const host = document.querySelector('.sq-pass .sq-it .t'); host.innerHTML = window.MainMenu.ft(t, 'white', 15); window.SquishMenu._x.fitFT(host);
      const b = host.querySelector('svg').getBoundingClientRect(); return [b.x, b.y, b.width, b.height];
    }, t);
    const p = await piece(`Titles/${slug(t)}`, { sel: '.sq-pass .sq-it .t svg' }, tb, 10);
    titles.push({ key: t, w: dpx(tb[2]), h: dpx(tb[3]), ...p });
  }
  L.panel.card.titles = titles;
  console.log(`  titles: ${titles.length}`);

  // the descriptions, with the browser's own line breaks, at the card widths the A15 shows (1-2 cards, 3 cards)
  const descs = new Set(['Gems to spend in the Shop', 'Coins to spend in the Shop', 'A new room opens in your house',
    'The backyard\'s 7 yard games (buy each one\'s toy there to play it)', 'Your house gets its next look', 'Nothing new at this level']);
  for (const t of SIZES) { descs.add(CHEST_NAME[t] + ' chest'); for (const g of squad.GROUPS) descs.add(g.name + ' · ' + CHEST_NAME[t] + ' chest'); }
  const lines = [];
  for (const basis of [196, (dpx(items[2]) - 24) / 3]) {
    await page.evaluate(b => { document.querySelector('.sq-pass .sq-it').style.flexBasis = b + 'px'; }, basis);
    for (const t of descs) {
      const r = await page.evaluate(t => {
        const d = document.querySelector('.sq-pass .sq-it .d'); d.textContent = t;
        const n = d.firstChild, out = []; let cur = '', top = null;
        for (let i = 0; i < t.length; i++) {
          const rg = document.createRange(); rg.setStart(n, i); rg.setEnd(n, i + 1);
          const rc = rg.getClientRects()[0];
          if (rc && top !== null && rc.top > top + 2) { out.push(cur); cur = ''; }
          if (rc) top = rc.top;
          cur += t[i];
        }
        out.push(cur);
        return { lines: out.map(s => s.trim()), w: d.parentNode.getBoundingClientRect().width };
      }, t);
      lines.push({ key: t, width: r3(dpx(r.w) - 2 * L.panel.card.padX), lines: r.lines });
    }
  }
  await page.evaluate(() => { document.querySelector('.sq-pass .sq-it').style.flexBasis = ''; document.querySelector('.sq-pass .sq-it .d').textContent = 'Forest Friends · Common chest'; });
  L.panel.card.descLines = lines;
}

// ---- other states: the action's looks, the coin and gem piles, the rooms, the plain chests, "nothing new" ----
{
  // CLAIMED (level 1): the action's frame and its check
  await select(1);
  const actB = await box('.sq-pass .sq-lvpan .act');
  L.panel.column.actFrame = await piece('act-frame', { sel: '.sq-pass .sq-lvpan .act', hide: ['*'] }, actB);
  const ic = await box('.sq-pass .sq-lvpan .act > svg');
  const sp = await box('.sq-pass .sq-lvpan .act span');
  L.panel.column.iconSize = dpx(ic[2]); L.panel.column.iconGap = dpx(sp[0] - ic[0] - ic[2]);
  L.panel.column.actText = (await page.evaluate(() => window.__rt(document.querySelector('.sq-pass .sq-lvpan .act span')))).spec;
  L.panel.column.check = await piece('act-check', { sel: '.sq-pass .sq-lvpan .act > svg' }, ic, 4);
  L.panel.column.actIconTop = dpx(ic[1] - actB[1]);
  // the coin pile (1 card)
  const pile = await box('.sq-pass .sq-it .pile');
  L.panel.card.piles = [{ key: 'coins', w: dpx(pile[2]), h: dpx(pile[3]), ...(await piece('pile-coins', { sel: '.sq-pass .sq-it .pile' }, pile, 8)) }];
  // locked (level 9: 40 gems, not reached): the lock, and the gem pile
  await select(9);
  const lk = await box('.sq-pass .sq-lvpan .act > svg');
  L.panel.column.lock = await piece('act-lock', { sel: '.sq-pass .sq-lvpan .act > svg' }, lk, 4);
  const gp = await box('.sq-pass .sq-it .pile');
  L.panel.card.piles.push({ key: 'gems', w: dpx(gp[2]), h: dpx(gp[3]), ...(await piece('pile-gems', { sel: '.sq-pass .sq-it .pile' }, gp, 8)) });
  // the rooms (level 2: coins and the Kitchen; the room picture in the second card)
  await select(2);
  const rooms = [];
  for (const f of fs.readdirSync(path.join(BUILT, 'app/art/rooms')).filter(f => f.endsWith('.jpg')).sort()) {
    const key = f.replace(/\.jpg$/, '');
    const rb = await page.evaluate(src => new Promise(r => { const im = document.querySelectorAll('.sq-pass .sq-it .ar img.pic')[0]; im.onload = im.onerror = () => r(); im.src = src; })
      .then(() => document.querySelectorAll('.sq-pass .sq-it .ar img.pic')[0].decode().catch(() => {}))
      .then(() => { const b = document.querySelectorAll('.sq-pass .sq-it .ar img.pic')[0].getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; }), 'app/art/rooms/' + f);
    rooms.push({ key, w: dpx(rb[2]), h: dpx(rb[3]), ...(await piece(`Rooms/${key}`, { sel: '.sq-pass .sq-it .ar img.pic' }, rb, 8)) });
  }
  L.panel.card.rooms = rooms;
  L.panel.card.roomAspect = 4 / 3;
  // the plain chests (a chest prize with no group: menu.js chestArt), level 3's prize swapped for each size
  const plain = [];
  for (const t of SIZES) {
    const road = STAND_IN.road.map(r => r.lv === 3 ? { ...r, t, g: null, gn: null, cn: null } : r);
    await sendState({ ...STAND_IN, road });
    await select(3);
    const sb = await box('.sq-pass .sq-it .ar > svg');
    plain.push({ key: t, w: dpx(sb[2]), h: dpx(sb[3]), ...(await piece(`Chests/plain-${t}`, { sel: '.sq-pass .sq-it .ar > svg' }, sb, 8)) });
  }
  L.panel.card.plainChests = plain;
  // nothing new at a level (a road shorter than 30 levels): the line, alone, centred in the card
  await sendState({ ...STAND_IN, road: STAND_IN.road.slice(0, 2) });
  await select(3);
  const nb = await box('.sq-pass .sq-it.none'), nd = await box('.sq-pass .sq-it.none .d');
  L.panel.card.noneDescTop = dpx(nd[1] - nb[1]); L.panel.card.noneDescHeight = dpx(nd[3]);
  L.panel.card.padBottom = 8;   // (.sq-it padding 12px 8px 8px: the line is centred between)
  await sendState(STAND_IN);
  await select(3);
}

// ---- the coin burst's icons (26 x 26, .sq-fly) ----
{
  const fly = [];
  for (const kind of ['coin', 'gem']) {
    const fb = await page.evaluate(kind => {
      const pg = document.querySelector('.sq-pass .mm-pg'), im = document.createElement('img'); im.className = 'sq-fly x-fly';
      im.src = kind === 'gem' ? window.SquishCoin.gem(0) : window.SquishCoin.coin(0); im.style.left = '400px'; im.style.top = '120px'; pg.appendChild(im);
      return im.decode().catch(() => {}).then(() => { const b = im.getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; });
    }, kind);
    fly.push({ key: kind, w: dpx(fb[2]), h: dpx(fb[3]), ...(await piece('fly-' + kind, { sel: '.sq-pass .x-fly' }, fb, 4)) });
    await page.evaluate(() => document.querySelector('.sq-pass .x-fly').remove());
  }
  // menu.js burst(): 9 icons, each from the reward's art (or the node) +-20 / +-15 dp, to the coin pill's coin
  L.fly = { icons: fly, count: 9, start: 0.03, every: 0.055, duration: 0.75, easing: 'cubic-bezier(.5,-0.2,.7,1)', scale: 0.7,
    fadeDelay: 0.62, fadeDuration: 0.2, fadeEasing: 'ease', removeAt: 1.3, jitter: [40, 30] };
}

// ---------------- the long pieces: drawn long in a wide page, their left parts cut to length ----------------
console.log('long pieces');
{
  await page.setViewportSize({ width: WIDE, height: H });
  await settle(1500);
  // a wooden plaque: its left part from one drawn long (same left edge, same stripes), its right cap from the A15's
  async function crop(name, capShot, refBox, sel, longWidth, cap) {
    if (longWidth) await page.evaluate(({ sel, refBox, w, k }) => {
      const e = document.querySelector(sel), p = e.parentNode.getBoundingClientRect();
      // (on the layout's 1/64 px grid, where the flex row had put it)
      const g = v => Math.round(v * 64) / 64;
      e.style.position = 'absolute'; e.style.left = g((refBox[0] - p.x) / k) + 'px'; e.style.top = g((refBox[1] - p.y) / k) + 'px'; e.style.width = w + 'px';
    }, { sel, refBox, w: longWidth, k });
    const lb = await box(sel);
    if (Math.abs(lb[0] - refBox[0]) > 1e-3 || Math.abs(lb[1] - refBox[1]) > 1e-3) console.log(`  ${name}: the long one moved (${lb[0] - refBox[0]}, ${lb[1] - refBox[1]})`);
    const long = await shot({ sel, hide: ['*'] }, lb);
    const c = capShot, bx = refBox[0] * K, bw = refBox[2] * K, pw = c.px[2];
    const dx = c.px[0] - bx, ext = c.px[0] + pw - (bx + bw), rw = Math.round(ext + cap * S);
    if (long.px[0] !== c.px[0] || long.px[1] !== c.px[1] || long.px[3] !== c.px[3]) console.log(`  ${name}: long and short differ in place: ${long.px} vs ${c.px}`);
    // the long one's left part must be the A15's, pixel for pixel, up to the cap
    let worst = 0;
    for (let y = 0; y < c.png.height; y++) for (let x = 0; x < pw - rw; x++) for (let ch = 0; ch < 4; ch++)
      worst = Math.max(worst, Math.abs(c.png.data[(y * pw + x) * 4 + ch] - long.png.data[(y * long.px[2] + x) * 4 + ch]));
    // (up to ~12/255 on single pixels is Chrome's gradient noise between two widths, not a shift: checked column by column)
    if (worst > 12) console.log(`  ${name}: the long one's left part differs from the A15's by ${worst}/255`);
    const bodyW = long.px[2] - Math.round(cap * S * 2);
    save(name + '-body', cut(long.png, 0, 0, bodyW, long.png.height));
    save(name + '-r', cut(c.png, pw - rw, 0, rw, c.png.height));
    if (longWidth) await page.evaluate(sel => { const e = document.querySelector(sel); e.style.position = e.style.left = e.style.top = e.style.width = ''; }, sel);
    return { mode: 'crop', body: { image: name + '-body', size: [bodyW, long.png.height] }, right: { image: name + '-r', size: [rw, c.png.height] },
      off: [r3(dx), r3(c.px[1] - refBox[1] * K)], ext: r3(ext) };
  }
  L.header.lv.frame = await crop('lv', L.header.lv.capShot, m.lv, '.sq-pass .sq-lvx', 600, 38);
  L.header.coins.frame = await crop('coins', L.header.coins.capShot, m.coins, '.sq-pass .sq-coins', 600, 36);
  L.panel.frame = await crop('panel', L.panel.capShot, m.panel, '.sq-pass .sq-lvpan', 0, 42);
  delete L.header.lv.capShot; delete L.header.coins.capShot; delete L.panel.capShot;
  await page.setViewportSize({ width: W, height: H });
  await settle(1500);
}

fs.writeFileSync(path.join(OUT, 'layout.json'), JSON.stringify(L, null, 1));
const count = d => fs.readdirSync(d, { recursive: true }).filter(f => String(f).endsWith('.png')).length;
console.log(`layout.json written; ${count(OUT)} pictures`);

// ================= self-check: put the pieces back together the way Unity does, compare with the page =================
// Positions come from layout.json and the same rules as LevelPassScreen.cs (track by level, panel by width, cards by
// count); the header's plaques take their widths from the page (Unity's come from its own text), the texts are drawn
// by the browser where the page has them (Unity draws its own).
const fontCss = fs.readdirSync(path.join(BUILT, 'vendor/css')).map(f => fs.readFileSync(path.join(BUILT, 'vendor/css', f), 'utf8')).join('\n')
  .replace(/url\(([^)]+)\)/g, (mm, u) => `url(${pathToFileURL(path.join(BUILT, 'vendor', u.replace(/["']/g, '').replace(/^\.\.\//, ''))).href})`);
const kd = L.page.k, Wd = W / kd, SS = kd * K;
const ops = [];
const img = (image, x, y, w, h, clip) => ops.push({ image, x, y, w, h, clip });
const at = (pic, bx, by) => img(pic.image, Math.round(bx + pic.off[0]), Math.round(by + pic.off[1]), pic.size[0], pic.size[1]);
const phaseOf = x => { const f = x - Math.floor(x); let best = 0, bd = 9; PHASES.forEach((p, i) => { const d = Math.min(Math.abs(f - p), 1 - Math.abs(f - p)); if (d < bd) { bd = d; best = i; } }); return best; };
// a stretching piece over a box (px): left part, middle, right cap
function stretch(st, bx, by, bw) {
  const x0 = Math.round(bx + st.off[0]), y0 = Math.round(by + st.off[1]), x1 = Math.round(bx + bw + st.ext), rw = st.right.size[0], h = st.right.size[1];
  if (st.mode === 'crop') { img(st.body.image, x0, y0, st.body.size[0], h, [x0, y0, x1 - rw - x0, h]); img(st.right.image, x1 - rw, y0, rw, h); return; }
  const lw = st.left.size[0];
  if (x1 - x0 < lw + rw) { const half = Math.floor((x1 - x0) / 2); img(st.left.image, x0, y0, lw, h, [x0, y0, half, h]); img(st.right.image, x1 - rw, y0, rw, h, [x0 + half, y0, x1 - x0 - half, h]); return; }
  img(st.left.image, x0, y0, lw, h); img(st.mid.image, x0 + lw, y0, x1 - rw - x0 - lw, h); img(st.right.image, x1 - rw, y0, rw, h);
}
const find = (list, key) => list.find(x => x.key === key);

function compose(state, sel, mm) {
  ops.length = 0;
  const xp = state.xp, road = state.road, rows = [];
  const N = Math.max(road.length, 30);
  const ROOMS = { living: { n: 'Living Room', lv: 1 }, kitchen: { n: 'Kitchen', lv: 2 }, bathroom: { n: 'Bathroom', lv: 4 }, bedroom: { n: 'Bedroom', lv: 6 }, backyard: { n: 'Backyard', lv: 8 }, dance: { n: 'Dance Room', lv: 11 }, movie: { n: 'Movie Room', lv: 12 }, beach: { n: 'Beach', lv: 30 } };
  for (let l = 1; l <= N; l++) {
    const r = road[l - 1] || null, un = [];
    Object.keys(ROOMS).forEach(kk => { if (ROOMS[kk].lv === l && l > 1) un.push({ key: kk, name: ROOMS[kk].n, kind: 'room' }); });
    if (l === 8) un.push({ key: 'yard', name: 'Yard Games', kind: 'yard' });
    const TI = { 5: 'Cozy home', 15: 'Lovely home', 25: 'Royal palace' };
    if (TI[l]) un.push({ key: 'tier', name: TI[l], kind: 'tier', tier: l === 5 ? 1 : l === 15 ? 2 : 3 });
    rows.push({ L: l, r, un, reached: xp.level >= l, got: !!(r && r.got), claimable: !!(r && r.claimable) });
  }
  img(L.background.image, 0, 0, L.background.size[0], L.background.size[1]);
  // header
  const H0 = L.header, hx = H0.left * SS, hy = H0.top * SS;
  at(H0.back.pic, hx + H0.back.box[0] * SS, hy + H0.back.box[1] * SS);
  at(H0.title.pic, hx + H0.title.box[0] * SS, hy + H0.title.box[1] * SS);
  if (mm.all) stretch(H0.claimAll.frame, mm.all[0] * K, mm.all[1] * K, mm.all[2] * K);
  stretch(H0.lv.frame, mm.lv[0] * K, mm.lv[1] * K, mm.lv[2] * K);
  const f = xp.need > 0 ? Math.min(1, Math.max(0, xp.into / xp.need)) : 0;
  const bar = H0.lv.bar;
  stretch(bar.track, mm.lvBar[0] * K, mm.lvBar[1] * K, bar.width * SS);
  if (f > 0) stretch(bar.fill, mm.lvBar[0] * K, mm.lvBar[1] * K, bar.width * f * SS);
  stretch(H0.coins.frame, mm.coins[0] * K, mm.coins[1] * K, mm.coins[2] * K);
  at(H0.coins.icon.pic, mm.coins[0] * K + H0.coins.icon.box[0] * SS, mm.coins[1] * K + H0.coins.icon.box[1] * SS);
  // track (scroll 0 in both references)
  const T0 = L.track, ty = T0.top * SS, scroll = Math.round(mm.scroll * SS), cx = l => T0.x0 + (l - 1) * T0.col;
  const tclip = [0, Math.round(ty), Math.round(Wd * SS), Math.round((T0.top + T0.height) * SS) - Math.round(ty)];
  const timg = (...a) => { const n = ops.length; (a.length === 3 ? at : stretch)(...a); for (let i = n; i < ops.length; i++) ops[i].clip = ops[i].clip ? intersect(ops[i].clip, tclip) : tclip; };
  const fill = Math.min(cx(N), cx(xp.level) + f * T0.col);
  timg(T0.rail, cx(1) * SS - scroll, ty + T0.railTop * SS, (cx(N) - cx(1)) * SS);
  if (fill - cx(1) > 0) timg(T0.fill, cx(1) * SS - scroll, ty + T0.railTop * SS, (fill - cx(1)) * SS);
  for (const row of rows) {
    const cl = row.claimable, got = row.got || (row.reached && !row.r), cur = row.L === xp.level && !cl, isSel = sel === row.L;
    const look = (row.reached ? 'gold' : 'brown') + (cur ? '-cur' : '') + (isSel ? '-sel' : cl && !cur ? '-glow' : '');
    const nx = (cx(row.L) - T0.nodeSize / 2) * SS - scroll, ny = ty + T0.nodeTop * SS, ph = phaseOf(nx);
    const lk = find(T0.nodes.looks, look);
    timg(lk.pics[ph], nx, ny);
    const badge = cl ? 'ex' : got ? (cur ? 'ok-cur' : 'ok') : row.reached ? null : 'lock';
    if (badge) timg(find(T0.nodes.badges, badge).pics[ph], nx, ny);
  }
  const yx = (fill - T0.youSize / 2) * SS - scroll, yy = ty + T0.youTop * SS, yph = phaseOf(yx);
  timg(T0.you.frame[yph], yx, yy);
  const cre = find(T0.you.creatures, state.creature); if (cre) timg(cre, yx, yy);
  timg(T0.you.pointer[yph], yx, yy);
  // panel
  const P = L.panel, px0 = P.left * SS, py0 = P.top * SS, pw = Wd - P.left - P.right;
  stretch(P.frame, px0, py0, pw * SS);
  const row = rows[sel - 1], C = P.column, colX = P.left + P.padLeft, colH = C.actTop + C.actHeight, colY = P.top + P.padTop + (P.itemsHeight - colH) / 2;
  const lt = find(C.lettering, `${sel}-${row.reached ? 'gold' : 'white'}`);
  at(lt, (colX + (C.width - lt.w) / 2) * SS, colY * SS);
  const ax = (colX + (C.width - C.actWidth) / 2) * SS, ay = (colY + C.actTop) * SS;
  if (row.r && row.claimable) at(C.claim, ax, ay);
  else { at(C.actFrame, ax, ay); const t = mm.texts.find(t => t.key === 'act'); if (t && row.reached) at(C.check, t.box[0] * K - (C.iconSize + C.iconGap) * SS, ay + C.actIconTop * SS); else if (t) at(C.lock, t.box[0] * K - (C.iconSize + C.iconGap) * SS, ay + C.actIconTop * SS); }
  const items = (row.r ? [{ r: row.r }] : []).concat(row.un.map(u => ({ u })));
  const Cd = P.card, iw = pw - P.padLeft - C.width - P.gap - P.padRight, n = Math.max(1, items.length);
  const cw = Math.min(Cd.basis, (iw - Cd.gap * (n - 1)) / n), total = n * cw + Cd.gap * (n - 1);
  const ix = P.left + P.padLeft + C.width + P.gap + (iw - total) / 2, iy = P.top + P.padTop;
  items.forEach((it, i) => {
    const x = ix + i * (cw + Cd.gap), arX = x + Cd.padX, arW = cw - 2 * Cd.padX, arY = iy + Cd.padTop;
    stretch(Cd.bg, x * SS, iy * SS, cw * SS);
    at(Cd.glow, (arX + arW / 2 - Cd.glow.w / 2) * SS, (arY + Cd.artHeight / 2 - Cd.glow.h / 2) * SS);
    let tag, art, artBox, title;
    if (it.r) {
      const r = it.r;
      if (r.k === 'chest') {
        tag = 'CHEST'; title = r.cn || CHEST_NAME[r.t] + ' Chest';
        if (r.g) { art = find(Cd.chests, `${r.g}-${r.t}`); const h = art.h, mt = Cd.chestFit.marginTopPct * arW, mb = Cd.chestFit.marginBottomPct * arW; artBox = [arX + (arW - art.w) / 2, arY + (Cd.artHeight - (h + mt + mb)) / 2 + mt]; }
        else { art = find(Cd.plainChests, r.t); artBox = [arX + (arW - art.w) / 2, arY + (Cd.artHeight - art.h) / 2]; }
      } else { tag = r.k === 'gems' ? 'GEMS' : 'COINS'; title = Number(r.n).toLocaleString('en-US') + (r.k === 'gems' ? ' Gems' : ' Coins'); art = find(Cd.piles, r.k); artBox = [arX + (arW - art.w) / 2, arY + (Cd.artHeight - art.h) / 2]; }
    } else {
      const u = it.u; tag = u.kind === 'tier' ? 'HOUSE' : u.kind === 'yard' ? 'GAMES' : 'NEW ROOM'; title = u.name;
      art = find(Cd.rooms, u.kind === 'tier' ? 'tier' + u.tier : u.key); artBox = [arX + (arW - art.w) / 2, arY];
    }
    const tg = find(Cd.tags, tag); at(tg, (x + (cw - tg.w) / 2) * SS, (iy + Cd.tagTop) * SS);
    at(art, artBox[0] * SS, artBox[1] * SS);
    const tt = find(Cd.titles, title); at(tt, (x + (cw - tt.w) / 2) * SS, (iy + Cd.titleTop) * SS);
  });
  return rows;
}
function intersect(a, b) { const x = Math.max(a[0], b[0]), y = Math.max(a[1], b[1]); return [x, y, Math.max(0, Math.min(a[0] + a[2], b[0] + b[2]) - x), Math.max(0, Math.min(a[1] + a[3], b[1] + b[3]) - y)]; }

function html(mm) {
  const tx = mm.texts.map(t => {
    const s = t.spec, sh = s.shadows.map(q => `${q.color} ${q.x * SS}px ${q.y * SS}px ${q.blur * SS}px`).join(',');
    const multi = s.lineHeight && t.box[3] > s.lineHeight * kd * 1.5;
    return `<div class="t" style="left:${t.box[0] * K}px;top:${t.box[1] * K}px;width:${t.box[2] * K}px;${multi ? `line-height:${s.lineHeight * SS}px;white-space:normal;text-align:center` : `line-height:${t.box[3] * K}px`};font:${s.weight} ${s.size * SS}px ${s.font};letter-spacing:${s.letterSpacing * SS}px;color:${s.color};text-shadow:${sh || 'none'}">${t.text}</div>`;
  }).join('\n');
  const im = ops.map(o => {
    const el = `<img src="${pathToFileURL(path.join(OUT, o.image + '.png')).href}" style="left:${o.x}px;top:${o.y}px;width:${o.w}px;height:${o.h}px">`;
    if (!o.clip) return el;
    const [cx0, cy0, cw0, ch0] = o.clip;
    return `<div class="c" style="left:${cx0}px;top:${cy0}px;width:${cw0}px;height:${ch0}px"><img src="${pathToFileURL(path.join(OUT, o.image + '.png')).href}" style="left:${o.x - cx0}px;top:${o.y - cy0}px;width:${o.w}px;height:${o.h}px"></div>`;
  }).join('\n');
  return `<!doctype html><html><head><style>${fontCss}
  html,body{margin:0;width:${W * K}px;height:${H * K}px;overflow:hidden;background:#000}
  img{position:absolute;display:block} .c{position:absolute;overflow:hidden} .t{position:absolute;white-space:nowrap}
  </style></head><body>${im}\n${tx}</body></html>`;
}

const cctx = await browser.newContext({ viewport: { width: W * K, height: H * K }, deviceScaleFactor: 1 });
const cpage = await cctx.newPage();
const results = [];
for (const [name, sel] of [['level-pass', 3], ['level-pass-L8', 8]]) {
  const rf = refs[name];
  compose(STAND_IN, sel, rf.m);
  fs.writeFileSync(path.join(CHECK, name + '.html'), html(rf.m));
  await cpage.goto(pathToFileURL(path.join(CHECK, name + '.html')).href);
  await cpage.evaluate(() => document.fonts.ready);
  await cpage.evaluate(() => Promise.all([...document.images].map(i => i.decode().catch(() => {}))));
  await cpage.waitForTimeout(300);
  const comp = PNG.sync.read(await cpage.screenshot());
  const ref = rf.png;
  const textMask = new Uint8Array(ref.width * ref.height);   // where Unity draws text (its own engine: counted apart)
  for (const t of rf.m.texts) {
    const x0 = Math.floor(t.box[0] * K) - 4, y0 = Math.floor(t.box[1] * K) - 4, x1 = Math.ceil((t.box[0] + t.box[2]) * K) + 4, y1 = Math.ceil((t.box[1] + t.box[3]) * K) + 4;
    for (let y = Math.max(0, y0); y < Math.min(ref.height, y1); y++) for (let x = Math.max(0, x0); x < Math.min(ref.width, x1); x++) textMask[y * ref.width + x] = 1;
  }
  let diff = 0, big = 0, tdiff = 0, tbig = 0;
  const dpng = new PNG({ width: ref.width, height: ref.height });
  for (let i = 0, p = 0; i < ref.data.length; i += 4, p++) {
    const d = Math.max(Math.abs(ref.data[i] - comp.data[i]), Math.abs(ref.data[i + 1] - comp.data[i + 1]), Math.abs(ref.data[i + 2] - comp.data[i + 2]));
    if (d > 2) { diff++; if (textMask[p]) tdiff++; }
    if (d > 40) { big++; if (textMask[p]) tbig++; }
    // (white: the pieces; dim red: where Unity draws text)
    const v = d > 2 ? Math.min(255, 60 + d * 3) : 0;
    dpng.data[i] = textMask[p] ? v >> 1 : v; dpng.data[i + 1] = dpng.data[i + 2] = textMask[p] ? 0 : v; dpng.data[i + 3] = 255;
  }
  fs.writeFileSync(path.join(CHECK, name + '-composed.png'), PNG.sync.write(comp));
  fs.writeFileSync(path.join(CHECK, name + '-diff.png'), PNG.sync.write(dpng));
  const n = ref.width * ref.height, pc = v => (100 * v / n).toFixed(3) + '%';
  results.push(`${name}: the pieces (outside the text Unity draws): ${pc(diff - tdiff)} of the screen's pixels differ (>2/255), ${pc(big - tbig)} clearly (>40/255);`
    + ` with the text (drawn here by the browser, in Unity by Unity): ${pc(diff)}, ${pc(big)}`);
}
results.forEach(r => console.log('self-check ' + r));
console.log(`  -> ${path.relative(REPO, CHECK)}/*-diff.png`);
await browser.close();
