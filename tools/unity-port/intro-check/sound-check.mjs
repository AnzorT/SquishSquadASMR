// The intro's sounds: IntroSynth's samples (unity/.../IntroSound.cs, written by `dotnet run -c Release -- sounds
// out/sounds.json`) against Chrome's WebAudio rendering the same voices in an OfflineAudioContext, as intro.js builds
// them (oscillator, setValueAtTime + exponentialRampToValueAtTime on its frequency and gain, start / stop).
//   cd tools/unity-port/intro-check && dotnet run -c Release -- sounds out/sounds.json && node sound-check.mjs
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const HERE = path.dirname(fileURLToPath(import.meta.url));
const sounds = JSON.parse(fs.readFileSync(path.join(HERE, 'out/sounds.json'), 'utf8'));
const browser = await chromium.launch();
const page = await browser.newPage();
const rendered = await page.evaluate(async sounds => {
  const out = [];
  for (const s of sounds) {
    const n = s.samples.length, ctx = new OfflineAudioContext(1, n, 44100);
    for (const v of s.voices) {
      const o = ctx.createOscillator(), g = ctx.createGain(), t = v.T0;
      o.type = v.type === 'Triangle' ? 'triangle' : 'sine';
      o.frequency.setValueAtTime(v.F0, t); if (v.F1 !== v.F0) o.frequency.exponentialRampToValueAtTime(v.F1, t + v.Dur);
      g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(v.Vol, t + v.Attack); g.gain.exponentialRampToValueAtTime(0.0001, t + v.Dur);
      o.connect(g); g.connect(ctx.destination); o.start(t); o.stop(t + v.Dur + 0.05);
    }
    out.push(Array.from((await ctx.startRendering()).getChannelData(0)));
  }
  return out;
}, sounds);
await browser.close();
let worst = 0;
sounds.forEach((s, i) => {
  const a = s.samples, b = rendered[i];
  let e = 0, p = 0, m = 0;
  for (let k = 0; k < a.length; k++) { const d = a[k] - b[k]; e += d * d; p += b[k] * b[k]; m = Math.max(m, Math.abs(d)); }
  const snr = 10 * Math.log10(p / Math.max(1e-30, e));
  worst = Math.max(worst, m);
  console.log(`${s.voices.map(v => `${v.type[0]}${Math.round(v.F0)}${v.F1 !== v.F0 ? '>' + Math.round(v.F1) : ''}@${v.T0}`).join(' + ').padEnd(70)} max |diff| ${m.toFixed(5)}  signal/difference ${snr.toFixed(1)} dB`);
});
console.log(`largest sample difference: ${worst.toFixed(5)} (full scale 1)`);
