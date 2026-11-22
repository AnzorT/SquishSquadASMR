// A software copy of what RigDrawer asks the GPU to do (unity/.../Rig/RigDrawer.cs + Shaders/RigFill.shader): plays a
// RigDisplay list with a stencil buffer (nonzero fills, union strokes, nested clips in the upper four bits), 4 samples
// per pixel at the standard 4x MSAA positions, the paint shaded once per pixel, layers resolved and laid back with
// their opacity or the fabric grain. So the check can compare the drawing with Chrome's screenshots, without Unity.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PlushCrush.Core.Rig;

sealed class RigRaster
{
    const int S = 4;
    static readonly float[] SX = { 0.375f, 0.875f, 0.125f, 0.625f }, SY = { 0.125f, 0.375f, 0.625f, 0.875f };

    sealed class Target { public float[] C; public byte[] St; public int Level; }

    public readonly int W, H;
    readonly float _k, _ox, _oy;              // pixel = rig * k + o
    readonly float[] _grain; readonly int _gw, _gh;
    readonly Stack<Target> _stack = new Stack<Target>();

    public RigRaster(int w, int h, float pxPerUnit, float ox, float oy, float[] grainAlpha, int gw, int gh)
    { W = w; H = h; _k = pxPerUnit; _ox = ox; _oy = oy; _grain = grainAlpha; _gw = gw; _gh = gh; }

    Target New() => new Target { C = new float[W * H * S * 4], St = new byte[W * H * S] };

    /// <summary>Plays the list; returns the pixels, premultiplied RGBA (W * H * 4).</summary>
    public float[] Play(List<RigCmd> cmds)
    {
        _stack.Clear(); _stack.Push(New());
        foreach (var c in cmds)
        {
            var T = _stack.Peek();
            switch (c.Kind)
            {
                case RigCmdKind.Draw:
                {
                    Fan(T, c.Mesh, c.Xf, T.Level);
                    var inv = c.Xf.Inverse();
                    Cover(T, c.Mesh, c.Xf, (byte)(T.Level << 4), (x, y) => { var q = inv.Apply(x, y); return RigDisplay.Shade(c, q.x, q.y); });
                    break;
                }
                case RigCmdKind.PushClip:
                    Fan(T, c.Mesh, c.Xf, T.Level);
                    ForBox(c.Mesh, c.Xf, (i) => { if (T.St[i] > (T.Level << 4)) T.St[i] = (byte)((T.Level + 1) << 4); });
                    T.Level++;
                    break;
                case RigCmdKind.PopClip:
                    ForBox(c.Mesh, c.Xf, (i) => { if ((T.St[i] & 0xF0) == (T.Level << 4)) T.St[i] = (byte)((T.Level - 1) << 4); });
                    T.Level--;
                    break;
                case RigCmdKind.PushLayer:
                case RigCmdKind.MaskBegin:
                    _stack.Push(New());
                    break;
                case RigCmdKind.PopLayer:
                {
                    var M = c.Masked ? _stack.Pop() : null;
                    var L = _stack.Pop(); var P = _stack.Peek();
                    Composite(L, M, P, c);
                    break;
                }
            }
        }
        var R = _stack.Pop(); var o = new float[W * H * 4];
        for (int p = 0; p < W * H; p++) for (int ch = 0; ch < 4; ch++) { float s = 0; for (int k = 0; k < S; k++) s += R.C[(p * S + k) * 4 + ch]; o[p * 4 + ch] = s / S; }
        return o;
    }

    /// <summary>A target resolved to pixels (premultiplied RGBA).</summary>
    float[] Resolve(Target L)
    {
        var o = new float[W * H * 4];
        for (int p = 0; p < W * H; p++) for (int ch = 0; ch < 4; ch++) { float s = 0; for (int k = 0; k < S; k++) s += L.C[(p * S + k) * 4 + ch]; o[p * 4 + ch] = s / S; }
        return o;
    }

