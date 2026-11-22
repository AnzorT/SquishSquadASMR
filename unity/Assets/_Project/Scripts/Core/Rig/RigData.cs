// The creature rig's data, as webgame/rive-rig.js (and the move files that add to it) has it: every move (STATES) as
// keyframe tracks per pose channel, the rest pose, the idle fidgets, each creature's squeeze and let-go reactions.
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

    public sealed class RigEvent { public double T; public string Name; public double X, Y; }

    public sealed class RigState
    {
        public string Name;
        public double Loop, Dur, Blend;           // 0 = not set
        public bool Look, NoBlink, Whip;
        public double? Soft, Stretch, Breathe;
        public string Next;
        public List<string> Seq;
        public List<RigEvent> Ev;
        public double[] Tap;
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
            if (o.TryGetValue("next", out v)) s.Next = v as string;
            if (o.TryGetValue("seq", out v) && v is List<object> seq) { s.Seq = new List<string>(); foreach (var x in seq) s.Seq.Add((string)x); }
            if (o.TryGetValue("tap", out v) && v is List<object> tap && tap.Count >= 2) s.Tap = new[] { Num(tap[0]), Num(tap[1]) };
            if (o.TryGetValue("ev", out v) && v is List<object> ev)
            {
                s.Ev = new List<RigEvent>();
                foreach (List<object> e in ev)
                    s.Ev.Add(new RigEvent { T = Num(e[0]), Name = e[1] as string, X = e.Count > 2 ? NumOr(e[2]) : 0, Y = e.Count > 3 ? NumOr(e[3]) : 0 });
            }
            if (o.TryGetValue("k", out v) && v is Dictionary<string, object> k)
                foreach (var ch in k) s.K.Add(new KeyValuePair<string, RigTrack>(ch.Key, ReadTrack((List<object>)ch.Value)));
            return s;
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
