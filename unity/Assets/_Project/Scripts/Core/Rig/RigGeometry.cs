// The rig's geometry helpers (rive-rig.js: parsePath, ellS, ellAx, toD, smoothD, closedD, hull) ported exactly, and the
// path type they write into. The game writes each frame's shapes as SVG path strings of numbers rounded to hundredths
// (r2); here the same commands and the same rounded numbers go into a RigPath (no strings, so nothing to garbage-collect
// on a phone), which the drawing turns into meshes and the check compares with the game's strings.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PlushCrush.Core.Rig
{
    /// <summary>A path segment: M / L (one point), Q (two), C (three), Z (none). P holds x, y pairs.</summary>
    public sealed class RigSeg
    {
        public char C;
        public double[] P;
        public RigSeg(char c, params double[] p) { C = c; P = p; }
        public int N => P.Length / 2;
    }

    public delegate (double x, double y) RigMap(double x, double y);

    /// <summary>One drawn outline: commands (M L Q C Z) and their numbers, already rounded as the game rounds them.</summary>
    public sealed class RigPath
    {
        public readonly List<char> Cmd = new List<char>(48);
        public readonly List<int> At = new List<int>(48);      // where each command's numbers start in V
        public readonly List<double> V = new List<double>(160);
        public int Version;                                    // changes whenever the path is rewritten

        public void Clear() { Cmd.Clear(); At.Clear(); V.Clear(); Version++; }
        public void C(char c) { Cmd.Add(c); At.Add(V.Count); }
        /// <summary>A point, rounded to hundredths (r2) as the game writes it.</summary>
        public void P(double x, double y) { V.Add(RigGeometry.R2(x)); V.Add(RigGeometry.R2(y)); }
        public void P((double x, double y) q) { V.Add(RigGeometry.R2(q.x)); V.Add(RigGeometry.R2(q.y)); }
        public int Count(int i) => (i + 1 < At.Count ? At[i + 1] : V.Count) - At[i];
        public bool Empty => Cmd.Count == 0;

        public void CopyFrom(RigPath o)
        {
            Clear();
            Cmd.AddRange(o.Cmd); At.AddRange(o.At); V.AddRange(o.V);
        }

        /// <summary>As an SVG path string (for logs and checks; the numbers as JS prints them).</summary>
        public override string ToString()
        {
            var s = new StringBuilder();
            for (int i = 0; i < Cmd.Count; i++)
            {
                if (i > 0 && Cmd[i] != 'Z') s.Append(' ');
                s.Append(Cmd[i]);
                int a = At[i], n = Count(i);
                for (int k = 0; k < n; k++) { if (k > 0) s.Append(' '); s.Append(RigGeometry.Js(V[a + k])); }
            }
            return s.ToString();
        }
    }

    public static class RigGeometry
    {
        const double TAU = Math.PI * 2, D2R = Math.PI / 180;

        /// <summary>rive-rig.js r2: rounded to hundredths, halves up (Math.round).</summary>
        public static double R2(double n) => JsRound(n * 100) / 100;

        /// <summary>JS Math.round: the nearest integer, halves toward +infinity (exact also just below a half).</summary>
        public static double JsRound(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        /// <summary>JS Math.hypot as V8 computes it (scaled by the largest value, Kahan-summed), so results match to the bit.</summary>
        public static double Hypot(double a, double b)
        {
            a = Math.Abs(a); b = Math.Abs(b);
            if (double.IsInfinity(a) || double.IsInfinity(b)) return double.PositiveInfinity;
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            double max = Math.Max(a, b);
            if (max == 0) return 0;
            double sum = 0, comp = 0;
            double n = a / max, sm = n * n - comp, pre = sum + sm; comp = (pre - sum) - sm; sum = pre;
            n = b / max; sm = n * n - comp; pre = sum + sm; sum = pre;
            return Math.Sqrt(sum) * max;
        }

        public static double Hypot(double a, double b, double c)
        {
            a = Math.Abs(a); b = Math.Abs(b); c = Math.Abs(c);
            if (double.IsInfinity(a) || double.IsInfinity(b) || double.IsInfinity(c)) return double.PositiveInfinity;
            if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(c)) return double.NaN;
            double max = Math.Max(a, Math.Max(b, c));
            if (max == 0) return 0;
            double sum = 0, comp = 0;
            foreach (var v in new[] { a, b, c }) { double n = v / max, sm = n * n - comp, pre = sum + sm; comp = (pre - sum) - sm; sum = pre; }
            return Math.Sqrt(sum) * max;
        }

        /// <summary>A number as JS prints it (String(n)): shortest round-trip form, "0" for -0, "NaN".</summary>
        public static string Js(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsPositiveInfinity(v)) return "Infinity";
            if (double.IsNegativeInfinity(v)) return "-Infinity";
            if (v == 0) return "0";
            if (Math.Abs(v) < 1e21 && v == Math.Floor(v)) return v.ToString("F0", CultureInfo.InvariantCulture);
            string s = v.ToString("R", CultureInfo.InvariantCulture);
            int e = s.IndexOf('E');
            if (e < 0) return s;
            int ex = int.Parse(s.Substring(e + 1), CultureInfo.InvariantCulture);
            return s.Substring(0, e) + "e" + (ex >= 0 ? "+" : "-") + Math.Abs(ex).ToString(CultureInfo.InvariantCulture);
        }

        // ---- parsePath ----
        static readonly Regex Tok = new Regex(@"[a-zA-Z]|-?\d*\.?\d+(?:e-?\d+)?", RegexOptions.Compiled);

        /// <summary>The tokens of a path string as the game splits them (letters and numbers).</summary>
        public static List<string> Tokens(string d)
        {
            var l = new List<string>();
            foreach (Match m in Tok.Matches(d ?? "")) l.Add(m.Value);
            return l;
        }

        public static List<RigSeg> ParsePath(string d)
        {
            var tk = Tokens(d);
            int i = 0; string cmd = ""; double x = 0, y = 0, sx = 0, sy = 0;
            var o = new List<RigSeg>();
            double n() => double.Parse(tk[i++], NumberStyles.Float, CultureInfo.InvariantCulture);
            bool isLetter(string s) => s.Length == 1 && char.IsLetter(s[0]);
            while (i < tk.Count)
            {
                if (isLetter(tk[i])) cmd = tk[i++];
                string C = cmd.ToUpperInvariant(); bool rel = cmd != C; double ox = rel ? x : 0, oy = rel ? y : 0;
                if (C == "Z") { o.Add(new RigSeg('Z')); x = sx; y = sy; continue; }
                if (C == "M" || C == "L")
                {
                    double px = ox + n(), py = oy + n();
                    o.Add(new RigSeg(C[0], px, py)); x = px; y = py;
                    if (C == "M") { sx = x; sy = y; cmd = rel ? "l" : "L"; }
                }
                else if (C == "Q") { double ax = ox + n(), ay = oy + n(), bx = ox + n(), by = oy + n(); o.Add(new RigSeg('Q', ax, ay, bx, by)); x = bx; y = by; }
                else if (C == "C") { double ax = ox + n(), ay = oy + n(), bx = ox + n(), by = oy + n(), cx = ox + n(), cy = oy + n(); o.Add(new RigSeg('C', ax, ay, bx, by, cx, cy)); x = cx; y = cy; }
                else i++;
            }
            return o;
        }

        // ---- toD: segments through a mapping ----
        public static void ToD(RigPath o, List<RigSeg> segs, RigMap F)
        {
            for (int s = 0; s < segs.Count; s++)
            {
                var g = segs[s];
                o.C(g.C);
                for (int i = 0; i < g.N; i++) o.P(F(g.P[2 * i], g.P[2 * i + 1]));
            }
        }

        // ---- ellS: an ellipse as n cubic segments, mapped straight into a path ----
        public static void EllS(RigPath o, double cx, double cy, double rx, double ry, double rot, int n, RigMap F)
        {
            if (n == 0) n = 8;
            double k = 4.0 / 3 * Math.Tan(Math.PI / (2 * n)), c = Math.Cos(rot * D2R), s = Math.Sin(rot * D2R);
            o.C('M'); o.P(F(cx + rx * c - 0 * s, cy + rx * s + 0 * c));
            for (int i = 0; i < n; i++)
            {
                double a0 = (double)i / n * TAU, a1 = (double)(i + 1) / n * TAU, c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
                double x, y;
                o.C('C');
                x = rx * (c0 - k * s0); y = ry * (s0 + k * c0); o.P(F(cx + x * c - y * s, cy + x * s + y * c));
                x = rx * (c1 + k * s1); y = ry * (s1 - k * c1); o.P(F(cx + x * c - y * s, cy + x * s + y * c));
                x = rx * c1; y = ry * s1; o.P(F(cx + x * c - y * s, cy + x * s + y * c));
            }
            o.C('Z');
        }

        /// <summary>ellAx: the ellipse an ellipsoid with these projected axes shows (up to three axes; a missing one is 0, 0).</summary>
        public static void EllAx(RigPath o, double cx, double cy, double a0x, double a0y, double a1x, double a1y, double a2x, double a2y, RigMap F)
        {
            double a = 0, b = 0, c = 0;
            a += a0x * a0x; b += a0x * a0y; c += a0y * a0y;
            a += a1x * a1x; b += a1x * a1y; c += a1y * a1y;
            a += a2x * a2x; b += a2x * a2y; c += a2y * a2y;
            double tr = (a + c) / 2, df = Math.Sqrt(Math.Max(0, (a - c) * (a - c) / 4 + b * b));
            EllS(o, cx, cy, Math.Sqrt(tr + df), Math.Sqrt(Math.Max(0.01, tr - df)), 0.5 * Math.Atan2(2 * b, a - c) / D2R, 8, F);
        }

        /// <summary>smoothD: an open spline through the first n points.</summary>
        public static void SmoothD(RigPath o, IList<(double x, double y)> P, int n = -1)
        {
            if (n < 0) n = P.Count;
            o.C('M'); o.P(P[0].x, P[0].y);
            for (int i = 0; i < n - 1; i++)
            {
                var p0 = i - 1 >= 0 ? P[i - 1] : P[i]; var p1 = P[i]; var p2 = P[i + 1]; var p3 = i + 2 < n ? P[i + 2] : p2;
                o.C('C');
                o.P(p1.x + (p2.x - p0.x) / 6, p1.y + (p2.y - p0.y) / 6);
                o.P(p2.x - (p3.x - p1.x) / 6, p2.y - (p3.y - p1.y) / 6);
                o.P(p2.x, p2.y);
            }
        }

        /// <summary>closedD: a closed spline through the points ('' under three).</summary>
        public static void ClosedD(RigPath o, IList<(double x, double y)> P)
        {
            int n = P.Count; if (n < 3) return;
            o.C('M'); o.P(P[0].x, P[0].y);
            for (int i = 0; i < n; i++)
            {
                var p0 = P[(i - 1 + n) % n]; var p1 = P[i]; var p2 = P[(i + 1) % n]; var p3 = P[(i + 2) % n];
                o.C('C');
                o.P(p1.x + (p2.x - p0.x) / 6, p1.y + (p2.y - p0.y) / 6);
                o.P(p2.x - (p3.x - p1.x) / 6, p2.y - (p3.y - p1.y) / 6);
                o.P(p2.x, p2.y);
            }
            o.C('Z');
        }

        static readonly Comparison<(double x, double y)> ByXY = (a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);

        /// <summary>hull: the convex hull (monotone chain), in the game's order. P is sorted in place.</summary>
        public static void Hull(List<(double x, double y)> P, List<(double x, double y)> o, List<(double x, double y)> up)
        {
            P.Sort(ByXY);
            o.Clear(); up.Clear();
            double cr((double x, double y) a, (double x, double y) b, (double x, double y) c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            for (int i = 0; i < P.Count; i++) { var p = P[i]; while (o.Count >= 2 && cr(o[o.Count - 2], o[o.Count - 1], p) <= 0) o.RemoveAt(o.Count - 1); o.Add(p); }
            for (int i = P.Count - 1; i >= 0; i--) { var p = P[i]; while (up.Count >= 2 && cr(up[up.Count - 2], up[up.Count - 1], p) <= 0) up.RemoveAt(up.Count - 1); up.Add(p); }
            if (up.Count > 0) up.RemoveAt(up.Count - 1);
            if (o.Count > 0) o.RemoveAt(o.Count - 1);
            o.AddRange(up);
        }
    }
}
