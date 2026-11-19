import React, { memo, useEffect, useRef } from 'react';
import { Animated, Easing } from 'react-native';
import { GameHub } from '../squishdoku/Hub';
import { LEVELS, doneIn } from './levels';

// Squish Sort's front page, as the other games' (2026-11-17, the user: "make sure the screens of squish sort matches
// the other game's screen"): the title on its own, REGULAR MODE (the fridge's 50-level map) and BLITZ MODE as the
// design's wood cards with a picture each (tools/sort/mode-art.mjs), tapped as a whole, and HOW TO PLAY with a ?. Only
// the fridge since 2026-11-17 ("lets make it only with food ok ? so delete the toy cupboard").

const PICS = { regular: require('../../assets/sort/mode-regular.jpg'), blitz: require('../../assets/sort/mode-blitz.jpg') };

function Hub({ rec, onRegular, onBlitz, onRules }) {
  return <GameHub title="SQUISH SORT" pics={PICS} lv={rec.fridge.lv} done={doneIn(rec, 'fridge')} total={LEVELS} top={rec.blitz.top} onRegular={onRegular} onBlitz={onBlitz} onRules={onRules} />;
}

export default memo(Hub);

// Doors that open, stay, shut, rattle and open again, forever (the game picker's little fridge).
export function useDoorLoop() {
  const door = useRef(new Animated.Value(1)).current;
  const wob = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    const T = (v, ms, ease = Easing.inOut(Easing.quad), val = door) => Animated.timing(val, { toValue: v, duration: ms, easing: ease, useNativeDriver: true });
    const loop = Animated.loop(
      Animated.sequence([
        T(1, 1400),
        T(0, 380, Easing.in(Easing.quad)),
        Animated.sequence([1, -1, 1, -1, 0].map((v) => T(v, 90, Easing.inOut(Easing.quad), wob))),
        T(0, 200),
        T(1, 420, Easing.out(Easing.back(1.4))),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [door, wob]);
  return { door, rotate: wob.interpolate({ inputRange: [-1, 1], outputRange: ['-5deg', '5deg'] }) };
}
