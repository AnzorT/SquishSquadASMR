// Every state of the Main Menu that's a picture per value, made by the game's own code:
//   names      the name plate per creature (plaque, stars and lettering follow its rarity and name) -> Names/
//   levels     the crest's number, levels 1..200                                                     -> Levels/
//   creatures  each creature standing on the menu, still (until the rig is ported)        -> Art/Generated (not in git)
//   scenes     the scene per level (1..30) and season (sunny, rainy, snowy)                 -> Art/Generated (not in git)
// Each kind gets an index (where each picture goes on the A15) next to layout.json, which Unity reads.
//
//   node tools/unity-port/export-menu-states.mjs                 all four
//   node tools/unity-port/export-menu-states.mjs names levels    some
import fs from 'node:fs';
import path from 'node:path';
import { PNG, MENU_OUT, SCENES_OUT, REPO, SEASONS, needBuild, launch, openMenu, openScenePage, drawScene, shootScene, shootPart, markCrestNumber, showAll, sceneAt } from './lib/menu-page.mjs';

const CREATURES_OUT = path.join(path.dirname(SCENES_OUT), 'MenuCreatures');
const MAX_LEVEL = 200;
const want = process.argv.slice(2);
const doIt = k => !want.length || want.includes(k);

needBuild();
const browser = await launch();
const writeIndex = (file, items) => {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify({ items }, null, 1));
  console.log(`  ${path.relative(REPO, file)}: ${items.length}`);
};
const save = (file, buf) => { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, buf); };

if (doIt('names') || doIt('levels') || doIt('creatures')) {
  const { ctx, page } = await openMenu(browser);
  const roster = await page.evaluate(() => window.SquishMenu._x.Q.ROSTER.map((c, id) => c && ({ id, key: c.key, name: c.name, rar: c.rar | 0 })).filter(Boolean));
  console.log(`roster: ${roster.length} creatures`);

  // Show a creature the menu's own way: its lobby says which one is selected (Lb.selected()), and the name plate and
  // the creature are drawn from that. Lb.select() ignores creatures the player doesn't own, so the answer is set
  // directly instead; the drawing is then checked to be that creature's.
  const select = (id, withRig) => page.evaluate(({ id, withRig }) => {
    const x = window.SquishMenu._x;
    if (!x.Lb._sel0) x.Lb._sel0 = x.Lb.selected;
    x.Lb.selected = () => id;
    if (withRig) x.mountRig(); else x.nameDraw();
    return (document.querySelector('.mm-name text') || {}).textContent || '';
  }, { id, withRig });

  if (doIt('names')) {
    const items = [];
    for (const c of roster) {
      const drawn = await select(c.id, false);
      if (drawn.trim() !== c.name.trim()) { console.log(`  ${c.key}: the plate says "${drawn}", not "${c.name}" (left out)`); continue; }
      await page.waitForTimeout(60);
      const shot = await shootPart(page, { sel: '.mm-name' });
      await showAll(page);
      if (!shot) { console.log(`  ${c.key}: no name plate`); continue; }
      save(path.join(MENU_OUT, 'Names', `nameplate-${c.key}.png`), shot.buf);
      items.push({ key: c.key, name: c.name, rar: c.rar, px: shot.px });
    }
    console.log(`names: ${items.length}`);
    writeIndex(path.join(MENU_OUT, 'Names', 'index.json'), items);
  }

  if (doIt('creatures')) {
    const items = [];
    for (const c of roster) {
      const drawn = await select(c.id, true);
      if (drawn.trim() !== c.name.trim()) { console.log(`  ${c.key}: the menu shows "${drawn}", not "${c.name}" (left out)`); continue; }
      // the rig loads its art, then draws on the next frames: let game time pass
      await page.waitForTimeout(400);
      for (let t = 0; t < 3000; t += 500) { await page.clock.runFor(500); await page.waitForTimeout(60); }
      const shot = await shootPart(page, { sel: '.mm-stage' });
      await showAll(page);
      if (!shot) { console.log(`  ${c.key}: nothing drawn`); continue; }
      save(path.join(CREATURES_OUT, `creature-${c.key}.png`), shot.buf);
      items.push({ key: c.key, px: shot.px, sceneAt: sceneAt(shot.px) });
    }
    console.log(`creatures: ${items.length}`);
    writeIndex(path.join(MENU_OUT, 'creatures.json'), items);
  }

  if (doIt('levels')) {
    const items = [];
    for (let n = 1; n <= MAX_LEVEL; n++) {
      // the crest drawn for level n the way the menu's HUD draws it (menu.js SquishMenu.crest over main-menu.js crest)
      await page.evaluate(n => { document.querySelector('.sq-crest').innerHTML = window.SquishMenu.crest(n, window.MainMenu.crest); }, n);
      await markCrestNumber(page);
      const shot = await shootPart(page, { sel: '.sq-crest', only: 'text.sq-num' });
      await showAll(page);
      if (!shot) { console.log(`  level ${n}: nothing drawn`); continue; }
      save(path.join(MENU_OUT, 'Levels', `crest-level-${n}.png`), shot.buf);
      items.push({ key: String(n), px: shot.px });
    }
    console.log(`levels: ${items.length}`);
    writeIndex(path.join(MENU_OUT, 'Levels', 'index.json'), items);
  }
  await ctx.close();
}

if (doIt('scenes')) {
  const { ctx, page } = await openScenePage(browser);
  // check first: level 1 rainy drawn this way must match the default export (the menu's own drawing)
  await drawScene(page, 1, 'rainy');
  const mine = PNG.sync.read(await shootScene(page));
  const theirs = PNG.sync.read(fs.readFileSync(path.join(MENU_OUT, 'scene-L1-rainy.png')));
  let off = 0;
  for (let i = 0; i < mine.data.length; i++) if (Math.abs(mine.data[i] - theirs.data[i]) > 2) off++;
  console.log(`scenes: check against the menu's own level 1 rainy: ${off} channel values differ (of ${mine.data.length})`);
  for (let L = 1; L <= 30; L++) for (const season of SEASONS) {
    await drawScene(page, L, season);
    save(path.join(SCENES_OUT, `scene-L${L}-${season}.png`), await shootScene(page));
  }
  console.log(`scenes: 90 -> ${path.relative(REPO, SCENES_OUT)}`);
  await ctx.close();
}
await browser.close();
