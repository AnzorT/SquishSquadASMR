// Each frame, the creature's drawing as a list of draw commands, in the browser's painting order: the SVG tree walked
// with the rig's current outlines, its parts stacked in the order render() sorted them. Both the Unity drawer
// (RigDrawer: GPU, stencil) and the check's software rasteriser play the same list, so the check sees exactly what
// the phone is asked to draw.
//
// Commands: Draw (a mesh with a paint), PushClip / PopClip (a clip path: what's drawn until the pop shows only inside
// it; clips nest), PushLayer / PopLayer (drawn apart, then laid on what's under it with an opacity, the fabric grain
// or a blur: how SVG applies group opacity and filters); a masked layer has MaskBegin between its content and its mask
// (the mask's luminance multiplies the content). Meshes are in the element's own units; Xf takes them to rig units.
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    public enum RigCmdKind { Draw, PushClip, PopClip, PushLayer, MaskBegin, PopLayer }

    public sealed class RigCmd
    {
        public RigCmdKind Kind;
        public RigMesh Mesh;                 // Draw, PushClip, PopClip (the same mesh as its push)
        public RigXf Xf = RigXf.Identity;    // the mesh's units -> rig units
        public RigPaint Paint; public float Alpha = 1;
        public float GX0, GY0, GX1, GY1;     // the shape's bounding box, in its own units (a gradient's objectBoundingBox)
        public float Opacity = 1; public RigFilter Filter; public bool Masked;   // layers
        public float RX0, RY0, RX1, RY1;     // PopLayer: the area it lays back (rig units; the filter region)
        public string Name;                  // the element drawn (for logs)
    }

    public sealed class RigDisplay
    {
        public readonly List<RigCmd> Cmds = new List<RigCmd>(128);
        /// <summary>Pixels per rig unit where it will be shown (curves are cut finer than Tolerance pixels).</summary>
        public float PxPerUnit = 4.75f, Tolerance = 0.1f;
        /// <summary>The move's own effect (stars, strokes), drawn over the creature as the game's fx layer is.</summary>
        public RigFx Fx;

        readonly RigTree _t; readonly RigRender _r;
        readonly List<RigCmd> _cmdPool = new List<RigCmd>(128);
        readonly List<RigMesh> _meshPool = new List<RigMesh>(64);
        int _nCmd, _nMesh;
        readonly RigContours _c = new RigContours();
        readonly List<RigNode> _rootOrder = new List<RigNode>(24), _headOrder = new List<RigNode>(48);
        readonly Dictionary<RigNode, string> _xfSeen = new Dictionary<RigNode, string>();
        readonly Dictionary<RigNode, RigXf> _xf = new Dictionary<RigNode, RigXf>();
        // the layers being built: the area their content covers (rig units)
        readonly List<float[]> _boxes = new List<float[]>();
        int _depth;
        // the game's DOM moves: the snout before the skull (occlude), the lumps over or under the body
        RigNode _head, _skull, _pgBody, _bodyEl;
        readonly List<RigNode> _snout = new List<RigNode>(), _lumps = new List<RigNode>();
        List<RigNode> _bodyOrder;
        readonly List<bool> _lumpSeen = new List<bool>();

        public RigDisplay(RigTree tree, RigRender render)
        {
            _t = tree; _r = render;
            tree.ByName.TryGetValue("pg.head", out _head); tree.ByName.TryGetValue("skull", out _skull);
            tree.ByName.TryGetValue("pg.body", out _pgBody); tree.ByName.TryGetValue("body", out _bodyEl);
            foreach (var n in new[] { "snoutJ", "snout", "snoutN", "snoutH" }) if (tree.ByName.TryGetValue(n, out var s) && s.Parent == _head) _snout.Add(s);
            for (int i = 0; tree.ByName.TryGetValue("lumps." + i, out var l); i++) _lumps.Add(l);
            if (_pgBody != null && _lumps.Count > 0) _bodyOrder = new List<RigNode>(_pgBody.C);
        }

        /// <summary>Follows the game's DOM moves that depend on the order of frames (the lumps). Call every frame the rig
        /// renders, even when nothing is drawn (Build calls it too).</summary>
        public void Track()
        {
            if (_bodyOrder == null || _bodyEl == null) return;
            for (int i = 0; i < _lumps.Count && i < _r.LumpFront.Count; i++)
            {
                bool fr = _r.LumpFront[i];
                if (i < _lumpSeen.Count && _lumpSeen[i] == fr) continue;
                while (_lumpSeen.Count <= i) _lumpSeen.Add(!fr);
                _lumpSeen[i] = fr;
                _bodyOrder.Remove(_lumps[i]);
                int b = _bodyOrder.IndexOf(_bodyEl);
                _bodyOrder.Insert(fr ? b + 1 : b, _lumps[i]);   // insertBefore(el, fr ? body.nextSibling : body)
            }
        }

        public void Build()
        {
            Track();
            _nCmd = 0; _nMesh = 0; Cmds.Clear(); _depth = 0; _boxes.Clear();
            foreach (var g in _t.Gradients.Values) g.Refresh();
            if (_t.Shadow != null) Walk(_t.Shadow, 1, RigXf.Identity);
            if (_t.Root != null) Walk(_t.Root, 1, RigXf.Identity);
            if (Fx != null)
                foreach (var s in Fx.Shapes)
                {
                    if (!(s.Opacity > 0) || s.D.Empty) continue;
                    RigTess.Flatten(s.D, Tolerance / PxPerUnit, _c);
                    var m = Mesh();
                    if (s.Stroke) RigTess.Stroke(_c, s.Width, s.Cap, "miter", 4, PxPerUnit, m); else RigTess.Fill(_c, m);
                    if (m.Empty) continue;
                    var c = Cmd(RigCmdKind.Draw); c.Mesh = m; c.Paint = s.Paint; c.Alpha = s.Opacity; c.Name = "fx";
                    c.GX0 = _c.MinX; c.GY0 = _c.MinY; c.GX1 = _c.MaxX; c.GY1 = _c.MaxY;
                }
        }

        RigCmd Cmd(RigCmdKind k)
        {
            if (_nCmd == _cmdPool.Count) _cmdPool.Add(new RigCmd());
            var c = _cmdPool[_nCmd++];
            c.Kind = k; c.Mesh = null; c.Paint = null; c.Alpha = 1; c.Opacity = 1; c.Filter = null; c.Name = null; c.Masked = false; c.Xf = RigXf.Identity;
            Cmds.Add(c); return c;
        }

        RigMesh Mesh()
        {
            if (_nMesh == _meshPool.Count) _meshPool.Add(new RigMesh());
            return _meshPool[_nMesh++];
        }

        static float Opacity(RigNode n) => n.El != null && !double.IsNaN(n.El.Opacity) ? (float)n.El.Opacity : n.StaticOpacity;

        /// <summary>A node's transform: the one the rig writes (El "transform"), else its own.</summary>
        RigXf Xf(RigNode n)
        {
            if (n.El == null || !n.El.Str.TryGetValue("transform", out var s)) return n.Xf;
            if (_xfSeen.TryGetValue(n, out var seen) && seen == s) return _xf[n];
            var x = RigXf.Parse(s); _xfSeen[n] = s; _xf[n] = x; return x;
        }

        /// <summary>The children in the order the game's DOM has them this frame.</summary>
        List<RigNode> Children(RigNode n)
        {
            if (n == _t.Root)
            {   // the parts the depth sort appends, in its order; the ones it doesn't sort stay first
                _rootOrder.Clear();
                foreach (var k in n.C) if (k.Name == null || !k.Name.StartsWith("pg.") || !_r.Order.Contains(k.Name.Substring(3))) _rootOrder.Add(k);
                foreach (var p in _r.Order) if (_t.ByName.TryGetValue("pg." + p, out var g) && g.Parent == n) _rootOrder.Add(g);
                return _rootOrder;
            }
            if (n == _head && _r.SnoutBehind && _snout.Count > 0 && _skull != null)
            {   // snout.occlude: its elements moved, in order, before the skull
                _headOrder.Clear();
                foreach (var k in n.C) { if (k == _skull) _headOrder.AddRange(_snout); if (!_snout.Contains(k)) _headOrder.Add(k); }
                return _headOrder;
            }
            if (n == _pgBody && _bodyOrder != null) return _bodyOrder;
            return n.C;
        }

        void Grow(RigXf xf, float x0, float y0, float x1, float y1)
        {
            if (_depth == 0) return;
            var b = _boxes[_depth - 1];
            void at(float x, float y) { var p = xf.Apply(x, y); if (p.x < b[0]) b[0] = p.x; if (p.y < b[1]) b[1] = p.y; if (p.x > b[2]) b[2] = p.x; if (p.y > b[3]) b[3] = p.y; }
            at(x0, y0); at(x1, y0); at(x1, y1); at(x0, y1);
        }

        void Walk(RigNode n, float alpha, RigXf xf)
        {
            if (n.Hidden || n.Tag == "defs" || n.Tag == "clipPath" || n.Tag == "mask") return;
            float op = Opacity(n);
            if (!(op > 0)) return;
            xf = xf.Then(Xf(n));
            bool layer = n.Filter != null || n.Mask != null || (op < 1 && Paints(n, 0) > 1);
            if (!layer) alpha *= op;

            RigMesh clip = null;
            if (n.ClipPath != null)
            {
                var cp = n.ClipPath.El?.D;
                if (cp == null || cp.Empty) return;                      // an empty clip path hides everything
                RigTess.Flatten(cp, Tolerance / PxPerUnit, _c);
                clip = Mesh(); RigTess.Fill(_c, clip);
                if (clip.Empty) return;
                var c = Cmd(RigCmdKind.PushClip); c.Mesh = clip; c.Xf = xf; c.Name = n.Name ?? n.ClipPath.Name;
            }
            if (layer)
            {
                var c = Cmd(RigCmdKind.PushLayer); c.Opacity = op; c.Filter = n.Filter; c.Masked = n.Mask != null; c.Name = n.Name;
                if (_boxes.Count <= _depth) _boxes.Add(new float[4]);
                var b = _boxes[_depth++]; b[0] = b[1] = float.MaxValue; b[2] = b[3] = float.MinValue;
            }

            if (n.Tag == "path" || n.Tag == "ellipse" || n.Tag == "rect") Shape(n, layer ? 1 : alpha, xf);
            else foreach (var k in Children(n)) Walk(k, layer ? 1 : alpha, xf);

            if (layer)
            {
                var b = _boxes[--_depth];
                if (n.Mask != null)
                {
                    Cmd(RigCmdKind.MaskBegin).Name = n.Mask.Name;
                    _depth++; foreach (var k in n.Mask.C) Walk(k, 1, xf); _depth--;
                }
                var c = Cmd(RigCmdKind.PopLayer); c.Opacity = op * alpha; c.Filter = n.Filter; c.Masked = n.Mask != null; c.Name = n.Name;
                if (b[0] > b[2]) { c.RX0 = c.RY0 = 0; c.RX1 = c.RY1 = 0; }
                else if (n.Filter != null)
                {   // the filter region: a share of the content's box (x -15% .. +130% of its width ...)
                    float w = b[2] - b[0], h = b[3] - b[1], f = n.Filter.Blur ? 0 : 0;
                    c.RX0 = b[0] + n.Filter.RX * w - f; c.RY0 = b[1] + n.Filter.RY * h - f; c.RX1 = c.RX0 + n.Filter.RW * w; c.RY1 = c.RY0 + n.Filter.RH * h;
                }
                else { c.RX0 = b[0] - 1; c.RY0 = b[1] - 1; c.RX1 = b[2] + 1; c.RY1 = b[3] + 1; }
                if (_depth > 0) { var pb = _boxes[_depth - 1]; pb[0] = Math.Min(pb[0], c.RX0); pb[1] = Math.Min(pb[1], c.RY0); pb[2] = Math.Max(pb[2], c.RX1); pb[3] = Math.Max(pb[3], c.RY1); }
            }
            if (clip != null) { var c = Cmd(RigCmdKind.PopClip); c.Mesh = clip; c.Xf = xf; }
        }

        /// <summary>How many paints (fills, strokes) a subtree would draw, up to two (one paint takes an opacity directly).</summary>
        int Paints(RigNode n, int have)
        {
            if (have > 1 || n.Hidden || n.Tag == "defs" || n.Tag == "clipPath" || n.Tag == "mask" || !(Opacity(n) > 0)) return have;
            if (n.Tag == "path" || n.Tag == "ellipse" || n.Tag == "rect")
            {
                if (!HasGeometry(n)) return have;
                if (!FillOf(n).IsNone) have++;
                if (!StrokeOf(n).IsNone && StrokeWidth(n) > 0) have++;
                return have;
            }
            if (n.Filter != null || n.Mask != null) return have + 2;
            foreach (var k in n.C) { have = Paints(k, have); if (have > 1) break; }
            return have;
        }

        static bool HasGeometry(RigNode n) => n.Tag != "path" || (n.El != null && !n.El.D.Empty);
        static float StrokeWidth(RigNode n) => n.El != null && !double.IsNaN(n.El.StrokeWidth) ? (float)n.El.StrokeWidth : n.StrokeWidth;

        /// <summary>The fill / stroke: the one the rig writes as an attribute (collar segments), else the cascaded one.</summary>
        static RigPaint FillOf(RigNode n) => Dyn(n, "fill", ref n.DynFill) ?? n.Fill;
        static RigPaint StrokeOf(RigNode n) => Dyn(n, "stroke", ref n.DynStroke) ?? n.Stroke;
        static RigPaint Dyn(RigNode n, string attr, ref RigPaint slot)
        {
            if (n.El == null || !n.El.Str.TryGetValue(attr, out var v)) return null;
            if (slot == null) slot = new RigPaint();
            if (v == "none") { slot.IsNone = true; return slot; }
            slot.IsNone = false; slot.Grad = null;
            if (!RigColor.TryParse(v, out slot.Color)) slot.IsNone = true;
            return slot;
        }

        void Shape(RigNode n, float alpha, RigXf xf)
        {
            var e = n.El;
            if (n.Tag == "ellipse")
            {
                float cx = e != null && !double.IsNaN(e.Cx) ? (float)e.Cx : n.Cx, cy = e != null && !double.IsNaN(e.Cy) ? (float)e.Cy : n.Cy,
                      rx = e != null && !double.IsNaN(e.Rx) ? (float)e.Rx : n.Rx, ry = e != null && !double.IsNaN(e.Ry) ? (float)e.Ry : n.Ry;
                RigTess.Ellipse(cx, cy, rx, ry, PxPerUnit, _c);
            }
            else if (n.Tag == "rect")
            {
                _c.Clear(); if (!(n.W > 0) || !(n.H > 0)) return;
                _c.Begin(); _c.Pt(n.X, n.Y, true); _c.Pt(n.X + n.W, n.Y, true); _c.Pt(n.X + n.W, n.Y + n.H, true); _c.Pt(n.X, n.Y + n.H, true); _c.Closed[0] = true;
            }
            else
            {
                if (e == null) throw new NotSupportedException("RigDisplay: a path the rig doesn't write (" + (n.Name ?? n.Tag) + ")");
                RigTess.Flatten(e.D, Tolerance / PxPerUnit, _c);
            }
            if (_c.Empty) return;
            var fill = FillOf(n);
            if (!fill.IsNone && n.FillOpacity > 0)
            {
                var m = Mesh(); RigTess.Fill(_c, m);
                if (!m.Empty) Draw(n, m, fill, alpha * n.FillOpacity, xf);
            }
            float w = StrokeWidth(n); var stroke = StrokeOf(n);
            if (!stroke.IsNone && w > 0 && n.StrokeOpacity > 0)
            {
                var m = Mesh(); RigTess.Stroke(_c, w, n.Cap, n.Join, n.MiterLimit, PxPerUnit, m, n.Dash);
                if (!m.Empty) Draw(n, m, stroke, alpha * n.StrokeOpacity, xf);
            }
        }

        void Draw(RigNode n, RigMesh m, RigPaint p, float alpha, RigXf xf)
        {
            if (p.Grad != null && !p.Grad.UserSpace && (_c.MaxX - _c.MinX <= 0 || _c.MaxY - _c.MinY <= 0)) return;   // a bbox gradient on a flat shape draws nothing
            var c = Cmd(RigCmdKind.Draw); c.Mesh = m; c.Paint = p; c.Alpha = alpha; c.Name = n.Name; c.Xf = xf;
            c.GX0 = _c.MinX; c.GY0 = _c.MinY; c.GX1 = _c.MaxX; c.GY1 = _c.MaxY;
            Grow(xf, m.MinX, m.MinY, m.MaxX, m.MaxY);
        }

        // ---- the paint at a point (the GPU shader does the same) ----

        /// <summary>The colour a paint gives at (x, y) in the shape's own units, premultiplied, times alpha.</summary>
        public static RigColor Shade(RigCmd c, float x, float y)
        {
            var p = c.Paint; RigColor col;
            if (p.Grad == null) col = p.Color;
            else
            {
                var g = p.Grad; float u = x, v = y;
                if (!g.UserSpace) { u = (x - c.GX0) / (c.GX1 - c.GX0); v = (y - c.GY0) / (c.GY1 - c.GY0); }
                if (!g.Inv.IsIdentity) { var q = g.Inv.Apply(u, v); u = q.x; v = q.y; }
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

        /// <summary>A mask's value at a pixel (premultiplied mask colour in): its luminance (sRGB coefficients) times alpha.</summary>
        public static float Luminance(RigColor m) => 0.2125f * m.R + 0.7154f * m.G + 0.0721f * m.B;   // premultiplied: already x alpha
    }
}
