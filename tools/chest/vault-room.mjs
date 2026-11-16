// The vault room (REDESIGN_V9, 2026-11-16, the user: "where is the vault room?" -> the place every chest is opened):
// the chest smash page (tools/chest/host.html) stands in an underground treasure vault, where "To the treasure vault"
// (src/components/VaultLoader.js) flies in through the cave. A stone wall with a glowing arch behind the chest, two
// wall torches, a stone floor, gold, gems and old chests in the corners. Drawn once here as a picture (the page shows
// it under its canvas; its torches' flicker and the group's banners are CSS on top), at the A15's landscape pixels.
//
//   NODE_PATH=<a folder with playwright> node tools/chest/vault-room.mjs   -> tools/chest/vault-room.jpg
//   node tools/chest/build.mjs                                            (copies it next to host.html)
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const HERE = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(HERE, 'vault-room.jpg');

const W = 1000, H = 460, FLOOR = 300; // the viewBox; the floor starts at y 300 (65%), where the chest stands
let seed = 7;
const rnd = () => ((seed = (seed * 9301 + 49297) % 233280) / 233280);
const r1 = (x) => Math.round(x * 10) / 10;
const OL = '#2a1a10';

// the back wall: rows of stone blocks, each a little different
let wall = '';
for (let row = 0, y = -6; y < FLOOR; row++, y += 36) {
  for (let x = row % 2 ? -48 : -6; x < W; x += 92) {
    const w = 88 + (rnd() - 0.5) * 6, s = rnd();
    const fill = s < 0.33 ? '#6b5646' : s < 0.66 ? '#75604e' : '#614d3e';
    wall += `<rect x="${r1(x)}" y="${y}" width="${r1(w)}" height="32" rx="6" fill="${fill}"/>`
      + `<rect x="${r1(x + 4)}" y="${y + 3}" width="${r1(w - 8)}" height="5" rx="2.5" fill="#ffffff" opacity=".07"/>`
      + (rnd() < 0.18 ? `<path d="M${r1(x + 20 + rnd() * 40)} ${y + 6} l6 9 l-4 7 l7 8" stroke="#3a2a1e" stroke-width="1.6" fill="none" opacity=".7"/>` : '');
  }
}
// the arch behind the chest: a ring of lighter stones round a deep, warmly lit recess with treasure in it
const AX = 500, AY = 300, AR = 168;
let arch = `<path d="M${AX - AR} ${AY} V${AY - 120} A${AR} ${AR} 0 0 1 ${AX + AR} ${AY - 120} V${AY}Z" fill="url(#recess)"/>`;
for (let i = 0; i <= 12; i++) {
  const a = Math.PI + (i / 12) * Math.PI, a2 = Math.PI + ((i + 1) / 12) * Math.PI;
  if (i === 12) break;
  const p = (ang, r) => `${r1(AX + Math.cos(ang) * r)} ${r1(AY - 120 + Math.sin(ang) * r)}`;
  arch += `<path d="M${p(a, AR)} A${AR} ${AR} 0 0 1 ${p(a2, AR)} L${p(a2, AR + 34)} A${AR + 34} ${AR + 34} 0 0 0 ${p(a, AR + 34)}Z" fill="${i % 2 ? '#8d7660' : '#9a836b'}" stroke="${OL}" stroke-width="2.5"/>`;
}
for (const side of [-1, 1]) {
  for (let k = 0; k < 4; k++) arch += `<rect x="${side < 0 ? AX - AR - 34 : AX + AR}" y="${AY - 120 + k * 30}" width="34" height="30" fill="${k % 2 ? '#8d7660' : '#9a836b'}" stroke="${OL}" stroke-width="2.5"/>`;
}
// treasure glowing inside the arch: a heap of gold with sparkles
let heap = '';
for (let i = 0; i < 70; i++) {
  const t = rnd(), x = AX - 140 + rnd() * 280, y = AY - 8 - Math.pow(1 - Math.abs(x - AX) / 150, 1.4) * 70 * rnd();
  heap += `<ellipse cx="${r1(x)}" cy="${r1(y)}" rx="${r1(7 + t * 4)}" ry="${r1(3 + t * 2)}" fill="${t < 0.5 ? '#ffd34a' : '#f2b22c'}" stroke="#9a5a0c" stroke-width="1.1"/>`;
}
for (let i = 0; i < 9; i++) heap += `<path d="M${r1(AX - 120 + rnd() * 240)} ${r1(AY - 20 - rnd() * 70)} l3 -9 l3 9 l9 3 l-9 3 l-3 9 l-3 -9 l-9 -3Z" fill="#fff6c8" opacity=".85"/>`;

