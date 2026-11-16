import { GameBack, useGameDims, useWide } from '../royal/GameFrame';
import React, { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Animated, AppState, BackHandler, Easing, PanResponder, StyleSheet, Text, View } from 'react-native';
import RoundButton, { BackGlyph } from '../components/candy/RoundButton';
import useRewardedAd from '../components/useRewardedAd';
import sfx from '../audio/sfx';
import { FxLayer, PopIn } from '../squishdoku/fx';
import { makeHaptics, prefetchImages } from '../squishdoku/game';
import { Countdown } from '../squishdoku/BlitzGame';
import { BlitzOverCard, BlitzSaveCard, PauseCard as BlitzPauseCard } from '../squishdoku/Overlays';
import { blitzSaveGems } from '../squishdoku/levels';
import * as squad from '../squad/api';
import { BtnLabel, Chip, SdButton } from '../squishdoku/ui';
import { F, INK } from '../squishdoku/theme';
import { saveStack } from '../firebase/firestore';
import { Banner } from '../sort/Game';
import { HintCard, PauseCard, ResultCard } from './Cards';
import { bestMove, canMove, makePuzzle, same, stackOf, topOf } from './engine';
import { LEVELS, TIER_COLORS, TIER_NAMES, afterBlitz, afterLevel, blitzSpec, levelSpec, record } from './levels';
import { Cushion, GoalCard, OVER, StarBadge, Squishy, bodyH } from './Pieces';
import { pickCast } from './roster';
import { usePingPong } from '../components/idleLoop';

// One level of Squish Stack (SQUISH_STACK.md), and the next ones after it:
//   intro  the level's number and what's in it
//   deal   the squishies drop onto their cushions, one after another
//   play   tap a cushion to lift its top squishy (it hovers and wiggles),
//          tap another to put it there; or drag it. A big one dropped on a
//          smaller one squashes it flat and boings home (no move counted).
//          No clock (the user, 2026-11-16), and no UNDO: RESTART and HINT.
//   won
// The puzzle's rules are engine.js; this draws and animates them.
//
// BLITZ MODE (`blitz`, levels.js blitzSpec; 2026-11-16, the user: "the same
// thing" as Squishdoku's): after a 3, 2, 1, GO! puzzle after puzzle, with no
// end and no clock. A big one dropped on a smaller one is the mistake that
// stops the run ('held'). It can be saved, once with a rewarded ad (free with Remove Ads)
// or with gems, each save twice the last (functions/squad.js blitzSave), and
// the run goes on from the same puzzle; or it's GAME OVER and the puzzles
// cleared pay out (levels.js afterBlitz). No hints.

const LOAD_SOUNDS = ['st_lift', 'st_land', 'st_cushion', 'st_ouch', 'st_bounce', 'st_undo', 'st_hint', 'st_empty', 'st_news', 'sd_tick', 'sd_solve', 'sd_confetti', 'sd_boardIn', 'sd_timeUp'];

const GAP = 16; // between the tallest tower and a lifted squishy
const GRAVITY = Easing.bezier(0.55, 0, 1, 0.45);

const NEWS = {
  picture: { text: 'GOAL CARD!', sub: 'Make the cushions look like the picture' },
  cushion: { text: 'A 4TH CUSHION!', sub: 'Big towers move much faster now' },
};

// --- geometry -------------------------------------------------------------------------

// Squished a little by the ones sitting on it.
const weightOf = (above) => ({ sx: 1 + 0.035 * above, sy: Math.max(0.8, 1 - 0.045 * above) });

// A stack's feet lines (0 = the cushion, negative is up), bottom to top.
function stackLines(list, bh) {
  let yb = 0;
  const out = [];
  list.forEach((r, j) => {
    const w = weightOf(list.length - 1 - j);
    out.push({ r, yb, ...w });
    yb -= bh[r] * w.sy * (1 - OVER);
  });
  const height = list.length ? -(out[out.length - 1].yb - bh[list[list.length - 1]]) : 0;
  return { out, height };
}

// Where everything goes in a w × h play area: a column per cushion, the
// squishies sized by rank (the smallest half the biggest), all shrunk
// together until the tallest tower and a lifted squishy above it fit.
function geometry(w, h, n, k, who) {
  const colW = w / k;
  const cushW = Math.min(colW * 0.86, 150);
  const cushH = Math.max(24, Math.min(cushW * 0.34, 44));
  const surface = h - 18 - cushH * 0.6;
  const ratio = who.map((c) => bodyH(c, 1));
  const widths = (m) => Array.from({ length: n }, (_, r) => m * (n === 1 ? 1 : 0.5 + (0.5 * r) / (n - 1)));
  const maxW = Math.min(colW * 0.94, 140);
  let bw = widths(maxW);
  let bh = bw.map((x, r) => x * ratio[r]);
  const tower = stackLines(Array.from({ length: n }, (_, i) => n - 1 - i), bh).height;
  const s = Math.max(0.3, Math.min(1, (surface - 10) / (tower + GAP + Math.max(...bh))));
  bw = widths(maxW * s);
  bh = bw.map((x, r) => x * ratio[r]);
  return { w, h, k, colW, cushW, cushH, surface, bw, bh, hover: surface - tower * s - GAP };
}
const cx = (G, p) => G.colW * (p + 0.5);
// every squishy's top-left, its weight and drawing order (`skip`: the lifted one)
function spotsOf(G, pos, skip = -1) {
  const m = {};
  for (let p = 0; p < G.k; p++) {
    stackLines(stackOf(pos, p).filter((r) => r !== skip), G.bh).out.forEach((o, j) => {
      m[o.r] = { x: cx(G, p) - G.bw[o.r] / 2, y: G.surface + o.yb - G.bh[o.r], sx: o.sx, sy: o.sy, z: 10 + j };
    });
  }
  return m;
}

// --- drawing ---------------------------------------------------------------------------

