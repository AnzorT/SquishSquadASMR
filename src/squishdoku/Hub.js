import React, { memo } from 'react';
import { Image, ScrollView, StyleSheet, Text, View } from 'react-native';
import ToggleSwitch from '../components/candy/ToggleSwitch';
import { LinearGradient } from 'expo-linear-gradient';
import OutlinedTitle from '../components/candy/OutlinedTitle';
import { PopIn, Pulse, Rock, SlideIn } from './fx';
import { BtnLabel, SdButton } from './ui';
import { HEAD_START, LEVELS } from './levels';
import sfx from '../audio/sfx';
import { Lettering, Plaque } from '../royal';
import { F, INK } from './theme';
import { useWide } from '../royal/GameFrame';

// Squishdoku's front page: the title, the two modes, HEAD START and HOW TO PLAY.

// a mode's card (Squish Sort's and Squish Stack's BLITZ cards too)
// `big`: Squishdoku's landscape hub, its two cards side by side in the middle (REDESIGN_V9, the user: wider and taller)
export function ModeCard({ kind, title, lines, cta, onPress, delay, children, big, width }) {
  const wide = useWide(); // (landscape: a little shorter)
  // (REDESIGN_V8: the regular card a warm peach, not pink)
  const colors = kind === 'blitz' ? ['#e0fbff', '#9feeff', '#5fd0f5'] : ['#fff6ea', '#ffd9b0', '#ffb877'];
  const ring = kind === 'blitz' ? '#0c5a9c' : '#8a3a0e';
  return (
    <SlideIn delay={delay} dy={50} style={[styles.cardWrap, width ? { width, alignSelf: 'auto' } : null]}>
      <View style={[styles.cardRing, { backgroundColor: ring }]}>
        <LinearGradient colors={colors} locations={[0, 0.5, 1]} style={[styles.cardFace, wide && styles.cardFaceWide, big && styles.cardFaceBig]}>
          <View style={styles.cardShine} />
          <View style={styles.cardBody}>
            <Text style={[styles.cardTitle, big && styles.cardTitleBig, { color: ring }]}>{title}</Text>
            {lines.map((l) => (
              <Text key={l} style={[styles.cardLine, big && styles.cardLineBig]}>
                {l}
              </Text>
            ))}
            {children}
          </View>
          <Pulse to={1.06} ms={1300}>
            <SdButton kind={kind === 'blitz' ? 'cyan' : 'green'} onPress={onPress} sound="sd_select" faceStyle={[styles.ctaFace, big && styles.ctaFaceBig]}>
              <BtnLabel kind={kind === 'blitz' ? 'cyan' : 'green'} size={big ? 21 : 18}>
                {cta}
              </BtnLabel>
            </SdButton>
          </Pulse>
        </LinearGradient>
      </View>
    </SlideIn>
  );
}

// Squishdoku's two modes as the app's new design (2026-11-16, the user: "remodel the cards … to match the new design",
// no PLAY or GO buttons, the card itself is tapped): a wood plaque like the Game Park's game card, the mode's name in
// the menu's lettering, a picture in a gold-ringed window (tools/squishdoku/mode-art.mjs), and one line under it.
// Squish Stack's and Squish Hop's hubs use it too, with their own pictures (`pic`; tools/games/mode-art.mjs).
const PIC = { regular: require('../../assets/squishdoku/mode-regular.jpg'), blitz: require('../../assets/squishdoku/mode-blitz.jpg') };
export function ModePanel({ kind, pic, title, onPress, delay, width, picH = 138, children }) {
  return (
    <SlideIn delay={delay} dy={50} style={width ? { width } : styles.cardWrap}>
      <Plaque
        variant="vel"
        radius={22}
        onPress={() => {
          sfx.play('sd_select');
          onPress();
        }}
        style={styles.panel}
      >
        <Lettering text={title} variant="white" size={22} />
        <View style={styles.winRing}>
          <View style={[styles.win, { height: picH }]}>
            <Image source={pic || PIC[kind]} style={styles.winPic} resizeMode="cover" fadeDuration={0} />
          </View>
        </View>
        <View style={styles.info}>{children}</View>
      </Plaque>
    </SlideIn>
  );
}

