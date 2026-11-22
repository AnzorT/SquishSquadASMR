// dotnet run -- <rig-states.json> <trace.json>: replays the recorded frames through RigEngine and compares every
// pose channel and the drawing's extra outputs. Exit code 1 on any difference beyond 1e-7.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlushCrush.Core.Rig;

static class Program
{
    static int Main(string[] args)
    {
        var data = RigData.FromJson(File.ReadAllText(args[0]));
        var trace = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(args[1]));
        string key = (string)trace["key"];
        double t0 = (double)trace["t0"], fps = (double)trace["fps"];
        var script = ((List<object>)trace["script"]).Cast<List<object>>().ToDictionary(e => (int)(double)e[0], e => (string)e[1]);
        var frames = ((List<object>)trace["frames"]).Cast<Dictionary<string, object>>().ToList();
        var rnd = new RigRandom(0x1f2e3d4c);
        int draws = 0;
        var R = new RigEngine(data, key, t0, () => { draws++; return rnd.Next(); }) { Generic = false };
        int bad = 0; double worst = 0; string worstAt = "";
        for (int i = 0; i < frames.Count; i++)
        {
            var fr = frames[i]; int f = (int)(double)fr["f"];
            double now = f == 0 ? t0 : t0 + f * 1000 / fps, dt = f == 0 ? 0.016 : 1 / fps;
            if (script.TryGetValue(f, out var w))
            {
                if (w.StartsWith("play:")) R.Play(w.Substring(5), now);
                else if (w == "press") { if (R.CurName != "idle") R.SetState("idle", now); R.Down = true; R.Drag = false; R.Press = true; R.PressT = now; }
                else if (w == "release") { R.Press = false; R.Shots.Add(new RigShot { S = data.Rel.TryGetValue(key, out var rs) ? rs : data.Release, T0 = now }); R.Down = false; }
                else if (w.StartsWith("drag:")) { if (!R.Drag) { R.Down = true; R.Drag = true; R.SpinTw = null; R.Spin0 = R.SpinOff; } R.SpinOff = R.Spin0 + double.Parse(w.Substring(5)) / 169 * 360; }
                else if (w == "dragEnd") { R.SpinTw = new RigEngine.SpinTween { From = R.SpinOff, To = RigMath.Round(R.SpinOff / 360) * 360, T0 = now, D = 650 }; R.Down = false; R.Drag = false; }
            }
            var P = R.Tick(now, dt);
            var J = fr["P"] as Dictionary<string, object>;
            if (J == null) { Console.WriteLine($"frame {f}: no pose recorded"); continue; }
            void Cmp(string what, double mine, object theirs)
            {
                double t = theirs is double d ? d : double.NaN;
                bool bothNaN = double.IsNaN(mine) && double.IsNaN(t);
                double diff = bothNaN ? 0 : Math.Abs(mine - t);
                if (double.IsNaN(diff)) diff = double.PositiveInfinity;
                if (diff > worst) { worst = diff; worstAt = $"frame {f} ({R.CurName}) {what}: mine {mine} game {t}"; }
                if (diff > 1e-7 && bad++ < 12) Console.WriteLine($"frame {f} ({R.CurName}) {what}: mine {mine} game {t}");
            }
            foreach (var kv in J) Cmp(kv.Key, P[kv.Key], kv.Value);
            foreach (var k in P.V.Keys) if (!J.ContainsKey(k) && bad++ < 12) Console.WriteLine($"frame {f}: channel {k} only in mine");
            if (fr.TryGetValue("rr", out var rr) && (int)(double)rr != draws && bad++ < 12) Console.WriteLine($"frame {f}: {draws} random draws so far, the game {rr}");
            Cmp("hs", R.Hs, fr["hs"]); Cmp("ed", R.Ed, fr["ed"]); Cmp("ld", R.Ld, fr["ld"]);
            Cmp("spTw", R.SpTw, fr["spTw"]); Cmp("spF", R.SpF, fr["spF"]); Cmp("spinOff", R.SpinOff, fr["spinOff"]);
            if ((string)fr["state"] != R.CurName && bad++ < 12) Console.WriteLine($"frame {f}: state mine {R.CurName} game {fr["state"]}");
            var ms = fr["ms"] as Dictionary<string, object>;
            if ((ms == null) != (R.Ms == null)) { if (bad++ < 12) Console.WriteLine($"frame {f}: stretch {(R.Ms == null ? "missing in mine" : "only in mine")}"); }
            else if (ms != null) { Cmp("ms.s", R.Ms.S, ms["s"]); Cmp("ms.c", R.Ms.C, ms["c"]); Cmp("ms.n", R.Ms.N, ms["n"]); }
        }
        Console.WriteLine($"{frames.Count} frames, {bad} differences beyond 1e-7; largest: {worst:E2} at {worstAt}");
        return bad > 0 ? 1 : 0;
    }
}
