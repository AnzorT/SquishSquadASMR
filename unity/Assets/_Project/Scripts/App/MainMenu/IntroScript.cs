// The game intro's story, "Squish wakes the house" (webgame/app/intro.js, window.SquadIntro.run), as a timeline with
// no Unity in it: the same waits, tweens, easings and numbers as intro.js, stepped by a clock (milliseconds since the
// intro started). MenuIntro.cs draws what it says on the menu every frame; tools/unity-port/intro-check runs it in
// .NET against values recorded from the game itself.
//
// How intro.js's JavaScript maps here:
//   await wait(ms)          yield return Wait(ms)               (a timer: runs at its own time, as the page's clock)
//   await tween(ms, fn, e)  yield return Tween(ms, fn, e)       (fn(eased, t) once a frame from the next frame on)
//   tween(...) (no await)   Tween(...)
//   setTimeout(fn, ms)      After(ms, fn)
//   requestAnimationFrame   Every(fn) (a loop: fn returns false to stop)
// Timers run before the frame's tweens and loops, each at its own time (Now is the timer's time while it runs, as
// performance.now() in a timer callback); a tween or loop started during the frame's own pass first runs next frame,
// as requestAnimationFrame does.
//
// What intro.js does to the page shows up here as state (read every frame) and as events (sounds, the creature's moves,
// the effects, the app's phases), so the drawing side has no logic of its own.
using System;
using System.Collections.Generic;

namespace PlushCrush.App.MainMenu
{
    public sealed class IntroScript
    {
        // ---------------- intro.js's numbers ----------------

        /// <summary>A window of the house (main-menu-scene.js win(), open): r = rect x y w h, a = arch x y w h,
        /// c = circle cx cy r, d = dome cx baseY r (a half disc on its base line).</summary>
        public struct Win
        {
            public char K; public double A, B, C, D;
            public Win(char k, double a, double b, double c, double d = 0) { K = k; A = a; B = b; C = c; D = d; }
            /// <summary>box(w): x, y, width, height.</summary>
            public (double x, double y, double w, double h) Box =>
                K == 'r' || K == 'a' ? (A, B, C, D) : K == 'c' ? (A - C, B - C, 2 * C, 2 * C) : K == 'd' ? (A - C, B - C, 2 * C, C) : (A - C, B - D, 2 * C, 2 * D);
        }

        public static readonly string[] Rooms = { "living", "bedroom", "bathroom", "kitchen", "movie", "dance" };   // WIN's order
        public static readonly Dictionary<string, Win[]> WIN = new Dictionary<string, Win[]>
        {
            ["living"] = new[] { new Win('r', 434, 218, 36, 40), new Win('r', 530, 218, 36, 40) },
            ["bedroom"] = new[] { new Win('a', 298, 164, 42, 48), new Win('r', 366, 176, 30, 30) },
            ["bathroom"] = new[] { new Win('c', 629, 185, 17), new Win('r', 668, 164, 46, 44) },
            ["kitchen"] = new[] { new Win('r', 282, 246, 62, 36), new Win('c', 385, 265, 13) },
            ["movie"] = new[] { new Win('r', 604, 242, 92, 40), new Win('r', 712, 248, 16, 34) },
            ["dance"] = new[] { new Win('d', 500, 170, 46) },
        };
        public static readonly string[] ORDER = { "living", "bedroom", "bathroom" }, REST = { "kitchen", "movie", "dance" };

        /// <summary>Who peeks out at the end: key, room, window index, centre x (NaN = the window's centre).</summary>
        public static readonly (string key, string room, int wi, double cx)[] PEEK =
        {
            ("biscuit", "living", 0, double.NaN), ("berri", "living", 1, double.NaN), ("nibbles", "bedroom", 0, double.NaN),
            ("appy", "bedroom", 1, double.NaN), ("peachy", "bathroom", 0, double.NaN), ("scoop", "bathroom", 1, double.NaN),
            ("truffle", "kitchen", 0, double.NaN), ("boba", "movie", 0, 628), ("bao", "movie", 0, 672),
        };
        public const int Letters = 11;   // SQUISH SQUAD without the space

        public static class NOTE { public const double C5 = 523.25, E5 = 659.25, G5 = 783.99, C6 = 1046.5, E6 = 1318.5, G6 = 1568; }

        // the box: centre of its bottom (BX, BY), size, the top's 3/4 view
        public const double BX = 500, BY = 432, BW = 168, BH = 116, FX0 = BX - BW / 2, FX1 = BX + BW / 2, FTOP = BY - BH, TOPH = 20, IN = 18;

