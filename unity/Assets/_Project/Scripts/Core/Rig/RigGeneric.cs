// render() of rive-generic.js (the generic rig: the 98 creatures rive-rig.js has no hand-made definition for), ported
// line for line. setup() (which works out the roles of the creature's own art and builds the rig) isn't ported: its
// result comes as data (RigGenericGeo, exported by tools/unity-port/rig/record-generic.mjs). Each frame's pose becomes
// the attributes the game writes to the drawing (d, opacity, stroke-width, cx / cy / rx / ry), on RigEls named as the
// recorder names the elements; the drawing (RigTree / RigDisplay) is the same as the hand-made rigs'.
//
// render() is one function with a shared core (the body mesh: glob, the head sphere: sph / headT, the art's shapes by
// role, paws, eyes, mouth, shadow) and many per-creature blocks (if (I.bun) ..., if (I.pn) ...). This file has the
// core; each creature kit is a partial file (RigGeneric.Bun.cs ...) listing the setup() keys it draws. A creature whose
// rig uses anything not ported yet is refused when the renderer is made (Unported), with the feature's name.
// Checked number for number against the game by tools/unity-port/rig/check. RIG_PORT.md step 5.
using System;
using System.Collections.Generic;
using static PlushCrush.Core.Rig.RigMath;
using J = PlushCrush.Core.Rig.RigGenericGeo;

namespace PlushCrush.Core.Rig
{
    public sealed partial class RigGeneric
    {
        const double TAU = Math.PI * 2;
        const int ZSurf = 0, ZBack = 1, ZGiven = 2;   // sz()'s zm: undefined, 'b', a number

        readonly RigGenericGeo _geo;
        readonly string _key;
        public readonly List<RigEl> All = new List<RigEl>();
        readonly Dictionary<string, RigEl> _els = new Dictionary<string, RigEl>();
        /// <summary>The element of that name (every element of the drawing has one), or null.</summary>
        public RigEl El(string name) => name != null && _els.TryGetValue(name, out var e) ? e : null;

        /// <summary>Where things are on the creature this frame (I.W: eyes, paws, mouth, head, feet...), in rig units.</summary>
        public readonly Dictionary<string, (double x, double y)> W = new Dictionary<string, (double x, double y)>();
        public (double x, double y) TopW;
        public readonly List<(double x, double y)> EyeW = new List<(double x, double y)>(2);
        /// <summary>A branch this frame needed that isn't ported (it isn't drawn): null when none.</summary>
        public string Missing;
        /// <summary>The generic rig sorts nothing by depth through an order key (it moves elements): always "".</summary>
        public string OrderKey => "";

        /// <summary>For the check while porting: draw what's ported and skip the rest instead of refusing the creature.</summary>
        public static bool AllowPartial;

        // ---------------- setup()'s result ----------------
        sealed class Item
        {
            public RigEl E; public string Role, Tag; public bool InBody, OpT, Owner; public double OpN;
            public double Rot, Cx, Cy, Ox, Oy, Rx, Ry, Side, Hx, Hy, Bx, By, Bw, Bh;
            public List<RigSeg> Segs; public Dictionary<string, object> Raw;
        }
        sealed class Eye { public RigEl G, E, Tp, H1, H2, Clip; }

        readonly Item[] _items, _arms, _tops, _feet; readonly Item _side, _main;
        // I.geo
        readonly double G, TOP, HX, HY, HR, NY, pawX, pawY, MOx, MOy, MOw, BBx, BBy, BBw, BBh;
        readonly double[] EYx, EYy, EYr;
        // flags
        readonly double rk, srI, armOut, armReach, zc0;
        readonly bool sky, whole, hb, mReach, armStr, avoidHead;
        // elements
        readonly RigEl tintR, tintG, bodyG, mouth, mopen, tongue, tout, shadow, headClip;
        readonly Eye[] _eyes; readonly RigEl[] _tubes;
        // I.mapHand: Mittens' hand offsets onto this creature (setup's closure + the ROUND wrappers)
        readonly double[] _CY; readonly double _mhKx, _mhD0; readonly bool _handFix;
        static readonly double[] MY = { 17, 47, 58, 76, 95.5 };

