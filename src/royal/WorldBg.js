import React, { memo } from 'react';
import { Animated, Image, StyleSheet } from 'react-native';

// The mini-games' level screens' background (2026-11-16, the user: the levels screen's background should match the
// map's design): the main menu's world, the picture behind the house and the Game Park, without the house. Rendered
// to pictures by webgame/tools/levels-bg.mjs, one per season. The menu's world changes with the month
// (main-menu-scene.js autoSeason), and so does this.
const PICS = {
  sunny: require('../../assets/royal/levels-sunny.jpg'),
  rainy: require('../../assets/royal/levels-rainy.jpg'),
  snowy: require('../../assets/royal/levels-snowy.jpg'),
};
const seasonNow = () => {
  const mo = new Date().getMonth();
  return mo === 11 || mo <= 1 ? 'snowy' : mo >= 4 && mo <= 8 ? 'sunny' : 'rainy';
};

export const WorldBg = memo(function WorldBg({ style }) {
  // (width and height too: on web an Image without them takes the picture's own size, 2340 x 1080)
  return <Image source={PICS[seasonNow()]} resizeMode="cover" fadeDuration={0} style={[StyleSheet.absoluteFill, styles.full, style]} />;
});

const styles = StyleSheet.create({ full: { width: '100%', height: '100%' } });

// Behind a game's level map while it's up (`on`), fading in as the map slides in (sw: useViewSwitch). Drawn first in
// the screen, under its top bar.
export function MapBackdrop({ sw, on }) {
  if (!on) return null;
  return (
    <Animated.View pointerEvents="none" style={[StyleSheet.absoluteFill, { opacity: sw.style.opacity }]}>
      <WorldBg />
    </Animated.View>
  );
}
