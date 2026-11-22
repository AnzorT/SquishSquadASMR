// A move's own effect drawn over the creature (rive-rig.js FX, in its fx layer above the fabric-textured body):
// happy's eight twinkling stars, surprised's three exclamation strokes over the head. Functions of the move's time, no
// randomness; ported exactly. (sleep's floating 'z's are text: not ported yet. The game's particles - emit /
// drawParts - come with the screens that use them: no menu move emits any.)
using System;
using System.Collections.Generic;

namespace PlushCrush.Core.Rig
{
    public sealed class RigFxShape
    {
        public readonly RigPath D = new RigPath();
        public readonly RigPaint Paint = new RigPaint();
        public RigColor Color { get => Paint.Color; set => Paint.Color = value; }
        public bool Stroke; public float Width; public string Cap = "butt";
        public float Opacity = 1;
    }

    public sealed class RigFx
    {
        const double TAU = Math.PI * 2;
        public readonly List<RigFxShape> Shapes = new List<RigFxShape>();
        readonly List<RigFxShape> _pool = new List<RigFxShape>();
        static readonly RigColor White = new RigColor(1, 1, 1, 1), Pale = new RigColor(1, 0xf3 / 255f, 0xa0 / 255f, 1), Purple = new RigColor(0x8f / 255f, 0x3c / 255f, 0xf2 / 255f, 1);

        static double R2(double v) => RigGeometry.R2(v);

        RigFxShape Next()
        {
            if (Shapes.Count == _pool.Count) _pool.Add(new RigFxShape());
            var s = _pool[Shapes.Count]; s.D.Clear(); s.Stroke = false; s.Width = 0; s.Cap = "butt"; s.Opacity = 1;
            Shapes.Add(s); return s;
        }

        /// <summary>The effect of this move at t ms into it (I.cur.name, now - I.cur.t0); topW: the head's top (RigRender.TopW).</summary>
        public void Build(string move, double t, (double x, double y) topW)
        {
            Shapes.Clear();
            if (move == "happy")
                for (int i = 0; i < 8; i++)
                {
                    double a = (double)i / 8 * TAU + i * 0.7, u = (t / 900 + (double)i / 8) % 1, r = 36 * (0.55 + 0.45 * u);
                    var s = Next();
                    Star4(s.D, 50 + Math.Cos(a) * r, 52 + Math.Sin(a) * r * 0.8, 1.2 + 2.2 * Math.Sin(Math.PI * u));
                    s.Color = i % 2 != 0 ? Pale : White; s.Opacity = (float)R2(Math.Sin(Math.PI * u));
                }
            else if (move == "surprised")
            {
                double u = RigMath.Clamp((t - 300) / 700, 0, 1);
                if (u <= 0 || u >= 1) return;
                double x0 = topW.x, y0 = topW.y, aa = Math.Sin(Math.PI * u);
                foreach (var d in new[] { -34.0, 0, 34 })
                {
                    double rr = d * RigMath.D2R, sz = 6 + u * 4, x1 = x0 + Math.Sin(rr) * sz, y1 = y0 - 3 - Math.Cos(rr) * sz, x2 = x0 + Math.Sin(rr) * (sz + 5), y2 = y0 - 3 - Math.Cos(rr) * (sz + 5);
                    var s = Next(); s.D.C('M'); s.D.P(x1, y1); s.D.C('L'); s.D.P(x2, y2);
                    s.Stroke = true; s.Width = 1.6f; s.Cap = "round"; s.Color = Purple; s.Opacity = (float)R2(aa);
                }
            }
        }

        // star4(x, y, r): a four-point star of quadratic curves through its centre
        static void Star4(RigPath o, double x, double y, double r)
        {
            o.C('M'); o.P(x, y - r);
            o.C('Q'); o.P(x, y); o.P(x + r, y);
            o.C('Q'); o.P(x, y); o.P(x, y + r);
            o.C('Q'); o.P(x, y); o.P(x - r, y);
            o.C('Q'); o.P(x, y); o.P(x, y - r);
            o.C('Z');
        }
    }
}
