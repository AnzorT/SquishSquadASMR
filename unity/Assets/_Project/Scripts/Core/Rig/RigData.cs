// The creature rig's data, as webgame/rive-rig.js (and the move files that add to it) has it: every move (STATES) as
// keyframe tracks per pose channel, the rest pose, the idle fidgets, each creature's squeeze and let-go reactions, the
// particle presets (PRE).
// Exported from the running game by tools/unity-port/rig/record-rig.mjs (rig-states.json); nothing is retyped here.
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    /// <summary>One channel's keyframes: [ms, value, ease]. The ease belongs to the keyframe it leads to (kv()).</summary>
    public sealed class RigTrack
    {
        public double[] T, V;
        public string[] E;
    }

    /// <summary>Where a particle starts (emit's w): a point the rig drew last frame (I.W: 'head', 'footL'...) or a fixed one.</summary>
    public struct RigWhere { public string Name; public bool At; public double X, Y; }

    /// <summary>An emit: a PRE name, where, how many (0: the preset's own n).</summary>
    public sealed class RigEmit { public string Pre; public RigWhere W; public double N; public double T0, T1, Every; }

    /// <summary>A particle preset (rive-rig.js PRE and the entries other files add): count, spread, speed, gravity,
    /// life (ms), size range, colours, kind (dot, drop, bubble, star, heart, wave, wisp, leaf, duck, haze, puff, seed, note).</summary>
    public sealed class RigPre
    {
        public double N, Sp, Vx, Vy, G = double.NaN, Life = double.NaN, R0, R1;
        public string[] Col;   // null: none (white)
        public string Kind;
        public bool Out, Hop;
    }

    public sealed class RigEvent { public double T; public string Name; public double X, Y; public RigEmit Emit; }

    public sealed class RigState
    {
        public string Name;
        public double Loop, Dur, Blend;           // 0 = not set
        public bool Look, NoBlink, Whip;
        public double? Soft, Stretch, Breathe, PropZ;
        public string Next;
        public List<string> Seq;
        public List<RigEvent> Ev;
        public double[] Tap;
        public List<RigEmit> Emit;   // emit windows: [t0, t1, every ms, PRE name, where, n]
        public List<RigEmit> Em;     // a let-go reaction's own particles (REL: [name, where, n])
        public readonly List<KeyValuePair<string, RigTrack>> K = new List<KeyValuePair<string, RigTrack>>();
    }

    public sealed class RigData
    {
        public readonly Dictionary<string, RigState> States = new Dictionary<string, RigState>();
        public readonly List<KeyValuePair<string, double>> Rest = new List<KeyValuePair<string, double>>();
        public readonly List<string> Fidgets = new List<string>();
        public readonly Dictionary<string, List<string>> Fidk = new Dictionary<string, List<string>>();
        public readonly Dictionary<string, List<KeyValuePair<string, double>>> Press = new Dictionary<string, List<KeyValuePair<string, double>>>();
        public readonly Dictionary<string, RigState> Rel = new Dictionary<string, RigState>();
        public RigState Release;
        public readonly HashSet<string> Soft = new HashSet<string>(), Hyaw = new HashSet<string>();
        public readonly Dictionary<string, RigPre> Pre = new Dictionary<string, RigPre>();

        public RigState State(string name) => name != null && States.TryGetValue(name, out var s) ? s : null;

        public static RigData FromJson(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var d = new RigData();
            foreach (var kv in (Dictionary<string, object>)root["STATES"]) d.States[kv.Key] = ReadState(kv.Key, kv.Value as Dictionary<string, object>);
            foreach (var kv in (Dictionary<string, object>)root["REST"]) d.Rest.Add(new KeyValuePair<string, double>(kv.Key, Num(kv.Value)));
            foreach (var f in (List<object>)root["FIDGETS"]) d.Fidgets.Add((string)f);
            if (root.TryGetValue("FIDK", out var fk) && fk is Dictionary<string, object> fkd)
                foreach (var kv in fkd) { var l = new List<string>(); foreach (var f in (List<object>)kv.Value) l.Add((string)f); d.Fidk[kv.Key] = l; }
            if (root.TryGetValue("PRESS", out var pr) && pr is Dictionary<string, object> prd)
                foreach (var kv in prd)
                {
                    var l = new List<KeyValuePair<string, double>>();
                    foreach (var c in (Dictionary<string, object>)kv.Value) l.Add(new KeyValuePair<string, double>(c.Key, Num(c.Value)));
                    d.Press[kv.Key] = l;
                }
            if (root.TryGetValue("REL", out var rl) && rl is Dictionary<string, object> rld)
                foreach (var kv in rld) d.Rel[kv.Key] = ReadState("REL." + kv.Key, kv.Value as Dictionary<string, object>);
            if (root.TryGetValue("RELEASE", out var re) && re is Dictionary<string, object> red) d.Release = ReadState("RELEASE", red);
            foreach (var s in (List<object>)root["SOFT"]) d.Soft.Add((string)s);
            foreach (var s in (List<object>)root["HYAW"]) d.Hyaw.Add((string)s);
            if (root.TryGetValue("PRE", out var pre) && pre is Dictionary<string, object> pd)
                foreach (var kv in pd) if (kv.Value is Dictionary<string, object> q) d.Pre[kv.Key] = ReadPre(q);
            return d;
        }

        public static RigState ReadState(string name, Dictionary<string, object> o)
        {
            var s = new RigState { Name = name };
            if (o == null) return s;
            if (o.TryGetValue("loop", out var v)) s.Loop = Num(v);
            if (o.TryGetValue("dur", out v)) s.Dur = Num(v);
            if (o.TryGetValue("blend", out v)) s.Blend = Num(v);
            if (o.TryGetValue("look", out v)) s.Look = Truthy(v);
            if (o.TryGetValue("noBlink", out v)) s.NoBlink = Truthy(v);
            if (o.TryGetValue("whip", out v)) s.Whip = Truthy(v);
            if (o.TryGetValue("soft", out v) && v != null) s.Soft = Num(v);
            if (o.TryGetValue("stretch", out v) && v != null) s.Stretch = Num(v);
            if (o.TryGetValue("breathe", out v) && v != null) s.Breathe = Num(v);
            if (o.TryGetValue("propZ", out v) && v != null) s.PropZ = Num(v);
            if (o.TryGetValue("next", out v)) s.Next = v as string;
            if (o.TryGetValue("seq", out v) && v is List<object> seq) { s.Seq = new List<string>(); foreach (var x in seq) s.Seq.Add((string)x); }
            if (o.TryGetValue("tap", out v) && v is List<object> tap && tap.Count >= 2) s.Tap = new[] { Num(tap[0]), Num(tap[1]) };
            if (o.TryGetValue("ev", out v) && v is List<object> ev)
            {
                s.Ev = new List<RigEvent>();
                foreach (List<object> e in ev)
                {
                    var r = new RigEvent { T = Num(e[0]), Name = e[1] as string, X = e.Count > 2 ? NumOr(e[2]) : 0, Y = e.Count > 3 ? NumOr(e[3]) : 0 };
                    if (r.Name == "emit") r.Emit = new RigEmit { Pre = e.Count > 2 ? e[2] as string : null, W = e.Count > 3 ? Where(e[3]) : default, N = e.Count > 4 ? NumOr(e[4]) : 0 };
                    s.Ev.Add(r);
                }
            }
            if (o.TryGetValue("emit", out v) && v is List<object> em)
            {
                s.Emit = new List<RigEmit>();
                foreach (List<object> m in em)
                    s.Emit.Add(new RigEmit { T0 = Num(m[0]), T1 = Num(m[1]), Every = Num(m[2]), Pre = m.Count > 3 ? m[3] as string : null, W = m.Count > 4 ? Where(m[4]) : default, N = m.Count > 5 ? NumOr(m[5]) : 0 });
            }
            if (o.TryGetValue("em", out v) && v is List<object> rm)
            {
                s.Em = new List<RigEmit>();
                foreach (List<object> m in rm)
                    s.Em.Add(new RigEmit { Pre = m.Count > 0 ? m[0] as string : null, W = m.Count > 1 ? Where(m[1]) : default, N = m.Count > 2 ? NumOr(m[2]) : 0 });
            }
            if (o.TryGetValue("k", out v) && v is Dictionary<string, object> k)
                foreach (var ch in k) s.K.Add(new KeyValuePair<string, RigTrack>(ch.Key, ReadTrack((List<object>)ch.Value)));
            return s;
        }

        /// <summary>emit's w: [x, y] or a name in I.W (anything else: neither, as undefined is).</summary>
        public static RigWhere Where(object v)
        {
            if (v is List<object> a) return new RigWhere { At = true, X = a.Count > 0 ? Num(a[0]) : double.NaN, Y = a.Count > 1 ? Num(a[1]) : double.NaN };
            return new RigWhere { Name = v as string };
        }

        static RigPre ReadPre(Dictionary<string, object> q)
        {
            var p = new RigPre();
            object v;
            if (q.TryGetValue("n", out v)) p.N = NumOr(v);
            if (q.TryGetValue("sp", out v)) p.Sp = Num(v); else p.Sp = double.NaN;
            if (q.TryGetValue("vx", out v)) p.Vx = NumOr(v);   // (q.vx || 0)
            if (q.TryGetValue("vy", out v)) p.Vy = NumOr(v);
            if (q.TryGetValue("g", out v)) p.G = Num(v);
            if (q.TryGetValue("life", out v)) p.Life = Num(v);
            if (q.TryGetValue("r", out v) && v is List<object> r && r.Count > 1) { p.R0 = Num(r[0]); p.R1 = Num(r[1]); } else { p.R0 = p.R1 = double.NaN; }
            if (q.TryGetValue("col", out v) && v is List<object> c) { p.Col = new string[c.Count]; for (int i = 0; i < c.Count; i++) p.Col[i] = c[i] as string; }
            if (q.TryGetValue("kind", out v)) p.Kind = v as string;
            if (q.TryGetValue("out", out v)) p.Out = Truthy(v);
            if (q.TryGetValue("hop", out v)) p.Hop = Truthy(v);
            return p;
        }

        static RigTrack ReadTrack(List<object> keys)
        {
            var t = new RigTrack { T = new double[keys.Count], V = new double[keys.Count], E = new string[keys.Count] };
            for (int i = 0; i < keys.Count; i++)
            {
                var k = (List<object>)keys[i];
                t.T[i] = Num(k[0]); t.V[i] = Num(k[1]);
                t.E[i] = k.Count > 2 ? k[2] as string : null;
            }
            return t;
        }

        static double Num(object v) => v is double d ? d : v is bool b ? (b ? 1 : 0) : v == null ? 0 : double.NaN;
        static double NumOr(object v) => v is double d ? d : 0;
        static bool Truthy(object v) => v is bool b ? b : v is double d ? d != 0 && !double.IsNaN(d) : v != null;
    }
}
