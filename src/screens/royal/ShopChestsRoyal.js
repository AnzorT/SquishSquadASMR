import React, { memo, useCallback, useMemo, useRef, useState } from 'react';
import { FlatList, Image, ImageBackground, StyleSheet, Text, View } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import Svg, { Path } from 'react-native-svg';
import Pressable from '../../components/Pressable';
import { BY_ID, GDEAL, LUCK_MAX, RAINBOW, SIZES, AD_CHESTS, owns, lineup } from '../../squad/data';
import GCHEST, { CHEST_NAMES } from '../../squad/groupChestArt';
import { COIN, GEM } from '../../squad/currencyArt';
import { Bob, TickingText, hms, untilMidnight } from '../../squad/ui';
import TutTarget from '../../tutorial/Target';
import { fmtNum } from '../../format';
import { Btn, InkText, Lettering, Orb, Plaque, FONT, INK } from '../../royal';
import { allChests, chestInfo } from '../ShopChests';

// The Shop's Chests tab, landscape. REDESIGN_V8 (the user's remodel): pages, one at a time, with arrows at the
// screen's sides, one per group (today's free chests, deal and Lucky meter are the Daily tab: ShopDailyRoyal): its name above its four chests side by side (Common, Rare, Epic, Legendary; the Rainbow chest is never sold,
// it's given when the group is collected). The same data and moves as before (ShopChests.js: chestInfo, allChests; the
// Shop's handlers).

const fmt = fmtNum;
const SIZE_NAME = ['Common', 'Rare', 'Epic', 'Legendary', 'Rainbow'];
const SIZE_DOT = [['#f5e6d3', '#b08a68'], ['#d4ecff', '#2f8cff'], ['#ecd8ff', '#a24bf0'], ['#fff3b8', '#f08c00'], ['#ff8fd8', '#ffe38a', '#9ff7c8', '#9fd8ff', '#d6b3ff']];
const GROUP_LABEL = { snack: 'Snack Shack', fruit: 'Fruit Patch', ocean: 'Ocean Pals', pet: 'Pet Shop', forest: 'Forest Friends', sky: 'Sky & Stars', dream: 'Dreamland', bakery: 'Sweet Bakery', heroes: 'Masked Heroes', elemental: 'Elemental Heroes', spooky: 'Spooky Squish' };
const CARD_H = 206; // a card's height (the page is the 285 under the Shop's tabs: the name, the cards, a line, dots)

const Note = ({ children, color = '#fff6e2', size = 11.5, style, lines }) => (
  <Text style={[styles.note, { color, fontSize: size }, style]} numberOfLines={lines}>
    {children}
  </Text>
);

// "Open free" or the countdown (re-rendered each second while it runs). Extra free chests (a streak's chest days) open
// while the timer runs.
function FreeButton({ freeAt, bonus, onFree }) {
  const [, setT] = useState(0);
  const timed = (freeAt || 0) <= Date.now();
  const n = (timed ? 1 : 0) + (bonus || 0);
  React.useEffect(() => {
    if (n > 0) return undefined;
    const t = setInterval(() => setT((x) => x + 1), 1000);
    return () => clearInterval(t);
  }, [n, freeAt]);
  if (n > 0)
    return (
      <TutTarget name="freeChest">
        <Btn label={n > 1 ? `OPEN FREE · ${n}` : 'OPEN FREE'} size={15} width={136} height={38} glow onPress={onFree} />
      </TutTarget>
    );
  return (
    <Plaque variant="vel" radius={14} style={styles.wait}>
      <InkText size={12.5}>{`Next in ${hms(freeAt - Date.now())}`}</InkText>
    </Plaque>
  );
}

function Tile({ title, sub, art, children }) {
  return (
    <Plaque variant="vel" radius={18} rim={3} ring={2} lip={4} style={styles.tile}>
      <Lettering text={title} variant="white" size={17} />
      <Note size={11}>{sub}</Note>
      <Bob ms={2800} dy={6} style={styles.tileArt}>
        <Image source={art} style={styles.fill} resizeMode="contain" fadeDuration={0} />
      </Bob>
      {children}
    </Plaque>
  );
}

