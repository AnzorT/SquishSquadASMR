// Shared by record-generic.mjs and survey-generic.mjs: the test copy of the built game the generic rig is recorded in
// (the app's files untouched). rive-rig.js is changed as record-rig.mjs changes it (tick() reachable as
// RiveRig.__tick, no frame loop, tick()'s and the particles' random draws from window.__rr, each frame's pose to
// window.__rigP);
// rive-generic.js gets a hook right after setup(), before the first render (window.__rgSetup(I, { RC, pet, hero })),
// and render()'s own Math.random() (Nimbo's rain) draws from window.__rr too.
import fs from 'node:fs';
import path from 'node:path';
import { BUILT } from '../lib/menu-page.mjs';

export function makeTestCopy(TEST) {
  fs.rmSync(TEST, { recursive: true, force: true });
  fs.mkdirSync(TEST, { recursive: true });
  for (const e of fs.readdirSync(BUILT)) if (e !== 'rive-rig.js' && e !== 'rive-generic.js') fs.symlinkSync(path.join(BUILT, e), path.join(TEST, e));
  let src = fs.readFileSync(path.join(BUILT, 'rive-rig.js'), 'utf8');
  const must = (s, what, f = 'rive-rig.js') => { if (!src.includes(s)) throw new Error(f + ' changed: ' + what); };
  {
    const a = src.indexOf('  function tick(I, now, dt) {'), b = src.indexOf('  function whipFx(I, P) {');
    if (a < 0 || b < a) throw new Error('rive-rig.js changed: tick()');
    src = src.slice(0, a) + src.slice(a, b).replace(/Math\.random\(\)/g, 'window.__rr()') + src.slice(b);
    // the particles' random numbers too (emit and drawParts), as record-rig.mjs
    const pa = src.indexOf('  function emit(I, name, w, n) {'), pb = src.indexOf('  function wPrep(I) {');
    if (pa < 0 || pb < pa || !src.slice(pa, pb).includes('function drawParts(I, dt) {')) throw new Error('rive-rig.js changed: emit / drawParts');
    src = src.slice(0, pa) + src.slice(pa, pb).replace(/Math\.random\(\)/g, 'window.__rr()') + src.slice(pb);
  }
  must('    I.propZ = st.propZ;', 'the render call');
  src = src.replace('    I.propZ = st.propZ;', '    if (window.__rigP) window.__rigP(I, P, now, dt);\n    I.propZ = st.propZ;');
  must('  const kick = () => {', 'kick');
  src = src.replace('  const kick = () => {', '  const kick = () => { if (window.__noLoop) return;');
  must('setInterval(() => { const n = performance.now(); if (ALL.size', 'the watchdog');
  src = src.replace('setInterval(() => { const n = performance.now(); if (ALL.size', 'setInterval(() => { const n = performance.now(); if (window.__noLoop) return; if (ALL.size');
  src = src.replace('window.RiveRig = { v: 2,', 'window.RiveRig = { __tick: (I, now, dt) => tick(I, now, dt), __RELEASE: RELEASE, v: 2,');
  fs.writeFileSync(path.join(TEST, 'rive-rig.js'), src);

  src = fs.readFileSync(path.join(BUILT, 'rive-generic.js'), 'utf8');
  const f = 'rive-generic.js';
  must('    I.items = items; I.render = render(H); I.ready = true;', 'the end of setup()', f);
  src = src.replace('    I.items = items; I.render = render(H); I.ready = true;',
    '    I.items = items; if (window.__rgSetup) window.__rgSetup(I, { RC: ROUND[key] || null, pet: !!PETS[key], hero: HERO[key] || null }); I.render = render(H); I.ready = true;');
  const a = src.indexOf('  const render = H => (I, P, now) => {'), b = src.indexOf('  const inHeadPt = ');
  if (a < 0 || b < a) throw new Error(f + ' changed: render()');
  src = src.slice(0, a) + src.slice(a, b).replace(/Math\.random\(\)/g, 'window.__rr()') + src.slice(b);
  fs.writeFileSync(path.join(TEST, 'rive-generic.js'), src);
  return { renderFrom: src.slice(0, a).split('\n').length, renderTo: src.slice(0, b).split('\n').length - 1, file: path.join(TEST, 'rive-generic.js') };
}

