// render() of rive-generic.js: Bao's bun (ROUND.bun; setup() builds I.bun): the pleats fanning out from the knot on
// top of the bun, its shadow, the knot itself (a little 3D ball: the outline round its points, its own gradient) and
// the creases on the knot, all riding on the head sphere as he turns.
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;
using J = PlushCrush.Core.Rig.RigGenericGeo;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigGeneric
    {
        static readonly string[] BunKeys = { "bun" };

        sealed class BunKit { public double[] PlA, CrA; public RigEl[] Pl, Cr; public RigEl KSh, Kn, Gr; public double Cx, Cy, Cz, Rx, Ry; }
        BunKit _bun;
        readonly List<(double x, double y)> _kOut = new List<(double x, double y)>(84), _kHull = new List<(double x, double y)>(48), _kUp = new List<(double x, double y)>(48);

        void InitBun()
        {
            var B = J.Obj(_geo.G, "bun"); if (B == null) return;
            var pl = J.List(B, "pl"); var cr = J.List(B, "cr"); var C = J.List(B, "C");
            _bun = new BunKit { PlA = new double[pl.Count], Pl = new RigEl[pl.Count], CrA = new double[cr.Count], Cr = new RigEl[cr.Count],
                KSh = El(J.El(J.Get(B, "kSh"))), Kn = El(J.El(J.Get(B, "kn"))), Gr = El(J.El(J.Get(B, "gr"))),
                Cx = J.Num(C[0]), Cy = J.Num(C[1]), Cz = J.Num(C[2]), Rx = J.N(B, "rx"), Ry = J.N(B, "ry") };
            for (int i = 0; i < pl.Count; i++) { _bun.PlA[i] = J.N(pl[i], "a"); _bun.Pl[i] = El(J.El(J.Get(pl[i], "el"))); }
            for (int i = 0; i < cr.Count; i++) { _bun.CrA[i] = J.N(cr[i], "a"); _bun.Cr[i] = El(J.El(J.Get(cr[i], "el"))); }
        }

        double RotZ(double dx, double z) => -dx * syh + z * cyh;

        void Bun()
        {
            var B = _bun; double Cx0 = B.Cx, Cy0 = B.Cy, Cz0 = B.Cz, tw = Math.Sin(_now / 900) * 0.04;
            for (int i = 0; i < B.Pl.Length; i++)
            {
                double a = B.PlA[i] + tw, s = Math.Sin(a), c = Math.Cos(a), dr = 3 + 4.6 * Math.Abs(s), a2 = a - 0.38, x2 = 50 + 9 * Math.Sin(a2), z2 = 9 * Math.Cos(a2);
                var d = Dp(B.Pl[i]);
                d.C('M'); d.P(Sph(50 + 2.5 * s, 31, ZGiven, 2.5 * c));
                d.C('Q'); d.P(Sph(x2, 31.6 + dr * 0.35, ZGiven, z2)); d.P(Sph(50 + 15 * s, 31 + dr, ZGiven, 15 * c));
                Op(B.Pl[i], R2(0.8 * SM((RotZ(15 * s, 15 * c) + 2) / 7)));
            }
            var sp0 = Sph(50, 31.4, ZGiven, 0); Cx(B.KSh, R2(sp0.x)); Cy(B.KSh, R2(sp0.y)); Rx(B.KSh, 7.2); Ry(B.KSh, 2);
            _kOut.Clear();
            for (int i = 0; i < 6; i++)
            {
                double v = -1 + i / 5.0 * 2, rr = Math.Sqrt(1 - v * v * 0.92);
                for (int k = 0; k < 14; k++) { double f = k / 14.0 * TAU; _kOut.Add(Sph(Cx0 + B.Rx * rr * Math.Sin(f), Cy0 + B.Ry * v, ZGiven, Cz0 + B.Rx * rr * Math.Cos(f))); }
            }
            RigGeometry.Hull(_kOut, _kHull, _kUp); RigGeometry.ClosedD(Dp(B.Kn), _kHull);
            var kc = Sph(Cx0, Cy0, ZGiven, Cz0); Cx(B.Gr, R2(kc.x - 2.6)); Cy(B.Gr, R2(kc.y - 2.4));
            for (int j = 0; j < B.Cr.Length; j++)
            {
                double a = B.CrA[j] + tw * 3; var d = Dp(B.Cr[j]);
                for (int i = 0; i <= 6; i++)
                {
                    double u = -1 + i / 3.0, v = -0.15 - 0.55 * (1 - u * u * 0.6), ang = a + u * 0.75, rr = Math.Sqrt(Math.Max(0, 1 - v * v)) * 1.02;
                    d.C(i > 0 ? 'L' : 'M'); d.P(Sph(Cx0 + B.Rx * rr * Math.Sin(ang), Cy0 + B.Ry * v, ZGiven, Cz0 + B.Rx * rr * Math.Cos(ang)));
                }
                Op(B.Cr[j], R2(SM((RotZ(Math.Sin(a), Math.Cos(a)) - 0.1) / 0.35)));
            }
        }
    }
}
