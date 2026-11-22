// dotnet run -- <rig-states.json> <trace.json> [<geo.json>]: replays the recorded frames through RigEngine and compares
// every pose channel and the drawing's extra outputs (exit code 1 on any difference beyond 1e-7); with the creature's
// geometry, also draws each frame with RigRender and compares every attribute the game wrote to its drawing and the
// parts' stacking order (path numbers: the game rounds to hundredths, so a value a hair from a half can round the other
// way - those are counted apart; anything further off is a difference).
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
        RigRender Rd = args.Length > 2 ? new RigRender(RigGeo.FromJson(File.ReadAllText(args[2])), key) : null;
        var dc = new DrawCheck();
        int dbgFrame = Environment.GetEnvironmentVariable("HULL_FRAME") is string hf ? int.Parse(hf) : -1, curF = 0;
        if (Rd != null && dbgFrame >= 0) Rd.EarPointsHook = (ear, pts) =>
        {
            if (curF != dbgFrame) return;
            var P0 = pts.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            double cr((double x, double y) o, (double x, double y) a, (double x, double y) b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            var lo = new List<(double x, double y)>();
            Console.WriteLine($"ear {ear}: {pts.Count} points");
            foreach (var p in P0) { while (lo.Count >= 2) { double c = cr(lo[lo.Count - 2], lo[lo.Count - 1], p); if (Math.Abs(c) < 1e-6) Console.WriteLine($"  lower: near-zero cross {c:E3} at ({p.x:R}, {p.y:R})"); if (c <= 0) lo.RemoveAt(lo.Count - 1); else break; } lo.Add(p); }
            var up = new List<(double x, double y)>();
            for (int i = P0.Count - 1; i >= 0; i--) { var p = P0[i]; while (up.Count >= 2) { double c = cr(up[up.Count - 2], up[up.Count - 1], p); if (Math.Abs(c) < 1e-6) Console.WriteLine($"  upper: near-zero cross {c:E3} at ({p.x:R}, {p.y:R})"); if (c <= 0) up.RemoveAt(up.Count - 1); else break; } up.Add(p); }
            for (int i = 1; i < P0.Count; i++) if (Math.Abs(P0[i].x - P0[i - 1].x) < 1e-9) Console.WriteLine($"  same x: ({P0[i - 1].x:R}, {P0[i - 1].y:R}) ({P0[i].x:R}, {P0[i].y:R})");
        };
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
            curF = f;
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
            if (Rd != null && f > 0 && fr.TryGetValue("draw", out var dw) && dw is Dictionary<string, object> draw)
            {
                Rd.Render(P, R, now);
                dc.Frame(f, R.CurName, Rd, draw, fr.TryGetValue("order", out var od) ? od as string : null);
            }
        }
        if (Rd != null) dc.Report();
        if (Rd != null && dc.Bad > 0) bad += dc.Bad;
        Console.WriteLine($"{frames.Count} frames, {bad} differences beyond 1e-7; largest: {worst:E2} at {worstAt}");
        return bad > 0 ? 1 : 0;
    }
}

/// <summary>Compares RigRender's drawing with the game's recorded attribute writes.</summary>
sealed class DrawCheck
{
    public int Bad, Frames, Attrs, Exact, Flips, Orders, OrderBad;
    public double WorstFlip, WorstTie; public int NearTies; public string WorstTieAt;
    int _shown;
    readonly HashSet<string> _unknown = new HashSet<string>();

    readonly SortedDictionary<string, SortedSet<int>> _badAt = new SortedDictionary<string, SortedSet<int>>();
    int _f; string _el;
    void Say(string m)
    {
        if (_el != null) { if (!_badAt.TryGetValue(_el, out var l)) _badAt[_el] = l = new SortedSet<int>(); l.Add(_f); }
        if (_shown++ < 25) Console.WriteLine(m);
    }