// A squishy: placed (x, y), squished by the ones on it (wx, wy, from its
// feet), and its own squash / hop / wiggle on top.
const Piece = memo(function Piece({ c, w, h, a, z }) {
  const wT = a.wy.interpolate({ inputRange: [0, 1], outputRange: [h / 2, 0] });
  const sqX = a.sq.interpolate({ inputRange: [-1, 0, 1], outputRange: [0.9, 1, 1.22] });
  const sqY = a.sq.interpolate({ inputRange: [-1, 0, 1], outputRange: [1.12, 1, 0.74] });
  const sqT = a.sq.interpolate({ inputRange: [-1, 0, 1], outputRange: [-0.06 * h, 0, 0.13 * h] });
  const rotate = a.rot.interpolate({ inputRange: [-1, 1], outputRange: ['-6deg', '6deg'] });
  return (
    <Animated.View pointerEvents="none" style={[styles.piece, { width: w, height: h, zIndex: z, opacity: a.o, transform: [{ translateX: a.x }, { translateY: a.y }] }]}>
      <Animated.View style={{ transform: [{ translateY: wT }, { scaleX: a.wx }, { scaleY: a.wy }] }}>
        <Animated.View style={{ transform: [{ translateY: a.hop }, { translateY: sqT }, { scaleX: sqX }, { scaleY: sqY }, { rotate }] }}>
          <Squishy c={c} w={w} />
        </Animated.View>
      </Animated.View>
    </Animated.View>
  );
});

// A cushion in its place, bumping when something lands on it, glowing for
// the tutorial and the hint ('white' / 'gold').
const Seat = memo(function Seat({ i, x, top, w, h, star, bump, glow }) {
  // one native loop (src/components/idleLoop.js)
  const pulse = usePingPong(960, { on: !!glow });
  return (
    <View pointerEvents="none" style={[styles.seat, { left: x - w / 2, top, width: w, height: h + 5 }]}>
      {glow ? (
        <Animated.View
          style={[
            styles.halo,
            {
              borderRadius: h,
              backgroundColor: glow === 'gold' ? 'rgba(255,224,69,0.85)' : 'rgba(255,255,255,0.8)',
              opacity: pulse.interpolate({ inputRange: [0, 1], outputRange: [0.3, 0.95] }),
              transform: [{ scale: pulse.interpolate({ inputRange: [0, 1], outputRange: [0.94, 1.08] }) }],
            },
          ]}
        />
      ) : null}
      <Animated.View
        style={{
          transform: [
            { translateY: bump.interpolate({ inputRange: [-1, 0, 1], outputRange: [-h * 0.04, 0, h * 0.09] }) },
            { scaleX: bump.interpolate({ inputRange: [-1, 0, 1], outputRange: [0.97, 1, 1.07] }) },
            { scaleY: bump.interpolate({ inputRange: [-1, 0, 1], outputRange: [1.06, 1, 0.8] }) },
          ],
        }}
      >
        <Cushion w={w} h={h} i={i} />
      </Animated.View>
      {star ? (
        <View style={[styles.badge, { left: w / 2 - 14, top: h - 9 }]}>
          <StarBadge size={28} />
        </View>
      ) : null}
    </View>
  );
});

// a number in the counter: a small label over it
const Stat = memo(function Stat({ label, value, gold }) {
  return (
    <View style={styles.stat}>
      <Text style={[styles.statLabel, gold && styles.gold]}>{label}</Text>
      <Text style={[styles.statNum, gold && styles.gold]}>{value}</Text>
    </View>
  );
});

// state with a ref beside it, for the touch handlers and the timers
function useRefState(init) {
  const [v, setV] = useState(init);
  const ref = useRef(init);
  const set = useCallback((x) => {
    ref.current = x;
    setV(x);
  }, []);
  return [v, ref, set];
}

// --- the game --------------------------------------------------------------------------

