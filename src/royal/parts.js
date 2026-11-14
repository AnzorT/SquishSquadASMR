import React, { memo, useEffect, useRef, useState } from 'react';
import { Animated, Easing, Image, Pressable, StyleSheet, Text, View, useWindowDimensions } from 'react-native';
import Svg, { Circle, Defs, G, LinearGradient, Path, Pattern, RadialGradient, Rect, Stop } from 'react-native-svg';
import ShadowText from '../components/candy/ShadowText';
import { COIN, GEM } from '../squad/currencyArt';
import Plaque from './Plaque';
import Lettering from './Lettering';
import { FONT, ICON, INK, ORB, PAGE, RIM, TX_W } from './theme';

// The rest of the menu's kit (main-menu.js): the jewel orb buttons, the coin / gem pills, the gold PLAY-style button,
// the full-screen page (background + header), and the Stage that scales the design's 393-tall units to the phone.

let UID = 0;
const fmt = (n) => (n == null ? '' : String(Math.round(n)).replace(/\B(?=(\d{3})+(?!\d))/g, ','));

// The design's units on the phone: the menu scales by k = screen height / 393 (main-menu.js layout()).
export function useK() {
  const { width, height } = useWindowDimensions();
  return Math.min(width, height) / 393;
}

// A landscape page in design units: 393 tall, as wide as the screen allows, scaled up to the phone (.mm-pg).
export function Stage({ children, style }) {
  const { width, height } = useWindowDimensions();
  const H = Math.min(width, height), W = Math.max(width, height), k = H / 393;
  return (
    <View style={[StyleSheet.absoluteFill, style]}>
      <View style={{ position: 'absolute', left: 0, top: 0, width: W / k, height: 393, transform: [{ scale: k }], transformOrigin: 'left top' }}>{children}</View>
    </View>
  );
}

// a 24 x 24 stroke icon with the menu's ink drop under it
export function Icon({ d, size = 20, color = '#ffffff', width = 2.5, drop = true }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24">
      {drop ? <Path d={d} stroke={INK} strokeWidth={width} fill="none" strokeLinecap="round" strokeLinejoin="round" translateY={1.4} /> : null}
      <Path d={d} stroke={color} strokeWidth={width} fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </Svg>
  );
}

// The jewel orb (.mm-orb): 40 across, a 3 gold rim, the ink outline and lip. icon: an ICON key or a path.
export const Orb = memo(function Orb({ icon = 'back', size = 40, onPress, iconWidth = 3, style, children, disabled }) {
  const [id] = useState(() => 'orb' + ++UID);
  const d = ICON[icon] || icon;
  const ring = 1.6, lip = 3, rim = 3;
  return (
    <Pressable onPress={onPress} disabled={disabled} hitSlop={8} style={({ pressed }) => [{ width: size, height: size }, style, pressed && { transform: [{ translateY: 2 }] }]}>
      <View pointerEvents="none" style={{ position: 'absolute', left: -ring, top: -ring + lip, width: size + ring * 2, height: size + ring * 2, borderRadius: size, backgroundColor: INK }} />
      <View pointerEvents="none" style={{ position: 'absolute', left: -ring, top: -ring, width: size + ring * 2, height: size + ring * 2, borderRadius: size, backgroundColor: INK }} />
      <Svg width={size} height={size} style={StyleSheet.absoluteFill}>
        <Defs>
          <LinearGradient id={id + 'r'} x1="0" y1="0" x2="0" y2="1">
            {RIM.map(([o, c], i) => <Stop key={i} offset={o} stopColor={c} />)}
          </LinearGradient>
          <RadialGradient id={id + 'f'} cx="34%" cy="28%" rx="98%" ry="98%" fx="34%" fy="28%">
            {ORB.map(([o, c], i) => <Stop key={i} offset={o} stopColor={c} />)}
          </RadialGradient>
        </Defs>
        <Circle cx={size / 2} cy={size / 2} r={size / 2} fill={`url(#${id}r)`} />
        <Circle cx={size / 2} cy={size / 2} r={size / 2 - rim} fill={`url(#${id}f)`} />
      </Svg>
      <View pointerEvents="none" style={[StyleSheet.absoluteFill, styles.center]}>
        {children || <Icon d={d} size={size / 2} width={iconWidth} />}
      </View>
    </Pressable>
  );
});

// White text with the ink outline (.tx-w)
export function InkText({ children, size = 13.5, style, font = FONT.display, numberOfLines }) {
  return (
    <ShadowText style={[{ color: '#ffffff', fontFamily: font, fontSize: size, includeFontPadding: false }, style]} shadows={TX_W} numberOfLines={numberOfLines}>
      {children}
    </ShadowText>
  );
}

// The coin / gem pill (.mm-pill): a velvet pill with tips, the currency picture hanging off its left end.
export const Pill = memo(function Pill({ cur = 'coins', amount, onPress, style }) {
  return (
    <Plaque variant="vel" tips onPress={onPress} style={[styles.pill, style]}>
      <Image source={cur === 'gems' ? GEM[0] : COIN[0]} style={styles.pillImg} />
      <InkText size={13.5}>{fmt(amount)}</InkText>
    </Plaque>
  );
});

