// The fx layer over the creature (rive-rig.js: I.fxG.innerHTML = drawParts(I, dt) + FX[move](I, t)), as shapes: first
// the particles (drawParts' markup: dots, drops, bubbles, stars, hearts, waves, wisps, leaves, the duck, dust clouds,
// puffs, seeds, notes), then the move's own effect (happy's eight twinkling stars, surprised's three strokes over the
// head). No randomness here: the particles are the engine's (RigParticles.cs). Ported exactly; each shape keeps the
// numbers the game writes (the check compares them with its markup). (sleep's floating 'z's are text: not ported yet;
// nor the water, dust-bath and whip-blur layers after it.)
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    public enum RigFxKind { Path, Circle, Ellipse, Group, End }

    /// <summary>One element of the fx markup: a path, circle or ellipse, or a group (its children follow, up to its End).</summary>
    public sealed class RigFxShape
    {
        public RigFxKind Kind;
        public RigPath D;                            // a path's outline: Own, or a fixed one
        public readonly RigPath Own = new RigPath();
        public double Cx, Cy, Rx, Ry;                // a circle (r: Rx) or an ellipse, as written
        public string Fill, Stroke;                  // as written (null: not written - black fill, no stroke)
        public readonly RigPaint FillP = new RigPaint(), StrokeP = new RigPaint { IsNone = true };
        public double Width = double.NaN;            // stroke-width (NaN: not written)
        public string Cap;                           // stroke-linecap (null: not written)
        public double Opacity = double.NaN;          // NaN: not written
        // transform, as written: 't' translate(x y), 'r' rotate(a), 's' scale(k), 'S' scale(x y)
        public int NXf; public readonly char[] XfOp = new char[3]; public readonly double[] XfV = new double[6];
        public int End;                              // a group: the index of its End

        public RigXf Xf
        {
            get
            {
                double a = 1, b = 0, c = 0, d = 1, e = 0, f = 0;
                for (int i = 0, v = 0; i < NXf; i++)
                {
                    double A = 1, B = 0, C = 0, D = 1, E = 0, F = 0;
                    switch (XfOp[i])
                    {
                        case 't': E = XfV[v++]; F = XfV[v++]; break;
                        case 'r': { double r = XfV[v++] * Math.PI / 180; A = Math.Cos(r); B = Math.Sin(r); C = -B; D = A; break; }
                        case 's': A = D = XfV[v++]; break;
                        case 'S': A = XfV[v++]; D = XfV[v++]; break;
                    }
                    double na = a * A + c * B, nb = b * A + d * B, nc = a * C + c * D, nd = b * C + d * D, ne = a * E + c * F + e, nf = b * E + d * F + f;
                    a = na; b = nb; c = nc; d = nd; e = ne; f = nf;
                }
                return new RigXf { A = (float)a, B = (float)b, C = (float)c, D = (float)d, E = (float)e, F = (float)f };
            }
        }

        public void T(double x, double y) { XfOp[NXf++] = 't'; int v = Vals(); XfV[v] = x; XfV[v + 1] = y; }
        public void Rot(double a) { XfOp[NXf++] = 'r'; XfV[Vals()] = a; }
        public void Sc(double k) { XfOp[NXf++] = 's'; XfV[Vals()] = k; }
        public void Sc(double x, double y) { XfOp[NXf++] = 'S'; int v = Vals(); XfV[v] = x; XfV[v + 1] = y; }
        /// <summary>Where the last op's numbers start.</summary>
        int Vals() { int v = 0; for (int i = 0; i < NXf - 1; i++) v += XfOp[i] == 't' || XfOp[i] == 'S' ? 2 : 1; return v; }
    }

    public sealed class RigFx
    {
        const double TAU = Math.PI * 2;
        public readonly List<RigFxShape> Shapes = new List<RigFxShape>();
        readonly List<RigFxShape> _pool = new List<RigFxShape>();
        readonly Stack<int> _open = new Stack<int>();
        readonly Dictionary<string, RigColor> _cols = new Dictionary<string, RigColor>();
        /// <summary>The dust clouds' soft gradient (the drawing's phz; RigDisplay sets it).</summary>
        public RigGradient Haze;

        static double R2(double v) => RigGeometry.R2(v);
        static double SM(double x) => RigMath.SM(x);

        // the particles' fixed outlines (drawParts' d strings) and the duck's parts
        static readonly RigPath DropD = Fixed("M0 -2 Q1.4 0.4 0 1.3 Q-1.4 0.4 0 -2Z"), HeartD = Fixed("M0 2.6 C-4.6 -0.6 -3.4 -5.6 0 -3 C3.4 -5.6 4.6 -0.6 0 2.6Z"),
            WispD = Fixed("M-5 0 Q-2 -1.6 1 0 T5 -0.6 Q7 -1.6 6 -3 Q4.6 -3.6 4.6 -2.2"), LeafD = Fixed("M0 -2.6 Q1.8 -0.8 0.3 2.3 L0 2.9 L-0.3 2.3 Q-1.8 -0.8 0 -2.6Z"),
            DuckTail = Fixed("M-3 -2.6 L-4.5 -4.2 L-2.4 -3.5Z"), DuckWing = Fixed("M-1.6 -2.6 Q0 -1.2 1.4 -2.6"), NoteD = Fixed("M1.3 -0.3 L1.3 -5 Q3 -4.2 3.3 -2.6");

        static RigPath Fixed(string d)
        {
            var o = new RigPath(); var tk = RigGeometry.Tokens(d);
            foreach (var t in tk)
                if (char.IsLetter(t[0])) o.C(t[0]);
                else o.V.Add(double.Parse(t, System.Globalization.CultureInfo.InvariantCulture));
            return o;
        }

        RigFxShape Next(RigFxKind k)
        {
            if (Shapes.Count == _pool.Count) _pool.Add(new RigFxShape());
            var s = _pool[Shapes.Count];
            s.Kind = k; s.D = null; s.Fill = null; s.Stroke = null; s.FillP.IsNone = false; s.FillP.Grad = null; s.FillP.Color = new RigColor(0, 0, 0, 1);
            s.StrokeP.IsNone = true; s.StrokeP.Grad = null; s.Width = double.NaN; s.Cap = null; s.Opacity = double.NaN; s.NXf = 0; s.End = 0;
            Shapes.Add(s); return s;
        }

        RigFxShape Path(RigPath fixedD = null) { var s = Next(RigFxKind.Path); if (fixedD != null) s.D = fixedD; else { s.D = s.Own; s.Own.Clear(); } return s; }
        RigFxShape Circle(double cx, double cy, double r) { var s = Next(RigFxKind.Circle); s.Cx = cx; s.Cy = cy; s.Rx = s.Ry = r; return s; }
        RigFxShape Ellipse(double cx, double cy, double rx, double ry) { var s = Next(RigFxKind.Ellipse); s.Cx = cx; s.Cy = cy; s.Rx = rx; s.Ry = ry; return s; }
        RigFxShape Group() { _open.Push(Shapes.Count); return Next(RigFxKind.Group); }
        void EndGroup() { int g = _open.Pop(); Shapes[g].End = Shapes.Count; Next(RigFxKind.End); }

        /// <summary>A fill / stroke as written: a colour, 'none', or the dust gradient (url(#phz…)).</summary>
        void Paint(RigPaint p, string v, bool fill)
        {
            p.Grad = null; p.IsNone = false;
            if (v == "none") { p.IsNone = true; return; }
            if (v.StartsWith("url(#phz", StringComparison.Ordinal)) { p.Grad = Haze; p.IsNone = Haze == null; return; }
            if (!_cols.TryGetValue(v, out var c)) { if (!RigColor.TryParse(v, out c)) c.A = -1; _cols[v] = c; }
            if (c.A < 0) { p.IsNone = !fill; p.Color = new RigColor(0, 0, 0, 1); return; }   // an invalid value is ignored (fill: black)
            p.Color = c;
        }
        void F(RigFxShape s, string v) { s.Fill = v ?? "undefined"; Paint(s.FillP, s.Fill, true); }
        void S(RigFxShape s, string v, double w) { s.Stroke = v ?? "undefined"; Paint(s.StrokeP, s.Stroke, false); s.Width = w; }

        /// <summary>The layer this frame: the engine's particles, then the effect of its move t ms in; topW: the head's top (RigRender.TopW).</summary>
        public void Build(RigEngine E, double now, (double x, double y) topW)
        {
            Shapes.Clear(); _open.Clear();
            Particles(E.Parts);
            Move(E.CurName, now - E.CurT0, topW);
        }

        // drawParts: each particle as the game writes it
        void Particles(List<RigPart> parts)
        {
            foreach (var p in parts)
            {
                double u = p.Age / p.Life, op = R2(Math.Min(1, u * 8) * (1 - SM((u - 0.7) / 0.3)));
                double tx = R2(p.X), ty = R2(p.Y);
                RigFxShape s;
                switch (p.Kind)
                {
                    case "dot": s = Circle(R2(p.X), R2(p.Y), R2(p.R)); F(s, p.Col); s.Opacity = op; break;
                    case "drop":
                        s = Path(DropD); s.T(tx, ty); s.Rot(R2(Math.Atan2(-p.Vx, p.Vy) / RigMath.D2R)); s.Sc(R2(p.R));
                        F(s, p.Col); S(s, "#ffffff", 0.3); s.Opacity = op; break;
                    case "bubble":
                    {
                        double pop = SM((u - 0.88) / 0.12), rr = p.R * (1 + pop * 0.4);
                        var g = Group(); g.Opacity = R2(op * (1 - pop));
                        s = Circle(R2(p.X + Math.Sin(p.Age / 300 + p.Ph) * 1.2), R2(p.Y), R2(rr)); F(s, "rgba(215,240,255,0.35)"); S(s, "#ffffff", 0.45);
                        s = Circle(R2(p.X - rr * 0.35), R2(p.Y - rr * 0.35), R2(rr * 0.22)); F(s, "#ffffff");
                        EndGroup(); break;
                    }
                    case "star": s = Path(); Star4(s.D, p.X, p.Y, p.R * (0.6 + 0.6 * Math.Sin(Math.PI * u))); F(s, p.Col); s.Opacity = op; break;
                    case "heart": s = Path(HeartD); s.T(tx, ty); s.Sc(R2(p.R * (0.8 + u * 0.5))); F(s, "#ff5c9e"); S(s, "#ffffff", 0.5); s.Opacity = op; break;
                    case "wave":
                    {
                        double w = Math.Sin(p.Age / 260 + p.Ph) * 1.2;
                        s = Path(); s.T(tx, ty); s.Sc(R2(p.R));
                        var d = s.D; d.C('M'); d.P(0, 0); d.C('q'); d.P(-1.6 + w, -2); d.P(0, -4); d.C('q'); d.P(1.6 + w, -2); d.P(0, -4);
                        S(s, p.Col, 1.1); F(s, "none"); s.Cap = "round"; s.Opacity = R2(op * 0.9); break;
                    }
                    case "wisp":
                    {
                        double sc = p.R * (0.8 + u * 0.6), dr = p.Vx < 0 ? -1 : 1;
                        s = Path(WispD); s.T(tx, ty); s.Sc(R2(dr * sc), R2(sc));
                        S(s, p.Col, R2(0.9 / sc)); F(s, "none"); s.Cap = "round"; s.Opacity = R2(op * 0.9); break;
                    }
                    case "leaf":
                    {
                        double sw = Math.Sin(p.Age / 260 + p.Ph);
                        s = Path(LeafD); s.T(R2(p.X + sw * 2.2), R2(p.Y)); s.Rot(R2(p.Ph * 57 + sw * 55)); s.Sc(R2(p.R));
                        F(s, p.Col); S(s, "#ffffff", 0.25); s.Opacity = op; break;
                    }
                    case "duck":
                    {
                        double sq = p.Landed && p.Age - p.Land < 140 ? 1 - SM((p.Age - p.Land) / 140) : 0, wd = Math.Sin(p.Age / 110 + p.Ph) * 14, dr = p.Vx < 0 ? -1 : 1, sc = p.R * 1.5;
                        var g = Group(); g.T(tx, ty); g.Sc(R2(dr * sc * (1 + sq * 0.18)), R2(sc * (1 - sq * 0.22))); g.Rot(R2(wd)); g.Opacity = op;
                        F(Ellipse(0.2, -2, 3.5, 2.3), "#e9a512"); F(Ellipse(-0.2, -2.4, 3.1, 1.9), "#ffd43b"); F(Path(DuckTail), "#ffd43b");
                        s = Path(DuckWing); S(s, "#e9a512", 0.5); F(s, "none"); s.Cap = "round";
                        F(Circle(1.95, -4.85, 1.85), "#e9a512"); F(Circle(1.8, -5, 1.75), "#ffd43b"); F(Ellipse(3.7, -4.6, 1.05, 0.5), "#ff8a2a");
                        F(Circle(2.4, -5.4, 0.36), "#2a1a30"); F(Ellipse(1.1, -5.8, 0.6, 0.35), "#fff8d0"); s = Ellipse(-1.2, -3.3, 1, 0.4); F(s, "#fff8d0"); s.Opacity = 0.8;
                        EndGroup(); break;
                    }
                    case "haze": s = Circle(R2(p.X), R2(p.Y), R2(p.R * (0.8 + u * 1.1))); F(s, "url(#phz)"); s.Opacity = op; break;   // a soft cloud of dust that spreads as it rises
                    case "puff": s = Circle(R2(p.X), R2(p.Y), R2(p.R * (0.7 + u * 0.8))); F(s, p.Col); s.Opacity = R2(op * 0.85); break;
                    case "seed": s = Ellipse(R2(p.X), R2(p.Y), R2(p.R * 0.7), R2(p.R)); F(s, p.Col); s.Opacity = op; break;
                    case "note":
                    {
                        var g = Group(); g.T(tx, ty); g.Rot(R2(Math.Sin(p.Age / 200) * 12)); g.Opacity = op;
                        s = Ellipse(0, 0, 1.5, 1.1); F(s, p.Col); s.Rot(-20);
                        s = Path(NoteD); S(s, p.Col, 0.8); F(s, "none"); s.Cap = "round";
                        EndGroup(); break;
                    }
                }
            }
        }

        static readonly string White = "#ffffff", Pale = "#fff3a0", Purple = "#8f3cf2";

        // FX[move]
        void Move(string move, double t, (double x, double y) topW)
        {
            if (move == "happy")
                for (int i = 0; i < 8; i++)
                {
                    double a = (double)i / 8 * TAU + i * 0.7, u = (t / 900 + (double)i / 8) % 1, r = 36 * (0.55 + 0.45 * u);
                    var s = Path();
                    Star4(s.D, 50 + Math.Cos(a) * r, 52 + Math.Sin(a) * r * 0.8, 1.2 + 2.2 * Math.Sin(Math.PI * u));
                    F(s, i % 2 != 0 ? Pale : White); s.Opacity = R2(Math.Sin(Math.PI * u));
                }
            else if (move == "surprised")
            {
                double u = RigMath.Clamp((t - 300) / 700, 0, 1);
                if (u <= 0 || u >= 1) return;
                double x0 = topW.x, y0 = topW.y, aa = Math.Sin(Math.PI * u);
                for (int k = 0; k < 3; k++)
                {
                    double d = k == 0 ? -34 : k == 1 ? 0 : 34;
                    double rr = d * RigMath.D2R, sz = 6 + u * 4, x1 = x0 + Math.Sin(rr) * sz, y1 = y0 - 3 - Math.Cos(rr) * sz, x2 = x0 + Math.Sin(rr) * (sz + 5), y2 = y0 - 3 - Math.Cos(rr) * (sz + 5);
                    var s = Path(); s.D.C('M'); s.D.P(x1, y1); s.D.C('L'); s.D.P(x2, y2);
                    S(s, Purple, 1.6); s.Cap = "round"; s.Opacity = R2(aa);
                }
            }
        }

        // star4(x, y, r): a four-point star of quadratic curves through its centre
        static void Star4(RigPath o, double x, double y, double r)
        {
            o.C('M'); o.P(x, y - r);
            o.C('Q'); o.P(x, y); o.P(x + r, y);
            o.C('Q'); o.P(x, y); o.P(x, y + r);
            o.C('Q'); o.P(x, y); o.P(x - r, y);
            o.C('Q'); o.P(x, y); o.P(x, y - r);
            o.C('Z');
        }
    }
}
