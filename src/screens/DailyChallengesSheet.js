import React, { memo, useEffect, useState } from 'react';
import { useWindowDimensions, View, Text, Pressable, StyleSheet, ScrollView } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import Svg, { Rect, Path } from 'react-native-svg';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { candyColors, candyFonts } from '../theme/candyTheme';
import OutlinedTitle from '../components/candy/OutlinedTitle';
import RoundButton, { CloseGlyph } from '../components/candy/RoundButton';
import CandyButton from '../components/candy/CandyButton';
import { dailyView } from '../progression';
import sfx from '../audio/sfx';

// The design's DAILY CHALLENGES panel (the 2026-10-03 drop): over Home, a
// white-rimmed pink card with "In Squish Squad" (3 game challenges) and "In
// the Crib" (3 Crib ones, OPEN CRIB), each row a progress bar, its reward
// pill, CLAIM +N when done, DONE once claimed; and the daily chest along the
// bottom: claim all six for +250 coins, a token and the streak's reward
// (the streak has its own screen: StreakScreen). The rules and the rows come
// from src/progression.js; the sheet re-reads them every minute so the "new
// challenges in 5h 12m" countdown keeps time.

function ChestIcon() {
  return (
    <Svg width={34} height={32} viewBox="0 0 30 28">
      <Rect x={3} y={11} width={24} height={15} rx={3} fill="#cf955a" stroke="#6b4426" strokeWidth={2} />
      <Path d="M3 13 Q3 3 15 3 Q27 3 27 13 Z" fill="#e6b47c" stroke="#6b4426" strokeWidth={2} strokeLinejoin="round" />
      <Rect x={12} y={10} width={6} height={8} rx={1.5} fill="#ffe045" stroke="#a04a00" strokeWidth={1.6} />
    </Svg>
  );
}

const Row = memo(function Row({ row, onClaim, wide }) {
  return (
    <View style={[styles.row, wide && styles.rowWide]}>
      <View style={styles.rowMain}>
        <Text style={styles.rowTitle}>{row.title}</Text>
        <View style={styles.barRow}>
          <View style={styles.bar}>
            <LinearGradient colors={['#ffe08a', '#ffb347', '#f07a1c']} locations={[0, 0.55, 1]} style={[styles.barFill, { width: `${row.pct}%` }]} />
          </View>
          <Text style={styles.barLabel}>{row.label}</Text>
        </View>
      </View>
      <View style={styles.rewardCol}>
        {row.open ? (
          <View style={styles.rewardPill}>
            <Text style={styles.rewardText}>{`+${row.reward}`}</Text>
          </View>
        ) : row.claimable ? (
          <CandyButton variant="gold" size="xs" onPress={() => onClaim(row.id)} label={`CLAIM +${row.reward}`} textStyle={styles.claimText} />
        ) : (
          <View style={styles.donePill}>
            <Text style={styles.doneText}>DONE</Text>
          </View>
        )}
        {!row.claimed ? <Text style={styles.xpText}>{`+${row.xp} XP`}</Text> : null}
      </View>
    </View>
  );
});

function Section({ title, right, rows, onClaim, wide }) {
  return (
    <View style={[styles.section, wide && styles.sectionWide]}>
      <View style={styles.sectionHead}>
        <Text style={styles.sectionTitle}>{title}</Text>
        {right}
      </View>
      {rows.map((r) => (
        <Row key={r.id} row={r} onClaim={onClaim} wide={wide} />
      ))}
    </View>
  );
}

