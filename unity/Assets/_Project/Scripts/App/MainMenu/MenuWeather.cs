// The menu's weather, drawn the way webgame/app/menu.js draws it (weather()): one band of drops 115 units tall,
// repeated five times, on a layer that slides one band per cycle. Units are the scene's (viewBox 1000 x 460, laid on
// the screen with xMidYMax slice), so the drops sit where they do in the game on every screen shape.
// Rain: 28 streaks, the layer moves (4, -115) -> (0, 0) every 225 ms. Snow: 23 flakes, (0, -115) -> (3.5, 0) in 3.5 s.
// The drops themselves come from layout.json (menu.js's own formulas, run by the exporter).
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.MainMenu
{
    public sealed class MenuWeather : VisualElement
    {
        const float ViewW = 1000f, ViewH = 460f;

        readonly MenuLayout.WeatherSpec _spec;
        readonly bool _rain, _snow;
        readonly float[] _from, _to;
        readonly float _cycle;     // seconds
        readonly float _frozenT;   // where the reference screenshot holds the cycle (0..1)
        readonly float _refScale;  // the scene's scale on the A15, to read frozenOffset

        public MenuWeather(MenuLayout.WeatherSpec spec, string season, float refWidth, float refHeight)
        {
            _spec = spec;
            name = "weather";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;

            _rain = spec != null && season == "rainy" && spec.rain != null && spec.rain.drops != null;
            _snow = spec != null && season == "snowy" && spec.snow != null && spec.snow.flakes != null;
            // TODO(phase 2): sunny birds (menu.js: three, when the house is past f > 0.3)
            if (!_rain && !_snow) { style.display = DisplayStyle.None; return; }

            _from = _rain ? spec.rain.from : spec.snow.from;
            _to = _rain ? spec.rain.to : spec.snow.to;
            _cycle = (_rain ? spec.rain.ms : spec.snow.ms) / 1000f;
            _refScale = Mathf.Max(refWidth / ViewW, refHeight / ViewH);
            // the reference's offset, back to a point in the cycle (the y movement is the long one)
            float fy = spec.frozenOffset != null && spec.frozenOffset.Length > 1 ? spec.frozenOffset[1] : _from[1] * _refScale;
            float span = (_to[1] - _from[1]) * _refScale;
            _frozenT = Mathf.Abs(span) > 1e-3f ? Mathf.Clamp01((fy - _from[1] * _refScale) / span) : 0f;

            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            schedule.Execute(Move).Every(0); // every frame
        }

        float Scale(out float ox, out float oy)
        {
            var r = contentRect;
            float s = Mathf.Max(r.width / ViewW, r.height / ViewH);
            ox = (r.width - ViewW * s) / 2f;   // xMid
            oy = r.height - ViewH * s;         // YMax
            return s;
        }

        void Move()
        {
            if (!_rain && !_snow) return;
            float s = Scale(out _, out _);
            float t = ReferenceOverlay.FreezeAnimations ? _frozenT : (Time.realtimeSinceStartup % _cycle) / _cycle;
            float x = Mathf.Lerp(_from[0], _to[0], t) * s, y = Mathf.Lerp(_from[1], _to[1], t) * s;
            style.translate = new Translate(x, y);
        }

        void Draw(MeshGenerationContext mgc)
        {
            float s = Scale(out float ox, out float oy);
            if (s <= 0) return;
            var p = mgc.painter2D;
            float band = _spec.bandHeight;
            if (_rain)
            {
                var rain = _spec.rain;
                var c = Css.ToColor(rain.color, Color.white);
                p.lineWidth = rain.width * s;
                p.lineCap = LineCap.Butt;
                foreach (int k in _spec.bands)
                    foreach (var d in rain.drops)
                    {
                        p.strokeColor = new Color(c.r, c.g, c.b, d.opacity);
                        p.BeginPath();
                        p.MoveTo(new Vector2(ox + d.x * s, oy + (d.y + k * band) * s));
                        p.LineTo(new Vector2(ox + (d.x + rain.line[0]) * s, oy + (d.y + k * band + rain.line[1]) * s));
                        p.Stroke();
                    }
            }
            else
            {
                var snow = _spec.snow;
                var c = Css.ToColor(snow.color, Color.white);
                foreach (int k in _spec.bands)
                    foreach (var f in snow.flakes)
                    {
                        p.fillColor = new Color(c.r, c.g, c.b, f.opacity);
                        p.BeginPath();
                        p.Arc(new Vector2(ox + f.x * s, oy + (f.y + k * band) * s), f.r * s, Angle.Degrees(0f), Angle.Degrees(360f));
                        p.Fill();
                    }
            }
        }
    }
}

