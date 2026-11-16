// "To the treasure vault" (REDESIGN_V9 step 7, 2026-11-16: the user's Squish-Squad-assets.zip, its loading screen
// for going to open a chest): over a chest smash while its page loads. A treasure map on the wood table; a red dotted
// trail draws itself from Home towards the X as the bar fills, the player's squishy hopping along it in a gold coin;
// once the smash has painted (`ready`: ChestSmash's onShown) the bar fills, the map zooms into the X in a golden
// glow, and the screen fades off the chest. Shown for at least MIN_MS, so it reads even when the page is quick.
//
// The table, the map and the bar's parts are pictures (webgame/tools/vault-art.mjs draws them once from the
// recording's stills); only the dots, the coin, the bar and the zoom move, on the native driver. Laid out in the
// recording's frame (639 x 295), scaled to fit the screen.
import React, { memo, useEffect, useMemo, useRef, useState } from 'react';
import { Animated, Easing, Image, StyleSheet, Text, View, useWindowDimensions } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { Lettering } from '../royal';
import { FONT } from '../royal/theme';
import sfx from '../audio/sfx';
import { selectedId } from '../squad/lobby';
import { BAR, FRAME, MAP, MAP_PAD, TRAIL, X } from './vaultArt';

const TABLE = require('../../assets/royal/vault-table.jpg');
const MAP_ART = require('../../assets/royal/vault-map.webp');
const FILL = require('../../assets/royal/vault-fill.png');
const KEY = require('../../assets/royal/vault-key.png');
const GLOW = require('../../assets/royal/vault-glow.png');

const MIN_MS = 1600; // on screen at least this long
const CREEP_MS = 5200; // the bar's creep towards 90% while the page loads
const TIPS = [
  'The lucky meter guarantees an Epic or better at least every 10 chests',
  'Rarities: Common, Rare, Epic, Legendary, Rainbow',
  'Smash a Common or Rare chest hard: its first hit can upgrade it',
  'Collect a whole group to earn its Rainbow chest',
];
const ZOOM = 2.6; // how far the map zooms into the X on the way out
const COIN = 30; // the creature's coin, frame units
const N = TRAIL.length;
// the trail's dots show as the bar passes them (the last one just before 100%)
const AT = TRAIL.map((_, i) => (i / (N - 1)) * 0.985);

// The picture of the player's squishy (the one picked in the Lobby / on the Menu) for the coin
export function buddyUriOf(creatures, profile) {
  const id = String(selectedId(profile) || '');
  const c = (creatures || []).find((x) => String(x.id) === id);
  return c && c.thumb ? c.thumb.url : null;
}

// The player's squishy in its gold coin (a paw when there's no picture yet)
const Coin = memo(function Coin({ uri, size }) {
  return (
    <LinearGradient colors={['#fff1b0', '#f2c24a', '#c98a1c']} style={[styles.coin, { width: size, height: size, borderRadius: size / 2, padding: size * 0.09 }]}>
      <View style={[styles.coinFace, { borderRadius: size / 2 }]}>
        {uri ? <Image source={{ uri }} style={styles.coinPic} resizeMode="contain" fadeDuration={0} /> : <Text style={[styles.paw, { fontSize: size * 0.42 }]}>🐾</Text>}
      </View>
    </LinearGradient>
  );
});