    public void Frame(int f, string state, RigRender R, Dictionary<string, object> draw, string order)
    {
        Frames++; _f = f;
        var mine = R.All.ToDictionary(e => e.Name);
        foreach (var kv in draw)
        {
            _el = kv.Key;
            if (!mine.TryGetValue(kv.Key, out var e)) { if (_unknown.Add(kv.Key)) { Bad++; Say($"frame {f}: the game draws {kv.Key}, mine has no such element"); } continue; }
            var at = (Dictionary<string, object>)kv.Value;
            foreach (var a in at)
            {
                Attrs++;
                string theirs = (string)a.Value;
                int bit = Bit(a.Key);
                if (bit == 0) { Bad++; Say($"frame {f} ({state}) {kv.Key}.{a.Key}: attribute not handled by mine"); continue; }
                if ((e.Written & bit) == 0) { Bad++; Say($"frame {f} ({state}) {kv.Key}.{a.Key}: the game wrote '{Short(theirs)}', mine didn't"); continue; }
                if (a.Key == "d") CmpPath(f, state, kv.Key, e.D, theirs);
                else CmpNum(f, state, kv.Key + "." + a.Key, Val(e, a.Key), theirs);
            }
        }
        foreach (var e in R.All)
            foreach (var an in new[] { "d", "opacity", "stroke-width", "cx", "cy", "rx", "ry" })
                if ((e.Written & Bit(an)) != 0 && !(draw.TryGetValue(e.Name, out var at) && ((Dictionary<string, object>)at).ContainsKey(an)))
                { Bad++; Say($"frame {f} ({state}) {e.Name}.{an}: mine wrote it, the game didn't"); }
        _el = "order";
        if (order != null) { Orders++; if (order != R.OrderKey) { OrderBad++; Bad++; Say($"frame {f} ({state}) order: mine {R.OrderKey}, the game {order}"); } }
    }

    static int Bit(string a) => a == "d" ? RigEl.AD : a == "opacity" ? RigEl.AOpacity : a == "stroke-width" ? RigEl.AStroke
        : a == "cx" ? RigEl.ACx : a == "cy" ? RigEl.ACy : a == "rx" ? RigEl.ARx : a == "ry" ? RigEl.ARy : 0;
    static double Val(RigEl e, string a) => a == "opacity" ? e.Opacity : a == "stroke-width" ? e.StrokeWidth : a == "cx" ? e.Cx : a == "cy" ? e.Cy : a == "rx" ? e.Rx : e.Ry;
    static string Short(string s) => s.Length > 90 ? s.Substring(0, 90) + "…" : s;

    void One(int f, string state, string what, double m, double t)
    {
        double d = Math.Abs(m - t);
        if (d < 1e-9 || (double.IsNaN(m) && double.IsNaN(t))) { Exact++; return; }
        if (d <= 0.0101) { Flips++; if (d > WorstFlip) WorstFlip = d; return; }
        Bad++; Say($"frame {f} ({state}) {what}: mine {RigGeometry.Js(m)}, the game {RigGeometry.Js(t)}");
    }