    /// <summary>feGaussianBlur: a separable Gaussian (sigma in pixels, 3 sigma each side) on premultiplied pixels.</summary>
    float[] Blur(float[] src, float sigma)
    {
        int r = Math.Max(1, (int)Math.Ceiling(sigma * 3)); var w = new float[2 * r + 1]; float sum = 0;
        for (int i = -r; i <= r; i++) { w[i + r] = (float)Math.Exp(-i * i / (2.0 * sigma * sigma)); sum += w[i + r]; }
        for (int i = 0; i < w.Length; i++) w[i] /= sum;
        var tmp = new float[src.Length]; var o = new float[src.Length];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) for (int ch = 0; ch < 4; ch++)
        { float acc = 0; for (int i = -r; i <= r; i++) { int xx = x + i; if (xx < 0 || xx >= W) continue; acc += src[(y * W + xx) * 4 + ch] * w[i + r]; } tmp[(y * W + x) * 4 + ch] = acc; }
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) for (int ch = 0; ch < 4; ch++)
        { float acc = 0; for (int i = -r; i <= r; i++) { int yy = y + i; if (yy < 0 || yy >= H) continue; acc += tmp[(yy * W + x) * 4 + ch] * w[i + r]; } o[(y * W + x) * 4 + ch] = acc; }
        return o;
    }

    void Composite(Target L, Target M, Target P, RigCmd c)
    {
        byte lvl = (byte)(P.Level << 4);
        var px = Resolve(L); var mk = M != null ? Resolve(M) : null;
        if (c.Filter != null && c.Filter.Blur) px = Blur(px, c.Filter.Sigma * _k);
        // the area laid back (the filter region): pixel centres inside it
        float rx0 = c.RX0 * _k + _ox, ry0 = c.RY0 * _k + _oy, rx1 = c.RX1 * _k + _ox, ry1 = c.RY1 * _k + _oy;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (x + 0.5f < rx0 || x + 0.5f > rx1 || y + 0.5f < ry0 || y + 0.5f > ry1) continue;
                int p = y * W + x;
                float a = px[p * 4 + 3];
                if (a <= 0) continue;
                var col = new RigColor(px[p * 4], px[p * 4 + 1], px[p * 4 + 2], a);
                if (mk != null) { float m = RigDisplay.Luminance(new RigColor(mk[p * 4], mk[p * 4 + 1], mk[p * 4 + 2], mk[p * 4 + 3])); col = new RigColor(col.R * m, col.G * m, col.B * m, col.A * m); }
                if (c.Filter != null && !c.Filter.Blur)
                {
                    float rx = (x + 0.5f - _ox) / _k, ry = (y + 0.5f - _oy) / _k;
                    col = RigDisplay.Fabric(col, Grain((rx - c.Filter.X) / c.Filter.W, (ry - c.Filter.Y) / c.Filter.H));
                }
                float o = c.Opacity; col = new RigColor(col.R * o, col.G * o, col.B * o, col.A * o);
                for (int k = 0; k < S; k++)
                {
                    int i = p * S + k; if ((P.St[i] & 0xF0) != lvl) continue;
                    Blend(P.C, i * 4, col);
                }
            }
    }

    /// <summary>The grain's alpha at (u, v) in 0..1 of the image (bilinear; nothing outside it).</summary>
    float Grain(float u, float v)
    {
        if (_grain == null || u < 0 || v < 0 || u > 1 || v > 1) return 0;
        float fx = u * _gw - 0.5f, fy = v * _gh - 0.5f; int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy); float tx = fx - x0, ty = fy - y0;
        float at(int X, int Y) { X = Math.Max(0, Math.Min(_gw - 1, X)); Y = Math.Max(0, Math.Min(_gh - 1, Y)); return _grain[Y * _gw + X]; }
        float t = at(x0, y0) + (at(x0 + 1, y0) - at(x0, y0)) * tx, bb = at(x0, y0 + 1) + (at(x0 + 1, y0 + 1) - at(x0, y0 + 1)) * tx;
        return t + (bb - t) * ty;
    }

    static void Blend(float[] C, int q, RigColor s)
    {
        float k = 1 - s.A;
        C[q] = s.R + C[q] * k; C[q + 1] = s.G + C[q + 1] * k; C[q + 2] = s.B + C[q + 2] * k; C[q + 3] = s.A + C[q + 3] * k;
    }

    void Box(RigMesh m, RigXf xf, out int x0, out int y0, out int x1, out int y1)
    {
        float a0 = float.MaxValue, b0 = float.MaxValue, a1 = float.MinValue, b1 = float.MinValue;
        foreach (var (u, v) in new[] { (m.MinX, m.MinY), (m.MaxX, m.MinY), (m.MaxX, m.MaxY), (m.MinX, m.MaxY) })
        { var q = xf.Apply(u, v); a0 = Math.Min(a0, q.x); b0 = Math.Min(b0, q.y); a1 = Math.Max(a1, q.x); b1 = Math.Max(b1, q.y); }
        x0 = Math.Max(0, (int)Math.Floor(a0 * _k + _ox) - 1); y0 = Math.Max(0, (int)Math.Floor(b0 * _k + _oy) - 1);
        x1 = Math.Min(W - 1, (int)Math.Ceiling(a1 * _k + _ox) + 1); y1 = Math.Min(H - 1, (int)Math.Ceiling(b1 * _k + _oy) + 1);
    }

    void ForBox(RigMesh m, RigXf xf, Action<int> f)
    {
        Box(m, xf, out int x0, out int y0, out int x1, out int y1);
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) for (int k = 0; k < S; k++) f((y * W + x) * S + k);
    }

    /// <summary>The cover pass: where this level's low bits say "inside", shade and clear them.</summary>
    void Cover(Target T, RigMesh m, RigXf xf, byte lvl, Func<float, float, RigColor> shade)
    {
        Box(m, xf, out int x0, out int y0, out int x1, out int y1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int p = y * W + x; bool any = false;
                for (int k = 0; k < S; k++) if (T.St[p * S + k] > lvl) { any = true; break; }
                if (!any) continue;
                var col = shade((x + 0.5f - _ox) / _k, (y + 0.5f - _oy) / _k);
                for (int k = 0; k < S; k++) { int i = p * S + k; if (T.St[i] > lvl) { Blend(T.C, i * 4, col); T.St[i] = (byte)(T.St[i] & 0xF0); } }
            }
    }

    /// <summary>The stencil pass: count windings (or mark, for a union) in the low bits, inside the current clip level.</summary>
    void Fan(Target T, RigMesh m, RigXf xf, int level)
    {
        byte lv = (byte)(level << 4);
        for (int t = 0; t < m.NT; t += 3)
        {
            int ia = m.T[t], ib = m.T[t + 1], ic = m.T[t + 2];
            var A = xf.Apply(m.V[ia * 2], m.V[ia * 2 + 1]); var B = xf.Apply(m.V[ib * 2], m.V[ib * 2 + 1]); var Cc = xf.Apply(m.V[ic * 2], m.V[ic * 2 + 1]);
            double ax = A.x * _k + _ox, ay = A.y * _k + _oy, bx = B.x * _k + _ox, by = B.y * _k + _oy, cx = Cc.x * _k + _ox, cy = Cc.y * _k + _oy;
            double area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (area == 0) continue;
            int dir = area > 0 ? 1 : -1;
            if (area < 0) { var tx = bx; bx = cx; cx = tx; var ty = by; by = cy; cy = ty; }
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
            // top-left rule (y down, counter-clockwise on screen after the swap): an edge owns its samples when it's a top or left edge
            bool own(double ex0, double ey0, double ex1, double ey1) { double dx = ex1 - ex0, dy = ey1 - ey0; return dy < 0 || (dy == 0 && dx > 0); }
            bool oAB = own(ax, ay, bx, by), oBC = own(bx, by, cx, cy), oCA = own(cx, cy, ax, ay);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    for (int k = 0; k < S; k++)
                    {
                        double px = x + SX[k], py = y + SY[k];
                        double w0 = (bx - ax) * (py - ay) - (by - ay) * (px - ax), w1 = (cx - bx) * (py - by) - (cy - by) * (px - bx), w2 = (ax - cx) * (py - cy) - (ay - cy) * (px - cx);
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        if ((w0 == 0 && !oAB) || (w1 == 0 && !oBC) || (w2 == 0 && !oCA)) continue;
                        int i = (y * W + x) * S + k; byte s = T.St[i];
                        if ((s & 0xF0) != lv) continue;
                        int low = s & 0x0F;
                        low = m.Union ? 1 : (low + dir) & 0x0F;
                        T.St[i] = (byte)((s & 0xF0) | low);
                    }
        }
    }

    // ---- PNG in and out (8-bit, non-interlaced) ----

    public static void SavePng(string path, float[] premul, int w, int h)
    {
        var raw = new byte[(w * 4 + 1) * h];
        for (int y = 0; y < h; y++)
        {
            raw[y * (w * 4 + 1)] = 0;
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4, q = y * (w * 4 + 1) + 1 + x * 4; float a = premul[p + 3];
                byte b(float v) => (byte)Math.Max(0, Math.Min(255, Math.Round(v * 255)));
                raw[q] = b(a > 0 ? premul[p] / a : 0); raw[q + 1] = b(a > 0 ? premul[p + 1] / a : 0); raw[q + 2] = b(a > 0 ? premul[p + 2] / a : 0); raw[q + 3] = b(a);
            }
        }
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var hdr = new byte[13]; BE(hdr, 0, w); BE(hdr, 4, h); hdr[8] = 8; hdr[9] = 6;
        Chunk(fs, "IHDR", hdr);
        using (var ms = new MemoryStream()) { using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true)) z.Write(raw); Chunk(fs, "IDAT", ms.ToArray()); }
        Chunk(fs, "IEND", new byte[0]);
    }

    /// <summary>A PNG as straight RGBA bytes.</summary>
    public static byte[] LoadPng(string path, out int w, out int h)
    {
        var d = File.ReadAllBytes(path); int i = 8; w = h = 0; int ct = 0, bd = 0; var idat = new MemoryStream();
        while (i < d.Length)
        {
            int len = (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3]; string t = System.Text.Encoding.ASCII.GetString(d, i + 4, 4);
            if (t == "IHDR") { w = (d[i + 8] << 24) | (d[i + 9] << 16) | (d[i + 10] << 8) | d[i + 11]; h = (d[i + 12] << 24) | (d[i + 13] << 16) | (d[i + 14] << 8) | d[i + 15]; bd = d[i + 16]; ct = d[i + 17]; }
            else if (t == "IDAT") idat.Write(d, i + 8, len);
            i += 12 + len;
        }
        if (bd != 8) throw new NotSupportedException("png bit depth " + bd);
        int ch = ct == 6 ? 4 : ct == 2 ? 3 : ct == 4 ? 2 : ct == 0 ? 1 : throw new NotSupportedException("png colour type " + ct);
        idat.Position = 0; var raw = new MemoryStream(); using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.CopyTo(raw);
        var r = raw.ToArray(); int stride = w * ch; var px = new byte[stride * h]; var prev = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            int f = r[y * (stride + 1)]; var cur = new byte[stride];
            for (int x = 0; x < stride; x++)
            {
                int a = x >= ch ? cur[x - ch] : 0, b = prev[x], c = x >= ch ? prev[x - ch] : 0, v = r[y * (stride + 1) + 1 + x];
                int pr = f == 0 ? 0 : f == 1 ? a : f == 2 ? b : f == 3 ? (a + b) / 2 : Paeth(a, b, c);
                cur[x] = (byte)(v + pr);
            }
            Buffer.BlockCopy(cur, 0, px, y * stride, stride); prev = cur;
        }
        var o = new byte[w * h * 4];
        for (int p = 0; p < w * h; p++)
        {
            if (ch == 4) { o[p * 4] = px[p * 4]; o[p * 4 + 1] = px[p * 4 + 1]; o[p * 4 + 2] = px[p * 4 + 2]; o[p * 4 + 3] = px[p * 4 + 3]; }
            else if (ch == 3) { o[p * 4] = px[p * 3]; o[p * 4 + 1] = px[p * 3 + 1]; o[p * 4 + 2] = px[p * 3 + 2]; o[p * 4 + 3] = 255; }
            else if (ch == 2) { o[p * 4] = o[p * 4 + 1] = o[p * 4 + 2] = px[p * 2]; o[p * 4 + 3] = px[p * 2 + 1]; }
            else { o[p * 4] = o[p * 4 + 1] = o[p * 4 + 2] = px[p]; o[p * 4 + 3] = 255; }
        }
        return o;
    }

    static int Paeth(int a, int b, int c) { int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c); return pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
    static void BE(byte[] b, int at, int v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }
    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, data.Length); s.Write(len);
        var td = new byte[4 + data.Length]; System.Text.Encoding.ASCII.GetBytes(type).CopyTo(td, 0); data.CopyTo(td, 4); s.Write(td);
        var crc = new byte[4]; BE(crc, 0, (int)Crc(td)); s.Write(crc);
    }
    static uint[] _crcT;
    static uint Crc(byte[] b)
    {
        if (_crcT == null) { _crcT = new uint[256]; for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; _crcT[n] = c; } }
        uint r = 0xFFFFFFFFu; foreach (var x in b) r = _crcT[(r ^ x) & 0xFF] ^ (r >> 8); return r ^ 0xFFFFFFFFu;
    }
}
