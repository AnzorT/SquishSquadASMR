// render() of rive-rig.js, continued: the body's other shapes (a 3D body, fur rings, lumps, a fluffy neck, the belly patch). Ported line for line like the rest of RigRender (see RigRender.cs);
// BodyFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] BodyFeatures = { "body3", "bodyD", "headD", "lumps", "neck3", "spots3", "belly", "bellyD" };

        RigEl _belly, _neck3, _spots3; RigEl[] _lumps;
        // D.body3: centre, radii, tilt (cos, sin)
        bool _b3; double _b3x, _b3y, _b3z, _b3rx, _b3ry, _b3rz, _b3c, _b3s;
        double[][] _lumpC, _lumpR; double[] _n3c, _n3r;
        // the belly: D.belly given, D.bellyTurn, R.belly and R.bellyP (flat(R.belly, 4) without its last point)
        bool _bellyArr, _bellyTurn; List<RigSeg> _bellySegs; List<(double x, double y)> _bellyP; double[] _bodyA;
        readonly List<(double x, double y)> _bellyPts = new List<(double x, double y)>(64);
        // D.spots3: each spot's point and normal on the body3 ellipsoid (they don't change), its size
        double[] _spX, _spY, _spZ, _spNX, _spNY, _spNZ, _spNL, _spR;
        static readonly double[] Ring10C = Ring10(false), Ring10S = Ring10(true);   // cos / sin of k / 10 * TAU
        static double[] Ring10(bool sin) { var a = new double[10]; for (int k = 0; k < 10; k++) { double t = k / 10.0 * TAU; a[k] = sin ? Math.Sin(t) : Math.Cos(t); } return a; }
        // fur rings (I.furH, I.furB)
        Fur _furH, _furB;

        static Dictionary<string, object> Obj(object v) => v as Dictionary<string, object>;
        static double[] ArrOf(Dictionary<string, object> o, string k) => o != null && o.TryGetValue(k, out var v) && v is List<object> ? RigGeo.Arr(v) : null;
        static double NumOf(Dictionary<string, object> o, string k) => o != null && o.TryGetValue(k, out var v) ? RigGeo.Num(v) : double.NaN;

        void InitBody()
        {
            var B3 = Obj(_g.Raw("body3"));
            _b3 = _g.Has("body3");
            if (_b3)
            {
                var c = ArrOf(B3, "c"); var r = ArrOf(B3, "r");
                _b3x = c[0]; _b3y = c[1]; _b3z = c[2]; _b3rx = r[0]; _b3ry = r[1]; _b3rz = r[2];
                double t3 = Or0(NumOf(B3, "tilt")) * D2R; _b3c = Math.Cos(t3); _b3s = Math.Sin(t3);
            }
            _bodyA = _g.A("body");
            // mount: I.furH = D.headD ? furRing(D.headD, D.head) : null; I.furB = D.bodyD && !D.body3 ? furRing(D.bodyD, D.body) : null
            _furH = _g.Has("headD") ? FurRing(_g.Raw("headD") as string, _g.A("head")) : null;
            _furB = _g.Has("bodyD") && !_b3 ? FurRing(_g.Raw("bodyD") as string, _bodyA) : null;
            if ((_furH != null) != _g.FurH || (_furB != null) != _g.FurB) throw new InvalidOperationException("RigRender: " + _key + "'s fur rings don't match the game's");

            if (_g.Has("belly") || _g.Has("bellyD"))
            {
                _belly = El("belly");
                _bellyArr = _g.Has("belly"); _bellyTurn = _g.Has("bellyTurn");
                _bellySegs = RigGeo.SegList(_g.RGRaw.TryGetValue("belly", out var bs) ? bs as List<object> : null);
                _bellyP = Flat(_bellySegs, 4); if (_bellyP.Count > 0) _bellyP.RemoveAt(_bellyP.Count - 1);
            }
            if (_g.Has("neck3")) { var N3 = Obj(_g.Raw("neck3")); _neck3 = El("neck3"); _n3c = ArrOf(N3, "c"); _n3r = ArrOf(N3, "r"); }
            if (_g.Has("lumps"))
            {
                var L = (List<object>)_g.Raw("lumps");
                _lumps = Els("lumps.{0}", L.Count); _lumpC = new double[L.Count][]; _lumpR = new double[L.Count][];
                for (int i = 0; i < L.Count; i++) { _lumpC[i] = ArrOf(Obj(L[i]), "c"); _lumpR[i] = ArrOf(Obj(L[i]), "r"); LumpFront.Add(false); }
            }
            if (_g.Has("spots3") && _b3)
            {
                _spots3 = El("spots3");
                var pts = (List<object>)Obj(_g.Raw("spots3"))["pts"]; int n = pts.Count;
                _spX = new double[n]; _spY = new double[n]; _spZ = new double[n]; _spNX = new double[n]; _spNY = new double[n]; _spNZ = new double[n]; _spNL = new double[n]; _spR = new double[n];
                double brx = _b3rx, bry = _b3ry, brz = _b3rz, c3 = _b3c, s3 = _b3s;
                for (int i = 0; i < n; i++)
                {
                    var p = RigGeo.Arr(pts[i]); double a = p[0], b = p[1];
                    double A = a * D2R, B = b * D2R, lx = brx * Math.Cos(B) * Math.Sin(A), ly = -bry * Math.Sin(B), lz = brz * Math.Cos(B) * Math.Cos(A),
                        X = lx, Y = ly * c3 - lz * s3, Z = ly * s3 + lz * c3, nlx = lx / (brx * brx), nly = ly / (bry * bry), nlz = lz / (brz * brz), NX = nlx, NY = nly * c3 - nlz * s3, NZ = nly * s3 + nlz * c3;
                    _spX[i] = X; _spY[i] = Y; _spZ[i] = Z; _spNX[i] = NX; _spNY[i] = NY; _spNZ[i] = NZ; _spNL[i] = Or1(RigGeometry.Hypot(NX, NY, NZ)); _spR[i] = p[2];
                }
            }
        }

        /// <summary>D.body3: the body as a turning ellipsoid.</summary>
        void Body3()
        {
            double bx = _b3x, by = _b3y, bz = _b3z, brx = _b3rx, bry = _b3ry, brz = _b3rz, c3 = _b3c, s3 = _b3s;
            RigGeometry.EllAx(Dp(Body), 50 + (bx - 50) * cyb + bz * syb, by, brx * cyb, 0, bry * s3 * syb, bry * c3, brz * c3 * syb, -brz * s3, _glob);
        }

        /// <summary>D.bodyD (fur ring, I.furB): the body outline from furPts.</summary>
        void BodyFur()
        {
            var o = Dp(Body); var pts = FurPts(_furB, yb);
            for (int i = 0; i < pts.Count; i++) { o.C(i > 0 ? 'L' : 'M'); o.P(Glob(pts[i].x, pts[i].y)); }
            o.C('Z');
        }

        /// <summary>D.headD (fur ring, I.furH): the skull outline from furPts, into skD.</summary>
        void HeadFur(RigPath skD, RigMap hF)
        {
            var pts = FurPts(_furH, yh);
            for (int i = 0; i < pts.Count; i++) { skD.C(i > 0 ? 'L' : 'M'); skD.P(hF(pts[i].x, pts[i].y)); }
            skD.C('Z');
        }

        /// <summary>D.lumps (sets LumpFront): each is over the body while its centre is nearer than the body's.</summary>
        void Lumps()
        {
            if (_lumps == null) return;
            double dB = (_b3 ? _b3z : 0) * cyb;
            for (int i = 0; i < _lumps.Length; i++)
            {
                double lx = _lumpC[i][0], ly = _lumpC[i][1], lz = _lumpC[i][2], lrx = _lumpR[i][0], lry = _lumpR[i][1], lrz = _lumpR[i][2];
                bool fr = -(lx - 50) * syb + lz * cyb > dB;
                RigGeometry.EllAx(Dp(_lumps[i]), 50 + (lx - 50) * cyb + lz * syb, ly, lrx * cyb, 0, 0, lry, lrz * syb, 0, _glob);
                LumpFront[i] = fr;
            }
        }

        /// <summary>D.neck3: a turning ellipsoid over the top of the body, under the head.</summary>
        void Neck3()
        {
            if (_neck3 == null) return;
            double nx = _n3c[0], ny = _n3c[1], nz = _n3c[2], nrx = _n3r[0], nry = _n3r[1], nrz = _n3r[2];
            RigGeometry.EllAx(Dp(_neck3), 50 + (nx - 50) * cyb + nz * syb, ny, nrx * cyb, 0, 0, nry, nrz * syb, 0, _glob);
        }

        /// <summary>D.spots3, D.belly / D.bellyD / D.bellyTurn (after the whiskers in render()).</summary>
        void Belly()
        {
            if (_spots3 != null)
            {
                double bx = _b3x, by = _b3y, bz = _b3z; var o = Dp(_spots3);
                for (int i = 0; i < _spX.Length; i++)
                {
                    double X = _spX[i], Y = _spY[i], Z = _spZ[i], NX = _spNX[i], NY = _spNY[i], NZ = _spNZ[i], r = _spR[i], fz = (-NX * syb + NZ * cyb) / _spNL[i];
                    if (fz < 0.06) continue;
                    double sx = 50 + (bx + X - 50) * cyb + (bz + Z) * syb, sy0 = by + Y, nxs = NX * cyb + NZ * syb, nys = NY, nl = Or1(RigGeometry.Hypot(nxs, nys)), ux = nxs / nl, uy = nys / nl, mn = r * fz;
                    _bellyPts.Clear();
                    for (int k = 0; k < 10; k++) { double c = Ring10C[k], s = Ring10S[k]; _bellyPts.Add(Glob(sx - uy * r * c + ux * mn * s, sy0 + ux * r * c + uy * mn * s)); }
                    RigGeometry.ClosedD(o, _bellyPts);
                }
                if (o.Empty) { o.C('M'); o.P(0, 0); }
            }
            if (_belly == null) return;
            if (_b3 && _bellyArr)
            {
                // painted on the body3 ellipsoid; gone before side-on
                double bx = _b3x, by = _b3y, bz = _b3z, brx = _b3rx, bry = _b3ry, brz = _b3rz;
                _bellyPts.Clear();
                foreach (var p in _bellyP)
                {
                    double x = p.x, y = p.y, dx = x - bx, v = Clamp((y - by) / bry, -0.98, 0.98), cw = Math.Sqrt(1 - v * v), w = brx * cw, dd = brz * cw,
                        q = 1 - (dx / w) * (dx / w), z = bz + dd * Math.Sqrt(Math.Max(0, q)), rim = RigGeometry.Hypot(w * cyb, dd * syb), Xc = bz * syb;
                    _bellyPts.Add(Glob(50 + Clamp(dx * cyb + z * syb, Xc - rim, Xc + rim), y));
                }
                RigGeometry.ClosedD(Dp(_belly), _bellyPts); Op(_belly, R2(SM((cyb - 0.15) / 0.45)));
            }
            else if (_bellyTurn && _bellyArr)
            {
                // bellyTurn: painted on the body's front, sliding to the rim and fading as it goes round the back
                double BX = _bodyA[0], BY = _bodyA[1], BW = _bodyA[2], BH = _bodyA.Length > 3 ? _bodyA[3] : double.NaN;
                _bellyPts.Clear();
                foreach (var p in _bellyP)
                {
                    double x = p.x, y = p.y, dx = x - BX, v = Clamp((y - BY) / BH, -1, 1), w = BW * Math.Sqrt(1 - v * v), wc = BW * Math.Sqrt(1 - v * v * 0.36), q = wc * wc - dx * dx, z = q > 0 ? Math.Sqrt(q) : 0;
                    double X = dx * cyb + z * syb; if (-dx * syb + z * cyb < 0) X = (X < 0 ? -1 : 1) * wc;
                    _bellyPts.Add(Glob(BX + Clamp(X, -w, w), y));
                }
                RigGeometry.ClosedD(Dp(_belly), _bellyPts); Op(_belly, R2(SM((cyb + 0.3) / 0.4)));
            }
            else RigGeometry.ToD(Dp(_belly), _bellySegs, _glob);
        }

        /// <summary>rive-rig.js flat: an outline as a polygon, n points per curve.</summary>
        static List<(double x, double y)> Flat(List<RigSeg> segs, int n)
        {
            var o = new List<(double x, double y)>(); double px = 0, py = 0;
            foreach (var g in segs)
            {
                var P = g.P;
                if (g.C == 'M' || g.C == 'L') { px = P[0]; py = P[1]; o.Add((px, py)); }
                else if (g.C == 'Q')
                {
                    for (int i = 1; i <= n; i++) { double t = (double)i / n, m = 1 - t; o.Add((m * m * px + 2 * m * t * P[0] + t * t * P[2], m * m * py + 2 * m * t * P[1] + t * t * P[3])); }
                    px = P[2]; py = P[3];
                }
                else if (g.C == 'C')
                {
                    for (int i = 1; i <= n; i++)
                    {
                        double t = (double)i / n, m = 1 - t;
                        o.Add((m * m * m * px + 3 * m * m * t * P[0] + 3 * m * t * t * P[2] + t * t * t * P[4], m * m * m * py + 3 * m * m * t * P[1] + 3 * m * t * t * P[3] + t * t * t * P[5]));
                    }
                    px = P[4]; py = P[5];
                }
            }
            return o;
        }

        // ---- fur rings (furRing / furPts): the scalloped outline as puffs on four rings through the top and bottom of the ball ----
        sealed class FurBall { public double Th, D, R, S, C; }   // S, C: sin / cos of Th
        sealed class Fur
        {
            public (double l0, FurBall[] T)[] R;
            public double Cx, Cy, Rx, Ry; public int N;
            public double[] VsA, VsR;                 // the valleys, sorted by angle (vfn)
            public double[] Rho0, SinM, CosM;         // per outline point: vfn(th) * 0.995, sin th, cos th
            public double[] Cs, Cph, Cr;              // this frame's puffs (furPts' C)
            public readonly List<(double x, double y)> Out = new List<(double x, double y)>(FurM);

            public double Vfn(double th)
            {
                th = Wrap(th); int n = VsA.Length;
                for (int i = 0; i < n; i++)
                {
                    double a0 = VsA[i], a1 = VsR[i], b0 = VsA[(i + 1) % n], b1 = VsR[(i + 1) % n];
                    double span = Wrap(b0 - a0); if (span <= 0) span += TAU;
                    double o = Wrap(th - a0); if (o < 0) o += TAU;
                    if (o <= span) return a1 + (b1 - a1) * o / span;
                }
                return VsR[0];
            }
        }
        const int FurM = 168;
        static readonly Regex FurNum = new Regex(@"-?\d+\.?\d*", RegexOptions.Compiled);
        static double Wrap(double a) => Math.Atan2(Math.Sin(a), Math.Cos(a));

        /// <summary>furRing(d, E): null unless d is 8+ quadratic scallops (M, Q, Z only).</summary>
        static Fur FurRing(string d, double[] E)
        {
            int n = 0; foreach (char ch in d) if (ch == 'Q') n++;
            if (n < 8) return null;
            foreach (char ch in d) if ("CLAHVSclahvs".IndexOf(ch) >= 0) return null;
            double cx = E[0], cy = E[1], rx = E[2], ry0 = E.Length > 3 ? E[3] : double.NaN, ry = Or1Def(ry0, rx);
            var ms = FurNum.Matches(d); var v = new double[ms.Count];
            for (int i = 0; i < v.Length; i++) v[i] = double.Parse(ms[i].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            (double th, double rr) pol(double x, double y) => (Math.Atan2((x - cx) / rx, -(y - cy) / ry), RigGeometry.Hypot((x - cx) / rx, (y - cy) / ry));
            var V = new List<(double th, double rr)>(); var Pk = new List<(double th, double rr)>();
            double p0x = v[0], p0y = v[1];
            for (int k = 2; k + 3 < v.Length; k += 4)
            {
                double c0 = v[k], c1 = v[k + 1], p1x = v[k + 2], p1y = v[k + 3];
                V.Add(pol(p0x, p0y)); bool has = false; (double th, double rr) best = default;
                for (int i = 1; i < 20; i++)
                {
                    double t = i / 20.0, u = 1 - t; var q = pol(u * u * p0x + 2 * u * t * c0 + t * t * p1x, u * u * p0y + 2 * u * t * c1 + t * t * p1y);
                    if (!has || q.rr > best.rr) { best = q; has = true; }
                }
                Pk.Add(best); p0x = p1x; p0y = p1y;
            }
            // Vs = V sorted by angle (stable, like the game's sort)
            var Vs = new List<(double th, double rr)>(V);
            for (int i = 1; i < Vs.Count; i++) { var x = Vs[i]; int j = i - 1; while (j >= 0 && Vs[j].th - x.th > 0) { Vs[j + 1] = Vs[j]; j--; } Vs[j + 1] = x; }
            var F = new Fur { Cx = cx, Cy = cy, Rx = rx, Ry = ry, N = n, VsA = new double[Vs.Count], VsR = new double[Vs.Count] };
            for (int i = 0; i < Vs.Count; i++) { F.VsA[i] = Vs[i].th; F.VsR[i] = Vs[i].rr; }
            var T = new FurBall[Pk.Count];
            for (int i = 0; i < Pk.Count; i++)
            {
                var q = Pk[i]; var pv = Pk[(i - 1 + n) % n]; var nx = Pk[(i + 1) % n];
                double dl = (Math.Abs(Wrap(q.th - pv.th)) + Math.Abs(Wrap(nx.th - q.th))) / 4, vv = (F.Vfn(q.th - dl) + F.Vfn(q.th + dl)) / 2;
                double lo = 0.001, hi = q.rr;
                for (int it = 0; it < 40; it++)
                {
                    double r0 = (lo + hi) / 2, D0 = q.rr - r0, f = D0 * Math.Cos(dl) + Math.Sqrt(Math.Max(0, r0 * r0 - D0 * D0 * Math.Sin(dl) * Math.Sin(dl))) - vv;
                    if (f > 0) hi = r0; else lo = r0;
                }
                double r = (lo + hi) / 2; T[i] = new FurBall { Th = q.th, D = q.rr - r, R = r };
            }
            var T2 = new FurBall[T.Length];
            for (int i = 0; i < T.Length; i++) { var t = T[i]; var u = T[(i + 1) % n]; T2[i] = new FurBall { Th = t.Th + Wrap(u.Th - t.Th) / 2, D = (t.D + u.D) / 2, R = (t.R + u.R) / 2 }; }
            foreach (var b in T) { b.S = Math.Sin(b.Th); b.C = Math.Cos(b.Th); }
            foreach (var b in T2) { b.S = Math.Sin(b.Th); b.C = Math.Cos(b.Th); }
            F.R = new[] { (Math.PI / 2, T), (0.0, T), (Math.PI / 4, T2), (3 * Math.PI / 4, T2) };
            int nc = 2 * T.Length + 2 * T2.Length; F.Cs = new double[nc]; F.Cph = new double[nc]; F.Cr = new double[nc];
            F.Rho0 = new double[FurM]; F.SinM = new double[FurM]; F.CosM = new double[FurM];
            for (int i = 0; i < FurM; i++) { double th = (double)i / FurM * TAU; F.Rho0[i] = F.Vfn(th) * 0.995; F.SinM[i] = Math.Sin(th); F.CosM[i] = Math.Cos(th); }
            return F;
        }
        static double Or1Def(double v, double def) => v != 0 && !double.IsNaN(v) ? v : def;

        /// <summary>furPts(F, yaw): the outline (168 points) of the ball and its puffs, turned by yaw.</summary>
        static List<(double x, double y)> FurPts(Fur F, double yaw)
        {
            int c = 0;
            foreach (var (l0, T) in F.R)
            {
                double xs = Math.Sin(l0 + yaw);
                foreach (var t in T) { double x = t.S * t.D * xs, y = t.C * t.D; F.Cs[c] = RigGeometry.Hypot(x, y); F.Cph[c] = Math.Atan2(x, y); F.Cr[c] = t.R; c++; }
            }
            F.Out.Clear();
            for (int i = 0; i < FurM; i++)
            {
                double th = (double)i / FurM * TAU, rho = F.Rho0[i];   // the head ball itself (valley size)
                for (int k = 0; k < c; k++)
                {
                    double s = F.Cs[k], r = F.Cr[k], dl = th - F.Cph[k], sn = s * Math.Sin(dl);
                    if (Math.Abs(sn) < r) { double cd = Math.Cos(dl); if (cd > 0) { double e = s * cd + Math.Sqrt(r * r - sn * sn); if (e > rho) rho = e; } }
                }
                F.Out.Add((F.Cx + F.Rx * rho * F.SinM[i], F.Cy - F.Ry * rho * F.CosM[i]));
            }
            return F.Out;
        }
    }
}
