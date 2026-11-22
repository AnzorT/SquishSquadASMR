// The rig's particles (rive-rig.js PRE, where, emit and the motion half of drawParts): crumbs, drops, tears, bubbles,
// sparkles, hearts, stink waves, steam, notes, a rubber duck that hops... A move fires them (an 'emit' event, an emit
// window, a let-go reaction), each starts where the rig drew a part last frame (I.W) and flies with its preset's speed,
// spread and gravity until its life is up. They live in the engine (I.parts) and draw their random numbers from its
// delegate, in the game's order; RigFx draws them. Pooled: no garbage per frame.
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    /// <summary>One particle (an entry of I.parts).</summary>
    public sealed class RigPart
    {
        public double X, Y, Vx, Vy, G, Life, Age, R, Ph;
        public string Col, Kind;
        public bool Hop; public double Gy;        // gy: the floor a hopping one bounces on (null unless Hop)
        public bool Landed; public double Land;   // land: the age it last bounced at
    }

    public sealed partial class RigEngine
    {
        const double TAU = Math.PI * 2;
        /// <summary>I.parts: the live particles, oldest first.</summary>
        public readonly List<RigPart> Parts = new List<RigPart>(32);
        readonly Stack<RigPart> _freeParts = new Stack<RigPart>();
        /// <summary>I.W: where the rig drew its parts last frame (RigRender.W; null until set).</summary>
        public Dictionary<string, (double x, double y)> W;

        /// <summary>where(): a fixed point (its side by which half it's in) or a part of the creature (side by its name).</summary>
        (double x, double y, double sd) Where(RigWhere w)
        {
            if (w.At) return (w.X, w.Y, w.X < 50 ? -1 : 1);
            var p = W != null && w.Name != null && W.TryGetValue(w.Name, out var q) ? q : (50, 50);
            string n = w.Name;
            double sd = n != null && n.EndsWith("L", StringComparison.Ordinal) ? -1 : n != null && (n.EndsWith("R", StringComparison.Ordinal) || n == "tail") ? 1 : 0;
            return (p.Item1, p.Item2, sd);
        }

        /// <summary>emit(): n particles of preset name (0: the preset's own count), at most 140 alive.</summary>
        public void Emit(string name, RigWhere w, double n)
        {
            if (name == null || !Data.Pre.TryGetValue(name, out var q)) return;
            var (x, y, sd) = Where(w);
            if (!(n != 0 && !double.IsNaN(n))) n = q.N;
            for (int i = 0; i < n && Parts.Count < 140; i++)
            {
                double a = _rand() * TAU, m = _rand() * q.Sp, side = q.Out ? (sd != 0 ? sd : (_rand() < 0.5 ? -1 : 1)) : 1;
                var p = _freeParts.Count > 0 ? _freeParts.Pop() : new RigPart();
                p.X = x + Math.Cos(a) * 0.6; p.Y = y + Math.Sin(a) * 0.6; p.Vx = q.Vx * side + Math.Cos(a) * m; p.Vy = q.Vy + Math.Sin(a) * m * 0.7; p.G = q.G;
                p.Life = q.Life * (0.8 + _rand() * 0.4); p.Age = 0;
                p.R = q.R0 + _rand() * (q.R1 - q.R0);
                if (q.Col != null) { int k = (int)Math.Floor(_rand() * q.Col.Length); p.Col = k < q.Col.Length ? q.Col[k] : null; } else p.Col = "#fff";
                p.Kind = q.Kind; p.Ph = _rand() * TAU; p.Hop = q.Hop; p.Gy = y; p.Landed = false; p.Land = 0;
                Parts.Add(p);
            }
        }

        /// <summary>drawParts' motion: age, gravity, the hop's bounce; the dead ones dropped (order kept).</summary>
        void StepParts(double dt)
        {
            int k = 0;
            for (int i = 0; i < Parts.Count; i++)
            {
                var p = Parts[i];
                p.Age += dt * 1000;
                if (p.Age > p.Life) { _freeParts.Push(p); continue; }
                p.Vy += p.G * dt; p.X += p.Vx * dt; p.Y += p.Vy * dt;
                if (p.Hop && p.Y > p.Gy && p.Vy > 0) { p.Y = p.Gy; p.Vy = -24 * (0.85 + _rand() * 0.3); p.Land = p.Age; p.Landed = true; }
                Parts[k++] = p;
            }
            if (k < Parts.Count) Parts.RemoveRange(k, Parts.Count - k);
        }
    }
}
