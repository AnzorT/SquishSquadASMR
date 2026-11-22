// render() of rive-rig.js (the 20 hand-made rigs), ported line for line: each frame's pose becomes the outline of
// every part of the creature (body, head, ears, paws, tail, face) and the order the parts are stacked in. Each part
// keeps the attributes the game writes to its SVG element (d, opacity, stroke-width, cx / cy / rx / ry); the drawing
// (RigDrawing) turns them into meshes. Checked number for number against the game by tools/unity-port/rig/check.
//
// Ported so far: everything Mittens draws (RIG_PORT.md step 2). Rigs that use a branch not ported yet (webbed feet,
// wings, crests, lobe ears, a 3D body, fur rings, cheek tufts, a snout, collars, a mane, spines...) are refused when
// the renderer is made, with the feature's name, instead of being drawn wrong.
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    /// <summary>One drawn element and the attributes the rig writes to it.</summary>
    public sealed class RigEl
    {
        public const int AD = 1, AOpacity = 2, AStroke = 4, ACx = 8, ACy = 16, ARx = 32, ARy = 64;
        public readonly string Name;
        public readonly RigPath D = new RigPath();
        public double Opacity = double.NaN, StrokeWidth = double.NaN, Cx = double.NaN, Cy = double.NaN, Rx = double.NaN, Ry = double.NaN; // NaN = never written
        public int Written;   // the attributes written this frame (A* bits)
        public RigEl(string name) { Name = name; }
    }

    public sealed class RigRender
    {
        const double TAU = Math.PI * 2;
        const int ZSurf = 0, ZBack = 1, ZGiven = 2;   // sph()'s zm: undefined, 'b', a number

        readonly RigGeo _g;
        readonly string _key;
        public readonly List<RigEl> All = new List<RigEl>();
        public readonly RigEl Body, Skull, Clip, TintR, TintG, BodyG, Tail, TailH, TailB, Muzzle, Stripes, Back, Nose, NoseHL, Mouth, Mopen, Tongue, Tout, Shadow;
        public readonly RigEl[] FeetF, FeetClip, FeetP, EarO, EarClip, EarN, Arms, ArmT, TailT, Cheeks, EyeE, EyeClip, EyeH1, EyeH2, EyeTp, EyeG, Wh;

        /// <summary>The parts from back to front (render's depth sort), and as the game joins them.</summary>
        public readonly List<string> Order = new List<string>(16);
        public string OrderKey = "";
        /// <summary>Where things are on the creature this frame (I.W: eyes, paws, mouth, tail tip...), in rig units.</summary>
        public readonly Dictionary<string, (double x, double y)> W = new Dictionary<string, (double x, double y)>();
        public (double x, double y) TopW;
        public readonly (double x, double y)[] EyeW = new (double x, double y)[2];

        // set from outside, as the game's I.propArm / I.propK / I.lt.L.contact
        public string PropArm; public double? PropK; public double LightContact = 1;
        /// <summary>For the check: each ear's surface points, before the outline is taken round them.</summary>
        public Action<int, List<(double x, double y)>> EarPointsHook;

        // ---- D, read once ----
        readonly double G, Hh, HX, HY, HR, SR, HRY, SRY, KY, NX, NY, HN0, eyeR, hkx;
        readonly double? footZ, footK, armZ, armCut, earF, earK;
        readonly double[] footS, cheekR, muzzleAt, noseAt, mouthAt, backAt;
        readonly double[][] pads, cheeksAt, eyes;
        readonly double? mouthD;
        readonly bool mouthOne, tail3, hasTail, hasNoseHL;
        readonly double armD, tailK, tailW, tailHLW, tailWag;

        public RigRender(RigGeo geo, string key)
        {
            _g = geo; _key = key;
            var missing = Unported(geo);
            if (missing != null) throw new NotSupportedException("RigRender: " + key + " uses " + missing + ", not ported yet (RIG_PORT.md step 4)");

            G = geo.N("G"); Hh = G - geo.N("top");
            var head = geo.A("head"); HX = head[0]; HY = head[1]; HR = head[2];
            SR = HR + 0.5; HRY = head.Length > 3 && head[3] != 0 && !double.IsNaN(head[3]) ? head[3] : HR; SRY = HRY + 0.5; KY = SRY / SR;
            var neck = geo.A("neck"); NX = neck[0]; NY = neck[1];
            HN0 = Math.Max(8, G - neck[1] + 2);
            eyeR = geo.N("eyeR"); hkx = geo.Hkx != 0 && !double.IsNaN(geo.Hkx) ? geo.Hkx : 1;
            footZ = geo.Opt("footZ"); footK = geo.Opt("footK"); armZ = geo.Opt("armZ"); armCut = geo.Opt("armCut");
            earF = geo.Opt("earF"); earK = geo.Has("earK") ? geo.N("earK") : (double?)null;
            footS = geo.A("footS") ?? new[] { 9, 5.5, 8 };
            pads = Rows(geo.Raw("pads")) ?? new[] { new[] { 0, 1, 5, 2.8 } };
            cheekR = geo.A("cheekR") ?? new[] { 4, 2.6 };
            cheeksAt = Rows(geo.Raw("cheeks")) ?? new[] { new double[] { 33, 56 }, new double[] { 67, 56 } };
            eyes = Rows(geo.Raw("eyes"));
            muzzleAt = geo.A("muzzle") ?? new double[] { 50, 57, 16, 10 };
            noseAt = geo.A("nose") ?? new[] { 50, 54, 2.6, 1.8 };
            mouthAt = geo.A("mouth") ?? new[] { 50, 58, 3.5 };
            mouthD = geo.Opt("mouthD"); mouthOne = geo.Has("mouthOne");
            backAt = geo.A("backAt");
            armD = geo.Or("armD", 6.5);
            tail3 = geo.Has("tail3"); hasTail = geo.Has("tail");
            tailK = geo.Or("tailK", 7); tailW = geo.Or("tailW", 7); tailHLW = geo.Or("tailHLW", 0.3); tailWag = geo.Or("tailWag", 1.3);
            hasNoseHL = geo.Has("noseHL");

            RigEl el(string n) { var e = new RigEl(n); All.Add(e); return e; }
            RigEl[] els(string f, int n) { var a = new RigEl[n]; for (int i = 0; i < n; i++) a[i] = el(string.Format(f, i)); return a; }
            Body = el("body"); BodyG = el("bodyG");
            Skull = el("skull"); Clip = el("clip"); TintR = el("tintR"); TintG = el("tintG");
            FeetF = els("feet.{0}.f", 2); FeetClip = els("feet.{0}.clip", 2); FeetP = els("feet.{0}.p", 2);
            int ne = geo.Ears.Length;
            EarO = els("ears.{0}.o", ne); EarClip = els("ears.{0}.clip", ne); EarN = els("ears.{0}.n", ne);
            Arms = els("arms.{0}", 2); ArmT = els("armT.{0}", 2);
            Tail = el("tail"); TailH = el("tailH");
            if (tail3) { TailT = els("tailT.{0}", 2); TailB = el("tailB"); }
            Muzzle = el("muzzle"); Stripes = el("stripes"); Back = el("back"); Nose = el("nose");
            if (hasNoseHL) NoseHL = el("noseHL");
            Cheeks = els("cheeks.{0}", 2);
            EyeE = els("eyes.{0}.e", 2); EyeClip = els("eyes.{0}.clip", 2); EyeH1 = els("eyes.{0}.h1", 2); EyeH2 = els("eyes.{0}.h2", 2);
            EyeTp = els("eyes.{0}.tp", 2); EyeG = els("eyes.{0}.g", 2);
            Mouth = el("mouth"); Mopen = el("mopen"); Tongue = el("tongue"); Tout = el("tout");
            Wh = els("wh.{0}", geo.Wh.Count);
            Shadow = el("shadow");

            _glob = Glob; _headT = HeadT; _face = Face; _hFPuff = HFPuff; _back = (x, y) => Sph(x, y, ZBack); _earN = EarNMap; _eye = EyeMap; _whisker = WhiskerMap;
            _sp3 = geo.Tail != null ? new (double x, double y)[geo.Tail.Length] : new (double x, double y)[0];
            _tailHL = new List<(double x, double y)>(_sp3.Length);
        }

        /// <summary>The first thing this creature's rig uses that isn't ported yet, or null.</summary>
        public static string Unported(RigGeo g)
        {
            if (g.Generic) return "the generic rig (rive-generic.js)";
            if (g.FurH) return "fur rings on the head (furH)";
            if (g.FurB) return "fur rings on the body (furB)";
            foreach (var k in new[] { "body3", "lumps", "neck3", "web", "crest", "wing", "tailTaper", "tufts", "faceExtra", "snout", "hugCheeks", "eyeDome",
                                      "spots3", "belly", "bellyD", "bellyTurn", "mane", "headExtra", "crown", "pup", "collar", "hideBody" })
                if (g.Has(k)) return "D." + k;
            foreach (var e in g.Ears) if (e.Lobe != null) return "lobe ears";
            if (g.Ears.Length > 2) return "more than two ears";
            return null;
        }

        static double[][] Rows(object v)
        {
            if (!(v is List<object> l)) return null;
            var o = new double[l.Count][];
            for (int i = 0; i < l.Count; i++) o[i] = RigGeo.Arr(l[i]);
            return o;
        }

        // ---- the frame's pose, as render()'s locals ----
        RigPose P;
        double yb, yh, ya, cya, sya, cyb, syb, cyh, syh, cp, sp, bnd, sq, sy, ct, stt, hs, chr, shr, hyo, pf;
        double Px, Plift, Plean;
        bool hasMS, hasDn; double MSs, MSc, MSn, MAx, MAy, dnx, dny, dna;
        readonly RigMap _glob, _headT, _face, _hFPuff, _back, _earN, _eye, _whisker;
        readonly (double x, double y)[] _sp3;
        readonly List<(double x, double y)> _tailHL, _pts = new List<(double x, double y)>(256), _hull = new List<(double x, double y)>(64), _up = new List<(double x, double y)>(64);
        int _eBk; readonly int[] _aBk = new int[2];

        // keys: the parts' depths, in the order the game's object gets them
        readonly List<string> _kN = new List<string>(16);
        readonly List<double> _kV = new List<double>(16);
        void Key(string n, double v) { int i = _kN.IndexOf(n); if (i < 0) { _kN.Add(n); _kV.Add(v); } else _kV[i] = v; }
        double KeyOf(string n) { int i = _kN.IndexOf(n); return i < 0 ? double.NaN : _kV[i]; }

        static double Or0(double v) => v != 0 && !double.IsNaN(v) ? v : 0;
        static double Or1(double v) => v != 0 && !double.IsNaN(v) ? v : 1;
        static bool Truthy(double v) => v != 0 && !double.IsNaN(v);
        static double JsSign(double v) => double.IsNaN(v) ? double.NaN : v > 0 ? 1 : v < 0 ? -1 : v;
        static double R2(double v) => RigGeometry.R2(v);

        static RigPath Dp(RigEl e) { e.D.Clear(); e.Written |= RigEl.AD; return e.D; }
        static void Op(RigEl e, double v) { e.Opacity = v; e.Written |= RigEl.AOpacity; }
        static void Sw(RigEl e, double v) { e.StrokeWidth = v; e.Written |= RigEl.AStroke; }

        (double x, double y) Glob(double x, double y)
        {
            if (hasDn)
            {
                double ddx = x - dnx, ddy = y - dny, g = Math.Exp(-(ddx * ddx + ddy * ddy) / 90);
                if (g > 0.01) { double vx = 50 - dnx, vy = 60 - dny, L = Or1(RigGeometry.Hypot(vx, vy)); x += vx / L * dna * 4.5 * g; y += vy / L * dna * 4.5 * g; }
            }
            double u = Clamp((G - y) / Hh, 0, 1), kx = 1 + sq * (0.45 + 0.6 * 4 * u * (1 - u)), X0 = (x - 50) * kx + Plean * Math.Pow(u, 1.5), h = (G - y) * sy, X = X0, Y = -h;
            if (bnd != 0)
            {
                double hn = HN0 * sy, c = bnd / hn;
                if (h <= hn) { double a = c * h; X = (1 - Math.Cos(a)) / c + X0 * Math.Cos(a); Y = -(Math.Sin(a) / c - X0 * Math.Sin(a)); }
                else { double dh = h - hn, cb = Math.Cos(bnd), sb = Math.Sin(bnd); X = (1 - cb) / c + X0 * cb + dh * sb; Y = -(sb / c - X0 * sb + dh * cb); }
            }
            double gx = 50 + X * ct - Y * stt + Px, gy = G + X * stt + Y * ct - Plift;
            if (hasMS)
            {
                double w = SM(u * 1.5), k = 1 + MSs * w, dx = gx - MAx, dy = gy - MAy, al = (dx * MSc + dy * MSn) * k, pe = (-dx * MSn + dy * MSc) / k;
                gx = MAx + al * MSc - pe * MSn; gy = MAy + al * MSn + pe * MSc;
            }
            return (gx, gy);
        }

        (double x, double y) HeadT(double x, double y)
        {
            x = 50 + (x - 50) * (1 + hs * 0.6); y = NY - (NY - y) * (1 - hs) + hyo;
            double dx = x - NX, dy = y - NY;
            return Glob(NX + dx * chr - dy * shr, NY + dx * shr + dy * chr);
        }

        double Sz(double dx, double dy, int zm, double zv)
        {
            if (zm == ZGiven) return zv;
            double q = 1 - (dx / SR) * (dx / SR) - (dy / SRY) * (dy / SRY), z = q > 0 ? SR * Math.Sqrt(q) : 0;
            return zm == ZBack ? -z : z;
        }

        (double x, double y) Sph(double x, double y, int zm = ZSurf, double zv = 0)
        {
            double dx = x - HX, dy = y - HY, z = Sz(dx, dy, zm, zv), x1 = dx * cyh + z * syh, z1 = -dx * syh + z * cyh, X = x1, Y = dy * cp - z1 * sp * KY;
            if (zm == ZSurf && dy * sp / KY + z1 * cp < 0) { double nr = Or1(RigGeometry.Hypot(X / SR, Y / SRY)); X /= nr; Y /= nr; }
            return HeadT(HX + X, HY + Y);
        }

        double SphZ(double x, double y, int zm = ZSurf, double zv = 0)
        {
            double dx = x - HX, dy = y - HY, z = Sz(dx, dy, zm, zv), z1 = -dx * syh + z * cyh;
            return dy * sp / KY + z1 * cp;
        }

        (double x, double y) Face(double x, double y) => Sph(x, y);
        double Zc(double x, double y, int zm = ZSurf, double zv = 0) => SM((SphZ(x, y, zm, zv) - 1.5) / 6);
        (double x, double y) Byw(double x, double y, double z) => Glob(50 + (x - 50) * cyb + z * syb, y);
        double BywZ(double x, double z) => -(x - 50) * syb + z * cyb;

        (double x, double y) HFPuff(double x, double y)
        {
            double dy = y - 58, g = Math.Exp(-dy * dy / 80);
            return HeadT(x + (x < 50 ? -1 : 1) * pf * 3.2 * g * Clamp(Math.Abs(x - 50) / 20, 0, 1), y);
        }

        /// <summary>One frame: the pose (after tick()) and the engine's extras for the drawing.</summary>
        public void Render(RigPose pose, RigEngine eng, double now) => Render(pose, eng.Hs, eng.Ed, eng.Ld, eng.Ms, eng.Dent, eng.St?.PropZ, now);

        public void Render(RigPose pose, double ihs, double ied, double ild, RigStretch ms, RigDent dent, double? propZ, double now)
        {
            P = pose;
            foreach (var e in All) e.Written = 0;
            Px = P["x"]; Plift = P["lift"]; Plean = P["lean"];
            yb = (P["spin"] + P["turn"] * 40) * D2R; yh = yb + (Or0(P["hyaw"]) + Or0(P["twist"])) * 40 * D2R; ya = yb + Or0(P["twist"]) * 24 * D2R;
            cya = Math.Cos(ya); sya = Math.Sin(ya);
            double ph = P["pitch"] * 22 * D2R;
            cyb = Math.Cos(yb); syb = Math.Sin(yb); cyh = Math.Cos(yh); syh = Math.Sin(yh); cp = Math.Cos(ph); sp = Math.Sin(ph);
            bnd = Clamp(Or0(P["bend"]), -32, 32) * D2R;
            hasMS = ms != null && ms.S > 0.004;
            if (hasMS) { MSs = ms.S; MSc = ms.C; MSn = ms.N; }
            MAx = 50 + Px; MAy = G - Plift - (G - HY) * (1 - Clamp(P["sq"], -0.25, 0.45));
            sq = Clamp(P["sq"], -0.25, 0.45); sy = 1 - sq;
            double tr = P["tilt"] * D2R; ct = Math.Cos(tr); stt = Math.Sin(tr);
            hasDn = dent != null && dent.A > 0.01;
            if (hasDn) { dnx = dent.X; dny = dent.Y; dna = dent.A; }
            hs = ihs;
            double hr = P["htilt"] * D2R; chr = Math.Cos(hr); shr = Math.Sin(hr); hyo = -P["pitch"] * 1.1;
            W.Clear();

            // body, head
            RigGeometry.ToD(Dp(Body), _g.Body, _glob);
            pf = Clamp(P["puff"], -0.5, 1.2);
            RigMap hF = Truthy(pf) ? _hFPuff : _headT;
            var skD = Dp(Skull); RigGeometry.ToD(skD, _g.Skull, hF);
            Dp(Clip).CopyFrom(skD);
            double rd = Clamp(P["red"], 0, 1), gr = Clamp(P["green"], 0, 1);
            Dp(TintR).CopyFrom(skD); Op(TintR, R2(rd * 0.34)); Dp(TintG).CopyFrom(skD); Op(TintG, R2(gr * 0.26));
            if (gr > 0.01) Dp(BodyG).CopyFrom(Body.D);
            Op(BodyG, R2(gr * 0.26));
            _kN.Clear(); _kV.Clear();
            Key("body", 0); Key("head", 1); Key("collar", 1.5); Key("prop", propZ ?? 5);

            // feet: ellipsoids that keep their roundness from every side; pads painted on the front
            for (int i = 0; i < _g.Feet.Length && i < 2; i++)
            {
                var f = _g.Feet[i];
                double st = Clamp(Or0(P["sit"]), 0, 1), cy = f.y - (i == 1 ? P["footR"] : P["footL"]) + st * 4.6, z = (footZ ?? 8) + st * 7,
                    X = 50 + (f.x - 50) * (1 + st * 0.3) * cyb + z * syb, fsd = f.x < 50 ? 1 : -1;
                double FS0 = footS[0] * (1 + st * 0.06), FS1 = footS[1] * (1 + st * 0.75), FS2 = footS[2];
                var fd = Dp(FeetF[i]); RigGeometry.EllAx(fd, X, cy, FS0 * cyb, 0, fsd * st * FS1 * 0.28 * cyb, FS1, FS2 * syb, 0, _glob);
                Dp(FeetClip[i]).CopyFrom(fd);
                double zp = z + 7.5; var pd = Dp(FeetP[i]);
                foreach (var pad in pads)
                {
                    double dx = pad[0], dy = pad[1], rx = pad[2], ry = pad[3];
                    RigGeometry.EllAx(pd, 50 + (f.x * (1 + st * 0.3) - st * 15 + dx - fsd * st * dy * 0.28 - 50) * cyb + zp * syb, cy + dy * (1 + st * 0.75) - st * 0.8,
                        rx * cyb, 0, 0, ry * (1 + st * 0.6), 0, 0, _glob);
                }
                Op(FeetP[i], R2(0.9 * SM(cyb * 3)));
                Key(i == 1 ? "footR" : "footL", -(f.x - 50) * syb + z * cyb + (footK ?? -10));
                W[i == 1 ? "footR" : "footL"] = Glob(X, cy);
            }

            // ears: rounded cones whose roots sit on the curved head, bent from the root; parts buried in the head are dropped
            for (int i = 0; i < _g.Ears.Length; i++)
            {
                var e = _g.Ears[i];
                double a = (e.Flop ? (e.Side < 0 ? P["earL"] : -P["earR"]) * 0.9 : (e.Side < 0 ? -P["earL"] : P["earR"])) * D2R, px = e.Pivot[0], py = e.Pivot[1];
                _pts.Clear();
                foreach (var q in e.Secs)
                {
                    double w = Math.Pow(q.S, 1.3), c = Math.Cos(a * w), s = Math.Sin(a * w), nx = e.Nx * c - e.Ny * s, ny = e.Nx * s + e.Ny * c;
                    var (cx, cy) = Bend(px, py, a, q.Cx, q.Cy, w);
                    for (int k = 0; k < 14; k++)
                    {
                        double f = k / 14.0 * TAU, cf = Math.Cos(f), X = cx + nx * q.Hw * cf, Y = cy + ny * q.Hw * cf;
                        if (!e.Flop)
                        {
                            double wr = 1 - SM((q.S - 0.08) / 0.3);
                            if (wr > 0)
                            {
                                double dx0 = X - HX, dy0 = Y - HY, rr = RigGeometry.Hypot(dx0 / SR, dy0 / SRY);
                                if (rr > 0.94) { double r1 = 0.94 + (rr - 0.94) * (1 - wr); X = HX + dx0 * r1 / rr; Y = HY + dy0 * r1 / rr; }
                            }
                        }
                        double Z = EarZm(e, X, Y) - q.Hd * 0.15 + q.Hd * Math.Sin(f), dx = X - HX, dy = Y - HY;
                        if (e.Flop || (dx / SR) * (dx / SR) + (dy / SRY) * (dy / SRY) + (Z / SR) * (Z / SR) >= 0.81) _pts.Add(Sph(X, Y, ZGiven, Z));
                    }
                }
                EarPointsHook?.Invoke(i, _pts);
                RigGeometry.Hull(_pts, _hull, _up);
                var ed = Dp(EarO[i]); RigGeometry.ClosedD(ed, _hull); Dp(EarClip[i]).CopyFrom(ed);
                if (e.N != null) { _ear = e; _earA = a; RigGeometry.ToD(Dp(EarN[i]), e.N, _earN); }
                Op(EarN[i], R2(0.8 * SM(cyh * cp * 3)));
                string kn = i > 0 ? "earR" : "earL";
                Key(kn, e.Behind ? -99 : SphZ(px, py, ZGiven, EarZm(e, px, py)) + (e.Flop ? 2.5 : 1));
                if (earK.HasValue && !e.Behind)
                {
                    double k = KeyOf(kn) + earK.Value, hf = cyh * cp;
                    if (earF.HasValue) { if (_eBk != 0) { if (hf > earF.Value + 0.05) _eBk = 0; } else if (hf < earF.Value - 0.05) _eBk = 1; }
                    Key(kn, k > 1.35 && _eBk == 0 ? k : -40);
                }
                var tq = e.Secs[e.Secs.Length - 1]; var tb = Bend(px, py, a, tq.Cx, tq.Cy, 1);
                W[kn] = Sph(tb.x, tb.y, ZGiven, 0);
            }

            // paws: ellipsoids that swing around the body and stay round, on a tube from the shoulder
            double Ied = Or0(ied);
            for (int i = 0; i < _g.Arms.Length && i < 2; i++)
            {
                var A = _g.Arms[i]; bool Lf = A.Side < 0;
                double av = Lf ? P["armL"] : P["armR"], bse = Lf ? 20 : -20, wr = SM(Math.Abs(av) / 45), ang = ((Lf ? P["armL"] : -P["armR"]) + (bse - A.E[4]) * wr) * D2R,
                    c = Math.Cos(ang), s = Math.Sin(ang), px = A.Pivot[0], py = A.Pivot[1], vx = A.E[0] - px, vy = A.E[1] - py;
                double mhx = (Lf ? P["hLx"] : -P["hRx"]) * hkx, mhy = Lf ? P["hLy"] : P["hRy"], hy = mhy, hz = Lf ? P["hLz"] : P["hRz"],
                    cx = px + vx * c - vy * s + (Lf ? mhx : -mhx) + Ied * 0.35, cy = py + vx * s + vy * c + hy, z = (armZ ?? 9) + hz + Math.Max(0, -hy) * 0.25;
                double sx = px - A.Side * 1.2, sy0 = py + 1.5, ddx = cx - sx, ddy = cy - sy0, len = RigGeometry.Hypot(ddx, ddy);
                double r0 = A.E[4] * D2R + ang;
                if (len > 9) { double want = Math.Atan2(ddy, ddx) - Math.PI / 2, df = want - r0; df = ((df % TAU) + TAU * 1.5) % TAU - Math.PI; r0 += df * SM((len - 9) / 8); }
                double c0 = Math.Cos(r0), s0 = Math.Sin(r0);
                double a0x = A.E[2] * c0 * cya + 0 * sya, a0y = A.E[2] * s0, a1x = -A.E[3] * s0 * cya + 0 * sya, a1y = A.E[3] * c0, a2x = 0 * cya + armD * sya, a2y = 0;
                double PX = 50 + (cx - 50) * cya + z * sya;
                RigGeometry.EllAx(Dp(Arms[i]), PX, cy, a0x, a0y, a1x, a1y, a2x, a2y, _glob);
                string kn = i > 0 ? "armR" : "armL";
                Key(kn, -(cx - 50) * sya + z * cya);
                if (armCut.HasValue)
                {
                    double rf = -(A.E[0] - 50) * sya + (armZ ?? 9) * cya;
                    if (_aBk[i] != 0) { if (rf > armCut.Value + 0.6) _aBk[i] = 0; } else if (rf < armCut.Value - 0.6) _aBk[i] = 1;
                    if (_aBk[i] != 0) Key(kn, Math.Min(KeyOf(kn), 0.3));
                }
                var Swp = Glob(50 + (sx - 50) * cya + 4 * sya, sy0); var Pw = Glob(PX, cy);
                double mx = (Swp.x + Pw.x) / 2, my = (Swp.y + Pw.y) / 2, tx = Pw.x - Swp.x, ty = Pw.y - Swp.y, tl = Or1(RigGeometry.Hypot(tx, ty)), nx = -ty / tl, ny = tx / tl;
                if (nx * A.Side < 0) { nx = -nx; ny = -ny; }
                double bu = Math.Max(0, tl - 10) * 0.18;
                var at = Dp(ArmT[i]); at.C('M'); at.P(Swp); at.C('Q'); at.P(mx + nx * bu, my + ny * bu); at.P(Pw);
                Sw(ArmT[i], R2(9.5 * Math.Sqrt((1 + sq * 0.4) * sy))); Op(ArmT[i], R2(SM((len - 7.5) / 5)));
                W[Lf ? "pawL" : "pawR"] = Pw;
            }

            // tail: a tube whose spine curls back in depth, with a soft highlight along the top
            if (_g.Tail != null)
            {
                var b = _g.Tail[0]; double wa = P["wag"] * D2R, cu = P["curl"] * 40 * D2R, dnk = _key == "mittens" || _key == "duchess" ? Clamp(Or0(P["wd"]) * 1.6, 0, 1) * 1.4 : 0;
                for (int i = 0; i < _g.Tail.Length; i++)
                {
                    var t = _g.Tail[i]; double x = t[0], y = t[1], s = t[2], z = t[3];
                    double th = (tail3 ? wa * 0.3 : wa) * Math.Pow(s, 1.2) - cu * Math.Pow(s, 1.6) + dnk * Math.Pow(s, 1.1), c = Math.Cos(th), sn = Math.Sin(th), vx = x - b[0], vy = y - b[1];
                    double X = b[0] + vx * c - vy * sn, Y = b[1] + vx * sn + vy * c, Z = z;
                    if (tail3) { double tyy = wa * tailWag * Math.Pow(s, 1.1), cw = Math.Cos(tyy), sw = Math.Sin(tyy), ux = X - b[0], uz = Z - b[3]; X = b[0] + ux * cw - uz * sw; Z = b[3] + ux * sw + uz * cw; }
                    _sp3[i] = Byw(X, Y, Z);
                }
                double tw = tailK * Math.Sqrt((1 + sq * 0.5) * sy);
                RigGeometry.SmoothD(Dp(Tail), _sp3); Sw(Tail, R2(tw * tailW / 7));
                _tailHL.Clear(); for (int i = 0; i < _sp3.Length - 1; i++) _tailHL.Add((_sp3[i].x - tw * 0.16, _sp3[i].y - tw * 0.18));
                RigGeometry.SmoothD(Dp(TailH), _tailHL); Sw(TailH, R2(tw * tailW / 7 * tailHLW));
                if (TailT != null)
                {
                    RigGeometry.SmoothD(Dp(TailT[0]), _sp3, Math.Min(6, _sp3.Length)); Sw(TailT[0], R2(tw * 1.55));
                    RigGeometry.SmoothD(Dp(TailT[1]), _sp3, Math.Min(10, _sp3.Length)); Sw(TailT[1], R2(tw * 1.25));
                    var r0 = _sp3[0]; var r1 = _sp3[2]; double dx = r1.x - r0.x, dy = r1.y - r0.y, dl = Or1(RigGeometry.Hypot(dx, dy));
                    TailB.Cx = R2(r0.x + dx / dl * tw * 0.3); TailB.Cy = R2(r0.y + dy / dl * tw * 0.3); TailB.Rx = R2(tw * 1.12); TailB.Ry = R2(tw * 0.95);
                    TailB.Written |= RigEl.ACx | RigEl.ACy | RigEl.ARx | RigEl.ARy;
                    Op(TailB, R2(SM((-0.15 - cyb) / 0.5)));
                }
                if (hasTail) Key("tail", tail3 ? BywZ(64, -22) : BywZ(66, -12));
                Key("collar", 1.5);
                W["tail"] = _sp3[_sp3.Length - 1];
            }

            // face, painted on the head sphere
            RigGeometry.ToD(Dp(Muzzle), _g.Muzzle, _face); Op(Muzzle, R2(Zc(muzzleAt[0], muzzleAt[1])));
            RigGeometry.ToD(Dp(Stripes), _g.Stripes, _face); Op(Stripes, R2(Zc(50, 21)));
            RigGeometry.ToD(Dp(Back), _g.Back, _back); Op(Back, R2(Zc(backAt != null ? backAt[0] : 50, backAt != null ? backAt[1] : 23, ZBack)));
            RigGeometry.ToD(Dp(Nose), _g.Nose, _face); Op(Nose, R2(Zc(noseAt[0], noseAt[1])));
            if (NoseHL != null) { RigGeometry.ToD(Dp(NoseHL), _g.NoseHL, _face); Op(NoseHL, R2(0.7 * Zc(noseAt[0], noseAt[1]))); }
            double sm = Clamp(P["smile"], 0, 1);
            for (int i = 0; i < cheeksAt.Length && i < 2; i++)
            {
                double cx = cheeksAt[i][0], cy = cheeksAt[i][1] - sm * 0.6, pk = 1 + Math.Max(0, pf) * 0.35;
                RigGeometry.EllS(Dp(Cheeks[i]), cx + (i > 0 ? 1 : -1) * Math.Max(0, pf) * 1.2, cy, cheekR[0] * (1 + sm * 0.08) * pk, cheekR[1] * pk, 0, 8, _face);
                Op(Cheeks[i], R2(Clamp(0.5 + Clamp(P["blush"], 0, 1) * 0.45 + Math.Max(0, pf) * 0.25 + rd * 0.3, 0, 1) * Zc(cx, cy)));
            }

            // eyes
            double ox = P["lx"] * 2.6, oy = P["ly"] * 2.2, ws = 1 + Clamp(P["wide"], 0, 1) * 0.22, hh = Clamp(P["hap"], 0, 1), qz = Clamp(P["sqz"], 0, 1),
                crs = Clamp(P["cross"], 0, 1) * 1.8, tyr = Clamp(P["teary"], 0, 1);
            for (int i = 0; i < eyes.Length && i < 2; i++)
            {
                double c = Clamp(Math.Max(P["close"], i > 0 ? P["winkR"] : P["winkL"]), 0, 1) * (1 - SM(hh * 1.6)), dir = i > 0 ? -1 : 1,
                    cx = eyes[i][0] + ox + dir * crs, cy = eyes[i][1] + oy - hh * 0.8, r = eyeR * ws;
                _ecx = cx; _ecy = cy; _ec = c; _esl = Clamp(P["sl"], 0, 1) * 0.32; _eout = i > 0 ? 1 : -1; _eh = hh; _eqz = qz; _edir = dir; _er = r;
                var eD = Dp(EyeE[i]); RigGeometry.EllS(eD, cx, cy, r, r, 0, 8, _eye); Dp(EyeClip[i]).CopyFrom(eD);
                Sw(EyeE[i], R2(1.3 * Math.Max(Math.Max(SM((c - 0.5) / 0.45), SM((hh - 0.5) / 0.45)), SM(qz * 1.6))));
                double ha = (1 - SM((c - 0.4) / 0.25)) * (1 - SM((hh - 0.12) / 0.3)) * (1 - SM(qz * 2.5)), hk = ws * (1 + tyr * 0.4), hd = c * r * 0.7;
                RigGeometry.EllS(Dp(EyeH1[i]), cx - 1.7 * ws, cy - 1.9 * ws + hd, 1.8 * hk, 1.8 * hk, 0, 6, _face);
                RigGeometry.EllS(Dp(EyeH2[i]), cx + 1.8 * ws, cy + 1.6 * ws, 0.7 * hk, 0.7 * hk, 0, 6, _face);
                RigGeometry.EllS(Dp(EyeTp[i]), cx, cy + r * 0.8, r, r * 0.42, 0, 8, _face); Op(EyeTp[i], R2(tyr * 0.85 * (1 - qz) * (1 - SM(c * 1.5))));
                Op(EyeH1[i], R2(ha)); Op(EyeH2[i], R2(ha * 0.9)); Op(EyeG[i], R2(Zc(cx, cy)));
                EyeW[i] = Face(cx, cy); W[i > 0 ? "eyeR" : "eyeL"] = EyeW[i];
            }

            // mouth: the little 'w' (or one wide grin), opening into a mouth with a tongue
            {
                double MX0 = mouthAt[0], MY0 = mouthAt[1], MH0 = mouthAt[2], mk0 = mouthD ?? MH0 / 3.5;
                double op = Clamp(P["open"], 0, 1), fr = Clamp(P["frown"], 0, 1), mw = P["mw"] * (1 + sm * 0.12), hw = MH0 * mw * (1 + op * 0.3),
                    y0 = MY0 + (-sm * 0.5 + fr * 1.3) * mk0, d = (2.3 * (1 + sm * 0.45) - fr * 4) * (1 - op * 0.55) * mk0, ym = MY0 - fr * 0.2 * mk0;
                double mv = Zc(MX0, MY0);
                Lip(Dp(Mouth), MX0, hw, y0, d, ym); Op(Mouth, R2(mv));
                if (op > 0.03)
                {
                    var mo = Dp(Mopen); Lip(mo, MX0, hw, y0, d, ym);
                    mo.C('C'); mo.P(Face(R2(MX0 + hw), R2(y0 + op * 8 * mk0))); mo.P(Face(R2(MX0 - hw), R2(y0 + op * 8 * mk0))); mo.P(Face(R2(MX0 - hw), R2(y0))); mo.C('Z');
                    RigGeometry.EllS(Dp(Tongue), MX0, y0 + op * 4.2 * mk0, 2.3 * op * mw * mk0, 1.4 * op * mk0, 0, 8, _face);
                    Op(Mopen, R2(mv)); Op(Tongue, R2(mv));
                }
                else { Op(Mopen, 0); Op(Tongue, 0); }
                double tg = Clamp(P["tongue"], 0, 1);
                Op(Tout, R2(mv * SM(tg * 3)));
                if (tg > 0.02) RigGeometry.EllS(Dp(Tout), MX0, y0 + (1.3 + tg * 1.9) * mk0, 2.2 * (1 + tg * 0.15) * mk0, (1 + tg * 1.7) * mk0, 0, 8, _face);
            }
            W["mouth"] = Face(mouthAt[0], mouthAt[1]); W["nose"] = Face(noseAt[0], noseAt[1]);
            W["body"] = Glob(50, 78); W["belly"] = Glob(50, 83); W["sideL"] = Glob(30, 78); W["sideR"] = Glob(70, 78);

            // whiskers (wsk folds them to about half length and fans them apart)
            double wk = Clamp(Or0(P["wsk"]), 0, 1);
            for (int i = 0; i < _g.Wh.Count; i++)
            {
                double sd = i < 2 ? -1 : 1, far = 1 - SM((syh * sd - 0.06) / 0.22);
                var w = _g.Wh[i];
                _wFold = wk > 0.01;
                if (_wFold) { double sg = i == 0 || i == 3 ? 1 : -1, an = 0.34 * wk * sg; _wr0x = w[0].P[0]; _wr0y = w[0].P[1]; _wca = Math.Cos(an); _wsa = Math.Sin(an); _wk = 1 - 0.5 * wk; }
                RigGeometry.ToD(Dp(Wh[i]), w, _whisker);
                Op(Wh[i], R2(0.8 * Zc(i < 2 ? 36 : 64, 56) * far));
            }

            TopW = HeadT(50, _g.N("top")); W["head"] = TopW;

            // ground shadow, shrinking as the creature leaves the floor
            double gl = Math.Max(0, Plift);
            Shadow.Cx = R2(50 + Px + Plean * 0.15); Shadow.Cy = R2(G + 0.6); Shadow.Rx = R2(25 * (1 + sq * 0.45) * (1 - gl / 70)); Shadow.Ry = R2(3.6 * (1 - gl / 90));
            Shadow.Written |= RigEl.ACx | RigEl.ACy | RigEl.ARx | RigEl.ARy;
            Op(Shadow, R2(0.2 * Or1(LightContact) * (1 - gl / 50) * (1 - Clamp(Or0(P["dbb"]), 0, 1))));

            // a held prop sits just in front of the arm that holds it
            if (PropArm != null && _kN.Contains(PropArm)) Key("prop", KeyOf(PropArm) + (PropK ?? 0.3));

            // depth sort the parts as the creature turns (stable, like the game's sort)
            Order.Clear(); Order.AddRange(_kN);
            for (int i = 1; i < Order.Count; i++)
            {
                string n = Order[i]; double v = _kV[_kN.IndexOf(n)]; int j = i - 1;
                while (j >= 0 && _kV[_kN.IndexOf(Order[j])] - v > 0) { Order[j + 1] = Order[j]; j--; }
                Order[j + 1] = n;
            }
            string os = string.Join(",", Order);
            if (os != OrderKey) OrderKey = os;
        }

        // ---- per-part mappings (their parameters are set just before each use) ----
        RigEar _ear; double _earA;
        (double x, double y) Bend(double px, double py, double a, double x, double y, double w)
        {
            double vx = x - px, vy = y - py, c = Math.Cos(a * w), s = Math.Sin(a * w);
            return (px + vx * c - vy * s, py + vx * s + vy * c);
        }
        double EarZm(RigEar e, double x, double y)
        {
            double dx = x - HX, dy = y - HY, q = 1 - (dx / SR) * (dx / SR) - (dy / SRY) * (dy / SRY);
            return e.Flop ? (q > 0 ? 0.12 * SR * Math.Sqrt(q) : 1) : (q > 0 ? -0.35 * SR * Math.Sqrt(q) : 0);
        }
        double HdAt(RigEar e, double s)
        {
            var L = e.Secs; int j = 0;
            while (j < L.Length - 2 && L[j + 1].S < s) j++;
            var A = L[j]; var B = j + 1 < L.Length ? L[j + 1] : A;
            double u = B.S > A.S ? Clamp((s - A.S) / (B.S - A.S), 0, 1) : 0;
            return Lerp(A.Hd, B.Hd, u);
        }
        (double x, double y) EarNMap(double x, double y)
        {
            var e = _ear; double px = e.Pivot[0], py = e.Pivot[1];
            double s = Clamp(((x - px) * e.Ux + (y - py) * e.Uy) / e.Amax, 0, 1), hd = HdAt(e, s);
            var (bx, by) = Bend(px, py, _earA, x, y, Math.Pow(s, 1.3));
            return Sph(bx, by, ZGiven, EarZm(e, bx, by) - hd * 0.15 + hd * 0.85);
        }

        double _ecx, _ecy, _ec, _esl, _eout, _eh, _eqz, _edir, _er;
        (double x, double y) EyeMap(double x, double y)
        {
            double dy = y - _ecy, ce = Clamp(_ec + _esl * (0.5 + 0.5 * _eout * (x - _ecx) / _er), 0, 1), X = x, Y = _ecy + dy * (dy < 0 ? 1 - 2 * ce : 1 - 2 * _eh);
            if (_eqz > 0)
            {
                double cu = Clamp((x - _ecx) / _er, -1, 1), u = _edir > 0 ? (cu + 1) / 2 : (1 - cu) / 2;
                X = Lerp(X, _ecx + _edir * (-0.8 + u * 1.4) * _er, _eqz); Y = Lerp(Y, _ecy + JsSign(dy) * (1 - u) * 0.6 * _er, _eqz);
            }
            return Face(X, Y);
        }

        bool _wFold; double _wr0x, _wr0y, _wca, _wsa, _wk;
        (double x, double y) WhiskerMap(double x, double y)
        {
            if (_wFold) { double dx = (x - _wr0x) * _wk, dy = (y - _wr0y) * _wk; x = _wr0x + dx * _wca - dy * _wsa; y = _wr0y + dx * _wsa + dy * _wca; }
            return Face(x, y);
        }

        /// <summary>The lip line, from the numbers the game rounds into its path string, painted on the face.</summary>
        void Lip(RigPath o, double MX0, double hw, double y0, double d, double ym)
        {
            o.C('M'); o.P(Face(R2(MX0 - hw), R2(y0)));
            if (mouthOne) { o.C('Q'); o.P(Face(R2(MX0), R2(y0 + d * 2.2))); o.P(Face(R2(MX0 + hw), R2(y0))); return; }
            o.C('Q'); o.P(Face(R2(MX0 - hw / 2), R2(y0 + d))); o.P(Face(R2(MX0), R2(ym)));
            o.C('Q'); o.P(Face(R2(MX0 + hw / 2), R2(ym + d))); o.P(Face(R2(MX0 + hw), R2(y0)));
        }
    }
}