// the floor: stone flags in perspective
let floor = `<rect x="0" y="${FLOOR}" width="${W}" height="${H - FLOOR}" fill="url(#floorG)"/>`;
for (const y of [FLOOR, 322, 352, 392, 442]) floor += `<path d="M0 ${y} H${W}" stroke="${OL}" stroke-width="2.4" opacity=".75"/>`;
for (let i = -12; i <= 12; i++) {
  const xb = AX + i * 120, xt = AX + i * 62;
  floor += `<path d="M${xt} ${FLOOR} L${xb} ${H}" stroke="${OL}" stroke-width="2.2" opacity=".6"/>`;
}
floor += `<rect x="0" y="${FLOOR - 4}" width="${W}" height="8" fill="#4a3628"/>`;
floor += `<ellipse cx="${AX}" cy="372" rx="250" ry="62" fill="url(#spot)"/>`;

// the corners: gold piles, gems, a goblet, old chests
function pile(cx, cy, w, h, n) {
  let g = '';
  for (let i = 0; i < n; i++) {
    const t = rnd(), x = cx + (rnd() - 0.5) * w, k = 1 - Math.abs(x - cx) / (w / 2), y = cy - Math.pow(Math.max(0, k), 1.2) * h * rnd();
    g += `<ellipse cx="${r1(x)}" cy="${r1(y)}" rx="${r1(9 + t * 5)}" ry="${r1(4 + t * 2)}" fill="${t < 0.5 ? '#ffd34a' : '#f6b830'}" stroke="#8a4e08" stroke-width="1.3"/>`
      + `<ellipse cx="${r1(x - 2)}" cy="${r1(y - 1)}" rx="${r1(4 + t * 2)}" ry="1.4" fill="#fff6c8" opacity=".7"/>`;
  }
  return g;
}
const gem = (x, y, c, s) => `<g transform="translate(${x} ${y}) scale(${s})"><path d="M-12 -4 L-6 -12 H6 L12 -4 L0 12Z" fill="${c}" stroke="${OL}" stroke-width="2"/><path d="M-12 -4 H12 M-6 -12 L-3 -4 L0 12 L3 -4 L6 -12" stroke="#ffffff" stroke-width="1.3" fill="none" opacity=".55"/></g>`;
const oldChest = (x, y, s, c) => `<g transform="translate(${x} ${y}) scale(${s})"><rect x="-40" y="-26" width="80" height="44" rx="5" fill="${c}" stroke="${OL}" stroke-width="3"/>`
  + `<path d="M-40 -24 Q-40 -54 0 -54 Q40 -54 40 -24Z" fill="${c}" stroke="${OL}" stroke-width="3"/><path d="M-24 -52 V18 M24 -52 V18" stroke="#c9a04a" stroke-width="6"/><path d="M-24 -52 V18 M24 -52 V18" stroke="${OL}" stroke-width="1.2" opacity=".6"/>`
  + `<rect x="-8" y="-28" width="16" height="18" rx="3" fill="#ffd34a" stroke="${OL}" stroke-width="2.4"/></g>`;