// A sweep of light across a button now and then (the menu's PLAY .sq-glint)
function Glint({ width }) {
  const t = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    const loop = Animated.loop(Animated.sequence([Animated.delay(1900), Animated.timing(t, { toValue: 1, duration: 1000, easing: Easing.inOut(Easing.quad), useNativeDriver: true }), Animated.timing(t, { toValue: 0, duration: 0, useNativeDriver: true }), Animated.delay(500)]));
    loop.start();
    return () => loop.stop();
  }, [t]);
  const translateX = t.interpolate({ inputRange: [0, 1], outputRange: [-width * 0.5, width * 1.3] });
  return (
    <View pointerEvents="none" style={[StyleSheet.absoluteFill, { overflow: 'hidden', borderRadius: 18 }]}>
      <Animated.View style={{ position: 'absolute', top: -10, bottom: -10, width: width * 0.26, transform: [{ translateX }, { skewX: '-18deg' }] }}>
        <Svg width="100%" height="100%">
          <Defs>
            <LinearGradient id="glintG" x1="0" y1="0" x2="1" y2="0">
              <Stop offset="0" stopColor="#ffffff" stopOpacity={0} />
              <Stop offset="0.5" stopColor="#ffffff" stopOpacity={0.8} />
              <Stop offset="1" stopColor="#ffffff" stopOpacity={0} />
            </LinearGradient>
          </Defs>
          <Rect x="0" y="0" width="100%" height="100%" fill="url(#glintG)" />
        </Svg>
      </Animated.View>
    </View>
  );
}

// The gold button (PLAY, CLAIM, CHOOSE …): a gold plaque with tips and the menu's white-and-brown lettering.
// variant 'vel' for the quiet one. glow: the pulsing halo (mmGlow); glint: the sweep of light.
export function Btn({ label, size = 20, width, height = 42, radius = 14, variant = 'gold', glow = false, glint = false, onPress, disabled, style, children, tips = true }) {
  return (
    <Plaque variant={variant} tips={tips} radius={radius} glow={glow} onPress={onPress} disabled={disabled} style={[{ width, height }, disabled && { opacity: 0.55 }, style]}>
      {glint && width ? <Glint width={width} /> : null}
      {children || <Lettering text={label} variant={variant === 'gold' ? 'whiteB' : 'white'} size={size} />}
    </Plaque>
  );
}

// The full-screen page's background (.mm-page2): deep purple, faint diamonds, soft rays from above.
export const PageBg = memo(function PageBg({ style }) {
  const { width, height } = useWindowDimensions();
  const W = Math.max(width, height), H = Math.min(width, height);
  const ox = W / 2, oy = -0.1 * H, R = W + H;
  const rays = [];
  for (let a = 0; a < 360; a += 18) {
    const a0 = ((a - 90) * Math.PI) / 180, a1 = ((a + 6 - 90) * Math.PI) / 180;
    rays.push(`M${ox} ${oy} L${ox + R * Math.cos(a0)} ${oy + R * Math.sin(a0)} L${ox + R * Math.cos(a1)} ${oy + R * Math.sin(a1)}Z`);
  }
  return (
    <View pointerEvents="none" style={[StyleSheet.absoluteFill, style]}>
      <Svg width={W} height={H}>
        <Defs>
          <RadialGradient id="pageBg" cx={W / 2} cy={0} rx={W * 1.2} ry={H * 1.2} fx={W / 2} fy={0} gradientUnits="userSpaceOnUse">
            {PAGE.stops.map(([o, c], i) => <Stop key={i} offset={o} stopColor={c} />)}
          </RadialGradient>
          <Pattern id="pageA" patternUnits="userSpaceOnUse" width="12" height="12" patternTransform="rotate(45)">
            <Rect x="0" y="0" width="6" height="12" fill="#ffffff" fillOpacity={0.055} />
          </Pattern>
          <Pattern id="pageB" patternUnits="userSpaceOnUse" width="12" height="12" patternTransform="rotate(-45)">
            <Rect x="0" y="0" width="6" height="12" fill="#ffffff" fillOpacity={0.055} />
          </Pattern>
        </Defs>
        <Rect x="0" y="0" width={W} height={H} fill="url(#pageBg)" />
        <Rect x="0" y="0" width={W} height={H} fill="url(#pageA)" />
        <Rect x="0" y="0" width={W} height={H} fill="url(#pageB)" />
        <G fill="#ffe6a0" fillOpacity={0.07}>
          {rays.map((d, i) => <Path key={i} d={d} />)}
        </G>
      </Svg>
    </View>
  );
});

// The page header (.mm-pgh): back orb, gold title, a note, then whatever goes on the right (pills).
export function PageHeader({ title, note, onBack, right, style }) {
  return (
    <View style={[styles.header, style]}>
      {onBack ? <Orb icon="back" onPress={onBack} /> : null}
      <Lettering text={title} variant="gold" size={30} />
      {note ? <Text style={styles.note} numberOfLines={1}>{note}</Text> : null}
      <View style={{ flex: 1 }} />
      {right}
    </View>
  );
}

const styles = StyleSheet.create({
  center: { alignItems: 'center', justifyContent: 'center' },
  pill: { height: 30, flexDirection: 'row', alignItems: 'center', gap: 10, paddingLeft: 4, paddingRight: 14 },
  pillImg: { width: 28, height: 28, marginLeft: -10, marginVertical: -6, resizeMode: 'contain' },
  header: { position: 'absolute', left: 16, right: 16, top: 12, height: 46, flexDirection: 'row', alignItems: 'center', gap: 12 },
  note: { fontFamily: FONT.body, fontSize: 11.5, color: '#e4d4ff' },
});