// HEAD START: each Regular level starts with the first row's creature placed, for a shorter clock and smaller prizes
// (levels.js HEAD_START). Blitz ignores the switch: every Blitz board starts with 1 creature placed anyway (BlitzGame.js).
// Said plainly, without the creature picture (the user:
// "isn't so clear", "remove the teddy bear").
function HeadStart({ on, onToggle, delay, width }) {
  return (
    <SlideIn delay={delay} dy={40} style={width ? { width } : styles.headWrap}>
      <Plaque variant="pearl" radius={18} style={styles.headCard}>
        <View style={styles.headText}>
          <View style={styles.headTop}>
            <Text style={styles.headTitle}>HEAD START</Text>
            <Text style={styles.headTag}>REGULAR MODE</Text>
          </View>
          <Text style={styles.headLine}>Every level starts with 1 creature already placed</Text>
          <Text style={styles.headCost}>{`${Math.round((1 - HEAD_START.time) * 100)}% less time · ${Math.round((1 - HEAD_START.reward) * 100)}% fewer coins & XP`}</Text>
        </View>
        <ToggleSwitch value={on} onToggle={onToggle} />
      </Plaque>
    </SlideIn>
  );
}

// the ? on HOW TO PLAY (the user: a ?, not an X)
export function QGlyph({ size = 18 }) {
  return (
    <View style={[styles.q, { width: size, height: size, borderRadius: size / 2 }]}>
      <Text style={[styles.qText, { fontSize: size * 0.72 }]}>?</Text>
    </View>
  );
}

// REGULAR MODE's line: how far through the levels, as a bar and "25/600" on one row (the user)
export function LevelsLine({ lv, done, total }) {
  return (
    <>
      <View style={styles.progTrack}>
        <LinearGradient colors={['#ffe38a', '#ffb02e']} start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }} style={[styles.progFill, { width: `${Math.max(4, (done / total) * 100)}%` }]} />
      </View>
      <Text style={styles.infoText}>{`${Math.min(lv, total)}/${total}`}</Text>
    </>
  );
}
// BLITZ MODE's only line: the most boards cleared in one run (the user: "the only text … the level you got to without
// errors")
export const BestLine = ({ top }) => <Text style={[styles.infoText, styles.infoCenter]}>{`BEST: ${top}`}</Text>;

export function RulesButton({ onPress, delay = 600 }) {
  return (
    <SlideIn delay={delay} dy={30}>
      <SdButton kind="white" onPress={onPress} sound="sd_mode" faceStyle={styles.rulesFace}>
        <QGlyph size={18} />
        <BtnLabel kind="white" size={14}>
          HOW TO PLAY
        </BtnLabel>
      </SdButton>
    </SlideIn>
  );
}

// the title on its own (the user, 2026-11-16: no creature on either side of it)
export function HubTitle({ text, size }) {
  return (
    <View style={styles.wTitle} pointerEvents="none">
      <PopIn delay={60} from={0.3} twist={-10}>
        <Rock deg={2} ms={3400}>
          <OutlinedTitle text={text} size={size} fill="gold" />
        </Rock>
      </PopIn>
    </View>
  );
}