        /// <summary>The captions, in intro.js's words (pictures made by the exporter, by key).</summary>
        public static readonly string[] Captions = { "house", "box", "squish0", "squish1", "squish2", "squish3" };

        // easings (intro.js)
        public static double Clamp(double x, double a, double b) => Math.Max(a, Math.Min(b, x));
        public static double EaseOut(double t) => 1 - Math.Pow(1 - t, 3);
        public static double EaseIn(double t) => t * t * t;
        public static double EaseIO(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        public static double BackOut(double t) { const double c = 1.9; return 1 + (c + 1) * Math.Pow(t - 1, 3) + c * Math.Pow(t - 1, 2); }
        public static double BounceOut(double t)
        {
            const double n = 7.5625, d = 2.75;
            if (t < 1 / d) return n * t * t;
            if (t < 2 / d) { t -= 1.5 / d; return n * t * t + 0.75; }
            if (t < 2.5 / d) { t -= 2.25 / d; return n * t * t + 0.9375; }
            t -= 2.625 / d; return n * t * t + 0.984375;
        }
        static double Linear(double t) => t;
        /// <summary>r1(): rounded to a tenth (the page writes numbers that way).</summary>
        public static double R1(double x) => JsRound(x * 10) / 10;
        /// <summary>toFixed(n), as a number.</summary>
        public static double Fixed(double x, int n) { double k = Math.Pow(10, n); return JsRound(x * k) / k; }
        static double JsRound(double x) => Math.Floor(x + 0.5);

        /// <summary>nightLight(n): the creature lit for the night, warmer with every room lit (the parts menu.js's light()
        /// reads: the tint and the rim).</summary>
        public static (string tint, double rim) NightLight(int n) => (MixC("#8f99cc", "#efe2d2", n / 3.0), 0.08 + 0.12 * n);

        /// <summary>menu.js light(): the filter the menu puts on the creature for a nightLight (brightness, saturate, sepia);
        /// null: none (daylight).</summary>
        public static (double brightness, double saturate, double sepia)? Filter(int? n)
        {
            if (n == null) return null;
            var (tint, rim) = NightLight(n.Value);
            double L = Clamp(Lum(tint) / Lum("#efe2d2"), 0, 1), warm = Clamp((rim - 0.08) / 0.36, 0, 1);
            return (Fixed(0.5 + 0.5 * L, 3), Fixed(0.6 + 0.4 * L, 3), warm > 0.05 ? Fixed(0.25 * warm, 3) : 0);
        }

        static double Lum(string c) { int n = Convert.ToInt32(c.Substring(1), 16); return (0.299 * (n >> 16 & 255) + 0.587 * (n >> 8 & 255) + 0.114 * (n & 255)) / 255; }

        public static string MixC(string a, string b, double t)
        {
            t = Clamp(t, 0, 1);
            var s = "#";
            for (int i = 0; i < 3; i++)
            {
                int A = Convert.ToInt32(a.Substring(1 + 2 * i, 2), 16), B = Convert.ToInt32(b.Substring(1 + 2 * i, 2), 16);
                s += ((int)JsRound(A + (B - A) * t)).ToString("x2");
            }
            return s;
        }

        /// <summary>center(room): the middle of the room's windows' boxes.</summary>
        public static (double x, double y) Center(string room)
        {
            var W = WIN[room]; double x = 0, y = 0;
            foreach (var w in W) { var b = w.Box; x += b.x + b.w / 2; y += b.y + b.h / 2; }
            return (x / W.Length, y / W.Length);
        }

        // ---------------- the state the page shows ----------------

        /// <summary>Milliseconds since the intro started (the clock it's stepped with).</summary>
        public double Now { get; private set; }
        public readonly string Weather;

        // camera (the world: transform-origin 50% 64%, scale(WorldScale) translateY(WorldTy %))
        public double WorldScale = 1.2, WorldTy = 3;
        public bool BlackOff;   // .si-black's opacity set to 0 (a 1.4 s ease transition)

        // the caption: which, shown (.on: opacity .45s ease), popped (.pop: scale 1.1, .3s cubic-bezier(.3,1.6,.5,1))
        public string Caption; public bool CaptionOn, CaptionPop;
        int _capN;

        // the box (boxT: translate(BX, BY + dy) rotate(rot) scale(sx, sy) translate(-BX, -BY)), its flaps (0 closed .. 1
        // open), the inside's opacity, the shadow's opacity, shown at all
        public double BoxDy = -480, BoxRot, BoxSx = 1, BoxSy = 1, BoxOpacity = 1, Flaps, ShadowOpacity = 0;
        public bool BoxShown = true, HitShown;
        public double InsideOpacity => R1(Clamp(Flaps * 4, 0, 1));

        // the tap rings (two circles pulsing round a point, from the intro's start: siRing 1.25 s, the second .62 s later)
        public bool RingOn; public double RingX, RingY, RingR = 30;

        // the rooms: hole (how far the night's veil opens on the windows), halo, pane (each window's opacity)
        public readonly Dictionary<string, double> Hole = new Dictionary<string, double>(), Halo = new Dictionary<string, double>(), Pane = new Dictionary<string, double>();
        public double DoorLight;   // the scene's .mm-doorlight
        public double Night = 0.8, Moon = 0.6, Dawn = 0, SceneB = 0;
        public bool NightShown = true;   // the night, the moon and the dawn layers (display none after the dawn)

        // the creature: its night light (nightLight(n), null = none), shown, its grow-out-of-the-box transform
        public int? Light;
        public double StageOpacity, StageScale = 1, StageTy;   // translateY(StageTy %) scale(StageScale), origin 50% 96%

        // the squad peeking out (each image's y and rotation about (cx, base)), the logo's letters, the end
        public bool PeeksOn; public readonly double[] PeekY = new double[9], PeekRot = new double[9];
        public int[] PeekOrder;   // the random order they come out in (index in PEEK -> its turn)
        public bool LogoOn; public readonly double[] LetterY = new double[Letters], LetterSx = new double[Letters], LetterSy = new double[Letters], LetterRot = new double[Letters];
        public bool TapOn, SkipShown = true, TapToStartArmed;

        // the squishes
        public bool SquishOpen;   // the intro's own pointer handlers are on Mittens
        public int Squished;      // n: rooms lit by squishing (0..3)
        public bool Flying;

        public bool Dead { get; private set; }
        public double FinishedAt = double.NaN;

        // ---------------- events ----------------

        public enum Wave { Sine, Triangle }
        /// <summary>A WebAudio voice (intro.js tone() and sweep()): from F0 to F1 (exponential), at T0 seconds after the
        /// event, the gain from 0.0001 up to Vol in Attack seconds then down to 0.0001 at Dur.</summary>
        public struct Voice { public double F0, F1, T0, Dur, Vol, Attack; public Wave Type; }
        public readonly List<Voice[]> Sounds = new List<Voice[]>();      // each a sound to play now (taken by the player)
        public readonly List<string> Moves = new List<string>();          // the creature's moves to play now
        public readonly List<string> Phases = new List<string>();         // "start", "awake", "done" for the app

        public sealed class Burst { public double X, Y, T0, Life; public P[] Ps; public struct P { public double Vx, Vy, R, Rot; public string C; public int Sh; } }
        public sealed class Puff { public double X, Y, T0; }
        public sealed class Spark { public double X, Y, T; public readonly List<(double x, double y)> Trail = new List<(double, double)>(); }
        public readonly List<Burst> Bursts = new List<Burst>();
        public readonly List<Puff> Puffs = new List<Puff>();
        public readonly List<Spark> Sparks = new List<Spark>();

        // ---------------- the runner ----------------

        public sealed class Await { public bool Done; }
        sealed class Timer { public double At; public long Seq; public Action Fn; public bool Off; }
        sealed class Frame { public Func<bool> Fn; public bool Off; }
        sealed class Co { public IEnumerator<Await> It; public Await Cur; }

        readonly List<Timer> _timers = new List<Timer>();
        readonly List<Frame> _frames = new List<Frame>();
        readonly List<Co> _cos = new List<Co>();
        readonly Func<double> _rand;
        long _seq;
        bool _inFramePass;
        readonly List<Frame> _newFrames = new List<Frame>();

        public IntroScript(string weather, Func<double> random)
        {
            Weather = weather == "sunny" || weather == "snowy" ? weather : "rainy";
            _rand = random ?? (() => 0.5);
            foreach (var r in Rooms) { Hole[r] = 0; Halo[r] = 0; Pane[r] = 0; }
            for (int j = 0; j < Letters; j++) { LetterSx[j] = 1; LetterSy[j] = 1; LetterY[j] = -190; }
            Phases.Add("start");
            Light = 0;   // api.light(nightLight(0))
            Start(Show());
        }

        /// <summary>Moves the clock to now (ms since the intro started) and runs everything due, as the page does in
        /// one frame.</summary>
        public void Step(double now)
        {
            Timers(now);
            // the frame: tweens and loops asked for before it
            _inFramePass = true;
            var pass = _frames.ToArray();
            foreach (var f in pass)
            {
                if (f.Off) continue;
                if (!f.Fn()) f.Off = true;
                Pump();
            }
            _inFramePass = false;
            _frames.RemoveAll(f => f.Off);
            _frames.AddRange(_newFrames); _newFrames.Clear();
            Pump();
        }

        /// <summary>Moves the clock to now running only the timers due (a moment between frames: a tap).</summary>
        public void Timers(double now)
        {
            if (now < Now) now = Now;
            // timers, in the order they're due
            while (true)
            {
                Timer next = null;
                foreach (var t in _timers) if (!t.Off && t.At <= now && (next == null || t.At < next.At || (t.At == next.At && t.Seq < next.Seq))) next = t;
                if (next == null) break;
                _timers.Remove(next);
                Now = Math.Max(Now, next.At);
                next.Fn();
                Pump();
            }
            _timers.RemoveAll(t => t.Off);
            Now = now;
        }

        void Start(IEnumerator<Await> it) { _cos.Add(new Co { It = it }); Pump(); }

        void Pump()
        {
            bool moved = true;
            while (moved)
            {
                moved = false;
                foreach (var c in _cos.ToArray())
                {
                    while (c.It != null && (c.Cur == null || c.Cur.Done))
                    {
                        if (!c.It.MoveNext()) { c.It = null; break; }
                        c.Cur = c.It.Current; moved = true;
                    }
                }
                _cos.RemoveAll(c => c.It == null);
            }
        }

        // (setTimeout(fn, 0) runs a millisecond later, as the page's timers do: the reference recordings show it)
        Timer After(double ms, Action fn) { var t = new Timer { At = Now + Math.Max(1, ms), Seq = _seq++, Fn = fn }; _timers.Add(t); return t; }

        Await Wait(double ms) { var a = new Await(); After(ms, () => a.Done = true); return a; }

        void Every(Func<bool> fn)
        {
            var f = new Frame { Fn = fn };
            if (_inFramePass) _newFrames.Add(f); else _frames.Add(f);
        }

        /// <summary>tween(ms, fn, ease): t from the time it's asked for, fn(ease(t), t) every frame from the next one.</summary>
        Await Tween(double ms, Action<double, double> fn, Func<double, double> ease = null)
        {
            var a = new Await(); double t0 = Now; var E = ease ?? EaseIO;
            Every(() =>
            {
                if (Dead) { a.Done = true; return false; }
                double t = Math.Min(1, (Now - t0) / ms);
                fn(E(t), t);
                if (t < 1) return true;
                a.Done = true; return false;
            });
            return a;
        }

        // ---------------- the page's pieces ----------------

        void Say(string key, bool pop = false)
        {
            int my = ++_capN;
            if (CaptionOn)
            {
                CaptionOn = false;
                After(300, () => { if (my != _capN || Dead) return; ShowCaption(key, pop); });
                return;
            }
            ShowCaption(key, pop);
        }

        void ShowCaption(string key, bool pop) { Caption = key; CaptionOn = true; if (pop) Bump(); }
        void Bump() { CaptionPop = true; After(280, () => CaptionPop = false); }
        void Hush() { _capN++; CaptionOn = false; }

        void BoxT(double dy = 0, double rot = 0, double sx = 1, double sy = 1, double? op = null)
        {
            BoxDy = R1(dy); BoxRot = R1(rot); BoxSx = Fixed(sx, 3); BoxSy = Fixed(sy, 3);
            if (op.HasValue) BoxOpacity = op.Value;
        }

        void RingAt(double? x, double y = 0, double r = 0)
        {
            if (x == null) { RingOn = false; return; }
            RingOn = true; RingX = x.Value; RingY = y; RingR = r;
        }

        void SetLit(string room, double v, double flash = 0, bool keepHoles = false)
        {
            double f = flash, dk = room == "dance" ? 0.6 : 1;
            if (!keepHoles) Hole[room] = Fixed(Clamp(v * 0.9 + f * 0.1, 0, 1) * dk, 3);
            Halo[room] = Fixed(Clamp(v * 0.85 + f * 0.6, 0, 1.4), 3);
            Pane[room] = Fixed(Clamp(v * 0.75 + f * 0.25, 0, 1) * dk, 3);
        }

        void Play(string move) => Moves.Add(move);

        static Voice V(double f, double t0, double dur, double vol = 0.08, Wave type = Wave.Sine) =>
            new Voice { F0 = f, F1 = f, T0 = t0, Dur = dur, Vol = vol, Attack = 0.012, Type = type };
        static Voice[] Tone(double f, double t0, double dur, double vol, Wave type = Wave.Sine) => new[] { V(f, t0, dur, vol, type) };
        static Voice[] Sweep(double f0, double f1, double dur, double vol, double t0 = 0) =>
            new[] { new Voice { F0 = f0, F1 = f1, T0 = t0, Dur = dur, Vol = vol, Attack = 0.02, Type = Wave.Sine } };
        public static Voice[] Chime(double f, double t0 = 0) =>
            new[] { V(f, t0, 1.4, 0.07), V(f * 2, t0, 0.8, 0.025, Wave.Triangle), V(f * 3.01, t0, 0.4, 0.012) };
        void Sound(params Voice[][] parts) => Sounds.Add(Cat(parts));
        static Voice[] Cat(params Voice[][] parts) { var all = new List<Voice>(); foreach (var p in parts) all.AddRange(p); return all.ToArray(); }

        /// <summary>Every sound the show plays (to make them ahead): the same voices as Sound() is given.</summary>
        public static IEnumerable<Voice[]> KnownSounds()
        {
            yield return Cat(Sweep(160, 70, 0.25, 0.12));
            yield return Cat(Sweep(300, 900, 0.22, 0.09), Tone(NOTE.G5, 0.06, 0.5, 0.05), Tone(NOTE.C6, 0.14, 0.6, 0.04));
            yield return Cat(Sweep(300, 140, 0.18, 0.05));
            for (int n = 0; n < 3; n++) { yield return Cat(Tone(NOTE.E5 * (1 + n * 0.12), 0, 0.25, 0.03, Wave.Triangle)); yield return Cat(Chime(new[] { NOTE.C5, NOTE.E5, NOTE.G5 }[n])); }
            foreach (var f in new[] { NOTE.C6, NOTE.E6, NOTE.G6 }) yield return Cat(Chime(f / 2));
            yield return Cat(Chime(NOTE.C5, 0), Chime(NOTE.G5, 0.14), Chime(NOTE.C6, 0.28), Chime(NOTE.E6, 0.42));
            for (int turn = 0; turn < PEEK.Length; turn++) yield return Cat(Tone(880 + turn * 90, 0, 0.14, 0.025, Wave.Triangle));
            for (int j = 0; j < Letters; j++) yield return Cat(Tone(240 + j * 22, 0, 0.16, 0.045, Wave.Triangle));
            yield return Cat(Chime(NOTE.G5, 0));
        }

        void DoBurst(double x, double y, int n, string[] cols, double spd, double life)
        {
            var P = new Burst.P[n];
            for (int i = 0; i < n; i++)
            {
                double a = -Math.PI / 2 + (_rand() - 0.5) * Math.PI * 1.6, v = spd * (0.45 + _rand() * 0.75);
                P[i] = new Burst.P { Vx = Math.Cos(a) * v, Vy = Math.Sin(a) * v, R = 2.2 + _rand() * 3.2, C = cols[i % cols.Length], Rot = _rand() * 360, Sh = i % 3 };
            }
            var b = new Burst { X = x, Y = y, T0 = Now, Life = life, Ps = P };
            Bursts.Add(b);
            Every(() => { if (Dead || (Now - b.T0) / life >= 1) { Bursts.Remove(b); return false; } return true; });
        }

        void DoPuff(double x, double y)
        {
            var p = new Puff { X = x, Y = y, T0 = Now };
            Puffs.Add(p);
            Every(() => { if (Dead || (Now - p.T0) / 700 >= 1) { Puffs.Remove(p); return false; } return true; });
        }

        /// <summary>A spark flies from Mittens to a room along an arc, leaving a twinkly trail.</summary>
        Await DoSpark((double x, double y) from, (double x, double y) to, double ms)
        {
            var a = new Await(); var s = new Spark(); double t0 = Now;
            double cx = (from.x + to.x) / 2 + (to.x < 500 ? 40 : -40), cy = Math.Min(from.y, to.y) - 110;
            Every(() =>
            {
                if (Dead) { Sparks.Remove(s); a.Done = true; return false; }
                double t = Math.Min(1, (Now - t0) / ms), e = EaseIO(t), u = 1 - e;
                s.X = u * u * from.x + 2 * u * e * cx + e * e * to.x; s.Y = u * u * from.y + 2 * u * e * cy + e * e * to.y; s.T = t;
                s.Trail.Add((s.X, s.Y)); if (s.Trail.Count > 16) s.Trail.RemoveAt(0);
                if (!Sparks.Contains(s)) Sparks.Add(s);
                if (t < 1) return true;
                Sparks.Remove(s); a.Done = true; return false;
            });
            return a;
        }

        void Flaps0(double o) => Flaps = o;

        // ---------------- input ----------------

        Await _boxTapped;
        Action _down, _up, _cancel;

        /// <summary>The box was tapped (its hit area, while HitShown).</summary>
        public void TapBox() { if (_boxTapped != null && HitShown && !Dead) { _boxTapped.Done = true; Pump(); } }
        /// <summary>A finger down / up / lost on Mittens (the intro's own handlers, while SquishOpen).</summary>
        public void SquishDown() { if (SquishOpen && !Dead) { _down?.Invoke(); Pump(); } }
        public void SquishUp() { if (SquishOpen && !Dead) { _up?.Invoke(); Pump(); } }
        public void SquishCancel() { if (SquishOpen && !Dead) _cancel?.Invoke(); }
        /// <summary>The overlay tapped after the logo: chime and finish.</summary>
        public void TapToStart() { if (!TapToStartArmed || Dead) return; Sound(Chime(NOTE.G5, 0)); Finish(); }
        /// <summary>Skip (or the phone's back): the end, at once.</summary>
        public void Finish()
        {
            if (Dead) return;
            Dead = true; FinishedAt = Now;
            foreach (var t in _timers) t.Off = true;
            _timers.Clear(); _frames.Clear(); _newFrames.Clear(); _cos.Clear();
            StageOpacity = 1; StageScale = 1; StageTy = 0; WorldScale = 1; WorldTy = 0;
            Light = null;
            Phases.Add("done");
        }

        // ---------------- the show ----------------

        IEnumerator<Await> Show()
        {
            yield return Wait(60);
            BlackOff = true;
            Tween(4200, (e, t) => { WorldScale = Fixed(1.2 - 0.2 * e, 4); WorldTy = Fixed(3 - 3 * e, 3); }, EaseIO);
            yield return Wait(900); Say("house");
            yield return Wait(2900); Hush();
            // 2. the box drops in and wobbles
            yield return Wait(350); ShadowOpacity = 0;
            yield return Tween(900, (e, t) => { BoxT(-480 * (1 - e)); ShadowOpacity = Fixed(0.42 * e, 2); }, BounceOut);
            DoPuff(BX, BY); Sound(Sweep(160, 70, 0.25, 0.12));
            yield return Tween(240, (e, t) => { double k = Math.Sin(t * Math.PI); BoxT(0, 0, 1 + 0.08 * k, 1 - 0.1 * k); });
            Say("box", true); HitShown = true; RingAt(BX, FTOP + BH / 2, 46);
            bool wob = true; double wt0 = Now;
            Every(() =>
            {
                if (!wob || Dead) return false;
                double t = (Now - wt0) / 1000, ph = t % 1.7, hop = ph < 0.34 ? Math.Sin(ph / 0.34 * Math.PI) : 0;
                BoxT(-10 * hop, Math.Sin(t * 9) * 1.6 * (0.3 + hop) + (ph < 0.34 ? Math.Sin(t * 34) * 2.4 : 0), 1 + 0.04 * hop, 1 - 0.05 * hop + 0.015 * Math.Sin(t * 6));
                Flaps0(0.07 * hop);
                return true;
            });
            bool nudging = true;
            void Nudge() { After(3200, () => { if (!nudging) return; Bump(); Nudge(); }); }
            Nudge();
            _boxTapped = new Await();
            yield return _boxTapped;
            nudging = false; wob = false; HitShown = false; RingAt(null); Hush();
            // 3. it bursts open, Mittens pops out
            yield return Tween(300, (e, t) => { double k = Math.Sin(t * Math.PI * 4) * (1 - t); BoxT(0, k * 5, 1 + 0.07 * Math.Sin(t * Math.PI), 1 - 0.1 * Math.Sin(t * Math.PI)); });
            Sound(Sweep(300, 900, 0.22, 0.09), Tone(NOTE.G5, 0.06, 0.5, 0.05), Tone(NOTE.C6, 0.14, 0.6, 0.04));
            Tween(420, (e, t) => Flaps0(e), BackOut);
            DoBurst(BX, FTOP - 6, 30, new[] { "#ff8fd0", "#ffe27a", "#8fe0ff", "#b8f0a0", "#ffffff" }, 260, 1200);
            // he grows up out of the box from his feet (hidden behind its face at first) with a little jump
            StageOpacity = 1; StageScale = 0.3; StageTy = 0; Play("surprised");
            yield return Tween(720, (e, t) =>
            {
                double j = Math.Sin(Math.Min(1, t * 1.3) * Math.PI) * 20;
                StageTy = Fixed(-j, 2); StageScale = Fixed(0.3 + 0.7 * BackOut(t), 3);
            }, Linear);
            yield return Wait(250);
            // the box falls away (folds flat and fades)
            yield return Tween(560, (e, t) => { BoxT(0, 0, 1 + 0.18 * t, 1 - 0.9 * EaseIn(t), Fixed(1 - EaseIn(t), 2)); ShadowOpacity = R1(0.42 * (1 - t)); });
            BoxShown = false; StageScale = 1; StageTy = 0; DoPuff(BX, BY - 2);
            Play("look"); yield return Wait(1500); Play("idle");
            // 4. squish him three times: each squish lights a room
            Say("squish0", true); RingAt(500, 352, 58);
            var done = new Await();
            bool mine = false;
            _down = () =>
            {
                if (Flying) return;
                mine = true; RingAt(null); Sound(Sweep(300, 140, 0.18, 0.05));
            };
            _up = () =>
            {
                if (!mine) return;
                mine = false;
                if (Flying) return;
                Flying = true;
                Start(SquishUp(done));
            };
            _cancel = () => mine = false;
            SquishOpen = true;
            yield return done;
            RingAt(null); yield return Wait(700);
            // 5. the whole house glows, then dawn: a morning in today's weather, the squad peeks out
            for (int i = 0; i < REST.Length; i++)
            {
                string room = REST[i]; double f = new[] { NOTE.C6, NOTE.E6, NOTE.G6 }[i] / 2;
                After(i * 280, () => { Sound(Chime(f)); Tween(450, (e, t) => SetLit(room, e, Math.Sin(t * Math.PI))); });
            }
            Tween(900, (e, t) => DoorLight = Fixed(0.95 * e, 2));
            yield return Wait(1200); Hush();
            Sound(Chime(NOTE.C5, 0), Chime(NOTE.G5, 0.14), Chime(NOTE.C6, 0.28), Chime(NOTE.E6, 0.42));
            Play("dance");
            yield return Tween(2800, (e, t) =>
            {
                Night = Fixed(0.8 * (1 - e), 3); Moon = Fixed(0.6 * (1 - Math.Min(1, t * 2)), 3);
                Dawn = Fixed(0.6 * Math.Sin(Math.Min(1, t * 1.1) * Math.PI), 3);
                SceneB = Fixed(Clamp((t - 0.2) / 0.6, 0, 1), 3);
                double day = Clamp((t - 0.45) / 0.5, 0, 1); foreach (var r in Rooms) SetLit(r, 1 - 0.7 * day, 0, true);
                if (Math.Floor(t * 12) != Math.Floor((t - 0.02) * 12)) Light = t < 0.55 ? (int?)3 : null;
            }, Linear);
            Light = null;
            NightShown = false;
            // the squad peeks out of the windows and waves
            if (PeekOrder == null)
            {
                var o = new List<int>(); for (int i = 0; i < PEEK.Length; i++) o.Add(i);
                for (int i = o.Count - 1; i > 0; i--) { int k = (int)(_rand() * (i + 1)); if (k > i) k = i; (o[i], o[k]) = (o[k], o[i]); }
                PeekOrder = new int[PEEK.Length]; for (int turn = 0; turn < o.Count; turn++) PeekOrder[o[turn]] = turn;
            }
            var pk = Peeks(); double pk0 = Now; PeeksOn = true;
            for (int i = 0; i < pk.Length; i++) { PeekY[i] = R1(pk[i].y0); PeekRot[i] = 0; }   // (under their windows until the next frame)
            Every(() =>
            {
                if (Dead) return false;
                for (int i = 0; i < pk.Length; i++)
                {
                    var p = pk[i]; double d = PeekOrder[i] * 150, el = Now - pk0 - d, t = Clamp(el / 700, 0, 1), y = p.y0 + (p.y1 - p.y0) * BackOut(t);
                    double wv = t >= 1 ? Math.Sin(el / 240) * 8 * Math.Min(1, (el - 700) / 400) : 0;
                    PeekY[i] = R1(y + (t >= 1 ? Math.Abs(Math.Sin(el / 480)) * -1.5 : 0)); PeekRot[i] = R1(wv);
                }
                return true;
            });
            for (int i = 0; i < PEEK.Length; i++) { int turn = PeekOrder[i]; After(150 * turn + 200, () => Sound(Tone(880 + turn * 90, 0, 0.14, 0.025, Wave.Triangle))); }
            yield return Wait(1100);
            // 6. the plush logo drops in, letter by letter
            LogoOn = true; double lt0 = Now; var thud = new bool[Letters];
            Every(() =>
            {
                if (Dead) return false;
                for (int j = 0; j < Letters; j++)
                {
                    double t = Clamp((Now - lt0 - j * 80) / 720, 0, 1), y = -190 * (1 - BounceOut(t)), land = t > 0.36 && t < 0.62 ? Math.Sin((t - 0.36) / 0.26 * Math.PI) : 0;
                    double idle = t >= 1 ? Math.Sin((Now - lt0) / 380 + j * 0.7) * 2.2 : 0;
                    LetterY[j] = Fixed(y + idle, 2); LetterSx[j] = Fixed(1 + 0.16 * land, 3); LetterSy[j] = Fixed(1 - 0.2 * land, 3);
                    LetterRot[j] = Fixed(t >= 1 ? Math.Sin((Now - lt0) / 520 + j) * 2 : 0, 2);
                    if (t > 0.36 && !thud[j]) { thud[j] = true; Sound(Tone(240 + j * 22, 0, 0.16, 0.045, Wave.Triangle)); }
                }
                return true;
            });
            yield return Wait(80 * Letters + 1000);
            Every(() => { TapOn = true; return false; });   // (added the frame after: its fade-in transition)
            SkipShown = false; TapToStartArmed = true;
        }

        // each squish: a spark to the next room, it lights up
        IEnumerator<Await> SquishUp(Await all)
        {
            string room = ORDER[Squished];
            Sound(Tone(NOTE.E5 * (1 + Squished * 0.12), 0, 0.25, 0.03, Wave.Triangle));
            DoBurst(500, 330, 10, new[] { "#fff3c0", "#ffd76a", "#ffffff" }, 120, 600);
            yield return DoSpark((500, 335), Center(room), 700);
            if (Dead) yield break;
            Sound(Chime(new[] { NOTE.C5, NOTE.E5, NOTE.G5 }[Squished]));
            var c = Center(room); DoBurst(c.x, c.y, 16, new[] { "#fff3c0", "#ffd76a", "#ffffff" }, 130, 900);
            Tween(500, (e, t) => SetLit(room, e, Math.Sin(t * Math.PI)));
            Squished++; Light = Squished;
            Say("squish" + Squished, true);
            if (Squished >= 3) Phases.Add("awake");   // the app starts its music (2026-11-17)
            Flying = false;
            if (Squished >= 3) { SquishOpen = false; all.Done = true; }
            else if (Squished == 1) After(2500, () => { if (Squished == 1 && !Dead) RingAt(500, 352, 58); });
        }

        /// <summary>Each peeking creature's picture: its square's size, x centre, y from (hidden under the window) to (out),
        /// and the point it waves about.</summary>
        public static (double sz, double cx, double y0, double y1, double baseY)[] Peeks()
        {
            var r = new (double, double, double, double, double)[PEEK.Length];
            for (int i = 0; i < PEEK.Length; i++)
            {
                var (key, room, wi, pcx) = PEEK[i];
                var b = WIN[room][wi].Box; double sz = Math.Min(b.w * 1.1, b.h * 1.3), cx = double.IsNaN(pcx) ? b.x + b.w / 2 : pcx;
                r[i] = (sz, cx, b.y + b.h, b.y + b.h - sz * 0.8, b.y + b.h + sz * 0.3);
            }
            return r;
        }

        /// <summary>flaps(o): the inside (L0 L1 R1 R0) and each flap's outline, hinged on its outer edge: o = 0 closed
        /// (lying flat), 1 = flung open up and out. Points in scene units, before the box's own transform.</summary>
        public static (double x, double y)[][] FlapShapes(double o)
        {
            (double, double) L0 = (FX0, FTOP), L1 = (FX0 + IN, FTOP - TOPH), R0 = (FX1, FTOP), R1p = (FX1 - IN, FTOP - TOPH);
            double a = o * 2.3;
            (double, double)[] Lf((double x, double y) h0, (double x, double y) h1, int sgn)
            {
                double w0 = (BX - h0.x) * sgn, w1 = (BX - h1.x) * sgn;
                return new[] { h0, h1, (R1(h1.x + Math.Cos(a) * w1 * sgn), R1(h1.y - Math.Sin(a) * w1 * 0.85)), (R1(h0.x + Math.Cos(a) * w0 * sgn), R1(h0.y - Math.Sin(a) * w0 * 0.85)) };
            }
            return new[] { new[] { L0, L1, R1p, R0 }, Lf(L0, L1, 1), Lf(R0, R1p, -1) };
        }
    }
}