        // the setup() keys the core draws (or that carry nothing for render); kits add theirs (RigGeneric.<kit>.cs)
        static readonly string[] CoreKeys = { "generic", "key", "u", "shadow", "art", "fxG", "pg", "E", "faceG", "mapHand", "toM", "fromM", "geo", "tubes", "rk", "sky", "sr", "armOut",
            "armReach", "whole", "hb", "mReach", "armStr", "avoidHead", "headClip", "items", "zc0" };
        static readonly string[] CoreRoles = { "head", "shine", "wsurf", "cheek", "foot", "arm", "xHide" };
        static readonly string[] ItemKeys = { "e", "tag", "inBody", "rot", "cx", "cy", "b", "a", "op", "fill", "ox", "oy", "rx", "ry", "role", "side", "hx", "hy", "segs", "pet", "_zv" };
        // ROUND flags that change I.mapHand (setup): only handFix is ported
        static readonly string[] HandFlags = { "pina", "avo", "berri", "lemmy", "kiwi", "peachy", "melo", "pitaya", "tutti", "hana", "boo", "pixie", "bk", "choco", "lowPaw" };
        static readonly HashSet<string> Ported = new HashSet<string>(Concat(CoreKeys, BunKeys));

        static IEnumerable<string> Concat(params string[][] a) { foreach (var l in a) foreach (var s in l) yield return s; }

        /// <summary>The first thing this creature's rig uses that isn't ported yet, or null.</summary>
        public static string Unported(RigGenericGeo geo)
        {
            foreach (var kv in geo.G) if (!Ported.Contains(kv.Key) && J.Truthy(kv.Value)) return "I." + kv.Key;
            if (geo.RC != null) foreach (var f in HandFlags) if (J.Has(geo.RC, f)) return "ROUND." + f + " (its I.mapHand)";
            if (J.List(geo.G, "items") is List<object> items)
                foreach (var q in items)
                {
                    var d = (Dictionary<string, object>)q; var role = J.Get(d, "role") as string;
                    if (Array.IndexOf(CoreRoles, role) < 0) return "items of role '" + role + "'";
                    foreach (var kv in d) if (Array.IndexOf(ItemKeys, kv.Key) < 0 && J.Truthy(kv.Value)) return "item." + kv.Key + " (role " + role + ")";
                }
            var main = J.Obj(J.Obj(geo.G, "geo"), "main");
            if (main != null && J.Get(main, "q") == null) return "geo.main without an item";
            return null;
        }