// A game's hub in the new design (Squish Stack's and Squish Hop's; Squishdoku's below adds HEAD START): the title,
// REGULAR MODE and BLITZ MODE, and HOW TO PLAY. Landscape: the title at the top, the two cards side by side in the
// middle, HOW TO PLAY under them; upright: one column.
export function GameHub({ title, pics, lv, done, total, top, onRegular, onBlitz, onRules }) {
  const wide = useWide();
  const regular = (width) => (
    <ModePanel kind="regular" pic={pics.regular} title="Regular Mode" onPress={onRegular} delay={300} width={width}>
      <LevelsLine lv={lv} done={done} total={total} />
    </ModePanel>
  );
  const blitz = (width) => (
    <ModePanel kind="blitz" pic={pics.blitz} title="Blitz Mode" onPress={onBlitz} delay={420} width={width}>
      <BestLine top={top} />
    </ModePanel>
  );
  if (wide)
    return (
      <View style={styles.wPage}>
        <View style={styles.wTop}>
          <HubTitle text={title} size={34} />
        </View>
        <View style={styles.wCards}>
          {regular(CARD_W)}
          {blitz(CARD_W)}
        </View>
        <View style={styles.wBottom}>
          <RulesButton onPress={onRules} />
        </View>
      </View>
    );
  return (
    <ScrollView style={styles.fill} contentContainerStyle={styles.wrap} showsVerticalScrollIndicator={false}>
      <HubTitle text={title} size={32} />
      {regular()}
      {blitz()}
      <RulesButton onPress={onRules} />
    </ScrollView>
  );
}

// `pl`: the player's level and XP (src/playerXp.js xpView); not shown since REDESIGN_V9
function Hub({ rec, head, onToggleHead, onRegular, onBlitz, onRules }) {
  const done = rec.lv - 1 + (rec.best[LEVELS] ? 1 : 0);
  const wide = useWide();
  const regular = (width) => (
    <ModePanel kind="regular" title="Regular Mode" onPress={onRegular} delay={300} width={width}>
      <LevelsLine lv={rec.lv} done={done} total={LEVELS} />
    </ModePanel>
  );
  const blitz = (width) => (
    <ModePanel kind="blitz" title="Blitz Mode" onPress={onBlitz} delay={420} width={width}>
      <BestLine top={rec.blitz.top} />
    </ModePanel>
  );
  const rules = <RulesButton onPress={onRules} />;
  const title = (size) => <HubTitle text="SQUISHDOKU" size={size} />;
  // landscape (REDESIGN_V9, the user's marked-up screenshot, 2026-11-16): the title at the top in the middle, level with
  // the back button; the two modes side by side in the middle; HEAD START and HOW TO PLAY in a row along the bottom
  if (wide)
    return (
      <View style={styles.wPage}>
        <View style={styles.wTop}>{title(34)}</View>
        <View style={styles.wCards}>
          {regular(CARD_W)}
          {blitz(CARD_W)}
        </View>
        <View style={styles.wBottom}>
          <HeadStart on={head} onToggle={onToggleHead} delay={520} width={400} />
          {rules}
        </View>
      </View>
    );
  return (
    // upright: one column, scrolling only on short screens
    <ScrollView style={styles.fill} contentContainerStyle={styles.wrap} showsVerticalScrollIndicator={false}>
      {title(32)}
      {regular()}
      {blitz()}
      <HeadStart on={head} onToggle={onToggleHead} delay={520} />
      {rules}
    </ScrollView>
  );
}

const CARD_W = 372;

