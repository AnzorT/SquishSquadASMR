// Turns the rig's outlines into triangles for the GPU: paths flattened into polylines (curves cut finer than a tenth
// of a pixel), fills as triangle fans (drawn with the stencil's nonzero rule, so any outline fills as the browser
// fills it), strokes as quads plus joins and caps (drawn as a union, so overlaps never blend twice).
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    /// <summary>Triangles in rig units. Union: covered = drawn (strokes); else the nonzero winding rule (fills).</summary>
    public sealed class RigMesh
    {
        public float[] V = new float[512]; public int NV;      // x, y pairs: NV vertices
        public int[] T = new int[768]; public int NT;          // indices: NT / 3 triangles
        public float MinX, MinY, MaxX, MaxY;                   // the vertices' bounds (the cover quad)
        public bool Union;
        public int Version;

        public void Clear(bool union) { NV = 0; NT = 0; Union = union; MinX = MinY = float.MaxValue; MaxX = MaxY = float.MinValue; Version++; }
        public bool Empty => NT == 0;

        public int Add(float x, float y)
        {
            if (NV * 2 + 2 > V.Length) Array.Resize(ref V, V.Length * 2);
            V[NV * 2] = x; V[NV * 2 + 1] = y;
            if (x < MinX) MinX = x; if (x > MaxX) MaxX = x; if (y < MinY) MinY = y; if (y > MaxY) MaxY = y;
            return NV++;
        }

        public void Tri(int a, int b, int c)
        {
            if (NT + 3 > T.Length) Array.Resize(ref T, T.Length * 2);
            T[NT++] = a; T[NT++] = b; T[NT++] = c;
        }
    }

    /// <summary>A path flattened: subpaths of points; Corner marks a path vertex (where joins apply), not a curve's inside.</summary>
    public sealed class RigContours
    {
        public readonly List<float> X = new List<float>(256), Y = new List<float>(256);
        public readonly List<bool> Corner = new List<bool>(256);
        public readonly List<int> Start = new List<int>(8);
        public readonly List<bool> Closed = new List<bool>(8);
        public float MinX, MinY, MaxX, MaxY;

        public void Clear() { X.Clear(); Y.Clear(); Corner.Clear(); Start.Clear(); Closed.Clear(); MinX = MinY = float.MaxValue; MaxX = MaxY = float.MinValue; }
        public int Count => Start.Count;
        public int End(int i) => i + 1 < Start.Count ? Start[i + 1] : X.Count;
        public bool Empty => X.Count == 0;

        public void Begin() { Start.Add(X.Count); Closed.Add(false); }
        public void Pt(float x, float y, bool corner)
        {
            X.Add(x); Y.Add(y); Corner.Add(corner);
            if (x < MinX) MinX = x; if (x > MaxX) MaxX = x; if (y < MinY) MinY = y; if (y > MaxY) MaxY = y;
        }
    }

    public static class RigTess
    {
        /// <summary>Flattens a path (M L Q T C Z, and their relative forms). tol: the largest distance (rig units) the
        /// polyline may stray from the curve.</summary>
        public static void Flatten(RigPath p, float tol, RigContours o)
        {
            o.Clear();
            float cx = 0, cy = 0, sx = 0, sy = 0, qx = 0, qy = 0; bool open = false, lastQ = false;
            for (int i = 0; i < p.Cmd.Count; i++)
            {
                char c = p.Cmd[i]; int a = p.At[i]; var V = p.V;
                bool rel = c >= 'a' && c <= 'z'; float ox = rel ? cx : 0, oy = rel ? cy : 0;
                if (rel) c = (char)(c - 32);
                bool wasQ = lastQ; lastQ = false;
                if (c == 'M')
                {
                    cx = sx = ox + (float)V[a]; cy = sy = oy + (float)V[a + 1];
                    o.Begin(); o.Pt(cx, cy, true); open = true;
                    continue;
                }
                if (c == 'Z')
                {
                    if (open) { o.Closed[o.Closed.Count - 1] = true; open = false; }
                    cx = sx; cy = sy;
                    continue;
                }
                if (!open) { o.Begin(); o.Pt(cx, cy, true); open = true; }   // drawing on after a Z starts at the subpath's start
                if (c == 'L') { cx = ox + (float)V[a]; cy = oy + (float)V[a + 1]; o.Pt(cx, cy, true); }
                else if (c == 'Q' || c == 'T')
                {
                    float x1, y1, x2, y2;
                    if (c == 'Q') { x1 = ox + (float)V[a]; y1 = oy + (float)V[a + 1]; x2 = ox + (float)V[a + 2]; y2 = oy + (float)V[a + 3]; }
                    else { x1 = wasQ ? 2 * cx - qx : cx; y1 = wasQ ? 2 * cy - qy : cy; x2 = ox + (float)V[a]; y2 = oy + (float)V[a + 1]; }   // T: the last control point mirrored
                    float ddx = cx - 2 * x1 + x2, ddy = cy - 2 * y1 + y2, M = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                    int n = Math.Max(1, Math.Min(256, (int)Math.Ceiling(Math.Sqrt(0.25 * M / tol))));
                    for (int k = 1; k <= n; k++) { float t = (float)k / n, m = 1 - t; o.Pt(m * m * cx + 2 * m * t * x1 + t * t * x2, m * m * cy + 2 * m * t * y1 + t * t * y2, k == n); }
                    cx = x2; cy = y2; qx = x1; qy = y1; lastQ = true;
                }
                else if (c == 'C')
                {
                    float x1 = ox + (float)V[a], y1 = oy + (float)V[a + 1], x2 = ox + (float)V[a + 2], y2 = oy + (float)V[a + 3], x3 = ox + (float)V[a + 4], y3 = oy + (float)V[a + 5];
                    float ax = cx - 2 * x1 + x2, ay = cy - 2 * y1 + y2, bx = x1 - 2 * x2 + x3, by = y1 - 2 * y2 + y3;
                    float M = (float)Math.Sqrt(Math.Max(ax * ax + ay * ay, bx * bx + by * by));
                    int n = Math.Max(1, Math.Min(256, (int)Math.Ceiling(Math.Sqrt(0.75 * M / tol))));   // Wang's formula
                    for (int k = 1; k <= n; k++)
                    {
                        float t = (float)k / n, m = 1 - t;
                        o.Pt(m * m * m * cx + 3 * m * m * t * x1 + 3 * m * t * t * x2 + t * t * t * x3, m * m * m * cy + 3 * m * m * t * y1 + 3 * m * t * t * y2 + t * t * t * y3, k == n);
                    }
                    cx = x3; cy = y3;
                }
            }
        }

        /// <summary>An ellipse as a closed polyline (n from its size in pixels).</summary>
        public static void Ellipse(float cx, float cy, float rx, float ry, float pxPerUnit, RigContours o)
        {
            o.Clear();
            if (!(rx > 0) || !(ry > 0)) return;
            int n = Math.Max(16, Math.Min(256, (int)Math.Ceiling(Math.PI * (rx + ry) * pxPerUnit / 1.5)));
            o.Begin();
            for (int k = 0; k < n; k++) { double a = k * 2 * Math.PI / n; o.Pt(cx + rx * (float)Math.Cos(a), cy + ry * (float)Math.Sin(a), false); }
            o.Closed[0] = true;
        }

        /// <summary>The fill: one triangle fan per subpath (open subpaths close for filling).</summary>
        public static void Fill(RigContours c, RigMesh m)
        {
            m.Clear(false);
            for (int s = 0; s < c.Count; s++)
            {
                int a = c.Start[s], b = c.End(s);
                if (b - a < 3) continue;
                int v0 = m.Add(c.X[a], c.Y[a]), prev = m.Add(c.X[a + 1], c.Y[a + 1]);
                for (int i = a + 2; i < b; i++) { int v = m.Add(c.X[i], c.Y[i]); m.Tri(v0, prev, v); prev = v; }
            }
        }

        /// <summary>The stroke: quads along each subpath, joins at its corners, caps on open ends.</summary>
        public static void Stroke(RigContours c, float width, string cap, string join, float miterLimit, float pxPerUnit, RigMesh m, float[] dash = null)
        {
            m.Clear(true);
            float h = width / 2;
            if (!(h > 0)) return;
            var pts = _pts; var crn = _crn;
            for (int s = 0; s < c.Count; s++)
            {
                int a = c.Start[s], b = c.End(s); bool closed = c.Closed[s];
                pts.Clear(); crn.Clear();
                for (int i = a; i < b; i++)
                {
                    if (pts.Count > 0) { var q = pts[pts.Count - 1]; if (Math.Abs(q.x - c.X[i]) < 1e-5f && Math.Abs(q.y - c.Y[i]) < 1e-5f) { crn[crn.Count - 1] |= c.Corner[i]; continue; } }
                    pts.Add((c.X[i], c.Y[i])); crn.Add(c.Corner[i]);
                }
                if (closed && pts.Count > 1) { var f = pts[0]; var l = pts[pts.Count - 1]; if (Math.Abs(f.x - l.x) < 1e-5f && Math.Abs(f.y - l.y) < 1e-5f) { pts.RemoveAt(pts.Count - 1); crn.RemoveAt(crn.Count - 1); } }
                if (dash != null && pts.Count > 1) { Dashes(m, pts, crn, closed, h, dash, cap, join, miterLimit, pxPerUnit); continue; }
                Poly(m, pts, crn, closed, h, cap, join, miterLimit, pxPerUnit);
            }
        }

        static readonly List<(float x, float y)> _dp = new List<(float x, float y)>(64);
        static readonly List<bool> _dc = new List<bool>(64);

        /// <summary>stroke-dasharray: the polyline cut into its dashes (from its start, offset 0), each stroked open with caps.</summary>
        static void Dashes(RigMesh m, List<(float x, float y)> pts, List<bool> crn, bool closed, float h, float[] dash, string cap, string join, float miterLimit, float pxPerUnit)
        {
            float total = 0; foreach (var d in dash) total += d;
            if (!(total > 0)) { Poly(m, pts, crn, closed, h, cap, join, miterLimit, pxPerUnit); return; }
            int k = 0; float left = dash[0]; bool on = true;
            _dp.Clear(); _dc.Clear(); _dp.Add(pts[0]); _dc.Add(true);
            int n = pts.Count, segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % n];
                float dx = b.x - a.x, dy = b.y - a.y, L = (float)Math.Sqrt(dx * dx + dy * dy), t = 0;
                while (L - t > left)
                {
                    t += left; float px = a.x + dx * t / L, py = a.y + dy * t / L;
                    if (on) { _dp.Add((px, py)); _dc.Add(true); if (_dp.Count > 1) Poly(m, _dp, _dc, false, h, cap, join, miterLimit, pxPerUnit); }
                    on = !on; k = (k + 1) % dash.Length; left = dash[k];
                    _dp.Clear(); _dc.Clear(); if (on) { _dp.Add((px, py)); _dc.Add(true); }
                }
                left -= L - t;
                if (on) { _dp.Add(b); _dc.Add(crn[(i + 1) % n]); }
            }
            if (on && _dp.Count > 1) Poly(m, _dp, _dc, false, h, cap, join, miterLimit, pxPerUnit);
        }

        static void Poly(RigMesh m, List<(float x, float y)> pts, List<bool> crn, bool closed, float h, string cap, string join, float miterLimit, float pxPerUnit)
        {
            {
                int n = pts.Count;
                if (n == 0) return;
                if (n == 1)
                {   // a zero-length subpath: a dot with round or square caps
                    if (cap == "round") Disc(m, pts[0].x, pts[0].y, h, pxPerUnit);
                    else if (cap == "square") Quad(m, pts[0].x - h, pts[0].y - h, pts[0].x + h, pts[0].y - h, pts[0].x + h, pts[0].y + h, pts[0].x - h, pts[0].y + h);
                    return;
                }
                int segs = closed ? n : n - 1;
                for (int i = 0; i < segs; i++)
                {
                    var p0 = pts[i]; var p1 = pts[(i + 1) % n];
                    float dx = p1.x - p0.x, dy = p1.y - p0.y, L = (float)Math.Sqrt(dx * dx + dy * dy), nx = -dy / L * h, ny = dx / L * h;
                    Quad(m, p0.x + nx, p0.y + ny, p1.x + nx, p1.y + ny, p1.x - nx, p1.y - ny, p0.x - nx, p0.y - ny);
                }
                for (int i = closed ? 0 : 1; i < (closed ? n : n - 1); i++)
                {
                    var pv = pts[(i - 1 + n) % n]; var p = pts[i]; var nx2 = pts[(i + 1) % n];
                    Join(m, pv, p, nx2, h, crn[i] ? join : "curve", miterLimit, pxPerUnit);
                }
                if (!closed)
                {
                    if (cap == "round") { Disc(m, pts[0].x, pts[0].y, h, pxPerUnit); Disc(m, pts[n - 1].x, pts[n - 1].y, h, pxPerUnit); }
                    else if (cap == "square") { SquareCap(m, pts[1], pts[0], h); SquareCap(m, pts[n - 2], pts[n - 1], h); }
                }
            }
        }

        static readonly List<(float x, float y)> _pts = new List<(float x, float y)>(256);
        static readonly List<bool> _crn = new List<bool>(256);

        static void Quad(RigMesh m, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            int a = m.Add(x0, y0), b = m.Add(x1, y1), c = m.Add(x2, y2), d = m.Add(x3, y3);
            m.Tri(a, b, c); m.Tri(a, c, d);
        }

        static void Disc(RigMesh m, float cx, float cy, float r, float pxPerUnit)
        {
            int n = Math.Max(8, Math.Min(64, (int)Math.Ceiling(Math.PI * r * pxPerUnit)));
            int c = m.Add(cx, cy), first = m.Add(cx + r, cy), prev = first;
            for (int k = 1; k < n; k++) { double a = k * 2 * Math.PI / n; int v = m.Add(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a)); m.Tri(c, prev, v); prev = v; }
            m.Tri(c, prev, first);
        }

        static void SquareCap(RigMesh m, (float x, float y) from, (float x, float y) end, float h)
        {
            float dx = end.x - from.x, dy = end.y - from.y, L = (float)Math.Sqrt(dx * dx + dy * dy); dx = dx / L * h; dy = dy / L * h;
            float nx = -dy, ny = dx;
            Quad(m, end.x + nx, end.y + ny, end.x + nx + dx, end.y + ny + dy, end.x - nx + dx, end.y - ny + dy, end.x - nx, end.y - ny);
        }

        static void Join(RigMesh m, (float x, float y) a, (float x, float y) p, (float x, float y) b, float h, string join, float miterLimit, float pxPerUnit)
        {
            float d1x = p.x - a.x, d1y = p.y - a.y, d2x = b.x - p.x, d2y = b.y - p.y;
            float l1 = (float)Math.Sqrt(d1x * d1x + d1y * d1y), l2 = (float)Math.Sqrt(d2x * d2x + d2y * d2y);
            d1x /= l1; d1y /= l1; d2x /= l2; d2y /= l2;
            float cr = d1x * d2y - d1y * d2x, dot = d1x * d2x + d1y * d2y;
            if (join == "curve") join = dot > 0.97f ? "bevel" : "round";   // inside a curve: a hair's turn needs no more than the gap filled
            if (join == "round") { Disc(m, p.x, p.y, h, pxPerUnit); return; }
            if (Math.Abs(cr) < 1e-6f && dot > 0) return;
            float s = cr > 0 ? -1 : 1;                                    // the outer side
            float n1x = -d1y * h * s, n1y = d1x * h * s, n2x = -d2y * h * s, n2y = d2x * h * s;
            int c = m.Add(p.x, p.y), u = m.Add(p.x + n1x, p.y + n1y), v = m.Add(p.x + n2x, p.y + n2y);
            m.Tri(c, u, v);
            if (join == "miter" || join == "miter-clip" || join == "arcs")
            {
                double theta = Math.Acos(Math.Max(-1, Math.Min(1, -dot)));  // the angle between the two segments
                double ratio = 1 / Math.Sin(theta / 2);
                if (ratio <= miterLimit)
                {
                    float mx = n1x + n2x, my = n1y + n2y, ml = (float)Math.Sqrt(mx * mx + my * my), len = (float)(h * ratio);
                    if (ml > 1e-6f) { int w = m.Add(p.x + mx / ml * len, p.y + my / ml * len); m.Tri(u, w, v); }
                }
            }
        }
    }
}
