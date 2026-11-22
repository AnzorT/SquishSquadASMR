// The creature rig's pose engine: webgame/rive-rig.js tick() and its helpers (setState, poseAt, kv, EZ, Fol, fire,
// blinkC), ported line for line so a creature moves exactly as in the game. It builds each frame's pose (~50 numbers:
// squash, lean, bend, spin, gaze, blink, arms, ears…) from layers: the move (with the idle underneath and a cross-fade
// from the last move), the gaze (glances, or the finger), blinks, the squeeze, one-shot reactions, a let-go spring back
// from a spin, the follow-through springs (ears, tail, head), the spin's body mechanics, the soft body.
// The drawing (render) is separate. Checked number for number against the game: tools/unity-port/rig.
//
// Faithful to the game, quirks included: in its soft-body step the two springs' step() calls sit after a // comment
// on the same line (rive-rig.js, the SOFT block), so they never move; same here.
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    /// <summary>A pose: channel -> value. Like the page's plain object: a channel nobody set reads as missing (NaN in
    /// arithmetic, as `undefined + 1` is in JS).</summary>
    public sealed class RigPose
    {
        public readonly Dictionary<string, double> V = new Dictionary<string, double>();
        public double this[string k] { get => V.TryGetValue(k, out var v) ? v : double.NaN; set => V[k] = value; }
        public bool Has(string k) => V.ContainsKey(k);
        public RigPose Clone() { var p = new RigPose(); foreach (var kv in V) p.V[kv.Key] = kv.Value; return p; }
    }

    /// <summary>The page's Fol: a damped spring, stepped in 6 ms slices.</summary>
    public sealed class RigSpring
    {
        public double V, D, W, Z;
        public RigSpring(double v, double w, double z = 1) { V = v; W = w; Z = z; }
        public double Step(double t, double dt)
        {
            int n = Math.Max(1, (int)Math.Ceiling(dt / 0.006));
            double h = dt / n;
            for (int i = 0; i < n; i++) { D += (W * W * (t - V) - 2 * Z * W * D) * h; V += D * h; }
            return V;
        }
    }

    /// <summary>The seeded generator the recorded traces use for the rig's own random draws (xorshift32).</summary>
    public sealed class RigRandom
    {
        uint _s;
        public RigRandom(uint seed) { _s = seed; }
        public double Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s / 4294967296.0; }
    }

    public static class RigMath
    {
        public static double Clamp(double v, double a, double b) => Math.Max(a, Math.Min(b, v));
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static double SM(double x) { x = Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
        public const double D2R = Math.PI / 180;
        /// <summary>JS Math.round: halves go up.</summary>
        public static double Round(double x) => Math.Floor(x + 0.5);

        // rive-rig.js bez(): Newton's method, 8 steps, clamped
        public static Func<double, double> Bez(double x1, double y1, double x2, double y2)
        {
            double cx = 3 * x1, bx = 3 * (x2 - x1) - cx, ax = 1 - cx - bx, cy = 3 * y1, by = 3 * (y2 - y1) - cy, ay = 1 - cy - by;
            return x =>
            {
                if (x <= 0) return 0; if (x >= 1) return 1;
                double t = x;
                for (int i = 0; i < 8; i++)
                {
                    double d = (3 * ax * t + 2 * bx) * t + cx;
                    if (Math.Abs(d) < 1e-5) break;
                    t = Clamp(t - (((ax * t + bx) * t + cx) * t - x) / d, 0, 1);
                }
                return ((ay * t + by) * t + cy) * t;
            };
        }

        static readonly Func<double, double> EzIo = Bez(0.45, 0, 0.55, 1), EzS = Bez(0.37, 0, 0.63, 1), EzBk = Bez(0.34, 1.45, 0.64, 1);

        /// <summary>EZ[name]: l, io, s, o, i, bk.</summary>
        public static double Ez(string name, double x)
        {
            switch (name)
            {
                case "l": return x;
                case null: case "io": return EzIo(x);
                case "s": return EzS(x);
                case "o": return 1 - Math.Pow(1 - x, 2.4);
                case "i": return Math.Pow(x, 2.2);
                case "bk": return EzBk(x);
                default: throw new ArgumentException("rig: unknown ease '" + name + "'");
            }
        }

        /// <summary>kv(): a track's value at t ms.</summary>
        public static double Kv(RigTrack K, double t)
        {
            if (t <= K.T[0]) return K.V[0];
            for (int i = 1; i < K.T.Length; i++)
                if (t <= K.T[i])
                    return K.V[i - 1] + (K.V[i] - K.V[i - 1]) * Ez(K.E[i] ?? "io", (t - K.T[i - 1]) / Math.Max(1, K.T[i] - K.T[i - 1]));
            return K.V[K.V.Length - 1];
        }

        /// <summary>blinkC(): how shut the eyes are, t ms into a blink.</summary>
        public static double BlinkC(double t) => t < 0 ? 0 : t < 75 ? Ez("i", t / 75) : t < 105 ? 1 : t < 240 ? 1 - Ez("o", (t - 105) / 135) : 0;
    }

    public sealed class RigShot { public RigState S; public double T0, X, Y; }
    public sealed class RigDent { public double X, Y, A; }
    public sealed class RigStretch { public double S, C, N; }

    /// <summary>One creature's rig (the page's I), ticked every frame.</summary>
    public sealed class RigEngine
    {
        public readonly RigData Data;
        public readonly string Key;
        readonly Func<double> _rand;
        /// <summary>Which moves the remote config allows for this creature (SquishConfig): all, until it's wired.</summary>
        public Func<string, bool> Allowed = _ => true;
        public Action<string> OnState;
        public bool Generic;  // a creature drawn by the generic rig (rive-generic.js) rather than a built-in one

        // state
        public string CurName = "idle"; public double CurT0;
        bool _hasPrev; string _prevName; double _prevT0; RigPose _prevFrozen;
        double _bt0, _bd = 280;
        public readonly List<string> Queue = new List<string>();
        int _evC = -1; HashSet<int> _evF = new HashSet<int>();
        public readonly List<RigShot> Shots = new List<RigShot>();
        public RigPose LastP;
        public Dictionary<string, double> Ext;

        // gaze, blink, fidgets
        public bool Fidgets = true, HFid;
        double _fidAt; string _lastFid;
        public double[] Ptr;  // the finger over the creature, (-1..1, -1..1), or null
        double[] _gl = { 0, 0 }; double _glAt, _blinkT = -1e9, _blinkAt; bool _dbl;

        // touch
        public bool Press, Down, Drag; public double PressT, Spin0, SpinOff;
        public SpinTween SpinTw;
        public sealed class SpinTween { public double From, To, T0, D; }

        // springs
        readonly RigSpring _flw = new RigSpring(1, 8), _fex = new RigSpring(0, 22), _fey = new RigSpring(0, 22),
            _fhx = new RigSpring(0, 6.5), _fhy = new RigSpring(0, 6.5), _fp = new RigSpring(0, 14),
            _fs = new RigSpring(0, 18, 0.55), _fx = new RigSpring(0, 9, 0.6), _fl = new RigSpring(0, 9, 0.6), _fsp = new RigSpring(0, 7, 0.7);
        double? _lastSpin;
        RigSpring _sbF, _sbW, _sbWl; double _sbLast; bool _hasSb;
        RigSpring _jelX, _jelY; bool _hasJel;
        RigSpring _motVx, _motVy; double? _motLx; double _motLy, _motYw; bool _hasMot;

        // outputs for the drawing, besides the pose
        public double Hs, Ed, Ld, SpTw, SpF;
        public RigStretch Ms;
        public RigDent Dent;
        public RigState St;   // the move tick() ended on (render reads its propZ)
        /// <summary>I.fromM: a move's tap point from Mittens' frame onto this creature (RigRender.FromM; null for Mittens).</summary>
        public Func<double, double, (double x, double y)> FromM;

        public RigEngine(RigData data, string key, double now, Func<double> random)
        {
            Data = data; Key = key; _rand = random;
            CurT0 = now; _glAt = now + 1200; _blinkAt = now + 1500;
        }

        RigState SO(string n) => (Key != null ? Data.State(n + "@" + Key) : null) ?? Data.State(n);

        RigState Default(string n) => Data.State(n);

        static readonly RigState DENT = Make("DENT", 600, ("dent", new double[] { 0, 0, 45, 1, 200, 0.3, 460, 0 }, new[] { null, "o", "io", "io" }),
            ("sq", new double[] { 0, 0, 50, 0.05, 200, -0.02, 360, 0 }, new[] { null, "o", "io", "io" }));

        static RigState Poke(double s) => Make("POKE", 1100,
            ("dent", new double[] { 0, 0, 45, 1, 200, 0.3, 460, 0 }, new[] { null, "o", "io", "io" }),
            ("lean", new double[] { 0, 0, 70, 4 * s, 300, -1.6 * s, 540, 0 }, new[] { null, "o", "io", "io" }),
            ("close", new double[] { 0, 0, 40, 1, 170, 1, 300, 0 }, new[] { null, "o", null, "io" }),
            ("hap", new double[] { 0, 0, 280, 0, 360, 1, 850, 1, 1050, 0 }, new[] { null, null, "o", null, "io" }),
            ("smile", new double[] { 0, 0, 280, 0, 360, 1, 850, 1, 1050, 0 }, new[] { null, null, "o", null, "io" }),
            ("open", new double[] { 0, 0, 280, 0, 360, 0.35, 850, 0.3, 1050, 0 }, new[] { null, null, "o", null, "io" }),
            ("sq", new double[] { 0, 0, 60, 0.06, 200, -0.03, 380, 0.05, 480, -0.02, 580, 0.04, 680, -0.01, 800, 0 }, new[] { null, "o", "io", "io", "io", "io", "io", "io" }));

        static RigState Make(string name, double dur, params (string ch, double[] tv, string[] e)[] tracks)
        {
            var s = new RigState { Name = name, Dur = dur };
            foreach (var (ch, tv, e) in tracks)
            {
                int n = tv.Length / 2;
                var t = new RigTrack { T = new double[n], V = new double[n], E = e };
                for (int i = 0; i < n; i++) { t.T[i] = tv[2 * i]; t.V[i] = tv[2 * i + 1]; }
                s.K.Add(new KeyValuePair<string, RigTrack>(ch, t));
            }
            return s;
        }

        // ---------------- poseAt ----------------
        public RigPose PoseAt(string name, double t)
        {
            var st = SO(name);
            var P = new RigPose();
            foreach (var kv in Data.Rest) P[kv.Key] = kv.Value;
            var id = Data.State("idle");
            if (name != "idle") { double ti = t % id.Loop; foreach (var kv in id.K) P[kv.Key] = RigMath.Kv(kv.Value, ti); }
            double tl = st.Loop != 0 ? t % st.Loop : Math.Min(t, st.Dur != 0 ? st.Dur : t);
            foreach (var kv in st.K) P[kv.Key] = RigMath.Kv(kv.Value, tl);
            if (st.Breathe.HasValue && st.Breathe.Value != 0)
            {
                double bk = st.Breathe.Value;
                P["sq"] += Math.Sin(t / 640) * 0.013 * bk; P["lean"] += Math.Sin(t / 1350) * 0.4 * bk; P["htilt"] += Math.Sin(t / 1900 + 1) * 0.8 * bk;
            }
            return P;
        }

        // ---------------- setState ----------------
        public void SetState(string name, double now, bool keepQ = false)
        {
            if (Data.State(name) == null) name = "idle";
            name = Gate(name);
            if (!keepQ) Queue.Clear();
            var sq = Data.State(name).Seq;
            if (sq != null && sq.Count > 0)
            {
                Queue.Clear(); for (int i = 1; i < sq.Count; i++) Queue.Add(sq[i]);
                name = sq[0]; if (Data.State(name) == null) name = "idle"; name = Gate(name);
            }
            if (_hasPrev) { _prevFrozen = LastP; _prevName = null; }
            else { _hasPrev = true; _prevFrozen = null; _prevName = CurName; _prevT0 = CurT0; }
            CurName = name; CurT0 = now; _bt0 = now;
            var st = Data.State(name);
            _bd = st.Blend != 0 ? st.Blend : 280;
            _evC = -1; _evF = new HashSet<int>();
            if (st.Tap != null) { var tp = FromM != null ? FromM(st.Tap[0], st.Tap[1]) : (x: st.Tap[0], y: st.Tap[1]); Shots.Add(new RigShot { S = DENT, T0 = now, X = tp.x, Y = tp.y }); }
            OnState?.Invoke(name);
        }

        // SquishConfig.gateState: a switched-off move plays idle (all allowed until the remote config is wired)
        string Gate(string name) => Allowed(name) ? name : "idle";

        public void Play(string name, double now) => SetState(name, now);

        void Fire(RigEvent e, double now)
        {
            if (e.Name == "blink" || e.Name == "blink2") { _blinkT = now; _dbl = e.Name == "blink2"; _blinkAt = now + 2600; }
            else if (e.Name == "poke") Shots.Add(new RigShot { S = Poke(e.X < 50 ? 1 : -1), T0 = now, X = e.X, Y = e.Y });
            // 'emit' (particles) is the drawing's
        }

        // ---------------- touch (the page's pointer handlers) ----------------
        public void PointerDown(double now) { if (CurName != "idle") SetState("idle", now); Down = true; Drag = false; }
        public void HoldStarted(double now) { if (Down && !Drag) { Press = true; PressT = now; } }   // 170 ms after the down, not dragged
        public void DragTo(double dxOverWidth) { if (!Drag) { Drag = true; SpinTw = null; Spin0 = SpinOff; } SpinOff = Spin0 + dxOverWidth * 360; }
        public void PointerUp(double now)
        {
            if (!Down) return;
            if (Drag) SpinTw = new SpinTween { From = SpinOff, To = RigMath.Round(SpinOff / 360) * 360, T0 = now, D = 650 };
            else if (Press) { Press = false; Shots.Add(new RigShot { S = (Key != null && Data.Rel.TryGetValue(Key, out var r)) ? r : Data.Release, T0 = now }); }
            // (a quick tap: nothing — in the game the poke branch is inside a // comment, rive-rig.js:1373)
            Down = false; Drag = false;
        }

        // ---------------- tick ----------------
        public RigPose Tick(double now, double dt)
        {
            var st = SO(CurName); double t = now - CurT0;
            if (st.Dur != 0 && t >= st.Dur)
            {
                string nx;
                if (Queue.Count > 0) { nx = Queue[0]; Queue.RemoveAt(0); } else nx = st.Next ?? "idle";
                SetState(nx, now, true); st = SO(CurName); t = 0;
            }
            var FL = FidgetList();
            if (CurName == "idle" && Fidgets && !Press && !Down && FL.Count > 0)
            {
                if (_fidAt == 0) _fidAt = now + (HFid ? 3200 + _rand() * 3600 : 4200 + _rand() * 4200);
                else if (now > _fidAt)
                {
                    _fidAt = 0; string f;
                    do { f = FL[(int)Math.Floor(_rand() * FL.Count)]; } while (FL.Count > 1 && f == _lastFid);
                    _lastFid = f; SetState(f, now, true); st = SO(f); t = 0;
                }
            }
            else if (CurName != "idle") _fidAt = 0;
            if (st.Ev != null)
            {
                int c = st.Loop != 0 ? (int)Math.Floor(t / st.Loop) : 0; double tl = st.Loop != 0 ? t % st.Loop : t;
                if (c != _evC) { _evC = c; _evF = new HashSet<int>(); }
                for (int i = 0; i < st.Ev.Count; i++) if (tl >= st.Ev[i].T && !_evF.Contains(i)) { _evF.Add(i); Fire(st.Ev[i], now); }
            }
            var P = PoseAt(CurName, t);
            if (_hasPrev)
            {
                double w = RigMath.Ez("io", RigMath.Clamp((now - _bt0) / _bd, 0, 1));
                if (w >= 1) { _hasPrev = false; _prevFrozen = null; }
                else
                {
                    var Q = _prevFrozen ?? PoseAt(_prevName, now - _prevT0);
                    foreach (var k in new List<string>(P.V.Keys))
                    {
                        if (k == "spin") { double d = P["spin"] - Q["spin"]; d = ((d % 360) + 540) % 360 - 180; P["spin"] = Q["spin"] + d * w; }
                        else { double q = Q.Has(k) ? Q[k] : P[k]; P[k] = RigMath.Lerp(q, P[k], w); }
                    }
                }
            }
            if (Ext != null) foreach (var kv in Ext) P[kv.Key] = (Truthy(P[kv.Key]) ? P[kv.Key] : 0) + kv.Value;
            LastP = P.Clone();

            // look layer: eyes lead, head follows
            _flw.Step(st.Look ? 1 : 0, dt); double lw = RigMath.Clamp(_flw.V, 0, 1);
            double tx = 0, ty = 0;
            if (Ptr != null) { tx = Ptr[0]; ty = Ptr[1]; }
            else
            {
                if (now > _glAt)
                {
                    double[] g;
                    if (_rand() < 0.3) g = new double[] { 0, 0 };
                    else { double gx = (_rand() * 2 - 1) * 0.9, gy = (_rand() - 0.5) * 0.7; g = new[] { gx, gy }; }
                    if (Math.Abs(g[0] - _gl[0]) > 0.9 && now - _blinkT > 800) { _blinkT = now + 60; _dbl = false; }
                    _gl = g; _glAt = now + 1400 + _rand() * 2600;
                }
                tx = _gl[0]; ty = _gl[1];
            }
            _fex.Step(tx, dt); _fey.Step(ty, dt); _fhx.Step(tx, dt); _fhy.Step(ty, dt);
            if (Key != null && Data.Hyaw.Contains(Key)) { P["hyaw"] += _fhx.V * 0.62 * lw; P["turn"] += _fhx.V * 0.06 * lw; }
            else P["turn"] += _fhx.V * 0.6 * lw;
            P["lx"] += _fex.V * lw; P["ly"] += _fey.V * lw; P["pitch"] -= _fhy.V * 0.35 * lw; P["htilt"] += _fhx.V * 2.5 * lw;

            // blink layer
            if (now > _blinkAt) { _blinkT = now; _dbl = _rand() < 0.2; _blinkAt = now + 2200 + _rand() * 3200; }
            if (!st.NoBlink) { double b = RigMath.BlinkC(now - _blinkT); if (_dbl) b = Math.Max(b, RigMath.BlinkC(now - _blinkT - 260)); P["close"] = Math.Max(P["close"], b); }

            // touch layer
            _fp.W = Press ? 14 : 28; _fp.Step(Press ? 1 : 0, dt);
            double pv = RigMath.Clamp(_fp.V, 0, 1.05), ht = Press ? now - PressT : 0;
            if (pv > 0.001)
            {
                P["sq"] += pv * (0.34 + 0.06 * RigMath.Clamp(ht / 2000, 0, 1)); P["hap"] = Math.Max(P["hap"], pv * 0.85);
                P["armL"] += pv * 22; P["armR"] += pv * 22; P["earL"] += pv * 14; P["earR"] += pv * 14;
                P["blush"] = Math.Max(P["blush"], pv * RigMath.Clamp((ht - 400) / 900, 0, 1));
                if (ht > 1300) P["x"] += Math.Sin(now / 24) * 0.22 * pv;
                if (Key != null && Data.Press.TryGetValue(Key, out var PR)) foreach (var kv in PR) P[kv.Key] += pv * kv.Value;
            }
            Dent = null;
            for (int i = 0; i < Shots.Count; i++)
            {
                var s = Shots[i]; double u = now - s.T0;
                if (u > s.S.Dur) { Shots.RemoveAt(i); i--; continue; }
                foreach (var kv in s.S.K)
                {
                    double v = RigMath.Kv(kv.Value, u);
                    if (kv.Key == "dent") Dent = new RigDent { X = s.X, Y = s.Y, A = v }; else P[kv.Key] += v;
                }
            }
            if (SpinTw != null)
            {
                double u = RigMath.Clamp((now - SpinTw.T0) / SpinTw.D, 0, 1);
                SpinOff = RigMath.Lerp(SpinTw.From, SpinTw.To, RigMath.Ez("bk", u));
                if (u >= 1) { SpinOff = SpinTw.To % 360; SpinTw = null; }
            }
            P["spin"] += SpinOff;

            // follow-through: head jiggle, ears and tail trail behind
            _fs.Step(P["sq"], dt); Hs = RigMath.Clamp((_fs.V - P["sq"]) * 0.55, -0.12, 0.12);
            double hx = P["x"] + P["lean"] + P["tilt"] * 0.9 + P["htilt"] * 0.4; _fx.Step(hx, dt); double ed = RigMath.Clamp(_fx.V - hx, -8, 8);
            _fl.Step(P["lift"], dt); double ld = RigMath.Clamp(_fl.V - P["lift"], -10, 10); Ld = ld;
            if (_lastSpin.HasValue) { double dd0 = P["spin"] - _lastSpin.Value; if (Math.Abs(dd0) > 180) _fsp.V += RigMath.Round(dd0 / 360) * 360; }
            _lastSpin = P["spin"];
            _fsp.Step(P["spin"], dt); double sd = RigMath.Clamp(_fsp.V - P["spin"], -40, 40);

            // the spin's body mechanics
            if (st.Whip)
            {
                if (_hasSb) { _sbF.V = P["spin"]; _sbF.D = 0; _sbLast = P["spin"]; _sbW.V = 0; _sbW.D = 0; _sbWl.V = 0; _sbWl.D = 0; }
                SpTw = 0; SpF = 0;
            }
            else
            {
                if (!_hasSb) { _hasSb = true; _sbF = new RigSpring(P["spin"], 7.5, 0.42); _sbW = new RigSpring(0, 10, 0.75); _sbWl = new RigSpring(0, 6, 0.55); _sbLast = P["spin"]; }
                double dd = P["spin"] - _sbLast;
                if (Math.Abs(dd) > 180) { double k = RigMath.Round(dd / 360) * 360; _sbF.V += k; dd -= k; }
                _sbLast = P["spin"];
                _sbF.Step(P["spin"], dt);
                double tw = RigMath.Clamp(_sbF.V - P["spin"], -40, 40), om = dt > 0.002 ? Math.Abs(dd) / dt : 0;
                _sbW.Step(RigMath.Clamp(om / 540, 0, 1), dt);
                double f = RigMath.Clamp(_sbW.V, 0, 1); _sbWl.Step(f, dt);
                SpTw = tw * 0.9 * RigMath.D2R; SpF = f;
                if (!Generic) P["twist"] = (Truthy(P["twist"]) ? P["twist"] : 0) + tw * 0.9 / 40;
                P["sq"] += -0.045 * f + RigMath.Clamp((_sbWl.V - f) * 0.14, -0.03, 0.05); P["lift"] += 1.5 * f; P["armL"] += 18 * f; P["armR"] += 18 * f;
            }
            Ed = ed;
            P["earL"] += RigMath.Clamp(-ed * 2 - ld * 1.3, -22, 22); P["earR"] += RigMath.Clamp(ed * 2 - ld * 1.3, -22, 22); P["wag"] += RigMath.Clamp(sd * 0.5 - ed * 3, -30, 30);

            // the soft body (SOFT rigs)
            if (Key != null && Data.Soft.Contains(Key))
            {
                if (!_hasJel) { _hasJel = true; _jelX = new RigSpring(0, 7, 0.3); _jelY = new RigSpring(0, 8, 0.36); }
                double g = st.Soft.HasValue && Truthy(st.Soft.Value) ? st.Soft.Value : 1;
                double jtx = P["x"] + P["tilt"] * 0.9 + P["turn"] * 5 + P["bend"] * 0.3 + P["lean"], jty = P["lift"];
                // (the game's J.x.step / J.y.step are commented out on this line: the springs stay at 0)
                P["bend"] += RigMath.Clamp((_jelX.V - jtx) * 2.6 * g, -16, 16); P["sq"] += RigMath.Clamp((_jelY.V - jty) * 0.008 * g, -0.06, 0.06);
                if (!_hasMot) { _hasMot = true; _motLx = null; _motLy = 0; _motYw = 0; _motVx = new RigSpring(0, 15, 1); _motVy = new RigSpring(0, 15, 1); }
                double lx = P["x"] + P["tilt"] * 0.9 + P["lean"] + P["bend"] * 0.35, yw = (P["turn"] + P["hyaw"] + P["twist"]) * 40;
                if (_motLx.HasValue && dt > 0.002)
                {
                    _motVx.Step((lx - _motLx.Value) / dt + (yw - _motYw) * RigMath.D2R / dt * 4, dt);
                    _motVy.Step(-(P["lift"] - _motLy) / dt * 0.45, dt);
                }
                _motLx = lx; _motLy = P["lift"]; _motYw = yw;
                double spd = Math.Sqrt(_motVx.V * _motVx.V + _motVy.V * _motVy.V), sg = st.Stretch ?? 1;
                Ms = spd > 2 ? new RigStretch { S = RigMath.Clamp(spd * 0.0034 * sg, 0, 0.26 * Math.Max(1, sg * 0.8)), C = _motVx.V / spd, N = _motVy.V / spd } : null;
            }
            else Ms = null;
            double sk = P["sk"], dv = P["dv"];
            if (Truthy(sk) || Truthy(dv)) P["lift"] -= (Truthy(sk) ? sk : 0) + (Truthy(dv) ? dv : 0) * 30;
            St = st;
            return P;
        }

        List<string> FidgetList()
        {
            List<string> L = (HFid && Key != null && Data.Fidk.TryGetValue(Key, out var k)) ? k : Data.Fidgets;
            var r = new List<string>(L.Count);
            foreach (var n in L) if (Allowed(n)) r.Add(n);
            return r;
        }

        /// <summary>JS truthiness of a number: not 0, not NaN (undefined reads as NaN here).</summary>
        static bool Truthy(double v) => v != 0 && !double.IsNaN(v);
    }
}