const styles = StyleSheet.create({
  fill: { flex: 1 },
  // landscape (REDESIGN_V9): title / the two modes / HEAD START and HOW TO PLAY
  wPage: { flex: 1, alignItems: 'center', justifyContent: 'space-between', paddingBottom: 12 },
  wTop: { height: 30, justifyContent: 'center', marginTop: -40 },
  wTitle: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 12 },
  wCards: { flexDirection: 'row', alignItems: 'stretch', gap: 22 },
  wBottom: { flexDirection: 'row', alignItems: 'center', gap: 18 },
  wrap: { flexGrow: 1, alignItems: 'stretch', justifyContent: 'center', paddingHorizontal: 16, paddingTop: 4, paddingBottom: 40, gap: 16 },
  // ModeCard (Squish Sort's and Squish Stack's hubs)
  cardWrap: { alignSelf: 'stretch' },
  cardRing: { borderRadius: 26, padding: 2.5, paddingBottom: 7 },
  cardFace: { borderRadius: 23.5, borderWidth: 3, borderColor: '#ffffff', flexDirection: 'row', alignItems: 'center', paddingHorizontal: 14, paddingVertical: 12, gap: 10, overflow: 'hidden' },
  cardShine: { position: 'absolute', left: 14, right: 14, top: 3, height: '35%', borderRadius: 20, backgroundColor: 'rgba(255,255,255,0.4)' },
  cardFaceWide: { paddingVertical: 8 },
  cardFaceBig: { minHeight: 150, paddingHorizontal: 18, paddingVertical: 14, gap: 12 },
  cardBody: { flex: 1, gap: 2 },
  cardTitle: { fontFamily: F.display, fontSize: 24, letterSpacing: 1, includeFontPadding: false },
  cardTitleBig: { fontSize: 30 },
  cardLine: { fontFamily: F.heavy, fontSize: 12.5, color: INK },
  cardLineBig: { fontSize: 14, lineHeight: 19 },
  ctaFace: { paddingHorizontal: 16, paddingVertical: 12 },
  ctaFaceBig: { paddingHorizontal: 18, paddingVertical: 14 },
  // the mode panels (the Game Park card: .sq-gc, its .win and .best)
  panel: { alignItems: 'stretch', justifyContent: 'flex-start', paddingHorizontal: 12, paddingTop: 6, paddingBottom: 12, gap: 8 },
  // (.win's rings: ink, gold, ink)
  winRing: { borderRadius: 17, borderWidth: 1.5, borderColor: INK, marginHorizontal: 2 },
  win: { borderRadius: 15.5, overflow: 'hidden', borderWidth: 2.5, borderColor: '#ffd76a', backgroundColor: '#6b4426' },
  winPic: { width: '100%', height: '100%' },
  info: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 4, minHeight: 24 },
  infoText: { fontFamily: F.display, fontSize: 16, color: '#ffffff', includeFontPadding: false, textShadowColor: INK, textShadowRadius: 1.5, textShadowOffset: { width: 0, height: 1.5 } },
  infoCenter: { flex: 1, textAlign: 'center', fontSize: 18, letterSpacing: 0.6 },
  progTrack: { flex: 1, height: 14, borderRadius: 7, backgroundColor: 'rgba(60,30,10,0.38)', borderWidth: 1, borderColor: 'rgba(255,236,170,0.6)', overflow: 'hidden' },
  progFill: { height: '100%', borderRadius: 7 },
  rulesFace: { paddingHorizontal: 16, paddingVertical: 8 },
  q: { backgroundColor: '#6b4426', alignItems: 'center', justifyContent: 'center' },
  qText: { fontFamily: F.display, color: '#fff6e2', includeFontPadding: false, lineHeight: undefined },
  headWrap: { alignSelf: 'stretch' },
  headCard: { flexDirection: 'row', alignItems: 'center', justifyContent: 'flex-start', gap: 12, paddingLeft: 16, paddingRight: 12, paddingVertical: 8 },
  headText: { flex: 1, gap: 1 },
  headTop: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  headTitle: { fontFamily: F.display, fontSize: 15, color: '#b86200', letterSpacing: 0.8, includeFontPadding: false },
  headTag: { fontFamily: F.display, fontSize: 9, color: '#ffffff', letterSpacing: 0.6, backgroundColor: '#b8641c', borderRadius: 999, paddingHorizontal: 7, paddingVertical: 1.5, overflow: 'hidden', includeFontPadding: false },
  headLine: { fontFamily: F.heavy, fontSize: 11.5, color: INK, lineHeight: 15 },
  headCost: { fontFamily: F.heavy, fontSize: 10.5, color: '#9a6a3a', lineHeight: 14 },
});

export default memo(Hub);