// the seeded generators (the rig's own: xorshift32 / 2^32, the same as RigRandom.cs; the page's Math.random)
export const RR = `(() => { let s = 0x1f2e3d4c; window.__rr = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; })();`;
export const SEED = `(() => { let s = 0x2f6b9a1; Math.random = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; })();`;

// In the page (page.addInitScript(rgLib)): window.__rgLib(host) gives the generic rig's naming and export for the rig
// mounted in host. Names: the generic rig's own (I.E, I.pg), the art's shapes by their place in I.items ('it.<i>'),
// anything else setup() keeps on I by its path there ('bun.pl.3.el', 'tubes.0', 'faceG'), defs by their id (the
// mount's suffix dropped: '#g', '#bkn'), the rest by their place in a named parent ('#g/stop.1', 'art/g.2/path.0').
// Taken once, after setup() and before the first render (render() moves things round; the names stay with the elements).
export function rgLib() {
  window.__rgLib = host => {
    const names = new Map();
    const ENGINE = new Set(['D', 'lookM', 'onState', 'cur', 'prev', 'shots', 'press', 'spinOff', 'ptr', 'gl', 'glAt', 'blinkT', 'blinkAt', 'dbl', 'hs', 'lastSpin', 'queue', 'ctr', 'ctT', 'emL', 'parts',
      'fidgets', 'ak', 'flw', 'fex', 'fey', 'fhx', 'fhy', 'fp', 'fs', 'fx', 'fl', 'fsp', 'spb', 'jel', 'mot', 'lastP', 'bt0', 'bd', 'evC', 'evF', 'fidAt', 'lastFid', 'dent', 'ms', 'ed', 'ld', 'spTw', 'spF',
      'propZ', 'ext', 'down', 'drag', 'pressT', 'spin0', 'spinTw', 'ht', 'curL', 'sceneL', 'olDirty', 'err']);
    const nameAll = I => {
      const seen = new Set();
      const walk = (o, at) => { if (!o || typeof o !== 'object') return; if (o instanceof Element) { if (!names.has(o)) names.set(o, at); return; }
        if (o instanceof Node || seen.has(o)) return; seen.add(o);
        if (o instanceof Map) { [...o.values()].forEach((v, i) => walk(v, at + '.$' + i)); return; }
        if (o instanceof Set) { [...o].forEach((v, i) => walk(v, at + '.$' + i)); return; }
        if (Array.isArray(o)) { o.forEach((v, i) => walk(v, at + '.' + i)); return; }
        for (const k of Object.keys(o)) walk(o[k], at ? at + '.' + k : k); };
      walk(I.E, 'E'); walk(I.pg, 'pg'); (I.items || []).forEach((q, i) => { if (q.e) walk(q.e, 'it.' + i); });
      for (const k of Object.keys(I)) if (!ENGINE.has(k) && k !== 'items' && k !== 'E' && k !== 'pg') walk(I[k], k);
      (I.items || []).forEach((q, i) => walk(q, 'it.' + i));
      const svg = host.querySelector('svg'); names.set(svg, 'svg');
      const sfx = '_g' + I.u;
      // (two ids can meet once the suffix is dropped, as Maki's 'mkt' + the art's 'mkt': the later one gets '~2')
      const used = new Set(names.values()), uniq = n => { let m = n, k = 2; while (used.has(m)) m = n + '~' + k++; used.add(m); return m; };
      svg.querySelectorAll('[id]').forEach(e => { if (!names.has(e)) names.set(e, uniq('#' + e.id.split(sfx).join('').split(I.u).join(''))); });
      const sub = (e, at) => [...e.children].forEach((c, i) => { let n = names.get(c); if (!n) { n = uniq(at + '/' + c.tagName + '.' + i); names.set(c, n); } sub(c, n); });
      sub(svg, 'svg');
      const dup = new Map(); names.forEach((n, e) => { if (dup.has(n)) throw new Error('two elements named ' + n); dup.set(n, e); });
    };
    const domPath = e => { const p = []; while (e && e !== host) { const par = e.parentNode; p.unshift(e.tagName + '.' + (par ? [...par.children].indexOf(e) : 0)); e = par; } return p.join('/'); };
    const nameOf = e => names.get(e) || ('?' + domPath(e));

    // setup()'s data as JSON: elements as {$el: name}, rects as {x, y, width, height}, Maps / Sets, NaN / Infinity as
    // {$num}, undefined in arrays as {$undef}, functions as {$fn: path} (listed: ported as code), an object met again as
    // {$ref: its first path} (render compares some by identity), class instances with their class ({$cls})
    const ser = (root, skip) => {
      const seen = new Map(), fns = [];
      const go = (v, at) => {
        if (v === undefined) return undefined;
        if (v === null || typeof v === 'string' || typeof v === 'boolean') return v;
        if (typeof v === 'number') return Number.isFinite(v) ? v : { $num: String(v) };
        if (typeof v === 'function') { fns.push(at); return { $fn: at }; }
        if (typeof v !== 'object') return { $other: String(v) };
        if (v instanceof Element) return { $el: nameOf(v) };
        if (v instanceof Node) return { $node: v.nodeName };
        if (seen.has(v)) return { $ref: seen.get(v) };
        seen.set(v, at);
        if ((typeof DOMRectReadOnly !== 'undefined' && v instanceof DOMRectReadOnly) || (typeof SVGRect !== 'undefined' && v instanceof SVGRect)) return { x: v.x, y: v.y, width: v.width, height: v.height };
        if (typeof SVGMatrix !== 'undefined' && (v instanceof SVGMatrix || (typeof DOMMatrixReadOnly !== 'undefined' && v instanceof DOMMatrixReadOnly))) return { $matrix: [v.a, v.b, v.c, v.d, v.e, v.f] };
        if (ArrayBuffer.isView(v)) return Array.from(v, (x, i) => go(x, at + '.' + i));
        if (v instanceof Map) return { $map: [...v].map(([k, x], i) => [go(k, at + '.$k' + i), go(x, at + '.$' + i)]) };
        if (v instanceof Set) return { $set: [...v].map((x, i) => go(x, at + '.$' + i)) };
        if (Array.isArray(v)) { const o = []; for (let i = 0; i < v.length; i++) { const x = go(v[i], at + '.' + i); o.push(x === undefined ? { $undef: 1 } : x); } return o; }
        const o = {}, pr = Object.getPrototypeOf(v);
        if (pr && pr !== Object.prototype && pr.constructor && pr.constructor.name) o.$cls = pr.constructor.name;
        for (const k of Object.keys(v)) { if (skip && skip(v, k)) continue; const x = go(v[k], at ? at + '.' + k : k); if (x !== undefined) o[k] = x; }
        return o;
      };
      return { data: go(root, ''), fns };
    };
    // the drawing's element tree: tag, name, attributes (paths keep their d: the art's own, unless render() writes it),
    // display none, children
    const tree = e => { const o = { t: e.tagName }, n = names.get(e); if (n) o.n = n; const a = {};
      for (const at of e.attributes) a[at.name] = at.value;
      if (e.style && e.style.display === 'none') a.display = 'none';
      if (Object.keys(a).length) o.a = a; if (e.children.length) o.c = [...e.children].map(tree); return o; };
    return { names, ENGINE, nameAll, nameOf, ser, tree };
  };
}
