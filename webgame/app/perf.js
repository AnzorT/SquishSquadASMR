// The in-app game's frame-rate guard (performance, 2026-11-14). Loaded first, before any design script.
// The creatures (rive-rig.js) rebuild and redraw their SVG every frame: the costliest thing left on a weak phone. This
// watches the page's frame rate and, while it stays low, lets the creatures update less often so everything else (the
// camera, the weather, the buttons) stays smooth:
//   step 1: every frame (60) -> under ~55 fps for 2 s -> step 2: every other frame (30)
//   step 2 -> still under ~45 fps for 3 s -> step 3: every third frame (20). Kept for the session.
// (Watched from 4 s after the page has loaded, not while it boots.)
// rive-rig.js asks SquishPerf.skipRig() once a frame (patched in by webgame/patches.mjs); a creature with sqHold set
// (the menu's, while hidden under the house or a page) isn't drawn at all.
// A phone with 2 GB of memory or 2 cores or fewer starts at step 2.
(function () {
  var weak = (navigator.deviceMemory && navigator.deviceMemory <= 2) || (navigator.hardwareConcurrency && navigator.hardwareConcurrency <= 2);
  var P = window.SquishPerf = { step: weak ? 2 : 1, fps: 0 };
  Object.defineProperty(P, 'half', { get: function () { return P.step > 1; } });
  var t0 = 0, n = 0, slow = 0, k = 0, last = 0;
  function watch(now) {
    if (P.step >= 3) return; // nothing lower to go to
    requestAnimationFrame(watch);
    // a single stall of over a second (a page or the house loading) is a hiccup, not a slow phone: start over
    if (now - last > 1000) t0 = 0;
    last = now;
    if (!t0) { t0 = now; n = 0; return; }
    n++;
    if (now - t0 < 1000) return;
    var fps = (n * 1000) / (now - t0);
    P.fps = fps; t0 = now; n = 0;
    slow = fps < (P.step === 1 ? 55 : 45) ? slow + 1 : 0;
    if (slow >= (P.step === 1 ? 2 : 3)) {
      P.step++; slow = 0;
      // noted for the chest smash's page (tools/chest/host.html): a phone that had to slow the menu's creatures opens
      // its chests in their lighter mode from the start
      try { localStorage.setItem('sq.perfStep', JSON.stringify({ step: P.step, t: Date.now() })); } catch (e) { /* no storage */ }
    }
  }
  // judged from 4 s after the page has loaded: while it boots it runs at 8-14 fps even on a fast machine, and
  // counting that put every phone at step 2 for the session
  function begin() { setTimeout(function () { requestAnimationFrame(watch); }, 4000); }
  if (document.readyState === 'complete') begin(); else window.addEventListener('load', begin);
  // a hidden page (the app in the background) doesn't count as slow
  document.addEventListener('visibilitychange', function () { t0 = 0; slow = 0; mt = 0; });
  P.skipRig = function () { if (P.step === 1) return false; k = (k + 1) % P.step; return k !== 0; };

  // The tester's FPS counter (src/components/FpsCounter.js): while the app asks (GameScreen, SquishPerf.meter(true)),
  // this page's own frames go to the app every half second: { fps, low: the slowest frame as fps, lite: creatures
  // drawn less often }. Off unless asked: it costs a frame callback and a message.
  var mt = 0, mlast = 0, mn = 0, mworst = 0, mid = 0;
  function meter(now) {
    mid = requestAnimationFrame(meter);
    if (!mt) { mt = mlast = now; mn = 0; mworst = 0; return; }
    var dt = now - mlast; mlast = now; mn++;
    if (dt > mworst) mworst = dt;
    if (now - mt < 500) return;
    var m = { fps: Math.round((mn * 1000) / (now - mt)), low: Math.round(1000 / Math.max(mworst, 1)), lite: P.step > 1 };
    mt = now; mn = 0; mworst = 0;
    if (window.ReactNativeWebView) window.ReactNativeWebView.postMessage(JSON.stringify({ type: 'fps', payload: m }));
  }
  P.meter = function (on) {
    if (on && !mid) { mt = 0; mid = requestAnimationFrame(meter); }
    else if (!on && mid) { cancelAnimationFrame(mid); mid = 0; }
  };
})();

// House moves on demand (memory, 2026-11-14). The design's house-moves.js builds every creature's own furniture moves
// (sit, lounge, bed, water, play, blocks, cheer…) as keyframe tables when the page loads: all ~86 creatures, ~230 MB of
// live JS memory, while a player has a handful in the house. On a 3-4 GB phone that pressure alone made everything
// stutter, and the build was part of the menu's slow first seconds. Here the builders (build, bedBuild, waterBuild,
// playBuild, bBuild, and house-cx.js's cBuild) keep a creature's calls and run them, in their order, the first time that
// creature is drawn (rive-rig.js asks need(key) before its frame, patched in by webgame/patches.mjs); the shared
// defaults (no key) are built at once. The result is the same tables, only for the creatures that show up.
// Each builder ends by noting the creature in its table (build -> PROF[key], bedBuild -> BED, waterBuild -> WAT,
// playBuild -> PLY, bBuild -> BB, cBuild -> CX), and the later creature files loop over Object.keys(PROF) to call the
// other builders: so a held call notes the creature there at once (with what it was given; the real build, later,
// writes its own value over it), or those files would skip every creature whose build is waiting.
(function () {
  var pend = {}, done = {};
  var TABLE = { build: 'PROF', bedBuild: 'BED', waterBuild: 'WAT', playBuild: 'PLY', bBuild: 'BB', cBuild: 'CX' };
  function wrap(fn, H, name) {
    return function (key, X) {
      if (key == null || key === '' || done[key]) return fn.apply(this, arguments);
      var self = this, args = arguments, T = H[TABLE[name]];
      if (T && !(key in T)) T[key] = X || {};
      (pend[key] || (pend[key] = [])).push(function () { fn.apply(self, args); });
    };
  }
  window.SquishLazy = {
    // house-moves.js, right after it made R.HOUSE and before the creature files' builders run
    house: function (H) {
      ['build', 'bedBuild', 'waterBuild', 'playBuild', 'bBuild'].forEach(function (n) { if (typeof H[n] === 'function') H[n] = wrap(H[n], H, n); });
      // house-cx.js adds cBuild (and its table CX) later: Object.assign(H, { cBuild, CX, ST0 })
      var cb = typeof H.cBuild === 'function' ? wrap(H.cBuild, H, 'cBuild') : H.cBuild;
      Object.defineProperty(H, 'cBuild', { configurable: true, enumerable: true, get: function () { return cb; }, set: function (f) { cb = typeof f === 'function' ? wrap(f, H, 'cBuild') : f; } });
    },
    // a creature is about to be drawn: build its moves now (once)
    need: function (key) {
      if (!key || done[key]) return;
      done[key] = 1;
      var q = pend[key]; if (!q) return;
      delete pend[key];
      q.forEach(function (f) { try { f(); } catch (e) { console.error('[SquishLazy]', key, e); } });
    },
    pending: function () { return Object.keys(pend).length; },
  };
})();
