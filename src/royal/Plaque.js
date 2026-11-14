import React, { memo, useEffect, useRef, useState } from 'react';
import { Animated, Easing, Pressable, StyleSheet, View } from 'react-native';
import Svg, { Circle, Defs, G, LinearGradient, Pattern, RadialGradient, Rect, Stop } from 'react-native-svg';
import { LinearGradient as LG } from 'expo-linear-gradient';
import { INK, PLAQUES, TIP } from './theme';

// The menu's plaque (main-menu.js .mm-p + .v-*): a fill inside a gold rim, a dark ink outline, a 3D lip under it, a
// shine along the top, a soft shade at the bottom, and optional diamond tips at both ends (.tip). The same numbers as
// the CSS: rim 2.5, outline 1.6, lip 3.
//
// variant: vel | gold | bronze | wood | amy | pearl | rainbow (theme.js PLAQUES)
// radius: the corner radius (999 = a pill); style: the box (width / height / padding / layout of the children)
// tips: the diamond tips; onPress: a button (it sinks 2 on press, as :active does)

let UID = 0;
const stopsOf = (stops) => stops.map(([o, c, a], i) => <Stop key={i} offset={o} stopColor={c} stopOpacity={a == null ? 1 : a} />);
// a CSS angle (0 = up, 90 = right) as the x1 / y1 / x2 / y2 of an SVG gradient over the box
function line(angle) {
  const r = (angle * Math.PI) / 180, dx = Math.sin(r), dy = -Math.cos(r);
  return { x1: 0.5 - dx / 2, y1: 0.5 - dy / 2, x2: 0.5 + dx / 2, y2: 0.5 + dy / 2 };
}

// The fill: one SVG (gradient, patterns, gloss, sparkles, the inner shine and shade). Exported for other shapes.
export const Fill = memo(function Fill({ variant = 'vel', top = true, shade = true }) {
  const P = PLAQUES[variant] || PLAQUES.vel;
  const [id] = useState(() => 'pq' + ++UID);
  const F = P.fill;
  return (
    <Svg style={StyleSheet.absoluteFill} width="100%" height="100%">
      <Defs>
        {F.radial ? (
          <RadialGradient id={id + 'f'} cx={F.radial.cx} cy={F.radial.cy} rx={F.radial.rx} ry={F.radial.ry} fx={F.radial.cx} fy={F.radial.cy} gradientUnits="objectBoundingBox">
            {stopsOf(F.stops)}
          </RadialGradient>
        ) : (
          <LinearGradient id={id + 'f'} {...line(F.angle)}>
            {stopsOf(F.stops)}
          </LinearGradient>
        )}
        {P.gloss ? (
          <LinearGradient id={id + 'g'} x1="0" y1="0" x2="0" y2="1">
            <Stop offset="0" stopColor="#ffffff" stopOpacity={P.gloss} />
            <Stop offset="0.46" stopColor="#ffffff" stopOpacity={0} />
          </LinearGradient>
        ) : null}
        {P.diamonds ? (
          <>
            <Pattern id={id + 'a'} patternUnits="userSpaceOnUse" width="12" height="12" patternTransform="rotate(45)">
              <Rect x="0" y="0" width="6" height="12" fill="#ffffff" fillOpacity={0.055} />
            </Pattern>
            <Pattern id={id + 'b'} patternUnits="userSpaceOnUse" width="12" height="12" patternTransform="rotate(-45)">
              <Rect x="0" y="0" width="6" height="12" fill="#ffffff" fillOpacity={0.055} />
            </Pattern>
          </>
        ) : null}
        {P.stripes ? (
          <Pattern id={id + 's'} patternUnits="userSpaceOnUse" width="11" height="4">
            <Rect x="0" y="0" width="2" height="4" fill="#50280a" fillOpacity={0.1} />
          </Pattern>
        ) : null}
        {shade ? (
          <LinearGradient id={id + 'd'} x1="0" y1="0" x2="0" y2="1">
            <Stop offset="0" stopColor="#14003c" stopOpacity={0} />
            <Stop offset="1" stopColor="#14003c" stopOpacity={0.35} />
          </LinearGradient>
        ) : null}
      </Defs>
      <Rect x="0" y="0" width="100%" height="100%" fill={`url(#${id}f)`} />
      {P.stripes ? <Rect x="0" y="0" width="100%" height="100%" fill={`url(#${id}s)`} /> : null}
      {P.diamonds ? (
        <G>
          <Rect x="0" y="0" width="100%" height="100%" fill={`url(#${id}a)`} />
          <Rect x="0" y="0" width="100%" height="100%" fill={`url(#${id}b)`} />
        </G>
      ) : null}
      {P.gloss ? <Rect x="0" y="0" width="100%" height="100%" fill={`url(#${id}g)`} /> : null}
      {P.sparkles ? (
        <G>
          <Circle cx="14%" cy="28%" r="1.2" fill="#ffffff" fillOpacity={0.95} />
          <Circle cx="63%" cy="72%" r="1.1" fill="#ffffff" fillOpacity={0.85} />
          <Circle cx="86%" cy="22%" r="1.3" fill="#fff6c8" fillOpacity={0.9} />
          <Circle cx="36%" cy="80%" r="1" fill="#ffffff" fillOpacity={0.7} />
        </G>
      ) : null}
      {shade ? <Rect x="0" y="85%" width="100%" height="15%" fill={`url(#${id}d)`} /> : null}
      {top ? <Rect x="0" y="0" width="100%" height="1.5" fill="#ffffff" fillOpacity={0.55} /> : null}
    </Svg>
  );
});

