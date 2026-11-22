// Each frame, the creature's drawing as a list of draw commands, in the browser's painting order: the SVG tree walked
// with the rig's current outlines, its parts stacked in the order render() sorted them. Both the Unity drawer
// (RigDrawer: GPU, stencil) and the check's software rasteriser play the same list, so the check sees exactly what
// the phone is asked to draw.
//
// Commands: Draw (a mesh with a paint), PushClip / PopClip (a clip path: what's drawn until the pop shows only inside
// it; clips nest), PushLayer / PopLayer (drawn apart, then laid on what's under it with an opacity or the fabric grain:
// how SVG applies group opacity and filters).
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    public enum RigCmdKind { Draw, PushClip, PopClip, PushLayer, PopLayer }

    public sealed class RigCmd
    {
        public RigCmdKind Kind;
        public RigMesh Mesh;                 // Draw, PushClip, PopClip (the same mesh as its push)
        public RigPaint Paint; public float Alpha = 1;
        public float GX0, GY0, GX1, GY1;     // the shape's bounding box (a gradient's objectBoundingBox)
        public float Opacity = 1; public RigFilter Filter;   // layers
        public string Name;                  // the element drawn (for logs)
    }

    public sealed class RigDisplay
    {
        public readonly List<RigCmd> Cmds = new List<RigCmd>(128);
        /// <summary>Pixels per rig unit where it will be shown (curves are cut finer than Tolerance pixels).</summary>
        public float PxPerUnit = 4.75f, Tolerance = 0.1f;

        readonly RigTree _t; readonly RigRender _r;
        readonly List<RigCmd> _cmdPool = new List<RigCmd>(128);
        readonly List<RigMesh> _meshPool = new List<RigMesh>(64);
        int _nCmd, _nMesh;
        readonly RigContours _c = new RigContours();
        readonly List<RigNode> _rootOrder = new List<RigNode>(24);

        public RigDisplay(RigTree tree, RigRender render) { _t = tree; _r = render; }

        public void Build()
        {
            _nCmd = 0; _nMesh = 0; Cmds.Clear();
            if (_t.Shadow != null) Walk(_t.Shadow, 1);
            if (_t.Root != null) Walk(_t.Root, 1);
        }

        RigCmd Cmd(RigCmdKind k)
        {
            if (_nCmd == _cmdPool.Count) _cmdPool.Add(new RigCmd());
            var c = _cmdPool[_nCmd++];
            c.Kind = k; c.Mesh = null; c.Paint = null; c.Alpha = 1; c.Opacity = 1; c.Filter = null; c.Name = null;
            Cmds.Add(c); return c;
        }

        RigMesh Mesh()
        {
            if (_nMesh == _meshPool.Count) _meshPool.Add(new RigMesh());
            return _meshPool[_nMesh++];
        }

        static float Opacity(RigNode n) => n.El != null && !double.IsNaN(n.El.Opacity) ? (float)n.El.Opacity : n.StaticOpacity;

        /// <summary>The root's parts in the order render() stacks them (groups it doesn't sort stay first, as in the DOM).</summary>
        List<RigNode> Children(RigNode n)
        {
            if (n != _t.Root) return n.C;
            _rootOrder.Clear();
            foreach (var k in n.C) if (k.Name == null || !k.Name.StartsWith("pg.") || !_r.Order.Contains(k.Name.Substring(3))) _rootOrder.Add(k);
            foreach (var p in _r.Order) if (_t.ByName.TryGetValue("pg." + p, out var g) && g.Parent == n) _rootOrder.Add(g);
            return _rootOrder;
        }

        void Walk(RigNode n, float alpha)
        {
            if (n.Hidden || n.Tag == "defs" || n.Tag == "clipPath" || n.Tag == "mask") return;
            float op = Opacity(n);
            if (!(op > 0)) return;
            bool layer = n.Filter != null || (op < 1 && Paints(n, 0) > 1);
            if (!layer) alpha *= op;

            RigMesh clip = null;
            if (n.ClipPath != null)
            {
                var cp = n.ClipPath.El?.D;
                if (cp == null || cp.Empty) return;                      // an empty clip path hides everything
                RigTess.Flatten(cp, Tolerance / PxPerUnit, _c);
                clip = Mesh(); RigTess.Fill(_c, clip);
                if (clip.Empty) return;
                var c = Cmd(RigCmdKind.PushClip); c.Mesh = clip; c.Name = n.Name ?? n.ClipPath.Name;
            }
            if (layer) { var c = Cmd(RigCmdKind.PushLayer); c.Opacity = op; c.Filter = n.Filter; c.Name = n.Name; }

            if (n.Tag == "path" || n.Tag == "ellipse") Shape(n, layer ? 1 : alpha);
            else foreach (var k in Children(n)) Walk(k, layer ? 1 : alpha);

            if (layer) { var c = Cmd(RigCmdKind.PopLayer); c.Opacity = op * (alpha); c.Filter = n.Filter; c.Name = n.Name; }
            if (clip != null) { var c = Cmd(RigCmdKind.PopClip); c.Mesh = clip; }
        }

        /// <summary>How many paints (fills, strokes) a subtree would draw, up to two (one paint takes an opacity directly).</summary>
        int Paints(RigNode n, int have)
        {
            if (have > 1 || n.Hidden || n.Tag == "defs" || n.Tag == "clipPath" || !(Opacity(n) > 0)) return have;
            if (n.Tag == "path" || n.Tag == "ellipse")
            {
                if (!HasGeometry(n)) return have;
                if (!n.Fill.IsNone) have++;
                if (!n.Stroke.IsNone && StrokeWidth(n) > 0) have++;
                return have;
            }
            foreach (var k in n.C) { have = Paints(k, have); if (have > 1) break; }
            return have;
        }

        static bool HasGeometry(RigNode n) => n.Tag == "ellipse" ? true : n.El != null && !n.El.D.Empty;
        static float StrokeWidth(RigNode n) => n.El != null && !double.IsNaN(n.El.StrokeWidth) ? (float)n.El.StrokeWidth : n.StrokeWidth;

        void Shape(RigNode n, float alpha)
        {
            if (n.Tag == "ellipse")
            {
                var e = n.El;
                float cx = e != null && !double.IsNaN(e.Cx) ? (float)e.Cx : n.Cx, cy = e != null && !double.IsNaN(e.Cy) ? (float)e.Cy : n.Cy,
                      rx = e != null && !double.IsNaN(e.Rx) ? (float)e.Rx : n.Rx, ry = e != null && !double.IsNaN(e.Ry) ? (float)e.Ry : n.Ry;
                RigTess.Ellipse(cx, cy, rx, ry, PxPerUnit, _c);
            }
            else
            {
                if (n.El == null) throw new NotSupportedException("RigDisplay: a path the rig doesn't write (" + (n.Name ?? n.Tag) + ")");
                RigTess.Flatten(n.El.D, Tolerance / PxPerUnit, _c);
            }
            if (_c.Empty) return;
            if (!n.Fill.IsNone && n.FillOpacity > 0)
            {
                var m = Mesh(); RigTess.Fill(_c, m);
                if (!m.Empty) Draw(n, m, n.Fill, alpha * n.FillOpacity);
            }
            float w = StrokeWidth(n);
            if (!n.Stroke.IsNone && w > 0 && n.StrokeOpacity > 0)
            {
                var m = Mesh(); RigTess.Stroke(_c, w, n.Cap, n.Join, n.MiterLimit, PxPerUnit, m);
                if (!m.Empty) Draw(n, m, n.Stroke, alpha * n.StrokeOpacity);
            }
        }

        void Draw(RigNode n, RigMesh m, RigPaint p, float alpha)
        {
            if (p.Grad != null && !p.Grad.UserSpace && (_c.MaxX - _c.MinX <= 0 || _c.MaxY - _c.MinY <= 0)) return;   // a bbox gradient on a flat shape draws nothing
            var c = Cmd(RigCmdKind.Draw); c.Mesh = m; c.Paint = p; c.Alpha = alpha; c.Name = n.Name;
            c.GX0 = _c.MinX; c.GY0 = _c.MinY; c.GX1 = _c.MaxX; c.GY1 = _c.MaxY;
        }

        // ---- the paint at a point (the GPU shader does the same) ----

        /// <summary>The colour a paint gives at (x, y) in rig units, premultiplied, times alpha.</summary>
        public static RigColor Shade(RigCmd c, float x, float y)
        {
            var p = c.Paint; RigColor col;
            if (p.Grad == null) col = p.Color;
            else
            {
                var g = p.Grad; float u = x, v = y;
                if (!g.UserSpace) { u = (x - c.GX0) / (c.GX1 - c.GX0); v = (y - c.GY0) / (c.GY1 - c.GY0); }
                float t;
                if (g.Radial) { float dx = u - g.Cx, dy = v - g.Cy; t = (float)Math.Sqrt(dx * dx + dy * dy) / g.R; }   // focal point = centre (as the game's)
                else { float ex = g.X2 - g.X1, ey = g.Y2 - g.Y1, L = ex * ex + ey * ey; t = L > 0 ? ((u - g.X1) * ex + (v - g.Y1) * ey) / L : 0; }
                col = Stop(g, t);
                return new RigColor(col.R * c.Alpha, col.G * c.Alpha, col.B * c.Alpha, col.A * c.Alpha);   // Stop is premultiplied already
            }
            float a = col.A * c.Alpha;
            return new RigColor(col.R * a, col.G * a, col.B * a, a);
        }

        /// <summary>A gradient's colour at t (pad), interpolated premultiplied (as Chrome does), premultiplied out.</summary>
        public static RigColor Stop(RigGradient g, float t)
        {
            int n = g.Off.Length;
            if (n == 0) return new RigColor(0, 0, 0, 0);
            RigColor P(int i) { var c = g.Col[i]; return new RigColor(c.R * c.A, c.G * c.A, c.B * c.A, c.A); }
            if (t <= g.Off[0]) return P(0);
            if (t >= g.Off[n - 1]) return P(n - 1);
            for (int i = 1; i < n; i++)
                if (t <= g.Off[i])
                {
                    float span = g.Off[i] - g.Off[i - 1], f = span > 0 ? (t - g.Off[i - 1]) / span : 1;
                    var a = P(i - 1); var b = P(i);
                    return new RigColor(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f, a.A + (b.A - a.A) * f);
                }
            return P(n - 1);
        }

        static float ToLin(float c) => c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float ToSrgb(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * (float)Math.Pow(c, 1 / 2.4f) - 0.055f;

        /// <summary>The fabric filter on one pixel (premultiplied in and out): the source multiplied by (1 - grain x its
        /// alpha) in linear RGB, as Chrome runs feComposite in + feBlend multiply (color-interpolation-filters: linearRGB).</summary>
        public static RigColor Fabric(RigColor s, float grain)
        {
            if (s.A <= 0) return s;
            float ab = grain * s.A, ar = s.A + ab - s.A * ab, k = 1 - ab;
            float r = ToLin(s.R / s.A) * s.A * k / ar, g = ToLin(s.G / s.A) * s.A * k / ar, b = ToLin(s.B / s.A) * s.A * k / ar;
            return new RigColor(ToSrgb(r) * ar, ToSrgb(g) * ar, ToSrgb(b) * ar, ar);
        }
    }
}