namespace PlushCrush.App.MainMenu
{
    /// <summary>The sunny-day birds (menu.js weather(): three, once the house is past f > 0.3). Each flies through 9
    /// points of a curve across the scene, straight between them (Web Animations keyframes, linear), in 18, 23 and
    /// 28 s, over and over. Drawn as the page draws them: "M0 0 q3 -3 6 0 q3 -3 6 0", stroke #4a4a5a, in scene units.</summary>
    public sealed class MenuBirds : VisualElement
    {
        const float ViewW = 1000f, ViewH = 460f;
        static readonly Color Ink = new Color32(0x4a, 0x4a, 0x5a, 0xff);
        readonly float _t0;

        public MenuBirds()
        {
            name = "birds";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            _t0 = Time.realtimeSinceStartup;
            generateVisualContent += Draw;
            schedule.Execute(MarkDirtyRepaint).Every(0);
        }

        /// <summary>main-menu-scene.js state(): f = min((rooms open - 1) / 7, (level - 1) / 29).</summary>
        public static float HouseF(int level)
        {
            int L = Mathf.Clamp(level, 1, 30);
            int[] opens = { 1, 2, 4, 6, 8, 11, 12, 30 }; // living, kitchen, bathroom, bedroom, backyard, dance, movie, beach
            int n = 0; foreach (int lv in opens) if (L >= lv) n++;
            return Mathf.Min((n - 1) / 7f, (L - 1) / 29f);
        }

        public static bool Shown(string season, int level) => season == "sunny" && HouseF(level) > 0.3f;

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            float s = Mathf.Max(r.width / ViewW, r.height / ViewH), ox = (r.width - ViewW * s) / 2f, oy = r.height - ViewH * s;
            float now = ReferenceOverlay.FreezeAnimations ? 0f : Time.realtimeSinceStartup - _t0;
            var p = mgc.painter2D;
            p.strokeColor = Ink;
            p.lineWidth = s;
            for (int i = 0; i < 3; i++)
            {
                Vector2 P0 = new Vector2(-40 - i * 30, 60 + i * 18), C = new Vector2(500, 20 + i * 10), P1 = new Vector2(1060, 80 + i * 12);
                float dur = 18 + i * 5, u = (now % dur) / dur * 8f; // 9 keyframes, 8 straight legs
                int k = Mathf.Min(7, Mathf.FloorToInt(u));
                Vector2 a = Bez(P0, C, P1, k * 0.125f), b = Bez(P0, C, P1, (k + 1) * 0.125f);
                Vector2 at = Vector2.LerpUnclamped(a, b, u - k);
                // the bird's box: its top-left at (ox + x*s, oy + y*s); its drawing (viewBox -1 -4 14 6) at that scale
                Vector2 o = new Vector2(ox + at.x * s + 1f * s, oy + at.y * s + 4f * s);
                p.BeginPath();
                p.MoveTo(o);
                p.QuadraticCurveTo(o + new Vector2(3, -3) * s, o + new Vector2(6, 0) * s);
                p.QuadraticCurveTo(o + new Vector2(9, -3) * s, o + new Vector2(12, 0) * s);
                p.Stroke();
            }
        }

        static Vector2 Bez(Vector2 p0, Vector2 c, Vector2 p1, float t) => (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * c + t * t * p1;
    }
}
