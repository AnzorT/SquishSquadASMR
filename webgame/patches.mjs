// The only edits the build makes inside the design's own code: one call into window.SquishConfig (app/config.js) at
// each spot where the engines decide what a creature does or what a house item costs, so the admin can control it
// from Firebase. With no config they change nothing (every call returns what the design already had).
//
// Each edit must match the prototype exactly once. If a new prototype zip changed one of these spots, the build stops
// and says which one, instead of shipping a game where a switch silently does nothing.
export const PATCHES = [
  {
    file: 'rive-rig.js',
    why: 'every animation on every screen goes through setState: a switched-off one plays its fallback instead',
    find: "  function setState(I, name, now, keepQ) {\n    if (!STATES[name]) name = 'idle';",
    replace: "  function setState(I, name, now, keepQ) {\n    if (!STATES[name]) name = 'idle';\n    if (window.SquishConfig) name = window.SquishConfig.gateState(I.ak, name, STATES);",
  },
  {
    file: 'rive-rig.js',
    why: 'a sequence (e.g. "poke") starts with its first step: gate that too',
    find: "if (STATES[name].seq) { I.queue = STATES[name].seq.slice(1); name = STATES[name].seq[0]; if (!STATES[name]) name = 'idle'; }",
    replace: "if (STATES[name].seq) { I.queue = STATES[name].seq.slice(1); name = STATES[name].seq[0]; if (!STATES[name]) name = 'idle'; if (window.SquishConfig) name = window.SquishConfig.gateState(I.ak, name, STATES); }",
  },
  {
    file: 'rive-rig.js',
    why: 'idle fidgets: pick only from the switched-on ones',
    find: 'const FL = (I.hfid && I.key && FIDK[I.key]) || FIDGETS;',
    replace: 'const FL = window.SquishConfig ? window.SquishConfig.filter(I.ak, (I.hfid && I.key && FIDK[I.key]) || FIDGETS) : ((I.hfid && I.key && FIDK[I.key]) || FIDGETS);',
  },
  {
    file: 'rig-shuffle.js',
    why: 'menu / picker showcase: pick only switched-on moves',
    find: 'if (!ctrl) return; let k; do { k = LIST[Math.floor(Math.random() * LIST.length)]; } while (k === last);',
    replace: 'if (!ctrl) return; const L = window.SquishConfig && ctrl._I ? window.SquishConfig.filter(ctrl._I.ak, LIST) : LIST; if (!L.length) { wait(); return; } let k; do { k = L[Math.floor(Math.random() * L.length)]; } while (k === last && L.length > 1);',
  },
  {
    file: 'house-walk.js',
    why: 'the walk style (gait) can be chosen per creature',
    find: 'prof(key) { key = (window.SQ_ALIAS && window.SQ_ALIAS[key]) || key; return PROF[key] || DEF; },',
    replace: 'prof(key) { key = (window.SQ_ALIAS && window.SQ_ALIAS[key]) || key; const P0 = PROF[key] || DEF; return window.SquishConfig ? window.SquishConfig.walkProfile(key, P0) : P0; },',
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'furniture actions: a switched-off one is skipped like one the creature does not have',
    find: "hsGet(c, n) { const S = window.RiveRig && window.RiveRig.HS; return S ? S[n + '@' + c.key] || S[n] || null : null; }",
    replace: "hsGet(c, n) { const S = window.RiveRig && window.RiveRig.HS; if (S && window.SquishConfig && !window.SquishConfig.allowed(c.key, n)) return null; return S ? S[n + '@' + c.key] || S[n] || null : null; }",
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'house items: prices and unlock levels from Firebase',
    find: 'const rows = RD.edit(RD.parseCSV(await q.text()))',
    replace: 'const rows = (r => window.SquishConfig ? window.SquishConfig.applyItems(r) : r)(RD.edit(RD.parseCSV(await q.text())))',
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'house items: hidden ones leave the catalog (already bought ones still draw)',
    find: 'if (rows.length) { this.rows = rows; this.byId = new Map(rows.map(x => [x.id, x])); return; }',
    replace: 'if (rows.length) { this.byId = new Map(rows.map(x => [x.id, x])); this.rows = window.SquishConfig ? window.SquishConfig.visibleItems(rows) : rows; return; }',
  },
  // ---- the Main Menu's Mini Games page: the app's games (PLAY / GAMES); the yard games only from the backyard ----
  {
    file: 'main-menu.js',
    why: 'PLAY / GAMES lists the app\'s games (Squish Time, Squishdoku, Sort, Stack, Hop: app/games.js), not the yard games',
    find: 'const games = () => [SQUISH].concat((window.YardGames && window.YardGames.GAMES) || []);',
    replace: 'const games = () => (window.SquishApp && window.SquishApp.menuGames && window.SquishApp.menuGames(SQUISH)) || [SQUISH].concat((window.YardGames && window.YardGames.GAMES) || []);',
  },
  {
    file: 'main-menu.js',
    why: 'an app game is open unless the app says it isn\'t (the yard games keep the Backyard rule)',
    find: "const open = G => G.id === 'squish' || !!(st && st.open.backyard);",
    replace: "const open = G => G.app ? G.open !== false : (G.id === 'squish' || !!(st && st.open.backyard));",
  },
  {
    file: 'main-menu.js',
    why: 'an app game\'s card shows its progress (e.g. "Level 37 / 600")',
    find: "'<div class=\"best\">' + (B[g.id] ? '★ Best ' + B[g.id] : 'New!') + '</div>'",
    replace: "'<div class=\"best\">' + (g.best != null ? esc(g.best) : B[g.id] ? '★ Best ' + B[g.id] : 'New!') + '</div>'",
  },
  {
    file: 'main-menu.js',
    why: 'PLAY on an app game opens it in the app',
    find: "    function playGame(g) {\n      if (g.id === 'squish') {",
    replace: "    function playGame(g) {\n      if (g.app && opts.onPlay) { opts.onPlay(g.id, selId()); return; }\n      if (g.id === 'squish') {",
  },
  {
    file: 'main-menu.js',
    why: 'the same page, as the Yard Games (the backyard\'s button: kind "yard"), also from inside the house',
    find: "    function openGames() {\n      if (busy || inside) return; const G = games(), B = yb(), c = cre();",
    replace: "    function openGames(kind) {\n      if (busy || (inside && kind !== 'yard')) return; opts._yard = kind === 'yard'; const G = opts._yard ? ((window.YardGames && window.YardGames.GAMES) || []) : games(), B = yb(), c = cre();",
  },
  {
    file: 'main-menu.js',
    why: 'the Yard Games page\'s title',
    find: "ft('Mini Games', 'gold', 30)",
    replace: "ft(opts._yard ? 'Yard Games' : 'Mini Games', 'gold', 30)",
  },
  {
    file: 'main-menu.js',
    why: 'the app games page\'s subtitle (the app\'s note; the yard page keeps the design\'s)',
    find: "' ready · your creature plays every game</div>",
    replace: "' ready · ' + (!opts._yard && window.SquishApp && window.SquishApp.gamesNote || 'your creature plays every game') + '</div>",
  },
  {
    file: 'main-menu.js',
    why: 'a yard page opened from the house shows over the house (it sits in the menu frame, under the house)',
    find: "ov.appendChild(page); fitFT(page); return page; }",
    replace: "ov.appendChild(page); if (opts._yard && inside) { host.style.position = 'relative'; host.style.zIndex = '30'; } fitFT(page); return page; }",
  },
  {
    file: 'main-menu.js',
    why: 'closing it shows the house again',
    find: "if (page) { page.remove(); page = null; } }",
    replace: "if (page) { page.remove(); page = null; } host.style.zIndex = ''; }",
  },
  {
    file: 'main-menu.js',
    why: 'a yard game\'s back button returns to the Yard Games, not the app games',
    find: "p.querySelector('[data-x]').onclick = () => openGames();",
    replace: "p.querySelector('[data-x]').onclick = () => openGames(opts._yard ? 'yard' : undefined);",
  },
  {
    file: 'main-menu.js',
    why: 'and so does finishing one',
    find: "if (page && page.contains(mid)) openGames(); }, 0);",
    replace: "if (page && page.contains(mid)) openGames(opts._yard ? 'yard' : undefined); }, 0);",
  },
  // ---- round 11 (2026-11-14, the user's requests): app/menu.js does the new parts; these hand it the menu's insides ----
  {
    file: 'main-menu.js',
    why: 'app/menu.js gets the menu\'s insides (its layers, creature, state, helpers) and is told when the menu is up',
    find: '    draw(); layout(); mountRig();',
    replace: String.raw`    opts._x = { root, frame, world, scene, hots, stage, name, veil, hud, ov, fade, cre, selId, mountRig, nameDraw, hudDraw, draw, layout, toast, pageOpen, closePage, games, open, playGame, fitFT, Q, Lb, Sc,
      get geo() { return geo; }, get k() { return k; }, get busy() { return busy; }, set busy(v) { busy = v; }, get inside() { return inside; },
      get ctrl() { return ctrl; }, get sh() { return sh; }, get st() { return st; }, set preview(v) { preview = v; } };
    draw(); layout(); mountRig();
    if (window.SquishMenu && window.SquishMenu.mounted) window.SquishMenu.mounted(opts._x);`,
  },
  {
    file: 'main-menu.js',
    why: 'CREATURES is CHARACTERS (the user)',
    find: "tab('creatures', 'CREATURES', ",
    replace: "tab('creatures', 'CHARACTERS', ",
  },
  {
    file: 'main-menu.js',
    why: 'no GAMES tab (the user): PLAY is the way to the games',
    find: "+ tab('games', 'GAMES', 'M6 9h12a3 3 0 0 1 3 3v2a3 3 0 0 1-5.4 1.8L14.5 14h-5l-1.1 1.8A3 3 0 0 1 3 14v-2a3 3 0 0 1 3-3Z M8 11v3 M6.5 12.5h3', 'v-vel')",
    replace: '',
  },
  {
    file: 'main-menu.js',
    why: 'no "Let\'s play / Mini Games" board (the user): PLAY stays, on its own',
    find: String.raw`+ '<div class="mm-card mm-p v-vel">' + STARG + gems() + '<div class="k">LET\'S PLAY</div><div class="ttl">' + ft('Mini Games', 'gold', 21) + '</div>'`,
    replace: String.raw`+ '<div class="mm-card sq-solo">'`,
  },
  {
    file: 'main-menu.js',
    why: 'the board\'s "All 5 games ready!" line goes with it',
    find: String.raw`+ '<div class="sub">' + (nOpen === G.length ? 'All ' + G.length + ' games ready!' : nOpen + ' ready · ' + (G.length - nOpen) + ' open at Lv ' + Sc.ROOMS.backyard.lv) + '</div>'`,
    replace: '',
  },
  {
    file: 'main-menu.js',
    why: 'and so do its game icons',
    find: String.raw`+ '<div class="icons">' + G.slice(0, 8).map(g => '<i class="' + (open(g) ? '' : 'lk') + '" title="' + esc(g.name) + '">' + (g.icon || squishIcon()) + '</i>').join('') + '</div>'`,
    replace: '',
  },
  {
    file: 'main-menu.js',
    why: 'PLAY on its own: bigger, with a glint running across it',
    find: String.raw`'<button class="mm-play mm-p tip v-gold" data-act="play" aria-label="Play">' + ft('PLAY', 'whiteB', 30) + '</button></div>'`,
    replace: String.raw`'<button class="mm-play mm-p tip v-gold" data-act="play" aria-label="Play"><span class="sq-glint"></span>' + ft('PLAY', 'whiteB', 34) + '</button></div>'`,
  },
  {
    file: 'main-menu.js',
    why: 'no "Cozy home" / "Next: room" pills (the user); the crest is alive (app/menu.js) and opens the Level Pass',
    find: String.raw`+ '<div class="mm-lv"><div class="tag"><div class="h mm-p tip v-gold" data-act="house">' + st.tierName + '</div>' + (st.next ? '<div class="n mm-p tip v-vel" data-act="house"><span class="tx-w" style="display:flex;align-items:center;gap:4px">' + LOCK + 'Next: ' + st.next.name + ' · Lv ' + st.next.lv + '</span></div>' : '<div class="n mm-p tip v-vel" data-act="house"><span class="tx-w">Every room is open!</span></div>') + '</div><div data-act="house">' + crest(st.L) + '</div></div>';`,
    replace: String.raw`+ '<div class="mm-lv"><div class="sq-crest" data-act="level" role="button" aria-label="Level Pass">' + (window.SquishMenu ? window.SquishMenu.crest(st.L, crest) : crest(st.L)) + '</div></div>';`,
  },
  {
    file: 'main-menu.js',
    why: 'CHARACTERS opens the full Characters screen (app/menu.js), not the small picker',
    find: "if (t === 'creatures') openPicker();",
    replace: "if (t === 'creatures') { if (!(window.SquishMenu && window.SquishMenu.characters(opts._x))) openPicker(); }",
  },
  {
    file: 'main-menu.js',
    why: 'PLAY walks to the Game Park (app/menu.js); the crest opens the Level Pass',
    find: "        if (a === 'play') openGames();",
    replace: "        if (a === 'level') { if (window.SquishMenu) window.SquishMenu.levels(opts._x); else goHome('living'); }\n        else if (a === 'play') { if (!(window.SquishMenu && window.SquishMenu.play(opts._x))) openGames(); }",
  },
  {
    file: 'main-menu-scene.js',
    why: 'no "Lv n · room" sign hanging on each locked part of the house (the user: not needed). The boards stay, so locked still shows.',
    find: '  function lockSign(x, y, lv, name, sc) {\n',
    replace: '  function lockSign(x, y, lv, name, sc) {\n    if (window.SquishMenu) return \'\';\n',
  },
  {
    file: 'main-menu-scene.js',
    why: 'a road from the creature\'s pad to the left edge, where it joins the Game Park\'s road (the user: connect them)',
    find: '    // the pad the creature stands on (a soft lit stone)\n',
    replace: '    if (window.SquishMenu && window.SquishMenu.houseRoad) g += window.SquishMenu.houseRoad(st, c0, c1, mix);\n    // the pad the creature stands on (a soft lit stone)\n',
  },
  {
    file: 'main-menu-scene.js',
    why: 'no stone pad (the user: "the carpet") under the creature: it stands on the path',
    find: "    g += '<ellipse cx=\"500\" cy=\"420\" rx=\"118\" ry=\"24\" fill=\"url(#mmPadGlow)\"/>",
    replace: "    if (!window.SquishMenu) g += '<ellipse cx=\"500\" cy=\"420\" rx=\"118\" ry=\"24\" fill=\"url(#mmPadGlow)\"/>",
  },
  {
    file: 'main-menu-scene.js',
    why: 'the front-left bush stood where that road runs: it moves to the roadside, below it',
    find: 'g += bush(40, 420, 1.4, st, 21) + ',
    replace: 'g += bush(40, 466, 1.4, st, 21) + ',
  },
  {
    file: 'main-menu-scene.js',
    why: 'the Game Park (left of the house): the same sky, grass, trees and weather, drawn by app/menu.js',
    find: '    let g = defs + sky(st) + ground(st);',
    replace: String.raw`    if (st.park && window.SquishMenu && window.SquishMenu.parkArt) {
      let pg = defs + window.SquishMenu.parkArt(st, { sky, ground, tree, bush, palm, weather, mix, rnd, SS });
      const pu = 'mm' + (++UID) + '_'; pg = pg.replace(/(id="|url\(#)mm/g, '$1' + pu);
      return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 460" preserveAspectRatio="xMidYMax slice" width="100%" height="100%" style="display:block">' + pg + '</svg>';
    }
    let g = defs + sky(st) + ground(st);`,
  },
];

export function applyPatches(rel, text) {
  for (const p of PATCHES) {
    if (p.file !== rel) continue;
    const n = text.split(p.find).length - 1;
    if (n !== 1) throw new Error(`patch for ${p.file} (${p.why}): expected the code once, found it ${n} times. The prototype changed here; update webgame/patches.mjs.`);
    text = text.replace(p.find, () => p.replace);
  }
  return text;
}