const Deal = memo(function Deal({ gk, done, onBuy }) {
  return (
    <Plaque variant="vel" radius={18} rim={3} ring={2} lip={4} style={styles.deal}>
      <View style={styles.dealTop}>
        <Plaque variant="gold" radius={8} style={styles.dealChip}>
          <Text style={styles.dealChipT}>DAILY DEAL</Text>
        </Plaque>
        <TickingText style={styles.dealTimer} text={(now) => `Ends in ${hms(untilMidnight(now))}`} />
      </View>
      <Bob ms={2600} dy={7} style={styles.dealArt}>
        <Image source={GCHEST[gk]['legendary-deal']} style={styles.fill} resizeMode="contain" fadeDuration={0} />
        <LinearGradient colors={['#fff3b0', '#ffd83a', '#ffb000']} style={styles.dealBadge}>
          <Text style={styles.dealBadgeT}>-50%</Text>
        </LinearGradient>
      </Bob>
      <Lettering text={(CHEST_NAMES[gk] || [])[3] || 'Legendary chest'} variant="gold" size={14} />
      <View style={styles.row4}>
        <Image source={COIN[1]} style={styles.i16} />
        <Note size={11} color="#fff">{`+${fmt(GDEAL.coins)} · ${GROUP_LABEL[gk] || ''}`}</Note>
      </View>
      {done ? (
        <Plaque variant="vel" radius={14} style={styles.wait}>
          <InkText size={12}>Bought! New deal tomorrow</InkText>
        </Plaque>
      ) : (
        <Btn width={176} height={38} glow onPress={onBuy}>
          <View style={styles.priceRow}>
            <Text style={styles.was}>{GDEAL.was}</Text>
            <Image source={GEM[0]} style={styles.i24} />
            <Lettering text={fmt(GDEAL.gems)} variant="whiteB" size={19} />
          </View>
        </Btn>
      )}
    </Plaque>
  );
});

// One chest of a group (REDESIGN_V8): its art on the group's backdrop, its size, its name, the odds, OPEN for its price.
// Tap the art for what's inside (the sheet).
const SIZE_INK = ['#8a5a34', '#1f6fd6', '#8a2ad8', '#c46a00'];
const ChestCard = memo(function ChestCard({ gk, size, ti, info, first, onOpen, onBuy }) {
  const C = info;
  const gemsCur = C.T.price.cur === 'gems';
  return (
    <Plaque variant="vel" radius={18} rim={3} ring={2} lip={4} style={styles.card}>
      <Pressable silent onPress={() => onOpen(gk, size)} style={styles.cardTopWrap}>
        <ImageBackground source={GCHEST[gk].bg} style={styles.cardTop} imageStyle={styles.cardTopImg} resizeMode="cover">
          <Bob ms={2800} delay={(ti * 0.7) % 2.8} style={styles.cardArt}>
            <Image source={GCHEST[gk][`${size}-grow`]} style={styles.fill} resizeMode="contain" fadeDuration={0} />
          </Bob>
          <LinearGradient colors={SIZE_DOT[ti]} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={styles.sizeChip}>
            <Text style={[styles.sizeChipT, { color: SIZE_INK[ti] }]}>{SIZE_NAME[ti].toUpperCase()}</Text>
          </LinearGradient>
        </ImageBackground>
      </Pressable>
      <Lettering text={C.name} variant="white" size={13} />
      <Note size={10} color="#fff6e2" lines={1}>{C.odds}</Note>
      <TutTarget name={first ? 'buy:hot' : `buy:${gk}:${size}`}>
        <Btn width={140} height={38} onPress={() => onBuy(gk, size)}>
          <View style={styles.priceRow}>
            <Lettering text="OPEN" variant="whiteB" size={15} />
            <Image source={gemsCur ? GEM[0] : COIN[0]} style={styles.i22} />
            <Lettering text={fmt(C.T.price.n)} variant="whiteB" size={15} />
          </View>
        </Btn>
      </TutTarget>
    </Plaque>
  );
});

// The group's Rainbow chest is never sold (the user's ruling, REDESIGN_V8): it's given once the rest of the group is
// collected, with the group's Rainbow character inside. A line under the chests says how far along it is, or opens it.
function RainbowLine({ gk, rb, onBuy }) {
  if (!rb || !rb.reward) return <View style={styles.rbLine} />;
  if (rb.ready && !rb.opened)
    return (
      <View style={styles.rbLine}>
        <Btn width={300} height={32} glow onPress={() => onBuy(gk, 'set')}>
          <Lettering text={`RAINBOW CHEST READY · OPEN`} variant="whiteB" size={14} />
        </Btn>
      </View>
    );
  return (
    <View style={styles.rbLine}>
      <LinearGradient colors={SIZE_DOT[4]} start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }} style={styles.rbDot} />
      <Note size={11} color="#fff6e2">
        {rb.opened ? `${rb.reward.name}, this group's Rainbow, is in your squad.` : `Collect all ${GROUP_LABEL[gk] || 'this group'} pals for its Rainbow chest · ${rb.have} / ${rb.need}`}
      </Note>
    </View>
  );
}

