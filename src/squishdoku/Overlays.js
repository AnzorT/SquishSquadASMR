import React, { useEffect, useRef, useState } from 'react';
import { Animated, Easing, Image, StyleSheet, Text, View } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import OutlinedTitle from '../components/candy/OutlinedTitle';
import { RaysSpin } from '../components/candy/Decor';
import { CoinIcon } from '../squad/ui';
import { GEM } from '../squad/currencyArt';
import sfx from '../audio/sfx';
import { CountUp, FxLayer, PopIn, Rock, SlideIn } from './fx';
import { BtnLabel, Head, SdButton } from './ui';
import { clock } from './levels';
import { F, INK } from './theme';
import { useWide } from '../royal/GameFrame';

// The cards over the board: the result (score creatures popping in one by
// one, each with its own bigger sound; PERFECT's stamp; coins counting up;
// the player's XP bar; a level up), time's up, pause, and Blitz's mistake /
// back-to-level-1 / win cards. The XP pieces are Squish Sort's and Squish
// Stack's too.

// In landscape (REDESIGN_V8, every popup made for the sideways screen) a card is two columns side by side: what comes
// before <Split /> on the left (the title and the picture), the rest on the right (the numbers and the buttons). A card
// without one stays a single column. Upright, Split is nothing.
// `flip` swaps the two sides: the numbers and buttons on the left, the title and picture on the right (the user,
// 2026-11-16, for the STUCK card and every mini-game's result card).
export const Split = () => null;

