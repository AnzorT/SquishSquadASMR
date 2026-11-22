// render() of rive-generic.js, the shared parts: the art's shapes by role (head, shine, cheeks, feet), the paws and the
// tubes that join them to the body, the rig's face (eyes, mouth), where things are (I.W) and the shadow.
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigGeneric
    {
        RigPath _headD;              // this frame's headD (null: '')
        double _wcx, _wcy, _wk;      // the wsurf map's centre and scale

        (double x, double y) WsurfMap(double x, double y) => Sph(_wcx + (x - _wcx) * _wk, _wcy + (y - _wcy) * _wk);

        void Items(double ied)
        {
            _headD = null;
            foreach (var q in _items)
            {
                var e = q.E;
                switch (q.Role)
                {
                    case "head":
                    {
                        var d = Dp(e);
                        RigGeometry.ToD(d, q.Segs, Truthy(pf) ? _puff : NS < 1 ? _nrwHead : _headT);
                        _headD = d; break;
                    }
                    case "shine":
                    case "wsurf":
                    {
                        double k = 1; _wcx = q.Cx; _wcy = q.Cy; _wk = k;
                        RigGeometry.ToD(Dp(e), q.Segs, _wsurf);
                        Op(e, R2((q.OpT ? q.OpN : 1) * Zc(_wcx, _wcy) * 1)); break;
                    }
                    case "cheek":
                    {
                        double k = 1 + Math.Max(0, pf) * 0.35;
                        RigGeometry.EllS(Dp(e), q.Ox + q.Side * Math.Max(0, pf) * 1.2, q.Oy - Clamp(P["smile"], 0, 1) * 0.6, q.Rx * k, q.Ry * k, 0, 8, _face);
                        Op(e, R2(Clamp((q.OpT ? q.OpN : 0.5) + Clamp(P["blush"], 0, 1) * 0.45 + Math.Max(0, pf) * 0.25 + Clamp(P["red"], 0, 1) * 0.3, 0, 1) * Zc(q.Ox, q.Oy))); break;
                    }
                    case "foot":
                    {
                        bool L = q.Ox < 50; double st = Clamp(Or0(P["sit"]), 0, 1), cy0 = q.Oy - (L ? P["footL"] : P["footR"]) + st * 4.6, z = 6 + st * 7,
                            X = 50 + (q.Ox - 50) * (1 + st * 0.3) * cyb + z * syb, ry = q.Ry * (1 + st * 0.75);
                        RigGeometry.EllAx(Dp(e), X, cy0, q.Rx * (1 + st * 0.08) * cyb, 0, (L ? 1 : -1) * st * ry * 0.28 * cyb, ry, q.Rx * 0.9 * syb, 0, _glob);
                        break;
                    }
                }
            }
            if (_headD != null) headClip?.D.CopyFrom(_headD);
            if (_headD != null && headClip != null) headClip.Written |= RigEl.AD;

            // paws: reach anywhere (offsets remapped from Mittens' proportions), stay round, linked to the shoulder by a soft tube
            double ed = Or0(ied);
            foreach (var a in _arms) { if (armStr) MitArm(a, ed); else { Missing = "paws without I.armStr"; } }
            for (int i = 0; i < 2; i++)
            {
                string k = i == 0 ? "pawL" : "pawR"; if (W.ContainsKey(k)) continue;
                double s = i == 0 ? -1 : 1; var m = MapHand(i == 0 ? P["hLx"] : -P["hRx"], i == 0 ? P["hLy"] : P["hRy"]);
                W[k] = Glob(50 + s * Math.Abs(50 - pawX) + (i == 0 ? m.x : -m.x), pawY + m.y);
            }
        }

        void MitArm(Item a, double ed)
        {
            bool Lf = a.Side < 0; double av = Lf ? P["armL"] : P["armR"], wr = SM(Math.Abs(av) / 45), ang = ((Lf ? P["armL"] : -P["armR"]) + ((Lf ? 20 : -20) - Or0(a.Rot)) * wr) * D2R,
                c = Math.Cos(ang), s = Math.Sin(ang), px = a.Hx, py = a.Hy;
            var m = MapHand(Lf ? P["hLx"] : -P["hRx"], Lf ? P["hLy"] : P["hRy"]); double ox = m.x, oy = m.y, hz = Lf ? P["hLz"] : P["hRz"];
            double vx = a.Ox - px, vy = a.Oy - py, rk = 1 + armReach * SM((av - 60) / 80);
            double cx = px + (vx * c - vy * s) * rk + (Lf ? ox : -ox) + ed * 0.35 + a.Side * (!double.IsNaN(armOut) ? armOut : 7) * wr, cy = py + (vx * s + vy * c) * rk + oy;
            double z = 9 + hz + Math.Max(0, -oy) * 0.25;
            if (mReach) Missing = "I.mReach (paws to the mouth)";
            if (avoidHead) { double dx = cx - HX, dy = cy - HY, d = Or1(RigGeometry.Hypot(dx, dy)), mm = HR + a.Rx * 0.5; if (d < mm) { cx = HX + dx / d * mm; cy = HY + dy / d * mm; } }
            double sx = px - a.Side * 1.2, sy0 = py + 1.5, ddx = cx - sx, ddy = cy - sy0, len = RigGeometry.Hypot(ddx, ddy), rest = RigGeometry.Hypot(vx + a.Side * 1.2, vy - 1.5);
            double r0 = Or0(a.Rot) * D2R + ang;
            if (len > rest + 1) { double want = Math.Atan2(ddy, ddx) - Math.PI / 2, df = want - r0; df = ((df % TAU) + TAU * 1.5) % TAU - Math.PI; r0 += df * SM((len - rest - 1) / 8); }
            var ya = YawB(cy); double cyA = ya.c, syA = ya.s, c0 = Math.Cos(r0), s0 = Math.Sin(r0);
            // ax = [[rx c0, rx s0, 0], [-ry s0, ry c0, 0], [0, 0, rx 1.05]] turned by the yaw: [x cyA + z syA, y]
            double a0x = a.Rx * c0 * cyA + 0 * syA, a0y = a.Rx * s0, a1x = -a.Ry * s0 * cyA + 0 * syA, a1y = a.Ry * c0, a2x = 0 * cyA + a.Rx * 1.05 * syA, a2y = 0;
            double PX = 50 + (cx - 50) * cyA + (z - ZC) * syA; var Pw = Glob(PX, cy); double zv = BywZ(cx, z, cy), zH = -3;
            RigGeometry.EllAx(Dp(a.E), PX, cy, a0x, a0y, a1x, a1y, a2x, a2y, _glob); Op(a.E, zv < zH ? 0 : 1);
            var tb = _tubes[Lf ? 0 : 1];
            if (tb != null)
            {
                var Sw0 = Byw(sx, sy0, 4); double mx = (Sw0.x + Pw.x) / 2, my = (Sw0.y + Pw.y) / 2, tx = Pw.x - Sw0.x, ty = Pw.y - Sw0.y, tl = Or1(RigGeometry.Hypot(tx, ty));
                double nx = -ty / tl, ny = tx / tl; if (nx * a.Side < 0) { nx = -nx; ny = -ny; }
                double bu = Math.Max(0, tl - rest - 2) * 0.18, gw = SM((len - rest - 0.5) / 5);
                var d = Dp(tb); d.C('M'); d.P(Sw0.x, Sw0.y); d.C('Q'); d.P(mx + nx * bu, my + ny * bu); d.P(Pw.x, Pw.y);
                Sw(tb, R2(a.Rx * 1.6 * Math.Sqrt((1 + sq * 0.4) * sy) * gw)); Op(tb, zv < zH || gw < 0.02 ? 0 : 1);
            }
            W[Lf ? "pawL" : "pawR"] = Pw;
        }

        // ---- the face drawn by the rig ----
        double _ecx, _ecy, _er, _ec, _esl, _eout, _eh, _eqz, _edir;   // the eye map's frame
        readonly List<RigSeg> _lip = new List<RigSeg> { new RigSeg('M', 0, 0), new RigSeg('Q', 0, 0, 0, 0), new RigSeg('Q', 0, 0, 0, 0) };
        readonly List<RigSeg> _lipO = new List<RigSeg> { new RigSeg('M', 0, 0), new RigSeg('Q', 0, 0, 0, 0), new RigSeg('Q', 0, 0, 0, 0), new RigSeg('C', 0, 0, 0, 0, 0, 0), new RigSeg('Z') };

        (double x, double y) EyeMap(double x, double y)
        {
            double dy = y - _ecy, ce = Clamp(_ec + _esl * (0.5 + 0.5 * _eout * (x - _ecx) / _er), 0, 1), X = x, Y = _ecy + dy * (dy < 0 ? 1 - 2 * ce : 1 - 2 * _eh);
            if (_eqz > 0)
            {
                double cu = Clamp((x - _ecx) / _er, -1, 1), uu = _edir > 0 ? (cu + 1) / 2 : (1 - cu) / 2;
                X = Lerp(X, _ecx + _edir * (-0.8 + uu * 1.4) * _er, _eqz); Y = Lerp(Y, _ecy + JsSign(dy) * (1 - uu) * 0.6 * _er, _eqz);
            }
            return Face(X, Y);
        }
        static double JsSign(double v) => double.IsNaN(v) ? double.NaN : v > 0 ? 1 : v < 0 ? -1 : v;

        void Face()
        {
            double rd = Clamp(P["red"], 0, 1), gr = Clamp(P["green"], 0, 1);
            if (tintR != null) { var d = Dp(tintR); if (_headD != null) d.CopyFrom(_headD); Op(tintR, R2(rd * 0.34)); }
            if (tintG != null) { var d = Dp(tintG); if (_headD != null) d.CopyFrom(_headD); Op(tintG, R2(gr * 0.26)); }
            if (bodyG != null)
            {
                if (gr > 0.01 && _main != null) { var d = Dp(bodyG); d.CopyFrom(_main.E.D); }
                Op(bodyG, R2(gr * 0.26));
            }
            double ox = P["lx"] * 2.6, oy = P["ly"] * 2.2, ws = 1 + Clamp(P["wide"], 0, 1) * 0.22, h = Clamp(P["hap"], 0, 1), qz = Clamp(P["sqz"], 0, 1), cr = Clamp(P["cross"], 0, 1) * 1.8, ty = Clamp(P["teary"], 0, 1);
            EyeW.Clear();
            int n = EYx.Length;
            for (int i = 0; i < n; i++)
            {
                bool Lf = EYx[i] < (n > 1 ? (EYx[0] + EYx[1]) / 2 : 50); double dir = Lf ? 1 : -1, c = Clamp(Math.Max(P["close"], Lf ? P["winkL"] : P["winkR"]), 0, 1) * (1 - SM(h * 1.6));
                double R0 = (Truthy(EYr[i]) ? EYr[i] : 4.6) * 1.08, k = R0 / 5.2, cx = EYx[i] + ox * k + dir * cr * k, cy = EYy[i] + oy * k - h * 0.8 * k, r = R0 * ws, sl = Clamp(P["sl"], 0, 1) * 0.32, outv = Lf ? -1 : 1;
                var o = _eyes[i];
                _ecx = cx; _ecy = cy; _er = r; _ec = c; _esl = sl; _eout = outv; _eh = h; _eqz = qz; _edir = dir;
                var eD = Dp(o.E); RigGeometry.EllS(eD, cx, cy, r, r, 0, 8, _eyeMap);
                Dp(o.Clip).CopyFrom(eD);
                Sw(o.E, R2(1.3 * k * Math.Max(SM((c - 0.5) / 0.45), Math.Max(SM((h - 0.5) / 0.45), SM(qz * 1.6)))));
                double ha = (1 - SM((c - 0.4) / 0.25)) * (1 - SM((h - 0.12) / 0.3)) * (1 - SM(qz * 2.5)), hk = ws * (1 + ty * 0.4) * k, hd = c * r * 0.7;
                RigGeometry.EllS(Dp(o.H1), cx - 1.7 * k * ws, cy - 1.9 * k * ws + hd, 1.8 * hk, 1.8 * hk, 0, 6, _face);
                RigGeometry.EllS(Dp(o.H2), cx + 1.8 * k * ws, cy + 1.6 * k * ws, 0.7 * hk, 0.7 * hk, 0, 6, _face);
                Op(o.H1, R2(ha)); Op(o.H2, R2(ha * 0.9));
                RigGeometry.EllS(Dp(o.Tp), cx, cy + r * 0.8, r, r * 0.42, 0, 8, _face); Op(o.Tp, R2(ty * 0.85 * (1 - qz) * (1 - SM(c * 1.5))));
                Op(o.G, R2(Zc(cx, cy))); var w = Face(cx, cy); EyeW.Add(w); W[Lf ? "eyeL" : "eyeR"] = w;
            }
            if (!W.ContainsKey("eyeR") && W.TryGetValue("eyeL", out var el)) W["eyeR"] = el;
            {
                double mk0 = (Truthy(MOw) ? MOw : 9.6) * 0.36 / 3.5, sm = Clamp(P["smile"], 0, 1), op = Clamp(P["open"], 0, 1);
                if (sky)
                {   // I.dj: the mouth opens a little while a dance plays
                    double dj0 = Or0(_dj); _dj = dj0 + ((_cur.Contains("dance") ? 0.55 : 0) - Or0(_dj)) * Math.Min(1, dtS * 5);
                    op = Math.Max(op, _dj);
                }
                else op = Math.Max(op, 0);
                double fr = Clamp(P["frown"], 0, 1), mw = P["mw"] * (1 + sm * 0.12), hw = 3.5 * mk0 * mw * (1 + op * 0.3), X0 = MOx, Y0 = MOy;
                double y0 = Y0 + (-sm * 0.5 + fr * 1.3) * mk0, d = (2.3 * (1 + sm * 0.45) - fr * 4) * (1 - op * 0.55) * mk0, ym = Y0 - fr * 0.2 * mk0;
                Lip(_lip, X0, hw, y0, d, ym);
                double mv = Zc(X0, Y0);
                RigGeometry.ToD(Dp(mouth), _lip, _face); Op(mouth, R2(mv)); Sw(mouth, R2(1.2 * Math.Max(0.8, mk0)));
                if (op > 0.03)
                {
                    Lip(_lipO, X0, hw, y0, d, ym);
                    var C = _lipO[3].P; C[0] = R2(X0 + hw); C[1] = R2(y0 + op * 8 * mk0); C[2] = R2(X0 - hw); C[3] = R2(y0 + op * 8 * mk0); C[4] = R2(X0 - hw); C[5] = R2(y0);
                    RigGeometry.ToD(Dp(mopen), _lipO, _face);
                    RigGeometry.EllS(Dp(tongue), X0, y0 + op * 4.2 * mk0, 2.3 * op * mw * mk0, 1.4 * op * mk0, 0, 8, _face);
                    Op(mopen, R2(mv)); Op(tongue, R2(mv));
                }
                else { Op(mopen, 0); Op(tongue, 0); }
                double tg = Clamp(P["tongue"], 0, 1); Op(tout, R2(mv * SM(tg * 3)));
                if (tg > 0.02) RigGeometry.EllS(Dp(tout), X0, y0 + (1.3 + tg * 1.9) * mk0, 2.2 * (1 + tg * 0.15) * mk0, (1 + tg * 1.7) * mk0, 0, 8, _face);
                W["mouth"] = Face(X0, Y0); W["nose"] = Face(X0, Y0 - 3.5);
            }
        }

        /// <summary>The lip as the game writes it ('M x y Q x y x y Q x y x y', numbers rounded) and parses back.</summary>
        static void Lip(List<RigSeg> s, double X0, double hw, double y0, double d, double ym)
        {
            var M = s[0].P; M[0] = R2(X0 - hw); M[1] = R2(y0);
            var A = s[1].P; A[0] = R2(X0 - hw / 2); A[1] = R2(y0 + d); A[2] = R2(X0); A[3] = R2(ym);
            var B = s[2].P; B[0] = R2(X0 + hw / 2); B[1] = R2(ym + d); B[2] = R2(X0 + hw); B[3] = R2(y0);
        }

        // ---- where things are (I.W), for the particles, props and the light ----
        void Where()
        {
            W["head"] = TopW = HeadT(HX, TOP + 1); W["body"] = Glob(50, (HY + G) / 2 + 6); W["belly"] = Glob(50, G - (G - HY) * 0.28);
            W["sideL"] = Glob(BBx + BBw * 0.12, G - 14); W["sideR"] = Glob(BBx + BBw * 0.88, G - 14);
            for (int i = 0; i < 2; i++)
            {
                double sd = i == 0 ? -1 : 1; Item t = null; foreach (var q in _tops) if (q.Side == sd) { t = q; break; }
                W[i == 0 ? "earL" : "earR"] = t != null ? Sph(t.Cx, t.By + 2, ZGiven, 0) : HeadT(HX + sd * HR * 0.6, TOP + 4);
            }
            W["tail"] = _side != null ? Glob(_side.Cx, _side.Cy) : Glob(BBx + BBw * 0.9, G - 16);
            W["footL"] = _feet.Length > 0 ? Glob(_feet[0].Ox, _feet[0].Oy) : Glob(42, G - 3);
            W["footR"] = _feet.Length > 1 ? Glob(_feet[1].Ox, _feet[1].Oy) : Glob(58, G - 3);
        }

        void Shadow()
        {
            if (shadow == null) return;
            double cwh = 0, gl = Math.Max(0, Plift - cwh * (1 - Math.Cos(tr)));
            Cx(shadow, R2(50 + Px + Plean * 0.15 + cwh * Math.Sin(tr))); Cy(shadow, R2(G + 0.6));
            Rx(shadow, R2((_key == "pina" ? Math.Min(BBw, 56) : BBw) * 0.5 * (1 + sq * 0.45) * (1 - gl / 70))); Ry(shadow, R2(3.6 * (1 - gl / 90)));
            Op(shadow, R2(0.2 * (1 - gl / 50)));
        }
    }
}
