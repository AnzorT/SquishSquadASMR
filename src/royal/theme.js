// The Nov 13 Main Menu's look (webgame: main-menu.js, the CSS at its top), for React Native screens: the same colours,
// rims, fills and lettering, so a native screen (the mini-games, Squish Time) matches the menu's WebView pages.
// Sizes are the design's own units: a screen is 393 tall (Stage.js scales them to the phone, like the menu's `k`).

export const INK = '#3d1268';

// a gradient: [[offset 0-1, colour, opacity?], …]
export const RIM = [[0, '#fffbe2'], [0.24, '#ffdd6e'], [0.55, '#f2ac2c'], [0.82, '#b8680c'], [1, '#ffd474']]; // --rimG (top to bottom)
export const TIP = [[0, '#fff6c8'], [0.55, '#f2ac2c'], [1, '#a8600a']]; // the diamond tips (135deg)

// The plaques (.v-vel, .v-gold …): `fill` is a radial ({ cx, cy, rx, ry } in fractions of the box, like CSS
// "130% 160% at 50% -20%") or a linear gradient ({ angle }: 180 = top to bottom); `rim` its border; `gloss`: the white
// shine over the top half; `sparkles`, `diamonds`, `stripes`: the overlays; `ink`: its text colour.
export const PLAQUES = {
  vel: { fill: { radial: { cx: 0.5, cy: -0.2, rx: 1.3, ry: 1.6 }, stops: [[0, '#b07cff'], [0.38, '#7a36dc'], [0.76, '#4a149a'], [1, '#320b74']] }, rim: RIM, sparkles: true, diamonds: true, ink: '#ffffff' },
  gold: { fill: { angle: 180, stops: [[0, '#fff1b0'], [0.4, '#ffd650'], [0.76, '#f6a524'], [1, '#ffc84a']] }, rim: [[0, '#fffbe8'], [0.6, '#e9a530'], [1, '#8a4a06']], gloss: 0.6, sparkles: true, ink: INK },
  bronze: { fill: { angle: 180, stops: [[0, '#ffd7b4'], [0.42, '#ea9a64'], [0.78, '#b45e2c'], [1, '#d98a52']] }, rim: RIM, gloss: 0.45, ink: '#ffffff' },
  wood: { fill: { angle: 180, stops: [[0, '#e9b97c'], [0.45, '#c4874a'], [1, '#8c5426']] }, rim: [[0, '#ffe3b0'], [0.6, '#a8692e'], [1, '#5a3010']], stripes: true, ink: '#ffffff' },
  amy: { fill: { radial: { cx: 0.5, cy: 0, rx: 1.2, ry: 1.4 }, stops: [[0, '#e6c8ff'], [0.4, '#a565ff'], [0.8, '#6420c8'], [1, '#4a148e']] }, rim: RIM, sparkles: true, ink: '#ffffff' },
  pearl: { fill: { radial: { cx: 0.3, cy: 0, rx: 1.3, ry: 1.5 }, stops: [[0, '#ffffff'], [0.35, '#fbf4ff'], [0.7, '#ecdcff'], [1, '#dcc6f6']] }, rim: RIM, sparkles: true, ink: INK },
  rainbow: { fill: { angle: 100, stops: [[0, '#ffc1e6'], [0.25, '#ffe7a0'], [0.5, '#b9f6d8'], [0.75, '#b6e2ff'], [1, '#e2c8ff']] }, rim: [[0, '#ffffff'], [0.5, '#d9c4ff'], [1, '#9a78e8']], sparkles: true, ink: INK },
};

