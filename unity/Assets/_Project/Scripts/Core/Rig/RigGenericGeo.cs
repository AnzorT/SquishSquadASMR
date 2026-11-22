// A creature drawn by the generic rig (webgame rive-generic.js), as tools/unity-port/rig/record-generic.mjs exports it
// (geo-<key>.json): what setup() worked out from the creature's art and left on the rig for render() to read (G: the
// geometry, the art's shapes with their roles and outlines, the per-creature kits), the creature's ROUND flags (RC), and
// the drawing's element tree as setup() left it. setup() isn't ported: this is its result, as data.
//
// In G, elements are {$el: name}, an object met a second time is {$ref: path} (resolved here to the same object, so
// identity holds as in the game), NaN / Infinity are {$num}, undefined array entries {$undef}, functions {$fn} (ported
// as code: RigGeneric).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlushCrush.Core.Rig
{
    public sealed class RigGenericGeo
    {
        public Dictionary<string, object> Root, G, RC, Tree;
        public string Key;

        public static bool IsGeneric(Dictionary<string, object> root) => root.TryGetValue("generic", out var g) && g is bool b && b;

        public static RigGenericGeo FromJson(string json) => From((Dictionary<string, object>)MiniJson.Parse(json));

        public static RigGenericGeo From(Dictionary<string, object> root)
        {
            if (!IsGeneric(root)) throw new FormatException("not a generic rig's geometry (record-generic.mjs)");
            var g = new RigGenericGeo { Root = root, Key = root["key"] as string, G = (Dictionary<string, object>)root["G"], Tree = root["tree"] as Dictionary<string, object> };
            g.RC = root.TryGetValue("RC", out var rc) ? rc as Dictionary<string, object> : null;
            g.Resolve(g.G);
            return g;
        }

        // ---- {$ref: path}: the object first met at that path (from G's root, keys and indexes joined by '.') ----
        object Find(string path)
        {
            object o = G;
            foreach (var k in path.Split('.'))
            {
                if (o is Dictionary<string, object> d) o = d.TryGetValue(k, out var v) ? v : null;
                else if (o is List<object> l && int.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i < l.Count) o = l[i];
                else return null;
                if (o is Dictionary<string, object> r && r.Count == 1 && r.TryGetValue("$ref", out var rp)) o = Find((string)rp);
            }
            return o;
        }

        void Resolve(object o)
        {
            if (o is Dictionary<string, object> d)
            {
                List<string> keys = null;
                foreach (var kv in d) if (IsRef(kv.Value)) (keys ?? (keys = new List<string>())).Add(kv.Key);
                if (keys != null) foreach (var k in keys) d[k] = Find((string)((Dictionary<string, object>)d[k])["$ref"]);
                foreach (var kv in d) if (!(keys != null && keys.Contains(kv.Key))) Resolve(kv.Value);
            }
            else if (o is List<object> l)
            {
                for (int i = 0; i < l.Count; i++) { if (IsRef(l[i])) l[i] = Find((string)((Dictionary<string, object>)l[i])["$ref"]); else Resolve(l[i]); }
            }
        }

        static bool IsRef(object v) => v is Dictionary<string, object> r && r.Count == 1 && r.ContainsKey("$ref");

        // ---- reading G the way the game's JS does ----
        /// <summary>o[k] (null when missing).</summary>
        public static object Get(object o, string k) => o is Dictionary<string, object> d && d.TryGetValue(k, out var v) ? v : null;
        public static Dictionary<string, object> Obj(object o, string k) => Get(o, k) as Dictionary<string, object>;
        public static List<object> List(object o, string k) => Get(o, k) as List<object>;

        /// <summary>A value as a JS number in arithmetic (undefined -> NaN, null -> 0, true -> 1, "0.8" -> 0.8).</summary>
        public static double Num(object v)
        {
            switch (v)
            {
                case null: return 0;
                case double d: return d;
                case bool b: return b ? 1 : 0;
                case string s: { s = s.Trim(); if (s.Length == 0) return 0; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : double.NaN; }
                case Dictionary<string, object> m when m.TryGetValue("$num", out var n):
                    return (string)n == "Infinity" ? double.PositiveInfinity : (string)n == "-Infinity" ? double.NegativeInfinity : double.NaN;
                case Dictionary<string, object> m when m.ContainsKey("$undef"): return double.NaN;
                default: return double.NaN;
            }
        }

        /// <summary>o[k] as a number: missing (undefined) -> NaN.</summary>
        public static double N(object o, string k) => o is Dictionary<string, object> d && d.TryGetValue(k, out var v) ? Num(v) : double.NaN;

        /// <summary>JS truthiness.</summary>
        public static bool Truthy(object v)
        {
            switch (v)
            {
                case null: return false;
                case bool b: return b;
                case double d: return d != 0 && !double.IsNaN(d);
                case string s: return s.Length > 0;
                case Dictionary<string, object> m when m.ContainsKey("$undef"): return false;
                case Dictionary<string, object> m when m.TryGetValue("$num", out var n): return (string)n != "NaN";
                default: return true;
            }
        }

        public static bool Has(object o, string k) => Truthy(Get(o, k));

        /// <summary>An element reference ({$el: name}) -> its name.</summary>
        public static string El(object v) => v is Dictionary<string, object> m && m.TryGetValue("$el", out var n) ? (string)n : null;

        /// <summary>parsePath()'s segments as exported ([{c: 'M', p: [[x, y]]}, ...]) -> RigSegs.</summary>
        public static List<RigSeg> Segs(object v)
        {
            if (!(v is List<object> l)) return null;
            var o = new List<RigSeg>(l.Count);
            foreach (Dictionary<string, object> s in l)
            {
                var c = ((string)s["c"])[0];
                var pts = s.TryGetValue("p", out var pv) && pv is List<object> pl ? pl : null;
                var p = new double[pts == null ? 0 : pts.Count * 2];
                for (int i = 0; pts != null && i < pts.Count; i++) { var xy = (List<object>)pts[i]; p[2 * i] = Num(xy[0]); p[2 * i + 1] = Num(xy[1]); }
                o.Add(new RigSeg(c, p));
            }
            return o;
        }

        /// <summary>The names of every element in the drawing's tree, in document order.</summary>
        public List<string> Names()
        {
            var o = new List<string>();
            void walk(Dictionary<string, object> n)
            {
                if (n.TryGetValue("n", out var nm) && nm is string s) o.Add(s);
                if (n.TryGetValue("c", out var c) && c is List<object> cl) foreach (Dictionary<string, object> k in cl) walk(k);
            }
            if (Tree != null) walk(Tree);
            return o;
        }
    }
}