function DailyChallengesSheet({ visible, profile, onClose, onClaim, onClaimChest, onOpenCrib }) {
  const insets = useSafeAreaInsets();
  const winD = useWindowDimensions();
  const wideNow = winD.width > winD.height; // landscape (REDESIGN_V7)
  const [clock, setClock] = useState(() => new Date());
  useEffect(() => {
    if (!visible) return undefined;
    setClock(new Date());
    const iv = setInterval(() => setClock(new Date()), 60000);
    return () => clearInterval(iv);
  }, [visible]);
  useEffect(() => {
    if (visible) sfx.play('popOpen');
  }, [visible]);
  if (!visible) return null;
  const v = dailyView(profile, clock);
  return (
    <View style={styles.overlay}>
      <Pressable style={[StyleSheet.absoluteFill, styles.scrim]} onPress={onClose} />
      {/* landscape (REDESIGN_V7, V8): a wide panel the screen's height, the two lists side by side */}
      <View style={[styles.ring, wideNow ? { top: 10, bottom: 10, left: '6%', right: '6%' } : { top: insets.top + 50, bottom: 56 + insets.bottom }]}>
        <View style={styles.rim}>
          <ScrollView style={styles.panel} contentContainerStyle={[styles.panelContent, wideNow && styles.panelContentWide]} showsVerticalScrollIndicator={false}>
            <View style={styles.header}>
              <View style={{ flex: 1 }}>
                <OutlinedTitle text="DAILY CHALLENGES" fill="gold" size={24} outline={2} ring={0} drop={0} letterSpacing={0} />
                <Text style={styles.resetIn}>{`New challenges in ${v.resetIn}`}</Text>
              </View>
              <RoundButton size={36} onPress={onClose} hitSlop={8}>
                <CloseGlyph />
              </RoundButton>
            </View>

            {/* opened from the Crib (no onOpenCrib), "you are here" moves over */}
            <View style={wideNow ? styles.cols : styles.stack}>
              <Section title="In Squish Squad" right={onOpenCrib ? <Text style={styles.here}>YOU ARE HERE</Text> : null} rows={v.game} onClaim={onClaim} wide={wideNow} />
              <Section
                title="In the Crib"
                right={onOpenCrib ? <CandyButton variant="blue" size="xs" label="OPEN CRIB" onPress={onOpenCrib} textStyle={styles.openCribText} /> : <Text style={styles.here}>YOU ARE HERE</Text>}
                rows={v.crib}
                onClaim={onClaim}
                wide={wideNow}
              />
            </View>

            <LinearGradient colors={['#fff7d6', '#ffe9a8']} style={[styles.chest, wideNow && styles.chestWide]}>
              <ChestIcon />
              <View style={{ flex: 1, gap: 5 }}>
                <Text style={styles.chestText}>{`Claim all ${v.total} for the daily chest · +${v.chestReward} coins + ${v.chestXp} XP + your streak reward`}</Text>
                <View style={styles.chestBar}>
                  <LinearGradient colors={['#fffbd6', '#ffe045', '#ff9500']} locations={[0, 0.45, 1]} style={[styles.barFill, { width: `${v.chestPct}%` }]} />
                </View>
              </View>
              {v.chestReady ? (
                <CandyButton variant="pink" size="xs" label="OPEN" pulse="soft" onPress={onClaimChest} textStyle={styles.claimText} />
              ) : v.chestDone ? (
                <View style={styles.donePill}>
                  <Text style={styles.doneText}>OPENED</Text>
                </View>
              ) : null}
              <Text style={styles.chestCount}>{`${v.claimedN}/${v.total}`}</Text>
            </LinearGradient>
          </ScrollView>
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  overlay: { ...StyleSheet.absoluteFillObject, zIndex: 60, elevation: 60 },
  scrim: { backgroundColor: 'rgba(50,24,6,0.6)' },
  ring: {
    position: 'absolute',
    left: 14,
    right: 14,
    borderRadius: 31,
    padding: 3,
    backgroundColor: candyColors.pinkRing,
    shadowColor: '#3f2412',
    shadowOpacity: 0.5,
    shadowRadius: 20,
    shadowOffset: { width: 0, height: 14 },
    elevation: 12,
  },
  rim: { flex: 1, borderRadius: 28, borderWidth: 4, borderColor: '#ffffff', backgroundColor: candyColors.paper, overflow: 'hidden' },
  panel: { flex: 1 },
  panelContent: { padding: 14, paddingTop: 16, gap: 12, flexGrow: 1 },
  panelContentWide: { paddingTop: 10, paddingBottom: 10, gap: 8 },
  stack: { gap: 12 },
  cols: { flexDirection: 'row', gap: 12 },
  sectionWide: { flex: 1, gap: 6 },
  rowWide: { paddingVertical: 6 },
  chestWide: { paddingVertical: 8 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  resetIn: { marginTop: 6, fontFamily: candyFonts.bodyHeavy, fontSize: 11, color: candyColors.muted },
  section: { gap: 8 },
  sectionHead: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  sectionTitle: { fontFamily: candyFonts.display, fontSize: 15, color: '#b8641c' },
  here: { fontFamily: candyFonts.bodyBlack, fontSize: 10, letterSpacing: 1, color: candyColors.muted },
  openCribText: { fontSize: 11 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    paddingVertical: 10,
    paddingHorizontal: 12,
    borderRadius: 16,
    backgroundColor: '#ffffff',
    borderWidth: 2,
    borderColor: '#ecd3b0',
    shadowColor: '#ecd3b0',
    shadowOpacity: 1,
    shadowRadius: 0,
    shadowOffset: { width: 0, height: 3 },
    elevation: 2,
  },
  rowMain: { flex: 1, minWidth: 0, gap: 6 },
  rowTitle: { fontFamily: candyFonts.bodyHeavy, fontSize: 13, lineHeight: 16, color: candyColors.ink },
  barRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  bar: { flex: 1, height: 10, borderRadius: 6, backgroundColor: '#f6ead8', borderWidth: 1.5, borderColor: '#e2c49c', overflow: 'hidden' },
  barFill: { height: '100%', borderRadius: 6 },
  barLabel: { fontFamily: candyFonts.bodyHeavy, fontSize: 11, color: candyColors.muted },
  rewardCol: { alignItems: 'center', gap: 2 },
  xpText: { fontFamily: candyFonts.bodyBlack, fontSize: 10, color: '#b8641c' },
  rewardPill: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 999, backgroundColor: '#fff5d6', borderWidth: 1.5, borderColor: '#ffcd3c' },
  rewardText: { fontFamily: candyFonts.bodyBlack, fontSize: 12, color: candyColors.goldInk },
  claimText: { fontSize: 12 },
  donePill: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 999, backgroundColor: '#d8ffc2', borderWidth: 1.5, borderColor: '#26a94e' },
  doneText: { fontFamily: candyFonts.bodyBlack, fontSize: 11, color: '#17652f' },
  chest: {
    marginTop: 'auto',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    padding: 12,
    borderRadius: 18,
    borderWidth: 2,
    borderColor: '#ffcd3c',
  },
  chestText: { fontFamily: candyFonts.bodyBlack, fontSize: 12, lineHeight: 15, color: '#7a3d00' },
  chestBar: { height: 10, borderRadius: 6, backgroundColor: '#ffffff', borderWidth: 1.5, borderColor: '#ffcd3c', overflow: 'hidden' },
  chestCount: { fontFamily: candyFonts.bodyBlack, fontSize: 12, color: '#7a3d00' },
});

export default memo(DailyChallengesSheet);
