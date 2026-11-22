// The creature's drawing as the game builds it in mount() (an SVG element tree: groups per part, paths, ellipses,
// gradients, clip paths, the fabric filter), read from geo-<key>.json's "tree". Each element the rig writes to is
// bound to its RigEl by name; everything else (colours, stroke styles, which clip / gradient / filter applies) is the
// tree's own static data. Styles cascade the SVG way (fill, stroke and their widths / caps / joins are inherited).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlushCrush.Core.Rig
{
    /// <summary>A colour, straight (not premultiplied) alpha, 0..1, sRGB as written.</summary>
    public struct RigColor
    {
        public float R, G, B, A;
        public RigColor(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }

        public static bool TryParse(string s, out RigColor c)
        {
            c = default; if (string.IsNullOrEmpty(s)) return false; s = s.Trim();
            if (s[0] == '#')
            {
                string h = s.Substring(1);
                if (h.Length == 3) h = new string(new[] { h[0], h[0], h[1], h[1], h[2], h[2] });
                if (h.Length != 6 && h.Length != 8) return false;
                int v(int i) => int.Parse(h.Substring(i, 2), NumberStyles.HexNumber);
                c = new RigColor(v(0) / 255f, v(2) / 255f, v(4) / 255f, h.Length == 8 ? v(6) / 255f : 1); return true;
            }
            if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int a = s.IndexOf('('), b = s.IndexOf(')'); if (a < 0 || b < a) return false;
                var p = s.Substring(a + 1, b - a - 1).Split(',');
                float n(int i) => float.Parse(p[i].Trim(), CultureInfo.InvariantCulture);
                c = new RigColor(n(0) / 255f, n(1) / 255f, n(2) / 255f, p.Length > 3 ? n(3) : 1); return true;
            }
            switch (s.ToLowerInvariant())
            {
                case "white": c = new RigColor(1, 1, 1, 1); return true;
                case "black": c = new RigColor(0, 0, 0, 1); return true;
                case "transparent": c = new RigColor(0, 0, 0, 0); return true;
            }
            return false;
        }
    }

    /// <summary>An affine transform (SVG matrix(a b c d e f)): x' = a x + c y + e, y' = b x + d y + f.</summary>
    public struct RigXf
    {
        public float A, B, C, D, E, F;
        public static readonly RigXf Identity = new RigXf { A = 1, D = 1 };
        public bool IsIdentity => A == 1 && B == 0 && C == 0 && D == 1 && E == 0 && F == 0;
        public (float x, float y) Apply(float x, float y) => (A * x + C * y + E, B * x + D * y + F);
        /// <summary>this * o (o applied first).</summary>
        public RigXf Then(RigXf o) => new RigXf { A = A * o.A + C * o.B, B = B * o.A + D * o.B, C = A * o.C + C * o.D, D = B * o.C + D * o.D, E = A * o.E + C * o.F + E, F = B * o.E + D * o.F + F };
        public RigXf Inverse()
        {
            float det = A * D - B * C; if (det == 0) return Identity;
            return new RigXf { A = D / det, B = -B / det, C = -C / det, D = A / det, E = (C * F - D * E) / det, F = (B * E - A * F) / det };
        }

        /// <summary>An SVG transform list: translate, scale, rotate (with a centre), matrix.</summary>
        public static RigXf Parse(string s)
        {
            var m = Identity; if (string.IsNullOrEmpty(s)) return m;
            int i = 0;
            while (i < s.Length)
            {
                while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
                int a = s.IndexOf('(', i); if (a < 0) break; int b = s.IndexOf(')', a); if (b < 0) break;
                string name = s.Substring(i, a - i).Trim();
                var parts = s.Substring(a + 1, b - a - 1).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                var v = new float[parts.Length]; for (int k = 0; k < v.Length; k++) v[k] = float.Parse(parts[k], CultureInfo.InvariantCulture);
                RigXf t = Identity;
                switch (name)
                {
                    case "translate": t.E = v.Length > 0 ? v[0] : 0; t.F = v.Length > 1 ? v[1] : 0; break;
                    case "scale": t.A = v.Length > 0 ? v[0] : 1; t.D = v.Length > 1 ? v[1] : t.A; break;
                    case "rotate":
                    {
                        double r = (v.Length > 0 ? v[0] : 0) * Math.PI / 180; float c = (float)Math.Cos(r), sn = (float)Math.Sin(r);
                        t = new RigXf { A = c, B = sn, C = -sn, D = c };
                        if (v.Length >= 3) { var tr = new RigXf { A = 1, D = 1, E = v[1], F = v[2] }; var back = new RigXf { A = 1, D = 1, E = -v[1], F = -v[2] }; t = tr.Then(t).Then(back); }
                        break;
                    }
                    case "matrix": if (v.Length == 6) t = new RigXf { A = v[0], B = v[1], C = v[2], D = v[3], E = v[4], F = v[5] }; break;
                }
                m = m.Then(t); i = b + 1;
            }
            return m;
        }
    }

    /// <summary>A linear or radial gradient (spread: pad). Stops in sRGB, straight alpha.</summary>
    public sealed class RigGradient
    {
        public bool Radial, UserSpace;
        public RigXf Inv = RigXf.Identity;   // the inverse of its gradientTransform (points into the gradient's space)
        public RigNode Node;                 // its element (the rig rewrites some gradients every frame: Refresh)
        string _seen;
        public float Cx = 0.5f, Cy = 0.5f, R = 0.5f, Fx = float.NaN, Fy = float.NaN;   // radial (fractions of the bbox, or units)
        public float X1, Y1, X2 = 1, Y2;                                               // linear
        public float[] Off = new float[0];
        public RigColor[] Col = new RigColor[0];

        /// <summary>Takes the attributes the rig wrote to the gradient and its stops (cx, cy, r, gradientTransform, offset).</summary>
        public void Refresh()
        {
            if (Node == null) return;
            var e = Node.El; bool any = e != null && (e.Str.Count > 0 || !double.IsNaN(e.Cx) || !double.IsNaN(e.Cy));
            foreach (var st in Node.C) if (st.El != null && st.El.Str.Count > 0) any = true;
            if (!any) return;
            if (e != null)
            {
                if (!double.IsNaN(e.Cx)) Cx = (float)e.Cx; else if (e.Str.TryGetValue("cx", out var sx)) Cx = RigTree.Len(sx);   // the rig writes cx / cy as numbers
                if (!double.IsNaN(e.Cy)) Cy = (float)e.Cy; else if (e.Str.TryGetValue("cy", out var sy)) Cy = RigTree.Len(sy);
                string v;
                if (e.Str.TryGetValue("r", out v)) R = RigTree.Len(v);
                if (e.Str.TryGetValue("gradientTransform", out v) && v != _seen) { _seen = v; Inv = RigXf.Parse(v).Inverse(); }
                if (Radial) { Fx = Cx; Fy = Cy; }
            }
            int k = 0; float last = 0;
            foreach (var st in Node.C)
            {
                if (st.Tag != "stop") continue;
                if (k < Off.Length && st.El != null && st.El.Str.TryGetValue("offset", out var o)) Off[k] = Math.Max(last, Math.Min(1, Math.Max(0, RigTree.Len(o))));
                if (k < Off.Length) last = Off[k];
                k++;
            }
        }
    }

    /// <summary>A fill or stroke: none, a colour, or a gradient.</summary>
    public sealed class RigPaint
    {
        public static readonly RigPaint None = new RigPaint { IsNone = true };
        public bool IsNone; public RigColor Color; public RigGradient Grad;
    }

    public sealed class RigNode
    {
        public string Tag, Name, Id;
        public readonly Dictionary<string, string> A = new Dictionary<string, string>();
        public readonly List<RigNode> C = new List<RigNode>();
        public RigNode Parent;
        public RigEl El;                     // the rig's element of this name, if the rig writes to it

        // the computed style (cascaded at load: none of these change while the rig runs)
        public RigPaint Fill, Stroke;
        public float StrokeWidth = 1, FillOpacity = 1, StrokeOpacity = 1, MiterLimit = 4;
        public string Cap = "butt", Join = "miter";
        public float StaticOpacity = 1;
        public RigNode ClipPath;             // the <clipPath>'s shape (its path child)
        public RigNode Mask;                 // the <mask> (its children are drawn as the mask)
        public RigFilter Filter;
        public RigXf Xf = RigXf.Identity;    // its own transform attribute (the rig may rewrite it: El.Str)
        public float[] Dash;                 // stroke-dasharray (inherited)
        public float X, Y, W, H;             // a rect's geometry
        public RigPaint DynFill, DynStroke;  // fills / strokes the rig writes as attributes
        public bool Hidden;
        public float Cx, Cy, Rx, Ry;          // an ellipse's static geometry

        public string Attr(string k) => A.TryGetValue(k, out var v) ? v : null;
    }

    /// <summary>A filter the rig uses: the fabric grain (a black noise image, alpha only, laid over user space at X, Y, W,
    /// H and multiplied in) or a Gaussian blur (Sigma in user units). Its region is a share of the element's box.</summary>
    public sealed class RigFilter
    {
        public bool Blur; public float Sigma;
        public string Image; public float X, Y, W, H;
        public float RX = -0.1f, RY = -0.1f, RW = 1.2f, RH = 1.2f;
    }

    public sealed class RigTree
    {
        public RigNode Svg, Shadow, Root;
        public readonly Dictionary<string, RigNode> ById = new Dictionary<string, RigNode>();
        public readonly Dictionary<string, RigNode> ByName = new Dictionary<string, RigNode>();
        public readonly Dictionary<string, RigGradient> Gradients = new Dictionary<string, RigGradient>();
        public readonly Dictionary<string, RigFilter> Filters = new Dictionary<string, RigFilter>();

        /// <summary>The tree of geo-<key>.json, bound to the renderer's elements.</summary>
        public static RigTree FromGeo(RigGeo geo, RigRender render)
        {
            if (!geo.Root.TryGetValue("tree", out var t) || !(t is Dictionary<string, object> td)) throw new FormatException("geo has no drawing tree (re-run record-rig.mjs)");
            var T = new RigTree();
            T.Svg = Read(td, null, T);
            foreach (var kv in T.ById) ReadDef(T, kv.Key, kv.Value);
            foreach (var kv in T.Gradients) { kv.Value.Node = T.ById[kv.Key]; if (kv.Value.Node.Attr("gradientTransform") is string gt) kv.Value.Inv = RigXf.Parse(gt).Inverse(); }
            var els = new Dictionary<string, RigEl>();
            foreach (var e in render.All) els[e.Name] = e;
            foreach (var kv in T.ByName) if (els.TryGetValue(kv.Key, out var el)) kv.Value.El = el;
            T.ByName.TryGetValue("shadow", out T.Shadow); T.ByName.TryGetValue("root", out T.Root);
            Cascade(T, T.Svg, null);
            return T;
        }

        static RigNode Read(Dictionary<string, object> o, RigNode parent, RigTree T)
        {
            var n = new RigNode { Tag = (string)o["t"], Parent = parent };
            if (o.TryGetValue("n", out var nm)) { n.Name = (string)nm; T.ByName[n.Name] = n; }
            if (o.TryGetValue("a", out var a) && a is Dictionary<string, object> ad) foreach (var kv in ad) n.A[kv.Key] = kv.Value as string;
            if (n.A.TryGetValue("id", out var id)) { n.Id = id; T.ById[id] = n; }
            if (o.TryGetValue("c", out var c) && c is List<object> cl) foreach (Dictionary<string, object> k in cl) n.C.Add(Read(k, n, T));
            return n;
        }

        static void ReadDef(RigTree T, string id, RigNode n)
        {
            if (n.Tag == "radialGradient" || n.Tag == "linearGradient")
            {
                var g = new RigGradient { Radial = n.Tag == "radialGradient", UserSpace = n.Attr("gradientUnits") == "userSpaceOnUse" };
                float L(string k, float def) => n.Attr(k) is string v ? Len(v) : def;
                if (g.Radial) { g.Cx = L("cx", 0.5f); g.Cy = L("cy", 0.5f); g.R = L("r", 0.5f); g.Fx = L("fx", g.Cx); g.Fy = L("fy", g.Cy); }
                else { g.X1 = L("x1", 0); g.Y1 = L("y1", 0); g.X2 = L("x2", 1); g.Y2 = L("y2", 0); }
                var off = new List<float>(); var col = new List<RigColor>(); float last = 0;
                foreach (var s in n.C)
                {
                    if (s.Tag != "stop") continue;
                    float o = s.Attr("offset") is string ov ? Math.Max(last, Math.Min(1, Math.Max(0, Len(ov)))) : last; last = o;
                    RigColor.TryParse(s.Attr("stop-color") ?? "#000", out var cc);
                    if (s.Attr("stop-opacity") is string so) cc.A *= float.Parse(so, CultureInfo.InvariantCulture);
                    off.Add(o); col.Add(cc);
                }
                g.Off = off.ToArray(); g.Col = col.ToArray();
                T.Gradients[id] = g;
            }
            else if (n.Tag == "filter")
            {
                RigNode img = null, comp = null, blend = null;
                foreach (var k in n.C) { if (k.Tag == "feImage") img = k; else if (k.Tag == "feComposite") comp = k; else if (k.Tag == "feBlend") blend = k; }
                if (n.C.Count == 1 && n.C[0].Tag == "feGaussianBlur")
                {
                    T.Filters[id] = Region(n, new RigFilter { Blur = true, Sigma = float.Parse(n.C[0].Attr("stdDeviation") ?? "0", CultureInfo.InvariantCulture) });
                    return;
                }
                if (img == null || comp == null || blend == null || n.C.Count != 3 || comp.Attr("operator") != "in" || blend.Attr("mode") != "multiply")
                    return;   // other filters are refused where they're used
                float F(string k) => float.Parse(img.Attr(k), CultureInfo.InvariantCulture);
                T.Filters[id] = Region(n, new RigFilter { Image = img.Attr("href"), X = F("x"), Y = F("y"), W = F("width"), H = F("height") });
            }
        }

        static RigFilter Region(RigNode n, RigFilter f)
        {
            if (n.Attr("x") is string x) f.RX = Len(x); if (n.Attr("y") is string y) f.RY = Len(y);
            if (n.Attr("width") is string w) f.RW = Len(w); if (n.Attr("height") is string h) f.RH = Len(h);
            return f;
        }

        /// <summary>A length: "38%" -> 0.38, "12" -> 12.</summary>
        internal static float Len(string v) { v = v.Trim(); return v.EndsWith("%") ? float.Parse(v.Substring(0, v.Length - 1), CultureInfo.InvariantCulture) / 100 : float.Parse(v, CultureInfo.InvariantCulture); }

        static string Ref(string v) { if (v == null) return null; int a = v.IndexOf("#"), b = v.IndexOf(')'); return a >= 0 && b > a ? v.Substring(a + 1, b - a - 1) : null; }

        RigPaint Paint(string v, RigPaint inherit)
        {
            if (v == null) return inherit;
            v = v.Trim();
            if (v == "none") return RigPaint.None;
            if (v.StartsWith("url("))
            {
                var id = Ref(v);
                if (id != null && Gradients.TryGetValue(id, out var g)) return new RigPaint { Grad = g };
                if (id != null && ById.TryGetValue(id, out var gn) && (gn.Tag == "radialGradient" || gn.Tag == "linearGradient")) throw new NotSupportedException("RigTree: gradient " + v);
                throw new NotSupportedException("RigTree: paint " + v + " not ported yet");
            }
            if (RigColor.TryParse(v, out var c)) return new RigPaint { Color = c };
            throw new NotSupportedException("RigTree: colour '" + v + "' not understood");
        }

        static void Cascade(RigTree T, RigNode n, RigNode p)
        {
            n.Fill = p == null ? new RigPaint { Color = new RigColor(0, 0, 0, 1) } : p.Fill;
            n.Stroke = p == null ? RigPaint.None : p.Stroke;
            if (p != null) { n.StrokeWidth = p.StrokeWidth; n.Cap = p.Cap; n.Join = p.Join; n.FillOpacity = p.FillOpacity; n.StrokeOpacity = p.StrokeOpacity; n.MiterLimit = p.MiterLimit; }
            float F(string k, float d) => n.Attr(k) is string s ? float.Parse(s, CultureInfo.InvariantCulture) : d;
            n.Fill = T.Paint(n.Attr("fill"), n.Fill);
            n.Stroke = T.Paint(n.Attr("stroke"), n.Stroke);
            n.StrokeWidth = F("stroke-width", n.StrokeWidth);
            n.FillOpacity = F("fill-opacity", n.FillOpacity); n.StrokeOpacity = F("stroke-opacity", n.StrokeOpacity);
            n.MiterLimit = F("stroke-miterlimit", n.MiterLimit);
            n.Cap = n.Attr("stroke-linecap") ?? n.Cap; n.Join = n.Attr("stroke-linejoin") ?? n.Join;
            n.StaticOpacity = F("opacity", 1);
            n.Hidden = n.Attr("display") == "none";
            if (n.Tag == "ellipse") { n.Cx = F("cx", 0); n.Cy = F("cy", 0); n.Rx = F("rx", 0); n.Ry = F("ry", 0); }
            if (n.Tag == "rect") { n.X = F("x", 0); n.Y = F("y", 0); n.W = F("width", 0); n.H = F("height", 0); }
            n.Dash = p?.Dash;
            if (n.Attr("stroke-dasharray") is string da)
            {
                var parts = da.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (da.Trim() == "none" || parts.Length == 0) n.Dash = null;
                else { var v = new List<float>(); foreach (var q in parts) v.Add(float.Parse(q, CultureInfo.InvariantCulture)); if (v.Count % 2 == 1) v.AddRange(v.ToArray()); n.Dash = v.ToArray(); }
            }
            if (n.Attr("transform") is string tf) n.Xf = RigXf.Parse(tf);
            if (n.Attr("clip-path") is string cp)
            {
                var id = Ref(cp);
                if (id == null || !T.ById.TryGetValue(id, out var cn) || cn.C.Count != 1 || cn.C[0].Tag != "path") throw new NotSupportedException("RigTree: clip-path " + cp + " not ported yet");
                n.ClipPath = cn.C[0];
            }
            if (n.Attr("filter") is string fl)
            {
                var id = Ref(fl);
                if (id == null || !T.Filters.TryGetValue(id, out n.Filter)) throw new NotSupportedException("RigTree: filter " + fl + " not ported yet");
            }
            if (n.Attr("mask") is string mk)
            {
                var id = Ref(mk);
                if (id == null || !T.ById.TryGetValue(id, out n.Mask) || n.Mask.Tag != "mask") throw new NotSupportedException("RigTree: mask " + mk + " not ported yet");
            }
            foreach (var k in n.C) Cascade(T, k, n);
        }
    }
}
