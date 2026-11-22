// A creature's rig geometry as mount() in rive-rig.js prepares it: the definition (D: sizes, colours, which parts it
// has) and what mount() works out from it once (RG: parsed outlines, the ears' sections, the tail's spine). Exported
// from the running game by tools/unity-port/rig/record-rig.mjs (geo-<key>.json); data, so nothing here is retyped.
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    public struct RigEarSec { public double S, Cx, Cy, Hw, Hd; }

    public sealed class RigEar
    {
        public double Side, Ux, Uy, Amax, Nx, Ny;
        public double[] Pivot, Lobe;
        public List<RigSeg> O, N;
        public RigEarSec[] Secs;
        public bool Flop, Behind;
    }

    public sealed class RigArm { public double Side; public double[] E, Pivot; }

    public sealed class RigGeo
    {
        public Dictionary<string, object> Root;   // the whole file (D, RG, tree...)
        public Dictionary<string, object> D;
        public Dictionary<string, object> RGRaw;
        public List<RigSeg> Body, Skull, Muzzle, Nose, NoseHL, Stripes, Back;
        public List<List<RigSeg>> Wh = new List<List<RigSeg>>();
        public (double x, double y)[] Feet;
        public RigEar[] Ears;
        public RigArm[] Arms;
        public double[][] Tail;   // [x, y, s (0 root .. 1 tip), z]
        public double Hkx;
        public bool Generic, FurH, FurB;

        public static RigGeo FromJson(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var g = new RigGeo
            {
                Root = root,
                D = (Dictionary<string, object>)root["D"],
                RGRaw = (Dictionary<string, object>)root["RG"],
                Hkx = root.TryGetValue("hkx", out var hk) && hk is double h ? h : 0,
                Generic = Truthy(root.TryGetValue("generic", out var ge) ? ge : null),
                FurH = Truthy(root.TryGetValue("furH", out var fh) ? fh : null),
                FurB = Truthy(root.TryGetValue("furB", out var fb) ? fb : null),
            };
            var R = g.RGRaw;
            g.Body = Segs(R, "body"); g.Skull = Segs(R, "skull"); g.Muzzle = Segs(R, "muzzle"); g.Nose = Segs(R, "nose");
            g.NoseHL = Segs(R, "noseHL"); g.Stripes = Segs(R, "stripes"); g.Back = Segs(R, "back");
            if (R.TryGetValue("wh", out var wh) && wh is List<object> whl) foreach (var w in whl) g.Wh.Add(SegList(w as List<object>));
            var feet = (List<object>)R["feet"];
            g.Feet = new (double, double)[feet.Count];
            for (int i = 0; i < feet.Count; i++) { var f = (Dictionary<string, object>)feet[i]; g.Feet[i] = (Num(f["x"]), Num(f["y"])); }
            var ears = (List<object>)R["ears"];
            g.Ears = new RigEar[ears.Count];
            for (int i = 0; i < ears.Count; i++)
            {
                var e = (Dictionary<string, object>)ears[i];
                var E = new RigEar
                {
                    Side = Num(e["side"]), Pivot = Arr(e["pivot"]),
                    O = e.TryGetValue("o", out var o) ? SegList(o as List<object>) : null,
                    N = e.TryGetValue("n", out var n) ? SegList(n as List<object>) : null,
                    Ux = e.TryGetValue("ux", out var ux) ? Num(ux) : double.NaN, Uy = e.TryGetValue("uy", out var uy) ? Num(uy) : double.NaN,
                    Flop = Truthy(e.TryGetValue("flop", out var fl) ? fl : null), Behind = Truthy(e.TryGetValue("behind", out var be) ? be : null),
                    Lobe = e.TryGetValue("lobe", out var lo) && lo is List<object> ? Arr(lo) : null,
                };
                if (e.TryGetValue("S", out var so) && so is Dictionary<string, object> S)
                {
                    E.Amax = Num(S["amax"]); E.Nx = Num(S["nx"]); E.Ny = Num(S["ny"]);
                    var secs = (List<object>)S["secs"];
                    E.Secs = new RigEarSec[secs.Count];
                    for (int k = 0; k < secs.Count; k++)
                    {
                        var q = (Dictionary<string, object>)secs[k];
                        E.Secs[k] = new RigEarSec { S = Num(q["s"]), Cx = Num(q["cx"]), Cy = Num(q["cy"]), Hw = Num(q["hw"]), Hd = Num(q["hd"]) };
                    }
                }
                g.Ears[i] = E;
            }
            var arms = (List<object>)R["arms"];
            g.Arms = new RigArm[arms.Count];
            for (int i = 0; i < arms.Count; i++) { var a = (Dictionary<string, object>)arms[i]; g.Arms[i] = new RigArm { Side = Num(a["side"]), E = Arr(a["e"]), Pivot = Arr(a["pivot"]) }; }
            if (R.TryGetValue("tail", out var tl) && tl is List<object> tll)
            {
                g.Tail = new double[tll.Count][];
                for (int i = 0; i < tll.Count; i++) g.Tail[i] = Arr(tll[i]);
            }
            return g;
        }

        // ---- reading D the way the game's JS does ----
        /// <summary>D.k as a number (NaN when missing, like undefined in arithmetic).</summary>
        public double N(string k) => D.TryGetValue(k, out var v) ? Num(v) : double.NaN;
        /// <summary>D.k != null ? D.k : null</summary>
        public double? Opt(string k) => D.TryGetValue(k, out var v) && v != null ? Num(v) : (double?)null;
        /// <summary>D.k || def (for numbers: 0 and NaN fall back too)</summary>
        public double Or(string k, double def) { double v = N(k); return v != 0 && !double.IsNaN(v) ? v : def; }
        /// <summary>D.k as a number array, or null.</summary>
        public double[] A(string k) => D.TryGetValue(k, out var v) && v is List<object> l ? Arr(l) : null;
        /// <summary>!!D.k</summary>
        public bool Has(string k) => D.TryGetValue(k, out var v) && Truthy(v);
        public object Raw(string k) => D.TryGetValue(k, out var v) ? v : null;

        public static bool Truthy(object v) => v != null && !(v is bool b && !b) && !(v is double d && (d == 0 || double.IsNaN(d))) && !(v is string s && s.Length == 0);
        public static double Num(object v) => v is double d ? d : v is bool b ? (b ? 1 : 0) : v == null ? 0 : double.NaN;
        public static double[] Arr(object v)
        {
            var l = (List<object>)v; var a = new double[l.Count];
            for (int i = 0; i < a.Length; i++) a[i] = l[i] is double d ? d : double.NaN;
            return a;
        }

        static List<RigSeg> Segs(Dictionary<string, object> R, string k) => R.TryGetValue(k, out var v) && v is List<object> l ? SegList(l) : null;

        public static List<RigSeg> SegList(List<object> l)
        {
            if (l == null) return null;
            var o = new List<RigSeg>(l.Count);
            foreach (Dictionary<string, object> s in l)
            {
                char c = ((string)s["c"])[0];
                if (!s.TryGetValue("p", out var pv) || !(pv is List<object> pl)) { o.Add(new RigSeg(c)); continue; }
                var p = new double[pl.Count * 2];
                for (int i = 0; i < pl.Count; i++) { var q = (List<object>)pl[i]; p[2 * i] = Num(q[0]); p[2 * i + 1] = Num(q[1]); }
                o.Add(new RigSeg(c, p));
            }
            return o;
        }
    }
}