const goblet = (x, y) => `<g transform="translate(${x} ${y})"><path d="M-14 -40 H14 Q14 -14 0 -12 Q-14 -14 -14 -40Z" fill="#ffcf3a" stroke="${OL}" stroke-width="2.4"/><path d="M0 -12 V4 M-10 6 H10" stroke="${OL}" stroke-width="5" stroke-linecap="round"/><path d="M0 -12 V4 M-10 6 H10" stroke="#ffcf3a" stroke-width="2.6" stroke-linecap="round"/>${gem(0, -28, '#e8445a', 0.35)}</g>`;
let corners = oldChest(84, 352, 1.05, '#8a5a32') + oldChest(912, 360, 0.95, '#7a4e2a');
corners += pile(110, 452, 260, 70, 90) + pile(890, 452, 260, 64, 90) + pile(250, 456, 120, 22, 22) + pile(760, 456, 120, 22, 22);
corners += gem(60, 418, '#4cc3ff', 1.1) + gem(170, 432, '#e8445a', 0.9) + gem(840, 424, '#6fe07a', 1) + gem(950, 434, '#b07cff', 0.9) + goblet(205, 418) + goblet(805, 414);

// the torches (their flames flicker in CSS on the page, over these)
const torch = (x, y) => `<g transform="translate(${x} ${y})"><circle r="70" fill="url(#torchGlow)"/>`
  + `<path d="M-14 26 H14 L8 46 H-8Z" fill="#5a5a62" stroke="${OL}" stroke-width="2.5"/><rect x="-5" y="-4" width="10" height="34" rx="3" fill="#8a5a32" stroke="${OL}" stroke-width="2.4"/>`
  + `<path d="M-12 -2 H12 L10 6 H-10Z" fill="#6a6a72" stroke="${OL}" stroke-width="2.2"/>`
  + `<path d="M0 -46 C12 -28 14 -16 8 -6 Q0 2 -8 -6 C-14 -16 -10 -30 0 -46Z" fill="#ff8a2a" stroke="#c2410c" stroke-width="2"/><path d="M0 -30 C6 -20 6 -12 2 -6 Q0 -3 -2 -6 C-6 -12 -5 -20 0 -30Z" fill="#ffe680"/></g>`;
const torches = torch(200, 150) + torch(800, 150);

const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" preserveAspectRatio="xMidYMax slice" width="100%" height="100%">
<defs>
  <linearGradient id="wallBg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#22160e"/><stop offset="1" stop-color="#3a2a1e"/></linearGradient>
  <radialGradient id="recess" cx=".5" cy=".85" r=".8"><stop offset="0" stop-color="#ffcf6a"/><stop offset=".35" stop-color="#b8661c"/><stop offset=".75" stop-color="#3a1e0c"/><stop offset="1" stop-color="#1a0e06"/></radialGradient>
  <linearGradient id="floorG" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#6a5442"/><stop offset="1" stop-color="#3e2e22"/></linearGradient>
  <radialGradient id="spot"><stop offset="0" stop-color="#ffe2a0" stop-opacity=".5"/><stop offset="1" stop-color="#ffe2a0" stop-opacity="0"/></radialGradient>
  <radialGradient id="torchGlow"><stop offset="0" stop-color="#ffcf6a" stop-opacity=".55"/><stop offset=".5" stop-color="#ff9a3a" stop-opacity=".18"/><stop offset="1" stop-color="#ff9a3a" stop-opacity="0"/></radialGradient>
  <radialGradient id="vig" cx=".5" cy=".55" r=".75"><stop offset=".55" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity=".55"/></radialGradient>
</defs>
<rect x="-200" y="-100" width="${W + 400}" height="${H + 200}" fill="url(#wallBg)"/>
${wall}${arch}${heap}${floor}${torches}${corners}
<rect x="-200" y="-100" width="${W + 400}" height="${H + 200}" fill="url(#vig)"/>
</svg>`;

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 832, height: 384 }, deviceScaleFactor: 2340 / 832 });
await page.setContent(`<!doctype html><html><body style="margin:0;width:100vw;height:100vh;background:#22160e">${svg}</body></html>`);
await page.screenshot({ path: OUT, type: 'jpeg', quality: 84 });
await browser.close();
console.log(path.relative(process.cwd(), OUT), Math.round(fs.statSync(OUT).size / 1024) + ' KB');