function VaultLoader({ ready, uri, onDone }) {
  const { width: W, height: H } = useWindowDimensions();
  const s = Math.min(W / FRAME.w, H / FRAME.h);
  const ox = (W - FRAME.w * s) / 2;
  const oy = (H - FRAME.h * s) / 2;
  const fx = (u) => ox + u * s;
  const fy = (u) => oy + u * s;

  const prog = useRef(new Animated.Value(0)).current;
  const zoom = useRef(new Animated.Value(0)).current;
  const out = useRef(new Animated.Value(0)).current;
  const hop = useRef(new Animated.Value(0)).current;
  const flick = useRef(new Animated.Value(0)).current;
  const [pct, setPct] = useState(0);
  const [tip, setTip] = useState(() => Math.floor(Math.random() * TIPS.length));
  const tipFade = useRef(new Animated.Value(1)).current;
  const t0 = useRef(Date.now()).current;
  const leaving = useRef(false);
  const doneRef = useRef(onDone);
  doneRef.current = onDone;

  // in, the creep, the coin's hops, the candle
  useEffect(() => {
    Animated.timing(out, { toValue: 1, duration: 180, useNativeDriver: true }).start();
    const creep = Animated.timing(prog, { toValue: 0.9, duration: CREEP_MS, easing: Easing.out(Easing.cubic), useNativeDriver: true });
    creep.start();
    const hops = Animated.loop(
      Animated.sequence([
        Animated.timing(hop, { toValue: 1, duration: 170, easing: Easing.out(Easing.quad), useNativeDriver: true }),
        Animated.timing(hop, { toValue: 0, duration: 170, easing: Easing.in(Easing.quad), useNativeDriver: true }),
      ]),
    );
    hops.start();
    const candle = Animated.loop(
      Animated.sequence([
        Animated.timing(flick, { toValue: 1, duration: 420, easing: Easing.inOut(Easing.sin), useNativeDriver: true }),
        Animated.timing(flick, { toValue: 0, duration: 560, easing: Easing.inOut(Easing.sin), useNativeDriver: true }),
      ]),
    );
    candle.start();
    const id = prog.addListener(({ value }) => setPct((p) => (Math.round(value * 100) !== p ? Math.round(value * 100) : p)));
    const tips = setInterval(() => {
      Animated.timing(tipFade, { toValue: 0, duration: 220, useNativeDriver: true }).start(() => {
        setTip((i) => (i + 1) % TIPS.length);
        Animated.timing(tipFade, { toValue: 1, duration: 260, useNativeDriver: true }).start();
      });
    }, 2800);
    return () => {
      creep.stop();
      hops.stop();
      candle.stop();
      prog.removeListener(id);
      clearInterval(tips);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ready (and on long enough): the bar fills, the map zooms into the X, the screen fades off the chest
  useEffect(() => {
    if (!ready || leaving.current) return undefined;
    const go = () => {
      if (leaving.current) return;
      leaving.current = true;
      prog.stopAnimation();
      Animated.timing(prog, { toValue: 1, duration: 260, easing: Easing.out(Easing.quad), useNativeDriver: true }).start(() => {
        sfx.play('swoosh');
        Animated.parallel([
          Animated.timing(zoom, { toValue: 1, duration: 620, easing: Easing.in(Easing.cubic), useNativeDriver: true }),
          Animated.sequence([Animated.delay(430), Animated.timing(out, { toValue: 0, duration: 240, useNativeDriver: true })]),
        ]).start(() => doneRef.current && doneRef.current());
      });
    };
    const wait = MIN_MS - (Date.now() - t0);
    if (wait <= 0) {
      go();
      return undefined;
    }
    const t = setTimeout(go, wait);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready]);

  // the map's box on the screen (its picture has MAP_PAD of torn edge round it) and the X in it
  const mw = (MAP.w + MAP_PAD * 2) * s;
  const mh = (MAP.h + MAP_PAD * 2) * s;
  const mx = (u) => (MAP_PAD + u) * s; // map units -> the map box's own
  const xX = mx(X[0]);
  const xY = mx(X[1]);
  // zooming about the X: a scale about the box's middle, moved back so the X stays where it is
  const mapMove = useMemo(
    () => [
      { translateX: zoom.interpolate({ inputRange: [0, 1], outputRange: [0, (1 - ZOOM) * (xX - mw / 2)] }) },
      { translateY: zoom.interpolate({ inputRange: [0, 1], outputRange: [0, (1 - ZOOM) * (xY - mh / 2)] }) },
      { scale: zoom.interpolate({ inputRange: [0, 1], outputRange: [1, ZOOM] }) },
    ],
    [zoom, xX, xY, mw, mh],
  );
  // the coin along the trail, hopping
  const coinMove = useMemo(() => {
    const cs = COIN * s;
    const tx = prog.interpolate({ inputRange: AT, outputRange: TRAIL.map(([x]) => mx(x) - cs / 2), extrapolate: 'clamp' });
    const ty = prog.interpolate({ inputRange: AT, outputRange: TRAIL.map(([, y]) => mx(y) - cs * 0.62), extrapolate: 'clamp' });
    return [{ translateX: tx }, { translateY: Animated.add(ty, hop.interpolate({ inputRange: [0, 1], outputRange: [0, -5 * s] })) }];
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [prog, hop, s]);
  const dots = useMemo(
    () =>
      TRAIL.map(([x, y], i) => (
        <Animated.View
          key={i}
          style={[
            styles.dot,
            { left: mx(x) - 1.8 * s, top: mx(y) - 1.8 * s, width: 3.6 * s, height: 3.6 * s, borderRadius: 1.8 * s },
            { opacity: prog.interpolate({ inputRange: [Math.max(0, AT[i] - 0.012), AT[i] + 0.0001], outputRange: [0, 1], extrapolate: 'clamp' }) },
          ]}
        />
      )),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [prog, s],
  );

  // the bar: its fill slides in from the left inside the groove, the key riding its end
  const inset = 3 * s;
  const innerW = BAR.w * s - inset * 2;
  const innerH = BAR.h * s - inset * 2;
  const fillMove = [{ translateX: prog.interpolate({ inputRange: [0, 1], outputRange: [-innerW, 0] }) }];
  const keyMove = [{ translateX: prog.interpolate({ inputRange: [0, 1], outputRange: [0, innerW] }) }];
  const glowOp = zoom.interpolate({ inputRange: [0, 0.35, 1], outputRange: [0, 0.85, 1] });
  const glowSize = 150 * s;

  return (
    <Animated.View style={[StyleSheet.absoluteFill, styles.root, { opacity: out }]} pointerEvents="auto">
      <Image source={TABLE} resizeMode="cover" style={[StyleSheet.absoluteFill, styles.full]} fadeDuration={0} />
      {/* the candle's light */}
      <Animated.Image
        source={GLOW}
        fadeDuration={0}
        style={[styles.abs, { left: fx(48) - 34 * s, top: fy(22) - 34 * s, width: 68 * s, height: 68 * s, opacity: flick.interpolate({ inputRange: [0, 1], outputRange: [0.28, 0.5] }) }]}
      />
      {/* the map, its trail and the coin, zooming as one */}
      <Animated.View style={[styles.abs, { left: fx(MAP.x - MAP_PAD), top: fy(MAP.y - MAP_PAD), width: mw, height: mh, transform: mapMove }]}>
        <Image source={MAP_ART} style={styles.full} resizeMode="stretch" fadeDuration={0} />
        {dots}
        <Animated.Image source={GLOW} fadeDuration={0} style={[styles.abs, { left: xX - glowSize / 2, top: xY - glowSize / 2, width: glowSize, height: glowSize, opacity: glowOp }]} />
        <Animated.View style={[styles.abs, { left: 0, top: 0, transform: coinMove }]}>
          <Coin uri={uri} size={COIN * s} />
        </Animated.View>
      </Animated.View>
      {/* the title */}
      <View style={[styles.abs, styles.center, { left: 0, right: 0, top: fy(10) }]} pointerEvents="none">
        <Lettering text="To the treasure vault" variant="gold" size={Math.round(25 * s)} />
      </View>
      {/* the bar */}
      <View style={[styles.abs, styles.bar, { left: fx(BAR.x), top: fy(BAR.y), width: BAR.w * s, height: BAR.h * s, borderRadius: (BAR.h * s) / 2, padding: inset - 1.5 * s }]}>
        <View style={[styles.groove, { borderRadius: innerH / 2 }]}>
          <Animated.Image source={FILL} resizeMode="stretch" fadeDuration={0} style={{ width: innerW, height: innerH, borderRadius: innerH / 2, transform: fillMove }} />
        </View>
      </View>
      <Animated.Image source={KEY} fadeDuration={0} style={[styles.abs, { left: fx(BAR.x) + inset - 14 * s, top: fy(BAR.y + BAR.h / 2) - 14 * s, width: 28 * s, height: 28 * s, transform: keyMove }]} />
      <Text style={[styles.abs, styles.pct, { left: fx(BAR.x + BAR.w + 9), top: fy(BAR.y + BAR.h / 2) - 9 * s, fontSize: 11.5 * s, lineHeight: 18 * s }]}>{`${pct}%`}</Text>
      <Animated.Text numberOfLines={1} style={[styles.abs, styles.tip, { left: 0, right: 0, top: fy(272), fontSize: 9.6 * s, opacity: tipFade }]}>
        <Text style={styles.tipHead}>Tip: </Text>
        {TIPS[tip]}
      </Animated.Text>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  root: { backgroundColor: '#8a5a32', zIndex: 80, elevation: 80, overflow: 'hidden' },
  // (width and height too: on web an Image without them takes the picture's own size)
  full: { width: '100%', height: '100%' },
  abs: { position: 'absolute' },
  center: { alignItems: 'center' },
  dot: { position: 'absolute', backgroundColor: '#d0402e' },
  coin: { alignItems: 'stretch', justifyContent: 'center', shadowColor: '#3a1e08', shadowOpacity: 0.45, shadowRadius: 3, shadowOffset: { width: 0, height: 2 }, elevation: 3, borderWidth: 1, borderColor: '#8a5a1a' },
  coinFace: { flex: 1, backgroundColor: '#fff6e2', overflow: 'hidden', alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: '#b8801c' },
  coinPic: { width: '96%', height: '96%' },
  paw: { textAlign: 'center' },
  bar: { backgroundColor: '#fff1c4', borderWidth: 1.5, borderColor: '#6b4426', shadowColor: '#2a1404', shadowOpacity: 0.5, shadowRadius: 4, shadowOffset: { width: 0, height: 2 }, elevation: 4 },
  groove: { flex: 1, backgroundColor: '#7a4a22', overflow: 'hidden', borderWidth: 1, borderColor: '#5e3a1c' },
  pct: { fontFamily: FONT.display, color: '#fff6e2', textShadowColor: '#3a1e08', textShadowOffset: { width: 0, height: 1.5 }, textShadowRadius: 1 },
  tip: { textAlign: 'center', fontFamily: FONT.bodyHeavy, color: '#fff6e2', textShadowColor: '#3a1e08', textShadowOffset: { width: 0, height: 1 }, textShadowRadius: 1 },
  tipHead: { color: '#ffd56b' },
});

export default memo(VaultLoader);