// the Lucky meter: an Epic or better at least every LUCK_MAX chests; how close the next one is
function Lucky({ epic, onOdds }) {
  const n = Math.min(epic || 0, LUCK_MAX);
  const left = Math.max(1, LUCK_MAX - n);
  return (
    <Plaque variant="vel" radius={18} rim={3} ring={2} lip={4} style={styles.lucky}>
      <Svg width={34} height={34} viewBox="0 0 24 24">
        <Path d="M12 2L14.6 8.6L21.5 9.2L16.2 13.7L17.9 20.5L12 16.8L6.1 20.5L7.8 13.7L2.5 9.2L9.4 8.6Z" fill="#ffc928" stroke={INK} strokeWidth={1.4} strokeLinejoin="round" />
      </Svg>
      <Lettering text="Lucky meter" variant="white" size={16} />
      <Note size={11} style={styles.center}>{`Epic or better in ${left} ${left === 1 ? 'chest' : 'chests'}`}</Note>
      <View style={styles.luckyBar}>
        {Array.from({ length: LUCK_MAX }, (_, i) => (
          <View key={i} style={[styles.luckySeg, i < n ? { backgroundColor: i >= LUCK_MAX - 3 ? '#ff8a1e' : '#ffd24a' } : null]} />
        ))}
      </View>
      <Pressable onPress={onOdds} style={styles.oddsLink}>
        <Text style={styles.oddsLinkT}>Drop odds</Text>
      </Pressable>
    </Plaque>
  );
}

const SOLD = ['common', 'rare', 'epic', 'legendary'];
function GroupPage({ x, first, width, profile, artIds, setRewards, rainbow, onOpenSheet, onBuy }) {
  const rb = rainbow === 'chest' ? chestInfo(x.k, 'set', profile, artIds, setRewards, rainbow).earned : null;
  const c0 = chestInfo(x.k, 'common', profile, artIds, setRewards, rainbow);
  return (
    <View style={[styles.page, { width }]}>
      <View style={styles.pageHead}>
        {!!x.tag && (
          <LinearGradient colors={x.tagBg} start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }} style={styles.cardTag}>
            <Text style={styles.cardTagT}>{x.tag}</Text>
          </LinearGradient>
        )}
        <Lettering text={GROUP_LABEL[x.k] || x.k} variant="gold" size={24} />
        <View style={styles.haveChip}>
          <Text style={styles.haveChipT}>{`${c0.have} / ${c0.pool.length} collected`}</Text>
        </View>
      </View>
      <View style={styles.cards}>
        {SOLD.map((size, ti) => (
          <ChestCard key={size} gk={x.k} size={size} ti={ti} first={first && ti === 0} info={ti === 0 ? c0 : chestInfo(x.k, size, profile, artIds, setRewards, rainbow)} onOpen={onOpenSheet} onBuy={onBuy} />
        ))}
      </View>
      <RainbowLine gk={x.k} rb={rb} onBuy={onBuy} />
    </View>
  );
}

// The Daily tab (REDESIGN_V8, the user: a tab of its own, left of Chests): today's free chest, the Rare chest for an
// ad, the daily deal and the Lucky meter
export const ShopDailyRoyal = memo(function ShopDailyRoyal({ profile, day, hidden, onFree, onAd, onDeal, onOdds }) {
  const hot = useMemo(() => lineup(day, hidden), [day, hidden]);
  const welcome = !!profile && !owns(profile, '1') && !profile.chestOpens;
  const adsLeft = Math.max(0, AD_CHESTS - (profile?.adChests?.day === day ? profile.adChests.n || 0 : 0));
  return (
    <View style={styles.dailyWrap}>
      <TodayPage profile={profile} day={day} hot={hot} welcome={welcome} adsLeft={adsLeft} onFree={onFree} onAd={onAd} onDeal={onDeal} onOdds={onOdds} />
    </View>
  );
});

