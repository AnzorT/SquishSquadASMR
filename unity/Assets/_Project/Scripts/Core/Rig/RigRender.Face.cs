// render() of rive-rig.js, continued: the face's other parts (cheek tufts and eye domes, faceExtra, the 3D snout, headExtra, the crown). Ported line for line like the rest of RigRender (see RigRender.cs);
// FaceFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using System.Globalization;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] FaceFeatures = { "tufts", "eyeDome", "faceExtra", "snout", "headExtra", "crown" };

        RigMap _wrapF, _hugF;
        double _hY, _hw;   // hugF's turn this frame (render()'s hY, hw)
        // D.tufts: front copy, back copy, white (eye domes), each one's mask gradient (#tfg<i>) and its two stops; T.c, lift, r, white
        RigEl[] _tuftEl, _tuftB, _tuftW, _tuftG, _tuftS0, _tuftS1; List<RigSeg>[] _tuftD; double[][] _tuftC, _tuftWhite; double[] _tuftLift, _tuftR;
        RigMap[] _tuftMaps;   // I.tuftMap
        double[] _tcx, _tcy, _tfxs, _tC0x, _tC0y, _tc2, _ts2;   // each tuft's TM parameters this frame
        bool _eyeDome; string _tuftKy;
        double _tgHx, _tgHy, _tgNx, _tgNy; string _tgStr; double[] _tgO0, _tgO1; string[] _tgS0, _tgS1;   // the last strings written
        // D.faceExtra: outlines (RG.fx), the map each uses (0 face, 1 headT (gloss), 2 hugF (hug), 3 wrapF (big)), op, centre (RG.fxC)
        RigEl[] _fxEl; List<RigSeg>[] _fxD; int[] _fxMode; double[] _fxOp; double[][] _fxC;
        // D.snout: c, r, out, tilt, occlude, the jaw (r, drop, tilt, tiltOpen), the nose's r
        RigEl _snoutJ, _snout, _snoutN, _snoutH; double[] _snC, _snR, _snJR, _snNR; double _snOut, _snTilt, _jDrop, _jTilt, _jTiltOpen; bool _snOcclude;
        // D.headExtra, D.crown
        RigEl[] _hxEl, _crownEl; List<RigSeg>[] _hxD, _crownD; RigMap _crownMap;
        double _crbx, _crby, _crDy, _crZ, _crc, _crs;

        void InitFace()
        {
            double num(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? RigGeo.Num(v) : double.NaN;
            double orD(Dictionary<string, object> o, string k, double d) { double v = num(o, k); return v != 0 && !double.IsNaN(v) ? v : d; }
            bool has(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) && RigGeo.Truthy(v);
            List<RigSeg>[] segs(string k, int n)
            {
                var l = _g.RGRaw.TryGetValue(k, out var v) ? v as List<object> : null; var o = new List<RigSeg>[n];
                for (int i = 0; i < n; i++) o[i] = RigGeo.SegList(l[i] as List<object>);
                return o;
            }
            _wrapF = WrapF; _hugF = HugF;

            if (_g.Has("tufts"))
            {
                var tl = (List<object>)_g.Raw("tufts"); int n = tl.Count;
                _tuftEl = Els("tufts.{0}", n); _tuftB = Els("tuftsB.{0}", n); _tuftW = new RigEl[n];
                _tuftG = Els("#tfg{0}", n); _tuftS0 = Els("#tfg{0}/stop.0", n); _tuftS1 = Els("#tfg{0}/stop.1", n);
                _tuftD = segs("tufts", n); _tuftC = new double[n][]; _tuftWhite = new double[n][]; _tuftLift = new double[n]; _tuftR = new double[n];
                _tuftMaps = new RigMap[n]; _tcx = new double[n]; _tcy = new double[n]; _tfxs = new double[n]; _tC0x = new double[n]; _tC0y = new double[n]; _tc2 = new double[n]; _ts2 = new double[n];
                _tgO0 = new double[n]; _tgO1 = new double[n]; _tgS0 = new string[n]; _tgS1 = new string[n];
                for (int i = 0; i < n; i++)
                {
                    var T = (Dictionary<string, object>)tl[i];
                    _tuftC[i] = RigGeo.Arr(T["c"]); _tuftLift[i] = orD(T, "lift", 0); _tuftR[i] = orD(T, "r", 9.5);
                    if (has(T, "white")) { _tuftWhite[i] = RigGeo.Arr(T["white"]); _tuftW[i] = El("tuftsW." + i); }
                    int k = i; _tuftMaps[i] = (x, y) => TuftMap(k, x, y);
                }
                _tuftKy = ToFixed4(SRY / SR);
            }
            _eyeDome = _g.Has("eyeDome");

            if (_g.Raw("faceExtra") is List<object> fl && fl.Count > 0)
            {
                int n = fl.Count; var big = _g.RGRaw.TryGetValue("fxBig", out var fb) ? fb as List<object> : null; var fc = (List<object>)_g.RGRaw["fxC"];
                _fxEl = Els("fx.{0}", n); _fxD = segs("fx", n); _fxMode = new int[n]; _fxOp = new double[n]; _fxC = new double[n][];
                for (int i = 0; i < n; i++)
                {
                    var fe = (Dictionary<string, object>)fl[i]; bool bg = big != null && RigGeo.Truthy(big[i]);
                    _fxMode[i] = has(fe, "gloss") ? 1 : has(fe, "hug") ? 2 : bg && !has(fe, "sph") ? 3 : 0;
                    _fxOp[i] = orD(fe, "op", 1); _fxC[i] = RigGeo.Arr(fc[i]);
                }
            }

            if (_g.Has("snout"))
            {
                var S0 = (Dictionary<string, object>)_g.Raw("snout");
                _snC = RigGeo.Arr(S0["c"]); _snR = RigGeo.Arr(S0["r"]); _snOut = orD(S0, "out", 0); _snTilt = orD(S0, "tilt", 0); _snOcclude = has(S0, "occlude");
                if (has(S0, "jaw"))
                {
                    var J = (Dictionary<string, object>)S0["jaw"];
                    _snJR = RigGeo.Arr(J["r"]); _jDrop = orD(J, "drop", 2.2); _jTilt = orD(J, "tilt", 0); _jTiltOpen = orD(J, "tiltOpen", 0.5);
                    _snoutJ = El("snoutJ");
                }
                _snNR = has(S0, "nose") ? RigGeo.Arr(((Dictionary<string, object>)S0["nose"])["r"]) : new[] { 2.5, 2, 1.6 };
                _snout = El("snout"); _snoutN = El("snoutN"); _snoutH = El("snoutH");
            }

            if (_g.Raw("headExtra") is List<object> hl && hl.Count > 0) { _hxEl = Els("hx.{0}", hl.Count); _hxD = segs("hx", hl.Count); }
            if (_g.Raw("crown") is List<object> cl && cl.Count > 0)
            {
                _crownEl = Els("crown.{0}", cl.Count); _crownD = segs("crown", cl.Count); _crownMap = CrownMap;
                var cb = _g.A("crownBase"); _crbx = cb[0]; _crby = cb[1]; _crDy = _g.Or("crownDy", 0); _crZ = _g.Opt("crownZ") ?? -3;
            }
        }

        /// <summary>render()'s wrapF: on the head sphere near the middle, flattening to the plain head (headT) towards the rim.</summary>
        (double x, double y) WrapF(double x, double y)
        {
            double dx = x - HX, dy = y - HY, q = 1 - (dx / SR) * (dx / SR) - (dy / SRY) * (dy / SRY), w = SM((q - 0.04) / 0.32);
            if (w >= 1) return Sph(x, y);
            var a = Sph(x, y); var b = HeadT(x, y);
            return (b.x + (a.x - b.x) * w, b.y + (a.y - b.y) * w);
        }

        /// <summary>render()'s hugF: round the head, the turn spread over the front half so a mark keeps its width (wrapF past 45 deg).</summary>
        (double x, double y) HugF(double x, double y)
        {
            const double HP = Math.PI / 2;
            double dx = x - HX, dy = y - HY, z = Sz(dx, dy, ZSurf, 0), r = RigGeometry.Hypot(dx, z), th = Math.Atan2(dx, z),
                t2 = th < 0 ? -HP + (th + HP) * (HP + _hY) / HP : _hY + th * (HP - _hY) / HP, z1 = r * Math.Cos(t2);
            var b = HeadT(HX + r * Math.Sin(t2), HY + dy * cp - z1 * sp * KY);
            if (_hw >= 1) return b;
            var a = WrapF(x, y);
            return (a.x + (b.x - a.x) * _hw, a.y + (b.y - a.y) * _hw);
        }

        /// <summary>D.tufts (after the muzzle).</summary>
        void Tufts()
        {
            // fluffy rosettes past the sides of the face (or Ribbit's eye domes): a copy behind the head always, the one in front
            // hidden from the inside out by a radial mask round the head centre that grows as the tuft rolls round to the far side
            if (_tuftEl == null) return;
            for (int i = 0; i < _tuftEl.Length; i++)
            {
                double cx = _tuftC[i][0], cy = _tuftC[i][1], dx = cx - HX, dy = cy - HY, q = 1 - (dx / SR) * (dx / SR) - (dy / SRY) * (dy / SRY), z0 = SR * Math.Sqrt(Math.Max(0.02, q)),
                    th0 = Math.Atan2(dx, z0), thN = th0 * 0.55, th = th0 + yh,
                    fxs = (0.64 + 0.36 * Clamp(Math.Cos(thN + yh), 0, 1)) / (0.64 + 0.36 * Math.Cos(thN)), lf = 1 + _tuftLift[i] / SR;
                var C0 = lf > 1 ? Sph(HX + dx * lf, HY + dy * lf, ZGiven, z0 * lf) : Sph(cx, cy);
                double hr2 = P["htilt"] * D2R * 0.6, c2 = Math.Cos(hr2), s2 = Math.Sin(hr2), ct = Math.Cos(th);
                _tcx[i] = cx; _tcy[i] = cy; _tfxs[i] = fxs; _tC0x[i] = C0.x; _tC0y[i] = C0.y; _tc2[i] = c2; _ts2[i] = s2;
                var TM = _tuftMaps[i];
                var td = Dp(_tuftEl[i]); RigGeometry.ToD(td, _tuftD[i], TM); Dp(_tuftB[i]).CopyFrom(td);
                if (_tuftW[i] != null) { var Wt = _tuftWhite[i]; RigGeometry.EllS(Dp(_tuftW[i]), cx + Wt[0], cy + Wt[1], Wt[2], Wt[2], 0, 10, TM); }
                var Hc = HeadT(HX, HY);
                double dC = RigGeometry.Hypot(C0.x - Hc.x, C0.y - Hc.y), tr = _tuftR[i], f = SM((0.26 - ct) / 0.52), rho = (dC - tr) + f * 2 * tr, Rg = 60;
                var g = _tuftG[i];
                double hx = R2(Hc.x), hy = R2(Hc.y), nhx = R2(-Hc.x), nhy = R2(-Hc.y);
                if (_tgStr == null || hx != _tgHx || hy != _tgHy || nhx != _tgNx || nhy != _tgNy)
                {
                    _tgHx = hx; _tgHy = hy; _tgNx = nhx; _tgNy = nhy;
                    _tgStr = "translate(" + RigGeometry.Js(hx) + " " + RigGeometry.Js(hy) + ") scale(1 " + _tuftKy + ") translate(" + RigGeometry.Js(nhx) + " " + RigGeometry.Js(nhy) + ")";
                }
                g.Cx = hx; g.Cy = hy; g.Written |= RigEl.ACx | RigEl.ACy; g.Set("gradientTransform", _tgStr);
                double o1 = f > 0.001 ? Clamp(rho / Rg, 0, 1) : 0, o2 = f > 0.001 ? Clamp((rho + 1.8) / Rg, 0, 1) : 0;
                if (_tgS0[i] == null || o1 != _tgO0[i]) { _tgO0[i] = o1; _tgS0[i] = ToFixed4(o1); }
                if (_tgS1[i] == null || o2 != _tgO1[i]) { _tgO1[i] = o2; _tgS1[i] = ToFixed4(o2); }
                _tuftS0[i].Set("offset", _tgS0[i]); _tuftS1[i].Set("offset", _tgS1[i]);
                Op(_tuftEl[i], 1); Op(_tuftB[i], R2(SM((ct + 0.95) / 0.25)));
            }
        }

        /// <summary>Tuft k's TM: its outline turned with the head, narrowed as it turns away.</summary>
        (double x, double y) TuftMap(int k, double x, double y)
        {
            double lx = (x - _tcx[k]) * _tfxs[k], ly = y - _tcy[k];
            return (_tC0x[k] + lx * _tc2[k] - ly * _ts2[k], _tC0y[k] + lx * _ts2[k] + ly * _tc2[k]);
        }

        /// <summary>D.faceExtra (E.fx).</summary>
        void FaceExtra()
        {
            if (_fxEl == null) return;
            double yw = ((yh / D2R) % 360 + 540) % 360 - 180;
            _hY = Clamp(yw, -85, 85) * D2R; _hw = 1 - SM((Math.Abs(yw) - 45) / 40);
            for (int i = 0; i < _fxEl.Length; i++)
            {
                int m = _fxMode[i];
                RigGeometry.ToD(Dp(_fxEl[i]), _fxD[i], m == 1 ? _headT : m == 2 ? _hugF : m == 3 ? _wrapF : _face);
                Op(_fxEl[i], R2(_fxOp[i] * (m == 1 ? 1 : m == 2 ? Zc(HX, _fxC[i][1]) : m == 3 ? 1 : Zc(_fxC[i][0], _fxC[i][1]))));
            }
        }

        /// <summary>render()'s dep: how far a point relative to the head centre faces you.</summary>
        double HeadDep(double x, double y, double z)
        {
            double z1 = -x * syh + z * cyh;
            return y * sp / KY + z1 * cp;
        }

        /// <summary>D.snout (before the nose; sets SnoutBehind, W["nose"]).</summary>
        void Snout()
        {
            // an ellipsoid set into the face, pushed out, its long axis straight ahead; a nose ellipsoid on its tip, a jaw under it
            if (_snout == null) return;
            double la = _snR[0], lb = _snR[1], lc = _snR[2], dx = _snC[0] - HX, dy = _snC[1] - HY, q = 1 - (dx / SR) * (dx / SR) - (dy / SRY) * (dy / SRY), z0 = (q > 0 ? SR * Math.Sqrt(q) : 0) + _snOut;
            var C = HeadPv(dx, dy, z0); double ux = _snTilt;
            var A0 = HeadPv(la, 0, 0); var A1 = HeadPv(0, lb, 0); var A2 = HeadPv(0, lb * ux, lc);
            double sv = SM((HeadDep(dx, dy, z0) + 5) / 4.5), svv = sv;
            // occlude: no fading; once the root turns to the far side it is drawn behind the skull at full strength
            if (_snOcclude) { bool bk = HeadDep(dx, dy, z0) < 0; svv = 1; SnoutBehind = bk; }
            RigGeometry.EllAx(Dp(_snout), HX + C.x, HY + C.y, A0.x, A0.y, A1.x, A1.y, A2.x, A2.y, _headT); Op(_snout, R2(svv));
            if (_snoutJ != null)
            {
                double ja = _snJR[0], jb = _snJR[1], jc = _snJR[2], op = Clamp(P["open"], 0, 1), jt = _jTilt + op * _jTiltOpen;
                var JC = HeadPv(dx, dy + lb * 0.62 + op * _jDrop, z0 - lc * 0.08 - op * 0.6);
                var J0 = HeadPv(ja, 0, 0); var J1 = HeadPv(0, jb, 0); var J2 = HeadPv(0, jc * jt, jc);
                RigGeometry.EllAx(Dp(_snoutJ), HX + JC.x, HY + JC.y, J0.x, J0.y, J1.x, J1.y, J2.x, J2.y, _headT); Op(_snoutJ, R2(svv));
            }
            double nz = z0 + lc * 0.9, ny = dy + lb * ux * 0.9 - lb * 0.12, na = _snNR[0], nb = _snNR[1], nc = _snNR[2];
            var NC = HeadPv(dx, ny, nz);
            var N0 = HeadPv(na, 0, 0); var N1 = HeadPv(0, nb, 0); var N2 = HeadPv(0, 0, nc);
            RigGeometry.EllAx(Dp(_snoutN), HX + NC.x, HY + NC.y, N0.x, N0.y, N1.x, N1.y, N2.x, N2.y, _headT); Op(_snoutN, R2(svv));
            var HC = HeadPv(dx - na * 0.35, ny - nb * 0.4, nz + nc * 0.6);
            RigGeometry.EllS(Dp(_snoutH), HX + HC.x, HY + HC.y, na * 0.32, nb * 0.26, 0, 6, _headT);
            Op(_snoutH, R2(_snOcclude ? (SnoutBehind ? 0 : 0.75 * SM((HeadDep(dx, dy, z0) + 2) / 4)) : sv * 0.75));
            W["nose"] = HeadT(HX + NC.x, HY + NC.y);
        }

        /// <summary>The map eye i is painted with (D.eyeDome: its tuft's).</summary>
        RigMap EyeFace(int i) => _eyeDome && _tuftMaps != null && i < _tuftMaps.Length ? _tuftMaps[i] : _face;

        /// <summary>D.headExtra (E.hx).</summary>
        void HeadExtra()
        {
            if (_hxEl == null) return;
            for (int i = 0; i < _hxEl.Length; i++) RigGeometry.ToD(Dp(_hxEl[i]), _hxD[i], _wrapF);
        }

        /// <summary>D.crown.</summary>
        void Crown()
        {
            if (_crownEl == null) return;
            double ang = ((P["earR"] - P["earL"]) * 0.25 - _ied * 2.5 + P["htilt"] * 0.3) * D2R;
            _crc = Math.Cos(ang); _crs = Math.Sin(ang);
            for (int i = 0; i < _crownEl.Length; i++) RigGeometry.ToD(Dp(_crownEl[i]), _crownD[i], _crownMap);
        }

        (double x, double y) CrownMap(double x, double y)
        {
            double vx = x - _crbx, vy = y - _crby;
            return Sph(_crbx + vx * _crc - vy * _crs, _crby + vx * _crs + vy * _crc + Math.Min(0, sq) * 2 + _crDy, ZGiven, _crZ);
        }

        /// <summary>JS x.toFixed(4), exactly: the nearest multiple of 0.0001 to x's exact value, halves up (for |x| under 1e9).</summary>
        static string ToFixed4(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            bool neg = x < 0; double a = neg ? -x : x; ulong n;
            if (a >= 1e9) n = (ulong)Math.Floor(a * 10000 + 0.5);
            else
            {
                // a = m * 2^e, so a * 20000 = m * 625 * 2^(e + 5) (under 2^63); n = floor((a * 20000 + 1) / 2)
                long bits = BitConverter.DoubleToInt64Bits(a); int be = (int)((bits >> 52) & 0x7FF); ulong m = (ulong)(bits & 0xFFFFFFFFFFFFFL);
                int e = be == 0 ? -1074 : be - 1075; if (be != 0) m |= 1UL << 52;
                int k = -(e + 5);   // at least 17 here
                n = k >= 63 ? 0 : (m * 625 + (1UL << k)) >> (k + 1);
            }
            return (neg ? "-" : "") + (n / 10000).ToString(CultureInfo.InvariantCulture) + "." + (n % 10000).ToString("D4", CultureInfo.InvariantCulture);
        }
    }
}