        public RigGeneric(RigGenericGeo geo, string key = null)
        {
            _geo = geo; _key = key ?? geo.Key;
            var missing = Unported(geo);
            if (missing != null && !AllowPartial) throw new NotSupportedException("RigGeneric: " + _key + " uses " + missing + ", not ported yet (RIG_PORT.md step 5)");
            foreach (var n in geo.Names()) { var e = new RigEl(n); All.Add(e); _els[n] = e; }
            var I = geo.G;
            RigEl el(object v) => El(J.El(v));

            // I.geo
            var g = J.Obj(I, "geo");
            G = J.N(g, "G"); TOP = J.N(g, "TOP"); HX = J.N(g, "HX"); HY = J.N(g, "HY"); HR = J.N(g, "HR"); NY = J.N(g, "NY");
            pawX = J.N(g, "pawX"); pawY = J.N(g, "pawY");
            var MO = J.Obj(g, "MO"); MOx = J.N(MO, "x"); MOy = J.N(MO, "y"); MOw = J.N(MO, "w");
            var BB = J.Obj(g, "BB"); BBx = J.N(BB, "x"); BBy = J.N(BB, "y"); BBw = J.N(BB, "width"); BBh = J.N(BB, "height");
            var EY = J.List(g, "EY") ?? new List<object>();
            EYx = new double[EY.Count]; EYy = new double[EY.Count]; EYr = new double[EY.Count];
            for (int i = 0; i < EY.Count; i++) { EYx[i] = J.N(EY[i], "x"); EYy[i] = J.N(EY[i], "y"); EYr[i] = J.N(EY[i], "r"); }

            // flags (JS: I.rk || 1 and the like are applied where they're read)
            rk = J.N(I, "rk"); srI = J.N(I, "sr"); armReach = J.N(I, "armReach"); zc0 = J.N(I, "zc0");
            armOut = J.Get(I, "armOut") == null ? double.NaN : J.N(I, "armOut");   // I.armOut != null ? I.armOut : 7
            sky = J.Has(I, "sky"); whole = J.Has(I, "whole"); hb = J.Has(I, "hb"); mReach = J.Has(I, "mReach"); armStr = J.Has(I, "armStr"); avoidHead = J.Has(I, "avoidHead");

            // the art's shapes
            var items = J.List(I, "items") ?? new List<object>();
            _items = new Item[items.Count];
            var arms = new List<Item>(); var tops = new List<Item>(); var feet = new List<Item>();
            for (int i = 0; i < items.Count; i++)
            {
                var q = (Dictionary<string, object>)items[i]; var b = J.Obj(q, "b");
                var it = new Item
                {
                    Raw = q, E = el(J.Get(q, "e")), Role = J.Get(q, "role") as string, Tag = J.Get(q, "tag") as string, InBody = J.Has(q, "inBody"),
                    OpT = J.Truthy(J.Get(q, "op")), OpN = J.Num(J.Get(q, "op")), Owner = J.Has(q, "owner"),
                    Rot = J.N(q, "rot"), Cx = J.N(q, "cx"), Cy = J.N(q, "cy"), Ox = J.N(q, "ox"), Oy = J.N(q, "oy"), Rx = J.N(q, "rx"), Ry = J.N(q, "ry"),
                    Side = J.N(q, "side"), Hx = J.N(q, "hx"), Hy = J.N(q, "hy"), Bx = J.N(b, "x"), By = J.N(b, "y"), Bw = J.N(b, "width"), Bh = J.N(b, "height"),
                    Segs = J.Segs(J.Get(q, "segs")),
                };
                if (J.Get(q, "rot") == null) it.Rot = double.NaN;   // null: (a.rot || 0) reads 0, as NaN does
                _items[i] = it;
                if (it.Role == "arm") arms.Add(it); if (it.Role == "top" && !it.Owner) tops.Add(it); if (it.Role == "foot") feet.Add(it);
                if (it.Role == "side" && _side == null) _side = it;
            }
            _arms = arms.ToArray(); _tops = tops.ToArray(); _feet = feet.ToArray();
            var mq = J.Get(J.Obj(g, "main"), "q");
            for (int i = 0; i < items.Count; i++) if (ReferenceEquals(items[i], mq)) _main = _items[i];

            // the rig's own elements
            var E = J.Obj(I, "E");
            tintR = el(J.Get(E, "tintR")); tintG = el(J.Get(E, "tintG")); bodyG = el(J.Get(E, "bodyG"));
            mouth = el(J.Get(E, "mouth")); mopen = el(J.Get(E, "mopen")); tongue = el(J.Get(E, "tongue")); tout = el(J.Get(E, "tout"));
            var eyes = J.List(E, "eyes") ?? new List<object>();
            _eyes = new Eye[eyes.Count];
            for (int i = 0; i < eyes.Count; i++) { var o = eyes[i]; _eyes[i] = new Eye { G = el(J.Get(o, "g")), E = el(J.Get(o, "e")), Tp = el(J.Get(o, "tp")), H1 = el(J.Get(o, "h1")), H2 = el(J.Get(o, "h2")), Clip = el(J.Get(o, "clip")) }; }
            shadow = el(J.Get(I, "shadow")); headClip = el(J.Get(I, "headClip"));
            var tb = J.List(I, "tubes") ?? new List<object>();
            _tubes = new RigEl[2]; for (int i = 0; i < 2 && i < tb.Count; i++) _tubes[i] = el(tb[i]);

            // I.mapHand (setup: kx, CY from the art's top, eyes, mouth, paws and feet; ROUND.handFix keeps the paws where the art has them)
            double eyeY = J.N(g, "eyeY");
            var src = new[] { TOP, eyeY, MOy, pawY, G };
            _CY = new double[src.Length];
            for (int i = 0; i < src.Length; i++) _CY[i] = i > 0 && src[i] <= src[i - 1] ? src[i - 1] + 1 : src[i];   // (map's third argument: the source array)
            _mhKx = Math.Max(0.5, Math.Abs(50 - pawX) / 18);
            _handFix = geo.RC != null && J.Has(geo.RC, "handFix");
            _mhD0 = MapHand0(0, 0).y;

            _glob = Glob; _headT = HeadT; _face = Face; _wsurf = WsurfMap; _puff = PuffMap; _nrwHead = NrwHead; _eyeMap = EyeMap;
            InitBun();
        }

