// render() of rive-rig.js, continued: feet, ears and arms of other shapes (webbed feet, a crest, lobe ears, wings). Ported line for line like the rest of RigRender (see RigRender.cs);
// LimbFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] LimbFeatures = { "web", "crest", "ear:lobe", "wing" };

        // D.web: the outline and toes (RG.web, RG.webT), web.k / out, the leg (web.leg: top, w)
        List<RigSeg> _webD, _webT; RigEl[] _webLeg; RigMap _webMap;
        double _webK, _webOut, _legTop, _legW;
        double _wbx0, _wco, _wso, _wz, _wcy, _wd0;   // mp's parameters (this foot)
        // D.crest: each feather's outline (RG.crest), k, root, turn
        RigEl[] _crestEl; List<RigSeg>[] _crestD; double[] _crestK, _crestU0, _crestV0, _crestTurn; RigMap _crestMap;
        double _cca, _csa, _cu0, _cv0, _cst, _cct;
        // lobe ears: each one's mask paths (the skull, the body: E.ears[i].mk) and its last transform
        RigEl[][] _lobeMk; double[] _lobeTx; string[] _lobeTs;
        // D.wing: the outline (RG.wing), the feather lines (RG.wingF), out / rest / bulge / root, the body3 depth
        List<RigSeg> _wingD, _wingFD; RigEl[] _wingF; RigMap _wingMap;
        double _wgOut, _wgRest, _wgBulge, _wgRo, _wgRy0, _wgRz0, _wgBz0;
        double _wgsd, _wgco, _wgso, _wgcw, _wgsw, _wgcl, _wgsl, _wgxr;   // this wing's rot / W3 / P3 parameters

        void InitLimbs()
        {
            double num(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? RigGeo.Num(v) : double.NaN;
            double orD(Dictionary<string, object> o, string k, double d) { double v = num(o, k); return v != 0 && !double.IsNaN(v) ? v : d; }
            List<RigSeg> segs(string k) => _g.RGRaw.TryGetValue(k, out var v) ? RigGeo.SegList(v as List<object>) : null;
            List<object> list(string k) => _g.RGRaw.TryGetValue(k, out var v) ? v as List<object> : null;

            if (_g.Has("web"))
            {
                var W = (Dictionary<string, object>)_g.Raw("web");
                _webD = segs("web"); _webT = segs("webT"); _webK = orD(W, "k", 0.32); _webOut = orD(W, "out", 0); _webMap = WebMap;
                if (W.TryGetValue("leg", out var lg) && RigGeo.Truthy(lg))
                {
                    var L = (Dictionary<string, object>)lg; _legTop = num(L, "top"); _legW = orD(L, "w", 3);
                    _webLeg = Els("feet.{0}.leg", 2);
                }
            }
            if (_g.Has("crest"))
            {
                var cl = (List<object>)_g.Raw("crest"); var rg = list("crest"); int n = cl.Count;
                _crestEl = Els("crest.{0}", n); _crestD = new List<RigSeg>[n];
                _crestK = new double[n]; _crestU0 = new double[n]; _crestV0 = new double[n]; _crestTurn = new double[n];
                for (int i = 0; i < n; i++)
                {
                    var C = (Dictionary<string, object>)cl[i]; var root = RigGeo.Arr(C["root"]);
                    _crestK[i] = C.TryGetValue("k", out var k) && k != null ? RigGeo.Num(k) : 1;
                    _crestU0[i] = root[0]; _crestV0[i] = root[1]; _crestTurn[i] = orD(C, "turn", 0);
                    _crestD[i] = RigGeo.SegList(rg[i] as List<object>);
                }
                _crestMap = CrestMap;
            }
            for (int i = 0; i < _g.Ears.Length; i++)
            {
                if (_g.Ears[i].Lobe == null) continue;
                if (_lobeMk == null) { _lobeMk = new RigEl[_g.Ears.Length][]; _lobeTx = new double[_g.Ears.Length]; _lobeTs = new string[_g.Ears.Length]; }
                _lobeMk[i] = new[] { El("ears." + i + ".mk.0"), El("ears." + i + ".mk.1") };
            }
            if (_g.Has("wing"))
            {
                var WG = (Dictionary<string, object>)_g.Raw("wing"); var root = RigGeo.Arr(WG["root"]);
                _wingD = segs("wing"); _wingFD = segs("wingF");
                if (WG.TryGetValue("f", out var f) && RigGeo.Truthy(f)) _wingF = Els("wingF.{0}", 2);
                _wgOut = orD(WG, "out", 0); _wgRest = orD(WG, "rest", 0); _wgBulge = orD(WG, "bulge", 0);
                _wgRo = root[0]; _wgRy0 = root[1]; _wgRz0 = root[2];
                _wgBz0 = _g.Has("body3") ? RigGeo.Arr(((Dictionary<string, object>)_g.Raw("body3"))["c"])[2] : 0;
                _wingMap = WingP3;
            }
        }

        /// <summary>D.web: foot i (f.x, f.y; st, cy, z, X as render() computes them); sets its key and I.W.</summary>
        void FootWeb(int i, double fx, double fy, double st, double cy, double z, double X)
        {
            // flat webbed feet (a duck's): the top-view outline lies on the ground, toes forward and a little out, seen from a little above
            double ao = _webOut * D2R * (fx < 50 ? -1 : 1), bx0 = 50 + (fx - 50) * (1 + st * 0.3), d0 = -(bx0 - 50) * syb + z * cyb;
            _wco = Math.Cos(ao); _wso = Math.Sin(ao); _wbx0 = bx0; _wz = z; _wcy = cy; _wd0 = d0;
            var fd = Dp(FeetF[i]); RigGeometry.ToD(fd, _webD, _webMap); Dp(FeetClip[i]).CopyFrom(fd);
            var pd = Dp(FeetP[i]); if (_webT != null) RigGeometry.ToD(pd, _webT, _webMap);
            if (_webLeg != null)
            {
                var h0 = WebMap(0, 0.6); var top = Glob(X, Math.Min(cy - 1, _legTop + (cy - fy) * 0.4));
                var ld = Dp(_webLeg[i]); ld.C('M'); ld.P(h0); ld.C('L'); ld.P(top);
                Sw(_webLeg[i], R2(_legW * Math.Sqrt(sy)));
            }
            Key(i == 1 ? "footR" : "footL", d0 + (footK ?? -10)); W[i == 1 ? "footR" : "footL"] = Glob(X, cy);
        }

        (double x, double y) WebMap(double u, double v)
        {
            double wx = _wbx0 + u * _wco + v * _wso, wz = _wz - u * _wso + v * _wco;
            return Glob(50 + (wx - 50) * cyb + wz * syb, _wcy + _webK * ((-(wx - 50) * syb + wz * cyb) - _wd0));
        }

        /// <summary>D.crest (before the ears).</summary>
        void Crest()
        {
            // feathers standing up from the top of the head in its middle plane, swung out by turn, raised by the ear channels
            if (_crestEl == null) return;
            for (int i = 0; i < _crestEl.Length; i++)
            {
                double a = (Or0(P["earL"]) + Or0(P["earR"])) / 2 * _crestK[i] * D2R, tu = _crestTurn[i] * D2R;
                _cca = Math.Cos(a); _csa = Math.Sin(a); _cu0 = _crestU0[i]; _cv0 = _crestV0[i]; _cst = Math.Sin(tu); _cct = Math.Cos(tu);
                RigGeometry.ToD(Dp(_crestEl[i]), _crestD[i], _crestMap);
            }
        }

        (double x, double y) CrestMap(double u, double v)
        {
            double du = u - _cu0, dv = v - _cv0, U = _cu0 + du * _cca - dv * _csa, V = _cv0 + du * _csa + dv * _cca;
            return Sph(HX + U * _cst, V, ZGiven, -U * _cct);
        }

        /// <summary>render()'s pv: a point relative to the head centre, turned and nodded with the head.</summary>
        (double x, double y) HeadPv(double x, double y, double z)
        {
            double z1 = -x * syh + z * cyh;
            return (x * cyh + z * syh, y * cp - z1 * sp * KY);
        }

        /// <summary>Lobe ear i (e.lobe), bent by a; its masks use the skull (skD) and the body.</summary>
        void EarLobe(int i, RigEar e, double a, RigPath skD)
        {
            // a rounded ellipsoid hanging beside the head; the head / body shapes that hide it slide away as its side turns to you
            var L = e.Lobe; double px = e.Pivot[0], py = e.Pivot[1];
            double lx = L[0], ly = L[1], la = L[2], lb = L[3], lc = L[4], lz = L.Length > 5 ? L[5] : double.NaN, lr = L.Length > 6 ? L[6] : double.NaN;
            double ca = Math.Cos(a), sa = Math.Sin(a), vx = lx - px, vy = ly - py, cx = px + vx * ca - vy * sa, cy = py + vx * sa + vy * ca, rr = Or0(lr) * D2R + a, cr = Math.Cos(rr), sr = Math.Sin(rr);
            var C = HeadPv(cx - HX, cy - HY, Or0(lz));
            var A0 = HeadPv(la * cr, la * sr, 0); var A1 = HeadPv(-lb * sr, lb * cr, 0); var A2 = HeadPv(0, 0, lc);
            var ld = Dp(EarO[i]); RigGeometry.EllAx(ld, HX + C.x, HY + C.y, A0.x, A0.y, A1.x, A1.y, A2.x, A2.y, _headT); Dp(EarClip[i]).CopyFrom(ld); Op(EarN[i], 0);
            double bk = 1 - SM((-e.Side * syh - 0.2) / 0.35), tx = R2(-e.Side * (1 - bk) * 2 * (la + lb));
            if (_lobeTs[i] == null || tx != _lobeTx[i]) { _lobeTx[i] = tx; _lobeTs[i] = "translate(" + RigGeometry.Js(tx) + " 0)"; }
            var mk = _lobeMk[i];
            Dp(mk[0]).CopyFrom(skD); mk[0].Set("transform", _lobeTs[i]);
            Dp(mk[1]).CopyFrom(Body.D); mk[1].Set("transform", _lobeTs[i]);
            string kn = i > 0 ? "earR" : "earL";
            Key(kn, 2.5); W[kn] = HeadT(HX + C.x, HY + C.y + lb * 0.8);
        }

        /// <summary>D.wing: both arms as wings (instead of the paw loop).</summary>
        void Wings()
        {
            // flat feathered shapes hinged at the shoulder: folded along the side at rest, opened then lifted by the arm channel
            for (int i = 0; i < _g.Arms.Length && i < 2; i++)
            {
                double sd = _g.Arms[i].Side; bool Lf = sd < 0;
                double av = Clamp((Lf ? P["armL"] : P["armR"]) - (Lf ? P["hLy"] : P["hRy"]) * 2, -40, 170), o = _wgOut * D2R;
                _wgco = Math.Cos(o); _wgso = Math.Sin(o);
                double om = (_wgRest + (90 - _wgRest) * SM(av / 60)) * D2R, lm = (av > 30 ? (av - 30) * 0.9 : av < 0 ? av * 0.4 : 0) * D2R;
                _wgcw = Math.Cos(om); _wgsw = Math.Sin(om); _wgcl = Math.Cos(lm); _wgsl = Math.Sin(lm);
                _wgsd = sd; _wgxr = 50 + sd * _wgRo + (Lf ? P["hLx"] : -P["hRx"]) * 0.3;
                var Nv = WingRot(sd * _wgco, -_wgso, 0); double nd = -Nv.x * sya + Nv.z * cya;
                var C = WingW3(10, 3, 0); double dC = -(C.x - 50) * sya + C.z * cya;
                RigGeometry.ToD(Dp(Arms[i]), _wingD, _wingMap);
                if (_wingF != null) { RigGeometry.ToD(Dp(_wingF[i]), _wingFD, _wingMap); Op(_wingF[i], R2(0.8 * SM(Math.Abs(nd) * 3 - 0.3))); }
                Op(ArmT[i], 0); Key(i > 0 ? "armR" : "armL", dC - _wgBz0 * cya); W[Lf ? "pawL" : "pawR"] = WingP3(19, 4);
            }
        }

        (double x, double y, double z) WingRot(double x0, double y0, double z0)
        {
            double sd = _wgsd, x1 = x0 * _wgcw - sd * z0 * _wgsw, z1 = sd * x0 * _wgsw + z0 * _wgcw;
            return (x1 * _wgcl + sd * y0 * _wgsl, -sd * x1 * _wgsl + y0 * _wgcl, z1);
        }

        (double x, double y, double z) WingW3(double A, double B, double bu)
        {
            var (x, y, z) = WingRot(_wgsd * (B * _wgso + bu * _wgco), B * _wgco - bu * _wgso, -A);
            return (_wgxr + x, _wgRy0 + y, _wgRz0 + z);
        }

        (double x, double y) WingP3(double A, double B)
        {
            double ta = (A - 10) / 12, tb = (B - 3) / 7;
            var (x, y, z) = WingW3(A, B, _wgBulge * Math.Max(0, 1 - ta * ta - tb * tb));
            return Glob(50 + (x - 50) * cya + z * sya, y);
        }
    }
}
