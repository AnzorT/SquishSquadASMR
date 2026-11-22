// The Main Menu's Game Park overlay (the games carousel after PLAY: webgame/app/menu.js showGames), taken apart for
// Unity: every piece the browser draws, saved as its own PNG on the Galaxy A15's pixel grid (832 x 384 dp at 2.8125),
// plus layout.json saying where each goes. Unity puts them back together and moves them as the page does
// (GameParkOverlay.cs).
//
//   - The park is opened the instant way (SquishMenu.mounted with SquishApp.screen = 'park', no walk), with stand-in
//     player data (SquishApp.state.games, the app's four games after Squish Time). Transitions are switched off so
//     every piece is shot where it rests; the animations are held (sqSqz at its first frame, sqCen off).
//   - Every card is shot while it is the centre one: its frame (wood, rims, the window's rings; Squish Time's title
//     lettering drawn in), its window picture (a poster, or Squish Time's pink background), the window's inner shading,
//     the PLAY / LOCKED button. Variants UI Toolkit can't make itself are pictures too: brightness(1.35) for sqCen
//     (the centre card's flash), saturate(.7) for the buttons of the cards that aren't the centre one, the locked
//     window (saturate(.2) brightness(.7)) and the lock.
//   - Under the window and the button the frame is cut away (where those cover it fully, 2 px in): Unity's opacity
//     multiplies each picture on its own, the browser's fades the card as one, and only where pictures overlap does
//     that differ. (The button's cut stays covered while it's pressed down 2 px.)
//   - Squish Time's creature squishes (sqSqz): its still picture is its own piece (per creature: the default one here,
//     every creature into Art/Generated with the `creatures` argument), the squish lines too. Frybo's card shows a clip
//     (app/previews/squish.mp4): its first frame is a window picture, the clip is copied next to it.
//   - Plain text (each game's line, its progress chip, the toast's words) is drawn by Unity: layout.json carries its
//     place and style. The chip and the toast are pill plaques that stretch with their words: one picture each, with
//     the slices Unity stretches them by.
//   - The dots: the current one and the others as 18 px wide pills (9-sliced: they change width as they change).
//
//   node webgame/build.mjs --out webgame/test/out/webgame
//   node tools/unity-port/export-game-park.mjs              -> unity/Assets/_Project/Resources/Screens/GamePark/
//   node tools/unity-port/export-game-park.mjs creatures    (also every creature's Squish Time picture -> Art/Generated)
//   then it checks itself: each card's centred state put back together in the browser, compared with the real overlay.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { PNG, REPO, BUILT, SCENES_OUT, W, H, K, needBuild, launch, openMenu, trim } from './lib/menu-page.mjs';

const OUT = path.join(REPO, 'unity/Assets/_Project/Resources/Screens/GamePark');
const CREATURES_OUT = path.join(path.dirname(SCENES_OUT), 'ParkCreatures');   // generated, not in git
const CHECK = path.join(REPO, 'webgame/test/out/unity-park-check');
const ALL_CREATURES = process.argv.includes('creatures');

// Stand-in player data: the app's four games as src/game/player.js menuGames() sends them (Squish Time comes first,
// from the design itself). Their progress lines are drawn by Unity, so any values do.
export const GAMES = [
  { id: 'squishdoku', name: 'Squishdoku', item: '600 puzzle levels', best: 'Level 37 / 600', color: '#3fc4ec' },
  { id: 'sort', name: 'Squish Sort', item: 'Fridge memory', best: 'Level 4 / 50', color: '#ff9a3c' },
  { id: 'stack', name: 'Squish Stack', item: 'Tower puzzles', best: 'Level 9 / 50 · ★ 12', color: '#9b6bff' },
  { id: 'hop', name: 'Squish Hop', item: 'Cushion hopping', best: 'Level 3 / 100', color: '#2fc6a8' },
];
// every transition finished at once (pieces shot where they rest), sqCen (the centre card's flash) off
export const SETTLED_CSS = '*,*::before,*::after{transition:none !important} .sq-slot .sq-gc{animation:none !important}';

const run = async (page, ms) => { for (let t = 0; t < ms; t += 250) { await page.clock.runFor(250); await page.waitForTimeout(40); } };

// The menu, opened straight on the park (the way the app does it coming back from a game: no walk), settled.
// locked: the four app games locked (they're never locked today; Squish Time can't be).
export async function openPark(browser, { locked = false } = {}) {
  const { ctx, page } = await openMenu(browser);
  await page.addStyleTag({ content: SETTLED_CSS });
  await page.evaluate(({ games, locked }) => {
    const A = window.SquishApp;
    A.state.games = games.map(g => Object.assign({}, g, locked ? { open: false } : {}));
    try { localStorage.removeItem('squishsquad.park.v1'); } catch (e) { /* none */ }
    A.screen = 'park'; A._parkDone = false;
    window.SquishMenu.mounted(window.SquishMenu._x);
  }, { games: GAMES, locked });
  await run(page, 1500);
  if (!(await page.$('.sq-pk.in'))) throw new Error('the park did not open');
  return { ctx, page };
}

// the card i in the centre (the dots' own click: go(i))
export async function centre(page, i) {
  await page.evaluate(i => document.querySelectorAll('.sq-pos i')[i].click(), i);
  await page.waitForTimeout(150);
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(new URL(import.meta.url).pathname)) await main();