// The dim scrim and a card springing up into the middle.
// broad: as wide as a two-column card, in one column (Blitz's save card: its buttons in a row)
export function Overlay({ visible, children, tint = 'rgba(50,24,6,0.55)', broad = false, flip = false }) {
  const wide = useWide();
  const t = useRef(new Animated.Value(0)).current;
  const [shown, setShown] = useState(visible);
  useEffect(() => {
    if (visible) {
      setShown(true);
      t.setValue(0);
      Animated.spring(t, { toValue: 1, friction: 6.5, tension: 90, useNativeDriver: true }).start();
    } else if (shown) {
      Animated.timing(t, { toValue: 0, duration: 180, easing: Easing.in(Easing.quad), useNativeDriver: true }).start(() => setShown(false));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible]);
  if (!shown) return null;
  let body = children;
  const list = wide ? React.Children.toArray(children) : null;
  const at = list ? list.findIndex((c) => c && c.type === Split) : -1;
  if (at >= 0) {
    const first = <View key="a" style={styles.wSide}>{list.slice(0, at)}</View>;
    const rest = <View key="b" style={styles.wSide}>{list.slice(at + 1)}</View>;
    body = flip ? [rest, first] : [first, rest];
  }
  const two = at >= 0;
  return (
    <View style={[styles.overlay, wide && styles.overlayWide]} pointerEvents={visible ? 'auto' : 'none'}>
      <Animated.View style={[StyleSheet.absoluteFill, { backgroundColor: tint, opacity: t.interpolate({ inputRange: [0, 1], outputRange: [0, 1], extrapolate: 'clamp' }) }]} />
      <Animated.View
        style={[
          styles.card,
          (two || (wide && broad)) && styles.cardWide,
          {
            opacity: t.interpolate({ inputRange: [0, 0.3, 1], outputRange: [0, 1, 1], extrapolate: 'clamp' }),
            transform: [{ translateY: t.interpolate({ inputRange: [0, 1], outputRange: [120, 0] }) }, { scale: t.interpolate({ inputRange: [0, 1], outputRange: [0.7, 1] }) }],
          },
        ]}
      >
        <View style={styles.cardRim}>
          <LinearGradient colors={['#fffaf0', '#fff1dc', '#f6e2c4']} locations={[0, 0.55, 1]} style={[styles.cardFill, wide && styles.cardFillShort, two && styles.cardFillWide]}>
            {body}
          </LinearGradient>
        </View>
      </Animated.View>
    </View>
  );
}

// One of the result's three score creatures: an earned one bursts in with
// rays and gold sparks; a missed one thuds in grey.
function Slot({ uri, earned, delay, big }) {
  const t = useRef(new Animated.Value(0)).current;
  const fx = useRef(null);
  const size = big ? 78 : 64;
  useEffect(() => {
    const timer = setTimeout(() => {
      if (earned) {
        fx.current?.burst('gold', size / 2 + 8, size / 2 + 8, 1);
      }
    }, delay + 60);
    Animated.spring(t, { toValue: 1, friction: earned ? 3.6 : 7, tension: earned ? 170 : 120, delay, useNativeDriver: true }).start();
    return () => clearTimeout(timer);
  }, [t, delay, earned, size]);
  const scale = t.interpolate({ inputRange: [0, 1], outputRange: [earned ? 0 : 1.6, 1] });
  const rotate = t.interpolate({ inputRange: [0, 1], outputRange: [earned ? '-30deg' : '0deg', '0deg'] });
  return (
    <View style={{ width: size + 16, height: size + 16, alignItems: 'center', justifyContent: 'center', marginTop: big ? -10 : 6 }}>
      <Head uri={uri} size={size} dim ring="rgba(255,255,255,0.7)" bg="rgba(230,200,160,0.35)" style={{ position: 'absolute' }} />
      {earned ? (
        <Animated.View pointerEvents="none" style={{ position: 'absolute', opacity: t }}>
          <RaysSpin size={size * 1.9} color="#ffe45c" opacity={0.6} />
        </Animated.View>
      ) : null}
      <Animated.View style={{ position: 'absolute', opacity: t.interpolate({ inputRange: [0, 0.2, 1], outputRange: [0, 1, 1] }), transform: [{ scale }, { rotate }] }}>
        {earned ? <Head uri={uri} size={size} ring="#ffd23a" bg="#fffbe6" /> : <Head uri={uri} size={size} dim ring="#cfc3dd" bg="#efe9f5" />}
      </Animated.View>
      <FxLayer ref={fx} />
    </View>
  );
}

// "PERFECT!" slamming down like a stamp.
function Stamp({ delay, text = 'PERFECT!' }) {
  const t = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    Animated.timing(t, { toValue: 1, duration: 260, delay, easing: Easing.in(Easing.cubic), useNativeDriver: true }).start();
  }, [t, delay]);
  return (
    <Animated.View
      pointerEvents="none"
      style={[
        styles.stamp,
        {
          opacity: t.interpolate({ inputRange: [0, 0.3, 1], outputRange: [0, 1, 1] }),
          transform: [{ scale: t.interpolate({ inputRange: [0, 1], outputRange: [3.2, 1] }) }, { rotate: t.interpolate({ inputRange: [0, 1], outputRange: ['-30deg', '-9deg'] }) }],
        },
      ]}
    >
      <Text style={styles.stampText}>{text}</Text>
      <Text style={styles.stampSub}>NO ✕ · NO MISTAKES</Text>
    </Animated.View>
  );
}

