import React, { createContext, useContext } from 'react';
import { StyleSheet, Text, View, useWindowDimensions } from 'react-native';
import { SafeAreaInsetsContext } from 'react-native-safe-area-context';
import { Lettering, Orb, PageBg, Plaque, FONT } from './index';
import CandyBackground from '../components/candy/CandyBackground';
import RoundButton, { BackGlyph } from '../components/candy/RoundButton';

// A mini-game in the landscape app (REDESIGN_V7 steps 6-9): the game plays in a panel in the middle (its own screens,
// laid out as before, sized to the panel), framed by the new look — the game's name and a line about it on the left,
// its tips on the right. In portrait it's the game alone, as before.
//
// The games size their boards from their own containers (onLayout); the few places that read the window's size read
// the panel's instead through useGameDims().

const Dims = createContext(null);
// the size a game lays out in: the panel's in landscape, else the window's
export function useGameDims() {
  const win = useWindowDimensions();
  return useContext(Dims) || win;
}

const NO_INSETS = { top: 0, bottom: 0, left: 0, right: 0 };

// `full` (REDESIGN_V8, the user: each game laid out sideways): the game has its own landscape layout and takes the
// whole screen (useGameDims gives the window; a game tells portrait from landscape by width > height).
export function useWide() {
  const { width, height } = useGameDims();
  return width > height;
}

// A game screen's background and back button (REDESIGN_V8): in landscape the wood look's warm page and its wooden back
// orb; upright, the candy ones the games had
export function GameBackground({ style, sparkles, children }) {
  const wide = useWide();
  if (!wide) return <CandyBackground style={style} sparkles={sparkles}>{children}</CandyBackground>;
  return (
    <View style={[styles.bg, style]}>
      <PageBg />
      {children}
    </View>
  );
}
export function GameBack({ onPress, size = 40 }) {
  const wide = useWide();
  if (wide) return <Orb icon="back" size={size} onPress={onPress} />;
  return (
    <RoundButton size={size} onPress={onPress}>
      <BackGlyph />
    </RoundButton>
  );
}

export default function GameFrame({ title, line, tips = [], full = false, children }) {
  const { width: W, height: H } = useWindowDimensions();
  if (W <= H || full) return children;
  const colW = Math.round(Math.min(W - 2 * 190, H * 0.74));
  const side = (W - colW) / 2;
  const dims = { width: colW, height: H, scale: 1, fontScale: 1 };
  return (
    <View style={styles.root}>
      <PageBg />
      <View style={[styles.side, { width: side }]}>
        <Plaque variant="vel" radius={22} rim={3.5} ring={2} lip={4} style={styles.card}>
          <Lettering text={title} variant="gold" size={side > 300 ? 28 : 22} />
          {line ? <Text style={styles.line}>{line}</Text> : null}
        </Plaque>
      </View>
      <View style={[styles.col, { width: colW }]}>
        {/* the game sees a phone-shaped screen: the panel's size, no notch insets (the panel is clear of them) */}
        <Dims.Provider value={dims}>
          <SafeAreaInsetsContext.Provider value={NO_INSETS}>{children}</SafeAreaInsetsContext.Provider>
        </Dims.Provider>
      </View>
      <View style={[styles.side, { width: side }]}>
        {tips.length ? (
          <Plaque variant="vel" radius={22} rim={3.5} ring={2} lip={4} style={styles.card}>
            <Lettering text="How to play" variant="white" size={17} />
            {tips.map((t, i) => (
              <Text key={i} style={styles.tip}>{`• ${t}`}</Text>
            ))}
          </Plaque>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: '#d27c3e' },
  root: { ...StyleSheet.absoluteFillObject, flexDirection: 'row', backgroundColor: '#d27c3e' },
  side: { height: '100%', alignItems: 'center', justifyContent: 'center', paddingHorizontal: 18 },
  card: { alignSelf: 'stretch', paddingHorizontal: 16, paddingVertical: 16, gap: 10 },
  line: { fontFamily: FONT.body, fontSize: 13, lineHeight: 18, color: '#fff6e2', textAlign: 'center' },
  tip: { alignSelf: 'stretch', fontFamily: FONT.body, fontSize: 12.5, lineHeight: 17, color: '#fff6e2' },
  col: { height: '100%', overflow: 'hidden', borderLeftWidth: 3, borderRightWidth: 3, borderColor: '#ffd76a' },
});