        // setup's pl(): piecewise linear through the knots (extrapolated below the first)
        static double Pl(double[] A, double[] B, double v)
        {
            if (v <= A[0]) return B[0] + (v - A[0]) * ((B[1] - B[0]) / (A[1] - A[0]));
            for (int i = 1; i < A.Length; i++) if (v <= A[i] || i == A.Length - 1) return B[i - 1] + (v - A[i - 1]) * (B[i] - B[i - 1]) / (A[i] - A[i - 1]);
            return double.NaN;
        }
        (double x, double y) MapHand0(double inX, double hy) => (inX * _mhKx, Pl(MY, _CY, 76 + hy) - pawY);
        (double x, double y) MapHand(double inX, double hy)
        {
            var m = MapHand0(inX, hy);
            if (_handFix) m = (m.x, m.y - _mhD0 * Clamp(1 + Math.Min(0, hy) / 18, 0, 1));
            return m;
        }
        /// <summary>I.fromM: a point on Mittens' frame (a move's tap) onto this creature (the engine's FromM).</summary>
        public Func<double, double, (double x, double y)> FromM => (x, y) => (HX + (x - 50) / 29 * HR, Pl(MY, _CY, y));
        /// <summary>I.toM: a point on this creature onto Mittens' frame.</summary>
        public (double x, double y) ToM(double x, double y) => (50 + (x - HX) / HR * 29, Pl(_CY, MY, y));

        // ---------------- the frame: render()'s locals ----------------
        RigPose P;
        double _now; string _cur = "idle";
        double Px, Plift, Plean, Hh, SR, yb, yh, ph, cyb, syb, cyh, syh, cp, sp, TWS, sq, sy, tr, ct, stt, bnd, HN, MAx, MAy, hs, chr, shr, hyo, RK, ZC, NS, pf, dtS;
        bool TWon, hasMS, hasDn; double MSs, MSc, MSn, dnx, dny, dna;
        double _lt = double.NaN, _dj = double.NaN;   // I._lt, I.dj (undefined until first set)
        readonly RigMap _glob, _headT, _face, _wsurf, _puff, _nrwHead, _eyeMap;

        static double Or0(double v) => v != 0 && !double.IsNaN(v) ? v : 0;
        static double Or1(double v) => v != 0 && !double.IsNaN(v) ? v : 1;
        static bool Truthy(double v) => v != 0 && !double.IsNaN(v);
        static double R2(double v) => RigGeometry.R2(v);
        static RigPath Dp(RigEl e) { e.D.Clear(); e.Written |= RigEl.AD; return e.D; }
        static void Op(RigEl e, double v) { e.Opacity = v; e.Written |= RigEl.AOpacity; }
        static void Sw(RigEl e, double v) { e.StrokeWidth = v; e.Written |= RigEl.AStroke; }
        static void Cx(RigEl e, double v) { e.Cx = v; e.Written |= RigEl.ACx; }
        static void Cy(RigEl e, double v) { e.Cy = v; e.Written |= RigEl.ACy; }
        static void Rx(RigEl e, double v) { e.Rx = v; e.Written |= RigEl.ARx; }
        static void Ry(RigEl e, double v) { e.Ry = v; e.Written |= RigEl.ARy; }

