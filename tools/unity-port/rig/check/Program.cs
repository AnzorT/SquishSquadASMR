// dotnet run -- <rig-states.json> <trace.json> [<geo.json>]: replays the recorded frames through RigEngine and compares
// every pose channel and the drawing's extra outputs (exit code 1 on any difference beyond 1e-7); with the creature's
// geometry, also draws each frame with RigRender and compares every attribute the game wrote to its drawing and the
// parts' stacking order (path numbers: the game rounds to hundredths, so a value a hair from a half can round the other
// way - those are counted apart; anything further off is a difference), and the fx layer (particles and the move's
// effect, RigFx) with the markup the game wrote there.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlushCrush.Core.Rig;

static class Program
{
    /// <summary>What one frame costs on this machine: tick, outlines, the draw list (tessellation), and its size.</summary>
    static void Perf(RigData data, string key, RigGeo geo, double t0, double fps)
    {
        var rnd = new RigRandom(7); var E = new RigEngine(data, key, t0, () => rnd.Next());
        var R = new RigRender(geo, key); var D = new RigDisplay(RigTree.FromGeo(geo, R), R) { PxPerUnit = 4.753125f };
        var sw = new System.Diagnostics.Stopwatch(); long tick = 0, render = 0, build = 0; int n = 1200, verts = 0, tris = 0, cmds = 0;
        for (int f = 0; f < n; f++)
        {
            double now = t0 + f * 1000 / fps; if (f == 300) E.Play("hop", now); if (f == 700) E.Play("happy", now);
            sw.Restart(); var P = E.Tick(now, 1 / fps); tick += sw.ElapsedTicks;
            sw.Restart(); R.Render(P, E, now); render += sw.ElapsedTicks;
            sw.Restart(); D.Build(); build += sw.ElapsedTicks;
            if (f >= 200) { cmds += D.Cmds.Count; foreach (var c in D.Cmds) if (c.Mesh != null && c.Kind != RigCmdKind.PopClip) { verts += c.Mesh.NV; tris += c.Mesh.NT / 3; } }
        }
        double ms(long t) => t * 1000.0 / System.Diagnostics.Stopwatch.Frequency / n;
        int m = n - 200;
        Console.WriteLine($"per frame (this machine): tick {ms(tick):F3} ms, outlines {ms(render):F3} ms, draw list {ms(build):F3} ms; {cmds / m} commands, {verts / m} vertices, {tris / m} triangles");
    }

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
        RigGeo geo = args.Length > 2 ? RigGeo.FromJson(File.ReadAllText(args[2])) : null;
        // RIG_PARTIAL=1: while porting, draw what's ported and report the rest instead of refusing the creature
        RigRender.AllowPartial = Environment.GetEnvironmentVariable("RIG_PARTIAL") == "1";
        if (geo != null && RigRender.Unported(geo) is string why) Console.WriteLine((RigRender.AllowPartial ? "partial: " : "") + "not ported yet: " + why);
        RigRender Rd = geo != null ? new RigRender(geo, key) : null;
        // with screenshots in the trace: draw those frames in software (the GPU's list) and compare with Chrome's pixels
        var shots = trace.TryGetValue("shots", out var so) && so is Dictionary<string, object> sd ? sd : null;
        var shotFrames = shots != null ? new HashSet<int>(((List<object>)shots["frames"]).Select(o => (int)(double)o)) : new HashSet<int>();
        Shots shooter = shots != null && Rd != null && Environment.GetEnvironmentVariable("RIG_NOSHOTS") != "1" ? Shots.Make(shots, geo, Rd, Path.GetDirectoryName(Path.GetFullPath(args[1])), key, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2])), "fabric.png")) : null;
        if (Rd != null) { R.FromM = Rd.FromM; R.W = Rd.W; }
        var dc = new DrawCheck(); var fc = new FxCheck(); var fxm = new RigFx();
        bool fxRec = trace.TryGetValue("fxLayer", out var fl) && fl is bool fb && fb;   // the frames carry the fx layer's markup (older traces don't)
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
                else if (w == "release")
                {
                    R.Press = false; var RS = data.Rel.TryGetValue(key, out var rs) ? rs : data.Release; R.Shots.Add(new RigShot { S = RS, T0 = now });
                    if (RS.Em != null) foreach (var m in RS.Em) R.Emit(m.Pre, m.W, m.N);
                    R.Down = false;
                }
                else if (w.StartsWith("emit:"))
                {   // emit:<preset>:<a part, or x,y>[:<n>]
                    var a = w.Split(':');
                    var wh = a[2].Contains(",") ? new RigWhere { At = true, X = double.Parse(a[2].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), Y = double.Parse(a[2].Split(',')[1], System.Globalization.CultureInfo.InvariantCulture) } : new RigWhere { Name = a[2] };
                    R.Emit(a[1], wh, a.Length > 3 ? double.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture) : 0);
                }
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
            bool hasDraw = fr.TryGetValue("draw", out var dw) && dw is Dictionary<string, object>;
            // every frame is drawn, as in the game (the particles start where the parts were drawn); frame 0 (the game's mount
            // runs a frame; pools write everything on their first frame) isn't compared
            if (Rd != null) Rd.Render(P, R, now);
            if (Rd != null && f > 0 && hasDraw) dc.Frame(f, R.CurName, Rd, (Dictionary<string, object>)dw, fr.TryGetValue("order", out var od) ? od as string : null);
            if (Rd != null && hasDraw && fxRec) { fxm.Build(R, now, Rd.TopW); fc.Frame(f, R.CurName, fxm, R.Parts, fr.TryGetValue("fx", out var fo) ? fo as string : null); }
            if (shooter != null && f > 0) shooter.Track();
            if (shooter != null && shotFrames.Contains(f)) shooter.Frame(f, R, Rd, now);
        }
        if (Rd != null) { dc.Report(); if (fxRec) fc.Report(); else Console.WriteLine("particles (fx layer): not recorded in this trace (re-run record-rig.mjs)"); }
        if (Rd != null && Environment.GetEnvironmentVariable("RIG_PERF") == "1") Perf(data, key, geo, t0, fps);
        shooter?.Report();
        if (Rd != null) bad += dc.Bad + fc.Bad;
        Console.WriteLine($"{frames.Count} frames, {bad} differences beyond 1e-7; largest: {worst:E2} at {worstAt}");
        return bad > 0 ? 1 : 0;
    }
}