export default function StackGame({ startLv = 1, cast, blitz = false, profile, uid, adsFree, haptics: hapticsOn = true, onExit }) {
  const wide = useWide(); // landscape (REDESIGN_V8)
  const dims = useGameDims();
  const haptics = useRef(makeHaptics(hapticsOn)).current;
  const ad = useRewardedAd();
  const [lv, setLv] = useState(startLv);
  const [round, setRound] = useState(0);
  // Blitz: the run, its puzzle, the puzzles cleared, its saves, which of its
  // cards is up, and the countdown
  const [run, setRun] = useState(0);
  const [idx, setIdx] = useState(0);
  const idxRef = useRef(0);
  idxRef.current = idx;
  const [cleared, setCleared] = useState(0);
  const [blitzCard, setBlitzCard] = useState(null); // save | over
  const blitzCardRef = useRef(null);
  blitzCardRef.current = blitzCard;
  const [failReason, setFailReason] = useState('mistake');
  const [adUsed, setAdUsed] = useState(false);
  const [gemSaves, setGemSaves] = useState(0);
  const [saving, setSaving] = useState(false);
  const [saveErr, setSaveErr] = useState(null);
  const [over, setOver] = useState(null);
  const [count, setCount] = useState(null); // 3, 2, 1, 'GO!'
  const runStarted = useRef(false);
  // the run so far: the puzzles it cleared, and whether it's been paid out (payOut)
  const runRec = useRef({ cleared: 0, ended: false });
  // the level at the player's skill when it starts (adaptive difficulty;
  // fixed for the puzzle even when the saved skill changes under it)
  const profileRef = useRef(profile);
  profileRef.current = profile;
  const spec = useMemo(
    () => (blitz ? blitzSpec(idx, record(profileRef.current).bSkill) : levelSpec(lv, record(profileRef.current).skill)),
    [lv, round, idx, run], // eslint-disable-line react-hooks/exhaustive-deps
  );
  // a fresh puzzle and fresh squishies for every level and every retry
  const puzzle = useMemo(() => makePuzzle(spec), [spec]);
  const who = useMemo(() => pickCast(cast, spec.n), [puzzle]); // eslint-disable-line react-hooks/exhaustive-deps

  const [area, setArea] = useState(null);
  const areaRef = useRef(null);
  const origin = useRef({ x: 0, y: 0 });
  const onArea = useCallback((e) => {
    const { width, height } = e.nativeEvent.layout;
    if (width > 0 && height > 0) setArea((a) => (a && a.w === width && a.h === height ? a : { w: width, h: height }));
    areaRef.current?.measure((x, y, w, h, px, py) => (origin.current = { x: px, y: py }));
  }, []);
  const g = useMemo(() => (area && who.length === spec.n ? geometry(area.w, area.h, spec.n, spec.k, who) : null), [area, spec, who]);

  const anims = useMemo(
    () =>
      who.map(() => ({
        x: new Animated.Value(0),
        y: new Animated.Value(0),
        o: new Animated.Value(0),
        wx: new Animated.Value(1),
        wy: new Animated.Value(1),
        sq: new Animated.Value(0),
        hop: new Animated.Value(0),
        rot: new Animated.Value(0),
        tgt: null, // where it's headed
        w: -1, // the weight it's squished to
        tok: 0, // bumped by every new move, so a stale landing does nothing
        wig: null,
      })),
    [who],
  );
  const bumps = useMemo(() => Array.from({ length: 4 }, () => new Animated.Value(0)), []);
  const fx = useRef(null);
  const fxTop = useRef(null); // Blitz: over the cards, a new best's confetti

  // --- state ---
  const [phase, phaseRef, setPhase] = useRefState('load');
  const [pos, posRef, setPos] = useRefState(puzzle.start);
  const [held, heldRef, setHeld] = useRefState(null); // { r, from }
  const [moves, movesRef, setMoves] = useRefState(0);
  const [hinted, hintedRef, setHinted] = useRefState(false);
  const [paused, pausedRef, setPaused] = useRefState(false);
  const hist = useRef([]);
  const [canUndo, setCanUndo] = useState(false);
  const [top, setTop] = useState(-1); // the last one moved, drawn over the rest
  const [glow, setGlow] = useState(null); // the hint: { from, to }
  const [dragCol, setDragCol] = useState(-1);
  const [banner, setBanner] = useState(null);
  const [result, setResult] = useState(null);
  const [hintAsk, setHintAsk] = useState(false);
  const hintAskRef = useRef(false);
  hintAskRef.current = hintAsk;
  const say = useCallback((text, more = {}) => setBanner({ text, key: Date.now(), ...more }), []);

  // timers, cleared on a new puzzle and on leaving
  const timers = useRef([]);
  const alive = useRef(true);
  const later = useCallback((ms, fn) => {
    const id = setTimeout(() => alive.current && fn(), ms);
    timers.current.push(id);
    return id;
  }, []);
  const clearTimers = useCallback(() => {
    timers.current.forEach(clearTimeout);
    timers.current = [];
  }, []);
  useEffect(() => {
    alive.current = true;
    sfx.preload(LOAD_SOUNDS);
    return () => {
      alive.current = false;
      clearTimers();
    };
  }, [clearTimers]);

  // what the handlers and timers read
  const live = useRef({});
  live.current = { g, anims, puzzle, spec, who, bumps };

  // --- moving things ---------------------------------------------------------------------

  const rateOf = (r) => 1.25 - (0.45 * r) / Math.max(1, live.current.spec.n - 1);
  const hoverY = (r) => live.current.g.hover - live.current.g.bh[r];
  const xOf = (r, p) => cx(live.current.g, p) - live.current.g.bw[r] / 2;

  // Everyone not lifted springs to where they belong, squished by what's on them.
  function settle() {
    const { g: G, anims: AS } = live.current;
    if (!G) return;
    const h = heldRef.current;
    const m = spotsOf(G, posRef.current, h ? h.r : -1);
    Object.keys(m).forEach((k) => {
      const r = Number(k);
      const A = AS[r];
      const s = m[r];
      if (A.w !== s.sy) {
        A.w = s.sy;
        Animated.parallel([
          Animated.spring(A.wx, { toValue: s.sx, friction: 5, tension: 140, useNativeDriver: true }),
          Animated.spring(A.wy, { toValue: s.sy, friction: 5, tension: 140, useNativeDriver: true }),
        ]).start();
      }
      if (A.tgt && Math.abs(A.tgt.x - s.x) < 0.5 && Math.abs(A.tgt.y - s.y) < 0.5) return;
      A.tgt = { x: s.x, y: s.y };
      A.tok += 1;
      Animated.parallel([
        Animated.spring(A.x, { toValue: s.x, friction: 7, tension: 120, useNativeDriver: true }),
        Animated.spring(A.y, { toValue: s.y, friction: 7, tension: 120, useNativeDriver: true }),
      ]).start();
    });
  }

  // steps: [{ x, y, ms, ex, ey }] one after another; `done` only if nothing
  // else moved it meanwhile
  function fly(r, steps, done) {
    const A = live.current.anims[r];
    const tok = ++A.tok;
    const last = steps[steps.length - 1];
    A.tgt = { x: last.x, y: last.y };
    Animated.sequence(
      steps.map((s) =>
        Animated.parallel([
          Animated.timing(A.x, { toValue: s.x, duration: s.ms, easing: s.ex || Easing.inOut(Easing.quad), useNativeDriver: true }),
          Animated.timing(A.y, { toValue: s.y, duration: s.ms, easing: s.ey || Easing.inOut(Easing.quad), useNativeDriver: true }),
        ]),
      ),
    ).start(({ finished }) => {
      if (finished && A.tok === tok && alive.current && done) done();
    });
  }

  function wiggle(r, on) {
    const A = live.current.anims[r];
    A.wig?.stop();
    A.wig = null;
    if (!on) {
      Animated.spring(A.rot, { toValue: 0, friction: 4, tension: 160, useNativeDriver: true }).start();
      return;
    }
    A.wig = Animated.loop(
      Animated.sequence([
        Animated.timing(A.rot, { toValue: 1, duration: 260, easing: Easing.inOut(Easing.sin), useNativeDriver: true }),
        Animated.timing(A.rot, { toValue: -1, duration: 260, easing: Easing.inOut(Easing.sin), useNativeDriver: true }),
      ]),
    );
    A.wig.start();
  }

  function bump(p, k = 1) {
    const B = live.current.bumps[p];
    B.setValue(0);
    Animated.sequence([
      Animated.timing(B, { toValue: k, duration: 80, easing: Easing.out(Easing.quad), useNativeDriver: true }),
      Animated.spring(B, { toValue: 0, friction: 3.5, tension: 170, useNativeDriver: true }),
    ]).start();
  }

  function pickUp(r, p) {
    const A = live.current.anims[r];
    setHeld({ r, from: p });
    setTop(r);
    setGlow(null);
    sfx.play('st_lift', { rate: rateOf(r) });
    haptics.tap();
    if (A.w !== 1) {
      A.w = 1;
      Animated.parallel([
        Animated.spring(A.wx, { toValue: 1, friction: 5, tension: 140, useNativeDriver: true }),
        Animated.spring(A.wy, { toValue: 1, friction: 5, tension: 140, useNativeDriver: true }),
      ]).start();
    }
    A.tgt = { x: xOf(r, p), y: hoverY(r) };
    A.tok += 1;
    Animated.parallel([
      Animated.timing(A.x, { toValue: A.tgt.x, duration: 150, easing: Easing.out(Easing.quad), useNativeDriver: true }),
      Animated.timing(A.y, { toValue: A.tgt.y, duration: 200, easing: Easing.out(Easing.back(1.6)), useNativeDriver: true }),
    ]).start();
    wiggle(r, true);
    settle(); // the stack under it stands up a little
  }

  function land(r, soft = false) {
    if (heldRef.current && heldRef.current.r === r) return;
    const { g: G, anims: AS } = live.current;
    settle(); // the ones under it squish down a little
    const A = AS[r];
    A.sq.setValue(0);
    Animated.sequence([
      Animated.timing(A.sq, { toValue: soft ? 0.6 : 1, duration: 70, useNativeDriver: true }),
      Animated.spring(A.sq, { toValue: 0, friction: 3.5, tension: 170, useNativeDriver: true }),
    ]).start();
    const p = posRef.current[r];
    if (stackOf(posRef.current, p)[0] === r) {
      bump(p, soft ? 0.6 : 1);
      sfx.play('st_cushion', { rate: rateOf(r) });
    } else sfx.play('st_land', { rate: rateOf(r) });
    if (soft) return;
    haptics.place();
    const s = spotsOf(G, posRef.current)[r];
    fx.current?.burst('dust', cx(G, p), s.y + G.bh[r] * 0.98, 0.8);
    if (phaseRef.current === 'play' && same(posRef.current, live.current.puzzle.goal)) win();
  }

  // the lifted one let go over cushion p
  function drop(p, dragged = false) {
    const h = heldRef.current;
    if (!h) return;
    const G = live.current.g;
    const { r, from } = h;
    wiggle(r, false);
    setDragCol(-1);
    setHeld(null);
    if (p === from || p < 0) {
      const s = spotsOf(G, posRef.current)[r];
      fly(r, [{ x: s.x, y: s.y, ms: 170, ey: GRAVITY }], () => land(r, true));
      return;
    }
    if (canMove(posRef.current, from, p) < 0) {
      bounce(r, from, p);
      return;
    }
    const next = posRef.current.slice();
    next[r] = p;
    setPos(next);
    hist.current.push([r, from, p]);
    setCanUndo(true);
    setMoves(movesRef.current + 1);
    const s = spotsOf(G, next)[r];
    const dx = Math.abs(cx(G, p) - cx(G, from));
    if (dragged) fly(r, [{ x: s.x, y: s.y, ms: 200, ex: Easing.out(Easing.quad), ey: GRAVITY }], () => land(r));
    else fly(r, [{ x: s.x, y: hoverY(r), ms: 110 + dx * 0.45 }, { x: s.x, y: s.y, ms: 170, ey: GRAVITY }], () => land(r));
  }

  // Too big for cushion p: it lands on the small one's head, squashes it
  // flat, and boings home. No move counted.
  function bounce(r, from, p) {
    const { g: G, anims: AS, spec: S } = live.current;
    const t = topOf(posRef.current, p);
    const ts = spotsOf(G, posRef.current)[t];
    const sit = ts.y + G.bh[t] * (1 - ts.sy) + G.bh[t] * 0.42 - G.bh[r];
    const dx = Math.abs(cx(G, p) - cx(G, from));
    setTop(r);
    fly(r, [{ x: xOf(r, p), y: hoverY(r), ms: 110 + dx * 0.45 }, { x: xOf(r, p), y: sit, ms: 140, ey: GRAVITY }], () => {
      const T = AS[t];
      T.sq.setValue(0);
      Animated.sequence([
        Animated.timing(T.sq, { toValue: 1.7, duration: 80, useNativeDriver: true }),
        Animated.timing(T.sq, { toValue: 1.55, duration: 180, useNativeDriver: true }),
        Animated.spring(T.sq, { toValue: 0, friction: 3, tension: 140, useNativeDriver: true }),
      ]).start();
      sfx.play('st_ouch');
      haptics.wrong();
      fx.current?.burst('angry', cx(G, p), ts.y + G.bh[t] * 0.5, 1);
      say('TOO BIG!', { small: true, ink: '#8e1030', hold: 380, sub: S.lv <= 3 ? 'Big ones never sit on smaller ones' : null });
      if (blitz) blitzMistake();
      const tok = AS[r].tok;
      later(260, () => {
        if (AS[r].tok !== tok || (heldRef.current && heldRef.current.r === r)) return;
        sfx.play('st_bounce');
        const s = spotsOf(G, posRef.current)[r];
        fly(r, [{ x: xOf(r, p), y: hoverY(r) - 24, ms: 220, ey: Easing.out(Easing.quad) }, { x: s.x, y: hoverY(r) - 10, ms: 130 + dx * 0.45 }, { x: s.x, y: s.y, ms: 170, ey: GRAVITY }], () => land(r, true));
      });
    });
  }

  // Blitz: a mistake stops the run; then the save card
  function blitzMistake() {
    setPhase('held');
    setGlow(null);
    if (heldRef.current) drop(heldRef.current.from);
    later(1000, () => offerSave('mistake'));
  }

  // Blitz: a mistake — save the run, or END RUN for GAME OVER
  function offerSave(reason) {
    setFailReason(reason);
    setSaveErr(null);
    setBlitzCard('save');
  }

  // The run goes on from the same puzzle.
  function goOn() {
    setSaving(false);
    setBlitzCard(null);
    sfx.play('sd_revive');
    setPhase('play');
  }
  function saveByAd() {
    const back = () => {
      setAdUsed(true);
      acts.current.goOn();
    };
    if (adsFree) back();
    else ad.show(back);
  }
  function saveByGems() {
    if (saving) return;
    setSaving(true);
    setSaveErr(null);
    squad
      .blitzSave(gemSaves)
      .then(() => {
        setGemSaves((n) => n + 1);
        acts.current.goOn();
      })
      .catch((e) => {
        setSaving(false);
        setSaveErr(String((e && e.message) || e) === 'not_enough_gems' ? 'Not enough gems' : "Couldn't reach the server. Try again?");
      });
  }

  // Pays the run out once (the puzzles it cleared) and saves the record. Also
  // when the player quits or starts over in the middle of a run, so cleared
  // puzzles always count.
  function payOut() {
    const R = runRec.current;
    if (!blitz || !runStarted.current || R.ended) return null;
    R.ended = true;
    const out = afterBlitz(profileRef.current, { cleared: R.cleared });
    if (uid) saveStack(uid, out.update, out.coins, out.xp, profileRef.current);
    return out;
  }

  // Blitz: GAME OVER — the puzzles cleared, the best, and what they paid
  function endRun() {
    setPhase('lost');
    setGlow(null);
    if (heldRef.current) drop(heldRef.current.from);
    const top = record(profileRef.current).blitz.top;
    const out = payOut() || { coins: 0, xp: 0, levelUps: [], newTop: false };
    const res = { ...out, cleared: runRec.current.cleared, best: Math.max(top, runRec.current.cleared) };
    setOver(res);
    setBlitzCard('over');
    if (res.newTop) {
      sfx.play('sd_blitzWin');
      haptics.win();
      [0, 300, 600].forEach((d, i) => later(d, () => fxTop.current?.burst('confetti', dims.width * (0.25 + 0.25 * i), dims.height * 0.3, 1)));
    } else {
      sfx.play('sd_blitzFail');
      haptics.wrong();
      later(600, () => sfx.play('sd_drain'));
    }
  }

  function undo() {
    if (phaseRef.current !== 'play' || pausedRef.current) return;
    if (heldRef.current) drop(heldRef.current.from);
    const m = hist.current.pop();
    if (!m) return;
    setCanUndo(hist.current.length > 0);
    const [r, from, to] = m;
    const next = posRef.current.slice();
    next[r] = from;
    setPos(next);
    setTop(r);
    setGlow(null);
    sfx.play('st_undo');
    const G = live.current.g;
    const s = spotsOf(G, next)[r];
    fly(r, [{ x: xOf(r, to), y: hoverY(r), ms: 140, ey: Easing.out(Easing.quad) }, { x: s.x, y: hoverY(r), ms: 150 }, { x: s.x, y: s.y, ms: 160, ey: GRAVITY }], () => land(r, true));
    settle(); // the stack it left stands up
  }

  // everyone back where this puzzle started
  function restart() {
    if (phaseRef.current !== 'play' || pausedRef.current) return;
    if (heldRef.current) {
      wiggle(heldRef.current.r, false);
      setHeld(null);
    }
    setPos(live.current.puzzle.start.slice());
    hist.current = [];
    setCanUndo(false);
    setMoves(0);
    setGlow(null);
    sfx.play('st_undo', { rate: 0.8 });
    settle();
  }

  function showHint() {
    if (phaseRef.current !== 'play') return;
    if (heldRef.current) drop(heldRef.current.from);
    const { puzzle: P, anims: AS, g: G } = live.current;
    const m = bestMove(P.sol, posRef.current);
    if (!m) return;
    const [r, from, to] = m;
    setHinted(true);
    setGlow({ from, to });
    const A = AS[r];
    Animated.sequence(
      [0, 1].flatMap(() => [
        Animated.timing(A.hop, { toValue: -G.bh[r] * 0.28, duration: 170, easing: Easing.out(Easing.quad), useNativeDriver: true }),
        Animated.timing(A.hop, { toValue: 0, duration: 200, easing: Easing.bounce, useNativeDriver: true }),
      ]),
    ).start();
    sfx.play('st_hint');
    fx.current?.burst('spark', cx(G, to), G.surface - 4, 1);
    later(2800, () => setGlow((x) => (x && x.from === from && x.to === to ? null : x)));
  }
  function askHint() {
    if (phaseRef.current !== 'play' || pausedRef.current) return;
    if (hintedRef.current) watchHint();
    else setHintAsk(true);
  }
  function watchHint() {
    setHintAsk(false);
    if (adsFree) {
      showHint();
      return;
    }
    // the ad leaves the app (the pause card comes up); a reward brings the game back
    ad.show(() => {
      setPaused(false);
      later(250, () => acts.current.showHint());
    });
  }

  function win() {
    setPhase('won');
    setGlow(null);
    const { g: G, anims: AS, puzzle: P, spec: S } = live.current;
    sfx.play('sd_solve');
    haptics.win();
    // a hop down each tower, top first
    let i = 0;
    for (let p = 0; p < S.k; p++) {
      stackOf(posRef.current, p)
        .slice()
        .reverse()
        .forEach((r) => {
          const d = 150 + i++ * 70;
          later(d, () =>
            Animated.sequence([
              Animated.timing(AS[r].hop, { toValue: -G.bh[r] * 0.3, duration: 170, easing: Easing.out(Easing.quad), useNativeDriver: true }),
              Animated.timing(AS[r].hop, { toValue: 0, duration: 220, easing: Easing.bounce, useNativeDriver: true }),
            ]).start(),
          );
        });
    }
    // (Blitz keeps its confetti for a new best at GAME OVER)
    if (!blitz) {
      later(250, () => {
        sfx.play('sd_confetti');
        fx.current?.burst('confetti', G.w / 2, G.h * 0.35, 1.2);
      });
      later(650, () => fx.current?.burst('confetti', G.w * 0.25, G.h * 0.5, 1));
      later(800, () => fx.current?.burst('confetti', G.w * 0.75, G.h * 0.5, 1));
    }
    if (blitz) {
      // one more cleared; on to the next puzzle
      const i = idxRef.current;
      runRec.current.cleared = i + 1;
      later(300, () => {
        setCleared(i + 1);
        sfx.play('sd_pip', { rate: 0.9 + Math.min(12, i) * 0.05 });
      });
      later(1300, () => {
        sfx.play('sd_blitzNext');
        setIdx(i + 1);
      });
      return;
    }
    const out = afterLevel(profileRef.current, S, P.par, { won: true, moves: movesRef.current, hinted: hintedRef.current });
    if (uid) saveStack(uid, out.update, out.coins, out.xp, profileRef.current);
    const { stars, coins, xp, before, after, levelUps, unlocked } = out;
    const res = { stars, coins, xp, before, after, levelUps, unlocked, moves: movesRef.current, par: P.par, hinted: hintedRef.current };
    later(1500, () => setResult(res));
  }

  // --- the round -----------------------------------------------------------------------

  function startPlay() {
    setPhase('play');
    if (blitz) runStarted.current = true;
  }
  function deal() {
    const { g: G, anims: AS, puzzle: P, spec: S } = live.current;
    setPhase('deal');
    const m = spotsOf(G, P.start);
    let last = 0;
    for (let p = 0; p < S.k; p++) {
      stackOf(P.start, p).forEach((r, j) => {
        const d = p * 110 + j * 130;
        last = Math.max(last, d);
        later(d, () => {
          Animated.timing(AS[r].o, { toValue: 1, duration: 120, useNativeDriver: true }).start();
          fly(r, [{ x: m[r].x, y: m[r].y, ms: 380, ey: GRAVITY }], () => land(r, true));
        });
      });
    }
    later(last + 560, () => {
      const news = S.news && !record(profileRef.current).best[S.lv] ? NEWS[S.news] : null;
      if (!news) {
        startPlay();
        return;
      }
      sfx.play('st_news');
      say(news.text, { small: true, sub: news.sub, hold: 1300, ink: '#0c5a9c' });
      later(1750, startPlay);
    });
  }

  // a new puzzle: reset everything and start
  const readyKey = g ? `${Math.round(g.w)}:${Math.round(g.h)}` : '';
  useEffect(() => {
    if (!g) return undefined;
    clearTimers();
    setResult(null);
    setPaused(false);
    setHintAsk(false);
    setGlow(null);
    setDragCol(-1);
    setBanner(null);
    setHeld(null);
    setTop(-1);
    setPos(puzzle.start.slice());
    hist.current = [];
    setCanUndo(false);
    setMoves(0);
    setHinted(false);
    // everyone above their spot, waiting to drop in (their spots are already
    // their targets, so nothing settles them there early)
    const m = spotsOf(g, puzzle.start);
    anims.forEach((A, r) => {
      A.wig?.stop();
      A.wig = null;
      A.tok += 1;
      [A.rot, A.sq, A.hop, A.o].forEach((v) => v.setValue(0));
      A.wx.setValue(m[r].sx);
      A.wy.setValue(m[r].sy);
      A.w = m[r].sy;
      A.x.setValue(m[r].x);
      A.y.setValue(m[r].y - g.h * 0.6);
      A.tgt = { x: m[r].x, y: m[r].y };
    });
    setPhase('intro');
    // a Blitz run opens with 3, 2, 1, GO! (the first puzzle drops in on GO)
    const countdown = blitz && !runStarted.current;
    if (!countdown) {
      const chips = [TIER_NAMES[spec.tier]];
      if (spec.goal === 'picture') chips.push('GOAL CARD');
      if (spec.k === 4) chips.push('4 CUSHIONS');
      say(blitz ? `PUZZLE ${spec.round}` : `LEVEL ${spec.lv}`, { chips, hold: 900 });
      sfx.play('sd_boardIn');
    }
    let gone = false;
    const t0 = Date.now();
    // (only creatures newer than the bundled cut-outs come over the network)
    prefetchImages(
      who.filter((c) => c.source.uri).map((c) => c.source.uri),
      2500,
    ).then(() => {
      if (gone) return;
      if (!countdown) {
        later(Math.max(0, 1250 - (Date.now() - t0)), deal);
        return;
      }
      [3, 2, 1, 'GO!'].forEach((s, i) =>
        later(200 + i * 700, () => {
          setCount(s);
          sfx.play(s === 'GO!' ? 'sd_go' : 'sd_count');
          haptics.tick();
          if (s === 'GO!') deal();
        }),
      );
      later(200 + 4 * 700, () => setCount(null));
    });
    return () => {
      gone = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [puzzle, readyKey]);

  // --- pause and leaving ---------------------------------------------------------------

  function pause() {
    if (phaseRef.current !== 'play' || pausedRef.current) return;
    if (heldRef.current) drop(heldRef.current.from);
    setPaused(true);
  }
  function resume() {
    setPaused(false);
  }
  function leave() {
    payOut();
    clearTimers();
    onExit();
  }
  const next = useCallback(() => {
    setLv((l) => Math.min(LEVELS, l + 1));
    setRound((r) => r + 1);
  }, []);
  const replay = useCallback(() => setRound((r) => r + 1), []);
  // Blitz: a fresh run from puzzle 1 (the one before is paid out first)
  function again() {
    payOut();
    clearTimers();
    runStarted.current = false;
    runRec.current = { cleared: 0, ended: false };
    setBlitzCard(null);
    setOver(null);
    setAdUsed(false);
    setGemSaves(0);
    setSaving(false);
    setSaveErr(null);
    setCleared(0);
    setCount(null);
    setPaused(false);
    setIdx(0);
    setRun((r) => r + 1);
  }

  const acts = useRef({});
  acts.current = { pickUp, drop, undo, restart, askHint, watchHint, showHint, pause, resume, leave, replay, again, goOn, saveByAd, saveByGems, endRun };
  // web, development only: the browser tests move squishies without finding the cushions on screen (src/web/README.md)
  if (__DEV__ && typeof window !== 'undefined' && window.document) {
    window.__stack = {
      best: () => bestMove(live.current.puzzle.sol, posRef.current),
      move: (from, to) => {
        const r = topOf(posRef.current, from);
        if (r < 0 || phaseRef.current !== 'play') return false;
        acts.current.pickUp(r, from);
        setTimeout(() => acts.current.drop(to), 60);
        return true;
      },
      state: () => ({ phase: phaseRef.current, pos: posRef.current.slice(), k: live.current.spec.k }),
    };
  }

  useEffect(() => {
    const sub = BackHandler.addEventListener('hardwareBackPress', () => {
      const A = acts.current;
      if (hintAskRef.current) setHintAsk(false);
      else if (pausedRef.current) A.resume();
      else if (phaseRef.current === 'play') A.pause();
      else if (blitzCardRef.current === 'save') A.endRun();
      else if (phaseRef.current === 'held') return true;
      else A.leave();
      return true;
    });
    return () => sub.remove();
  }, [pausedRef, phaseRef]);
  useEffect(() => {
    const sub = AppState.addEventListener('change', (s) => s !== 'active' && acts.current.pause());
    return () => sub.remove();
  }, []);

  // --- touch: tap a cushion to lift its top one, tap another to put it there; or drag ---

  const touch = useRef(null);
  const responder = useMemo(
    () =>
      PanResponder.create({
        onStartShouldSetPanResponder: () => phaseRef.current === 'play' && !pausedRef.current,
        onMoveShouldSetPanResponder: () => false,
        onPanResponderTerminationRequest: () => false,
        onPanResponderGrant: (e) => {
          const G = live.current.g;
          if (!G) return;
          const x = e.nativeEvent.pageX - origin.current.x;
          const y = e.nativeEvent.pageY - origin.current.y;
          const col = Math.max(0, Math.min(G.k - 1, Math.floor(x / G.colW)));
          touch.current = { x0: x, y0: y, moved: false, picked: false, dead: false };
          if (heldRef.current) return;
          const r = topOf(posRef.current, col);
          if (r < 0) {
            touch.current.dead = true;
            sfx.play('st_empty');
            bump(col, -1);
            return;
          }
          acts.current.pickUp(r, col);
          touch.current.picked = true;
        },
        onPanResponderMove: (e) => {
          const T = touch.current;
          const h = heldRef.current;
          const G = live.current.g;
          if (!T || T.dead || !h || !G) return;
          const x = e.nativeEvent.pageX - origin.current.x;
          const y = e.nativeEvent.pageY - origin.current.y;
          if (!T.moved) {
            if (Math.hypot(x - T.x0, y - T.y0) < 9) return;
            T.moved = true;
          }
          // held by its middle, a little above the finger so it can be seen
          const A = live.current.anims[h.r];
          const bw = G.bw[h.r];
          const bh = G.bh[h.r];
          A.tok += 1;
          A.tgt = null;
          A.x.stopAnimation();
          A.y.stopAnimation();
          A.x.setValue(Math.max(0, Math.min(G.w - bw, x - bw / 2)));
          A.y.setValue(Math.max(-bh * 0.3, Math.min(G.surface - bh, y - bh * 0.85)));
          const col = Math.max(0, Math.min(G.k - 1, Math.floor(x / G.colW)));
          if (col !== T.col) {
            T.col = col;
            setDragCol(col);
          }
        },
        onPanResponderRelease: (e) => {
          const T = touch.current;
          touch.current = null;
          const G = live.current.g;
          if (!T || T.dead || !heldRef.current || !G || phaseRef.current !== 'play') return;
          const x = e.nativeEvent.pageX - origin.current.x;
          const col = Math.max(0, Math.min(G.k - 1, Math.floor(x / G.colW)));
          if (T.moved) acts.current.drop(col, true);
          else if (!T.picked) acts.current.drop(col);
          // a tap that lifted it: it stays up for the next tap
        },
        onPanResponderTerminate: () => {
          const T = touch.current;
          touch.current = null;
          if (T && T.moved && heldRef.current) acts.current.drop(heldRef.current.from);
        },
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  );

  // --- drawing ---------------------------------------------------------------------------

  // (for the one render between a new puzzle and its reset, the old places
  // don't fit it)
  const at = pos.length === spec.n ? pos : puzzle.start;
  const zOf = {};
  for (let p = 0; p < spec.k; p++) stackOf(at, p).forEach((r, j) => (zOf[r] = 10 + j));
  const order = who.map((_, r) => r).sort((a, b) => (a === top ? 99 : zOf[a] || 0) - (b === top ? 99 : zOf[b] || 0));
  const nudge = spec.lv === 1 && moves === 0 && phase === 'play' ? (held ? puzzle.star : 0) : -1;
  const glowOf = (p) => (glow ? (p === glow.to ? 'gold' : p === glow.from ? 'white' : null) : p === nudge ? 'white' : null);
  const playing = phase === 'play' && !paused;
  // Blitz: the best run so far, and whether this one has beaten it
  const bestRun = record(profile).blitz.top;
  const beat = blitz && cleared > bestRun && bestRun > 0;

  // The counter (the user, 2026-11-16: "my attempt / attempts for 3 stars", no stars, no level, no game name, no
  // clock): the moves so far over the par, red once past it, and the level's tier. Blitz: the puzzles cleared and the
  // best run, and its one rule.
  const pastPar = moves > puzzle.par;
  const counterEl = (
    <View style={styles.counter}>
      {blitz ? (
        <>
          <Stat label="CLEARED" value={cleared} />
          <Stat label={beat ? 'NEW BEST' : 'BEST'} value={Math.max(bestRun, cleared)} gold={beat} />
        </>
      ) : (
        <View style={styles.stat}>
          <Text style={styles.statLabel}>MOVES</Text>
          <Text style={styles.countLine}>
            <Text style={[styles.countNum, pastPar && styles.countOver]}>{moves}</Text>
            <Text style={styles.countPar}>{` / ${puzzle.par}`}</Text>
          </Text>
        </View>
      )}
      <View style={[styles.challenge, { backgroundColor: TIER_COLORS[spec.tier] }]}>
        <Text style={styles.challengeText}>{TIER_NAMES[spec.tier]}</Text>
      </View>
      {blitz ? (
        <Chip bg="#e01e4a" color="#ffffff">
          1 MISTAKE = GAME OVER
        </Chip>
      ) : null}
    </View>
  );
  const backEl = <GameBack onPress={() => (phaseRef.current === 'play' ? acts.current.pause() : acts.current.leave())} />;
  const goalEl = <GoalCard who={who} goal={puzzle.goal} k={spec.k} star={puzzle.star} />;
  // upright: the back button and the counter along the top, the goal under them
  const topEl = (
    <>
      <View style={styles.hud}>
        {backEl}
        <View style={styles.titleBox} />
        {counterEl}
      </View>
      <View style={styles.info}>{goalEl}</View>
    </>
  );
  const areaEl = (
    <>
      {/* the cushions: everything is placed by geometry() */}
      <View ref={areaRef} style={styles.area} onLayout={onArea} {...responder.panHandlers}>
        {g ? (
          <>
            <View pointerEvents="none" style={[styles.rug, { height: g.cushH * 1.9, borderRadius: g.w }]} />
            {dragCol >= 0 ? <View pointerEvents="none" style={[styles.colGlow, { left: g.colW * dragCol + 4, width: g.colW - 8 }]} /> : null}
            {Array.from({ length: spec.k }, (_, p) => (
              <Seat key={p} i={p} x={cx(g, p)} top={g.surface - g.cushH * 0.4} w={g.cushW} h={g.cushH} star={puzzle.star === p} bump={bumps[p]} glow={glowOf(p)} />
            ))}
            {order.map((r) => (
              <Piece key={r} c={who[r]} w={g.bw[r]} h={g.bh[r]} a={anims[r]} z={r === top ? 99 : zOf[r] || 10} />
            ))}
            <Banner banner={banner} top={4} />
            <FxLayer ref={fx} />
          </>
        ) : null}
        {count != null ? <Countdown value={count} /> : null}
      </View>
    </>
  );
  // restart and hint (no UNDO: the user, 2026-11-16); sideways, side by side at the bottom right
  const ctrlEl = (
    <View style={[styles.controls, wide && styles.wControls]}>
      <SdButton kind="purple" onPress={() => acts.current.restart()} disabled={!playing || moves === 0} sound="sd_select" faceStyle={[styles.ctrlFace, wide && styles.wCtrlFace]} style={wide ? null : styles.ctrl}>
        <BtnLabel kind="purple" size={wide ? 14 : 15}>
          RESTART
        </BtnLabel>
      </SdButton>
      {!blitz ? (
        <SdButton kind="gold" onPress={() => acts.current.askHint()} disabled={!playing || (hinted && !adsFree && !ad.ready)} sound="sd_select" faceStyle={[styles.ctrlFace, wide && styles.wCtrlFace]} style={wide ? null : styles.ctrl}>
          <BtnLabel kind="gold" size={wide ? 14 : 15}>
            {adsFree ? 'HINT' : '▶ HINT'}
          </BtnLabel>
        </SdButton>
      ) : null}
    </View>
  );
  return (
    <View style={styles.fill}>
      {wide ? (
        // sideways (the user, 2026-11-16): the back button top left and the goal bottom left; the counter at the top in
        // the middle, the cushions under it; RESTART and HINT side by side at the bottom right. The two side columns are
        // as wide as each other, so the cushions sit in the middle of the screen.
        <View style={styles.wRow}>
          <View style={styles.wSide}>
            {backEl}
            <View style={styles.fill} />
            {goalEl}
          </View>
          <View style={styles.fill}>
            <View style={styles.wCounterRow}>{counterEl}</View>
            {areaEl}
          </View>
          <View style={[styles.wSide, styles.wSideR]}>{ctrlEl}</View>
        </View>
      ) : (
        <>
          {topEl}
          {areaEl}
          {ctrlEl}
        </>
      )}

      {blitz ? (
        <>
          <BlitzPauseCard blitz visible={paused} onResume={() => acts.current.resume()} onRestart={() => acts.current.again()} onQuit={() => acts.current.leave()} />
          <BlitzSaveCard
            visible={blitzCard === 'save'}
            unit="puzzle"
            reason={failReason}
            board={idx + 1}
            cleared={cleared}
            adUsed={adUsed}
            adReady={ad.ready}
            adsFree={adsFree}
            gems={Number(profile && profile.gems) || 0}
            price={blitzSaveGems(gemSaves)}
            saving={saving}
            error={saveErr}
            onAd={() => acts.current.saveByAd()}
            onGems={() => acts.current.saveByGems()}
            onEnd={() => acts.current.endRun()}
          />
          <BlitzOverCard visible={blitzCard === 'over'} unit="puzzle" res={over} onAgain={() => acts.current.again()} onQuit={() => acts.current.leave()} />
          <FxLayer ref={fxTop} style={styles.fxTop} />
        </>
      ) : (
        <>
          <ResultCard visible={!!result} res={result} spec={spec} last={lv >= LEVELS} onNext={next} onReplay={replay} onHub={() => acts.current.leave()} />
          <PauseCard
            visible={paused}
            onResume={() => acts.current.resume()}
            onRestart={() => {
              setPaused(false);
              replay();
            }}
            onQuit={() => acts.current.leave()}
          />
          <HintCard visible={hintAsk} adReady={ad.ready} adsFree={adsFree} onWatch={() => acts.current.watchHint()} onClose={() => setHintAsk(false)} />
        </>
      )}
      {phase === 'load' ? (
        <PopIn style={styles.loading}>
          <Text style={styles.loadingText}>Fluffing the cushions…</Text>
        </PopIn>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  hud: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 14, paddingTop: 6 },
  titleBox: { flex: 1, minWidth: 0 },
  challenge: { paddingHorizontal: 7, paddingVertical: 1, borderRadius: 999, borderWidth: 2, borderColor: '#ffffff' },
  challengeText: { fontFamily: F.display, fontSize: 10, letterSpacing: 0.6, color: '#ffffff' },
  info: { flexDirection: 'row', alignItems: 'stretch', gap: 10, paddingHorizontal: 14, marginTop: 8, minHeight: 72 },
  gold: { color: '#ffd23a' },
  // the cards sit at elevation 50 (squishdoku/Overlays.js)
  fxTop: { zIndex: 60, elevation: 60 },
  area: { flex: 1 },
  // landscape (REDESIGN_V8)
  wRow: { flex: 1, flexDirection: 'row' },
  wSide: { width: 150 },
  // the counter (both ways up): MOVES n / par, the tier; Blitz: cleared, best, the rule
  counter: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 12, paddingVertical: 3, borderRadius: 16, backgroundColor: 'rgba(91,58,41,0.75)', borderWidth: 2, borderColor: 'rgba(255,255,255,0.9)' },
  stat: { alignItems: 'center', minWidth: 44 },
  statLabel: { fontFamily: F.display, fontSize: 9.5, letterSpacing: 1, color: '#ffe6c2', includeFontPadding: false },
  statNum: { fontFamily: F.display, fontSize: 21, color: '#ffe045', includeFontPadding: false, fontVariant: ['tabular-nums'] },
  countLine: { includeFontPadding: false },
  countNum: { fontFamily: F.display, fontSize: 22, color: '#ffe045', fontVariant: ['tabular-nums'] },
  countOver: { color: '#ff8fa3' },
  countPar: { fontFamily: F.display, fontSize: 16, color: '#ffe6c2', fontVariant: ['tabular-nums'] },
  // sideways: two side columns of the same width, the counter and the cushions between them
  wSide: { width: 200, paddingHorizontal: 12, paddingTop: 6, paddingBottom: 10, alignItems: 'flex-start' },
  wSideR: { alignItems: 'flex-end', justifyContent: 'flex-end' },
  wCounterRow: { alignItems: 'center', paddingTop: 6 },
  wCtrlFace: { paddingHorizontal: 10, paddingVertical: 10 },
  wControls: { flexDirection: 'row', alignItems: 'flex-end', paddingHorizontal: 0, paddingTop: 0, paddingBottom: 0, gap: 8 },
  // (no dashed outline: the user, 2026-11-16)
  rug: { position: 'absolute', left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(255,255,255,0.16)' },
  colGlow: { position: 'absolute', top: 0, bottom: 0, borderRadius: 26, backgroundColor: 'rgba(255,255,255,0.16)' },
  seat: { position: 'absolute' },
  halo: { position: 'absolute', left: -10, right: -10, top: -9, bottom: -6 },
  badge: { position: 'absolute' },
  piece: { position: 'absolute', left: 0, top: 0 },
  controls: { flexDirection: 'row', justifyContent: 'center', gap: 8, paddingHorizontal: 12, paddingTop: 4, paddingBottom: 10 },
  ctrl: { flex: 1, maxWidth: 130 },
  ctrlFace: { paddingHorizontal: 6, paddingVertical: 10 },
  loading: { position: 'absolute', alignSelf: 'center', top: '45%' },
  loadingText: { fontFamily: F.display, fontSize: 16, color: INK },
});