        (double c, double s) YawH(double y) { if (!TWon) return (cyh, syh); double a = yh + TWS * Clamp((G - y) / Hh, 0, 1); return (Math.Cos(a), Math.Sin(a)); }
        (double c, double s) YawB(double y) { if (!TWon) return (cyb, syb); double a = yb + TWS * Clamp((G - y) / Hh, 0, 1); return (Math.Cos(a), Math.Sin(a)); }

        /// <summary>glob: the body mesh (squash, lean, bend, tilt, the dent, the motion stretch) applied to a point of the art.</summary>
        (double x, double y) Glob(double x, double y)
        {
            if (hasDn)
            {
                double ddx = x - dnx, ddy = y - dny, gg = Math.Exp(-(ddx * ddx + ddy * ddy) / 90);
                if (gg > 0.01) { double vx = HX - dnx, vy = (HY + G) / 2 - dny, L = Or1(RigGeometry.Hypot(vx, vy)); x += vx / L * dna * 4.5 * gg; y += vy / L * dna * 4.5 * gg; }
            }
            double u = Clamp((G - y) / Hh, 0, 1), X, Y;
            {
                double ub = u, k = 1 + sq * (0.45 + 0.6 * 4 * ub * (1 - ub)), X0 = (x - 50) * k + Plean * Math.Pow(ub, 1.5), h = (G - y) * sy; X = X0; Y = -h;
                if (bnd != 0)
                {
                    double hn = HN * sy, c = bnd / hn;
                    if (h <= hn) { double a = c * h; X = (1 - Math.Cos(a)) / c + X0 * Math.Cos(a); Y = -(Math.Sin(a) / c - X0 * Math.Sin(a)); }
                    else { double dh = h - hn, cb = Math.Cos(bnd), sb = Math.Sin(bnd); X = (1 - cb) / c + X0 * cb + dh * sb; Y = -(sb / c - X0 * sb + dh * cb); }
                }
            }
            double gx = 50 + X * ct - Y * stt + Px, gy = G + X * stt + Y * ct - Plift - 0;
            if (hasMS)
            {
                double w = SM(u * 1.5), kk = 1 + MSs * w, dx = gx - MAx, dy = gy - MAy, al = (dx * MSc + dy * MSn) * kk, pe = (-dx * MSn + dy * MSc) / kk;
                gx = MAx + al * MSc - pe * MSn; gy = MAy + al * MSn + pe * MSc;
            }
            return (gx, gy);
        }

        (double x, double y) HeadT(double x, double y)
        {
            x = HX + (x - HX) * (1 + hs * 0.6); y = NY - (NY - y) * (1 - hs) + hyo;
            double dx = x - HX, dy = y - NY, X = HX + dx * chr - dy * shr, Y = NY + dx * shr + dy * chr;
            return Glob(X, Y);
        }

        double Sz(double dx, double dy, int zm, double zv)
        {
            if (zm == ZGiven) return zv;
            double q = SR * SR - dx * dx - dy * dy, z = (q > 0 ? Math.Sqrt(q) : 0) * RK;
            return zm == ZBack ? -z : z;
        }

        /// <summary>sph: a point on the head sphere (turned, pitched), then the head's own moves and the body mesh.</summary>
        (double x, double y) Sph(double x, double y, int zm = ZSurf, double zv = 0)
        {
            double dx = x - HX, dy = y - HY, z = Sz(dx, dy, zm, zv) - ZC; var cs = YawH(y);
            double x1 = dx * cs.c + z * cs.s, z1 = -dx * cs.s + z * cs.c + ZC;
            return HeadT(HX + x1, HY + dy * cp - z1 * sp);
        }

        double SphZ(double x, double y, int zm = ZSurf, double zv = 0)
        {
            double dx = x - HX, dy = y - HY, z = Sz(dx, dy, zm, zv) - ZC; var cs = YawH(y);
            double z1 = -dx * cs.s + z * cs.c;
            return dy * sp + z1 * cp;
        }

