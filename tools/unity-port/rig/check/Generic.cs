// The check for a creature of the generic rig (record-generic.mjs): RigGeneric draws it; besides the attributes (DrawCheck)
// it compares where things are each frame (I.W: what the particles, props and light read), the DOM moves render() makes
// (a parent's children, by name) and counts frames that needed a branch not ported yet.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlushCrush.Core.Rig;

sealed class Generic
{
    readonly RigGeneric _g;
    int _bad, _wN, _wBad, _missing, _domFrames; double _wWorst; string _wWorstAt, _missWhat;
    readonly Dictionary<string, string> _dom = new Dictionary<string, string>();

    public Generic(RigGeneric g) { _g = g; }

    /// <summary>The renderer for a generic geometry, with the creature's own moves (rig-states-<key>.json beside the
    /// geometry: the ones the mount adds, which the shared rig-states.json misses) merged into the data.</summary>
    public static RigGeneric Make(RigData data, Dictionary<string, object> geoRoot, string geoPath, string key)
    {
        var own = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(geoPath)), $"rig-states-{key}.json");
        if (File.Exists(own))
        {
            var o = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(own)); int n = 0;
            foreach (var kv in (Dictionary<string, object>)o["STATES"]) { data.States[kv.Key] = RigData.ReadState(kv.Key, kv.Value as Dictionary<string, object>); n++; }
            Console.WriteLine($"{key}'s own moves: {n} (rig-states-{key}.json)");
        }
        else Console.WriteLine($"warning: no rig-states-{key}.json beside the geometry (the moves the mount adds are missing)");
        var geo = RigGenericGeo.From(geoRoot);
        RigGeneric.AllowPartial = Environment.GetEnvironmentVariable("RIG_PARTIAL") == "1";
        if (RigGeneric.Unported(geo) is string why) Console.WriteLine((RigGeneric.AllowPartial ? "partial: " : "") + "not ported yet: " + why);
        return new RigGeneric(geo, key);
    }

    /// <summary>What one frame costs on this machine (RIG_PERF=1): tick, outlines, the draw list, and its size.</summary>
    public static void Perf(RigData data, string key, Dictionary<string, object> geoRoot, double t0, double fps)
    {
        var rnd = new RigRandom(7); var G = new RigGeneric(RigGenericGeo.From(geoRoot), key);
        var E = new RigEngine(data, key, t0, () => rnd.Next()) { Generic = true, FromM = G.FromM };
        var D = new RigDisplay(RigTree.FromTree((Dictionary<string, object>)geoRoot["tree"], G.All), null) { PxPerUnit = 4.753125f };
        var sw = new System.Diagnostics.Stopwatch(); long tick = 0, render = 0, build = 0; int n = 1200, verts = 0, tris = 0, cmds = 0;
        for (int f = 0; f < n; f++)
        {
            double now = t0 + f * 1000 / fps; if (f == 300) E.Play("hop", now); if (f == 700) E.Play("happy", now);
            sw.Restart(); var P = E.Tick(now, 1 / fps); tick += sw.ElapsedTicks;
            sw.Restart(); G.Render(P, E, now); render += sw.ElapsedTicks;
            sw.Restart(); D.Build(); build += sw.ElapsedTicks;
            if (f >= 200) { cmds += D.Cmds.Count; foreach (var c in D.Cmds) if (c.Mesh != null && c.Kind != RigCmdKind.PopClip) { verts += c.Mesh.NV; tris += c.Mesh.NT / 3; } }
        }
        double ms(long t) => t * 1000.0 / System.Diagnostics.Stopwatch.Frequency / n;
        int m = n - 200;
        Console.WriteLine($"per frame (this machine): tick {ms(tick):F3} ms, outlines {ms(render):F3} ms, draw list {ms(build):F3} ms; {cmds / m} commands, {verts / m} vertices, {tris / m} triangles");
    }

    /// <summary>A pose against the recorded one (the mount's early ticks): differences beyond 1e-7.</summary>
    public static int Pose(string at, RigPose P, Dictionary<string, object> J)
    {
        int bad = 0; if (J == null) return 0;
        foreach (var kv in J)
        {
            double t = kv.Value is double d ? d : double.NaN, m = P[kv.Key];
            if (double.IsNaN(m) && double.IsNaN(t)) continue;
            if (!(Math.Abs(m - t) <= 1e-7)) { if (bad++ < 5) Console.WriteLine($"{at}: {kv.Key} mine {m} game {t}"); }
        }
        return bad;
    }

    public void Frame(int f, string state, DrawCheck dc, Dictionary<string, object> draw, Dictionary<string, object> fr)
    {
        dc.Frame(f, state, _g.All, draw, null, "");
        if (_g.Missing != null) { _missing++; _missWhat = _g.Missing; }
        // I.W, to the bit (not rounded by the game)
        if (fr.TryGetValue("W", out var wo) && wo is Dictionary<string, object> W)
        {
            foreach (var kv in W)
            {
                _wN++;
                var p = (List<object>)kv.Value; double x = (double)p[0], y = (double)p[1];
                if (!_g.W.TryGetValue(kv.Key, out var m)) { _wBad++; dc.Say($"frame {f} ({state}) W.{kv.Key}: not in mine", "W", f); continue; }
                double d = Math.Max(Math.Abs(m.x - x), Math.Abs(m.y - y));
                if (d > _wWorst) { _wWorst = d; _wWorstAt = $"frame {f} W.{kv.Key}"; }
                if (!(d <= 1e-9)) { _wBad++; dc.Say($"frame {f} ({state}) W.{kv.Key}: mine ({m.x}, {m.y}), the game ({x}, {y})", "W", f); }
            }
            foreach (var k in _g.W.Keys) if (!W.ContainsKey(k)) { _wBad++; dc.Say($"frame {f} ({state}) W.{k}: only in mine", "W", f); }
        }
        // the DOM moves (none ported yet: any is a difference)
        if (fr.TryGetValue("dom", out var dm) && dm is Dictionary<string, object> dom && dom.Count > 0)
        {
            _domFrames++;
            foreach (var kv in dom) { var s = string.Join(",", ((List<object>)kv.Value).Cast<string>()); if (!_dom.TryGetValue(kv.Key, out var l) || l != s) { _dom[kv.Key] = s; _bad++; dc.Say($"frame {f} ({state}) {kv.Key}: the game reordered its children ({s}), not ported", kv.Key, f); } }
        }
    }

    public int Report()
    {
        Console.WriteLine($"where things are (I.W): {_wN} points, {_wBad} differences (largest {_wWorst:E2}{(_wWorstAt != null ? " at " + _wWorstAt : "")})");
        if (_domFrames > 0) Console.WriteLine($"DOM moves: {_domFrames} frames with the game's elements reordered");
        if (_missing > 0) Console.WriteLine($"not ported: {_missing} frames needed {_missWhat}");
        return _bad + _wBad + _missing;
    }
}