// The player's XP bar filling from before → after (src/playerXp.js
// playerLevel()s), rolling over on a level up.
export function XpBar({ before, after, delay }) {
  const t = useRef(new Animated.Value(0)).current;
  const [lv, setLv] = useState(before.level);
  useEffect(() => {
    const ups = after.level - before.level;
    // the fill runs through each level up, then to where it lands
    const legs = [];
    let from = before.into / before.need;
    for (let i = 0; i < ups; i++) {
      legs.push([from, 1]);
      from = 0;
    }
    legs.push([from, after.into / after.need]);
    t.setValue(legs[0][0]);
    const timer = setTimeout(() => sfx.play('sd_xpFill'), delay);
    Animated.sequence(
      legs.map(([, to], i) =>
        Animated.sequence([
          ...(i > 0 ? [Animated.timing(t, { toValue: 0, duration: 1, useNativeDriver: false })] : []),
          Animated.timing(t, { toValue: to, duration: 650, delay: i === 0 ? delay : 0, easing: Easing.out(Easing.cubic), useNativeDriver: false }),
        ]),
      ),
    ).start();
    const lvTimers = [];
    for (let i = 1; i <= ups; i++) lvTimers.push(setTimeout(() => setLv(before.level + i), delay + i * 651));
    return () => {
      clearTimeout(timer);
      lvTimers.forEach(clearTimeout);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return (
    <View style={styles.xpRow}>
      <View style={styles.lvBadge}>
        <Text style={styles.lvText}>{lv}</Text>
      </View>
      <View style={styles.xpTrack}>
        <Animated.View style={[styles.xpFill, { width: t.interpolate({ inputRange: [0, 1], outputRange: ['0%', '100%'] }) }]}>
          <LinearGradient colors={['#ffe08a', '#f0a21c']} style={StyleSheet.absoluteFill} start={{ x: 0, y: 0 }} end={{ x: 1, y: 0 }} />
        </Animated.View>
      </View>
      <Text style={styles.xpLabel}>XP</Text>
    </View>
  );
}

// "+40 XP", beside a result's coins
export const XpPlus = ({ xp }) => (xp ? <Text style={styles.xpPlus}>{`+${xp} XP`}</Text> : null);

// Under a result's coins: the XP bar from `delay`, and LEVEL n! when it rose
// (its prize waits on the level road). `res`: { xp, before, after, levelUps }.
export function XpGain({ trigger, res, delay }) {
  useEffect(() => {
    if (!res?.levelUps?.length) return undefined;
    const timer = setTimeout(() => sfx.play('sd_levelUp'), delay + 600);
    return () => clearTimeout(timer);
  }, [trigger, res, delay]);
  if (!res?.xp || !res.before) return null;
  return (
    <>
      <SlideIn trigger={trigger} delay={delay - 200} dy={16}>
        <XpBar key={trigger} before={res.before} after={res.after} delay={delay} />
      </SlideIn>
      {res.levelUps?.length ? (
        <PopIn trigger={trigger} delay={delay + 600} style={styles.levelUp}>
          <Rock deg={3}>
            <Text style={styles.levelUpText}>{`LEVEL ${res.after.level}!`}</Text>
            <Text style={styles.levelUpSub}>A new prize on your level road</Text>
          </Rock>
        </PopIn>
      ) : null}
    </>
  );
}

// The result: `res` = { stars, perfect, ms, mistakes, levelCoins, coins, xp,
// levelUps, before, after, unlocked }, `spec` the level, `uris` the three
// score creatures.
export function ResultCard({ visible, res, spec, uris, onNext, onReplay, onMap, last }) {
  const [key, setKey] = useState(0);
  useEffect(() => {
    if (!visible || !res) return undefined;
    setKey((k) => k + 1);
    const timers = [];
    const at = (ms, fn) => timers.push(setTimeout(fn, ms));
    const STEP = 520;
    for (let i = 0; i < 3; i++) at(420 + i * STEP, () => sfx.play(i < res.stars ? `sd_star${i + 1}` : 'sd_starEmpty'));
    if (res.perfect) at(420 + 3 * STEP, () => sfx.play('sd_perfect'));
    return () => timers.forEach(clearTimeout);
  }, [visible, res]);
  if (!res) return <Overlay visible={false} />;
  const STEP = 520;
  const cAt = coinsAt(res);
  const titles = res.perfect ? 'PERFECT SQUISH!' : res.stars === 3 ? 'SQUISHTASTIC!' : res.stars === 2 ? 'NICE ONE!' : 'SOLVED!';
  return (
    <Overlay visible={visible} flip>
      <PopIn trigger={key} delay={80}>
        <OutlinedTitle text={titles} size={res.perfect ? 26 : 30} fill={res.perfect ? 'gold' : 'pink'} />
      </PopIn>
      <Text style={styles.sub}>{`LEVEL ${spec.lv} · ${spec.n}×${spec.n} · ${clock(res.ms)}${res.mistakes ? ` · ${res.mistakes} MISTAKE${res.mistakes > 1 ? 'S' : ''}` : ''}${spec.head ? ' · HEAD START' : ''}`}</Text>
      <View key={key} style={styles.slots}>
        {[0, 1, 2].map((i) => (
          <Slot key={i} uri={uris[i]} earned={i < res.stars} delay={420 + i * STEP} big={i === 1} />
        ))}
        {res.perfect ? <Stamp delay={420 + 3 * STEP - 120} /> : null}
      </View>
      <Split />
      <SlideIn trigger={key} delay={cAt - 150} dy={20} style={styles.rewardRow}>
        <CoinIcon size={26} />
        <CountUp value={res.levelCoins} delay={cAt} ms={800} style={styles.coins} prefix="+" onTick={() => sfx.play('sd_coinTick')} />
        {res.perfect ? <Text style={styles.mult}>×3</Text> : null}
        <XpPlus xp={res.xp} />
      </SlideIn>
      <XpGain trigger={key} res={res} delay={cAt + 500} />
      {res.unlocked ? (
        <PopIn trigger={key} delay={cAt + 600}>
          <Text style={styles.unlocked}>{`LEVEL ${spec.lv + 1} UNLOCKED`}</Text>
        </PopIn>
      ) : null}
      <SlideIn trigger={key} delay={420 + 2 * STEP + 300} dy={30} style={styles.btnRow}>
        <SdButton kind="white" onPress={onMap} sound="sd_select" faceStyle={styles.smallFace}>
          <BtnLabel kind="white" size={15}>MAP</BtnLabel>
        </SdButton>
        <SdButton kind="purple" onPress={onReplay} sound="sd_select" faceStyle={styles.smallFace}>
          <BtnLabel kind="purple" size={15}>REPLAY</BtnLabel>
        </SdButton>
        {!last ? (
          <SdButton kind="green" onPress={onNext} sound="sd_select" faceStyle={styles.bigFace}>
            <BtnLabel kind="green" size={18}>NEXT ▶</BtnLabel>
          </SdButton>
        ) : null}
      </SlideIn>
    </Overlay>
  );
}
const coinsAt = (res) => 420 + 3 * 520 + (res.perfect ? 600 : 100);

// Time's up: the timer-creature snoozing, RETRY or MAP.
// The clock ran out, or (`hearts`: the hearts were all lost) the mistakes did.
export function TimeUpCard({ visible, onRetry, onMap, uri, hearts = 0 }) {
  return (
    <Overlay visible={visible} tint="rgba(80,0,30,0.5)">
      <PopIn trigger={visible} delay={60}>
        <OutlinedTitle text={hearts ? 'OUT OF HEARTS!' : "TIME'S UP!"} size={hearts ? 28 : 32} fill="pink" />
      </PopIn>
      <PopIn trigger={visible} delay={220}>
        <Rock deg={8} ms={1800}>
          <Head uri={uri} size={86} dim ring="#ffffff" bg="#f3e6d4" />
        </Rock>
      </PopIn>
      <Split />
      <Text style={styles.body}>{hearts ? `${hearts} mistakes. The same board is waiting: think it through and try again!` : 'The same board is waiting. Try it again!'}</Text>
      <SlideIn trigger={visible} delay={380} style={styles.btnRow}>
        <SdButton kind="white" onPress={onMap} sound="sd_select" faceStyle={styles.smallFace}>
          <BtnLabel kind="white" size={15}>MAP</BtnLabel>
        </SdButton>
        <SdButton kind="pink" onPress={onRetry} sound="sd_select" faceStyle={styles.bigFace}>
          <BtnLabel kind="pink" size={18}>RETRY</BtnLabel>
        </SdButton>
      </SlideIn>
    </Overlay>
  );
}

export function PauseCard({ visible, onResume, onRestart, onQuit, blitz }) {
  return (
    <Overlay visible={visible}>
      <PopIn trigger={visible} delay={40}>
        <OutlinedTitle text="PAUSED" size={32} fill="pink" />
      </PopIn>
      <Text style={styles.body}>{blitz ? 'The Blitz clock is frozen. Take a breath!' : 'The clock is frozen. The board is hidden so no peeking!'}</Text>
      <Split />
      <View style={styles.col}>
        <SdButton kind="green" onPress={onResume} sound="sd_resume" faceStyle={styles.wideFace}>
          <BtnLabel kind="green" size={18}>RESUME</BtnLabel>
        </SdButton>
        <SdButton kind="purple" onPress={onRestart} sound="sd_select" faceStyle={styles.wideFace}>
          <BtnLabel kind="purple" size={16}>{blitz ? 'START OVER' : 'RESTART'}</BtnLabel>
        </SdButton>
        <SdButton kind="white" onPress={onQuit} sound="sd_boardOut" faceStyle={styles.wideFace}>
          <BtnLabel kind="white" size={16}>QUIT</BtnLabel>
        </SdButton>
      </View>
    </Overlay>
  );
}

// where a lost run goes back to (Squishdoku's says level; the others theirs)
const backTo = (unit) => (unit === 'board' ? 'BACK TO LEVEL 1' : `BACK TO ${unit.toUpperCase()} 1`);

// Blitz: a mistake with the revive still unused. (Squish Sort's and Squish
// Stack's Blitz too: `unit` is what a step of the run is called.)
export function ReviveCard({ visible, board, unit = 'board', adReady, adsFree, onRevive, onGiveUp }) {
  return (
    <Overlay visible={visible} tint="rgba(80,0,30,0.5)">
      <PopIn trigger={visible} delay={40}>
        <OutlinedTitle text="OOPS!" size={34} fill="pink" />
      </PopIn>
      <Text style={styles.body}>{`That's a mistake. Keep your run going on ${unit} ${board}?`}</Text>
      <Text style={styles.small}>One revive per run</Text>
      <Split />
      <View style={styles.col}>
        <SdButton kind="gold" onPress={onRevive} disabled={!adsFree && !adReady} sound="sd_select" faceStyle={styles.wideFace}>
          <BtnLabel kind="gold" size={17}>{adsFree ? 'REVIVE (FREE)' : adReady ? '▶ WATCH AD TO REVIVE' : 'LOADING AD…'}</BtnLabel>
        </SdButton>
        <SdButton kind="white" onPress={onGiveUp} sound="sd_select" faceStyle={styles.wideFace}>
          <BtnLabel kind="white" size={15}>{backTo(unit)}</BtnLabel>
        </SdButton>
      </View>
    </Overlay>
  );
}

// Blitz: the run is over (a mistake, or the clock).
export function BlitzFailCard({ visible, reason, reached, best, unit = 'board', onAgain, onQuit }) {
  return (
    <Overlay visible={visible} tint="rgba(80,0,30,0.55)">
      <PopIn trigger={visible} delay={40} from={2.4} twist={-12} friction={6} tension={160}>
        <OutlinedTitle text={`${backTo(unit)}!`} size={26} fill="pink" />
      </PopIn>
      <Text style={styles.body}>{reason === 'time' ? 'The Blitz clock ran out.' : 'One mistake sends the whole squad home.'}</Text>
      <Text style={styles.small}>{`You reached ${unit} ${reached} · best ${Math.max(best, reached)}`}</Text>
      <Split />
      <View style={styles.col}>
        <SdButton kind="cyan" onPress={onAgain} sound="sd_select" faceStyle={styles.wideFace}>
          <BtnLabel kind="cyan" size={18}>GO AGAIN</BtnLabel>
        </SdButton>
        <SdButton kind="white" onPress={onQuit} sound="sd_boardOut" faceStyle={styles.wideFace}>
          <BtnLabel kind="white" size={15}>QUIT</BtnLabel>
        </SdButton>
      </View>
    </Overlay>
  );
}

// Squishdoku's BLITZ MODE (2026-11-16, the user): a mistake or the clock ended the run. Save it — once with an ad (free
// with Remove Ads), or with gems (`price`, twice the last each time) — and go on from board `board`; or END RUN.
// (Squish Stack's and Squish Hop's BLITZ MODE too: `unit` is what a step of the run is called, `why` a line on what
// went wrong.)
export function BlitzSaveCard({ visible, reason, board, cleared, unit = 'board', why, adUsed, adReady, adsFree, gems, price, saving, error, onAd, onGems, onEnd }) {
  const short = gems < price;
  // (the user, 2026-11-16: the words in the middle of the card, the buttons below them in one row and 8% shorter;
  // the ad's button only until it's been used once in the run)
  return (
    <Overlay visible={visible} tint="rgba(80,0,30,0.5)" broad>
      <PopIn trigger={visible} delay={40}>
        <OutlinedTitle text={reason === 'time' ? "TIME'S UP!" : 'OOPS!'} size={34} fill="pink" />
      </PopIn>
      {why ? <Text style={styles.small}>{why}</Text> : null}
      <Text style={styles.body}>{`Save your run and keep going on ${unit} ${board}?`}</Text>
      <Text style={styles.small}>{`${cleared} ${unit}${cleared === 1 ? '' : 's'} cleared so far`}</Text>
      <View style={styles.saveRow}>
        {adUsed ? null : (
          <SdButton kind="gold" onPress={onAd} disabled={saving || (!adsFree && !adReady)} sound="sd_select" style={styles.saveBtn} faceStyle={styles.rowFace}>
            <BtnLabel kind="gold" size={15}>{adsFree ? 'SAVE IT (FREE)' : adReady ? '▶ WATCH AD TO SAVE' : 'LOADING AD…'}</BtnLabel>
          </SdButton>
        )}
        <View style={styles.saveBtn}>
          <SdButton kind="cyan" onPress={onGems} disabled={saving || short} sound="sd_select" faceStyle={styles.rowFace}>
            <BtnLabel kind={saving || short ? 'grey' : 'cyan'} size={15}>{saving ? 'SAVING…' : 'SAVE FOR'}</BtnLabel>
            {saving ? null : <Image source={GEM[0]} style={styles.gem} />}
            {saving ? null : <BtnLabel kind={short ? 'grey' : 'cyan'} size={15}>{String(price)}</BtnLabel>}
          </SdButton>
          {error || short ? <Text style={styles.err}>{error || `You have ${gems} gems`}</Text> : null}
        </View>
        <SdButton kind="white" onPress={onEnd} disabled={saving} sound="sd_select" style={styles.saveBtn} faceStyle={styles.rowFace}>
          <BtnLabel kind="white" size={15}>END RUN</BtnLabel>
        </SdButton>
      </View>
    </Overlay>
  );
}

// Squishdoku's BLITZ MODE: GAME OVER — the boards the run cleared, the best, and what they paid. (The user,
// 2026-11-16: no score creatures, Blitz has no stars; the title on top of everything; QUIT and PLAY AGAIN the same size,
// PLAY AGAIN in the gold of the app's other buttons.)
export function BlitzOverCard({ visible, res, unit = 'board', onAgain, onQuit }) {
  if (!res) return <Overlay visible={false} />;
  return (
    <Overlay visible={visible} tint="rgba(80,0,30,0.55)" broad>
      <PopIn trigger={visible} delay={40} from={2.4} twist={-12} friction={6} tension={160}>
        <OutlinedTitle text="GAME OVER" size={32} fill={res.newTop ? 'gold' : 'pink'} />
      </PopIn>
      <Text style={styles.sub}>{`${unit.toUpperCase()}S CLEARED`}</Text>
      <CountUp value={res.cleared} delay={300} ms={700} style={styles.bigNum} />
      <Text style={[styles.unlocked, !res.newTop && styles.bestPlain]}>{res.newTop ? 'NEW BEST!' : `BEST ${res.best}`}</Text>
      {res.coins ? (
        <SlideIn trigger={visible} delay={700} dy={20} style={styles.rewardRow}>
          <CoinIcon size={26} />
          <CountUp value={res.coins} delay={800} ms={1000} style={styles.coins} prefix="+" onTick={() => sfx.play('sd_coinTick')} ticks={12} />
          <XpPlus xp={res.xp} />
        </SlideIn>
      ) : null}
      {res.xp ? <XpGain trigger={visible} res={res} delay={1300} /> : null}
      <SlideIn trigger={visible} delay={900} dy={30} style={styles.overBtns}>
        <SdButton kind="white" onPress={onQuit} sound="sd_boardOut" faceStyle={styles.overFace}>
          <BtnLabel kind="white" size={17}>QUIT</BtnLabel>
        </SdButton>
        <SdButton kind="gold" onPress={onAgain} sound="sd_select" faceStyle={styles.overFace}>
          <BtnLabel kind="gold" size={17}>PLAY AGAIN</BtnLabel>
        </SdButton>
      </SlideIn>
    </Overlay>
  );
}

// (Squish Sort's and Squish Stack's too)
export function BlitzWinCard({ visible, res, uris, unit = 'board', onAgain, onQuit }) {
  const fx = useRef(null);
  useEffect(() => {
    if (!visible) return undefined;
    const timers = [0, 450, 900].map((ms, i) => setTimeout(() => fx.current?.burst('confetti', 60 + i * 80, 40, 1), ms));
    return () => timers.forEach(clearTimeout);
  }, [visible]);
  if (!res) return <Overlay visible={false} />;
  return (
    <Overlay visible={visible} flip>
      <PopIn trigger={visible} delay={60}>
        <OutlinedTitle text="BLITZ CHAMPION!" size={26} fill="gold" />
      </PopIn>
      <Text style={styles.sub}>{`10 ${unit.toUpperCase()}S · ${clock(res.ms)}${res.newBest ? ' · NEW BEST!' : ''}`}</Text>
      <View style={styles.slots}>
        {[0, 1, 2].map((i) => (
          <Slot key={i} uri={uris[i]} earned delay={300 + i * 300} big={i === 1} />
        ))}
      </View>
      <Split />
      <SlideIn trigger={visible} delay={1300} dy={20} style={styles.rewardRow}>
        <CoinIcon size={26} />
        <CountUp value={res.coins} delay={1400} ms={1100} style={styles.coins} prefix="+" onTick={() => sfx.play('sd_coinTick')} ticks={14} />
        <XpPlus xp={res.xp} />
      </SlideIn>
      <XpGain trigger={visible} res={res} delay={1900} />
      <SlideIn trigger={visible} delay={1700} dy={30} style={styles.btnRow}>
        <SdButton kind="white" onPress={onQuit} sound="sd_boardOut" faceStyle={styles.smallFace}>
          <BtnLabel kind="white" size={15}>DONE</BtnLabel>
        </SdButton>
        <SdButton kind="cyan" onPress={onAgain} sound="sd_select" faceStyle={styles.bigFace}>
          <BtnLabel kind="cyan" size={18}>AGAIN</BtnLabel>
        </SdButton>
      </SlideIn>
      <FxLayer ref={fx} />
    </Overlay>
  );
}


const styles = StyleSheet.create({
  overlay: { ...StyleSheet.absoluteFillObject, alignItems: 'center', justifyContent: 'center', padding: 18, zIndex: 50, elevation: 50 },
  overlayWide: { padding: 10 },
  card: { width: '100%', maxWidth: 380, borderRadius: 30, backgroundColor: '#6b4426', padding: 3, paddingBottom: 8 },
  // landscape: two columns
  cardWide: { maxWidth: 660 },
  cardFillShort: { paddingTop: 12, paddingBottom: 12, gap: 6 },
  cardFillWide: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 20, gap: 22 },
  wSide: { flex: 1, alignItems: 'center', gap: 6 },
  cardRim: { borderRadius: 27, backgroundColor: '#ffffff', padding: 3 },
  cardFill: { borderRadius: 24, paddingHorizontal: 16, paddingTop: 18, paddingBottom: 16, alignItems: 'center', gap: 8 },
  sub: { fontFamily: F.display, fontSize: 12.5, letterSpacing: 0.6, color: '#9a6a3a', textAlign: 'center' },
  slots: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', marginTop: 6, marginBottom: 22 },
  // across the creatures' feet, so they stay in view
  stamp: { position: 'absolute', bottom: -18, alignItems: 'center', borderWidth: 3.5, borderColor: '#e01e4a', borderRadius: 10, paddingHorizontal: 10, paddingVertical: 1, backgroundColor: 'rgba(255,255,255,0.92)' },
  stampText: { fontFamily: F.display, fontSize: 22, color: '#e01e4a', letterSpacing: 2, includeFontPadding: false },
  stampSub: { fontFamily: F.display, fontSize: 8.5, color: '#e01e4a', letterSpacing: 1, marginTop: -2 },
  rewardRow: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  coins: { fontFamily: F.display, fontSize: 28, color: '#b86200', includeFontPadding: false, minWidth: 70 },
  mult: { fontFamily: F.display, fontSize: 16, color: '#e01e4a' },
  xpPlus: { fontFamily: F.display, fontSize: 16, color: '#b8641c', marginLeft: 8 },
  xpRow: { flexDirection: 'row', alignItems: 'center', gap: 8, width: 260 },
  lvBadge: { width: 30, height: 30, borderRadius: 15, backgroundColor: '#b8641c', borderWidth: 2.5, borderColor: '#ffffff', alignItems: 'center', justifyContent: 'center' },
  lvText: { fontFamily: F.display, fontSize: 14, color: '#ffffff', includeFontPadding: false },
  xpTrack: { flex: 1, height: 14, borderRadius: 999, backgroundColor: 'rgba(107,68,38,0.18)', borderWidth: 2, borderColor: '#ffffff', overflow: 'hidden' },
  xpFill: { height: '100%', borderRadius: 999, overflow: 'hidden' },
  xpLabel: { fontFamily: F.display, fontSize: 9.5, color: '#9a6a3a', width: 54 },
  levelUp: { alignItems: 'center', backgroundColor: '#b8641c', borderRadius: 16, paddingHorizontal: 14, paddingVertical: 5, borderWidth: 3, borderColor: '#ffffff' },
  levelUpText: { fontFamily: F.display, fontSize: 17, color: '#ffffff', textAlign: 'center' },
  levelUpSub: { fontFamily: F.heavy, fontSize: 11, color: '#ffe9cc', textAlign: 'center' },
  unlocked: { fontFamily: F.display, fontSize: 13, color: '#16a86a', letterSpacing: 0.8 },
  btnRow: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'center', gap: 8, marginTop: 6 },
  col: { alignSelf: 'stretch', gap: 4, marginTop: 6 },
  smallFace: { paddingHorizontal: 14, paddingVertical: 10 },
  bigFace: { paddingHorizontal: 22, paddingVertical: 12 },
  wideFace: { paddingVertical: 12 },
  body: { fontFamily: F.heavy, fontSize: 14, color: INK, textAlign: 'center', paddingHorizontal: 6 },
  small: { fontFamily: F.bold, fontSize: 12, color: '#9a6a3a', textAlign: 'center' },
  gem: { width: 22, height: 22, resizeMode: 'contain', marginHorizontal: -3 },
  // Blitz's save card: the three buttons in a row, each 8% shorter than they were (wideFace with a 17 label: 55.6 tall
  // in its ring; with a 15 label, 11 above and below makes 51.2)
  saveRow: { flexDirection: 'row', alignItems: 'flex-start', alignSelf: 'stretch', justifyContent: 'center', gap: 10, marginTop: 10 },
  saveBtn: { flex: 1, maxWidth: 220 },
  rowFace: { paddingVertical: 11, paddingHorizontal: 8 },
  // the GAME OVER card's two buttons, the same size
  overBtns: { flexDirection: 'row', justifyContent: 'center', gap: 12, marginTop: 6 },
  overFace: { width: 190, paddingVertical: 11, paddingHorizontal: 8 },
  err: { fontFamily: F.heavy, fontSize: 12, color: '#e01e4a', textAlign: 'center' },
  bigNum: { fontFamily: F.display, fontSize: 40, color: '#b86200', includeFontPadding: false, textAlign: 'center', minWidth: 80 },
  bestPlain: { color: '#9a6a3a' },
});
