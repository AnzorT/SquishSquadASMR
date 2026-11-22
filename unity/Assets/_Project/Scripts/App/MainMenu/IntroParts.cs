// The intro's drawing pieces that aren't plain pictures (MenuIntro.cs puts them together):
//   SceneBox       a box laid exactly where the menu's scene picture goes (cover, centred, on the bottom); the house's
//                  pieces sit in it by percent of the scene picture's pixel grid, so they land on the A15's pixels
//   TintedPicture  a picture with a colour multiplied in, varying down the screen (UI Toolkit's vertex tint): the dawn's
//                  mix-blend-mode multiply, exactly (a multiply at opacity d = a tint of 1 - d + d * colour)
//   GradientRect   a vertical gradient (the night's veil), as a mesh with the gradient's colours at its stops
//   IntroRain      intro.js's own rain (rainBuild): two layers of drops (near, far), each a band repeated down the
//                  screen, slid one band per loop
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.MainMenu
{
    /// <summary>A box over the scene picture's place on the screen; its children by percent of the picture's grid.</summary>
    public sealed class SceneBox : VisualElement
    {
        readonly float _pw, _ph, _ss, _shift;

        public SceneBox(IntroLayout L, string name)
        {
            this.name = name;
            pickingMode = PickingMode.Ignore;
            _pw = L.ScenePixels[0]; _ph = L.ScenePixels[1]; _ss = L.SceneScale; _shift = L.Shift;
            style.position = Position.Absolute;
        }

        float _k;

        /// <summary>Placed for a screen of W x H (dp), as the menu's cover-sized scene picture.</summary>
        public void Fit(float W, float H)
        {
            float k = _k = Mathf.Max(W / _pw, H / _ph);
            style.left = (W - _pw * k) / 2f; style.top = H - _ph * k; style.width = _pw * k; style.height = _ph * k;
        }

        /// <summary>dp per picture pixel (0 until it's been fitted).</summary>
        public float K => _k;

        /// <summary>A scene unit point in this box's own coordinates (dp).</summary>
        public Vector2 Map(double x, double y) { float k = K; return new Vector2((float)((x * _ss + _shift) * k), (float)(y * _ss * k)); }
        /// <summary>Scene units to dp.</summary>
        public float Units(double u) => (float)(u * _ss * K);

        /// <summary>A child at a picture-grid box (px), by percent.</summary>
        public VisualElement Add(int[] px, Texture2D tex, string name = null, VisualElement into = null)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            PlaceAt(e, px);
            if (tex != null) { e.style.backgroundImage = new StyleBackground(tex); e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100)); }
            (into ?? this).Add(e);
            return e;
        }

        public void PlaceAt(VisualElement e, int[] px)
        {
            e.style.position = Position.Absolute;
            e.style.left = Length.Percent(px[0] / _pw * 100f); e.style.top = Length.Percent(px[1] / _ph * 100f);
            e.style.width = Length.Percent(px[2] / _pw * 100f); e.style.height = Length.Percent(px[3] / _ph * 100f);
        }
    }

    /// <summary>The dawn's colour (siDawn, a vertical gradient over the night's veil rect) at opacity d, as the tint
    /// it makes: 1 - d + d * colour, by panel y.</summary>
    public sealed class DawnTint
    {
        public float D, Y0, Y1;   // opacity; the veil rect's top and bottom (panel coordinates)
        readonly (float at, Color c)[] _stops;
        public DawnTint((float at, Color c)[] stops) { _stops = stops; }

        public IEnumerable<float> Breaks() { foreach (var s in _stops) yield return Y0 + s.at * (Y1 - Y0); }

        public Color At(float panelY)
        {
            if (D <= 0f) return Color.white;
            float f = Y1 > Y0 ? Mathf.Clamp01((panelY - Y0) / (Y1 - Y0)) : 0f;
            Color c = _stops[_stops.Length - 1].c;
            for (int i = 1; i < _stops.Length; i++)
                if (f <= _stops[i].at) { c = Color.LerpUnclamped(_stops[i - 1].c, _stops[i].c, (f - _stops[i - 1].at) / Mathf.Max(1e-6f, _stops[i].at - _stops[i - 1].at)); break; }
            return new Color(1f - D + D * c.r, 1f - D + D * c.g, 1f - D + D * c.b, 1f);
        }
    }

    /// <summary>A picture with a vertical tint multiplied in (exact between the tint's stops: the GPU interpolates the
    /// vertex colours linearly, as the gradient does).</summary>
    public sealed class TintedPicture : VisualElement
    {
        public Texture2D Texture;
        public DawnTint Tint;
        readonly List<float> _ys = new List<float>();

        public TintedPicture(Texture2D tex, DawnTint tint)
        {
            Texture = tex; Tint = tint;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (Texture == null || r.width <= 0 || r.height <= 0) return;
            var wb = worldBound;
            float toLocal = wb.height > 0 ? r.height / wb.height : 1f;
            _ys.Clear(); _ys.Add(0f); _ys.Add(r.height);
            if (Tint != null && Tint.D > 0f)
                foreach (var py in Tint.Breaks()) { float ly = (py - wb.y) * toLocal; if (ly > 0f && ly < r.height) _ys.Add(ly); }
            _ys.Sort();
            int strips = _ys.Count - 1;
            var md = mgc.Allocate(strips * 4, strips * 6, Texture);
            var uv = md.uvRegion;
            for (int i = 0; i < strips; i++)
            {
                float a = _ys[i], b = _ys[i + 1];
                Color32 ca = Tint != null ? (Color32)Tint.At(wb.y + a / toLocal) : (Color32)Color.white, cb = Tint != null ? (Color32)Tint.At(wb.y + b / toLocal) : (Color32)Color.white;
                float va = 1f - a / r.height, vb = 1f - b / r.height;
                md.SetNextVertex(new Vertex { position = new Vector3(0, a, Vertex.nearZ), tint = ca, uv = new Vector2(uv.xMin, uv.yMin + va * uv.height) });
                md.SetNextVertex(new Vertex { position = new Vector3(r.width, a, Vertex.nearZ), tint = ca, uv = new Vector2(uv.xMax, uv.yMin + va * uv.height) });
                md.SetNextVertex(new Vertex { position = new Vector3(r.width, b, Vertex.nearZ), tint = cb, uv = new Vector2(uv.xMax, uv.yMin + vb * uv.height) });
                md.SetNextVertex(new Vertex { position = new Vector3(0, b, Vertex.nearZ), tint = cb, uv = new Vector2(uv.xMin, uv.yMin + vb * uv.height) });
                ushort o = (ushort)(i * 4);
                md.SetNextIndex(o); md.SetNextIndex((ushort)(o + 1)); md.SetNextIndex((ushort)(o + 2));
                md.SetNextIndex(o); md.SetNextIndex((ushort)(o + 2)); md.SetNextIndex((ushort)(o + 3));
            }
        }
    }

    /// <summary>A rectangle filled with a vertical gradient (stops at fractions of its height).</summary>
    public sealed class GradientRect : VisualElement
    {
        readonly (float at, Color c)[] _stops;

        public GradientRect((float at, Color c)[] stops)
        {
            _stops = stops;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            int n = _stops.Length;
            var md = mgc.Allocate(n * 2, (n - 1) * 6);
            for (int i = 0; i < n; i++)
            {
                float y = _stops[i].at * r.height; Color32 c = _stops[i].c;
                md.SetNextVertex(new Vertex { position = new Vector3(0, y, Vertex.nearZ), tint = c });
                md.SetNextVertex(new Vertex { position = new Vector3(r.width, y, Vertex.nearZ), tint = c });
            }
            for (int i = 0; i < n - 1; i++)
            {
                ushort o = (ushort)(i * 2);
                md.SetNextIndex(o); md.SetNextIndex((ushort)(o + 1)); md.SetNextIndex((ushort)(o + 3));
                md.SetNextIndex(o); md.SetNextIndex((ushort)(o + 3)); md.SetNextIndex((ushort)(o + 2));
            }
        }
    }

    /// <summary>intro.js rainBuild(): near and far drops (rain: lines; snow: dots), a band of B = round(115 s) px repeated
    /// down the screen with each row shifted along the drift, the layer moved one band per loop.</summary>
    public sealed class IntroRain : VisualElement
    {
        sealed class Layer : VisualElement
        {
            public float B, DX, Ms, V; public int N, Li, Rows; public float Sw, Op; public bool Snow; public float W, S;
            public Layer() { pickingMode = PickingMode.Ignore; style.position = Position.Absolute; generateVisualContent += Draw; }

            static double H(int i, int li, int k) { double t = Math.Sin((i + li * 50) * 12.9898 + k * 78.233) * 43758.5453; return t - Math.Floor(t); }
            static float R1(double x) => (float)(Math.Floor(x * 10 + 0.5) / 10);

            void Draw(MeshGenerationContext mgc)
            {
                var p = mgc.painter2D;
                var c = Snow ? Color.white : (Color)new Color32(0xc8, 0xd6, 0xff, 0xff);
                p.strokeColor = new Color(c.r, c.g, c.b, Op);
                p.lineWidth = R1(Sw * S);
                p.lineCap = LineCap.Round;
                for (int k = 0; k < Rows; k++)
                {
                    float ox = R1(-k * B * DX), oy = k * B;
                    for (int i = 0; i < N; i++)
                    {
                        float x = R1(H(i, Li, 1) * (W + 60 * S)) + ox, y = R1(H(i, Li, 2) * B) + oy;
                        p.BeginPath();
                        p.MoveTo(new Vector2(x, y));
                        if (Snow) p.LineTo(new Vector2(x, y + 0.01f)); else p.LineTo(new Vector2(x + R1(-4 * S), y + R1(16 * S)));
                        p.Stroke();
                    }
                }
            }
        }

        readonly bool _snow, _on;
        readonly Layer[] _layers;
        float _w = -1, _h = -1;

        public IntroRain(string weather)
        {
            name = "intro-rain";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            style.overflow = Overflow.Hidden;
            _snow = weather == "snowy"; _on = weather == "rainy" || _snow;
            var spec = _snow ? new[] { (10, 2.6f, 0.85f, 70f), (8, 1.8f, 0.55f, 45f) } : new[] { (9, 1.4f, 0.6f, 900f), (7, 1.1f, 0.4f, 680f) };
            _layers = new Layer[_on ? 2 : 0];
            for (int i = 0; i < _layers.Length; i++)
            {
                var (n, sw, op, v) = spec[i];
                _layers[i] = new Layer { N = n, Li = i, Sw = sw, Op = op, Snow = _snow, V = v };
                Add(_layers[i]);
            }
            RegisterCallback<GeometryChangedEvent>(_ => Build());
        }

        void Build()
        {
            float W = layout.width, H = layout.height;
            if (!_on || W <= 0 || H <= 0 || (W == _w && H == _h)) return;
            _w = W; _h = H;
            float s = Mathf.Max(W / 1000f, H / 460f), B = Mathf.Round(115f * s), DX = _snow ? 40f / 620f : 26f / 620f;
            int rows = Mathf.CeilToInt(H / B) + 1;
            for (int i = 0; i < _layers.Length; i++)
            {
                var l = _layers[i];
                l.B = B; l.DX = DX; l.Rows = rows; l.W = W; l.S = s; l.Ms = Mathf.Round(B / (l.V * s) * 1000f);
                l.style.left = 0; l.style.top = -B; l.style.width = (float)(Math.Floor((W + 60 * s) * 10 + 0.5) / 10); l.style.height = rows * B;
                l.MarkDirtyRepaint();
            }
        }

        /// <summary>The layers' places at a time (ms since the intro started: the animations start with it).</summary>
        public void At(double ms)
        {
            foreach (var l in _layers)
            {
                if (l.Ms <= 0 || l.B <= 0) continue;
                float f = (float)(ms % l.Ms / l.Ms);
                l.style.translate = new Translate((float)(Math.Floor(-l.B * l.DX * 10 + 0.5) / 10) * f, l.B * f);
            }
        }
    }
}
