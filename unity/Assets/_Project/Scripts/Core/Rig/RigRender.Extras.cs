// render() of rive-rig.js, continued: the mane (spine coat, hedgehog spines, spiky or plain), the pup, collars and tags. Ported line for line like the rest of RigRender (see RigRender.cs);
// ExtraFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] ExtraFeatures = { "mane", "pup", "collar" };

        /// <summary>The spine coat's / the hedgehog's pools (E.coat.back..., E.hog.pre...): the game writes a member only when what
        /// it shows changed (el._d, el._t, el._o; all unset before the first frame).</summary>
        sealed class SpinePool
        {
            public readonly RigEl[] D, T;   // the spine (coat: the group's first path), its pale tip (coat: the last path; hog: none)
            public readonly bool[] Had;     // written once
            public readonly double[] O;     // hog: the stroke-opacity written (el._o)
            public SpinePool(RigEl[] d, RigEl[] t) { D = d; T = t; Had = new bool[d.Length]; O = new double[d.Length]; }
        }

        sealed class CoatSp { public double[] R, N, E; public double Len, W, O, Rim, I; public readonly RigPath D = new RigPath(), T = new RigPath(); }
        sealed class HogSp { public double[] B1, B2, T, N; public double Len, W, I, Hw, O; public readonly RigPath D = new RigPath(); }
        sealed class PupPart { public double[] Ear, Eye, Joy, Cheek; public bool Shine, MouthLine; public double Op; public List<RigSeg> Segs; }

        // ---- D.mane ----
        int _maneKind;   // 0 none, 1 spine coat (E.coat), 2 hedgehog spines (E.hog), 3 spiky (R.spk), 4 plain
        bool _hasPup, _pupKey;   // E.pup has parts; D.pup (keys.pup)
        RigEl _mane, _maneF;
        // plain
        List<RigSeg> _maneSegs; bool _maneBody; double _mHCx, _mHCy, _mK, _mRp, _mWide; RigMap _maneMap;
        // spiky: each spike's direction and tangent at rest (d, tg before rot), the triangles this frame
        double _kCx, _kCy, _kRt, _kRb, _kW; double[] _kDx, _kDy, _kDz, _kTx, _kTy, _kTz, _kZ; double[][] _kP; int[] _kOrd;
        // spine coat
        RigEl _cMB, _cMBF, _cMHF; SpinePool _cBack, _cBodyF, _cHeadF; CoatSp[] _cSp; double[] _cKey;
        double[][] _mPx, _mPy, _mPz, _mW, _mQx, _mQy, _mZ; double[] _cLay, _cLayUp, _cLA = new double[3]; double _cTipF;
        readonly List<int> _lsBack = new List<int>(128), _lsBodyF = new List<int>(64), _lsHeadF = new List<int>(128);
        readonly double[] _qx = new double[4], _qy = new double[4];
        // hedgehog spines
        RigEl _hDomeB, _hDomeF; SpinePool _hPre, _hPost, _hFs; HogSp[] _hSp; double[] _hKey; int _hNa;
        double[][][] _hG, _hQN; double[][] _hGW, _hSx, _hSy, _hSz, _hWx, _hWy; double _hBX0, _hBY0, _hBW0, _hBH0;
        readonly List<int> _ls0 = new List<int>(128), _ls1 = new List<int>(128), _ls2 = new List<int>(128);
        readonly double[] _q1 = new double[3], _q2 = new double[3], _qt = new double[3], _t3 = new double[3];

        // ---- D.pup ----
        RigEl[] _pupEl; RigEl _pupG; PupPart[] _pup; double _pcx, _pcy, _pr, _pBX, _pBY, _pBW, _pBH;
        double _pjT, _pj;   // I.pjT, I.pj
        PupPart _ph; double _ppj, _pbl, _psq, _pfb, _pca, _psa, _pec, _pes, _pcs; RigMap _pupMap;

        // ---- D.collar ----
        int _collarKind;   // 0 none, 1 the 3D ring (E.cr), 2 on the head, 3 on the body, 4 plain
        RigEl _collar, _tag; List<RigSeg> _collarSegs; double _tX, _tY, _tR, _cZ; RigMap _collarMap, _tagBodyMap;
        double _cfl, _colCa, _colSa, _ccz;
        RigEl CrSh, CrHl, CrLo, CrSt, CrBk, CrBki, CrBkp, CrRg, CrTg, CrTi, CrGl; RigEl[] CrSegs, CrMk;
        double _rYs, _rAf, _rAb, _rW, _rRx, _rRz, _rPad, _rbY, _rbW, _rbH; int[] _rcD, _rcB, _rcL;
        readonly List<int> _rVis = new List<int>(48);
        readonly int[] _segRgb = new int[48]; readonly string[] _segCs = new string[48];
        bool _tgHas; double _tgA, _tgV, _tgX, _tgY, _tgVx, _tgVy, _tgT;   // I.tgp: the tag's pendulum
        double _rotA = double.NaN, _rotX = double.NaN, _rotY = double.NaN; string _rot;

        static object Get(Dictionary<string, object> o, string k) => o != null && o.TryGetValue(k, out var v) ? v : null;
        /// <summary>o.k as JS reads it in arithmetic (undefined: NaN).</summary>
        static double OrOf(Dictionary<string, object> o, string k, double def) { double v = NumOf(o, k); return Truthy(v) ? v : def; }

        void InitExtras()
        {
            var R = _g.RGRaw;
            var mane = Obj(_g.Raw("mane"));
            if (_g.Has("mane"))
            {
                _mane = El("mane");
                var hog = Obj(Get(mane, "hog"));
                bool spiky = RigGeo.Truthy(Get(mane, "spiky"));
                if (spiky) _maneF = El("maneF");
                if (RigGeo.Truthy(hog) && RigGeo.Truthy(Get(hog, "coat"))) InitCoat(Obj(R["coat"]));
                else if (RigGeo.Truthy(hog)) InitHog(Obj(R["hog"]));
                else if (spiky && Obj(Get(R, "spk")) is Dictionary<string, object> spk) InitSpiky(spk);
                else
                {
                    _maneKind = 4; _maneSegs = RigGeo.SegList(Get(R, "mane") as List<object>);
                    var hc = ArrOf(mane, "c"); _mHCx = hc != null ? hc[0] : HX; _mHCy = hc != null ? hc[1] : HY;
                    _maneBody = Get(mane, "mode") as string == "body"; _maneMap = ManeMap;
                }
            }

            _pupKey = _g.Has("pup");
            if (Get(_g.D, "pup") is List<object> pups && pups.Count > 0)
            {
                _hasPup = true;
                var segs = (List<object>)R["pup"];
                _pup = new PupPart[pups.Count]; _pupEl = Els("pup.{0}", pups.Count); _pupG = El("pg.pup");
                for (int i = 0; i < pups.Count; i++)
                {
                    var h = Obj(pups[i]);
                    _pup[i] = new PupPart
                    {
                        Ear = RigGeo.Truthy(Get(h, "ear")) ? ArrOf(h, "ear") : null, Eye = RigGeo.Truthy(Get(h, "eye")) ? ArrOf(h, "eye") : null,
                        Joy = RigGeo.Truthy(Get(h, "joy")) ? ArrOf(h, "joy") : null, Cheek = RigGeo.Truthy(Get(h, "cheek")) ? ArrOf(h, "cheek") : null,
                        Shine = RigGeo.Truthy(Get(h, "shine")), MouthLine = RigGeo.Truthy(Get(h, "mouthLine")), Op = OrOf(h, "op", 1),
                        Segs = RigGeo.SegList(segs[i] as List<object>),
                    };
                }
                var pc = _g.A("pupC") ?? new double[] { 50, 71, 11 }; _pcx = pc[0]; _pcy = pc[1]; _pr = pc[2];
                var b = _g.A("body"); _pBX = b[0]; _pBY = b[1]; _pBW = b[2]; _pBH = b[3];
                _pupMap = PupMap;
            }

            var col = Obj(_g.Raw("collar"));
            if (_g.Has("collar"))
            {
                var tag = Obj(_g.Raw("tag"));
                if (RigGeo.Truthy(Get(col, "ring"))) InitRing(col, tag);
                else
                {
                    _collarKind = RigGeo.Truthy(Get(col, "head")) ? 2 : RigGeo.Truthy(Get(col, "body")) ? 3 : 4;
                    _collar = El("collar"); _tag = El("tag");
                    _collarSegs = RigGeo.SegList(Get(R, "collar") as List<object>);
                    _tX = tag != null ? NumOf(tag, "x") : 50; _tY = tag != null ? NumOf(tag, "y") : 66; _tR = NumOf(tag, "r");
                    _cZ = OrOf(col, "z", 14);
                    _collarMap = _collarKind == 2 ? (RigMap)CollarHeadMap : CollarBodyMap; _tagBodyMap = (x, y) => Byw(x, y, _ccz);
                }
            }
        }

        void InitSpiky(Dictionary<string, object> K)
        {
            _maneKind = 3;
            var C = ArrOf(K, "C"); _kCx = C[0]; _kCy = C[1]; _kRt = NumOf(K, "rt"); _kRb = NumOf(K, "rb"); _kW = NumOf(K, "w");
            var list = (List<object>)K["list"]; int n = list.Count;
            _kDx = new double[n]; _kDy = new double[n]; _kDz = new double[n]; _kTx = new double[n]; _kTy = new double[n]; _kTz = new double[n];
            _kZ = new double[n]; _kP = new double[n][]; _kOrd = new int[n];
            for (int i = 0; i < n; i++)
            {
                var q = Obj(list[i]); double th = NumOf(q, "th"), t = NumOf(q, "t");
                double sT = Math.Sin(th), ct2 = Math.Cos(t), st2 = Math.Sin(t);
                _kDx[i] = sT * ct2; _kDy[i] = sT * st2; _kDz[i] = -Math.Cos(th);
                if (Truthy(th)) { _kTx[i] = -st2; _kTy[i] = ct2; _kTz[i] = 0; } else { _kTx[i] = 1; _kTy[i] = 0; _kTz[i] = 0; }
                _kP[i] = new double[6];
            }
        }

        void InitCoat(Dictionary<string, object> K)
        {
            _maneKind = 1;
            var mesh = (List<object>)K["mesh"]; int nr = mesh.Count;
            _mPx = new double[nr][]; _mPy = new double[nr][]; _mPz = new double[nr][]; _mW = new double[nr][]; _mQx = new double[nr][]; _mQy = new double[nr][]; _mZ = new double[nr][];
            for (int r = 0; r < nr; r++)
            {
                var row = (List<object>)mesh[r]; int n = row.Count;
                _mPx[r] = new double[n]; _mPy[r] = new double[n]; _mPz[r] = new double[n]; _mW[r] = new double[n]; _mQx[r] = new double[n]; _mQy[r] = new double[n]; _mZ[r] = new double[n];
                for (int i = 0; i < n; i++) { var v = Obj(row[i]); var p = ArrOf(v, "p"); _mPx[r][i] = p[0]; _mPy[r][i] = p[1]; _mPz[r][i] = p[2]; _mW[r][i] = NumOf(v, "w"); }
            }
            var sp = (List<object>)K["sp"];
            _cSp = new CoatSp[sp.Count]; _cKey = new double[sp.Count];
            for (int i = 0; i < sp.Count; i++)
            {
                var s = Obj(sp[i]);
                _cSp[i] = new CoatSp { R = ArrOf(s, "r"), N = ArrOf(s, "n"), E = ArrOf(s, "e"), Len = NumOf(s, "len"), W = NumOf(s, "w"), O = NumOf(s, "o"), Rim = NumOf(s, "rim"), I = NumOf(s, "i") };
            }
            _cLay = ArrOf(K, "lay"); _cLayUp = ArrOf(K, "layUp"); _cTipF = NumOf(K, "tipF");
            SpinePool pool(string n, int c) => new SpinePool(Els("coat." + n + ".{0}/path.0", c), Els("coat." + n + ".{0}/path.1", c));
            _cMB = El("coat.mB"); _cBack = pool("back", sp.Count);
            _cMBF = El("coat.mBF"); _cBodyF = pool("bodyF", (int)NumOf(K, "nB"));
            _cMHF = El("coat.mHF"); _cHeadF = pool("headF", (int)NumOf(K, "nH"));
        }

        static double HogHw(double x, double y, double z) => SM((60 - y) / 18) * SM((z + 26) / 14);

        void InitHog(Dictionary<string, object> K)
        {
            _maneKind = 2;
            var G = (List<object>)K["G"]; var QN = (List<object>)K["QN"];
            _hNa = ((List<object>)K["A"]).Count;
            int nb = ((List<object>)K["B"]).Count;
            _hG = new double[G.Count][][]; for (int j = 0; j < G.Count; j++) _hG[j] = Rows(G[j]);
            _hQN = new double[QN.Count][][]; for (int j = 0; j < QN.Count; j++) _hQN[j] = Rows(QN[j]);
            // K.GW and each spike's s.hw are worked out on the first frame (exported once that has run)
            _hGW = Get(K, "GW") is List<object> gw ? Rows(gw) : null;
            if (_hGW == null) { _hGW = new double[G.Count][]; for (int j = 0; j < G.Count; j++) { _hGW[j] = new double[_hG[j].Length]; for (int i = 0; i < _hG[j].Length; i++) _hGW[j][i] = HogHw(_hG[j][i][0], _hG[j][i][1], _hG[j][i][2]); } }
            _hSx = new double[nb][]; _hSy = new double[nb][]; _hSz = new double[nb][]; _hWx = new double[nb][]; _hWy = new double[nb][];
            for (int j = 0; j < nb && j < G.Count; j++) { int n = _hG[j].Length; _hSx[j] = new double[n]; _hSy[j] = new double[n]; _hSz[j] = new double[n]; _hWx[j] = new double[n]; _hWy[j] = new double[n]; }
            var sp = (List<object>)K["sp"];
            _hSp = new HogSp[sp.Count]; _hKey = new double[sp.Count];
            for (int i = 0; i < sp.Count; i++)
            {
                var s = Obj(sp[i]);
                var h = _hSp[i] = new HogSp { B1 = ArrOf(s, "b1"), B2 = ArrOf(s, "b2"), T = ArrOf(s, "t"), N = ArrOf(s, "n"), Len = NumOf(s, "len"), W = NumOf(s, "w"), I = NumOf(s, "i") };
                h.Hw = s.ContainsKey("hw") ? NumOf(s, "hw") : HogHw((h.B1[0] + h.B2[0]) / 2, (h.B1[1] + h.B2[1]) / 2, (h.B1[2] + h.B2[2]) / 2);
            }
            var b = _g.A("body"); _hBX0 = b[0]; _hBY0 = b[1]; _hBW0 = b[2]; _hBH0 = b[3];
            int c = sp.Count;
            _hPre = new SpinePool(Els("hog.pre.{0}", c), null); _hDomeB = El("hog.domeB"); _hPost = new SpinePool(Els("hog.post.{0}", c), null);
            _hDomeF = El("hog.domeF"); _hFs = new SpinePool(Els("hog.fs.{0}", c), null);
        }

        void InitRing(Dictionary<string, object> col, Dictionary<string, object> tag)
        {
            _collarKind = 1;
            var C = Obj(col["ring"]);
            _rYs = NumOf(C, "ys"); _rAf = NumOf(C, "af"); _rAb = NumOf(C, "ab"); _rW = NumOf(C, "w"); _rRx = NumOf(C, "rx"); _rRz = NumOf(C, "rz"); _rPad = OrOf(C, "pad", 0.25);
            int[] hx(object c) { var s = (string)c; return new[] { Convert.ToInt32(s.Substring(1, 2), 16), Convert.ToInt32(s.Substring(3, 2), 16), Convert.ToInt32(s.Substring(5, 2), 16) }; }
            _rcD = hx(C["dark"]); _rcB = hx(col["fill"]); _rcL = hx(C["light"]);
            var b = _g.A("body"); _rbY = b[1]; _rbW = b[2]; _rbH = b[3];
            _tR = NumOf(tag, "r");
            CrSh = El("cr.sh"); CrSegs = Els("cr.segs.{0}", 48); CrHl = El("cr.hl"); CrLo = El("cr.lo"); CrSt = El("cr.st");
            CrBk = El("cr.bk"); CrBki = El("cr.bki"); CrBkp = El("cr.bkp"); CrRg = El("cr.rg"); CrTg = El("cr.tg"); CrTi = El("cr.ti"); CrGl = El("cr.gl");
            CrMk = Els("cr.mk.{0}", 2);
            for (int i = 0; i < 48; i++) _segRgb[i] = -1;
        }

        // ---- helpers ----
        /// <summary>Math.hypot of three, as V8 computes it (RigGeometry.Hypot without its array).</summary>
        static double Hypot3(double a, double b, double c)
        {
            a = Math.Abs(a); b = Math.Abs(b); c = Math.Abs(c);
            if (double.IsInfinity(a) || double.IsInfinity(b) || double.IsInfinity(c)) return double.PositiveInfinity;
            if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(c)) return double.NaN;
            double max = Math.Max(a, Math.Max(b, c));
            if (max == 0) return 0;
            double sum = 0, comp = 0, n, sm, pre;
            n = a / max; sm = n * n - comp; pre = sum + sm; comp = (pre - sum) - sm; sum = pre;
            n = b / max; sm = n * n - comp; pre = sum + sm; comp = (pre - sum) - sm; sum = pre;
            n = c / max; sm = n * n - comp; pre = sum + sm; sum = pre;
            return Math.Sqrt(sum) * max;
        }

        /// <summary>String(v) for an r2'd number, from a table for 0..1 (no string made per frame).</summary>
        static string Hundredth(double v)
        {
            int k = (int)Math.Round(v * 100);
            return k >= 0 && k <= 100 && k / 100.0 == v ? Hundredths[k] : RigGeometry.Js(v);
        }
        static readonly string[] Hundredths = MakeHundredths();
        static string[] MakeHundredths() { var a = new string[101]; for (int k = 0; k <= 100; k++) a[k] = RigGeometry.Js(k / 100.0); return a; }

        /// <summary>Two paths the same as the game's strings would be (numbers are r2'd, so equal numbers print the same).</summary>
        static bool SameD(RigPath a, RigPath b)
        {
            if (a.Cmd.Count != b.Cmd.Count || a.V.Count != b.V.Count) return false;
            for (int i = 0; i < a.Cmd.Count; i++) if (a.Cmd[i] != b.Cmd[i]) return false;
            for (int i = 0; i < a.V.Count; i++) { double x = a.V[i], y = b.V[i]; if (!(x == y || (double.IsNaN(x) && double.IsNaN(y)))) return false; }
            return true;
        }

        /// <summary>if (el._d !== d) { el._d = d; set(el, 'd', d); } (d null: '')</summary>
        static void PutD(RigEl e, RigPath d, bool had)
        {
            if (had && (d == null ? e.D.Empty : SameD(e.D, d))) return;
            var o = Dp(e); if (d != null) o.CopyFrom(d);
        }

        /// <summary>list.sort((a, b) => a[0] - b[0]): stable, by key.</summary>
        static void SortBy(List<int> l, double[] key)
        {
            for (int i = 1; i < l.Count; i++)
            {
                int n = l[i]; double v = key[n]; int j = i - 1;
                while (j >= 0 && key[l[j]] - v > 0) { l[j + 1] = l[j]; j--; }
                l[j + 1] = n;
            }
        }

        /// <summary>'M' + the four corners joined by ' L' + 'Z', turned to run one way round (ar >= 0).</summary>
        void Quad(RigPath o)
        {
            double ar = 0;
            for (int k = 0; k < 4; k++) { int k1 = (k + 1) % 4; ar += _qx[k] * _qy[k1] - _qx[k1] * _qy[k]; }
            o.C('M');
            if (ar < 0) { o.P(_qx[3], _qy[3]); for (int k = 2; k >= 0; k--) { o.C('L'); o.P(_qx[k], _qy[k]); } }
            else { o.P(_qx[0], _qy[0]); for (int k = 1; k < 4; k++) { o.C('L'); o.P(_qx[k], _qy[k]); } }
            o.C('Z');
        }

        static void Tri(RigPath o, double ax, double ay, double bx, double by, double cx, double cy, bool z)
        {
            o.C('M'); o.P(ax, ay); o.C('L'); o.P(bx, by); o.C('L'); o.P(cx, cy); if (z) o.C('Z');
        }

        /// <summary>The coat's projection: a point on the body (byw) or the head (sph, z given), mixed by how much it belongs to the head.</summary>
        (double x, double y) PJ(double px, double py, double pz, double w, out double z)
        {
            var B = Byw(px, py, pz); double zb = BywZ(px, pz);
            if (w <= 0.002) { z = zb; return B; }
            var A = Sph(px, py, ZGiven, pz); double za = SphZ(px, py, ZGiven, pz);
            if (w >= 0.998) { z = za; return A; }
            z = zb + (za - zb) * w;
            return (B.x + (A.x - B.x) * w, B.y + (A.y - B.y) * w);
        }

        void Mane(double now)
        {
            // the keys render()'s tail block gives (after tail and collar): the mane behind, its front part over the head, the coat's body part
            if (_maneKind != 0) { Key("mane", -30); Key("maneF", 1.3); if (_maneKind == 1) Key("coatB", 0.6); }
            if (_pupKey) Key("pup", 12);
            if (_maneKind == 1) Coat(now);
            else if (_maneKind == 2) Hog(now);
            else if (_maneKind == 3) Spiky();
            else if (_maneKind == 4)
            {
                _mWide = Clamp(P["wide"], 0, 1);
                _mK = 1 + Clamp(P["wide"], 0, 1) * 0.1 + Math.Max(0, sq) * 0.05; _mRp = 0.025 + Clamp(P["wide"], 0, 1) * 0.05;
                RigGeometry.ToD(Dp(_mane), _maneSegs, _maneMap);
            }
        }

        (double x, double y) ManeMap(double x, double y)
        {
            double dx = x - _mHCx, dy = y - _mHCy, th = Math.Atan2(dy, dx), m = _mK * (1 + _mRp * _mWide * Math.Sin(th * 7));
            return _maneBody ? Glob(_mHCx + dx * m, _mHCy + dy * m) : HeadT(_mHCx + dx * m, _mHCy + dy * m);
        }

        // spine coat: one shell over the head and back; its spines lie along it and stand up when he bristles
        void Coat(double now)
        {
            double ed = _ied, ld = _ild;
            double br = Clamp(Math.Max(Math.Max(Or0(P["bristle"]), Clamp(P["wide"], 0, 1) * 0.85), SM((-P["pitch"] - 0.3) / 0.25) * SM((sq - 0.08) / 0.06)), 0, 1);
            for (int j = 0; j < 3; j++) _cLA[j] = _cLay[j] + (_cLayUp[j] - _cLay[j]) * br;
            double gl = 1 + br * 0.22;
            for (int r = 0; r < _mPx.Length; r++)
                for (int i = 0; i < _mPx[r].Length; i++) { var q = PJ(_mPx[r][i], _mPy[r][i], _mPz[r][i], _mW[r][i], out _mZ[r][i]); _mQx[r][i] = q.x; _mQy[r][i] = q.y; }
            var mB = Dp(_cMB); var mBF = Dp(_cMBF); var mHF = Dp(_cMHF);
            for (int r = 0; r < _mPx.Length - 1; r++)
            {
                int n = _mPx[r].Length;
                for (int i = 0; i < n; i++)
                {
                    int i2 = (i + 1) % n;
                    _qx[0] = _mQx[r][i]; _qy[0] = _mQy[r][i]; _qx[1] = _mQx[r][i2]; _qy[1] = _mQy[r][i2];
                    _qx[2] = _mQx[r + 1][i2]; _qy[2] = _mQy[r + 1][i2]; _qx[3] = _mQx[r + 1][i]; _qy[3] = _mQy[r + 1][i];
                    double z = (_mZ[r][i] + _mZ[r][i2] + _mZ[r + 1][i2] + _mZ[r + 1][i]) / 4, w = (_mW[r][i] + _mW[r][i2] + _mW[r + 1][i2] + _mW[r + 1][i]) / 4;
                    Quad(z > 0 ? (w > 0.5 ? mHF : mBF) : mB);
                }
            }
            _lsBack.Clear(); _lsBodyF.Clear(); _lsHeadF.Clear();
            var cS = PJ(50, 62, 0, 0.5, out _); double f = _cTipF;
            for (int si = 0; si < _cSp.Length; si++)
            {
                var s = _cSp[si];
                double ou = _cLA[0] + s.Rim * (1 - br);
                double dv0 = s.N[0] * ou + s.E[0] * _cLA[1] + 0, dv1 = s.N[1] * ou + s.E[1] * _cLA[1] + _cLA[2], dv2 = s.N[2] * ou + s.E[2] * _cLA[1] + 0;
                double dl = Or1(Hypot3(dv0, dv1, dv2)), ln = s.Len * gl / dl;
                var A = PJ(s.R[0], s.R[1], s.R[2], s.O, out double zr);
                var Tt = PJ(s.R[0] + dv0 * ln, s.R[1] + dv1 * ln, s.R[2] + dv2 * ln, s.O, out double zt);
                double k = s.Len / 10;
                double vx = Tt.x - A.x, vy = Tt.y - A.y, L = RigGeometry.Hypot(vx, vy);
                if (L < 1.2) { double ox = A.x - cS.x, oy = A.y - cS.y, ol = Or1(RigGeometry.Hypot(ox, oy)); vx = ox / ol * 1.2; vy = oy / ol * 1.2; L = 1.2; }
                double nx = -vy / L, ny = vx / L, w = s.W, wob = Math.Sin(now / 520 + s.I * 1.7) * 0.2 * k,
                    Tx = A.x + vx + ed * 0.3 * k + nx * wob, Ty = A.y + vy - ld * 0.25 * k + ny * wob;
                double B1x = A.x + nx * w, B1y = A.y + ny * w, B2x = A.x - nx * w, B2y = A.y - ny * w;
                s.D.Clear(); Tri(s.D, B1x, B1y, Tx, Ty, B2x, B2y, false);
                // banded quill: the outer part is cream
                s.T.Clear(); if (Truthy(f)) Tri(s.T, B1x + (Tx - B1x) * f, B1y + (Ty - B1y) * f, Tx, Ty, B2x + (Tx - B2x) * f, B2y + (Ty - B2y) * f, true);
                _cKey[si] = (zr + zt) / 2;
                (zr > -1.5 ? (s.O > 0.5 ? _lsHeadF : _lsBodyF) : _lsBack).Add(si);
            }
            PutCoat(_cBack, _lsBack); PutCoat(_cBodyF, _lsBodyF); PutCoat(_cHeadF, _lsHeadF);
        }

        void PutCoat(SpinePool pool, List<int> list)
        {
            SortBy(list, _cKey);
            for (int i = 0; i < pool.D.Length; i++)
            {
                var s = i < list.Count ? _cSp[list[i]] : null;
                PutD(pool.D[i], s?.D, pool.Had[i]); PutD(pool.T[i], s?.T, pool.Had[i]);
                pool.Had[i] = true;
            }
        }

        // hedgehog spines: a shell over his back, each point turning with a blend of the body's yaw and the head's yaw + nod
        void HogRot(double[] p, double w, double ph, double[] o)
        {
            if (!Truthy(w)) { o[0] = 50 + (p[0] - 50) * cyb + p[2] * syb; o[1] = p[1]; o[2] = -(p[0] - 50) * syb + p[2] * cyb; return; }
            double a = yb + (yh - yb) * w, c = Math.Cos(a), s = Math.Sin(a), x = 50 + (p[0] - 50) * c + p[2] * s, y = p[1], z = -(p[0] - 50) * s + p[2] * c;
            if (Truthy(ph)) { double pa = ph * w, cc = Math.Cos(pa), ss = Math.Sin(pa), dy = y - HY; y = HY + dy * cc - z * ss; z = dy * ss + z * cc; }
            o[0] = x; o[1] = y; o[2] = z;
        }

        double HogFac(double[] n, double w)
        {
            if (!Truthy(w)) return -n[0] * syb + n[2] * cyb;
            double a = yb + (yh - yb) * w;
            return -n[0] * Math.Sin(a) + n[2] * Math.Cos(a);
        }

        (double x, double y) HogW2(double X, double Y)
        {
            double w = SM((NY - Y) / 12);
            if (w <= 0) return Glob(X, Y);
            var a = HeadT(X, Y); if (w >= 1) return a;
            var b = Glob(X, Y);
            return (b.x + (a.x - b.x) * w, b.y + (a.y - b.y) * w);
        }

        double HogSurf(double X, double Y)
        {
            double ax = (X - HX) / SR, ay = (Y - HY) / SRY, bx = (X - _hBX0) / _hBW0, by = (Y - _hBY0) / _hBH0, a = 1 - ax * ax - ay * ay, b = 1 - bx * bx - by * by;
            return Math.Max(a > 0 ? SR * Math.Sqrt(a) : -1e9, b > 0 ? _hBW0 * 0.75 * Math.Sqrt(b) : -1e9);
        }

        void Hog(double now)
        {
            double ed = _ied, ld = _ild, grow = 1 + Clamp(P["wide"], 0, 1) * 0.2 + Math.Max(0, sq) * 0.15, ph = P["pitch"] * 22 * D2R;
            int na = _hNa, nb = _hSx.Length;
            for (int j = 0; j < nb; j++)
                for (int i = 0; i < _hG[j].Length; i++)
                {
                    HogRot(_hG[j][i], _hGW[j][i], ph, _q1); _hSx[j][i] = _q1[0]; _hSy[j][i] = _q1[1]; _hSz[j][i] = _q1[2];
                    var w = HogW2(_q1[0], _q1[1]); _hWx[j][i] = w.x; _hWy[j][i] = w.y;
                }
            // back shell: every quad goes behind the body; quads facing the camera and in front of head / body are drawn again on top
            var dB = Dp(_hDomeB); var dF = Dp(_hDomeF);
            for (int j = 0; j < nb - 1; j++)
                for (int i = 0; i < na; i++)
                {
                    int i2 = (i + 1) % na;
                    _qx[0] = _hWx[j][i]; _qy[0] = _hWy[j][i]; _qx[1] = _hWx[j][i2]; _qy[1] = _hWy[j][i2];
                    _qx[2] = _hWx[j + 1][i2]; _qy[2] = _hWy[j + 1][i2]; _qx[3] = _hWx[j + 1][i]; _qy[3] = _hWy[j + 1][i];
                    Quad(dB);
                    if (HogFac(_hQN[j][i], (_hGW[j][i] + _hGW[j + 1][(i + 1) % na]) / 2) > 0.02)
                    {
                        double mx = (_hSx[j][i] + _hSx[j + 1][i2]) / 2, my = (_hSy[j][i] + _hSy[j + 1][i2]) / 2, mz = (_hSz[j][i] + _hSz[j][i2] + _hSz[j + 1][i2] + _hSz[j + 1][i]) / 4;
                        if (mz > HogSurf(mx, my) - 1.5) Quad(dF);
                    }
                }
            // spikes: away-facing ones sit under the shell, the rim ring on it, camera-facing ones in front of the body
            _ls0.Clear(); _ls1.Clear(); _ls2.Clear();
            for (int si = 0; si < _hSp.Length; si++)
            {
                var s = _hSp[si];
                for (int k3 = 0; k3 < 3; k3++) { double bc = (s.B1[k3] + s.B2[k3]) / 2; _t3[k3] = bc + (s.T[k3] - bc) * grow; }
                HogRot(s.B1, s.Hw, ph, _q1); HogRot(s.B2, s.Hw, ph, _q2); HogRot(_t3, s.Hw, ph, _qt);
                double f = HogFac(s.N, s.Hw), k = s.Len / 10, wob = Math.Sin(now / 520 + s.I * 1.7) * 0.25 * k;
                var A1 = HogW2(_q1[0], _q1[1]); var A2 = HogW2(_q2[0], _q2[1]); var At = HogW2(_qt[0], _qt[1]);
                {   // seen edge-on a spike keeps some width (it is a cone, not a flat blade)
                    double mx = (A1.x + A2.x) / 2, my = (A1.y + A2.y) / 2, ax = At.x - mx, ay = At.y - my, al = Or1(RigGeometry.Hypot(ax, ay)), px = -ay / al, py = ax / al,
                        hx = (A2.x - A1.x) / 2, hy = (A2.y - A1.y) / 2, dp = hx * px + hy * py, need = s.W * 0.8 - Math.Abs(dp);
                    if (need > 0) { double sg = dp < 0 ? -1 : 1, nx = hx + px * need * sg, ny = hy + py * need * sg; A1 = (mx - nx, my - ny); A2 = (mx + nx, my + ny); }
                }
                double tx = A2.x - A1.x, ty = A2.y - A1.y, tl = Or1(RigGeometry.Hypot(tx, ty)), Tpx = At.x + ed * 0.35 * k + tx / tl * wob, Tpy = At.y - ld * 0.3 * k + ty / tl * wob;
                double vis = Math.Abs((Tpx - A1.x) * ty - (Tpy - A1.y) * tx) / (tl * Math.Max(1, s.Len * grow)), qc0 = (_q1[0] + _q2[0]) / 2, qc1 = (_q1[1] + _q2[1]) / 2, qc2 = (_q1[2] + _q2[2]) / 2;
                bool front = f > 0.1 && qc2 > HogSurf(qc0, qc1) - 1.5;
                s.D.Clear(); Tri(s.D, A1.x, A1.y, Tpx, Tpy, A2.x, A2.y, false); s.O = R2(SM(vis * 3));
                _hKey[si] = (qc2 + _qt[2]) / 2;
                (front ? _ls2 : f < -0.01 ? _ls0 : _ls1).Add(si);
            }
            PutHog(_hPre, _ls0); PutHog(_hPost, _ls1); PutHog(_hFs, _ls2);
        }

        void PutHog(SpinePool pool, List<int> list)
        {
            SortBy(list, _hKey);
            for (int i = 0; i < pool.D.Length; i++)
            {
                var s = i < list.Count ? _hSp[list[i]] : null; double o = s != null ? s.O : 1;
                PutD(pool.D[i], s?.D, pool.Had[i]);
                if (!pool.Had[i] || !(pool.O[i] == o)) { pool.O[i] = o; pool.D[i].Set("stroke-opacity", Hundredth(o)); }
                pool.Had[i] = true;
            }
        }

        // spiky mane: triangles round the head, turning with it; those facing you go over the head (maneF)
        void Spiky()
        {
            double grow = 1 + Clamp(P["wide"], 0, 1) * 0.12;
            int n = _kDx.Length;
            for (int i = 0; i < n; i++)
            {
                double vx = _kDx[i], vy = _kDy[i], vz = _kDz[i], x1 = vx * cyh + vz * syh, z0 = -vx * syh + vz * cyh, d0 = x1, d1 = vy * cp - z0 * sp, d2 = vy * sp + z0 * cp;
                vx = _kTx[i]; vy = _kTy[i]; vz = _kTz[i]; x1 = vx * cyh + vz * syh; z0 = -vx * syh + vz * cyh;
                double g0 = x1, g1 = vy * cp - z0 * sp;
                var p = _kP[i]; _kZ[i] = d2; _kOrd[i] = i;
                double rr = _kRb * 0.9, ww = _kW;
                var a = HeadT(_kCx + d0 * rr + g0 * ww, _kCy + d1 * rr + g1 * ww); p[0] = a.x; p[1] = a.y;
                rr = _kRt * grow; ww = 0; a = HeadT(_kCx + d0 * rr + g0 * ww, _kCy + d1 * rr + g1 * ww); p[2] = a.x; p[3] = a.y;
                rr = _kRb * 0.9; ww = -_kW; a = HeadT(_kCx + d0 * rr + g0 * ww, _kCy + d1 * rr + g1 * ww); p[4] = a.x; p[5] = a.y;
            }
            for (int i = 1; i < n; i++)   // tris.sort((a, b) => a[0] - b[0]), stable
            {
                int m = _kOrd[i]; double v = _kZ[m]; int j = i - 1;
                while (j >= 0 && _kZ[_kOrd[j]] - v > 0) { _kOrd[j + 1] = _kOrd[j]; j--; }
                _kOrd[j + 1] = m;
            }
            var back = Dp(_mane); var front = _maneF != null ? Dp(_maneF) : null;
            for (int i = 0; i < n; i++)
            {
                int m = _kOrd[i]; double z = _kZ[m]; var p = _kP[m];
                var o = z < 0.18 ? back : z >= 0.18 ? front : null;
                if (o != null) Tri(o, p[0], p[1], p[2], p[3], p[4], p[5], true);
            }
        }

        // the pup on mamapaws' belly: a little face that sways, blinks and dances along
        void Pup(double now)
        {
            if (!_hasPup) return;
            double t = now / 1000, pdt = Truthy(_pjT) ? Math.Min(0.05, (now - _pjT) / 1000) : 0.016; _pjT = now;
            _pj = Or0(_pj) + ((_cur.Contains("dance") ? 1 : 0) - Or0(_pj)) * Math.Min(1, pdt * 5); double pj = SM(_pj);
            double fb = _pcy + _pr - 1, td = Math.Sin(t * 1.3) * 6 * (1 - pj) + Math.Sin(t * Math.PI * 2) * 9 * pj + Clamp(_ied, -4, 4) * 2, tl = td * D2R,
                ca = Math.Cos(tl), sa = Math.Sin(tl), psq = Math.Sin(t * 2.6) * 0.03 * (1 - pj) - Clamp(Or0(P["sq"]), -0.3, 0.3) * 0.7 * pj + Clamp(_ild, -6, 6) * 0.008, bl = (now % 3400) < 150 ? 0.12 : 1;
            _ppj = pj; _pbl = bl; _psq = psq; _pfb = fb; _pca = ca; _psa = sa;
            for (int i = 0; i < _pup.Length; i++)
            {
                var h = _pup[i];
                double ea = h.Ear != null ? -h.Ear[2] * (5 + Math.Sin(t * 2.6 - 0.9) * 5 * (1 - pj) + Math.Abs(Math.Sin(t * Math.PI * 2 - 0.6)) * 16 * pj + Clamp(_ild, -6, 6) * 1.2) * D2R - td * 0.5 * D2R : 0;
                _pec = Math.Cos(ea); _pes = Math.Sin(ea); _pcs = h.Cheek != null ? 1 + pj * 0.18 : 1;
                if (h.Shine || h.MouthLine || h.Joy != null || h.Cheek != null) Op(_pupEl[i], R2(h.Joy != null ? pj : h.Cheek != null ? Math.Min(1, h.Op + pj * 0.4) : h.Op * (1 - pj)));
                _ph = h; RigGeometry.ToD(Dp(_pupEl[i]), h.Segs, _pupMap);
            }
            Op(_pupG, R2(SM((cyb + 0.15) / 0.35)));
        }

        (double x, double y) PupMap(double x, double y)
        {
            var h = _ph; double pj = _ppj, sq = _psq, fb = _pfb;
            if (h.Eye != null) { double dy = y - h.Eye[1]; y = h.Eye[1] - pj * 0.5 + dy * (dy > 0 ? _pbl * (1 - 1.75 * pj) : _pbl); }
            if (h.Joy != null) y = h.Joy[1] + (y - h.Joy[1]) * Math.Max(0.05, pj);
            if (h.Cheek != null) { x = h.Cheek[0] + (x - h.Cheek[0]) * _pcs; y = h.Cheek[1] + (y - h.Cheek[1]) * _pcs; }
            if (h.Ear != null) { double p0 = x - h.Ear[0], q0 = y - h.Ear[1]; x = h.Ear[0] + p0 * _pec - q0 * _pes; y = h.Ear[1] + p0 * _pes + q0 * _pec; }
            double u0 = x - _pcx, v0 = y - _pcy, zp = Math.Sqrt(Math.Max(0, 1 - (u0 * u0 + v0 * v0) / (_pr * _pr))) * _pr * 0.5;
            x = _pcx + u0 * (1 - sq * 0.5); y = fb + (y - fb) * (1 + sq);
            double p = x - _pcx, q = y - fb; x = _pcx + p * _pca - q * _psa; y = fb + p * _psa + q * _pca;
            double dx = x - _pBX, v = Clamp((y - _pBY) / _pBH, -1, 1), wc = _pBW * Math.Sqrt(1 - v * v * 0.36), zb = Math.Sqrt(Math.Max(0, wc * wc - dx * dx));
            return Glob(_pBX + dx * cyb + (zb + zp) * syb, y);
        }

        // ---- collars ----
        double RingTy(double t) { double c = Math.Cos(t); return _rYs + (c >= 0 ? _rAf : _rAb) * c * c; }
        /// <summary>The band's radius at height y: the head sphere's or the body's outline there (whichever is wider) + a hair.</summary>
        double RingR(double y)
        {
            double a = (y - HY) / SRY, b = (y - _rbY) / _rbH;
            return Math.Max(a * a < 1 ? SR * Math.Sqrt(1 - a * a) : 0, b * b < 1 ? _rbW * Math.Sqrt(1 - b * b) : 0) + _rPad;
        }
        (double x, double y) RingPt(double t, double dy) { double y = RingTy(t) + dy, r = RingR(y); return Byw(50 + r * Math.Sin(t), y, r * Math.Cos(t)); }
        double RingDep(double t) => -_rRx * Math.Sin(t) * syb + _rRz * Math.Cos(t) * cyb;

        /// <summary>The edge line along the visible run of slices, dy below the band's top ('' when none shows).</summary>
        void RingLine(RigEl e, int k0, double dy)
        {
            var o = Dp(e); int n = _rVis.Count; if (n == 0) return;
            for (int m = 0; m < n; m++) { o.C(m == 0 ? 'M' : 'L'); o.P(RingPt((double)_rVis[(k0 + m) % n] / 48 * TAU, dy)); }
            o.C('L'); o.P(RingPt((double)(_rVis[(k0 + n - 1) % n] + 1) / 48 * TAU, dy));
        }

        void RingBq(RigEl e, double a, double y0, double y1)
        {
            var o = Dp(e);
            o.C('M'); o.P(RingPt(Math.PI - a, y0)); o.C('L'); o.P(RingPt(Math.PI + a, y0)); o.C('L'); o.P(RingPt(Math.PI + a, y1)); o.C('L'); o.P(RingPt(Math.PI - a, y1)); o.C('Z');
        }

        static int Mix(int[] a, int[] b, double f) =>
            ((int)RigGeometry.JsRound(a[0] + (b[0] - a[0]) * f) << 16) | ((int)RigGeometry.JsRound(a[1] + (b[1] - a[1]) * f) << 8) | (int)RigGeometry.JsRound(a[2] + (b[2] - a[2]) * f);

        void Collars(double now)
        {
            if (_collarKind == 1) Ring(now);
            else if (_collarKind == 2)
            {
                double fa = (-_ied * 4 + Plean * 1.5 + P["htilt"] * 0.4 - _ild * 1.2 + Math.Sin(now / 520) * 2) * D2R;
                _colCa = Math.Cos(fa); _colSa = Math.Sin(fa); _cfl = 1 + Math.Sin(now / 430) * 0.05 + Clamp(_ild, -10, 10) * 0.012;
                RigGeometry.ToD(Dp(_collar), _collarSegs, _collarMap); Op(_collar, R2(Zc(_tX, _tY)));
                RigGeometry.EllS(Dp(_tag), _tX, _tY, _tR, _tR, 0, 8, _face); Op(_tag, R2(Zc(_tX, _tY)));
            }
            else if (_collarKind == 3)
            {
                double fa = (Math.Sin(now / 520) * 3 + Plean * 1.5 - _ild * 1.2) * D2R;
                _colCa = Math.Cos(fa); _colSa = Math.Sin(fa); _cfl = 1 + Math.Sin(now / 430) * 0.05; _ccz = _cZ;
                RigGeometry.ToD(Dp(_collar), _collarSegs, _collarMap); Op(_collar, R2(SM(BywZ(_tX, _cZ) / 4)));
                _ccz = _cZ + 0.5; RigGeometry.EllS(Dp(_tag), _tX, _tY, _tR, _tR, 0, 8, _tagBodyMap); Op(_tag, R2(SM(BywZ(_tX, _ccz) / 4)));
            }
            else if (_collarKind == 4)
            {
                RigGeometry.ToD(Dp(_collar), _collarSegs, _glob);
                double sw = (-_ied * 6 + Math.Sin(now / 360) * 4 + Plean * 2 - _ild * 1.5) * D2R, cz = 13, tx = _tX + Math.Sin(sw) * _tR * 1.2, ty = _tY + (Math.Cos(sw) - 1) * _tR,
                    X = 50 + (tx - 50) * cyb + cz * syb;
                RigGeometry.EllS(Dp(_tag), X, ty, _tR * (0.75 + 0.25 * Math.Abs(cyb)), _tR, 0, 8, _glob); Op(_tag, R2(SM((-(tx - 50) * syb + cz * cyb) / 4)));
            }
        }

        (double x, double y) CollarHeadMap(double x, double y)
        {
            double u = (x - _tX) * _cfl, v = y - _tY;
            return Face(_tX + u * _colCa - v * _colSa, _tY + u * _colSa + v * _colCa);
        }
        (double x, double y) CollarBodyMap(double x, double y)
        {
            double u = (x - _tX) * _cfl, v = y - _tY;
            return Byw(_tX + u * _colCa - v * _colSa, _tY + u * _colSa + v * _colCa, _ccz);
        }

        // 3D ring collar: 48 slices round the neck, the near ones drawn and shaded by the light; edge lines, a buckle at the back,
        // the tag on a pendulum under the chin. Masked to his outline (head + body).
        void Ring(double now)
        {
            Dp(CrMk[0]).CopyFrom(Body.D); Dp(CrMk[1]).CopyFrom(_skD);
            const int N = 48; double W = _rW;
            Key("collar", 1.5);
            _rVis.Clear();
            for (int i = 0; i < N; i++)
            {
                double t0 = (double)i / N * TAU, t1 = (double)(i + 1) / N * TAU, tm = (t0 + t1) / 2; var seg = CrSegs[i];
                if (RingDep(tm) <= 0) { Dp(seg); continue; }
                _rVis.Add(i);
                double nx = Math.Sin(tm) * cyb + Math.Cos(tm) * syb, nz = -Math.Sin(tm) * syb + Math.Cos(tm) * cyb, lam = Clamp((-0.42 * nx + 0.9 * nz) / 0.993, 0, 1);
                int col = lam < 0.6 ? Mix(_rcD, _rcB, lam / 0.6) : Mix(_rcB, _rcL, (lam - 0.6) / 0.4 * 0.45);
                if (col != _segRgb[i]) { _segRgb[i] = col; _segCs[i] = "rgb(" + (col >> 16) + "," + ((col >> 8) & 255) + "," + (col & 255) + ")"; }
                var o = Dp(seg);
                o.C('M'); o.P(RingPt(t0, 0)); o.C('L'); o.P(RingPt(t1, 0)); o.C('L'); o.P(RingPt(t1, W)); o.C('L'); o.P(RingPt(t0, W)); o.C('Z');
                seg.Set("fill", _segCs[i]); seg.Set("stroke", _segCs[i]);
            }
            // the visible run of slices may wrap past 0 (front view): start it after the gap
            int k0 = 0, nv = _rVis.Count;
            for (int j = 0; j < nv; j++) if (_rVis[(j + nv - 1) % nv] != (_rVis[j] + N - 1) % N) { k0 = j; break; }
            RingLine(CrSh, k0, W + 0.7); RingLine(CrHl, k0, 0.45); RingLine(CrLo, k0, W - 0.35); RingLine(CrSt, k0, W * 0.5);
            // buckle at the back of the neck (t = pi)
            {
                double db = RingDep(Math.PI), o = SM((db - 5) / 5), d1 = 2.4 / _rRz, d2 = 1.25 / _rRz;
                if (o > 0.01)
                {
                    RingBq(CrBk, d1, -0.9, W + 0.9); RingBq(CrBki, d2, 0.25, W - 0.25);
                    var p = Dp(CrBkp); p.C('M'); p.P(RingPt(Math.PI - d2, W / 2)); p.C('L'); p.P(RingPt(Math.PI + d1 * 0.15, W / 2));
                }
                else { Dp(CrBk); Dp(CrBki); Dp(CrBkp); }
                double ro = R2(o); Op(CrBk, ro); Op(CrBki, ro); Op(CrBkp, ro);
            }
            // tag: a pendulum from a ring under the chin, swung by how the ring point moves
            {
                var A = RingPt(0, W + 0.2); double o = SM((cyb + 0.06) / 0.16);
                if (!_tgHas) { _tgHas = true; _tgA = 0; _tgV = 0; _tgX = A.x; _tgY = A.y; _tgVx = 0; _tgVy = 0; _tgT = now; }
                double dt = Clamp((now - _tgT) / 1000, 0.001, 0.05);
                double vx = (A.x - _tgX) / dt, vy = (A.y - _tgY) / dt, ax = Clamp((vx - _tgVx) / dt, -4000, 4000), ay = Clamp((vy - _tgVy) / dt, -4000, 4000), Lh = _tR + 1.7, g = 700;
                _tgV += (-(g - ay) / Lh * Math.Sin(_tgA) - ax / Lh * Math.Cos(_tgA) - 3.2 * _tgV) * dt; _tgA = Clamp(_tgA + _tgV * dt, -0.95, 0.95);
                _tgX = A.x; _tgY = A.y; _tgVx = vx; _tgVy = vy; _tgT = now;
                double a = _tgA + Math.Sin(now / 900) * 0.035, rcx = A.x + Math.Sin(a) * 1.1, rcy = A.y + Math.Cos(a) * 1.1, tcx = A.x + Math.Sin(a) * Lh, tcy = A.y + Math.Cos(a) * Lh,
                    fx = 0.22 + 0.78 * Math.Abs(cyb);
                double ra = R2(-a / D2R), rx = R2(tcx), ry = R2(tcy);
                if (_rot == null || !(ra == _rotA && rx == _rotX && ry == _rotY)) { _rotA = ra; _rotX = rx; _rotY = ry; _rot = "rotate(" + RigGeometry.Js(ra) + " " + RigGeometry.Js(rx) + " " + RigGeometry.Js(ry) + ")"; }
                CrRg.Cx = R2(rcx); CrRg.Cy = R2(rcy); CrRg.Rx = R2(1.05 * fx); CrRg.Ry = 1.05;
                CrTg.Cx = R2(tcx); CrTg.Cy = R2(tcy); CrTg.Rx = R2(_tR * 1 * fx); CrTg.Ry = R2(_tR * 1); CrTg.Set("transform", _rot);
                CrTi.Cx = R2(tcx); CrTi.Cy = R2(tcy); CrTi.Rx = R2(_tR * 0.62 * fx); CrTi.Ry = R2(_tR * 0.62); CrTi.Set("transform", _rot);
                CrGl.Cx = R2(tcx - _tR * 0.38 * fx); CrGl.Cy = R2(tcy - _tR * 0.42); CrGl.Rx = R2(_tR * 0.3 * fx); CrGl.Ry = R2(_tR * 0.2); CrGl.Set("transform", _rot);
                const int ell = RigEl.ACx | RigEl.ACy | RigEl.ARx | RigEl.ARy;
                CrRg.Written |= ell; CrTg.Written |= ell; CrTi.Written |= ell; CrGl.Written |= ell;
                double ro = R2(o); Op(CrRg, ro); Op(CrTg, ro); Op(CrTi, ro); Op(CrGl, R2(o * 0.75));
            }
        }
    }
}
