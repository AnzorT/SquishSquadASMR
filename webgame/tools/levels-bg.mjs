// The mini-games' level screens' background (2026-11-16, the user: "change the background of the levels screen to
// something that matches the map screen"): the main menu's world (main-menu-scene.js, the picture behind the house and the
// Game Park), drawn without the house: its sky, sea and grass, trees framing the edges, and the middle left open for
// the row of levels. One picture per season, as the menu's world changes with the month (src/royal/WorldBg.js picks it
// the same way). Rendered once by Chrome to JPEGs at the A15's landscape pixels (2340 x 1080): a picture costs the phone
// one texture, where the scene as live SVG would be redrawn in software on the UI thread.
//
//   node webgame/build.mjs                  (main-menu-scene.js comes from the build)
//   NODE_PATH=<a folder with playwright> node webgame/tools/levels-bg.mjs
//     -> assets/royal/levels-sunny.jpg, levels-rainy.jpg, levels-snowy.jpg
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const SCENE = path.join(REPO, 'android/app/src/main/assets/webgame/main-menu-scene.js');
const OUT = path.join(REPO, 'assets/royal');
const SEASONS = ['sunny', 'rainy', 'snowy'];
// the world at its liveliest (the menu's grows greener with the player's level: bare trees at 1, flowers from ~16)
const LEVEL = 30;

if (!fs.existsSync(SCENE)) throw new Error('no ' + SCENE + ': run node webgame/build.mjs first');
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1170, height: 540 }, deviceScaleFactor: 2 });
await page.setContent('<!doctype html><html><body style="margin:0;overflow:hidden;background:#000"><div id="s" style="width:100vw;height:100vh"></div></body></html>');
// the scene's art comes to parkArt as helpers (its own sky / ground / tree / bush): the park hook draws our picture
await page.evaluate(() => {
  window.SquishMenu = {
    still: true, // no rain or snow falling, no birds: a still picture
    parkArt(st, P) {
      let g = P.sky(st) + P.ground(st);
      // a few trees far back on either side of the horizon
      g += P.tree(128, 262, 0.62, st, 41) + P.tree(214, 254, 0.48, st, 44, { fruit: true }) + P.tree(796, 256, 0.52, st, 43) + P.tree(884, 266, 0.7, st, 42, { fruit: true });
      // big trees and bushes at the edges frame it, as the house scene's do; the middle stays open grass
      g += P.tree(26, 384, 1.5, st, 9, { fruit: true }) + P.tree(990, 404, 1.4, st, 12);
      g += P.bush(78, 468, 1.4, st, 21) + P.bush(930, 458, 1.5, st, 22);
      // the scene's own soft vignette
      g += '<rect x="-60" y="-20" width="1120" height="500" fill="url(#mmVig)"/>';
      return g;
    },
  };
});
await page.addScriptTag({ path: SCENE });
for (const season of SEASONS) {
  await page.evaluate(({ season, level }) => {
    const S = window.MainMenuScene;
    const st = S.state({ level, season });
    st.park = true;
    document.getElementById('s').innerHTML = S.svg(st);
  }, { season, level: LEVEL });
  await page.waitForTimeout(150);
  const file = path.join(OUT, `levels-${season}.jpg`);
  await page.screenshot({ path: file, type: 'jpeg', quality: 82 });
  console.log(path.relative(REPO, file), Math.round(fs.statSync(file).size / 1024) + ' KB');
}
await browser.close();