async function main() {
  needBuild();
  fs.mkdirSync(OUT, { recursive: true });
  fs.mkdirSync(CHECK, { recursive: true });
  // a fresh set (creatures.json stays: it's rewritten only with the `creatures` argument)
  for (const f of fs.readdirSync(OUT)) if (/\.(png|mp4)$/.test(f) || f === 'layout.json') fs.unlinkSync(path.join(OUT, f));
  const browser = await launch();

  const BASE_CSS = 'html,body{background:transparent !important} *{visibility:hidden !important}'
    + ' [data-x-show],[data-x-show] *{visibility:visible !important}'
    + ' [data-x-hide],[data-x-hide] *{visibility:hidden !important}'
    + ' [data-x-bare]{background:none !important;box-shadow:none !important}'
    + ' [data-x-noafter]::after{visibility:hidden !important}';

  // Show only some elements (selectors; everything else hidden, the page see-through), with extra CSS for a variant.
  // bare: their own background and box-shadow go (what's inside stays); noAfter: their ::after goes.
  async function only(page, spec) {
    await page.evaluate(({ spec, BASE_CSS }) => {
      ['data-x-show', 'data-x-hide', 'data-x-bare', 'data-x-noafter'].forEach(a => document.querySelectorAll('[' + a + ']').forEach(e => e.removeAttribute(a)));
      let st = document.getElementById('x-gp');
      if (!st) { st = document.createElement('style'); st.id = 'x-gp'; document.head.appendChild(st); }
      st.textContent = BASE_CSS;
      let vs = document.getElementById('x-gp-var');
      if (!vs) { vs = document.createElement('style'); vs.id = 'x-gp-var'; document.head.appendChild(vs); }
      vs.textContent = spec.css || '';
      const mark = (sels, a) => (sels || []).forEach(s => { const els = document.querySelectorAll(s); if (!els.length && a === 'data-x-show') throw new Error('not found: ' + s); els.forEach(e => e.setAttribute(a, '')); });
      mark(spec.show, 'data-x-show'); mark(spec.hide, 'data-x-hide'); mark(spec.bare, 'data-x-bare'); mark(spec.noAfter, 'data-x-noafter');
    }, { spec, BASE_CSS });
  }
  async function showAll(page, css = '') {
    await page.evaluate(css => {
      ['data-x-show', 'data-x-hide', 'data-x-bare', 'data-x-noafter'].forEach(a => document.querySelectorAll('[' + a + ']').forEach(e => e.removeAttribute(a)));
      const st = document.getElementById('x-gp'); if (st) st.textContent = '';
      const vs = document.getElementById('x-gp-var'); if (vs) vs.textContent = css;
    }, css);
  }
  const box = (page, sel) => page.evaluate(sel => { const e = document.querySelector(sel); if (!e) return null; const b = e.getBoundingClientRect(); return [b.x, b.y, b.width, b.height]; }, sel);
  const pxBox = b => b.map(v => +(v * K).toFixed(3));   // dp -> A15 px (floats: the page's own boxes are fractional)

  // A piece alone, trimmed to its painted pixels: { buf, png, px: [x, y, w, h] } in A15 screen pixels, or null.
  async function shoot(page, spec, clipCss, pad = 40) {
    const [x, y, w, h] = clipCss;
    const px0 = Math.max(0, Math.floor((x - pad) * K)), py0 = Math.max(0, Math.floor((y - pad) * K));
    const px1 = Math.min(W * K, Math.ceil((x + w + pad) * K)), py1 = Math.min(H * K, Math.ceil((y + h + pad) * K));
    await only(page, spec);
    // (pictures with decoding="async" are drawn a moment after they show again: decoded first, then a beat)
    await page.evaluate(() => Promise.all([...document.querySelectorAll('.sq-pk img')].map(im => im.decode().catch(() => {}))));
    await page.waitForTimeout(120);
    const shot = await page.screenshot({ omitBackground: true, clip: { x: px0 / K, y: py0 / K, width: (px1 - px0) / K, height: (py1 - py0) / K } });
    const t = trim(shot);
    if (!t) return null;
    return { buf: t.buf, png: PNG.sync.read(t.buf), px: [px0 + t.x, py0 + t.y, t.w, t.h] };
  }

  // Saved pictures, the same picture at the same place saved once (the four posters' cards share a frame...)
  const saved = [];
  function save(name, s, dir = OUT) {
    if (!s) return null;
    const hash = crypto.createHash('sha1').update(s.buf).digest('hex') + s.px.join(',');
    if (dir === OUT) { const same = saved.find(e => e.hash === hash); if (same) return { image: same.name, px: s.px }; }
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, name + '.png'), s.buf);
    if (dir === OUT) saved.push({ hash, name });
    return { image: name, px: s.px };
  }
  const asShot = (png, px) => ({ buf: PNG.sync.write(png), png, px });

  const layout = {
    screen: { width: W, height: H, scale: K },
    defaults: { creature: 'mittens', cur: 0 },
  };

  // ============ 1. the open games (all of them centred in turn) ============
  const { ctx, page } = await openPark(browser);
  const geo = await page.evaluate(() => {
    const x = window.SquishMenu._x, k = x.k, pgW = x.geo.W / k;
    const carL = Math.round(Math.max(200, pgW * 0.29)), CW = Math.max(300, Math.round(pgW - carL - 118));
    return { k, pgW, carL, CW, games: x.games().map(g => ({ id: g.id, name: g.name, open: x.open(g), best: g.best == null ? null : g.best })), creature: x.cre() && { key: x.cre().key, name: x.cre().name } };
  });
  const k = geo.k;
  console.log(`park: k ${k.toFixed(5)}, page ${geo.pgW.toFixed(1)} wide, carousel from ${geo.carL}, cards ${geo.CW} wide; ${geo.games.length} games; ${geo.creature.name}`);
  layout.page = { k: +k.toFixed(6), designHeight: 393, carL: geo.carL, carTop: 56, CW: geo.CW, slotHeight: 318 };
  layout.car = { box: pxBox(await box(page, '.sq-car')) };

  // ---- the pieces that are the same whichever card is centred ----
  const full = [0, 0, W, H];
  layout.dim = save('dim', await shoot(page, { show: ['.sq-pkdim'] }, full, 0));
  layout.header = {
    box: pxBox(await box(page, '.sq-pk .mm-pgh')),
    back: save('back', await shoot(page, { show: ['.sq-pk .mm-pgh .mm-orb'] }, await box(page, '.sq-pk .mm-pgh .mm-orb'), 20)),
    title: save('title', await shoot(page, { show: ['.sq-pk .mm-pgh svg.mm-ft'] }, await box(page, '.sq-pk .mm-pgh svg.mm-ft'), 20)),
  };
  // the rays and the halo as they rest (.55 and 1): Unity fades each picture in from 0 to 1, which is the page's
  // 0 -> .55 and 0 -> 1 (opacity multiplies)
  layout.rays = Object.assign(save('rays', await shoot(page, { show: ['.sq-rays'] }, await box(page, '.sq-rays'), 4)), { drawnAt: 0.55 });
  layout.halo = Object.assign(save('halo', await shoot(page, { show: ['.sq-halo'] }, await box(page, '.sq-halo'), 4)), { drawnAt: 1 });
  // the arrows, both at full strength (.off is opacity .4: Unity sets it)
  const ARR = '.sq-arr{opacity:1 !important}';
  layout.arrows = {
    left: save('arrow-l', await shoot(page, { show: ['.sq-arr.l'], css: ARR }, await box(page, '.sq-arr.l'), 20)),
    right: save('arrow-r', await shoot(page, { show: ['.sq-arr.r'], css: ARR }, await box(page, '.sq-arr.r'), 20)),
    leftBox: pxBox(await box(page, '.sq-arr.l')), rightBox: pxBox(await box(page, '.sq-arr.r')),
    offOpacity: 0.4,
  };
  // the dots: the current one (18 x 7) and the others, also drawn 18 wide (Unity 9-slices both to their width)
  {
    const onBox = await box(page, '.sq-pos i.on');
    const on = await shoot(page, { show: ['.sq-pos i.on'] }, onBox, 6);
    const WIDE = '.sq-pos i:nth-child(2){width:18px !important;border-radius:4px !important}';
    await showAll(page, WIDE);
    const offBox = await box(page, '.sq-pos i:nth-child(2)');
    const off = await shoot(page, { show: ['.sq-pos i:nth-child(2)'], css: WIDE }, offBox, 6);
    const rel = (s, b) => [s.px[0] - b[0] * K, s.px[1] - b[1] * K].map(v => +v.toFixed(3));
    // the caps the slices keep whole: a little under half the height (the pill's ends, its ring and edge)
    const slice = Math.floor(on.png.height / 2);
    await showAll(page);
    layout.dots = {
      on: Object.assign(save('dot-on', on), { offset: rel(on, onBox) }),
      off: Object.assign(save('dot-off', off), { offset: rel(off, offBox) }),
      slice,
      row: pxBox(await box(page, '.sq-pos')),
      wOn: 18, wOff: 7, h: 7, gap: 6, bottom: 7,
    };
  }

  // ---- each card, centred ----
  const C = '.sq-slot.c';
  const BRIGHT = `${C} .sq-gc{filter:brightness(1.35) !important}`;
  const DIM = `${C} .go{filter:saturate(.7) !important}`;
  layout.slot = { box: pxBox(await box(page, C)) };
  layout.cards = [];
  const masks = {};   // per card: where the window and the button cover the frame fully (frame cut-outs)
  for (let i = 0; i < geo.games.length; i++) {
    const g = geo.games[i];
    await centre(page, i);
    const slotB = await box(page, C);
    const card = { id: g.id, name: g.name, open: g.open };
    // the frame: the card without its window's insides and its info row (title lettering stays: it never changes)
    const frameSpec = { show: [`${C} .sq-gc`], hide: [`${C} .win > *`, `${C} .info`], noAfter: [`${C} .win`] };
    const frame = await shoot(page, frameSpec, slotB, 60);
    const frameB = await shoot(page, Object.assign({}, frameSpec, { css: BRIGHT }), slotB, 60);
    // the window: its picture alone, clipped to its rounded corners (no rings, no shading, no lock)
    const winB = await box(page, `${C} .win`);
    const winSpec = { show: [`${C} .win`], hide: [`${C} .lockv`, `${C} .sq-sqz > *`, `${C} .win video`], bare: [`${C} .win`], noAfter: [`${C} .win`] };
    const win = await shoot(page, winSpec, winB, 4);
    const winBright = await shoot(page, Object.assign({}, winSpec, { css: BRIGHT }), winB, 4);
    // the window's inner shading (::after: over the picture, the lock and the squishing creature)
    const shade = await shoot(page, { show: [`${C} .win`], hide: [`${C} .win > *`], bare: [`${C} .win`] }, winB, 4);
    // the button
    const goB = await box(page, `${C} .go`);
    const go = await shoot(page, { show: [`${C} .go`] }, goB, 20);
    const goDim = await shoot(page, { show: [`${C} .go`], css: DIM }, goB, 20);
    const goBright = await shoot(page, { show: [`${C} .go`], css: BRIGHT }, goB, 20);

    masks[g.id] = { frame: frame.px, win: win, go: go };
    card.frameShots = { normal: frame, bright: frameB };   // saved after the cut-outs (below)
    card.win = { box: pxBox(winB), radius: +(parseFloat(await page.evaluate(s => getComputedStyle(document.querySelector(s)).borderTopLeftRadius, `${C} .win`)) * k * K).toFixed(3),
      image: save('win-' + g.id, win).image, bright: save('win-' + g.id + '-bright', winBright).image, px: win.px };
    card.shade = save('shade-' + g.id, shade);
    card.go = { box: pxBox(goB), play: save('go-play', go), dim: save('go-play-dim', goDim), bright: save('go-play-bright', goBright) };

    // the info row's words (Unity draws them): the line, as the browser broke it, and the progress chip
    const info = await page.evaluate(C => {
      const s = document.querySelector(C), ds = s.querySelector('.ds'), best = s.querySelector('.best');
      const R = b => [b.x, b.y, b.width, b.height];
      const lines = [];
      if (ds && ds.firstChild) {
        const t = ds.firstChild, txt = t.textContent, r = document.createRange();
        let start = 0, top = null;
        for (let j = 0; j <= txt.length; j++) {
          let y = null;
          if (j < txt.length) { r.setStart(t, j); r.setEnd(t, j + 1); const rs = r.getClientRects(); if (rs.length) y = rs[0].y; }
          if (j === txt.length || (top != null && y != null && Math.abs(y - top) > 2)) {
            r.setStart(t, start); r.setEnd(t, j); const rs = [...r.getClientRects()];
            const x0 = Math.min(...rs.map(b => b.x)), x1 = Math.max(...rs.map(b => b.x + b.width));
            lines.push({ text: txt.slice(start, j), box: [x0, rs[0].y, x1 - x0, rs[0].height] });
            start = j; top = y;
          } else if (top == null) top = y;
        }
      }
      const bt = document.createRange(); bt.selectNodeContents(best);
      return { ds: ds ? { text: ds.textContent, box: R(ds.getBoundingClientRect()), maxWidth: s.querySelector('.info .txt').getBoundingClientRect().width, lines } : null,
        best: { text: best.textContent, box: R(best.getBoundingClientRect()), textBox: R(bt.getBoundingClientRect()) } };
    }, C);
    card.desc = info.ds && { text: info.ds.text, box: pxBox(info.ds.box), maxWidth: +(info.ds.maxWidth * K).toFixed(3), lines: info.ds.lines.map(l => ({ text: l.text, box: pxBox(l.box) })) };
    card.best = { text: info.best.text, box: pxBox(info.best.box), textBox: pxBox(info.best.textBox) };

    if (g.id === 'squish') {
      // the squishing creature (its <img>, held at sqSqz's first frame) and the squish lines (shot fully on)
      const imgB = await box(page, `${C} .sq-sqz img`);
      const SQZ = '.sq-sqz img{animation:none !important;transform:none !important} .sq-sqz i{animation:none !important;opacity:1 !important}';
      const cre = await shoot(page, { show: [`${C} .sq-sqz img`], css: SQZ }, imgB, 4);
      const creB = await shoot(page, { show: [`${C} .sq-sqz img`], css: SQZ + BRIGHT }, imgB, 4);
      const key = geo.creature.key;
      card.squish = {
        imgBox: pxBox(imgB),
        lineL: save('sqz-line-l', await shoot(page, { show: [`${C} .sq-sqz i.l`], css: SQZ }, await box(page, `${C} .sq-sqz i.l`), 6)),
        lineR: save('sqz-line-r', await shoot(page, { show: [`${C} .sq-sqz i.r`], css: SQZ }, await box(page, `${C} .sq-sqz i.r`), 6)),
        creature: { key, image: save('sqz-' + key, cre).image, bright: save('sqz-' + key + '-bright', creB).image, px: cre.px },
      };
      // Frybo's card: the clip's first frame in the window (menu.js PREVIEWS: <img src="app/previews/squish.jpg">,
      // object-fit cover), the clip itself next to it for Unity's video player
      await page.evaluate(C => {
        const w = document.querySelector(C + ' .win'), keep = w.innerHTML; w.dataset.keep = keep;
        w.innerHTML = '<img alt="" src="app/previews/squish.jpg">';
      }, C);
      await page.waitForFunction(C => { const im = document.querySelector(C + ' .win img'); return im && im.complete && im.naturalWidth > 0; }, C);
      const clip = await shoot(page, winSpec, winB, 4);
      const clipB = await shoot(page, Object.assign({}, winSpec, { css: BRIGHT }), winB, 4);
      await page.evaluate(C => { const w = document.querySelector(C + ' .win'); w.innerHTML = w.dataset.keep; delete w.dataset.keep; }, C);
      fs.copyFileSync(path.join(BUILT, 'app/previews/squish.mp4'), path.join(OUT, 'squish-clip.mp4'));
      card.squish.clip = { creature: 'frybo', image: save('win-squish-clip', clip).image, bright: save('win-squish-clip-bright', clipB).image, px: clip.px, video: 'squish-clip', size: [1000, 400] };
    }
    layout.cards.push(card);
    console.log(`card ${i} ${g.id}: frame ${frame.px[2]} x ${frame.px[3]}, window ${win.px[2]} x ${win.px[3]}${card.desc ? `, ${card.desc.lines.length} lines` : ''}`);
  }

  // the progress chip (a pill: one picture, stretched between its ends) and the words' styles
  await centre(page, 0);
  {
    const bB = await box(page, `${C} .best`);
    const NOTXT = `${C} .best{color:transparent !important}`;
    const chip = await shoot(page, { show: [`${C} .best`], css: NOTXT }, bB, 4);
    const chipB = await shoot(page, { show: [`${C} .best`], css: NOTXT + BRIGHT }, bB, 4);
    layout.chip = Object.assign(save('chip', chip), { bright: save('chip-bright', chipB).image, box: pxBox(bB), slice: Math.floor(chip.png.height / 2) });
  }
  await showAll(page);
  const style = sel => page.evaluate(sel => {
    const c = getComputedStyle(document.querySelector(sel));
    const shadows = c.textShadow === 'none' ? [] : c.textShadow.split(/,(?![^(]*\))/).map(s => {
      const col = (s.match(/rgba?\([^)]*\)/) || ['rgb(0,0,0)'])[0], nums = s.replace(col, '').trim().split(/\s+/).map(parseFloat);
      return { color: col, x: nums[0] || 0, y: nums[1] || 0, blur: nums[2] || 0 };
    });
    const lh = parseFloat(c.lineHeight);
    return { font: c.fontFamily.split(',')[0].replace(/["']/g, '').trim(), weight: +c.fontWeight, size: parseFloat(c.fontSize), color: c.color,
      letterSpacing: parseFloat(c.letterSpacing) || 0, lineHeight: isNaN(lh) ? 0 : lh, shadows,
      padding: [parseFloat(c.paddingTop), parseFloat(c.paddingRight), parseFloat(c.paddingBottom), parseFloat(c.paddingLeft)],
      background: c.backgroundColor };
  }, sel);
  // (sizes in the page's own px: Unity multiplies by k, as the page's scale(k) does)
  layout.text = { desc: await style(`${C} .ds`), best: await style(`${C} .best`) };
  // The ★ (Squish Hop's line, a chip's stars): Nunito has none, so the browser draws it from a fallback font; Unity
  // can't count on having one. A picture of it in each style, with its advance, set in the words' row.
  layout.stars = {};
  for (const [key, sel] of [['desc', `${C} .ds`], ['best', `${C} .best`]]) {
    await page.evaluate(sel => { const e = document.querySelector(sel); e.dataset.keep = e.innerHTML; e.innerHTML = '<span class="x-star">\u2605</span>'; }, sel);
    const sB = await box(page, '.x-star');
    const st = await shoot(page, { show: ['.x-star'] }, sB, 6);
    const stB = await shoot(page, { show: ['.x-star'], css: BRIGHT }, sB, 6);
    await page.evaluate(sel => { const e = document.querySelector(sel); e.innerHTML = e.dataset.keep; delete e.dataset.keep; }, sel);
    layout.stars[key] = Object.assign(save('star-' + key, st), { bright: save('star-' + key + '-bright', stB).image, box: pxBox(sB) });
    // a space's advance in that style (next to the ★, where Unity's labels stop): "a a" less "aa"
    layout.stars[key + 'Space'] = await page.evaluate(sel => {
      const e = document.querySelector(sel), keep = e.innerHTML, w = t => { e.innerHTML = '<span class="x-w">' + t + '</span>'; return e.querySelector('.x-w').getBoundingClientRect().width; };
      const v = w('a a') - w('aa'); e.innerHTML = keep; return v;
    }, sel) * K;
  }
  await showAll(page);
  await showAll(page);
  await ctx.close();

  // ============ 2. the locked games (the four app games locked: their window, the lock, LOCKED, the toast) ============
  {
    const { ctx, page } = await openPark(browser, { locked: true });
    for (let i = 0; i < geo.games.length; i++) {
      if (geo.games[i].id === 'squish') continue;   // (always open)
      await centre(page, i);
      const card = layout.cards[i];
      const winB = await box(page, `${C} .win`);
      const winSpec = { show: [`${C} .win`], hide: [`${C} .lockv`], bare: [`${C} .win`], noAfter: [`${C} .win`] };
      const win = await shoot(page, winSpec, winB, 4);
      const winBright = await shoot(page, Object.assign({}, winSpec, { css: BRIGHT }), winB, 4);
      card.locked = { image: save('win-' + card.id + '-locked', win).image, bright: save('win-' + card.id + '-locked-bright', winBright).image, px: win.px };
      const lockB = await box(page, `${C} .lockv svg`);
      card.lock = Object.assign(save('lock', await shoot(page, { show: [`${C} .lockv`] }, lockB, 6)),
        { bright: save('lock-bright', await shoot(page, { show: [`${C} .lockv`], css: BRIGHT }, lockB, 6)).image });
      const goB = await box(page, `${C} .go`);
      card.go.locked = save('go-locked', await shoot(page, { show: [`${C} .go`] }, goB, 20));
      card.go.lockedDim = save('go-locked-dim', await shoot(page, { show: [`${C} .go`], css: DIM }, goB, 20));
      card.go.lockedBright = save('go-locked-bright', await shoot(page, { show: [`${C} .go`], css: BRIGHT }, goB, 20));
    }
    // the toast (playAt on a locked game): its plaque, words taken out; mmToast held at its resting frame
    await centre(page, 1);
    await page.evaluate(C => document.querySelector(C + ' .go').click(), C);
    await page.waitForTimeout(100);
    const TOAST = '.sq-toast{animation:none !important;opacity:1 !important}';
    await showAll(page, TOAST);
    const tB = await box(page, '.sq-toast');
    const plaque = await shoot(page, { show: ['.sq-toast'], css: TOAST + '.sq-toast{color:transparent !important}' }, tB, 30);
    await showAll(page, TOAST);
    const ts = await page.evaluate(() => {
      const t = document.querySelector('.sq-toast'), c = getComputedStyle(t), r = document.createRange(); r.selectNodeContents(t);
      const b = r.getBoundingClientRect();
      return { text: t.textContent, textBox: [b.x, b.y, b.width, b.height], font: c.fontFamily.split(',')[0].replace(/["']/g, '').trim(), weight: +c.fontWeight,
        size: parseFloat(c.fontSize), color: c.color, letterSpacing: parseFloat(c.letterSpacing) || 0, lineHeight: 0, shadows: [],
        padding: [parseFloat(c.paddingTop), parseFloat(c.paddingRight), parseFloat(c.paddingBottom), parseFloat(c.paddingLeft)],
        border: parseFloat(c.borderTopWidth) || 0, background: '' };
    });
    // the plaque's ends (its rounded caps with their rings and shadow) stay whole; its middle stretches
    const capPx = Math.ceil(tB[0] * K - plaque.px[0] + tB[3] * K / 2) + 2;
    layout.toast = Object.assign(save('toast', plaque), { box: pxBox(tB), textBox: pxBox(ts.textBox), sample: ts.text, slice: capPx, bottom: 20, maxWidth: 0.7 });
    layout.text.toast = Object.assign({}, ts, { text: undefined, textBox: undefined });
    await showAll(page);
    await ctx.close();
  }

  // ============ 3. the frames, cut away where the window and the button cover them fully ============
  // (2 px in from those pictures' edges, so moving and scaled the seam never opens; the button's cut is the part it
  // covers both up and pressed down 2 px)
  const press = Math.round(2 * k * K);
  for (const card of layout.cards) {
    const m = masks[card.id];
    const covered = (pic, x, y) => {   // fully opaque at screen px (x, y), and 2 px around
      for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
        const X = x + dx - pic.px[0], Y = y + dy - pic.px[1];
        if (X < 0 || Y < 0 || X >= pic.png.width || Y >= pic.png.height) return false;
        if (pic.png.data[(Y * pic.png.width + X) * 4 + 3] < 255) return false;
      }
      return true;
    };
    const lockedWin = card.locked ? { png: PNG.sync.read(fs.readFileSync(path.join(OUT, card.locked.image + '.png'))), px: card.locked.px } : null;
    const cut = shot => {
      const png = PNG.sync.read(PNG.sync.write(shot.png)), [fx, fy] = shot.px;
      for (let y = 0; y < png.height; y++) for (let x = 0; x < png.width; x++) {
        const X = fx + x, Y = fy + y;
        const underWin = covered(m.win, X, Y) && (!lockedWin || covered(lockedWin, X, Y));
        const underGo = covered(m.go, X, Y) && covered(m.go, X, Y - press);
        if (underWin || underGo) png.data[(y * png.width + x) * 4 + 3] = 0;
      }
      return asShot(png, shot.px);
    };
    const f = save('frame-' + card.id, cut(card.frameShots.normal));
    const fb = save('frame-' + card.id + '-bright', cut(card.frameShots.bright));
    card.frame = { image: f.image, bright: fb.image, px: f.px };
    delete card.frameShots;
  }

  // ============ 4. every creature's Squish Time picture (optional: big, generated, not in git) ============
  const squishCard = layout.cards.find(c => c.id === 'squish');
  if (ALL_CREATURES) {
    const { ctx, page } = await openPark(browser);
    await centre(page, 0);
    const roster = await page.evaluate(() => window.SquishMenu._x.Q.ROSTER.map((c, id) => c && ({ id, key: c.key, name: c.name })).filter(Boolean));
    const imgB = await box(page, `${C} .sq-sqz img`);
    const SQZ = '.sq-sqz img{animation:none !important;transform:none !important}';
    const items = [];
    for (const c of roster) {
      // menu.js still(c, Q): the creature's thumbnail (offline there are no border looks: state.looks)
      const ok = await page.evaluate(({ C, id }) => new Promise(res => {
        const Q = window.SquishMenu._x.Q, T = window.SQ_THUMBS || {}, s = Q.src(id), u = s ? T[s] || s : null;
        const im = document.querySelector(C + ' .sq-sqz img');
        if (!u || !im) { res(false); return; }
        im.onload = () => res(true); im.onerror = () => res(false); im.src = u;
      }), { C, id: c.id });
      if (!ok) { console.log(`  ${c.key}: no picture`); continue; }
      await page.waitForTimeout(50);
      const s = await shoot(page, { show: [`${C} .sq-sqz img`], css: SQZ }, imgB, 4);
      const sb = await shoot(page, { show: [`${C} .sq-sqz img`], css: SQZ + BRIGHT }, imgB, 4);
      if (!s) continue;
      save('sqz-' + c.key, s, CREATURES_OUT);
      save('sqz-' + c.key + '-bright', sb, CREATURES_OUT);
      items.push({ key: c.key, name: c.name, px: s.px });
    }
    fs.writeFileSync(path.join(OUT, 'creatures.json'), JSON.stringify({ items }, null, 1));
    console.log(`creatures: ${items.length} -> ${path.relative(REPO, CREATURES_OUT)}`);
    await ctx.close();
  }

  fs.writeFileSync(path.join(OUT, 'layout.json'), JSON.stringify(layout, null, 1));
  const total = fs.readdirSync(OUT).filter(f => f.endsWith('.png')).reduce((n, f) => n + fs.statSync(path.join(OUT, f)).size, 0);
  console.log(`layout.json: ${layout.cards.length} cards; ${saved.length} pictures, ${(total / 1e6).toFixed(1)} MB`);

  // ============ 5. self-check ============
  await selfCheck(browser, layout, squishCard);
  await browser.close();
}

// Each card centred (and a locked one): the pieces put back together in the browser the way Unity places them, over
// the same page with the overlay hidden, compared with the real overlay.
async function selfCheck(browser, L, squishCard) {
  const k = L.page.k;
  const fontCss = fs.readdirSync(path.join(BUILT, 'vendor/css')).map(f => fs.readFileSync(path.join(BUILT, 'vendor/css', f), 'utf8')).join('\n')
    .replace(/url\(([^)]+)\)/g, (m, u) => `url(${pathToFileURL(path.join(BUILT, 'vendor', u.replace(/["']/g, '').replace(/^\.\.\//, ''))).href})`);
  const cctx = await browser.newContext({ viewport: { width: W * K, height: H * K }, deviceScaleFactor: 1 });
  const cpage = await cctx.newPage();
  const img = (name, px, extra = '') => `<img src="${pathToFileURL(path.join(OUT, name + '.png')).href}" style="left:${px[0]}px;top:${px[1]}px;${extra}">`;
  const txt = (t, box, s, extra = '') => {
    const sh = (s.shadows || []).map(q => `${q.color} ${q.x * k * K}px ${q.y * k * K}px ${q.blur * k * K}px`).join(',');
    return `<div class="t" style="left:${box[0]}px;top:${box[1]}px;height:${box[3]}px;line-height:${box[3]}px;font:${s.weight} ${s.size * k * K}px ${s.font};letter-spacing:${s.letterSpacing * k * K}px;color:${s.color};${sh ? 'text-shadow:' + sh + ';' : ''}${extra}">${t.replace(/&/g, '&amp;').replace(/</g, '&lt;')}</div>`;
  };
  // the dots as Unity draws them: a 9-sliced pill per dot, the row centred where the page centres it
  const dots = (n, cur) => {
    const D = L.dots, rowW = (D.wOn + (n - 1) * D.wOff + (n - 1) * D.gap) * k * K, cx = D.row[0] + D.row[2] / 2;
    let x = cx - rowW / 2, out = '';
    for (let i = 0; i < n; i++) {
      const on = i === cur, w = (on ? D.wOn : D.wOff) * k * K, pic = on ? D.on : D.off;
      const pw = pic.px[2] - (D.wOn - (on ? D.wOn : D.wOff)) * k * K;
      const left = Math.round(x + pic.offset[0]), top = Math.round(D.row[1] + pic.offset[1]);
      out += `<div style="position:absolute;left:${left}px;top:${top}px;width:${pw}px;height:${pic.px[3]}px;box-sizing:border-box;border-style:solid;border-width:0 ${D.slice}px;border-image:url(${pathToFileURL(path.join(OUT, pic.image + '.png')).href}) 0 ${D.slice} fill / 0 ${D.slice}px stretch"></div>`;
      x += w + D.gap * k * K;
    }
    return out;
  };
  const cases = L.cards.map((c, i) => ({ cur: i, locked: false })).concat(L.cards.some(c => c.locked) ? [{ cur: 1, locked: true }] : []);
  const lines = [];
  for (const cs of cases) {
    const { ctx, page } = await openPark(browser, { locked: cs.locked });
    await centre(page, cs.cur);
    const real = await page.screenshot();
    await page.addStyleTag({ content: '.sq-pk{visibility:hidden !important}' });
    await page.waitForTimeout(100);
    const base = await page.screenshot();
    await ctx.close();
    const basePath = path.join(CHECK, '_base.png');
    fs.writeFileSync(basePath, base);
    const c = L.cards[cs.cur], n = L.cards.length;
    const locked = cs.locked && c.locked;
    let html = `<!doctype html><html><head><style>${fontCss}
      html,body{margin:0;width:${W * K}px;height:${H * K}px;overflow:hidden}
      img{position:absolute;display:block} .t{position:absolute;white-space:nowrap}
    </style></head><body><img src="${pathToFileURL(basePath).href}" style="left:0;top:0">`;
    html += img(L.dim.image, L.dim.px);
    html += img(L.header.back.image, L.header.back.px) + img(L.header.title.image, L.header.title.px);
    html += img(L.rays.image, L.rays.px) + img(L.halo.image, L.halo.px);
    // the centre card
    html += img(c.frame.image, c.frame.px);
    if (c.squish) {
      html += img(c.win.image, c.win.px);
      // (the lines are see-through at sqLines' first frame; the creature at sqSqz's)
      html += img(c.squish.creature.image, c.squish.creature.px);
    } else html += img(locked ? c.locked.image : c.win.image, locked ? c.locked.px : c.win.px);
    if (locked) html += img(c.lock.image, c.lock.px);
    html += img(c.shade.image, c.shade.px);
    const d = L.text.desc, b = L.text.best;
    if (c.desc) c.desc.lines.forEach(l => { html += txt(l.text, [l.box[0], l.box[1], l.box[2], l.box[3]], d); });
    // the chip: its pill stretched to the words' width, the words in it
    html += `<div style="position:absolute;left:${c.best.box[0] + (L.chip.px[0] - L.chip.box[0])}px;top:${c.best.box[1] + (L.chip.px[1] - L.chip.box[1])}px;width:${c.best.box[2] + (L.chip.px[2] - L.chip.box[2])}px;height:${L.chip.px[3]}px;box-sizing:border-box;border-style:solid;border-width:0 ${L.chip.slice}px;border-image:url(${pathToFileURL(path.join(OUT, L.chip.image + '.png')).href}) 0 ${L.chip.slice} fill / 0 ${L.chip.slice}px stretch"></div>`;
    html += txt(c.best.text, c.best.textBox, b);
    const go = locked ? c.go.locked : c.go.play;
    html += img(go.image, go.px);
    // the arrows, the dots
    html += img(L.arrows.left.image, L.arrows.left.px, cs.cur === 0 ? `opacity:${L.arrows.offOpacity}` : '');
    html += img(L.arrows.right.image, L.arrows.right.px, cs.cur === n - 1 ? `opacity:${L.arrows.offOpacity}` : '');
    html += dots(n, cs.cur);
    html += '</body></html>';
    const file = path.join(CHECK, '_check.html');
    fs.writeFileSync(file, html);
    await cpage.goto(pathToFileURL(file).href);
    await cpage.evaluate(() => document.fonts.ready);
    await cpage.waitForTimeout(300);
    const composed = PNG.sync.read(await cpage.screenshot());
    const ref = PNG.sync.read(real);
    let diff = 0, big = 0, inCard = 0, nCard = 0, picDiff = 0, picBig = 0, nPic = 0;
    // the words Unity draws (their boxes, grown for their shadows): left out of the "pictures only" count
    const words = (c.desc ? c.desc.lines.map(l => l.box) : []).concat([c.best.textBox]).map(b => [b[0] - 8, b[1] - 8, b[2] + 16, b[3] + 16]);
    const inWords = (x, y) => words.some(b => x >= b[0] && x < b[0] + b[2] && y >= b[1] && y < b[1] + b[3]);
    const [sx, sy, sw, sh] = L.slot.box;
    const dpng = new PNG({ width: ref.width, height: ref.height });
    for (let y = 0; y < ref.height; y++) for (let x = 0; x < ref.width; x++) {
      const i = (y * ref.width + x) * 4;
      const dd = Math.max(Math.abs(ref.data[i] - composed.data[i]), Math.abs(ref.data[i + 1] - composed.data[i + 1]), Math.abs(ref.data[i + 2] - composed.data[i + 2]));
      const card = x >= sx && x < sx + sw && y >= sy && y < sy + sh;
      if (card) nCard++;
      if (dd > 2) { diff++; if (card) inCard++; }
      if (!inWords(x, y)) { nPic++; if (dd > 2) picDiff++; if (dd > 40) picBig++; }
      if (dd > 40) big++;
      dpng.data[i] = dpng.data[i + 1] = dpng.data[i + 2] = dd > 2 ? Math.min(255, 60 + dd * 3) : 0; dpng.data[i + 3] = 255;
    }
    const tag = `${c.id}${cs.locked ? '-locked' : ''}`;
    fs.writeFileSync(path.join(CHECK, `${tag}-reference.png`), real);
    fs.writeFileSync(path.join(CHECK, `${tag}-composed.png`), PNG.sync.write(composed));
    fs.writeFileSync(path.join(CHECK, `${tag}-diff.png`), PNG.sync.write(dpng));
    const N = ref.width * ref.height;
    const line = `self-check ${tag.padEnd(16)} ${(100 * diff / N).toFixed(3)}% of pixels differ (>2/255), ${(100 * big / N).toFixed(3)}% clearly (>40/255); in the card ${(100 * inCard / nCard).toFixed(2)}%; pictures only ${(100 * picDiff / nPic).toFixed(3)}% (${(100 * picBig / nPic).toFixed(3)}% clearly)`;
    console.log(line); lines.push(line);
  }
  fs.unlinkSync(path.join(CHECK, '_check.html'));
  fs.unlinkSync(path.join(CHECK, '_base.png'));
  fs.writeFileSync(path.join(CHECK, 'self-check.txt'), lines.join('\n') + '\n');
  console.log(`-> ${path.relative(REPO, CHECK)}/<card>-diff.png`);
  await cctx.close();
}