        (double x, double y) Face(double x, double y) => Sph(x, y);
        static double Vis(double z) => SM((z - 1.5) / 6);
        double Zc(double x, double y) => Vis(SphZ(x, y));
        (double x, double y) Byw(double x, double y, double z) { var cs = YawB(y); return Glob(50 + (x - 50) * cs.c + (z - ZC) * cs.s, y); }
        double BywZ(double x, double z, double y) { var cs = YawB(y); return -(x - 50) * cs.s + (z - ZC) * cs.c; }
        double Nrw(double x) => HX + (x - HX) * NS;
        (double x, double y) NrwHead(double x, double y) => HeadT(Nrw(x), y);
        (double x, double y) PuffMap(double x, double y)
        {
            double dy = y - MOy, gg = Math.Exp(-dy * dy / 80);
            return HeadT(Nrw(x + (x < HX ? -1 : 1) * pf * 3.2 * gg * Clamp(Math.Abs(x - HX) / 20, 0, 1)), y);
        }

        /// <summary>One frame: the pose (after tick()) and the engine's extras for the drawing.</summary>
        public void Render(RigPose pose, RigEngine eng, double now) => Render(pose, eng.Hs, eng.Ed, eng.SpTw, eng.Ms, eng.Dent, now, eng.CurName);

        public void Render(RigPose pose, double ihs, double ied, double ispTw, RigStretch ms, RigDent dent, double now, string cur = "idle")
        {
            P = pose; _now = now; _cur = cur ?? "idle"; Missing = null;
            foreach (var e in All) { e.Written = 0; if (e.StrWritten.Count > 0) e.StrWritten.Clear(); }
            Px = P["x"]; Plift = P["lift"]; Plean = P["lean"];
            Hh = G - TOP; SR = Truthy(srI) ? srI : HR + 0.5;
            double tw = Or0(P["twist"]);
            yb = (P["spin"] + (whole ? P["turn"] * 40 : 0) + tw * 12) * D2R;
            yh = (whole ? yb : yb + P["turn"] * 40 * D2R) + (Or0(P["hyaw"]) + tw * 0.7) * 40 * D2R;
            ph = P["pitch"] * 22 * D2R * 1;
            cyb = Math.Cos(yb); syb = Math.Sin(yb); cyh = Math.Cos(yh); syh = Math.Sin(yh); cp = Math.Cos(ph); sp = Math.Sin(ph);
            TWS = Clamp(Or0(ispTw), -0.7, 0.7); TWon = Math.Abs(TWS) > 0.0015;
            sq = Clamp(P["sq"], -0.25, 0.45); sy = 1 - sq; tr = (P["tilt"] + (hb ? P["htilt"] * 0.6 : 0)) * D2R; ct = Math.Cos(tr); stt = Math.Sin(tr);
            hasDn = dent != null && dent.A > 0.01; if (hasDn) { dnx = dent.X; dny = dent.Y; dna = dent.A; }
            bnd = Clamp(Or0(P["bend"]), -32, 32) * D2R; HN = Math.Max(8, G - NY + 2);
            hasMS = ms != null && ms.S > 0.004; if (hasMS) { MSs = ms.S; MSc = ms.C; MSn = ms.N; }
            MAx = 50 + Px; MAy = G - Plift - (G - HY) * sy;
            hs = hb ? 0 : ihs; double hr = hb ? 0 : P["htilt"] * D2R; chr = Math.Cos(hr); shr = Math.Sin(hr); hyo = hb ? 0 : -P["pitch"] * 1.1;
            RK = Or1(rk); ZC = Or0(zc0);
            pf = Clamp(P["puff"], -0.5, 1.2);
            dtS = !double.IsNaN(_lt) && _lt != 0 ? Clamp((now - _lt) / 1000, 0, 0.05) : 0.016; _lt = now;
            W.Clear();
            NS = RK < 1 ? Math.Sqrt(cyh * cyh + RK * RK * syh * syh) : 1;

            if (_bun != null) Bun();
            // the dizzy halo (P.dz: Shelly's Shell Spin; any creature's move can use it): makes its own elements, not ported yet
            if (Clamp(Or0(P["dz"]), 0, 1) > 0.005) Missing = "the dizzy halo (P.dz)";

            Items(ied);
            Face();
            Where();
            Shadow();
        }
    }
}
