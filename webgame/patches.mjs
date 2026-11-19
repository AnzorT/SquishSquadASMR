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
    why: 'an app game is open unless the app says it isn\'t; a yard game needs the Backyard and (2026-11-18, the user) its toy bought there (app/games.js yardLocked)',
    find: "const open = G => G.id === 'squish' || !!(st && st.open.backyard);",
    replace: "const open = G => G.app ? G.open !== false : (G.id === 'squish' || (!!(st && st.open.backyard) && !(window.SquishApp && window.SquishApp.yardLocked && window.SquishApp.yardLocked(G.id))));",
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
  // ---- the yard games' zip (2026-11-18, yard-mini-games.zip, laid over the prototype by build.mjs): the games went
  // landscape (yard-games v6) and got intro posters (yg-intros.js). These bring the designer's main-menu v14 parts
  // they need into our Nov 13 menu. ----
  {
    file: 'Main Menu.dc.html',
    why: 'the yard games\' intro posters (yg-intros.js: the zip\'s load order, after yard-games.js, before the games)',
    find: '<script src="yard-games.js?v=1"></script>',
    replace: '<script src="yard-games.js?v=6"></script>\n<script src="yg-intros.js?v=7"></script>',
  },
  {
    file: 'main-menu.js',
    why: 'a landscape yard game (G.land) plays across the whole page: no side columns (its own top bar and intro sheet carry back and how to play; main-menu v14)',
    find: '.mm-gp .bst{padding:4px 12px;border-radius:999px;font-size:12px;}',
    replace: '.mm-gp .bst{padding:4px 12px;border-radius:999px;font-size:12px;}\n.mm-gp.land .side{display:none;}\n.mm-gp.land .mid{flex:1;aspect-ratio:auto;padding:6px;display:flex;align-items:center;justify-content:center;}',
  },
  {
    file: 'rive-rig.js',
    why: 'no built-in look line (the app turns it off: app/rig-look.js draws the Firebase look): the creature\'s shapes aren\'t measured for one (it sampled every shape with isPointInFill as each creature appeared)',
    find: '  function outline(I) {\n    if (I.generic && !I.ready) return;\n',
    replace: '  function outline(I) {\n    if (I.generic && !I.ready) return;\n    if (!I.olEls && !(I.curL && I.curL.line)) { I.olDirty = 0; return; }\n',
  },
  {
    file: 'rive-rig.js',
    why: 'isPointInFill with a point older WebViews take (they want an SVGPoint, not a DOMPoint: "parameter 1 is not of type SVGPoint")',
    find: 'under[k].e.isPointInFill(new DOMPoint(lp.x, lp.y))',
    replace: 'under[k].e.isPointInFill(sqSvgPt(under[k].e, lp.x, lp.y))',
  },
  {
    file: 'rive-rig.js',
    why: '(its helper: createSVGPoint gives the type every WebView takes)',
    find: '  function outlineEls(I) {',
    replace: '  function sqSvgPt(e, x, y) { const s = e.ownerSVGElement; if (!s || !s.createSVGPoint) return new DOMPoint(x, y); const q = s.createSVGPoint(); q.x = x; q.y = y; return q; }\n  function outlineEls(I) {',
  },
  {
    file: 'main-menu.js',
    why: 'the gold ring round a yard game: its outer edge in the wood\'s brown, not purple (REDESIGN_V8\'s rule; now that the game fills the page it shows all round it)',
    find: '.mm-gp .mid .yg-frame{height:100%;width:auto;max-width:none;box-shadow:0 0 0 4px #ffd76a,0 0 0 6px #3d1268,',
    replace: '.mm-gp .mid .yg-frame{height:100%;width:auto;max-width:none;box-shadow:0 0 0 4px #ffd76a,0 0 0 6px #6b4426,',
  },
  {
    file: 'main-menu.js',
    why: 'the game page is .land for a landscape game (main-menu v14)',
    find: "const p = pageOpen('<div class=\"mm-gp\"><div class=\"side\">",
    replace: "const p = pageOpen('<div class=\"mm-gp' + (g.land ? ' land' : '') + '\"><div class=\"side\">",
  },
  // ---- 2026-11-18, the user on the Yard Games: wide cards with each game's poster, a list you drag sideways, and a
  // game opens only once its toy is bought in the Backyard (app/games.js yardCard / yardPage / yardLocked / yardNeed) ----
  {
    file: 'main-menu.js',
    why: 'a yard game\'s card on the Yard Games page: wide, with the game\'s poster (app/games.js yardCard)',
    find: "      const card = g => { const op = open(g), col = g.color || '#ff8a3d', feat = g.id === 'squish';",
    replace: "      const card = g => { if (opts._yard && window.SquishApp && window.SquishApp.yardCard) return window.SquishApp.yardCard(g, { open: open(g), best: B[g.id], backyard: !!(st && st.open.backyard), lv: Sc.ROOMS.backyard.lv, ft, esc, shade });\n        const op = open(g), col = g.color || '#ff8a3d', feat = g.id === 'squish';",
  },
  {
    file: 'main-menu.js',
    why: 'tapping a yard game whose toy isn\'t bought says which one to buy',
    find: "if (!open(g)) { toast(g.name + ' opens with the Backyard at level ' + Sc.ROOMS.backyard.lv); return; }",
    replace: "if (!open(g)) { toast((st && st.open.backyard && window.SquishApp && window.SquishApp.yardNeed && window.SquishApp.yardNeed(g.id)) || g.name + ' opens with the Backyard at level ' + Sc.ROOMS.backyard.lv); return; }",
  },
  {
    file: 'main-menu.js',
    why: 'the Yard Games page: the posters drawn in, and the list dragged sideways with a finger or the mouse (app/games.js yardPage)',
    find: "const gr = p.querySelector('.mm-gr'); gr.addEventListener('wheel', e => { if (Math.abs(e.deltaY) > Math.abs(e.deltaX)) { gr.scrollLeft += e.deltaY; e.preventDefault(); } }, { passive: false });",
    replace: "const gr = p.querySelector('.mm-gr'); gr.addEventListener('wheel', e => { if (Math.abs(e.deltaY) > Math.abs(e.deltaX)) { gr.scrollLeft += e.deltaY; e.preventDefault(); } }, { passive: false });\n      if (opts._yard && window.SquishApp && window.SquishApp.yardPage) window.SquishApp.yardPage(p, gr, G, cre());",
  },
  {
    file: 'main-menu.js',
    why: 'no coins pill on the Yard Games page (the user, 2026-11-18)',
    find: "+ '<div class=\"mm-pill mm-p tip v-vel\"><img alt=\"\" src=\"' + ((window.SquishCoin && window.SquishCoin.coin(0)) || BL) + '\"><span class=\"tx-w\">' + fmt(Q ? Q.wallet().coins : 0) + '</span></div></div>'",
    replace: "+ (opts._yard ? '' : '<div class=\"mm-pill mm-p tip v-vel\"><img alt=\"\" src=\"' + ((window.SquishCoin && window.SquishCoin.coin(0)) || BL) + '\"><span class=\"tx-w\">' + fmt(Q ? Q.wallet().coins : 0) + '</span></div>') + '</div>'",
  },
  // ---- 2026-11-18, the user: "change the popups of the yard mini games to match the ones in the app" (the look is
  // app/games.js YPOP: the app's cream card, outlined titles, gold / cream buttons, numbered rules, orange back) ----
  {
    file: 'yard-games.js',
    why: 'the results card, once built, gets the app\'s title lettering and coin (app/games.js yardPopup)',
    find: 'if (inst && inst.celebrate) try { inst.celebrate(st); } catch (e) {} }',
    replace: 'if (window.SquishApp && window.SquishApp.yardPopup) try { window.SquishApp.yardPopup(card, \'result\', st); } catch (e) { console.warn(\'[yard popup]\', e); }\n      if (inst && inst.celebrate) try { inst.celebrate(st); } catch (e) {} }',
  },
  {
    file: 'yard-games.js',
    why: 'and so does the intro\'s how-to sheet',
    find: "D('div', { cls: 'yg-best' }, sh, SAVE.best[G.id] ? 'Best ' + SAVE.best[G.id] : 'No best yet'); return; }",
    replace: "D('div', { cls: 'yg-best' }, sh, SAVE.best[G.id] ? 'Best ' + SAVE.best[G.id] : 'No best yet'); if (window.SquishApp && window.SquishApp.yardPopup) try { window.SquishApp.yardPopup(sh, 'intro'); } catch (e) { console.warn('[yard popup]', e); } return; }",
  },
  {
    file: 'yard-games.js',
    why: 'the results card\'s buttons in the app\'s words: YARD GAMES (cream) and PLAY AGAIN (gold)',
    find: "D('button', { cls: 'yg-btn yg-ghost', onclick: () => app.hub() }, row, 'Yard'); D('button', { cls: 'yg-btn', onclick: () => app.play(G) }, row, 'Play again');",
    replace: "D('button', { cls: 'yg-btn yg-ghost', onclick: () => app.hub() }, row, window.SquishApp ? 'Yard games' : 'Yard'); D('button', { cls: 'yg-btn', onclick: () => app.play(G) }, row, 'Play again');",
  },
  // ---- 2026-11-18, the user on Hoop Shot: "the character is too far away from the widest white line and that makes
  // us holding the balls not long enough because i then exit the screen", "each time i score i want to add 10 seconds
  // to the timer", "the ball is too much in the air and not in the characters hands" ----
  {
    file: 'yard-games.js',
    why: 'a game can add time to its clock (Hoop Shot: +10 s a basket)',
    find: 'setScore(n) { score = n; scP.textContent = score; }, timer(txt) {',
    replace: "setScore(n) { score = n; scP.textContent = score; }, addTime(s) { if (G.dur && playing) { left += s; tmP.classList.remove('yg-hurry'); } }, timer(txt) {",
  },
  {
    file: 'yg-hoop.js',
    why: 'you stand at the three-point arc (FZ m forward: feet at z 1.2, the arc at ~1.4), so you\'re higher on the screen, and right in front of the hoop (PX 0, was 0.5 m to its left; the user: "mittens is not in the middle / front of the hoop"); a full pull is 95 (was 150), so the scoring pulls (about half) end well inside the screen',
    find: 'const PX = -0.5, TH = 62 * Math.PI / 180, HOLD = [PX, 1.0, 0.36], REL = [PX, 1.34, 0.44], THROW = 120, PULL = 150, DT = 1 / 240, LIFE = 3.2;',
    replace: 'const FZ = 0.95, PX = 0, TH = 62 * Math.PI / 180, HOLD = [PX, 1.0, 0.36 + FZ], REL = [PX, 1.34, 0.44 + FZ], THROW = 120, PULL = 95, DT = 1 / 240, LIFE = 3.2;',
  },
  {
    file: 'yg-hoop.js',
    why: '(the creature stands there, facing the hoop: its turn is the design\'s own, straight on now that PX is 0)',
    find: 'const me = ctx.creature(ctx.playerKey, { yaw: 180 }); const f0 = P(PX, 0, 0.25); me.set(f0[0], f0[1], 1.12 * f0[2]);',
    replace: 'const me = ctx.creature(ctx.playerKey, { yaw: 180 }); const f0 = P(PX, 0, 0.25 + FZ); me.set(f0[0], f0[1], 1.12 * f0[2]);',
  },
  {
    file: 'yg-hoop.js',
    why: '(its shadow, sized to it)',
    find: 'const meSh = YG.shadow(shG, f0[0] + 10, f0[1] - 2, 46, 0.2);',
    replace: 'const meSh = YG.shadow(shG, f0[0] + 10 * f0[2] / 101.4, f0[1] - 2, 46 * f0[2] / 101.4, 0.2);',
  },
  {
    file: 'yg-hoop.js',
    why: 'the held ball sits between its two paws, in the middle (it follows them through the wind-up); seen from behind the body hides most of it (the user: "its ok if we dont see it too much"), and the throw lifts it into view over the head',
    find: 'else { const kk = held.kk || 0; held.x = HOLD[0]; held.y = HOLD[1] - 0.22 * kk + Math.sin(t / 400) * 0.012 * (1 - kk); held.z = HOLD[2] - 0.05 * kk; held.fire = fire > 0 ? 1 : 0; } }',
    replace: 'else { const kk = held.kk || 0, pw = pawsAt(); if (pw) { held.x = pw[0]; held.y = pw[1]; held.z = pw[2]; } else { held.x = HOLD[0]; held.y = HOLD[1] - 0.22 * kk + Math.sin(t / 400) * 0.012 * (1 - kk); held.z = HOLD[2] - 0.05 * kk; } held.fire = fire > 0 ? 1 : 0; } }',
  },
  {
    file: 'yg-hoop.js',
    why: '(between the paws: rig units -> the stage -> the world at the ball\'s depth)',
    find: '      // ---------- input + loop ----------',
    replace: "      // (app, 2026-11-18) the held ball: half way between the creature's paws, a touch up (they hold it from below)\n      function pawsAt() { const I = me.I, L = I && I.W && I.W.pawL, R = I && I.W && I.W.pawR; if (!L || !R || !isFinite(L[0] + L[1] + R[0] + R[1])) return null; const sz = me.size, sx = me.x - sz / 2 + (L[0] + R[0]) / 200 * sz, sy = me.y - sz * 0.93 + (L[1] + R[1]) / 200 * sz, z = HOLD[2], p0 = P(0, 0, z);\n        return [(sx - p0[0]) / p0[2], (p0[1] - sy) / p0[2] + BR * 0.3, z]; }\n      // ---------- input + loop ----------",
  },
  {
    file: 'yg-hoop.js',
    why: 'every basket adds 10 seconds to the clock',
    find: "ctx.burst(c[0], c[1] + 8, 'confetti', 14, { v: 120, life: 0.9 }); netSw = 1; netAt = tNow; cheerT = tNow; SFX.swish(); setTimeout(SFX.score, 80); }",
    replace: "ctx.burst(c[0], c[1] + 8, 'confetti', 14, { v: 120, life: 0.9 }); netSw = 1; netAt = tNow; cheerT = tNow; SFX.swish(); setTimeout(SFX.score, 80);\n        if (ctx.addTime && ctx.playing) { ctx.addTime(10); ctx.toast('+10s', W - 64, 78, '#9dffb0'); } }",
  },
  {
    file: 'yg-hoop.js',
    why: '(and the how-to says so)',
    find: "'Swish 3, rim or bank 2. Three in a row = on fire! At 20 points the hoop starts to move.'",
    replace: "'Swish 3, rim or bank 2. Every basket adds 10 seconds! Three in a row = on fire. At 20 points the hoop starts to move.'",
  },
  // ---- REDESIGN_V8 (2026-11-14): the wood look; the lettering's purple outline and lavender white turn brown and cream ----
  {
    file: 'main-menu.js',
    why: 'the gold lettering\'s outline is the wood\'s brown, not purple (REDESIGN_V8)',
    find: "    gold: { f: ['#fffbe6', '#ffe57e', '#ffbe2e', '#f08c10'], o: '#3d1268', w: 1 },",
    replace: "    gold: { f: ['#fffbe6', '#ffe57e', '#ffbe2e', '#f08c10'], o: '#6b4426', w: 1 },",
  },
  {
    file: 'main-menu.js',
    why: 'the white lettering: cream to warm, with a brown outline (was lavender and purple)',
    find: "    white: { f: ['#ffffff', '#ffffff', '#f6eeff', '#e4d4ff'], o: '#3d1268' },",
    replace: "    white: { f: ['#ffffff', '#ffffff', '#fff6e2', '#ffe6b8'], o: '#6b4426' },",
  },
  {
    file: 'main-menu.js',
    why: 'the gold tab\'s icon in the wood\'s brown (was purple)',
    find: "ic(d, cls === 'v-gold' ? '#3d1268' : '#fff', 2.5)",
    replace: "ic(d, cls === 'v-gold' ? '#6b4426' : '#fff6e2', 2.5)",
  },
  {
    file: 'main-menu.js',
    why: 'no SQUAD logo in the top bar (REDESIGN_V8): MY HOUSE / CHARACTERS / SHOP move to the left',
    find: "hud.innerHTML = '<div class=\"mm-top\">' + ft('SQUAD', 'gold', 30, 'mm-logo') + '<div class=\"mm-tabs\">'",
    replace: "hud.innerHTML = '<div class=\"mm-top\"><div class=\"mm-tabs\">'",
  },
  {
    file: 'main-menu.js',
    why: 'a trophy button next to the gear opens Achievements (REDESIGN_V8: taken out of Settings)',
    find: "+ '<button class=\"mm-orb\" data-act=\"settings\" aria-label=\"Settings\">' + ic(GEAR, '#fff', 2.2) + '</button></div>'",
    replace: "+ (window.SquishMenu && window.SquishMenu.trophy ? '<button class=\"mm-orb sq-troph\" data-act=\"trophies\" aria-label=\"Achievements\">' + window.SquishMenu.trophy() + '</button>' : '') + '<button class=\"mm-orb\" data-act=\"settings\" aria-label=\"Settings\">' + ic(GEAR, '#fff', 2.2) + '</button></div>'",
  },
  {
    file: 'main-menu.js',
    why: 'the trophy button asks the app for its Achievements screen',
    find: "        else if (a === 'settings') { if (opts.onSettings) opts.onSettings(); else toast('Settings open in the full app'); }",
    replace: "        else if (a === 'settings') { if (opts.onSettings) opts.onSettings(); else toast('Settings open in the full app'); }\n        else if (a === 'trophies') { if (window.SquishApp && window.SquishApp.inApp) window.SquishApp.send('open', { what: 'achievements' }); else toast('Achievements open in the full app'); }",
  },
  {
    file: 'Main Menu.dc.html',
    why: 'the page under the menu while the house loads: warm dark brown, not dark purple (REDESIGN_V8)',
    find: 'body { margin:0; background:#24123f; }',
    replace: 'body { margin:0; background:#3a2210; }',
  },
  {
    file: 'Main Menu.dc.html',
    why: 'its glow: amber, not violet (REDESIGN_V8)',
    find: 'background:radial-gradient(ellipse at 50% 0%, #4a2a7a 0%, #24123f 70%);',
    replace: 'background:radial-gradient(ellipse at 50% 0%, #6b4426 0%, #3a2210 70%);',
  },
  {
    file: 'Main Menu.dc.html',
    why: '"Loading your house…" in cream, not lavender (REDESIGN_V8)',
    find: "font:700 22px/1 'Fredoka',sans-serif; color:#e8dcff; }",
    replace: "font:700 22px/1 'Fredoka',sans-serif; color:#ffe9c8; }",
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
    why: 'MY HOUSE is CRIBFLUF (the user, 2026-11-16)',
    find: "tab('house', 'MY HOUSE', ",
    replace: "tab('house', 'CRIBFLUF', ",
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
  // ---- performance (2026-11-14): the scene is drawn still; the weather moves on its own layer (app/menu.js) ----
  {
    file: 'rive-rig.js',
    why: 'every creature on its own layer: its frame-by-frame redraw no longer repaints the room / scene under it (opacity, not transform: a transform hint would freeze its sharpness when the house zooms)',
    find: "const wrap = document.createElement('div'); wrap.style.cssText = 'position:relative;width:100%;height:100%;touch-action:none;cursor:pointer;user-select:none;-webkit-user-select:none;';",
    replace: "const wrap = document.createElement('div'); wrap.style.cssText = 'position:relative;width:100%;height:100%;touch-action:none;cursor:pointer;user-select:none;-webkit-user-select:none;will-change:opacity;';",
  },
  {
    file: 'rive-rig.js',
    why: 'creatures: every other frame when the phone cannot keep 60 (app/perf.js), none at all while hidden (sqHold); a creature\'s house moves are built before its first frame (SquishLazy, app/perf.js)',
    find: "function loop(now) { raf = ALL.size ? requestAnimationFrame(loop) : 0; const dt = clamp((now - (last || now - 16)) / 1000, 0.001, 1 / 20); last = now; ALL.forEach(I => { try { tick(I, now, dt); }",
    replace: "function loop(now) { raf = ALL.size ? requestAnimationFrame(loop) : 0; if (window.SquishPerf && window.SquishPerf.skipRig()) return; const dt = clamp((now - (last || now - 16)) / 1000, 0.001, 1 / 20); last = now; ALL.forEach(I => { if (I.sqHold) return; if (I.key && window.SquishLazy) window.SquishLazy.need(I.key); try { tick(I, now, dt); }",
  },
  // ---- memory (2026-11-14): every creature's house moves were built at page load (~230 MB); now on demand ----
  {
    file: 'house-moves.js',
    why: 'the house builders keep each creature\'s calls until it is drawn (SquishLazy in app/perf.js): only the creatures on screen get their moves built',
    find: "  if (window.HouseCreatures) window.HouseCreatures.forEach(f => f(R.HOUSE));\n  window.HouseCreatures = { push: f => f(R.HOUSE) };",
    replace: "  if (window.SquishLazy) window.SquishLazy.house(R.HOUSE);\n  if (window.HouseCreatures) window.HouseCreatures.forEach(f => f(R.HOUSE));\n  window.HouseCreatures = { push: f => f(R.HOUSE) };",
  },
  {
    file: 'rive-rig.js',
    why: 'a pose asked for by name and creature (thumbnails, _pose) builds that creature\'s house moves first',
    find: "  function poseAt(name, t, key) {\n",
    replace: "  function poseAt(name, t, key) {\n    if (key && window.SquishLazy) window.SquishLazy.need(key);\n",
  },
  {
    file: 'main-menu.js',
    why: 'the menu\'s picture without its animations (they repainted the whole full-screen SVG, blur filters included, every frame: ~30 fps on a weak phone)',
    find: 'function draw() { st = Sc.state({ level: level(), season: season === \'auto\' ? null : season }); scene.innerHTML = Sc.svg(st);',
    replace: 'function draw() { st = Sc.state({ level: level(), season: season === \'auto\' ? null : season }); scene.innerHTML = window.SquishMenu && window.SquishMenu.still ? window.SquishMenu.still(Sc.svg(st), st, scene) : Sc.svg(st);',
  },
  {
    file: 'main-menu-scene.js',
    why: 'rain, snow and birds: app/menu.js draws them on a layer the GPU slides (no repaint), so the scene leaves them out',
    find: '  function weather(st) {\n',
    replace: '  function weather(st) {\n    if (window.SquishMenu && window.SquishMenu.still) return \'\';\n',
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
  // REDESIGN_V9 (2026-11-16, the user's zip): the trip home gets its loading screen, "Heading home" (app/menu.js
  // headingHome): after the walk to the door, night falls on the house while the house loads underneath, then the
  // camera flies in through the door as before. These three let the trip, the house and the loader meet.
  {
    file: 'main-menu.js',
    why: 'the trip home: the house starts loading as the creature reaches the door, under the Heading home screen; the fly-in waits for it',
    find: "      await tween(900, (e, t) => { world.style.transform = 'scale(' + (1 + 5.2 * e).toFixed(3) + ')'; veil.style.opacity = t > 0.45 ? Math.min(1, (t - 0.45) / 0.5).toFixed(3) : 0; }, easeIn);\n      veil.style.pointerEvents = 'auto'; inside = true; busy = false;\n      const r = frame.getBoundingClientRect();\n      if (opts.onHome) opts.onHome(room || 'living', r);",
    replace: "      const HH = window.SquishMenu && window.SquishMenu.headingHome && opts.onHome ? window.SquishMenu.headingHome(frame, world, st) : null;\n      if (HH) { opts.onHome(room || 'living', frame.getBoundingClientRect()); if ((await HH.ready()) === 'cancel') { inside = true; busy = false; back(); return; } HH.zoom(); }\n      await tween(900, (e, t) => { world.style.transform = 'scale(' + (1 + 5.2 * e).toFixed(3) + ')'; veil.style.opacity = t > 0.45 ? Math.min(1, (t - 0.45) / 0.5).toFixed(3) : 0; }, easeIn);\n      veil.style.pointerEvents = 'auto'; inside = true; busy = false;\n      const r = frame.getBoundingClientRect();\n      if (HH) HH.end(); else if (opts.onHome) opts.onHome(room || 'living', r);",
  },
  {
    file: 'Main Menu.dc.html',
    why: 'the house is ready when it says so (its state.ready, after its first room is set up), not only when it has more than 12 pictures: a house with few things in it (11) always waited out the 12 s limit',
    find: "const el = this._hs, ok = el && el.querySelectorAll('svg').length > 12;",
    replace: "const el = this._hs, sh = window.__sh, ok = el && (el.querySelectorAll('svg').length > 12 || !!(sh && !sh.dead && sh.state && sh.state.ready));",
  },
  {
    file: 'Main Menu.dc.html',
    why: 'the house has drawn: with the Heading home screen up, it fades in once the camera has flown in through the door',
    find: 'if (ok || Date.now() - t0 > 12000) { clearInterval(this._poll); setTimeout(() => this.setState({ hop: 1 }), ok ? 250 : 0); }',
    replace: 'if (ok || Date.now() - t0 > 12000) { clearInterval(this._poll); const show = () => { if (this.state.house) this.setState({ hop: 1 }); }; if (window.SquishMenu && window.SquishMenu.houseDrawn) window.SquishMenu.houseDrawn(show); else setTimeout(show, ok ? 250 : 0); }',
  },
  {
    file: 'Main Menu.dc.html',
    why: 'back while the house is still loading: the Heading home screen gives up and the creature walks back out',
    find: '  leaveHouse = () => { clearInterval(this._poll); this.setState({ hop: 0 });',
    replace: '  leaveHouse = () => { clearInterval(this._poll); if (window.SquishMenu && window.SquishMenu.houseLeft) window.SquishMenu.houseLeft(); this.setState({ hop: 0 });',
  },
  // 2026-11-17 (the user: "when exiting crib back to main home page, the character exits with his back and not his
  // front"): the design walks the creature out of the door still turned round from the walk in (spinOff 180) and only
  // turns it to face us once it is back on its spot. It now comes out facing us: turned while it is still unseen
  // (the walk in ends at opacity 0 and the walk out starts there), and no turn at the end.
  {
    file: 'main-menu.js',
    why: 'back from the house: the creature walks out of the door facing us, not with its back to us',
    find: "if (!inside || busy) return; busy = true; inside = false; veil.style.pointerEvents = 'none'; const I = ctrl && ctrl._I;",
    replace: "if (!inside || busy) return; busy = true; inside = false; veil.style.pointerEvents = 'none'; const I = ctrl && ctrl._I; if (I) { I.spinTw = null; I.spinOff = 0; }",
  },
  {
    file: 'main-menu.js',
    why: 'back from the house: no turn round on the spot (it already faces us)',
    find: '      const s0 = I ? I.spinOff || 0 : 0; await tween(430, e => { if (I) I.spinOff = s0 + 180 * e; }); if (I) I.spinOff = 0;\n',
    replace: '',
  },
  {
    file: 'Squad House v6.dc.html',
    why: "the Dance Room's speakers play the room's own built-in beat, not the design's song (music/rubber-band-hustle.mp3, which the build no longer ships: the user kept only Velvet Map Study, 2026-11-16)",
    find: "const want = !this.dead && this.state.cur === 'dance' && !!(this.jams && this.jams.size), MN = window.MovieNight;",
    replace: 'const want = false, MN = window.MovieNight;',
  },
  // ---- 2026-11-18, the user: "in the yard, the games, they will not be placable, they will be purchasable and each game
  // that is purchased will unlock in the yard games icon on the top right corner". A yard game's toy (the hoop, the
  // goal, the hopscotch court, the trampolines, the slides, the cornhole boards, the ping-pong table:
  // SquishApp.yardGameOf, app/games.js) is bought once, never placed, and buying it unlocks its game behind the
  // backyard's YARD GAMES button (which shows a NEW badge). ----
  {
    file: 'Squad House v6.dc.html',
    why: 'a yard game\'s toy is bought once, and any one of its toys (three slides, two trampolines) unlocks the game, so the others are not for sale after it (the catalog, buyR)',
    find: 'const D = this.rnDraw(r, 0) || {}, surf = !!D.surf; if (surf && this.H.rOwn[id]) return true;',
    replace: "const D = this.rnDraw(r, 0) || {}, surf = !!D.surf; if (surf && this.H.rOwn[id]) return true;\n    const YGb = window.SquishApp && window.SquishApp.yardGameOf && window.SquishApp.yardGameOf('R:' + id, r); if (YGb && YGb.open) { this.toast(YGb.name + ' is already unlocked: play it in YARD GAMES'); return false; }",
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'buying it unlocks its game: says so, and the YARD GAMES button gets its NEW badge',
    find: "this.save(); this.setState({ coins: this.coinCount() }); this.toast(SN(r) + ' is yours!'); return true;",
    replace: "this.save(); this.setState({ coins: this.coinCount() }); if (YGb) { this.toast(YGb.name + ' unlocked! Play it in YARD GAMES'); if (window.SquishApp.yardNew) window.SquishApp.yardNew(YGb.id); } else this.toast(SN(r) + ' is yours!'); return true;",
  },
  {
    file: 'Squad House v6.dc.html',
    why: '(the catalog\'s item card: is it a yard game\'s toy)',
    find: 'const can = free || coinsN >= r.cost, on = surf && own && this.rnIsOn(r);\n    const tg = this.rnTag(r, D);',
    replace: "const can = free || coinsN >= r.cost, on = surf && own && this.rnIsOn(r), YGd = window.SquishApp && window.SquishApp.yardGameOf && window.SquishApp.yardGameOf('R:' + r.id, r);\n    const tg = YGd ? {} : this.rnTag(r, D);   // (no \"Tap to start the game, needs 3 friends in the room\": it never stands in the room)",
  },
  {
    file: 'Squad House v6.dc.html',
    why: '(no "x1" on it, and no price once its game is unlocked: UNLOCKED needs the room)',
    find: "hasCnt: !surf && own > 0, cnt: 'x' + own, op: lk ? 0.55 : 1,",
    replace: "hasCnt: !surf && own > 0 && !YGo, pD: YGo && YGo.open ? 'none' : 'flex', cnt: 'x' + own, op: lk ? 0.55 : 1,",
  },
  {
    file: 'Squad House v6.dc.html',
    why: '(the price on the tile can be hidden: both pages of the catalog book)',
    count: 2,
    find: `<div style="display:flex; align-items:center; gap:3px; font-family:'Fredoka',sans-serif; font-weight:700; font-size:11.5px; color:#5b3418;">{{ c.price }}`,
    replace: `<div style="display:{{ c.pD }}; align-items:center; gap:3px; font-family:'Fredoka',sans-serif; font-weight:700; font-size:11.5px; color:#5b3418;">{{ c.price }}`,
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'a bought one says UNLOCKED',
    find: "bTxt: lk ? 'LV ' + r.lv : st === 'own' ? (on ? 'IN USE' : 'OWNED') : 'BUY',",
    replace: "bTxt: lk ? 'LV ' + r.lv : st === 'own' ? (YGo ? 'UNLOCKED' : on ? 'IN USE' : 'OWNED') : 'BUY',",
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'its card says it is unlocked, not how many you own',
    find: "own: surf ? (own ? (on ? 'Up in the ' + RLAB[hr] : 'You own this') : '') : (own ? 'You own ' + own : ''),",
    replace: "own: YGd ? (YGd.open ? 'Unlocked! Play ' + YGd.name + ' in YARD GAMES' : '') : surf ? (own ? (on ? 'Up in the ' + RLAB[hr] : 'You own this') : '') : (own ? 'You own ' + own : ''),",
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'its card says what buying it does',
    find: 'desc: NT(r), hasDesc: !!NT(r),',
    replace: "desc: YGd ? 'A yard game: buying it unlocks ' + YGd.name + ' in YARD GAMES (the button top right in the Backyard). It isn\\'t placed in the yard.' : NT(r), hasDesc: !!(YGd || NT(r)),",
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'no second copy',
    find: 'canBuy: !lk && !(surf && own), price: String(r.cost),',
    replace: 'canBuy: !lk && !(surf && own) && !(YGd && YGd.open), price: String(r.cost),',
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'and no Place it',
    find: "hasPlace: !lk && own > 0 && !on, placeTxt: surf ? 'Put it up' : 'Place it',",
    replace: "hasPlace: !lk && own > 0 && !on && !YGd, placeTxt: surf ? 'Put it up' : 'Place it',",
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'the catalog\'s tile shows a bought one as owned (one copy), not for sale again',
    find: "st = lk ? 'lock' : surf && own ? 'own' : 'buy';",
    replace: "YGo = window.SquishApp && window.SquishApp.yardGameOf && window.SquishApp.yardGameOf('R:' + r.id, r), st = lk ? 'lock' : (surf && own) || (YGo && YGo.open) ? 'own' : 'buy';",
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'Renovate\'s tray of things to place leaves the yard games\' toys out',
    find: "if (tab === 'furn') return Object.keys(H.owned).filter(t => X.CAT[t] && X.CAT[t].reno && X.CAT[t].room === rk && this.freeCount(t) > 0)",
    replace: "if (tab === 'furn') return Object.keys(H.owned).filter(t => X.CAT[t] && X.CAT[t].reno && X.CAT[t].room === rk && this.freeCount(t) > 0 && !(window.SquishApp && window.SquishApp.yardGameOf && window.SquishApp.yardGameOf(t)))",
  },
  {
    file: 'Squad House v6.dc.html',
    why: 'one already standing in the yard (placed before) is taken out; it stays bought, so its game stays unlocked',
    find: 'this.H = { owned: s.owned || {}, rooms: s.rooms || {},',
    replace: 'this.H = { owned: s.owned || {}, rooms: window.SquishApp && window.SquishApp.yardStrip ? window.SquishApp.yardStrip(s.rooms || {}) : (s.rooms || {}),',
  },
  {
    file: 'Squad House v6.dc.html',
    why: '(the house\'s other catalog, the same rules: bought once, never placed)',
    find: 'if (M.surf && this.H.sOwn[id]) return;',
    replace: "if (M.surf && this.H.sOwn[id]) return; const YGa = window.SquishApp && window.SquishApp.yardGameOf && window.SquishApp.yardGameOf(id, M); if (YGa && YGa.open) { this.toast(YGa.name + ' is already unlocked: play it in YARD GAMES'); return; }",
  },
  {
    file: 'Squad House v6.dc.html',
    why: '(its buying says so too)',
    find: "this.save(); this.setState({ coins: this.coinCount() }); this.toast(M.n + ' is yours!');",
    replace: "this.save(); this.setState({ coins: this.coinCount() }); if (YGa) { this.toast(YGa.name + ' unlocked! Play it in YARD GAMES'); if (window.SquishApp.yardNew) window.SquishApp.yardNew(YGa.id); } else this.toast(M.n + ' is yours!');",
  },
  {
    file: 'Squad House v6.dc.html',
    why: '(and its card has no Place it for them)',
    find: 'hasPlace: !locked && (M.surf ? own && !useHere : this.freeCount(M.id) > 0),',
    replace: 'hasPlace: !locked && !(window.SquishApp && window.SquishApp.yardGameOf && window.SquishApp.yardGameOf(M.id, M)) && (M.surf ? own && !useHere : this.freeCount(M.id) > 0),',
  },
];

export function applyPatches(rel, text) {
  for (const p of PATCHES) {
    if (p.file !== rel) continue;
    const n = text.split(p.find).length - 1;
    const want = p.count || 1;
    if (n !== want) throw new Error(`patch for ${p.file} (${p.why}): expected the code ${want} time(s), found it ${n} times. The prototype changed here; update webgame/patches.mjs.`);
    text = text.split(p.find).join(p.replace);
  }
  return text;
}