function TodayPage({ width, profile, day, hot, welcome, adsLeft, onFree, onAd, onDeal, onOdds }) {
  const freeGk = welcome ? BY_ID['1'].set : hot[0];
  return (
    <View style={[styles.page, width ? { width } : null]}>
      <View style={styles.pageHead}>
        <Lettering text="Today" variant="gold" size={24} />
        <TickingText style={styles.headTimer} text={(now) => `Hot picks change in ${hms(untilMidnight(now))}`} />
      </View>
      <View style={styles.cards}>
        <Tile title="Free chest" sub="Every 4 hours" art={GCHEST[freeGk]['common-tile']}>
          <FreeButton freeAt={profile?.freeAt || 0} bonus={profile?.freeBonus || 0} onFree={onFree} />
        </Tile>
        <Tile title="Rare chest" sub="Watch and open" art={GCHEST[hot[1]]['rare-tile']}>
          <TutTarget name="adChest">
            <Btn variant={adsLeft ? 'gold' : 'vel'} width={136} height={38} disabled={!adsLeft} onPress={onAd}>
              <Lettering text={adsLeft ? `${profile?.adsFree ? 'FREE' : 'WATCH'} · ${adsLeft}` : 'TOMORROW'} variant={adsLeft ? 'whiteB' : 'white'} size={15} />
            </Btn>
          </TutTarget>
        </Tile>
        <Deal gk={hot[0]} done={profile?.dealDay === day} onBuy={onDeal} />
        <Lucky epic={profile?.epic || 0} onOdds={onOdds} />
      </View>
      <View style={styles.rbLine} />
    </View>
  );
}

// The Chests tab (REDESIGN_V8, the user's remodel): one page at a time, swiped or stepped with the arrows at the
// screen's sides. First today's chests, then each group (today's HOT groups, the season, the rest): the group's name
// above its four chests side by side, Common, Rare, Epic, Legendary.
function ShopChestsRoyal({ profile, day, artIds, hidden, setRewards, rainbow = RAINBOW, onOpenSheet, onBuy }) {
  const list = useMemo(() => allChests(day, hidden), [day, hidden]);
  const adsLeft = Math.max(0, AD_CHESTS - (profile?.adChests?.day === day ? profile.adChests.n || 0 : 0));
  const pages = useMemo(() => list.map((x, i) => ({ t: 'group', x, i })), [list]);
  const [w, setW] = useState(0);
  const [at, setAt] = useState(0);
  const ref = useRef(null);
  const go = useCallback(
    (i) => {
      const n = Math.max(0, Math.min(pages.length - 1, i));
      setAt(n);
      if (ref.current) ref.current.scrollToIndex({ index: n, animated: true });
    },
    [pages.length]
  );
  const render = ({ item }) => <GroupPage x={item.x} first={item.i === 0} width={w} profile={profile} artIds={artIds} setRewards={setRewards} rainbow={rainbow} onOpenSheet={onOpenSheet} onBuy={onBuy} />;
  return (
    <View style={styles.fill} onLayout={(e) => setW(Math.round(e.nativeEvent.layout.width))}>
      {w ? (
        <FlatList
          ref={ref}
          horizontal
          pagingEnabled
          data={pages}
          extraData={[profile, adsLeft, w]}
          keyExtractor={(it) => (it.t === 'group' ? `g:${it.x.k}` : it.t)}
          renderItem={render}
          getItemLayout={(_, i) => ({ length: w, offset: w * i, index: i })}
          onMomentumScrollEnd={(e) => setAt(Math.round(e.nativeEvent.contentOffset.x / w))}
          // (and while it moves: a browser's drag doesn't end with a momentum event)
          onScroll={(e) => setAt(Math.round(e.nativeEvent.contentOffset.x / w))}
          scrollEventThrottle={64}
          showsHorizontalScrollIndicator={false}
          initialNumToRender={1}
          maxToRenderPerBatch={1}
          windowSize={3}
        />
      ) : null}
      <Orb icon="left" size={40} onPress={() => go(at - 1)} disabled={at === 0} style={[styles.arrow, styles.arrowL, at === 0 && styles.arrowOff]} />
      <Orb icon="right" size={40} onPress={() => go(at + 1)} disabled={at >= pages.length - 1} style={[styles.arrow, styles.arrowR, at >= pages.length - 1 && styles.arrowOff]} />
      <View style={styles.dots} pointerEvents="none">
        {pages.map((_, i) => (
          <View key={i} style={[styles.dot, i === at && styles.dotOn]} />
        ))}
      </View>
    </View>
  );
}

export default memo(ShopChestsRoyal);

