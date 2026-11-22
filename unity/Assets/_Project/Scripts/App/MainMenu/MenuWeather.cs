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

        public MenuWeather(MenuLayout.WeatherSpec spec, float refWidth, float refHeight)
        {
            _spec = spec;
            name = "weather";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;

            _rain = spec != null && spec.season == "rainy" && spec.rain != null && spec.rain.drops != null;
            _snow = spec != null && spec.season == "snowy" && spec.snow != null && spec.snow.flakes != null;
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