// a diamond tip (.mm-p.tip::before / ::after)
function Tip({ side }) {
  return (
    <View pointerEvents="none" style={[styles.tip, side === 'l' ? styles.tipL : styles.tipR]}>
      <LG colors={TIP.map((s) => s[1])} locations={TIP.map((s) => s[0])} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={styles.tipIn}>
        <View style={styles.tipShine} />
      </LG>
    </View>
  );
}

// A gentle gold glow behind a button, pulsing (@keyframes mmGlow)
export function Glow({ radius = 16, spread = 10 }) {
  const t = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(t, { toValue: 1, duration: 1200, easing: Easing.inOut(Easing.sin), useNativeDriver: true }),
        Animated.timing(t, { toValue: 0, duration: 1200, easing: Easing.inOut(Easing.sin), useNativeDriver: true }),
      ])
    );
    loop.start();
    return () => loop.stop();
  }, [t]);
  const opacity = t.interpolate({ inputRange: [0, 1], outputRange: [0.35, 1] });
  return (
    <Animated.View pointerEvents="none" style={[StyleSheet.absoluteFill, { top: -spread, bottom: -spread, left: -spread, right: -spread, opacity }]}>
      <Svg width="100%" height="100%">
        <Defs>
          <RadialGradient id="royalGlow" cx="50%" cy="50%" rx="50%" ry="50%">
            <Stop offset="0.55" stopColor="#ffd66e" stopOpacity={0.9} />
            <Stop offset="1" stopColor="#ffd66e" stopOpacity={0} />
          </RadialGradient>
        </Defs>
        <Rect x="0" y="0" width="100%" height="100%" rx={radius + spread} fill="url(#royalGlow)" />
      </Svg>
    </Animated.View>
  );
}

function Plaque({ variant = 'vel', radius = 999, rim = 2.5, ring = 1.6, lip = 3, tips = false, glow = false, style, children, onPress, disabled, hitSlop }) {
  const P = PLAQUES[variant] || PLAQUES.vel;
  const inner = Math.max(0, radius - rim);
  const body = (pressed) => (
    <>
      {glow ? <Glow radius={Math.min(radius, 40)} /> : null}
      {/* the lip, the outline, the rim, the fill */}
      <View pointerEvents="none" style={[styles.abs, { top: -ring + lip, bottom: -ring - lip, left: -ring, right: -ring, borderRadius: radius + ring, backgroundColor: INK }]} />
      <View pointerEvents="none" style={[styles.abs, { top: -ring, bottom: -ring, left: -ring, right: -ring, borderRadius: radius + ring, backgroundColor: INK }]} />
      <LG pointerEvents="none" colors={P.rim.map((s) => s[1])} locations={P.rim.map((s) => s[0])} style={[styles.abs, styles.fill0, { borderRadius: radius }]} />
      <View pointerEvents="none" style={[styles.abs, { top: rim, bottom: rim, left: rim, right: rim, borderRadius: inner, overflow: 'hidden' }]}>
        <Fill variant={variant} />
      </View>
      {tips ? <Tip side="l" /> : null}
      {tips ? <Tip side="r" /> : null}
      {typeof children === 'function' ? children(pressed) : children}
    </>
  );
  if (!onPress) return <View style={[styles.box, style]}>{body(false)}</View>;
  return (
    <Pressable onPress={onPress} disabled={disabled} hitSlop={hitSlop} style={({ pressed }) => [styles.box, style, pressed && !disabled && styles.down]}>
      {({ pressed }) => body(pressed)}
    </Pressable>
  );
}

export default memo(Plaque);

const styles = StyleSheet.create({
  box: { position: 'relative', alignItems: 'center', justifyContent: 'center' },
  abs: { position: 'absolute' },
  fill0: { top: 0, bottom: 0, left: 0, right: 0 },
  down: { transform: [{ translateY: 2 }] },
  tip: { position: 'absolute', top: '50%', width: 11, height: 11, marginTop: -5.5, backgroundColor: INK, padding: 1.5, transform: [{ rotate: '45deg' }] },
  tipL: { left: -6 },   // (the CSS: 7 left of the padding edge, inside a 2.5 rim)
  tipR: { right: -6 },
  tipIn: { flex: 1 },
  tipShine: { position: 'absolute', left: 0, top: 0, width: 3, height: 3, backgroundColor: 'rgba(255,255,255,0.8)' },
});
