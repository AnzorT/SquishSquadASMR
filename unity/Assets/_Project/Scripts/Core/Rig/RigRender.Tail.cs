// render() of rive-rig.js, continued: the tapered solid tail (D.tailTaper: tailF, its shade tailS and the shade's clip). Ported line for line like the rest of RigRender (see RigRender.cs);
// TailFeatures lists the parts of D this file draws (RigRender.Unported refuses a creature using a part no file lists).
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigRender
    {
        static readonly string[] TailFeatures = { "tailTaper" };

        RigEl _tailF, _tailS, _tailSC;
        // hwAt(i) = tw * _hwA[i] * _hwB[i]: the taper and the root's flare (they don't change)
        double[] _hwA, _hwB, _tailHw;
        (double x, double y)[] _nr;
        readonly List<(double x, double y)> _tfPts = new List<(double x, double y)>(64);
        static readonly double[] CapF = { 0.25, 0.5, 0.75 }, BaseF = { 0.2, 0.4, 0.6, 0.8 };
        static readonly double[] CapS = SinPi(CapF, true), CapC = SinPi(CapF, false), BaseS = SinPi(BaseF, true), BaseC = SinPi(BaseF, false);
        static double[] SinPi(double[] f, bool sin) { var a = new double[f.Length]; for (int i = 0; i < f.Length; i++) { double t = f[i] * Math.PI; a[i] = sin ? Math.Sin(t) : Math.Cos(t); } return a; }

        void InitTail()
        {
            // mount: tailF / tailS / tailSC only on a 3D tail (D.tail3) with D.tailTaper
            if (!tail3 || !_g.Has("tailTaper") || _g.Tail == null) return;
            _tailF = El("tailF"); _tailS = El("tailS"); _tailSC = El("tailSC");
            var tt = _g.A("tailTaper"); double wb = tt[0], wt = tt[1];
            int n = _g.Tail.Length; _hwA = new double[n]; _hwB = new double[n]; _tailHw = new double[n]; _nr = new (double x, double y)[n];
            for (int i = 0; i < n; i++)
            {
                _hwA[i] = wb + (wt - wb) * Math.Pow((double)i / (n - 1), 0.85);
                _hwB[i] = 1 + 0.14 * (1 - SM((double)i / (n - 1) / 0.3));
            }
        }

        /// <summary>D.tailTaper, after tail / tailH: the spine sp3 and the tube width tw. The spine offset by a half-width that
        /// shrinks from the base to the tip, a round tip and a round base tucked into the body; the shade along the lower side.</summary>
        void TailTaper((double x, double y)[] sp3, double tw)
        {
            if (_tailF == null) return;
            int n = sp3.Length; var NR = _nr; var hw = _tailHw;
            for (int i = 0; i < n; i++)
            {
                hw[i] = tw * _hwA[i] * _hwB[i];
                var a = sp3[Math.Max(0, i - 1)]; var b = sp3[Math.Min(n - 1, i + 1)];
                double dx = b.x - a.x, dy = b.y - a.y, l = Or1(RigGeometry.Hypot(dx, dy));
                NR[i] = (-dy / l, dx / l);
            }
            double sum = 0; for (int i = 0; i < n; i++) sum += NR[i].y;
            double dn = sum >= 0 ? 1 : -1;
            var P = _tfPts; P.Clear();
            for (int i = 0; i < n; i++) P.Add((sp3[i].x - NR[i].x * hw[i] * dn, sp3[i].y - NR[i].y * hw[i] * dn));   // L
            {   // round tip, from the left edge over the end to the right edge
                var tp = sp3[n - 1]; var tq = sp3[n - 2];
                double ux = tp.x - tq.x, uy = tp.y - tq.y, ul = Or1(RigGeometry.Hypot(ux, uy)), hT = hw[n - 1];
                for (int k = 0; k < 3; k++)
                {
                    double nx = NR[n - 1].x * dn, ny = NR[n - 1].y * dn;
                    P.Add((tp.x + (ux / ul * CapS[k] - nx * CapC[k]) * hT, tp.y + (uy / ul * CapS[k] - ny * CapC[k]) * hT));
                }
            }
            for (int i = n - 1; i >= 0; i--) P.Add((sp3[i].x + NR[i].x * hw[i] * dn, sp3[i].y + NR[i].y * hw[i] * dn));   // Rr, reversed
            {   // round base, from the right edge round the back to the left edge
                var s0 = sp3[0]; var s1 = sp3[2];
                double vx0 = s1.x - s0.x, vy0 = s1.y - s0.y, vl0 = Or1(RigGeometry.Hypot(vx0, vy0)), h0 = hw[0], nx0 = NR[0].x * dn, ny0 = NR[0].y * dn;
                for (int k = 0; k < 4; k++)
                    P.Add((s0.x + (nx0 * BaseC[k] - vx0 / vl0 * BaseS[k] * 0.55) * h0, s0.y + (ny0 * BaseC[k] - vy0 / vl0 * BaseS[k] * 0.55) * h0));
            }
            var fd = Dp(_tailF); RigGeometry.ClosedD(fd, P); Dp(_tailSC).CopyFrom(fd);
            // the shade: from a quarter of the way out to past the lower edge
            P.Clear();
            for (int i = 1; i < n - 1; i++) P.Add((sp3[i].x + NR[i].x * hw[i] * dn * 0.25, sp3[i].y + NR[i].y * hw[i] * dn * 0.25));   // mid
            for (int i = n - 2; i >= 1; i--) P.Add((sp3[i].x + NR[i].x * hw[i] * dn * 1.5, sp3[i].y + NR[i].y * hw[i] * dn * 1.5));    // Rx, reversed
            RigGeometry.ClosedD(Dp(_tailS), P);
            // (the game also hides tail, tailH and tailT with style display none)
        }
    }
}