const styles = StyleSheet.create({
  fill: { width: '100%', height: '100%' },
  note: { fontFamily: FONT.body, includeFontPadding: false },
  center: { textAlign: 'center' },
  row4: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  priceRow: { flexDirection: 'row', alignItems: 'center', gap: 5 },
  i16: { width: 16, height: 16 },
  i22: { width: 22, height: 22, resizeMode: 'contain' },
  i24: { width: 24, height: 24, resizeMode: 'contain' },
  page: { height: '100%', alignItems: 'center', paddingTop: 2 },
  dailyWrap: { flex: 1, alignItems: 'center' },
  pageHead: { height: 32, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 10 },
  headTimer: { fontFamily: FONT.bodyHeavy, fontSize: 11, color: '#fff6e2' },
  haveChip: { paddingHorizontal: 9, paddingVertical: 2, borderRadius: 999, backgroundColor: 'rgba(107,68,38,0.55)' },
  haveChipT: { fontFamily: FONT.bodyHeavy, fontSize: 10.5, color: '#fff6e2' },
  cards: { height: CARD_H + 6, flexDirection: 'row', alignItems: 'center', gap: 12, marginTop: 2 },
  tile: { width: 156, height: CARD_H, paddingVertical: 10, justifyContent: 'space-between' },
  tileArt: { width: 112, height: 112 },
  wait: { height: 34, paddingHorizontal: 12 },
  deal: { width: 200, height: CARD_H, paddingVertical: 9, justifyContent: 'space-between' },
  dealTop: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  dealChip: { height: 20, paddingHorizontal: 7 },
  dealChipT: { fontFamily: FONT.display, fontSize: 9.5, letterSpacing: 0.8, color: INK, includeFontPadding: false },
  dealTimer: { fontFamily: FONT.bodyHeavy, fontSize: 9.5, color: '#fff6c8' },
  dealArt: { width: 130, height: 96 },
  dealBadge: { position: 'absolute', right: -6, top: 4, paddingHorizontal: 7, paddingVertical: 3, borderRadius: 10, borderWidth: 2, borderColor: INK, transform: [{ rotate: '12deg' }] },
  dealBadgeT: { fontFamily: FONT.display, fontSize: 13, color: '#a3001f', includeFontPadding: false },
  was: { fontFamily: FONT.bodyHeavy, fontSize: 12, color: '#8a4a06', textDecorationLine: 'line-through', marginRight: 2 },
  oddsLink: { marginTop: 6, paddingHorizontal: 12, paddingVertical: 5, borderRadius: 999, backgroundColor: 'rgba(107,68,38,0.45)' },
  oddsLinkT: { fontFamily: FONT.bodyHeavy, fontSize: 11, color: '#fff6e2' },
  card: { width: 160, height: CARD_H, paddingTop: 8, paddingBottom: 10, paddingHorizontal: 8, justifyContent: 'space-between' },
  cardTopWrap: { alignSelf: 'stretch' },
  cardTop: { height: 112, alignItems: 'center', justifyContent: 'center', overflow: 'hidden', borderRadius: 12, borderWidth: 2, borderColor: '#ffd76a' },
  cardTopImg: { borderRadius: 10 },
  cardArt: { width: 116, height: 100 },
  sizeChip: { position: 'absolute', left: 5, top: 5, paddingHorizontal: 7, paddingVertical: 2, borderRadius: 999, borderWidth: 1.5, borderColor: '#ffffff' },
  sizeChipT: { fontFamily: FONT.bodyBlack, fontSize: 9, letterSpacing: 0.8, includeFontPadding: false },
  cardTag: { paddingHorizontal: 8, paddingVertical: 3, borderRadius: 999, borderWidth: 1.5, borderColor: '#ffffff' },
  cardTagT: { fontFamily: FONT.bodyBlack, fontSize: 9, letterSpacing: 0.8, color: '#ffffff' },
  rbLine: { height: 26, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 7 },
  rbDot: { width: 14, height: 14, borderRadius: 7, borderWidth: 1.5, borderColor: '#ffffff' },
  lucky: { width: 156, height: CARD_H, paddingVertical: 14, justifyContent: 'center', gap: 7 },
  luckyBar: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'center', gap: 3, width: 140 },
  luckySeg: { width: 10, height: 13, borderRadius: 4, backgroundColor: 'rgba(107,68,38,0.35)' },
  arrow: { position: 'absolute', top: '50%', marginTop: -36 },
  arrowL: { left: 16 },
  arrowR: { right: 16 },
  arrowOff: { opacity: 0.35 },
  dots: { position: 'absolute', left: 0, right: 0, bottom: 3, flexDirection: 'row', justifyContent: 'center', gap: 5 },
  dot: { width: 6, height: 6, borderRadius: 3, backgroundColor: 'rgba(107,68,38,0.45)' },
  dotOn: { width: 16, backgroundColor: '#fff6e2' },
});
