import React, { memo, useCallback, useMemo, useState } from 'react';
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
import { Btn, InkText, Lettering, Plaque, FONT, INK } from '../../royal';
import { allChests, chestInfo } from '../ShopChests';

// The Shop's Chests tab in the new look, landscape (REDESIGN_V7 step 1): one shelf that scrolls sideways — the Free
// chest, the Rare chest (an ad), the daily deal, then every group's chest (today's HOT groups first, then the season,
// then the rest), each with its five sizes, and the Lucky meter at the end. The same data and moves as the portrait
// tab (ShopChests.js: chestInfo, allChests; the Shop's handlers); only the look and the layout are new.

const fmt = fmtNum;
const SIZE_NAME = ['Common', 'Rare', 'Epic', 'Legendary', 'Rainbow'];
const SIZE_DOT = [['#f5e6d3', '#b08a68'], ['#d4ecff', '#2f8cff'], ['#ecd8ff', '#a24bf0'], ['#fff3b8', '#f08c00'], ['#ff8fd8', '#ffe38a', '#9ff7c8', '#9fd8ff', '#d6b3ff']];
const GROUP_LABEL = { snack: 'Snack Shack', fruit: 'Fruit Patch', ocean: 'Ocean Pals', pet: 'Pet Shop', forest: 'Forest Friends', sky: 'Sky & Stars', dream: 'Dreamland', bakery: 'Sweet Bakery', heroes: 'Masked Heroes', elemental: 'Elemental Heroes', spooky: 'Spooky Squish' };
const H = 300; // a card's height (the shelf is ~318 of the page's 393)

