import React, { createContext, useContext } from 'react';
import { StyleSheet, Text, View, useWindowDimensions } from 'react-native';
import { SafeAreaInsetsContext } from 'react-native-safe-area-context';
import { Lettering, PageBg, Plaque, FONT } from './index';

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

export default function GameFrame({ title, line, tips = [], children }) {
  const { width: W, height: H } = useWindowDimensions();
  if (W <= H) return children;
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
  root: { ...StyleSheet.absoluteFillObject, flexDirection: 'row', backgroundColor: '#24123f' },
  side: { height: '100%', alignItems: 'center', justifyContent: 'center', paddingHorizontal: 18 },
  card: { alignSelf: 'stretch', paddingHorizontal: 16, paddingVertical: 16, gap: 10 },
  line: { fontFamily: FONT.body, fontSize: 13, lineHeight: 18, color: '#eadcff', textAlign: 'center' },
  tip: { alignSelf: 'stretch', fontFamily: FONT.body, fontSize: 12.5, lineHeight: 17, color: '#f3eaff' },
  col: { height: '100%', overflow: 'hidden', borderLeftWidth: 3, borderRightWidth: 3, borderColor: '#ffd76a' },
});