    void CmpNum(int f, string state, string what, double mine, string theirs)
    {
        double t = double.TryParse(theirs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
        One(f, state, what, mine, t);
    }

    void CmpPath(int f, string state, string name, RigPath mine, string theirs)
    {
        var A = RigGeometry.Tokens(mine.ToString()); var B = RigGeometry.Tokens(theirs);
        if (A.Count != B.Count || !SameNumbers(A, B))
        {
            // the ears' outlines are hulls round ~170 surface points, many of them a hair (1e-15) apart: which of two such
            // points the hull keeps depends on the last bit of sin / pow, which differs between maths libraries (and between
            // phones' WebViews in the game itself). Measured as the drawn curves' largest distance apart instead.
            if (name.StartsWith("ears.") && (name.EndsWith(".o") || name.EndsWith(".clip")))
            {
                double h = Hausdorff(Sample(A), Sample(B));
                NearTies++; if (h > WorstTie) { WorstTie = h; WorstTieAt = $"frame {f} {name}"; }
                if (h > 0.25) { Bad++; Say($"frame {f} ({state}) {name}.d: outlines {h:F3} apart"); }
                return;
            }
        }
        if (A.Count != B.Count) { Bad++; Say($"frame {f} ({state}) {name}.d: {A.Count} tokens in mine, {B.Count} in the game's\n   mine {Short(mine.ToString())}\n   game {Short(theirs)}"); return; }
        for (int i = 0; i < A.Count; i++)
        {
            bool la = char.IsLetter(A[i][0]), lb = char.IsLetter(B[i][0]);
            if (la || lb) { if (A[i] != B[i]) { Bad++; Say($"frame {f} ({state}) {name}.d token {i}: mine {A[i]}, the game {B[i]}"); return; } continue; }
            One(f, state, $"{name}.d #{i}", double.Parse(A[i], System.Globalization.CultureInfo.InvariantCulture), double.Parse(B[i], System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    static bool SameNumbers(List<string> A, List<string> B)
    {
        for (int i = 0; i < A.Count; i++)
        {
            if (char.IsLetter(A[i][0]) || char.IsLetter(B[i][0])) { if (A[i] != B[i]) return false; continue; }
            if (Math.Abs(double.Parse(A[i], System.Globalization.CultureInfo.InvariantCulture) - double.Parse(B[i], System.Globalization.CultureInfo.InvariantCulture)) > 0.0101) return false;
        }
        return true;
    }

    /// <summary>Points along a path of M / L / Q / C / Z (absolute), 24 per curve.</summary>
    static List<(double x, double y)> Sample(List<string> T)
    {
        var o = new List<(double x, double y)>(); int i = 0; char c = 'M'; double x = 0, y = 0, sx = 0, sy = 0;
        double n() => double.Parse(T[i++], System.Globalization.CultureInfo.InvariantCulture);
        while (i < T.Count)
        {
            if (char.IsLetter(T[i][0])) c = T[i++][0];
            if (c == 'Z') { o.Add((sx, sy)); x = sx; y = sy; continue; }
            if (c == 'M' || c == 'L') { x = n(); y = n(); if (c == 'M') { sx = x; sy = y; } o.Add((x, y)); continue; }
            if (c == 'Q') { double ax = n(), ay = n(), bx = n(), by = n(); for (int k = 1; k <= 24; k++) { double t = k / 24.0, m = 1 - t; o.Add((m * m * x + 2 * m * t * ax + t * t * bx, m * m * y + 2 * m * t * ay + t * t * by)); } x = bx; y = by; continue; }
            if (c == 'C') { double ax = n(), ay = n(), bx = n(), by = n(), cx = n(), cy = n(); for (int k = 1; k <= 24; k++) { double t = k / 24.0, m = 1 - t; o.Add((m * m * m * x + 3 * m * m * t * ax + 3 * m * t * t * bx + t * t * t * cx, m * m * m * y + 3 * m * m * t * ay + 3 * m * t * t * by + t * t * t * cy)); } x = cx; y = cy; continue; }
            i++;
        }
        return o;
    }

    static double Hausdorff(List<(double x, double y)> A, List<(double x, double y)> B)
    {
        double one(List<(double x, double y)> P, List<(double x, double y)> Q)
        {
            double worst = 0;
            foreach (var p in P)
            {
                double best = double.MaxValue;
                for (int j = 0; j + 1 < Q.Count; j++)
                {
                    var a = Q[j]; var b = Q[j + 1]; double vx = b.x - a.x, vy = b.y - a.y, L = vx * vx + vy * vy, t = L > 0 ? Math.Max(0, Math.Min(1, ((p.x - a.x) * vx + (p.y - a.y) * vy) / L)) : 0;
                    double dx = a.x + vx * t - p.x, dy = a.y + vy * t - p.y; best = Math.Min(best, dx * dx + dy * dy);
                }
                worst = Math.Max(worst, best);
            }
            return Math.Sqrt(worst);
        }
        return Math.Max(one(A, B), one(B, A));
    }

    public void Report()
    {
        if (NearTies > 0) Console.WriteLine($"ear outlines settled by a near-tie: {NearTies}, the drawn curves at most {WorstTie:F3} apart ({WorstTieAt})");
        foreach (var kv in _badAt) Console.WriteLine($"  {kv.Key}: {kv.Value.Count} frames ({string.Join(" ", kv.Value.Take(30))}{(kv.Value.Count > 30 ? " …" : "")})");
        Console.WriteLine($"drawing: {Frames} frames, {Attrs} attributes; numbers exact {Exact}, a rounding step off {Flips} (largest {WorstFlip:F4}); order {Orders - OrderBad}/{Orders} frames the same; {Bad} differences");
    }
}