const Note = ({ children, color = '#eadcff', size = 11.5, style, lines }) => (
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
        <Btn label={n > 1 ? `OPEN FREE · ${n}` : 'OPEN FREE'} size={16} width={146} height={40} glow onPress={onFree} />
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
    <Plaque variant="vel" radius={20} rim={3} ring={2} lip={4} style={styles.tile}>
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
    <Plaque variant="amy" radius={20} rim={3} ring={2} lip={4} style={styles.deal}>
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
      <Lettering text={(CHEST_NAMES[gk] || [])[3] || 'Legendary chest'} variant="gold" size={16} />
      <View style={styles.row4}>
        <Image source={COIN[1]} style={styles.i16} />
        <Note size={11} color="#fff">{`+${fmt(GDEAL.coins)} · ${GROUP_LABEL[gk] || ''}`}</Note>
      </View>
      {done ? (
        <Plaque variant="vel" radius={14} style={styles.wait}>
          <InkText size={12}>Bought! New deal tomorrow</InkText>
        </Plaque>
      ) : (
        <Btn width={196} height={42} glow onPress={onBuy}>
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

// one group's chest: its art on the group's backdrop, the sizes, the odds, Open
const Card = memo(function Card({ x, index, size, info, rb, onSize, onOpen, onBuy }) {
  const C = info;
  const gemsCur = C.T.price.cur === 'gems';
  return (
    <Plaque variant="vel" radius={20} rim={3} ring={2} lip={4} style={styles.card}>
      <Pressable silent onPress={() => onOpen(x.k, size)} style={styles.cardTopWrap}>
        <ImageBackground source={GCHEST[x.k].bg} style={styles.cardTop} imageStyle={styles.cardTopImg} resizeMode="cover">
          <Bob ms={2800} delay={(index * 0.6) % 2.8} style={styles.cardArt}>
            <Image source={GCHEST[x.k][`${size}-grow`]} style={styles.fill} resizeMode="contain" fadeDuration={0} />
          </Bob>
          {!!x.tag && (
            <LinearGradient colors={x.tagBg} start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }} style={styles.cardTag}>
              <Text style={styles.cardTagT}>{x.tag}</Text>
            </LinearGradient>
          )}
          <View style={styles.cardHave}>
            <Text style={styles.cardHaveT}>{`${C.have} / ${C.pool.length}`}</Text>
          </View>
        </ImageBackground>
      </Pressable>
      <Lettering text={GROUP_LABEL[x.k] || x.k} variant="gold" size={15} style={{ marginTop: 2 }} />
      <View style={styles.sizes}>
        {SIZES.map((T, ti) => {
          if (T.k === 'set' && rb && !rb.reward) return null;
          const on = T.k === size;
          return (
            <Pressable key={T.k} onPress={() => onSize(x.k, T.k)} hitSlop={4} style={[styles.sizeDot, on && styles.sizeOn]}>
              <LinearGradient colors={SIZE_DOT[ti]} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={styles.sizeIn} />
              {T.k === 'set' && rb && rb.ready ? <View style={styles.readyDot} /> : null}
            </Pressable>
          );
        })}
      </View>
      <Note size={11} color="#fff" lines={1}>{`${SIZE_NAME[C.ti]} · ${C.name}`}</Note>
      {C.earned ? (
        <>
          <Note size={10} lines={2} style={styles.center}>
            {C.earned.opened ? `${C.earned.reward.name} is in your squad.` : C.earned.ready ? `Ready! ${C.earned.reward.name} is inside.` : `Not sold: unlock every other pal (${C.earned.have} / ${C.earned.need}).`}
          </Note>
          <Btn label={C.earned.opened ? 'OPENED' : C.earned.ready ? 'OPEN FREE' : 'NOT YET'} variant={C.earned.ready && !C.earned.opened ? 'gold' : 'vel'} size={16} width={196} height={40} onPress={() => onBuy(x.k, size)} />
        </>
      ) : (
        <>
          <Note size={10} lines={1}>{`${C.odds} · dupe ${C.dupeCoins}`}</Note>
          <TutTarget name={index === 0 ? 'buy:hot' : `buy:${x.k}`}>
            <Btn width={196} height={42} onPress={() => onBuy(x.k, size)}>
              <View style={styles.priceRow}>
                <Lettering text="OPEN" variant="whiteB" size={17} />
                <Image source={gemsCur ? GEM[0] : COIN[0]} style={styles.i24} />
                <Lettering text={fmt(C.T.price.n)} variant="whiteB" size={17} />
              </View>
            </Btn>
          </TutTarget>
        </>
      )}
    </Plaque>
  );
});

function Head({ count, onOdds }) {
  return (
    <View style={styles.head}>
      <Lettering text="All" variant="gold" size={24} />
      <Lettering text="chests" variant="gold" size={24} />
      <Note size={11} style={styles.center}>{`${count} groups, every size`}</Note>
      <TickingText style={[styles.note, styles.center, { fontSize: 10.5, color: '#eadcff' }]} text={(now) => `Hot picks change in ${hms(untilMidnight(now))}`} />
      <Pressable onPress={onOdds} style={styles.oddsLink}>
        <Text style={styles.oddsLinkT}>Drop odds</Text>
      </Pressable>
    </View>
  );
}

function Lucky({ epic }) {
  const n = Math.min(epic || 0, LUCK_MAX);
  const left = Math.max(1, LUCK_MAX - n);
  return (
    <Plaque variant="vel" radius={20} rim={3} ring={2} lip={4} style={styles.lucky}>
      <Svg width={34} height={34} viewBox="0 0 24 24">
        <Path d="M12 2L14.6 8.6L21.5 9.2L16.2 13.7L17.9 20.5L12 16.8L6.1 20.5L7.8 13.7L2.5 9.2L9.4 8.6Z" fill="#ff5cc6" stroke={INK} strokeWidth={1.4} strokeLinejoin="round" />
      </Svg>
      <Lettering text="Lucky meter" variant="white" size={17} />
      <Note size={11} style={styles.center}>{`Epic or better in ${left} ${left === 1 ? 'chest' : 'chests'}`}</Note>
      <View style={styles.luckyBar}>
        {Array.from({ length: LUCK_MAX }, (_, i) => (
          <View key={i} style={[styles.luckySeg, i < n ? { backgroundColor: i >= LUCK_MAX - 3 ? '#ff5cc6' : '#b07cff' } : null]} />
        ))}
      </View>
    </Plaque>
  );
}

function ShopChestsRoyal({ profile, day, artIds, hidden, setRewards, rainbow = RAINBOW, onFree, onAd, onDeal, onOpenSheet, onBuy, onOdds }) {
  const hot = useMemo(() => lineup(day, hidden), [day, hidden]);
  const welcome = !!profile && !owns(profile, '1') && !profile.chestOpens;
  const list = useMemo(() => allChests(day, hidden), [day, hidden]);
  const adsLeft = Math.max(0, AD_CHESTS - (profile?.adChests?.day === day ? profile.adChests.n || 0 : 0));
  const [sel, setSel] = useState({});
  const onSize = useCallback((gk, size) => setSel((s) => ({ ...s, [gk]: size })), []);
  const freeGk = welcome ? BY_ID['1'].set : hot[0];
  const items = useMemo(() => [{ t: 'free' }, { t: 'rare' }, { t: 'deal' }, { t: 'head' }, ...list.map((x, i) => ({ t: 'chest', x, i })), { t: 'lucky' }], [list]);
  const render = ({ item }) => {
    if (item.t === 'free')
      return (
        <Tile title="Free chest" sub="Every 4 hours" art={GCHEST[freeGk]['common-tile']}>
          <FreeButton freeAt={profile?.freeAt || 0} bonus={profile?.freeBonus || 0} onFree={onFree} />
        </Tile>
      );
    if (item.t === 'rare')
      return (
        <Tile title="Rare chest" sub="Watch and open" art={GCHEST[hot[1]]['rare-tile']}>
          <TutTarget name="adChest">
            <Btn variant={adsLeft ? 'gold' : 'vel'} width={146} height={40} disabled={!adsLeft} onPress={onAd}>
              <Lettering text={adsLeft ? `${profile?.adsFree ? 'FREE' : 'WATCH'} · ${adsLeft}` : 'TOMORROW'} variant={adsLeft ? 'whiteB' : 'white'} size={15} />
            </Btn>
          </TutTarget>
        </Tile>
      );
    if (item.t === 'deal') return <Deal gk={hot[0]} done={profile?.dealDay === day} onBuy={onDeal} />;
    if (item.t === 'head') return <Head count={list.length} onOdds={onOdds} />;
    if (item.t === 'lucky') return <Lucky epic={profile?.epic || 0} />;
    const x = item.x;
    const size = sel[x.k] || 'common';
    const info = chestInfo(x.k, size, profile, artIds, setRewards, rainbow);
    const rb = rainbow === 'chest' ? chestInfo(x.k, 'set', profile, artIds, setRewards, rainbow).earned : null;
    return <Card x={x} index={item.i} size={size} info={info} rb={rb} onSize={onSize} onOpen={onOpenSheet} onBuy={onBuy} />;
  };
  return (
    <FlatList
      horizontal
      data={items}
      extraData={[profile, sel, adsLeft]}
      keyExtractor={(it) => (it.t === 'chest' ? `c:${it.x.k}` : it.t)}
      renderItem={render}
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.shelf}
      initialNumToRender={5}
      maxToRenderPerBatch={2}
      windowSize={5}
      removeClippedSubviews
    />
  );
}