// The lettering (main-menu.js FT): gradient fill + dark outline (+ a white sticker edge for `w`; `h`: left-to-right).
export const LETTERING = {
  gold: { f: ['#fffbe6', '#ffe57e', '#ffbe2e', '#f08c10'], o: INK, w: true },
  goldB: { f: ['#fffbe6', '#ffe57e', '#ffbe2e', '#f08c10'], o: '#4a1f04' },
  white: { f: ['#ffffff', '#ffffff', '#f6eeff', '#e4d4ff'], o: INK },
  whiteB: { f: ['#ffffff', '#ffffff', '#fff6e2', '#ffe6b8'], o: '#6a2c00' },
  wood: { f: ['#fffbea', '#ffe9b8', '#f2c27a', '#d08a3c'], o: '#3a1e0a' },
  bronze: { f: ['#ffffff', '#ffe9d4', '#ffc392', '#ef8f50'], o: '#4a1a06' },
  amethyst: { f: ['#ffffff', '#f5e6ff', '#dcb0ff', '#b273ff'], o: '#2a0a5c' },
  rainbow: { f: ['#ff8fd0', '#ffcf5a', '#7fe9b8', '#7fc4ff', '#c39bff'], o: '#4a1a7a', h: true },
};

// a character's nameplate and lettering by rarity (common .. rainbow), as main-menu.js PLQ / NFT
export const RARITY_PLAQUE = ['wood', 'bronze', 'amy', 'gold', 'rainbow'];
export const RARITY_LETTERING = ['wood', 'bronze', 'amethyst', 'whiteB', 'white'];

// the orb buttons (.mm-orb): a jewel inside a gold rim
export const ORB = [[0, '#ffffff'], [0.07, '#ffffff'], [0.16, '#d6b4ff'], [0.38, '#9a5cf0'], [0.7, '#5a1cb8'], [1, '#330a78']];

// the full-screen pages (.mm-page2)
export const PAGE = { stops: [[0, '#8a46ec'], [0.4, '#5a1cb8'], [0.8, '#2e0a6e'], [1, '#1e0650']] };

export const FONT = { display: 'Fredoka_700Bold', body: 'Nunito_700Bold', bodyHeavy: 'Nunito_800ExtraBold', bodyBlack: 'Nunito_900Black' };

// the white text with an ink outline (.tx-w), as hard shadows for components/candy/ShadowText
export const TX_W = [[0, 1.6, INK], [1.2, 0, INK], [-1.2, 0, INK], [0, -1.2, INK], [1, 1, INK], [-1, 1, INK]];

// the icons main-menu.js draws (24 x 24 strokes)
export const ICON = {
  back: 'M15 5L8 12L15 19',
  left: 'M14 6L8 12L14 18',
  right: 'M10 6L16 12L10 18',
  close: 'M7 7l10 10M17 7L7 17',
  check: 'M5 12.5l4.5 4.5L19 7.5',
  gear: 'M12 8.5a3.5 3.5 0 1 0 .01 0Z M19.4 13a7.6 7.6 0 0 0 0-2l2-1.6-2-3.4-2.4 1a7.4 7.4 0 0 0-1.7-1L15 3.5h-4l-.4 2.5a7.4 7.4 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.6a7.6 7.6 0 0 0 0 2l-2 1.6 2 3.4 2.4-1a7.4 7.4 0 0 0 1.7 1l.4 2.5h4l.4-2.5a7.4 7.4 0 0 0 1.7-1l2.4 1 2-3.4Z',
  house: 'M3 11.5 12 4l9 7.5 M5.5 10.5V20h13v-9.5 M10 20v-5h4v5',
  grid: 'M4 4h7v7H4Z M13 4h7v7h-7Z M4 13h7v7H4Z M13 13h7v7h-7Z',
  shop: 'M4 9h16l-1.5 11h-13Z M8 9V7a4 4 0 0 1 8 0v2',
  pause: 'M9 6v12 M15 6v12',
  restart: 'M4 12a8 8 0 1 0 2.3-5.7 M4 4v4h4',
  hint: 'M9 18h6 M10 21h4 M12 3a6 6 0 0 0-3.5 10.9c.6.4 1 1.1 1 1.9V16h5v-.2c0-.8.4-1.5 1-1.9A6 6 0 0 0 12 3Z',
};