sealed partial class DrawCheck { }

sealed partial class Shots
{
    readonly RigGeneric _gn;

    public static Shots Make(Dictionary<string, object> s, Dictionary<string, object> geoRoot, RigGeneric G, string dir, string key, string fabric)
    {
        try { return new Shots(s, geoRoot, G, dir, key, fabric); }
        catch (NotSupportedException e) { Console.WriteLine("pixels: not drawn (" + e.Message + ")"); return null; }
    }

    Shots(Dictionary<string, object> s, Dictionary<string, object> geoRoot, RigGeneric G, string dir, string key, string fabric)
    {
        _dir = dir; _key = key; _gn = G;
        double dsf = (double)s["dsf"]; var host = (Dictionary<string, object>)s["host"]; var clip = (Dictionary<string, object>)s["clip"];
        double hs = (double)host["s"];
        float k = (float)(hs / 100 * dsf), ox = (float)(((double)host["x"] - (double)clip["x"]) * dsf), oy = (float)(((double)host["y"] - (double)clip["y"]) * dsf);
        _w = (int)Math.Round((double)clip["width"] * dsf); _h = (int)Math.Round((double)clip["height"] * dsf);
        float[] grain = null; int gw = 0, gh = 0;
        if (File.Exists(fabric)) { var px = RigRaster.LoadPng(fabric, out gw, out gh); grain = new float[gw * gh]; for (int i = 0; i < grain.Length; i++) grain[i] = px[i * 4 + 3] / 255f; }
        _d = new RigDisplay(RigTree.FromTree((Dictionary<string, object>)geoRoot["tree"], G.All), null) { PxPerUnit = k, Fx = _fx };
        _r = new RigRaster(_w, _h, k, ox, oy, grain, gw, gh);
    }

    public void Frame(int f, RigEngine E, double now)
    {
        _fx.Build(E.CurName, now - E.CurT0, _gn.TopW);
        Compare(f);
    }
}