export default memo(ShopChestsRoyal);

const styles = StyleSheet.create({
  shelf: { paddingLeft: 14, paddingRight: 24, paddingTop: 6, gap: 18, alignItems: 'center' },
  fill: { width: '100%', height: '100%' },
  note: { fontFamily: FONT.body, includeFontPadding: false },
  center: { textAlign: 'center' },
  row4: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  priceRow: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  i16: { width: 16, height: 16 },
  i24: { width: 24, height: 24, resizeMode: 'contain' },
  tile: { width: 172, height: H, paddingVertical: 14, justifyContent: 'space-between' },
  tileArt: { width: 136, height: 136 },
  wait: { height: 36, paddingHorizontal: 12 },
  deal: { width: 238, height: H, paddingVertical: 12, justifyContent: 'space-between' },
  dealTop: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  dealChip: { height: 22, paddingHorizontal: 8 },
  dealChipT: { fontFamily: FONT.display, fontSize: 10.5, letterSpacing: 0.8, color: INK, includeFontPadding: false },
  dealTimer: { fontFamily: FONT.bodyHeavy, fontSize: 10.5, color: '#fff6c8' },
  dealArt: { width: 150, height: 118 },
  dealBadge: { position: 'absolute', right: -6, top: 4, paddingHorizontal: 7, paddingVertical: 3, borderRadius: 10, borderWidth: 2, borderColor: INK, transform: [{ rotate: '12deg' }] },
  dealBadgeT: { fontFamily: FONT.display, fontSize: 13, color: '#a3001f', includeFontPadding: false },
  was: { fontFamily: FONT.bodyHeavy, fontSize: 12, color: '#8a4a06', textDecorationLine: 'line-through', marginRight: 2 },
  head: { width: 132, height: H, alignItems: 'center', justifyContent: 'center', gap: 4 },
  oddsLink: { marginTop: 10, paddingHorizontal: 12, paddingVertical: 6, borderRadius: 999, backgroundColor: 'rgba(255,255,255,0.12)' },
  oddsLinkT: { fontFamily: FONT.bodyHeavy, fontSize: 11.5, color: '#ffe9a8' },
  card: { width: 238, height: H, paddingTop: 10, paddingBottom: 12, paddingHorizontal: 10, justifyContent: 'space-between' },
  cardTopWrap: { alignSelf: 'stretch' },
  cardTop: { height: 128, alignItems: 'center', justifyContent: 'center', overflow: 'hidden', borderRadius: 14, borderWidth: 2, borderColor: '#ffd76a' },
  cardTopImg: { borderRadius: 12 },
  cardArt: { width: 128, height: 112 },
  cardTag: { position: 'absolute', left: 6, top: 6, paddingHorizontal: 7, paddingVertical: 2, borderRadius: 999, borderWidth: 1.5, borderColor: '#ffffff' },
  cardTagT: { fontFamily: FONT.bodyBlack, fontSize: 8.5, letterSpacing: 0.8, color: '#ffffff' },
  cardHave: { position: 'absolute', right: 6, bottom: 6, paddingHorizontal: 7, paddingVertical: 2, borderRadius: 999, backgroundColor: 'rgba(30,0,60,0.55)' },
  cardHaveT: { fontFamily: FONT.bodyHeavy, fontSize: 10, color: '#ffe9a8' },
  sizes: { flexDirection: 'row', gap: 9 },
  sizeDot: { width: 24, height: 24, borderRadius: 12, padding: 3, backgroundColor: 'rgba(20,0,50,0.45)' },
  sizeOn: { backgroundColor: '#ffd24a', transform: [{ scale: 1.15 }] },
  sizeIn: { flex: 1, borderRadius: 9, borderWidth: 1.5, borderColor: INK },
  readyDot: { position: 'absolute', right: -2, top: -2, width: 9, height: 9, borderRadius: 5, backgroundColor: '#3ddc97', borderWidth: 1.5, borderColor: '#ffffff' },
  lucky: { width: 200, height: H, paddingVertical: 16, justifyContent: 'center', gap: 8 },
  luckyBar: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'center', gap: 4, width: 168 },
  luckySeg: { width: 12, height: 14, borderRadius: 4, backgroundColor: 'rgba(255,255,255,0.18)' },
});
