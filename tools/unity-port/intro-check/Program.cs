// dotnet run -c Release -- <trace.json>: replays a recording of the real intro (record-intro.mjs) through IntroScript:
// the same taps at the same moments, a step at every frame the page drew, and every value intro.js wrote that frame
// compared with IntroScript's (the camera, the box and its flaps, the rings, the rooms' lights, the door, the night,
// the moon, the dawn, the morning's house, Mittens' opacity, transform and filter, the peeks, the letters, the
// caption, TAP TO START, Skip) and Mittens' moves. Exit code 1 on any difference.
// The page writes its numbers rounded (toFixed, r1); IntroScript rounds the same way. A value a hair from a half can
// round the other way in the last digit: those are counted apart (near ties), anything else is a difference.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlushCrush.App.MainMenu;

static class Program
{
    static int diffs, ties, checks;
    static readonly Dictionary<string, int> diffBy = new Dictionary<string, int>();
    static readonly List<string> firstDiffs = new List<string>();

    static double[] Nums(string s) => s == null ? new double[0] : Regex.Matches(s, @"-?\d+(\.\d+)?(e-?\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
    static double? Num(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : e.ValueKind == JsonValueKind.String && e.GetString() != "" ? double.Parse(e.GetString(), CultureInfo.InvariantCulture) : (double?)null;
    static string Str(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ValueKind == JsonValueKind.Null ? null : e.ToString();

    static void Eq(double t, string what, double page, double ours, double unit)
    {
        checks++;
        double d = Math.Abs(page - ours);
        if (d < 1e-9) return;
        if (d <= unit * 1.0000001) { ties++; return; }
        Diff(t, what, page.ToString(CultureInfo.InvariantCulture), ours.ToString(CultureInfo.InvariantCulture));
    }

    static void Same(double t, string what, object page, object ours)
    {
        checks++;
        if (Equals(page, ours)) return;
        Diff(t, what, page?.ToString() ?? "null", ours?.ToString() ?? "null");
    }

    static void Diff(double t, string what, string page, string ours)
    {
        diffs++;
        string k = Regex.Replace(what, @"\[\d+\]", "[]");
        diffBy[k] = diffBy.TryGetValue(k, out var n) ? n + 1 : 1;
        if (firstDiffs.Count(d => d.Contains(" " + k.Split(' ')[0])) < 4 && firstDiffs.Count < 40) firstDiffs.Add($"  t={t:0.###} {what}: page {page}, IntroScript {ours}");
    }

    static readonly (string prefix, string key)[] Caps =
    {
        ("A little house", "house"), ("Something is moving", "box"), ("Squish Mittens", "squish0"), ("The living room", "squish1"),
        ("The bedroom too", "squish2"), ("The whole house", "squish3"),
    };

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "sounds") return Sounds(args[1]);
        var J = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
        string weather = J.GetProperty("weather").GetString();
        var inputs = J.GetProperty("inputs").EnumerateArray().Select(a => (t: a[0].GetDouble(), kind: a[1].GetString())).ToList();
        var moves = J.GetProperty("moves").EnumerateArray().Select(a => (t: a[0].GetDouble(), move: a[1].GetString())).ToList();
        var frames = J.GetProperty("frames").EnumerateArray().ToList();

        // the peeks' random order (Math.random in the page): when each starts rising, 150 ms apart
        var peeks = IntroScript.Peeks(); var start = new double[peeks.Length];
        for (int i = 0; i < peeks.Length; i++)
        {
            start[i] = double.NaN;
            foreach (var f in frames)
            {
                var p = f.GetProperty("v").GetProperty("peeks");
                if (p.GetArrayLength() > i && Num(p[i][0]) is double y && y < IntroScript.R1(peeks[i].y0) - 0.05) { start[i] = f.GetProperty("t").GetDouble(); break; }
            }
        }
        double s0 = start.Where(x => !double.IsNaN(x)).DefaultIfEmpty(0).Min();
        var order = start.Select(x => double.IsNaN(x) ? 0 : (int)Math.Round((x - s0) / 150)).ToArray();

        var S = new IntroScript(weather, () => 0.5) { PeekOrder = order };
        var ourMoves = new List<(double t, string move)>();
        int ii = 0;
        foreach (var f in frames)
        {
            double t = f.GetProperty("t").GetDouble();
            while (ii < inputs.Count && inputs[ii].t < t)
            {
                var (it, kind) = inputs[ii++];
                S.Timers(it);
                if (kind == "tapBox") S.TapBox(); else if (kind == "down") S.SquishDown(); else if (kind == "up") S.SquishUp(); else if (kind == "start") S.TapToStart();
                foreach (var m in S.Moves) ourMoves.Add((it, m)); S.Moves.Clear();
            }
            S.Step(t);
            foreach (var m in S.Moves) ourMoves.Add((t, m)); S.Moves.Clear();
            Compare(t, f.GetProperty("v"), S);
        }
        // the moves: the same, each within the frame the page played it in (the menu's own 'happy' after the end aside)
        if (S.Dead) moves = moves.Where(m => m.t < S.FinishedAt).ToList();
        Same(0, "moves", string.Join(",", moves.Select(m => m.move)), string.Join(",", ourMoves.Select(m => m.move)));
        for (int i = 0; i < Math.Min(moves.Count, ourMoves.Count); i++) if (Math.Abs(moves[i].t - ourMoves[i].t) > J.GetProperty("period").GetDouble()) Diff(moves[i].t, "move time " + moves[i].move, moves[i].t.ToString("0.#"), ourMoves[i].t.ToString("0.#"));

        Console.WriteLine($"{frames.Count} frames, {checks} values compared: {diffs} differences, {ties} near ties (the last digit of a rounded half)");
        foreach (var kv in diffBy.OrderByDescending(k => k.Value)) Console.WriteLine($"  {kv.Key}: {kv.Value}");
        foreach (var d in firstDiffs) Console.WriteLine(d);
        return diffs > 0 ? 1 : 0;
    }

    // `sounds <out.json>`: every sound the show plays, its voices and IntroSynth's samples (sound-check.mjs renders
    // the same voices with WebAudio in Chrome and compares)
    static int Sounds(string path)
    {
        var list = new List<object>();
        foreach (var vs in IntroScript.KnownSounds())
        {
            double end = vs.Max(v => v.T0 + v.Dur + 0.05);
            var data = IntroSound.Render(vs, (int)Math.Ceiling(end * 44100));
            list.Add(new { voices = vs.Select(v => new { v.F0, v.F1, v.T0, v.Dur, v.Vol, v.Attack, type = v.Type.ToString() }), samples = data });
        }
        File.WriteAllText(path, JsonSerializer.Serialize(list));
        Console.WriteLine($"{list.Count} sounds -> {path}");
        return 0;
    }

    static void Compare(double t, JsonElement v, IntroScript S)
    {
        bool dead = S.Dead;
        // camera
        var w = Str(v.GetProperty("world"));
        if (!dead && !string.IsNullOrEmpty(w)) { var n = Nums(w); Eq(t, "world scale", n[0], S.WorldScale, 1e-4); Eq(t, "world ty", n[1], S.WorldTy, 1e-3); }
        if (v.GetProperty("black").ValueKind != JsonValueKind.Null) Same(t, "black off", Str(v.GetProperty("black")) == "0", S.BlackOff);
        if (dead) return;
        // caption
        var cap = v.GetProperty("cap");
        if (cap.ValueKind == JsonValueKind.Array)
        {
            string text = cap[0].GetString(); var k = Caps.FirstOrDefault(c => text.StartsWith(c.prefix)).key;
            if (k != null) Same(t, "caption", k, S.Caption);
            Same(t, "caption on", cap[1].GetBoolean(), S.CaptionOn); Same(t, "caption pop", cap[2].GetBoolean(), S.CaptionPop);
        }
        // the box
        var box = v.GetProperty("box");
        bool shown = Str(box[2]) != "none";
        Same(t, "box shown", shown, S.BoxShown);
        if (shown)
        {
            var n = Nums(Str(box[0]));
            if (n.Length >= 5) { Eq(t, "box dy", n[1] - 432, S.BoxDy, 0.1); Eq(t, "box rot", n[2], S.BoxRot, 0.1); Eq(t, "box sx", n[3], S.BoxSx, 1e-3); Eq(t, "box sy", n[4], S.BoxSy, 1e-3); }
            if (Num(box[1]) is double op) Eq(t, "box opacity", op, S.BoxOpacity, 0.01);
            if (Num(box[4]) is double io) Eq(t, "box inside", io, S.InsideOpacity, 0.1);
            var fl = Nums(Str(box[5])); var ours = IntroScript.FlapShapes(S.Flaps)[1];
            for (int i = 0; i < Math.Min(fl.Length / 2, ours.Length); i++) { Eq(t, $"flap[{i}].x", fl[2 * i], ours[i].x, 0.1); Eq(t, $"flap[{i}].y", fl[2 * i + 1], ours[i].y, 0.1); }
        }
        if (Num(box[3]) is double sh) Eq(t, "box shadow", sh, S.ShadowOpacity, 0.1);
        Same(t, "box tap area", Str(v.GetProperty("hit")) != "none", S.HitShown);
        var ring = v.GetProperty("ring");
        if (ring.ValueKind == JsonValueKind.Array)
        {
            bool on = Str(ring[0]) == "1"; Same(t, "rings on", on, S.RingOn);
            if (on) { Eq(t, "rings x", Num(ring[1]).Value, S.RingX, 1e-9); Eq(t, "rings y", Num(ring[2]).Value, S.RingY, 1e-9); Eq(t, "rings r", Num(ring[3]).Value, S.RingR, 1e-9); }
        }
        // the rooms, the door, the night and the dawn
        foreach (var r in IntroScript.Rooms)
        {
            var a = v.GetProperty("rooms").GetProperty(r);
            Eq(t, r + " hole", Num(a[0]) ?? 0, S.Hole[r], 1e-3); Eq(t, r + " halo", Num(a[1]) ?? 0, S.Halo[r], 1e-3); Eq(t, r + " pane", Num(a[2]) ?? 0, S.Pane[r], 1e-3);
        }
        if (Num(v.GetProperty("door")) is double door) Eq(t, "door light", door, S.DoorLight, 0.01);
        var night = v.GetProperty("night");
        Same(t, "night shown", Str(night[1]) != "none", S.NightShown);
        if (S.NightShown)
        {
            Eq(t, "night", Num(night[0]).Value, S.Night, 1e-3); Eq(t, "moon", Num(v.GetProperty("moon")).Value, S.Moon, 1e-3); Eq(t, "dawn", Num(v.GetProperty("dawn")).Value, S.Dawn, 1e-3);
        }
        if (Num(v.GetProperty("sceneB")) is double sb) Eq(t, "morning", sb, S.SceneB, 1e-3);
        // Mittens
        var st = v.GetProperty("stage");
        if (st.ValueKind == JsonValueKind.Array)
        {
            Eq(t, "Mittens opacity", Num(st[0]) ?? 1, S.StageOpacity, 1e-9);
            var tr = Str(st[1]); var n = Nums(tr);
            double ty = tr.Contains("translateY") ? n[0] : 0, sc = tr.Contains("scale") ? n[n.Length - 1] : 1;
            Eq(t, "Mittens ty", ty, S.StageTy, 0.01); Eq(t, "Mittens scale", sc, S.StageScale, 1e-3);
            var fl = Nums(Str(st[2])); var f = IntroScript.Filter(S.Light);
            Same(t, "Mittens filtered", fl.Length > 0, f != null);
            if (f != null && fl.Length >= 2)
            {
                Eq(t, "Mittens brightness", fl[0], f.Value.brightness, 1e-3); Eq(t, "Mittens saturate", fl[1], f.Value.saturate, 1e-3);
                Eq(t, "Mittens sepia", fl.Length > 2 ? fl[2] : 0, f.Value.sepia, 1e-3);
            }
        }
        // the peeks
        var pk = v.GetProperty("peeks");
        if (pk.GetArrayLength() > 0)
        {
            Same(t, "peeks on", true, S.PeeksOn);
            for (int i = 0; i < pk.GetArrayLength(); i++)
            {
                Eq(t, $"peek[{i}].y", Num(pk[i][0]).Value, S.PeekY[i], 0.1);
                var tr = Nums(Str(pk[i][1])); if (tr.Length > 0) Eq(t, $"peek[{i}].rot", tr[0], S.PeekRot[i], 0.1);
            }
        }
        // the letters
        var lt = v.GetProperty("letters");
        if (lt.GetArrayLength() > 0)
        {
            Same(t, "logo on", true, S.LogoOn);
            for (int j = 0; j < lt.GetArrayLength(); j++)
            {
                var n = Nums(lt[j].GetString());
                Eq(t, $"letter[{j}].y", n[0], S.LetterY[j], 0.01);
                if (n.Length >= 4) { Eq(t, $"letter[{j}].sx", n[1], S.LetterSx[j], 1e-3); Eq(t, $"letter[{j}].sy", n[2], S.LetterSy[j], 1e-3); Eq(t, $"letter[{j}].rot", n[3], S.LetterRot[j], 0.01); }
            }
        }
        if (v.GetProperty("tap").ValueKind == JsonValueKind.True || v.GetProperty("tap").ValueKind == JsonValueKind.False) Same(t, "tap on", v.GetProperty("tap").GetBoolean(), S.TapOn);
        Same(t, "skip shown", Str(v.GetProperty("skip")) != "none", S.SkipShown);
    }
}