/// <summary>Compares RigRender's drawing with the game's recorded attribute writes.</summary>
sealed class DrawCheck
{
    public int Bad, Frames, Attrs, Exact, Flips, Orders, OrderBad;
    public double WorstFlip, WorstTie; public int NearTies; public string WorstTieAt;
    public readonly SortedDictionary<string, int> TieBy = new SortedDictionary<string, int>();
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
                if (bit == 0)
                {   // a string attribute (transform, fill, offset...): compared token by token, numbers to a rounding step
                    if (!e.StrWritten.Contains(a.Key)) { Bad++; Say($"frame {f} ({state}) {kv.Key}.{a.Key}: the game wrote '{Short(theirs)}', mine didn't"); continue; }
                    CmpStr(f, state, kv.Key + "." + a.Key, e.Str[a.Key], theirs);
                    continue;
                }
                if ((e.Written & bit) == 0) { Bad++; Say($"frame {f} ({state}) {kv.Key}.{a.Key}: the game wrote '{Short(theirs)}', mine didn't"); continue; }
                if (a.Key == "d") CmpPath(f, state, kv.Key, e.D, theirs);
                else CmpNum(f, state, kv.Key + "." + a.Key, Val(e, a.Key), theirs);
            }
        }
        foreach (var e in R.All)
            foreach (var an in e.StrWritten)
                if (!(draw.TryGetValue(e.Name, out var at2) && ((Dictionary<string, object>)at2).ContainsKey(an)))
                { _el = e.Name; Bad++; Say($"frame {f} ({state}) {e.Name}.{an}: mine wrote it, the game didn't"); }
        foreach (var e in R.All)
            foreach (var an in new[] { "d", "opacity", "stroke-width", "cx", "cy", "rx", "ry" })
                if ((e.Written & Bit(an)) != 0 && !(draw.TryGetValue(e.Name, out var at) && ((Dictionary<string, object>)at).ContainsKey(an)))
                { _el = e.Name; Bad++; Say($"frame {f} ({state}) {e.Name}.{an}: mine wrote it, the game didn't"); }
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

    void CmpStr(int f, string state, string what, string mine, string theirs)
    {
        if (mine == theirs) { Exact++; return; }
        var A = RigGeometry.Tokens(mine); var B = RigGeometry.Tokens(theirs);
        if (A.Count != B.Count || !SameNumbers(A, B)) { Bad++; Say($"frame {f} ({state}) {what}: mine '{Short(mine)}', the game '{Short(theirs)}'"); return; }
        for (int i = 0; i < A.Count; i++) if (!char.IsLetter(A[i][0])) One(f, state, what + " #" + i, double.Parse(A[i], System.Globalization.CultureInfo.InvariantCulture), double.Parse(B[i], System.Globalization.CultureInfo.InvariantCulture));
    }

    void CmpPath(int f, string state, string name, RigPath mine, string theirs)
    {
        var A = RigGeometry.Tokens(mine.ToString()); var B = RigGeometry.Tokens(theirs);
        if (A.Count != B.Count || !SameNumbers(A, B))
        {
            // the ears' outlines are hulls round ~170 surface points, many of them a hair (1e-15) apart: which of two such
            // points the hull keeps depends on the last bit of sin / pow, which differs between maths libraries (and between
            // phones' WebViews in the game itself). Measured as the drawn curves' largest distance apart instead.
            // the same: an ellipse whose start point turns on a near-round tie. Any path that differs is measured as a curve;
            // within 0.4 units (about two phone pixels) it's a near-tie, counted per element so a real bug still stands out
            {
                double h = Hausdorff(Sample(A), Sample(B));
                if (h <= 0.4)
                {
                    NearTies++; if (h > WorstTie) { WorstTie = h; WorstTieAt = $"frame {f} {name}"; }
                    TieBy[name] = TieBy.TryGetValue(name, out var n0) ? n0 + 1 : 1;
                    return;
                }
                if (A.Count == B.Count) { Bad++; Say($"frame {f} ({state}) {name}.d: outlines {h:F3} apart"); return; }
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
        if (NearTies > 0) Console.WriteLine($"outlines settled by a near-tie: {NearTies} ({string.Join(", ", TieBy.Select(kv => kv.Key + " " + kv.Value))}), the drawn curves at most {WorstTie:F3} apart ({WorstTieAt})");
        foreach (var kv in _badAt) Console.WriteLine($"  {kv.Key}: {kv.Value.Count} frames ({string.Join(" ", kv.Value.Take(30))}{(kv.Value.Count > 30 ? " …" : "")})");
        Console.WriteLine($"drawing: {Frames} frames, {Attrs} attributes; numbers exact {Exact}, a rounding step off {Flips} (largest {WorstFlip:F4}); order {Orders - OrderBad}/{Orders} frames the same; {Bad} differences");
    }
}

/// <summary>Compares the fx layer (RigFx: particles, then the move's effect) with the markup the game wrote to it.</summary>
sealed class FxCheck
{
    public int Bad, Frames, Els, Exact, Flips; public double WorstFlip;
    readonly SortedDictionary<string, int> _tags = new SortedDictionary<string, int>(), _kinds = new SortedDictionary<string, int>();
    readonly SortedSet<int> _badAt = new SortedSet<int>();
    int _shown, _f;
    void Say(string m) { Bad++; _badAt.Add(_f); if (_shown++ < 20) Console.WriteLine(m); }

    sealed class El { public string Tag; public readonly Dictionary<string, string> A = new Dictionary<string, string>(); }

    /// <summary>The markup as elements in order ('/g' closes a group); text is left out.</summary>
    static List<El> Parse(string s)
    {
        var o = new List<El>(); int i = 0;
        while (s != null && (i = s.IndexOf('<', i)) >= 0)
        {
            int e = s.IndexOf('>', i);
            if (s[i + 1] == '/') { if (s.Substring(i + 2, e - i - 2).Trim() == "g") o.Add(new El { Tag = "/g" }); i = e + 1; continue; }
            int j = i + 1; while (j < s.Length && !char.IsWhiteSpace(s[j]) && s[j] != '>' && s[j] != '/') j++;
            var el = new El { Tag = s.Substring(i + 1, j - i - 1) };
            while (true)
            {
                while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
                if (s[j] == '>' || s[j] == '/') break;
                int eq = s.IndexOf('=', j); string k = s.Substring(j, eq - j).Trim();
                char q = s[eq + 1]; int end = s.IndexOf(q, eq + 2);
                el.A[k] = System.Net.WebUtility.HtmlDecode(s.Substring(eq + 2, end - eq - 2)); j = end + 1;
            }
            bool self = s[j] == '/';
            o.Add(el); i = s.IndexOf('>', j) + 1;
            if (self && el.Tag == "g") o.Add(new El { Tag = "/g" });
        }
        return o;
    }

    static string J(double v) => RigGeometry.Js(v);

    /// <summary>RigFx's shapes as the elements and attributes the game writes.</summary>
    static List<El> Mine(RigFx fx)
    {
        var o = new List<El>();
        foreach (var s in fx.Shapes)
        {
            if (s.Kind == RigFxKind.End) { o.Add(new El { Tag = "/g" }); continue; }
            var e = new El { Tag = s.Kind == RigFxKind.Group ? "g" : s.Kind == RigFxKind.Path ? "path" : s.Kind == RigFxKind.Circle ? "circle" : "ellipse" };
            if (s.Kind == RigFxKind.Path) e.A["d"] = s.D.ToString();
            if (s.Kind == RigFxKind.Circle) { e.A["cx"] = J(s.Cx); e.A["cy"] = J(s.Cy); e.A["r"] = J(s.Rx); }
            if (s.Kind == RigFxKind.Ellipse) { e.A["cx"] = J(s.Cx); e.A["cy"] = J(s.Cy); e.A["rx"] = J(s.Rx); e.A["ry"] = J(s.Ry); }
            if (s.NXf > 0)
            {
                var t = new List<string>();
                for (int i = 0, v = 0; i < s.NXf; i++)
                    switch (s.XfOp[i])
                    {
                        case 't': t.Add($"translate({J(s.XfV[v])} {J(s.XfV[v + 1])})"); v += 2; break;
                        case 'r': t.Add($"rotate({J(s.XfV[v])})"); v++; break;
                        case 's': t.Add($"scale({J(s.XfV[v])})"); v++; break;
                        case 'S': t.Add($"scale({J(s.XfV[v])} {J(s.XfV[v + 1])})"); v += 2; break;
                    }
                e.A["transform"] = string.Join(" ", t);
            }
            if (s.Fill != null) e.A["fill"] = s.Fill;
            if (s.Stroke != null) e.A["stroke"] = s.Stroke;
            if (!double.IsNaN(s.Width)) e.A["stroke-width"] = J(s.Width);
            if (s.Cap != null) e.A["stroke-linecap"] = s.Cap;
            if (!double.IsNaN(s.Opacity)) e.A["opacity"] = J(s.Opacity);
            o.Add(e);
        }
        return o;
    }

    public void Frame(int f, string state, RigFx fx, List<RigPart> parts, string markup)
    {
        _f = f;
        foreach (var p in parts) _kinds[p.Kind ?? "?"] = _kinds.TryGetValue(p.Kind ?? "?", out var n) ? n + 1 : 1;
        var G = Parse(markup); var M = Mine(fx);
        if (G.Count == 0 && M.Count == 0) return;
        Frames++;
        for (int i = 0; i < Math.Max(G.Count, M.Count); i++)
        {
            if (i >= G.Count) { Say($"frame {f} ({state}) fx #{i}: mine draws a {M[i].Tag} the game doesn't ({M.Count} elements, the game {G.Count})"); return; }
            if (i >= M.Count) { Say($"frame {f} ({state}) fx #{i}: the game draws a {G[i].Tag} mine doesn't ({M.Count} elements, the game {G.Count}): {Show(G[i])}"); return; }
            var g = G[i]; var m = M[i];
            if (g.Tag != m.Tag) { Say($"frame {f} ({state}) fx #{i}: the game draws a {g.Tag} ({Show(g)}), mine a {m.Tag} ({Show(m)})"); return; }
            Els++; _tags[g.Tag] = _tags.TryGetValue(g.Tag, out var tn) ? tn + 1 : 1;
            foreach (var kv in g.A)
            {
                if (!m.A.TryGetValue(kv.Key, out var mv)) { Say($"frame {f} ({state}) fx #{i} {g.Tag}.{kv.Key}: the game wrote '{kv.Value}', mine didn't"); continue; }
                Cmp(f, state, $"fx #{i} {g.Tag}.{kv.Key}", mv, kv.Value);
            }
            foreach (var kv in m.A) if (!g.A.ContainsKey(kv.Key)) Say($"frame {f} ({state}) fx #{i} {m.Tag}.{kv.Key}: mine wrote '{kv.Value}', the game didn't");
        }
    }

    static string Show(El e) => e.Tag + " " + string.Join(" ", e.A.Select(kv => kv.Key + "=" + kv.Value));

    void Cmp(int f, string state, string what, string mine, string theirs)
    {
        if (mine == theirs) { Exact++; return; }
        if (mine.StartsWith("url(#phz") && theirs.StartsWith("url(#phz")) { Exact++; return; }   // (the mount's uid suffix)
        var A = RigGeometry.Tokens(mine); var B = RigGeometry.Tokens(theirs);
        bool num(string t) => !char.IsLetter(t[0]);
        if (A.Count != B.Count) { Say($"frame {f} ({state}) {what}: mine '{mine}', the game '{theirs}'"); return; }
        for (int i = 0; i < A.Count; i++)
        {
            if (!num(A[i]) || !num(B[i])) { if (A[i] != B[i]) { Say($"frame {f} ({state}) {what}: mine '{mine}', the game '{theirs}'"); return; } continue; }
            double a = double.Parse(A[i], System.Globalization.CultureInfo.InvariantCulture), b = double.Parse(B[i], System.Globalization.CultureInfo.InvariantCulture), d = Math.Abs(a - b);
            if (d < 1e-9) Exact++;
            else if (d <= 0.0101) { Flips++; if (d > WorstFlip) WorstFlip = d; }
            else { Say($"frame {f} ({state}) {what}: mine '{mine}', the game '{theirs}'"); return; }
        }
    }

    public void Report()
    {
        if (_badAt.Count > 0) Console.WriteLine($"  fx: {_badAt.Count} frames ({string.Join(" ", _badAt.Take(30))}{(_badAt.Count > 30 ? " …" : "")})");
        Console.WriteLine($"particles (fx layer): {Frames} frames drawn, {Els} elements ({string.Join(", ", _tags.Select(kv => kv.Key + " " + kv.Value))}); particle-frames by kind: {(_kinds.Count == 0 ? "none" : string.Join(", ", _kinds.Select(kv => kv.Key + " " + kv.Value)))}; numbers exact {Exact}, a rounding step off {Flips} (largest {WorstFlip:F4}); {Bad} differences");
    }
}

/// <summary>Draws the screenshot frames with RigDisplay + RigRaster and compares them with Chrome's.</summary>
sealed class Shots
{
    readonly RigDisplay _d; readonly RigRaster _r; readonly string _dir, _key; readonly int _w, _h;
    readonly List<string> _lines = new List<string>();

    public static Shots Make(Dictionary<string, object> s, RigGeo geo, RigRender R, string dir, string key, string fabric)
    {
        try { return new Shots(s, geo, R, dir, key, fabric); }
        catch (NotSupportedException e) { Console.WriteLine("pixels: not drawn (" + e.Message + ")"); return null; }
    }

    public Shots(Dictionary<string, object> s, RigGeo geo, RigRender R, string dir, string key, string fabric)
    {
        _dir = dir; _key = key;
        double dsf = (double)s["dsf"]; var host = (Dictionary<string, object>)s["host"]; var clip = (Dictionary<string, object>)s["clip"];
        double hs = (double)host["s"];
        float k = (float)(hs / 100 * dsf), ox = (float)(((double)host["x"] - (double)clip["x"]) * dsf), oy = (float)(((double)host["y"] - (double)clip["y"]) * dsf);
        _w = (int)Math.Round((double)clip["width"] * dsf); _h = (int)Math.Round((double)clip["height"] * dsf);
        float[] grain = null; int gw = 0, gh = 0;
        if (File.Exists(fabric)) { var px = RigRaster.LoadPng(fabric, out gw, out gh); grain = new float[gw * gh]; for (int i = 0; i < grain.Length; i++) grain[i] = px[i * 4 + 3] / 255f; }
        _d = new RigDisplay(RigTree.FromGeo(geo, R), R) { PxPerUnit = k, Fx = _fx };
        _r = new RigRaster(_w, _h, k, ox, oy, grain, gw, gh);
    }

    readonly RigFx _fx = new RigFx();
    public void Track() => _d.Track();

    public void Frame(int f, RigEngine E, RigRender R, double now)
    {
        _fx.Build(E, now, R.TopW);
        _d.Build();
        var mine = _r.Play(_d.Cmds);
        string mp = Path.Combine(_dir, $"mine-{_key}-{f}.png"); RigRaster.SavePng(mp, mine, _w, _h);
        string sp = Path.Combine(_dir, $"shot-{_key}-{f}.png");
        if (!File.Exists(sp)) { _lines.Add($"frame {f}: no screenshot"); return; }
        var shot = RigRaster.LoadPng(sp, out int sw, out int sh);
        if (sw != _w || sh != _h) { _lines.Add($"frame {f}: screenshot {sw}x{sh}, mine {_w}x{_h}"); return; }
        // both over one background colour, then per-pixel differences (0..255)
        double sum = 0; int over8 = 0, over32 = 0, n = 0; int maxd = 0;
        float[] bg = { 60 / 255f, 92 / 255f, 60 / 255f };
        for (int p = 0; p < _w * _h; p++)
        {
            float sa = shot[p * 4 + 3] / 255f, ma = mine[p * 4 + 3];
            if (sa == 0 && ma == 0) continue;
            n++; int dmax = 0;
            for (int ch = 0; ch < 3; ch++)
            {
                float a = shot[p * 4 + ch] / 255f * sa + bg[ch] * (1 - sa), b = mine[p * 4 + ch] + bg[ch] * (1 - ma);
                int d = (int)Math.Round(Math.Abs(a - b) * 255); sum += d; if (d > dmax) dmax = d;
            }
            if (dmax > 8) over8++; if (dmax > 32) over32++; if (dmax > maxd) maxd = dmax;
        }
        _lines.Add($"frame {f}: {n} pixels drawn; mean difference {sum / Math.Max(1, n * 3):F2}/255, off by more than 8: {100.0 * over8 / Math.Max(1, n):F2}%, by more than 32: {100.0 * over32 / Math.Max(1, n):F2}%, largest {maxd}");
    }

    public void Report() { Console.WriteLine("pixels (software copy of the GPU drawing vs Chrome):"); foreach (var l in _lines) Console.WriteLine("  " + l); }
}
