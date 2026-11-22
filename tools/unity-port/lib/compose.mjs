// A small compositor that draws pictures the way Unity's UI does, for the exporters' self-checks: each picture laid
// on with normal blending (premultiplied, in gamma space as the project's colour space is), an opacity, an optional
// tint multiplied into its colours (UI Toolkit's vertex tint, here a function of the screen row), an affine transform
// with bilinear sampling, and an optional hard clip (UI Toolkit's stencil clip has no anti-aliasing).
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const { PNG } = require(path.join(REPO, 'node_modules/pngjs'));

export const readPng = buf => { const p = PNG.sync.read(buf); return { w: p.width, h: p.height, data: p.data }; };

export class Canvas {
  constructor(w, h, bg) {
    this.w = w; this.h = h; this.c = new Float32Array(w * h * 4);
    if (bg) for (let i = 0; i < w * h; i++) { this.c[i * 4] = bg[0] / 255; this.c[i * 4 + 1] = bg[1] / 255; this.c[i * 4 + 2] = bg[2] / 255; this.c[i * 4 + 3] = 1; }
  }

  // img: { w, h, data (straight RGBA bytes) }. m: [a, b, c, d, e, f], image px -> canvas px (x' = a x + c y + e,
  // y' = b x + d y + f). opts: opacity, tint(y) -> [r, g, b] 0..1, clip(x, y) -> bool (pixel centres, canvas px).
  draw(img, m, opts = {}) {
    const op = opts.opacity == null ? 1 : opts.opacity;
    if (op <= 0) return;
    const [a, b, c, d, e, f] = m, det = a * d - b * c;
    if (Math.abs(det) < 1e-12) return;
    const ia = d / det, ib = -b / det, ic = -c / det, id = a / det, ie = (c * f - d * e) / det, iff = (b * e - a * f) / det;
    const xs = [0, img.w, 0, img.w].map((x, i) => a * x + c * [0, 0, img.h, img.h][i] + e), ys = [0, img.w, 0, img.w].map((x, i) => b * x + d * [0, 0, img.h, img.h][i] + f);
    const x0 = Math.max(0, Math.floor(Math.min(...xs)) - 1), x1 = Math.min(this.w, Math.ceil(Math.max(...xs)) + 1);
    const y0 = Math.max(0, Math.floor(Math.min(...ys)) - 1), y1 = Math.min(this.h, Math.ceil(Math.max(...ys)) + 1);
    const exact = a === 1 && b === 0 && c === 0 && d === 1 && Number.isInteger(e) && Number.isInteger(f);
    const D = img.data, W = img.w, H = img.h;
    const texel = (x, y, out, w) => {
      if (x < 0 || y < 0 || x >= W || y >= H) return;
      const i = (y * W + x) * 4, al = D[i + 3] / 255;
      out[0] += w * D[i] / 255 * al; out[1] += w * D[i + 1] / 255 * al; out[2] += w * D[i + 2] / 255 * al; out[3] += w * al;
    };
    const s = [0, 0, 0, 0];
    for (let y = y0; y < y1; y++) {
      const tint = opts.tint ? opts.tint(y + 0.5) : null;
      for (let x = x0; x < x1; x++) {
        if (opts.clip && !opts.clip(x + 0.5, y + 0.5)) continue;
        s[0] = s[1] = s[2] = s[3] = 0;
        if (exact) texel(x - e, y - f, s, 1);
        else {
          const u = ia * (x + 0.5) + ic * (y + 0.5) + ie - 0.5, v = ib * (x + 0.5) + id * (y + 0.5) + iff - 0.5;
          const ux = Math.floor(u), vy = Math.floor(v), fx = u - ux, fy = v - vy;
          if (ux < -1 || vy < -1 || ux >= W || vy >= H) continue;
          texel(ux, vy, s, (1 - fx) * (1 - fy)); texel(ux + 1, vy, s, fx * (1 - fy)); texel(ux, vy + 1, s, (1 - fx) * fy); texel(ux + 1, vy + 1, s, fx * fy);
        }
        if (s[3] <= 0) continue;
        if (tint) { s[0] *= tint[0]; s[1] *= tint[1]; s[2] *= tint[2]; }
        const k = (y * this.w + x) * 4, al = s[3] * op;
        for (let ch = 0; ch < 3; ch++) this.c[k + ch] = this.c[k + ch] * (1 - al) + s[ch] * op;
        this.c[k + 3] = this.c[k + 3] * (1 - al) + al;
      }
    }
  }

  // a rectangle (canvas px) filled with a colour per row: colour(y) -> [r, g, b] 0..1
  fillRows(x0, y0, x1, y1, colour, opacity = 1) {
    if (opacity <= 0) return;
    for (let y = Math.max(0, Math.floor(y0)); y < Math.min(this.h, Math.ceil(y1)); y++) {
      const col = colour(y + 0.5);
      for (let x = Math.max(0, Math.floor(x0)); x < Math.min(this.w, Math.ceil(x1)); x++) {
        const k = (y * this.w + x) * 4;
        for (let ch = 0; ch < 3; ch++) this.c[k + ch] = this.c[k + ch] * (1 - opacity) + col[ch] * opacity;
        this.c[k + 3] = this.c[k + 3] * (1 - opacity) + opacity;
      }
    }
  }

  png() {
    const out = new PNG({ width: this.w, height: this.h });
    for (let i = 0; i < this.w * this.h; i++) {
      const a = this.c[i * 4 + 3];
      for (let ch = 0; ch < 3; ch++) out.data[i * 4 + ch] = Math.max(0, Math.min(255, Math.round((a > 0 ? this.c[i * 4 + ch] / a : 0) * 255)));
      out.data[i * 4 + 3] = Math.round(Math.min(1, a) * 255);
    }
    return out;
  }
}

// how far two pictures of the same size are apart: share of pixels more than 2/255 off (any channel), and more than
// 40/255; with a diff picture. mask(x, y): only those pixels count.
export function diff(ref, got, mask) {
  let n = 0, d2 = 0, d40 = 0;
  const out = new PNG({ width: ref.w, height: ref.h });
  for (let y = 0; y < ref.h; y++) for (let x = 0; x < ref.w; x++) {
    const i = (y * ref.w + x) * 4;
    out.data[i + 3] = 255;
    if (mask && !mask(x, y)) continue;
    n++;
    const d = Math.max(Math.abs(ref.data[i] - got.data[i]), Math.abs(ref.data[i + 1] - got.data[i + 1]), Math.abs(ref.data[i + 2] - got.data[i + 2]));
    if (d > 2) d2++;
    if (d > 40) d40++;
    out.data[i] = out.data[i + 1] = out.data[i + 2] = d > 2 ? Math.min(255, 60 + d * 3) : 0;
  }
  return { n, over2: d2 / n, over40: d40 / n, png: out };
}

export { PNG };
