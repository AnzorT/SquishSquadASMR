// The pictures on Squish Stack's and Squish Hop's mode cards (their hubs, 2026-11-16, the user: "the same thing" as
// Squishdoku's hub). Like tools/squishdoku/mode-art.mjs: a frame of the game's Game Park clip
// (webgame/app/previews/<game>.mp4), as it is for REGULAR MODE, and for BLITZ MODE cooled to an electric blue with speed
// streaks and two gold lightning bolts.
// ffmpeg takes the frame (Playwright's Chromium can't decode the clips' H.264); Chromium paints the Blitz one.
//
//   node tools/games/mode-art.mjs
//     -> assets/stack/mode-regular.jpg, assets/stack/mode-blitz.jpg, assets/hop/mode-regular.jpg, assets/hop/mode-blitz.jpg
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
// the frame of each clip: Stack's tower almost built, Hop's board before the first hop
const GAMES = [
  { key: 'stack', at: 4.9 },
  { key: 'hop', at: 0.3 },
];

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'mode-art-'));
const browser = await chromium.launch();
const page = await browser.newPage();
for (const { key, at } of GAMES) {
  const png = path.join(tmp, `${key}.png`);
  execFileSync('ffmpeg', ['-v', 'error', '-y', '-ss', String(at), '-i', path.join(REPO, `webgame/app/previews/${key}.mp4`), '-frames:v', '1', png]);
  const src = `data:image/png;base64,${fs.readFileSync(png).toString('base64')}`;
  const shots = await page.evaluate(async (src) => {
    const img = new Image();
    img.src = src;
    await img.decode();
    const W = img.naturalWidth;
    const H = img.naturalHeight;
    const make = () => {
      const c = document.createElement('canvas');
      c.width = W;
      c.height = H;
      const x = c.getContext('2d');
      x.drawImage(img, 0, 0, W, H);
      return [c, x];
    };
    const [reg] = make();
    const [bz, x] = make();
    // (the same Blitz look as Squishdoku's: tools/squishdoku/mode-art.mjs)
    x.globalCompositeOperation = 'multiply';
    x.fillStyle = '#9cc2ff';
    x.fillRect(0, 0, W, H);
    x.globalCompositeOperation = 'source-over';
    const vg = x.createRadialGradient(W / 2, H / 2, H * 0.3, W / 2, H / 2, W * 0.62);
    vg.addColorStop(0, 'rgba(8,30,90,0)');
    vg.addColorStop(1, 'rgba(10,40,120,0.6)');
    x.fillStyle = vg;
    x.fillRect(0, 0, W, H);
    x.save();
    x.translate(W / 2, H / 2);
    x.rotate(-0.12);
    const streaks = [[-0.42, 0.06, 0.5], [-0.3, 0.035, 0.42], [-0.12, 0.05, 0.6], [0.08, 0.03, 0.38], [0.24, 0.055, 0.55], [0.38, 0.035, 0.45]];
    streaks.forEach(([y, h, len], i) => {
      const sy = y * H;
      const sh = h * H * 0.35;
      const sx = (i % 2 ? -0.55 : 0.05) * W;
      const sl = len * W;
      const g = x.createLinearGradient(sx, 0, sx + sl, 0);
      g.addColorStop(0, 'rgba(255,255,255,0)');
      g.addColorStop(0.6, 'rgba(210,240,255,0.55)');
      g.addColorStop(1, 'rgba(255,255,255,0)');
      x.fillStyle = g;
      x.fillRect(sx, sy, sl, sh);
    });
    x.restore();
    const BOLT = 'M58 0 L18 70 L44 70 L28 128 L84 46 L56 46 L74 0 Z';
    const bolt = (cx, cy, s, rot) => {
      x.save();
      x.translate(cx, cy);
      x.rotate(rot);
      x.scale(s, s);
      x.translate(-50, -64);
      const p = new Path2D(BOLT);
      x.shadowColor = 'rgba(255,214,90,0.95)';
      x.shadowBlur = 40 / s;
      x.lineJoin = 'round';
      x.strokeStyle = '#6b4426';
      x.lineWidth = 9;
      x.stroke(p);
      x.shadowBlur = 0;
      const g = x.createLinearGradient(0, 0, 0, 128);
      g.addColorStop(0, '#fffbe6');
      g.addColorStop(0.35, '#ffe57e');
      g.addColorStop(0.7, '#ffbe2e');
      g.addColorStop(1, '#f08c10');
      x.fillStyle = g;
      x.fill(p);
      x.strokeStyle = 'rgba(255,255,255,0.8)';
      x.lineWidth = 2.5;
      x.stroke(new Path2D('M56 6 L24 64'));
      x.restore();
    };
    bolt(W * 0.17, H * 0.5, 2.2, -0.18);
    bolt(W * 0.84, H * 0.47, 1.9, 0.16);
    return [reg.toDataURL('image/jpeg', 0.88), bz.toDataURL('image/jpeg', 0.88)];
  }, src);
  const out = path.join(REPO, 'assets', key);
  fs.mkdirSync(out, { recursive: true });
  ['regular', 'blitz'].forEach((name, i) => {
    const f = path.join(out, `mode-${name}.jpg`);
    fs.writeFileSync(f, Buffer.from(shots[i].split(',')[1], 'base64'));
    console.log(path.relative(REPO, f), `${Math.round(fs.statSync(f).size / 1024)} KB`);
  });
}
await browser.close();
fs.rmSync(tmp, { recursive: true, force: true });
