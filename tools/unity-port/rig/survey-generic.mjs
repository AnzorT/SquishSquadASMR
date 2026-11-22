// Surveys the generic rig (webgame rive-generic.js) over every creature that uses it, for planning its Unity port:
// which code of render() (and setup()) each creature runs (V8 block coverage, one creature at a time), what setup()
// leaves on I for render() to read (its keys, the roles of the art's shapes, the ROUND flags), and what the art files use
// (tags, attributes, filters, gradients...).
//
//   node tools/unity-port/rig/survey-generic.mjs [--keys bao,nimbo] [--seconds 24]
// Outputs (tools/unity-port/rig/out, not in git):
//   generic-survey.json: per creature: I's keys, item roles, RC, the render() / setup() character ranges it ran
//   generic-cov/render-<key>.txt: render() with the code that creature never ran elided (what to port for it)
//   generic-survey.txt: the summary (shared code, per-creature code, blocks by how many creatures run them, art features)
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { REPO, BUILT, needBuild, launch } from '../lib/menu-page.mjs';
import { makeTestCopy, rgLib, RR, SEED } from './generic-page.mjs';

const args = process.argv.slice(2);
const opt = (k, d) => args.includes(k) ? args[args.indexOf(k) + 1] : d;
const SECONDS = +opt('--seconds', 24);
const OUT = path.join(REPO, 'tools/unity-port/rig/out');
const TEST = path.join(REPO, 'webgame/test/out/gensurvey');
needBuild();
const T = makeTestCopy(TEST);
const SRC = fs.readFileSync(T.file, 'utf8');
const lineStart = []; { let o = 0; for (const l of SRC.split('\n')) { lineStart.push(o); o += l.length + 1; } }
const RENDER = [lineStart[T.renderFrom - 1], lineStart[T.renderTo]];                     // render()'s characters
const SETUP = [SRC.indexOf('  function setup(I, root, art, key, H, n) {'), SRC.indexOf('  const render = H => (I, P, now) => {')];
const HEROFX = [SRC.indexOf('  function heroFX(I, P, now, C) {'), SRC.indexOf('  const HDANCE = {')];   // the heroes' per-frame effects (render() calls it)
// other files render() calls into: the snack / fruit 3D models (SN3D.draw; their setup runs from setup())
const EXT = ['snack-3d.js', 'fruit-3d.js'].map(f => ({ f, re: new RegExp('/' + f.replace('.', '\\.') + '(\\?|$)'), n: fs.readFileSync(path.join(BUILT, f), 'utf8').length }));

const browser = await launch();
const ctx = await browser.newContext({ viewport: { width: 832, height: 384 } });
const page = await ctx.newPage();
await page.route(/^(https?|wss?):/, r => r.abort());
await page.addInitScript(SEED);
await page.addInitScript(RR);
await page.addInitScript(rgLib);
await page.addInitScript('window.__noLoop = true;');
await page.clock.install({ time: new Date('2026-11-13T12:00:00Z') });
await page.clock.pauseAt(new Date('2026-11-13T12:00:00Z'));
const cdp = await ctx.newCDPSession(page);
await cdp.send('Profiler.enable');
await page.goto(pathToFileURL(path.join(TEST, 'App.dc.html')).href);
await page.waitForTimeout(8000);
for (let t = 0; t < 3000; t += 500) { await page.clock.runFor(500); await page.waitForTimeout(80); }
const all = await page.evaluate(() => (window.CREATURES_V2 || []).map(c => c.name.toLowerCase()).filter(k => !window.RiveRig.DEFS[k]));
const KEYS = opt('--keys') ? opt('--keys').split(',') : all;
console.log(`${KEYS.length} generic creatures`);
await cdp.send('Profiler.startPreciseCoverage', { callCount: true, detailed: true });

// the moves each creature is driven through (frame: what), 60 frames a second
const FPS = 60, N = SECONDS * FPS;
const SCRIPT = [[120, 'play:hop'], [240, 'play:look'], [360, 'press'], [460, 'release'], [520, 'play:happy'], [700, 'drag:40'], [715, 'drag:140'], [730, 'dragEnd'],
  [800, 'play:dance'], [1160, 'play:eat_apple'], [1280, 'play:poke_head'], [1340, 'play:sleep'], [1400, 'play:cry']].filter(([f]) => f < N);
