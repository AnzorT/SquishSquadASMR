// Web, development only: the games' popups on their own, to check their layout without playing a level
// (http://localhost:8081/?phone=1&preview=1 in a window the phone's size, e.g. 832 x 384 for the A15 sideways), and
// the screens a test account can't reach (verify).
// window.__preview('sd-result') shows one card; the names are the keys of CARDS below. Nothing here touches the account.
import React, { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { useFonts } from 'expo-font';
import { Baloo2_500Medium, Baloo2_700Bold, Baloo2_800ExtraBold } from '@expo-google-fonts/baloo-2';
import { Nunito_400Regular, Nunito_600SemiBold, Nunito_700Bold, Nunito_800ExtraBold, Nunito_900Black } from '@expo-google-fonts/nunito';
import { Fredoka_500Medium, Fredoka_600SemiBold, Fredoka_700Bold } from '@expo-google-fonts/fredoka';
import { GameBackground } from '../royal/GameFrame';
import * as SD from '../squishdoku/Overlays';
import SdRules from '../squishdoku/Rules';
import * as SORT from '../sort/Cards';
import * as STACK from '../stack/Cards';
import * as HOP from '../hop/Cards';
import VerifyEmailScreen from '../screens/VerifyEmailScreen';
import VaultLoader from '../components/VaultLoader';
import StackScreen from '../screens/StackScreen';
import HopScreen from '../screens/HopScreen';
import StackGame from '../stack/Game';
import HopGame from '../hop/Game';
import STACK_ART from '../stack/art';

const noop = () => {};
// a number from the page's address (?lv=…), or `d`
const param = (k, d) => {
  const v = new URLSearchParams(window.location.search).get(k);
  return v == null || v === '' || Number.isNaN(Number(v)) ? d : Number(v);
};
const before = { level: 6, into: 560, need: 700 };
const after = { level: 7, into: 40, need: 800 };
const res = { stars: 2, perfect: false, ms: 83000, mistakes: 1, levelCoins: 90, coins: 90, xp: 40, levelUps: [7], before, after, unlocked: true };
const best = { ...res, stars: 3, perfect: true, mistakes: 0, levelCoins: 270, coins: 270 };
const blitz = { ms: 412000, coins: 600, xp: 120, newBest: true, levelUps: [], before, after: { level: 6, into: 680, need: 700 } };
const uris = [null, null, null];
// Squish Stack's and Squish Hop's players without the roster from Firestore: the bundled cut-outs (stack/art.js), the
// first three as the player's own (stack/roster.js castOf's shape)
const CAST = Object.entries(STACK_ART)
  .slice(0, 12)
  .map(([key, a], i) => ({ id: key, key, uri: null, source: a.src, ar: a.ar, box: [0, 0, 1, 1], mine: i < 3 }));
const over = { cleared: 7, best: 9, coins: 420, xp: 140, newTop: false, levelUps: [], before, after: { level: 6, into: 700 - 1, need: 700 } };

// the chest smash's loading screen on its own: the page "paints" after `ms` (?ms=…, 4500 by default), then it plays
// its way out and starts again (window.__vaultReady() makes it ready at once)
function VaultPreview() {
  const ms = Number(new URLSearchParams(window.location.search).get('ms')) || 4500;
  const [run, setRun] = useState(0);
  const [ready, setReady] = useState(false);
  useEffect(() => {
    setReady(false);
    window.__vaultReady = () => setReady(true);
    const t = setTimeout(() => setReady(true), ms);
    return () => clearTimeout(t);
  }, [run, ms]);
  return <VaultLoader key={run} ready={ready} uri={null} onDone={() => setTimeout(() => setRun((n) => n + 1), 900)} />;
}

const CARDS = {
  'sd-result': (v) => <SD.ResultCard visible={v} res={res} spec={{ lv: 12, n: 6 }} uris={uris} onNext={noop} onReplay={noop} onMap={noop} />,
  'sd-perfect': (v) => <SD.ResultCard visible={v} res={best} spec={{ lv: 12, n: 6, head: true }} uris={uris} onNext={noop} onReplay={noop} onMap={noop} />,
  'sd-timeup': (v) => <SD.TimeUpCard visible={v} onRetry={noop} onMap={noop} />,
  'sd-hearts': (v) => <SD.TimeUpCard visible={v} hearts={3} onRetry={noop} onMap={noop} />,
  'sd-pause': (v) => <SD.PauseCard visible={v} onResume={noop} onRestart={noop} onQuit={noop} />,
  'sd-revive': (v) => <SD.ReviveCard visible={v} board={4} adReady onRevive={noop} onGiveUp={noop} />,
  'sd-blitzfail': (v) => <SD.BlitzFailCard visible={v} reason="mistake" reached={4} best={6} onAgain={noop} onQuit={noop} />,
  'sd-blitzwin': (v) => <SD.BlitzWinCard visible={v} res={blitz} uris={uris} onAgain={noop} onQuit={noop} />,
  'sd-rules': (v) => <SdRules visible={v} roster={[]} onClose={noop} />,
  'sort-result': (v) => <SORT.ResultCard visible={v} res={{ ...res, mistakes: 0 }} spec={{ lv: 9, tier: 1, parMs: 40000 }} onNext={noop} onReplay={noop} onHub={noop} />,
  'sort-fail': (v) => <SORT.FailCard visible={v} reason="hearts" onRetry={noop} onHub={noop} />,
  'sort-pause': (v) => <SORT.PauseCard visible={v} onResume={noop} onRestart={noop} onQuit={noop} />,
  'sort-rules': (v) => <SORT.RulesCard visible={v} onClose={noop} />,
  'stack-result': (v) => <STACK.ResultCard visible={v} res={{ ...res, moves: 9, par: 7 }} spec={{ lv: 9, tier: 1 }} onNext={noop} onReplay={noop} onHub={noop} />,
  'stack-pause': (v) => <STACK.PauseCard visible={v} onResume={noop} onRestart={noop} onQuit={noop} />,
  'stack-hint': (v) => <STACK.HintCard visible={v} adReady onWatch={noop} onClose={noop} />,
  'stack-rules': (v) => <STACK.RulesCard visible={v} onClose={noop} />,
  'hop-result': (v) => <HOP.ResultCard visible={v} res={{ ...res, hops: 14, takebacks: 2 }} spec={{ lv: 9, tier: 1, room: 'living' }} nextRoom="Kitchen" onNext={noop} onReplay={noop} onHub={noop} />,
  'hop-stuck': (v) => <HOP.StuckCard visible={v} shut onUndo={noop} onRetry={noop} />,
  'hop-pause': (v) => <HOP.PauseCard visible={v} onResume={noop} onRestart={noop} onQuit={noop} />,
  'hop-hint': (v) => <HOP.HintCard visible={v} adReady onWatch={noop} onClose={noop} />,
  'hop-rules': (v) => <HOP.RulesCard visible={v} onClose={noop} />,
  // BLITZ MODE's save and GAME OVER cards, as Squish Stack and Squish Hop say them
  'stack-save': (v) => <SD.BlitzSaveCard visible={v} unit="puzzle" reason="mistake" board={6} cleared={5} adReady gems={35} price={20} onAd={noop} onGems={noop} onEnd={noop} />,
  'stack-over': (v) => <SD.BlitzOverCard visible={v} unit="puzzle" res={over} onAgain={noop} onQuit={noop} />,
  'hop-save': (v) => <SD.BlitzSaveCard visible={v} reason="mistake" why="That hop left cushions you can't clear." board={4} cleared={3} adUsed gems={5} price={10} onAd={noop} onGems={noop} onEnd={noop} />,
  'hop-over': (v) => <SD.BlitzOverCard visible={v} res={{ ...over, newTop: true, best: 7 }} onAgain={noop} onQuit={noop} />,
  // the hubs (2026-11-16), and the games themselves with the bundled cast (no account: nothing is saved, the ads are
  // free; don't tap a gem save, it calls the server)
  // (the screens with no roster: the hub and the map work, a level waits for the squishies)
  'stack-hub': () => <StackScreen profile={{ stack: { lv: 23, seen: true, blitz: { top: 14 } } }} creatures={[]} uid={null} onBack={noop} />,
  'hop-hub': () => <HopScreen profile={{ hop: { lv: 37, seen: true, blitz: { top: 6 } } }} creatures={[]} uid={null} onBack={noop} />,
  // (&top=… sets the best run, 3 by default: 0 makes the first board cleared a NEW BEST)
  'stack-blitz': () => <StackGame blitz cast={CAST} profile={{ gems: 50, stack: { blitz: { top: param('top', 3) } } }} uid={null} adsFree onExit={noop} />,
  'hop-blitz': () => <HopGame blitz cast={CAST} profile={{ gems: 50, hop: { blitz: { top: param('top', 3) } } }} uid={null} adsFree onExit={noop} />,
  'stack-level': () => <StackGame startLv={param('lv', 5)} cast={CAST} profile={{ stack: { lv: 50, seen: true } }} uid={null} adsFree onExit={noop} />,
  'hop-level': () => <HopGame startLv={param('lv', 8)} cast={CAST} profile={{}} uid={null} adsFree onExit={noop} />,
  // a whole screen: a new account's email check (its buttons only call Firebase when tapped)
  vault: () => <VaultPreview />,
  verify: () => <VerifyEmailScreen email="new.player@example.com" onVerified={noop} onLogout={noop} />,
};

export default function Preview() {
  const [fonts] = useFonts({
    Baloo2_500Medium,
    Baloo2_700Bold,
    Baloo2_800ExtraBold,
    Nunito_400Regular,
    Nunito_600SemiBold,
    Nunito_700Bold,
    Nunito_800ExtraBold,
    Nunito_900Black,
    Fredoka_500Medium,
    Fredoka_600SemiBold,
    Fredoka_700Bold,
  });
  const [name, setName] = useState(new URLSearchParams(window.location.search).get('card') || 'sd-result');
  useEffect(() => {
    window.__preview = setName;
    window.__previewNames = Object.keys(CARDS);
  }, []);
  if (!fonts) return null;
  const card = CARDS[name];
  return (
    <View style={StyleSheet.absoluteFill}>
      <GameBackground style={StyleSheet.absoluteFill} />
      {card ? <SafeAreaProvider key={name}>{card(true)}</SafeAreaProvider> : <Text>{`No card "${name}"`}</Text>}
    </View>
  );
}