await page.evaluate(({ FPS, SCRIPT }) => {
  let NOW = performance.now(); performance.now = () => NOW;
  window.__S = {};
  const ENGINE = new Set(['D', 'lookM', 'onState', 'cur', 'prev', 'shots', 'press', 'spinOff', 'ptr', 'gl', 'glAt', 'blinkT', 'blinkAt', 'dbl', 'hs', 'lastSpin', 'queue', 'ctr', 'ctT', 'emL', 'parts',
    'fidgets', 'ak', 'flw', 'fex', 'fey', 'fhx', 'fhy', 'fp', 'fs', 'fx', 'fl', 'fsp', 'spb', 'jel', 'mot', 'lastP', 'bt0', 'bd', 'evC', 'evF', 'fidAt', 'lastFid', 'dent', 'ms', 'ed', 'ld', 'spTw', 'spF',
    'propZ', 'ext', 'down', 'drag', 'pressT', 'spin0', 'spinTw', 'ht', 'curL', 'sceneL', 'olDirty', 'err', 'generic', 'key', 'u', 'art', 'fxG', 'pg', 'E', 'items', 'shadow', 'geo', 'faceG', 'tubes', 'headClip', 'mapHand', 'toM', 'fromM']);
  window.__rgSetup = (I, extra) => {
    const roles = {}; (I.items || []).forEach(q => { roles[q.role] = (roles[q.role] || 0) + 1; });
    const keys = Object.keys(I).filter(k => !ENGINE.has(k) && I[k] != null && I[k] !== false && I[k] !== 0);
    const vals = {}; keys.forEach(k => { const v = I[k]; vals[k] = typeof v === 'object' ? (v instanceof Element ? 'element' : Array.isArray(v) ? 'array' : 'object') : typeof v === 'function' ? 'function' : v; });
    // what record-generic.mjs would export: the functions setup() leaves on I (to port as code), the data's size
    let fns = [], kb = 0; try { const L = window.__rgLib(host); L.nameAll(I); const r = L.ser(I, (o, k) => o === I && L.ENGINE.has(k)); fns = r.fns; kb = JSON.stringify(r.data).length / 1024; } catch (e) { fns = ['(export failed: ' + e.message + ')']; }
    // what the drawing has to handle: the element tree as setup() left it (the art + what setup() added)
    const feat = new Set(), SKIP = new Set(['d', 'cx', 'cy', 'r', 'rx', 'ry', 'x', 'y', 'width', 'height', 'id', 'offset', 'stop-color', 'fill', 'stroke', 'stroke-width', 'opacity', 'transform', 'points', 'x1', 'x2', 'y1', 'y2', 'fx', 'fy',
      'data-art', 'data-k', 'data-lt', 'style', 'class', 'viewBox', 'xmlns', 'pointer-events', 'values', 'baseFrequency', 'numOctaves', 'seed', 'stdDeviation', 'result', 'href', 'gradientTransform']);
    const VAL = new Set(['stroke-linecap', 'stroke-linejoin', 'gradientUnits', 'clipPathUnits', 'fill-rule', 'clip-rule', 'mix-blend-mode', 'maskUnits', 'maskContentUnits', 'spreadMethod', 'operator', 'mode', 'type', 'in', 'in2', 'paint-order', 'vector-effect', 'dominant-baseline', 'text-anchor']);
    host.querySelectorAll('svg *').forEach(e => { const t = e.tagName; if (e.closest('#' + CSS.escape(I.fxG && I.fxG.id || '__none'))) return; feat.add('<' + t + '>');
      for (const a of e.attributes) { if (SKIP.has(a.name)) continue; feat.add(t + '@' + a.name + (VAL.has(a.name) ? '=' + a.value : '')); }
      const tf = e.getAttribute('transform') || ''; if (/skew|matrix/.test(tf)) feat.add('transform: skew/matrix'); if (e.getAttribute('gradientTransform')) feat.add(t + '@gradientTransform');
      for (const k of ['fill', 'stroke']) { const v = e.getAttribute(k) || ''; if (/^url\(/.test(v)) { const r = host.querySelector(v.slice(4, -1).replace(/"/g, '')); feat.add(k + '=url(' + (r ? r.tagName : 'nothing') + ')'); } }
      const fl = e.getAttribute('filter'); if (fl && fl !== 'none') { const f = host.querySelector(fl.slice(4, -1)); feat.add('filter used: ' + (f ? [...f.children].map(c => c.tagName).join('+') : 'nothing')); }
      if (e.getAttribute('mask')) feat.add('mask used'); if (e.getAttribute('clip-path')) { const c = host.querySelector(e.getAttribute('clip-path').slice(4, -1)); feat.add('clip-path: ' + (c ? [...c.children].map(x => x.tagName).join('+') : 'nothing')); }
      if (e.style && e.style.display === 'none') feat.add('style display none'); });
    window.__S[I.key] = { keys: vals, roles, RC: extra.RC, pet: extra.pet, hero: !!extra.hero, items: (I.items || []).length, els: I.art ? I.art.querySelectorAll('*').length : 0, fns, kb, feat: [...feat] };
  };
  let host, c, I, t0;
  window.__mount = key => { host = document.createElement('div'); host.style.cssText = 'position:fixed;left:0;top:0;width:169px;height:169px;'; document.body.appendChild(host);
    c = window.RiveRig.mount(host, key, {}); I = c._I; t0 = NOW; };
  window.__ready = () => !!I.ready;
  window.__run = () => {
    if (!I.ready) { c.destroy(); host.remove(); return 'not ready'; }
    const sc = new Map(SCRIPT.map(([f, w]) => [f, w]));
    for (let f = 1; f <= window.__N; f++) {
      const now = t0 + f * 1000 / FPS; NOW = now;
      const w = sc.get(f);
      if (w) {
        if (w.startsWith('play:')) c.play(w.slice(5));
        else if (w === 'press') { if (I.cur.name !== 'idle') c.play('idle'); I.down = { cx: 100, x: 50, y: 60 }; I.drag = false; I.press = true; I.pressT = now; }
        else if (w === 'release') { I.press = false; const RS = (I.key && window.RiveRig.REL[I.key]) || window.RiveRig.__RELEASE; I.shots.push({ S: RS, t0: now }); I.down = null; }
        else if (w.startsWith('drag:')) { if (!I.drag) { I.down = { cx: 100, x: 50, y: 60 }; I.drag = true; I.spinTw = null; I.spin0 = I.spinOff; } I.spinOff = I.spin0 + (+w.slice(5)) / 169 * 360; }
        else if (w === 'dragEnd') { I.spinTw = { from: I.spinOff, to: Math.round(I.spinOff / 360) * 360, t0: now, d: 650 }; I.down = null; I.drag = false; }
      }
      try { window.RiveRig.__tick(I, now, 1 / FPS); } catch (e) { c.destroy(); host.remove(); return 'error: ' + e.message; }
    }
    c.destroy(); host.remove(); return 'ok';
  };
}, { FPS, SCRIPT });
await page.evaluate(N => { window.__N = N; }, N);

const ranges = cov => {   // the characters of [a, b) that ran, as [from, to) runs relative to a
  const out = { render: [], setup: [], heroFX: [] };
  const run = (sc, k, a, b) => {
    const cnt = new Int32Array(b - a).fill(-1), R = [];
    for (const f of sc.functions) for (const r of f.ranges) if (r.endOffset > a && r.startOffset < b) R.push(r);
    R.sort((x, y) => x.startOffset - y.startOffset || y.endOffset - x.endOffset);
    for (const r of R) for (let i = Math.max(a, r.startOffset); i < Math.min(b, r.endOffset); i++) cnt[i - a] = r.count;
    out[k] = []; let s = -1; for (let i = 0; i <= cnt.length; i++) { const on = i < cnt.length && cnt[i] > 0; if (on && s < 0) s = i; if (!on && s >= 0) { out[k].push([s, i]); s = -1; } } };
  for (const E of EXT) { const sc = cov.find(s => E.re.test(s.url)); out[E.f] = []; if (sc) run(sc, E.f, 0, E.n); }
  const sc = cov.find(s => /\/rive-generic\.js(\?|$)/.test(s.url)); if (!sc) return out;
  run(sc, 'heroFX', HEROFX[0], HEROFX[1]);
  for (const [k, [a, b]] of [['render', RENDER], ['setup', SETUP]]) {
    const cnt = new Int32Array(b - a).fill(-1), R = [];
    for (const f of sc.functions) for (const r of f.ranges) if (r.endOffset > a && r.startOffset < b) R.push(r);
    R.sort((x, y) => x.startOffset - y.startOffset || y.endOffset - x.endOffset);
    for (const r of R) for (let i = Math.max(a, r.startOffset); i < Math.min(b, r.endOffset); i++) cnt[i - a] = r.count;
    let s = -1; for (let i = 0; i <= cnt.length; i++) { const on = i < cnt.length && cnt[i] > 0; if (on && s < 0) s = i; if (!on && s >= 0) { out[k].push([s, i]); s = -1; } }
  }
  return out;
};
const res = {};
await cdp.send('Profiler.takePreciseCoverage');   // (the page's own start-up)
for (const key of KEYS) {
  const t = Date.now();
  await page.evaluate(k => window.__mount(k), key);
  for (let i = 0; i < 200 && !(await page.evaluate(() => window.__ready())); i++) { await page.waitForTimeout(25); if (i % 10 === 9) await page.clock.runFor(100); }
  const st = await page.evaluate(() => window.__run());
  const { result } = await cdp.send('Profiler.takePreciseCoverage');
  const info = await page.evaluate(k => window.__S[k] || null, key);
  res[key] = { status: st, ...(info || {}), ...ranges(result) };
  const kb = k => (res[key][k] || []).reduce((s, [a, b]) => s + b - a, 0) / 1024;
  console.log(`${key}: ${st}, render ${kb('render').toFixed(1)} KB ran, heroFX ${kb('heroFX').toFixed(1)}, SN3D ${(kb('snack-3d.js') + kb('fruit-3d.js')).toFixed(1)}; ${((Date.now() - t) / 1000).toFixed(1)} s`);
}
await browser.close();
fs.mkdirSync(path.join(OUT, 'generic-cov'), { recursive: true });
fs.writeFileSync(path.join(OUT, 'generic-survey.json'), JSON.stringify({ render: RENDER, setup: SETUP, heroFX: HEROFX, ext: EXT.map(E => ({ f: E.f, n: E.n })), renderLines: [T.renderFrom, T.renderTo], script: SCRIPT, seconds: SECONDS, res }));

// ---- per creature: render() with what it never ran elided ----
const RS = SRC.slice(RENDER[0], RENDER[1]), RL = RS.split('\n'), RLs = []; { let o = 0; for (const l of RL) { RLs.push(o); o += l.length + 1; } }
const elided = rr => { const on = new Uint8Array(RS.length); rr.forEach(([a, b]) => on.fill(1, a, b)); const out = [];
  RL.forEach((l, i) => { let o = '', skip = false; for (let j = 0; j < l.length; j++) { if (on[RLs[i] + j]) { o += l[j]; skip = false; } else if (!skip) { o += '⟨…⟩'; skip = true; } }
    if (/[^⟨…⟩\s]/.test(o)) out.push((T.renderFrom + i) + ': ' + o); }); return out.join('\n'); };
for (const [k, r] of Object.entries(res)) if (r.render) fs.writeFileSync(path.join(OUT, 'generic-cov', `render-${k}.txt`), elided(r.render));

// ---- the summary ----
const keys = Object.keys(res).filter(k => res[k].status === 'ok');
const cnt = new Uint16Array(RS.length); keys.forEach(k => res[k].render.forEach(([a, b]) => { for (let i = a; i < b; i++) cnt[i]++; }));
const nonWs = i => !/\s/.test(RS[i]);
let tot = 0, shared = 0, some = 0, never = 0, one = 0; const hist = new Map();
for (let i = 0; i < RS.length; i++) { if (!nonWs(i)) continue; tot++; const c = cnt[i]; if (c >= keys.length * 0.9) shared++; else if (c > 1) some++; else if (c === 1) one++; else never++; }
const L = [];
L.push(`render(): ${(RS.length / 1024).toFixed(0)} KB, lines ${T.renderFrom}-${T.renderTo}; ${keys.length} creatures surveyed (${SECONDS} s each: ${SCRIPT.map(s => s[1]).join(', ')})`);
L.push(`characters (non-blank): ${tot}; run by >=90% of creatures: ${(100 * shared / tot).toFixed(1)}%, by 2..90%: ${(100 * some / tot).toFixed(1)}%, by exactly one: ${(100 * one / tot).toFixed(1)}%, by none: ${(100 * never / tot).toFixed(1)}%`);
L.push('');
L.push('per creature: KB of render() run, KB of it run by no other creature, KB of heroFX / snack-3d.js + fruit-3d.js (SN3D) run; I keys (setup outputs render reads); item roles; RC flags');
const only = k => { let n = 0; res[k].render.forEach(([a, b]) => { for (let i = a; i < b; i++) if (cnt[i] === 1 && nonWs(i)) n++; }); return n; };
const kbOf = (r, k) => (r[k] || []).reduce((s, [a, b]) => s + b - a, 0) / 1024;
keys.map(k => [k, res[k].render.reduce((s, [a, b]) => s + b - a, 0), only(k)]).sort((a, b) => b[1] - a[1]).forEach(([k, n, o]) => {
  const r = res[k], hf = kbOf(r, 'heroFX'), sn = kbOf(r, 'snack-3d.js') + kbOf(r, 'fruit-3d.js');
  L.push(`  ${k.padEnd(15)} ${(n / 1024).toFixed(1).padStart(5)} KB  own ${(o / 1024).toFixed(1).padStart(5)} KB${hf ? `  heroFX ${hf.toFixed(1)} KB` : ''}${sn ? `  SN3D ${sn.toFixed(1)} KB` : ''}  data ${(r.kb || 0).toFixed(0)} KB  fns: ${(r.fns || []).filter(f => !/^(mapHand|toM|fromM)$/.test(f)).join(' ') || '-'}  keys: ${Object.keys(r.keys || {}).join(' ')}  roles: ${Object.entries(r.roles || {}).map(([a, b]) => a + ':' + b).join(' ')}  RC: ${Object.keys(r.RC || {}).join(' ')}`); });
Object.entries(res).filter(([, r]) => r.status !== 'ok').forEach(([k, r]) => L.push(`  ${k}: ${r.status}`));
// the functions setup() leaves on I (render() calls them: ported as code, their data exported)
{ const F = new Map(); keys.forEach(k => (res[k].fns || []).forEach(f => { const g = f.replace(/\.\d+(\.|$)/g, '.#$1'); if (!F.has(g)) F.set(g, new Set()); F.get(g).add(k); }));
  L.push(''); L.push('functions setup() leaves on I (path: creatures)');
  [...F.entries()].sort((a, b) => b[1].size - a[1].size).forEach(([f, s]) => L.push(`  ${f}: ${s.size <= 8 ? [...s].join(', ') : s.size}`)); }
// top-level statements of render(): the lines, how many creatures run them, and which (when few)
L.push(''); L.push('render() by line: creatures running any of it (lines run by all are folded)');
let fold = null;
RL.forEach((l, i) => { const a = RLs[i], b = a + l.length; const who = keys.filter(k => res[k].render.some(([x, y]) => x < b && y > a && RS.slice(Math.max(x, a), Math.min(y, b)).trim()));
  const n = who.length; const tag = n === keys.length ? 'all' : n === 0 ? 'none' : n <= 6 ? who.join(',') : String(n);
  if (fold && fold.tag === tag && (tag === 'all' || tag === 'none')) { fold.to = T.renderFrom + i; fold.kb += l.length; return; }
  if (fold) L.push(`  ${fold.from}${fold.to !== fold.from ? '-' + fold.to : ''}: ${fold.tag} (${(fold.kb / 1024).toFixed(1)} KB)${fold.text ? '  ' + fold.text : ''}`);
  fold = { tag, from: T.renderFrom + i, to: T.renderFrom + i, kb: l.length, text: l.trim().replace(/\s+/g, ' ').slice(0, 90) }; });
if (fold) L.push(`  ${fold.from}${fold.to !== fold.from ? '-' + fold.to : ''}: ${fold.tag} (${(fold.kb / 1024).toFixed(1)} KB)  ${fold.text}`);

// ---- the drawing: what the trees setup() leaves use (the drawing side must handle it) ----
{ const F = new Map(); keys.forEach(k => (res[k].feat || []).forEach(f => { if (!F.has(f)) F.set(f, new Set()); F.get(f).add(k); }));
  L.push(''); L.push('the drawing trees after setup(): feature: how many creatures (which, when few)');
  [...F.entries()].sort((a, b) => b[1].size - a[1].size).forEach(([f, s]) => L.push(`  ${f}: ${s.size}${s.size <= 8 ? ' (' + [...s].join(', ') + ')' : ''}`)); }
// ---- the art: what the creatures' front SVGs use (the drawing side must handle it) ----
const feat = new Map(), add = (k, who) => { if (!feat.has(k)) feat.set(k, new Set()); feat.get(k).add(who); };
for (const k of keys) {
  const f = path.join(BUILT, 'export/tripo', k, k + '-front.svg'); if (!fs.existsSync(f)) continue;
  const s = fs.readFileSync(f, 'utf8').replace(/<metadata>[\s\S]*?<\/metadata>/, '');
  for (const m of s.matchAll(/<([a-zA-Z]+)([^>]*)>/g)) { const tag = m[1]; if (tag === 'svg') continue; add('<' + tag + '>', k);
    for (const a of m[2].matchAll(/([a-zA-Z:-]+)="([^"]*)"/g)) { const n = a[1];
      if (['d', 'cx', 'cy', 'r', 'rx', 'ry', 'x', 'y', 'width', 'height', 'id', 'offset', 'stop-color', 'fill', 'stroke', 'stroke-width', 'opacity', 'transform', 'points', 'x1', 'x2', 'y1', 'y2', 'data-art', 'values', 'in2', 'in', 'operator', 'mode', 'type', 'baseFrequency', 'numOctaves', 'seed', 'stdDeviation', 'xmlns', 'viewBox'].includes(n)) continue;
      add(tag + '@' + n + (['stroke-linecap', 'stroke-linejoin', 'gradientUnits', 'clipPathUnits', 'fill-rule', 'mix-blend-mode', 'maskUnits', 'spreadMethod'].includes(n) ? '=' + a[2] : ''), k); }
    if (/transform="[^"]*(skew|matrix)/.test(m[2])) add('transform: skew/matrix', k);
    if (/fill="url\(#[^)]*\)"/.test(m[2]) && tag !== 'stop') add('fill=url()', k);
    if (/stroke="url\(#[^)]*\)"/.test(m[2])) add('stroke=url()', k); }
  for (const m of s.matchAll(/<filter[^>]*>([\s\S]*?)<\/filter>/g)) add('filter: ' + [...m[1].matchAll(/<(fe[A-Za-z]+)/g)].map(x => x[1]).join('+'), k);
  for (const m of s.matchAll(/filter="([^"]*)"/g)) add('filter= ' + (m[1] === 'none' ? 'none' : 'url'), k);
}
L.push(''); L.push('art (front SVGs): feature: how many creatures (which, when few)');
[...feat.entries()].sort((a, b) => b[1].size - a[1].size).forEach(([f, s]) => L.push(`  ${f}: ${s.size}${s.size <= 8 ? ' (' + [...s].join(', ') + ')' : ''}`));
fs.writeFileSync(path.join(OUT, 'generic-survey.txt'), L.join('\n') + '\n');
console.log(L.slice(0, 3).join('\n'));
console.log('survey: ' + path.relative(REPO, path.join(OUT, 'generic-survey.txt')));
