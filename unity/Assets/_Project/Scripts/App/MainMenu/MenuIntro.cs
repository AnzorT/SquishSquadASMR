// The game intro on the Main Menu ("Squish wakes the house", webgame/app/intro.js), drawn every frame from
// IntroScript's timeline with the pictures tools/unity-port/export-intro.mjs took from the game (intro.json).
//
// The layers, bottom to top, as intro.js stacks them:
//   in the world (the camera moves them): the night's house (tinted by the dawn), the morning's fading in, the night's
//   veil (a gradient), each room's lit windows (the night's house cut to them: the veil's holes), the moon, the halos
//   and panes (their blend modes baked: see export-intro.mjs), the squad peeking out (clipped by their windows), the
//   box's inside and flaps (drawn here), Mittens, the box's face and shadow, the glows, rings and sparkles, the box's
//   tap area;
//   over the screen: the rain or snow, the captions, Skip, the logo's letters, TAP TO START, the black it starts from.
//
// The menu hands it (MainMenuScreen.Intro.cs) what menu.js's api gives intro.js: the screen, the world, Mittens (the
// menu's live rig, its night filter, its moves, its touch) and Finished, when the menu takes the screen back.
//
// Known differences from the page (see unity/PORT_PLAN.md, "Game intro"):
//   - the halos' and panes' blend modes are pictures made over the house as it is at rest (each room lit at night,
//     five points of the dawn): exact at rest, close while a room lights up and between the dawn's points;
//   - a fading group (the end) multiplies each piece's opacity instead of fading the group as one picture;
//   - the peeks' window clips have no anti-aliasing; pictures that scale (the camera's drift, a caption's pop) are
//     resampled where the browser redraws its vectors.
using System;
using System.Collections.Generic;
using PlushCrush.App.Motion;
using PlushCrush.App.Rig;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.MainMenu
{
    public sealed class MenuIntro
    {
        /// <summary>Mittens, as the menu has him for the intro.</summary>
        public sealed class Star
        {
            public VisualElement Host;            // what the intro moves (holds his rig and his picture)
            public Func<Rect> StageBox;           // the rig's 100 x 100 box (.mm-stage) in Host's coordinates
            public IMenuCreature Creature;        // his moves
            public Func<RigView> Rig;             // the live rig (null while it isn't there)
            public Action<Matrix4x4?> Filter;     // the night's light (menu.js light(): a CSS filter on him)
        }

        /// <summary>Debug: play the reference run (intro.json's script: the box tapped, three squishes, TAP TO START at
        /// the reference frames' times), held at a moment (ms), skipped at a moment.</summary>
        public sealed class Options { public bool Scripted; public double? HoldAt; public double? SkipAt; }

        public event Action<string> Phase;   // "start", "awake" (the third squish: the app's music), "done"
        public event Action Finished;        // the menu takes the screen back (api.finish)
        public bool Running => !_script.Dead;
        public IntroScript Script => _script;

        static readonly string[] Rooms = IntroScript.Rooms;
        const float HaloAlpha = 0.5f, PaneAlpha = 1f;   // the crossfades' typical alpha (export-intro.mjs pair())

        readonly IntroLayout _L;
        readonly IntroLayout.Season _S;
        readonly IntroScript _script;
        readonly Options _opt;
        readonly VisualElement _view, _world;
        readonly Star _star;
        readonly DawnTint _tint;

        // world
        SceneBox _low, _high, _back, _front;
        TintedPicture _sa, _door, _sb;
        readonly List<TintedPicture> _tinted = new List<TintedPicture>();
        VisualElement _veilBox; GradientRect _veil;
        readonly Dictionary<string, TintedPicture> _cuts = new Dictionary<string, TintedPicture>();
        readonly Dictionary<string, VisualElement[]> _halos = new Dictionary<string, VisualElement[]>(), _panes = new Dictionary<string, VisualElement[]>();
        readonly Dictionary<string, VisualElement> _plain = new Dictionary<string, VisualElement>();
        VisualElement _moon, _face, _shadow, _hit, _stageEl, _starHit, _fx;
        VisualElement[] _peekClip, _peekPic;
        readonly List<VisualElement> _glows = new List<VisualElement>();
        // screen
        IntroRain _rain;
        VisualElement _ov, _cap, _capPic, _skip, _tap, _tapPic, _black;
        VisualElement[] _letters;
        RigTouch _touch;

        readonly CssTransition _capOpacity = new CssTransition(0f, 0.45f, 0f, "ease"), _capScale = new CssTransition(1f, 0.3f, 0f, "cubic-bezier(.3,1.6,.5,1)");
        readonly CssTransition _blackOpacity = new CssTransition(1f, 1.4f, 0f, "ease"), _tapOpacity = new CssTransition(0f, 0.6f, 0f, "ease");
        readonly CssAnimation _ring, _ring2, _pulse;
        string _capKey;
        double _tapBorn = -1;
        double _t0 = -1;
        bool _finishing;
        readonly List<(double t, Action a)> _inputs = new List<(double, Action)>();
        IVisualElementScheduledItem _tick;

        public MenuIntro(VisualElement view, VisualElement world, VisualElement starBefore, Star star, string weather, IntroLayout layout, Options options = null)
        {
            _view = view; _world = world; _star = star; _L = layout; _opt = options ?? new Options();
            if (!_L.Seasons.TryGetValue(weather, out _S)) { Debug.LogWarning("[PlushCrush] Intro: no pictures for " + weather + " (export-intro.mjs --seasons " + weather + "); rainy instead"); weather = "rainy"; _S = _L.Seasons[weather]; }
            _script = new IntroScript(weather, () => UnityEngine.Random.value);
            _tint = new DawnTint(_L.Dawn);
            _ring = Ring(0f); _ring2 = Ring(0.62f);
            _pulse = new CssAnimation { Duration = 1.3f, Iterations = float.PositiveInfinity, Timing = Easing.EaseInOut };
            _pulse.Key(new CssKeyframe(0f).Scale(1f)).Key(new CssKeyframe(0.5f).Scale(1.09f)).Key(new CssKeyframe(1f).Scale(1f));
            BuildWorld(starBefore);
            BuildScreen(weather);
            if (_opt.Scripted)
            {
                _inputs.Add((_L.ScriptTapBox, () => _script.TapBox()));
                foreach (var s in _L.ScriptSquish) { _inputs.Add((s[0], SquishDown)); _inputs.Add((s[1], SquishUp)); }
                _inputs.Add((_L.ScriptStart, () => _script.TapToStart()));
            }
            if (_opt.SkipAt.HasValue) { _inputs.RemoveAll(i => i.t >= _opt.SkipAt.Value); _inputs.Add((_opt.SkipAt.Value, Skip)); }
            _inputs.Sort((a, b) => a.t.CompareTo(b.t));
            _view.RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            _tick = _view.schedule.Execute(Update).Every(0);
            Warm();
        }

        static CssAnimation Ring(float delay)
        {
            // siRing: 0% scale .55, opacity .95 -> 100% scale 1.5, opacity 0; 1.25 s ease-out, infinite
            var a = new CssAnimation { Duration = 1.25f, Delay = delay, Iterations = float.PositiveInfinity, Timing = Easing.EaseOut };
            a.Key(new CssKeyframe(0f).Scale(0.55f).Fade(0.95f)).Key(new CssKeyframe(1f).Scale(1.5f).Fade(0f));
            return a;
        }

        // ---------------------------------------------------------------- building

        static VisualElement Full(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute; e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0;
            return e;
        }

        TintedPicture Tinted(SceneBox box, IntroLayout.Piece p, Texture2D tex = null)
        {
            var t = new TintedPicture(tex ?? p.Texture, _tint);
            box.PlaceAt(t, p.Px);
            box.Add(t); _tinted.Add(t);
            return t;
        }

        void BuildWorld(VisualElement starBefore)
        {
            int at = starBefore != null && starBefore.parent == _world ? _world.IndexOf(starBefore) : _world.childCount;
            var full = new IntroLayout.Piece { Image = _S.Night, Px = new[] { 0, 0, _L.ScenePixels[0], _L.ScenePixels[1] } };
            // the night's house, the door's light, the morning's house
            _low = new SceneBox(_L, "intro-house");
            _sa = Tinted(_low, full);
            if (_S.Doorlight != null) { _door = Tinted(_low, _S.Doorlight); _door.style.opacity = 0; }
            _sb = Tinted(_low, full, _S.Morning != null ? IntroLayout.Load(_S.Morning) : full.Texture); _sb.style.opacity = 0;
            // the night's veil (veilFit: over the whole screen and a unit more)
            _veilBox = Full("intro-veil");
            _veil = new GradientRect(_L.Veil);
            _veilBox.Add(_veil);
            // the rooms' lit windows, the moon, the halos and panes, the squad peeking out
            _high = new SceneBox(_L, "intro-lights");
            foreach (var r in Rooms) if (_S.Cuts.TryGetValue(r, out var c) && c != null) { _cuts[r] = Tinted(_high, c); _cuts[r].style.opacity = 0; }
            _moon = _high.Add(_L.Moon.Px, _L.Moon.Texture, "moon");
            foreach (var (src, dst) in new[] { (_S.Halos, _halos), (_S.Panes, _panes) })
                foreach (var r in Rooms)
                {
                    var ps = src[r]; var es = new VisualElement[ps.Length];
                    for (int i = 0; i < ps.Length; i++) if (ps[i] != null) { es[i] = _high.Add(ps[i].Px, ps[i].Texture); es[i].style.visibility = Visibility.Hidden; }
                    dst[r] = es;
                }
            foreach (var r in Rooms)
            {
                if (_L.PlainHalos.TryGetValue(r, out var ph) && ph != null) { var e = _high.Add(ph.Px, ph.Texture); e.style.display = DisplayStyle.None; _plain[r + "/halo"] = e; }
                if (_L.PlainPanes.TryGetValue(r, out var pp) && pp != null) { var e = _high.Add(pp.Px, pp.Texture); e.style.display = DisplayStyle.None; _plain[r + "/pane"] = e; }
            }
            _peekClip = new VisualElement[_L.Peeks.Length]; _peekPic = new VisualElement[_L.Peeks.Length];
            for (int i = 0; i < _L.Peeks.Length; i++)
            {
                var clip = new VisualElement { name = "peek-" + _L.Peeks[i].Key, pickingMode = PickingMode.Ignore };
                clip.style.position = Position.Absolute; clip.style.overflow = Overflow.Hidden; clip.style.display = DisplayStyle.None;
                var pic = new VisualElement { pickingMode = PickingMode.Ignore };
                pic.style.position = Position.Absolute;
                pic.style.backgroundImage = new StyleBackground(IntroLayout.Load(_L.Peeks[i].Pic.Image));
                pic.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                clip.Add(pic); _high.Add(clip);
                _peekClip[i] = clip; _peekPic[i] = pic;
            }
            // the box's inside and flaps (behind Mittens)
            _back = new SceneBox(_L, "intro-box-back");
            _back.generateVisualContent += DrawBoxBack;
            // the box's face, its shadow, the glows, rings and sparkles, the box's tap area (in front of him)
            _front = new SceneBox(_L, "intro-front");
            _shadow = _front.Add(_L.BoxShadow.Px, _L.BoxShadow.Texture, "box-shadow"); _shadow.style.opacity = 0;
            _face = _front.Add(_L.BoxFace.Px, _L.BoxFace.Texture, "box-face");
            _fx = Full("intro-fx"); _fx.generateVisualContent += DrawFx; _front.Add(_fx);
            _hit = new VisualElement { name = "intro-box-hit" };
            _hit.style.position = Position.Absolute; _hit.style.display = DisplayStyle.None;
            foreach (var corner in new Action<float>[] { v => _hit.style.borderTopLeftRadius = v, v => _hit.style.borderTopRightRadius = v, v => _hit.style.borderBottomLeftRadius = v, v => _hit.style.borderBottomRightRadius = v }) corner(18f);
            _hit.RegisterCallback<ClickEvent>(e => { _script.TapBox(); e.StopPropagation(); });
            _front.Add(_hit);

            _world.Insert(at, _back);
            _world.Insert(at, _high);
            _world.Insert(at, _veilBox);
            _world.Insert(at, _low);
            int si = _star.Host.parent == _world ? _world.IndexOf(_star.Host) + 1 : _world.childCount;
            _world.Insert(si, _front);

            // Mittens: hidden until he pops out; his touch (the rig's own handlers, then the intro's)
            _star.Host.style.opacity = 0;
            _stageEl = new VisualElement { name = "intro-stage", pickingMode = PickingMode.Ignore };
            _stageEl.style.position = Position.Absolute;
            _starHit = new EllipseHit { name = "intro-mittens-hit" };
            _starHit.style.position = Position.Absolute;
            _starHit.style.left = Length.Percent(22); _starHit.style.top = Length.Percent(20); _starHit.style.width = Length.Percent(56); _starHit.style.height = Length.Percent(78);
            _stageEl.Add(_starHit);
            _star.Host.Add(_stageEl);
            _touch = new RigTouch(_starHit, _stageEl, _star.Rig);
            _touch.Down += SquishDownAfterRig;
            _touch.Up += () => _script.SquishUp();
            _touch.Cancel += () => _script.SquishCancel();
        }

        void BuildScreen(string weather)
        {
            int wi = _world.parent == _view ? _view.IndexOf(_world) + 1 : 0;
            _rain = new IntroRain(weather);
            _view.Insert(wi, _rain);
            _ov = Full("intro-overlay");
            _cap = new VisualElement { name = "intro-caption", pickingMode = PickingMode.Ignore };
            _cap.style.position = Position.Absolute; _cap.style.opacity = 0;
            _capPic = new VisualElement { pickingMode = PickingMode.Ignore }; _capPic.style.position = Position.Absolute;
            _capPic.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _cap.Add(_capPic);
            // the logo (under the caption, as intro.js inserts it), the caption, Skip, TAP TO START
            _letters = new VisualElement[_L.Logo.Length];
            for (int j = 0; j < _letters.Length; j++) { _letters[j] = Overlay(_L.Logo[j], "top-center", "letter-" + j); _letters[j].style.display = DisplayStyle.None; _letters[j].style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(96)); _ov.Add(_letters[j]); }
            _ov.Add(_cap);
            _skip = Overlay(_L.Skip, "top-right", "intro-skip");
            _skip.pickingMode = PickingMode.Position;
            _skip.RegisterCallback<ClickEvent>(e => { Skip(); e.StopPropagation(); });
            _ov.Add(_skip);
            _tap = Overlay(_L.Tap, "bottom-center", "intro-tap");
            _tapPic = _tap[0];
            _tap.style.display = DisplayStyle.None; _tap.style.opacity = 0;
            var sp = _L.Tap.Span; var tb = _L.Tap.Box;
            _tapPic.style.transformOrigin = new TransformOrigin(new Length(sp[0] + sp[2] / 2f - (_L.Tap.Px[0] / Kpx)), new Length(sp[1] + sp[3] / 2f - (_L.Tap.Px[1] / Kpx)));
            _ov.Add(_tap);
            _ov.RegisterCallback<ClickEvent>(_ => { if (_script.TapToStartArmed) _script.TapToStart(); });
            _view.Add(_ov);
            _black = Full("intro-black");
            _black.style.backgroundColor = (Color)new Color32(0x05, 0x06, 0x0f, 0xff);
            _view.Add(_black);
        }

        const float Kpx = 2.8125f;   // the A15's px per dp (the overlay pieces' px)

        /// <summary>An overlay piece in its element's box (dp, kept to the named screen edges), its picture inside.</summary>
        VisualElement Overlay(IntroLayout.Piece p, string anchor, string name)
        {
            var b = p.Box ?? new[] { p.Px[0] / Kpx, p.Px[1] / Kpx, p.Px[2] / Kpx, p.Px[3] / Kpx };
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Place(e, anchor, b[0], b[1], b[2], b[3]);
            var pic = new VisualElement { pickingMode = PickingMode.Ignore };
            pic.style.position = Position.Absolute;
            pic.style.left = p.Px[0] / Kpx - b[0]; pic.style.top = p.Px[1] / Kpx - b[1];
            pic.style.width = p.Px[2] / Kpx; pic.style.height = p.Px[3] / Kpx;
            pic.style.backgroundImage = new StyleBackground(p.Texture);
            pic.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            e.Add(pic);
            return e;
        }

        // the A15's 832 x 384 dp, kept to the same edges on other screens (MainMenuScreen.Place)
        static void Place(VisualElement e, string anchor, float x, float y, float w, float h)
        {
            const float W = 832f, H = 384f;
            e.style.position = Position.Absolute; e.style.width = w; e.style.height = h;
            var a = anchor.Split('-');
            if (a[0] == "bottom") e.style.bottom = H - (y + h); else e.style.top = y;
            if (a[1] == "right") e.style.right = W - (x + w);
            else if (a[1] == "center") { e.style.left = Length.Percent(50); e.style.marginLeft = x - W / 2f; }
            else e.style.left = x;
        }

        void Relayout()
        {
            float W = _view.layout.width, H = _view.layout.height;
            if (!(W > 0) || !(H > 0)) return;
            foreach (var b in new[] { _low, _high, _back, _front }) b.Fit(W, H);
            // the veil: veilFit (the screen in scene units, a unit more each side, rounded to tenths)
            float s = Mathf.Max(W / 1000f, H / 460f), ox = (W - 1000f * s) / 2f, oy = H - 460f * s, w = W / s, h = H / s;
            float vx = R1(500 - w / 2 - 1), vy = R1(460 - h - 1);
            _veil.style.left = ox + vx * s; _veil.style.top = oy + vy * s; _veil.style.width = R1(w + 2) * s; _veil.style.height = R1(h + 2) * s;
            _veil.MarkDirtyRepaint();
            PlaceInBoxes();
        }

        static float R1(float v) => Mathf.Floor(v * 10f + 0.5f) / 10f;

        // what's placed in scene units inside the boxes (dp, from their size)
        void PlaceInBoxes()
        {
            if (_front.K <= 0) return;
            var p = _front.Map(IntroScript.FX0 - 16, IntroScript.FTOP - 40);
            _hit.style.left = p.x; _hit.style.top = p.y; _hit.style.width = _front.Units(IntroScript.BW + 32); _hit.style.height = _front.Units(IntroScript.BH + 52);
            for (int i = 0; i < _L.Peeks.Length; i++)
            {
                var pk = _L.Peeks[i]; var b = IntroScript.WIN[pk.Room][pk.Wi]; var bx = b.Box; var clip = _peekClip[i];
                var at = _high.Map(bx.x, bx.y);
                clip.style.left = at.x; clip.style.top = at.y; clip.style.width = _high.Units(bx.w); clip.style.height = _high.Units(bx.h);
                float rx = b.K == 'r' ? _high.Units(1.5) : 0f, top = b.K == 'a' ? _high.Units(bx.w / 2) : b.K == 'c' ? _high.Units(bx.w / 2) : rx, bottom = b.K == 'c' ? top : rx;
                clip.style.borderTopLeftRadius = top; clip.style.borderTopRightRadius = top; clip.style.borderBottomLeftRadius = bottom; clip.style.borderBottomRightRadius = bottom;
                var pic = _peekPic[i];
                pic.style.width = pk.Pic.Px[2] * _high.K; pic.style.height = pk.Pic.Px[3] * _high.K;
            }
            var box = _star.StageBox();
            _stageEl.style.left = box.x; _stageEl.style.top = box.y; _stageEl.style.width = box.width; _stageEl.style.height = box.height;
            foreach (var t in _tinted) t.MarkDirtyRepaint();
            _back.MarkDirtyRepaint(); _fx.MarkDirtyRepaint();
        }

        /// <summary>.mm-hit: an ellipse in the stage box (left 22%, right 22%, top 20%, bottom 2%, border-radius 50%).</summary>
        sealed class EllipseHit : VisualElement
        {
            public override bool ContainsPoint(Vector2 p)
            {
                var r = contentRect; if (r.width <= 0 || r.height <= 0) return false;
                float x = (p.x - r.center.x) / (r.width / 2), y = (p.y - r.center.y) / (r.height / 2);
                return x * x + y * y <= 1f;
            }
        }

        // ---------------------------------------------------------------- input

        /// <summary>Skip (or the phone's back): the end, at once.</summary>
        public void Skip() { if (!_script.Dead) _script.Finish(); }

        // the intro's own pointerdown, after the rig's: the squeeze starts at once, so a quick tap is a squish too
        void SquishDownAfterRig()
        {
            if (_script.SquishOpen && !_script.Flying)
            {
                var rig = _star.Rig?.Invoke();
                if (rig != null) { rig.Engine.Press = true; rig.Engine.PressT = RigView.Clock; }
            }
            _script.SquishDown();
        }

        // the scripted run's squishes: the rig's handlers too, as a finger would
        void SquishDown() { var rig = _star.Rig?.Invoke(); rig?.Engine.PointerDown(RigView.Clock); SquishDownAfterRig(); }
        void SquishUp() { var rig = _star.Rig?.Invoke(); rig?.Engine.PointerUp(RigView.Clock); _script.SquishUp(); }

        // ---------------------------------------------------------------- every frame

        void Update()
        {
            double real = Time.realtimeSinceStartupAsDouble;
            if (_t0 < 0) _t0 = real;
            if (_finishing) return;
            double target = (real - _t0) * 1000.0;
            if (_opt.HoldAt.HasValue)
            {
                // fast-forward to the moment in the page's 16 ms frames, then hold it
                IntroSynth.Muted = true;
                double t = _script.Now;
                while (t < _opt.HoldAt.Value && !_script.Dead) { t = Math.Min(_opt.HoldAt.Value, Math.Floor(t / 16 + 1) * 16); StepTo(t); Drain(); }
                IntroSynth.Muted = false;
            }
            else StepTo(target);
            Apply();
            if (_script.Dead && !_finishing) Finish();
        }

        void StepTo(double t)
        {
            while (_inputs.Count > 0 && _inputs[0].t <= t)
            {
                var (at, a) = _inputs[0]; _inputs.RemoveAt(0);
                _script.Timers(at); a();
            }
            _script.Step(t);
        }

        void Drain()
        {
            foreach (var m in _script.Moves) _star.Creature.Play(m);
            _script.Moves.Clear();
            foreach (var s in _script.Sounds) IntroSynth.Play(s);
            _script.Sounds.Clear();
            foreach (var p in _script.Phases) Phase?.Invoke(p);
            _script.Phases.Clear();
        }

        void Apply()
        {
            Drain();
            var S = _script; float now = (float)(S.Now / 1000.0);
            float W = _view.layout.width, H = _view.layout.height;
            // camera
            _world.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(64));
            _world.style.scale = new Scale(new Vector3((float)S.WorldScale, (float)S.WorldScale, 1));
            _world.style.translate = new Translate(0, (float)(S.WorldScale * S.WorldTy / 100.0 * (H > 0 ? H : 384)));
            _blackOpacity.Set(S.BlackOff ? 0f : 1f, now); _black.style.opacity = _blackOpacity.Value1(now);
            // the caption
            if (S.Caption != _capKey && S.Caption != null && _L.Captions.TryGetValue(S.Caption, out var cp))
            {
                _capKey = S.Caption;
                var b = cp.Box; Place(_cap, "top-center", b[0], b[1], b[2], b[3]);
                _capPic.style.left = cp.Px[0] / Kpx - b[0]; _capPic.style.top = cp.Px[1] / Kpx - b[1]; _capPic.style.width = cp.Px[2] / Kpx; _capPic.style.height = cp.Px[3] / Kpx;
                _capPic.style.backgroundImage = new StyleBackground(cp.Texture);
                _cap.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            }
            _capOpacity.Set(S.CaptionOn ? 1f : 0f, now); _cap.style.opacity = _capOpacity.Value1(now);
            _capScale.Set(S.CaptionPop ? 1.1f : 1f, now); float cs = _capScale.Value1(now); _cap.style.scale = new Scale(new Vector3(cs, cs, 1));
            // the box
            _face.style.display = S.BoxShown ? DisplayStyle.Flex : DisplayStyle.None;
            _shadow.style.display = _face.style.display;
            _shadow.style.opacity = (float)S.ShadowOpacity;
            if (_front.K > 0)
            {
                var o = _front.Map(IntroScript.BX, IntroScript.BY); float k = _front.K;
                _face.style.transformOrigin = new TransformOrigin(new Length(o.x - _L.BoxFace.Px[0] * k), new Length(o.y - _L.BoxFace.Px[1] * k));
                _face.style.translate = new Translate(0, _front.Units(S.BoxDy));
                _face.style.rotate = new Rotate(Angle.Degrees((float)S.BoxRot));
                _face.style.scale = new Scale(new Vector3((float)S.BoxSx, (float)S.BoxSy, 1));
            }
            _face.style.opacity = (float)S.BoxOpacity;
            _back.style.opacity = (float)S.BoxOpacity;
            _back.style.display = S.BoxShown ? DisplayStyle.Flex : DisplayStyle.None;
            _back.MarkDirtyRepaint();
            _hit.style.display = S.HitShown ? DisplayStyle.Flex : DisplayStyle.None;
            // the night, the dawn, the rooms
            float n = S.NightShown ? (float)S.Night : 0f;
            _veilBox.style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _veil.style.opacity = n;
            var vb = _veil.worldBound;
            float d = S.NightShown ? (float)S.Dawn : 0f;
            if (d != _tint.D || vb.y != _tint.Y0 || vb.yMax != _tint.Y1) { _tint.D = d; _tint.Y0 = vb.y; _tint.Y1 = vb.yMax; foreach (var t in _tinted) t.MarkDirtyRepaint(); }
            _sb.style.opacity = (float)S.SceneB;
            if (_door != null) _door.style.opacity = (float)S.DoorLight;
            _moon.style.opacity = S.NightShown ? (float)S.Moon : 0f;
            float tDawn = S.NightShown ? 1f - n / 0.8f : 1f;
            var keys = _S.DawnKeys; int ka = 0;
            while (ka < keys.Length - 2 && tDawn > keys[ka + 1]) ka++;
            float wk = Mathf.Clamp01((tDawn - keys[ka]) / (keys[ka + 1] - keys[ka]));
            foreach (var r in Rooms)
            {
                if (_cuts.TryGetValue(r, out var c)) c.style.opacity = (float)S.Hole[r];
                Crossfade(_halos[r], ka, wk, (float)S.Halo[r], HaloAlpha);
                Crossfade(_panes[r], ka, wk, (float)S.Pane[r], PaneAlpha);
            }
            // Mittens
            var host = _star.Host; var box = _star.StageBox();
            host.style.opacity = (float)S.StageOpacity;
            host.style.transformOrigin = new TransformOrigin(new Length(box.x + 0.5f * box.width), new Length(box.y + 0.96f * box.height));
            host.style.translate = new Translate(0, (float)(S.StageTy / 100.0 * box.height));
            host.style.scale = new Scale(new Vector3((float)S.StageScale, (float)S.StageScale, 1));
            var f = IntroScript.Filter(S.Light);
            _star.Filter?.Invoke(f.HasValue ? CssFilter.Matrix((float)f.Value.brightness, (float)f.Value.saturate, (float)f.Value.sepia) : (Matrix4x4?)null);
            _starHit.pickingMode = S.StageOpacity > 0 ? PickingMode.Position : PickingMode.Ignore;
            // the squad
            if (S.PeeksOn)
            {
                var pks = IntroScript.Peeks();
                for (int i = 0; i < _peekClip.Length; i++)
                {
                    var pk = _L.Peeks[i]; var (sz, cx, y0, y1, baseY) = pks[i]; var bx = IntroScript.WIN[pk.Room][pk.Wi].Box;
                    _peekClip[i].style.display = DisplayStyle.Flex;
                    // the picture's top-left (scene units) when the image is at y, relative to the window's box
                    double dx = (pk.Pic.Px[0] - (pk.At[0] * _L.SceneScale + _L.Shift)) / _L.SceneScale, dy = (pk.Pic.Px[1] - pk.At[1] * _L.SceneScale) / _L.SceneScale;
                    double left = cx - sz / 2 + dx, top0 = y0 + dy, top = S.PeekY[i] + dy;
                    var pic = _peekPic[i];
                    pic.style.left = _high.Units(left - bx.x); pic.style.top = _high.Units(top0 - bx.y);
                    float shift = _high.Units(top - top0);
                    pic.style.translate = new Translate(0, shift);
                    pic.style.transformOrigin = new TransformOrigin(new Length(_high.Units(cx - left)), new Length(_high.Units(baseY - top0) - shift));
                    pic.style.rotate = new Rotate(Angle.Degrees((float)S.PeekRot[i]));
                }
            }
            // the logo, TAP TO START, Skip
            if (S.LogoOn)
            {
                float k = (H > 0 ? H : 384f) / 393f;   // the overlay's design px -> dp
                for (int j = 0; j < _letters.Length; j++)
                {
                    var e = _letters[j]; e.style.display = DisplayStyle.Flex;
                    e.style.translate = new Translate(0, (float)S.LetterY[j] * k);
                    e.style.scale = new Scale(new Vector3((float)S.LetterSx[j], (float)S.LetterSy[j], 1));
                    e.style.rotate = new Rotate(Angle.Degrees((float)S.LetterRot[j]));
                }
            }
            if (S.TapOn && _tapBorn < 0) { _tapBorn = S.Now; _tap.style.display = DisplayStyle.Flex; }
            if (_tapBorn >= 0)
            {
                _tapOpacity.Set(S.TapOn ? 1f : 0f, now); _tap.style.opacity = _tapOpacity.Value1(now);
                if (_pulse.Sample((float)((S.Now - _tapBorn) / 1000.0), CssPose.Identity, out var ps)) _tapPic.style.scale = new Scale(new Vector3(ps.ScaleX, ps.ScaleY, 1));
            }
            _ov.pickingMode = S.TapToStartArmed ? PickingMode.Position : PickingMode.Ignore;
            _skip.style.display = S.SkipShown ? DisplayStyle.Flex : DisplayStyle.None;
            // the effects, the weather
            PlaceGlows();
            _fx.MarkDirtyRepaint();
            _rain.At(S.Now);
        }

        // the night's and the next point's pictures: the later at g w, the earlier at g (1 - w) / (1 - g w a)
        static void Crossfade(VisualElement[] es, int ka, float w, float g, float a)
        {
            g = Mathf.Min(1f, g);
            float o2 = g * w, o1 = g * (1 - w) / Mathf.Max(1e-6f, 1 - o2 * a);
            for (int i = 0; i < es.Length; i++)
            {
                if (es[i] == null) continue;
                float o = i == ka ? o1 : i == ka + 1 ? o2 : 0f;
                es[i].style.visibility = o > 0 ? Visibility.Visible : Visibility.Hidden;   // (not display: no layout every frame)
                es[i].style.opacity = o;
            }
        }

        void PlaceGlows()
        {
            var sp = _script.Sparks;
            while (_glows.Count < sp.Count)
            {
                var g = new VisualElement { pickingMode = PickingMode.Ignore };
                g.style.position = Position.Absolute; g.style.backgroundImage = new StyleBackground(_L.Glow.Texture);
                g.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                _front.Insert(_front.IndexOf(_fx), g); _glows.Add(g);
            }
            float k = _front.K;
            for (int i = 0; i < _glows.Count; i++)
            {
                var g = _glows[i];
                if (i >= sp.Count || k <= 0) { g.style.display = DisplayStyle.None; continue; }
                var at = _front.Map(sp[i].X, sp[i].Y); var c = _front.Map(_L.Glow.At[0], _L.Glow.At[1]);
                g.style.display = DisplayStyle.Flex;
                g.style.left = at.x - c.x + _L.Glow.Px[0] * k; g.style.top = at.y - c.y + _L.Glow.Px[1] * k;
                g.style.width = _L.Glow.Px[2] * k; g.style.height = _L.Glow.Px[3] * k;
            }
        }

        // ---------------------------------------------------------------- drawn here

        // flaps(o) under boxT and the box's filter (its colours, filtered by the exporter)
        void DrawBoxBack(MeshGenerationContext mgc)
        {
            var S = _script; if (_back.K <= 0) return;
            var p = mgc.painter2D;
            double a = S.BoxRot * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
            Vector2 T((double x, double y) q)
            {
                double X = (q.x - IntroScript.BX) * S.BoxSx, Y = (q.y - IntroScript.BY) * S.BoxSy;
                return _back.Map(IntroScript.BX + ca * X - sa * Y, IntroScript.BY + S.BoxDy + sa * X + ca * Y);
            }
            void Poly((double x, double y)[] pts)
            {
                p.BeginPath(); p.MoveTo(T(pts[0]));
                for (int i = 1; i < pts.Length; i++) p.LineTo(T(pts[i]));
                p.ClosePath();
            }
            var shapes = IntroScript.FlapShapes(S.Flaps);
            float io = (float)S.InsideOpacity;
            if (io > 0) { p.fillColor = new Color(_L.BoxInside.r, _L.BoxInside.g, _L.BoxInside.b, io); Poly(shapes[0]); p.Fill(); }
            p.lineJoin = LineJoin.Round;
            p.lineWidth = _back.Units(_L.StrokeWidth) * Mathf.Sqrt((float)Math.Abs(S.BoxSx * S.BoxSy));
            p.strokeColor = _L.Stroke;
            for (int i = 1; i <= 2; i++) { p.fillColor = i == 1 ? _L.FlapL : _L.FlapR; Poly(shapes[i]); p.Fill(); p.Stroke(); }
        }

        static readonly Color RingC = new Color32(0xff, 0xe8, 0xa0, 0xff), PuffC = new Color32(0x8f, 0x8a, 0xa6, 0xff), SparkC = new Color32(0xff, 0xfb, 0xe6, 0xff), TrailA = new Color32(0xff, 0xe0, 0x8a, 0xff);

        // the tap rings, the bursts, puffs and sparks (intro.js burst(), puff(), spark())
        void DrawFx(MeshGenerationContext mgc)
        {
            var S = _script; var box = _front; if (box.K <= 0) return;
            var p = mgc.painter2D; double now = S.Now;
            // a 4-point star: M X Y-1.4r Q X Y X+1.4r Y ... (every curve's control point the centre), turned by rot degrees
            void Star4(double X, double Y, double r, double rot)
            {
                double a = rot * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
                Vector2 P(double dx, double dy) => box.Map(X + c * dx - s * dy, Y + s * dx + c * dy);
                var C = box.Map(X, Y);
                p.BeginPath(); p.MoveTo(P(0, -r * 1.4));
                p.QuadraticCurveTo(C, P(r * 1.4, 0)); p.QuadraticCurveTo(C, P(0, r * 1.4)); p.QuadraticCurveTo(C, P(-r * 1.4, 0)); p.QuadraticCurveTo(C, P(0, -r * 1.4));
                p.ClosePath();
            }
            // a rounded rect w x h (corner rx) centred on (X, Y), turned by rot degrees about its centre
            void RoundRect(double X, double Y, double w, double h, double rx, double rot)
            {
                double a = rot * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
                Vector2 P(double dx, double dy) => box.Map(X + c * dx - s * dy, Y + s * dx + c * dy);
                double hw = w / 2, hh = h / 2; float R = box.Units(Math.Min(rx, Math.Min(hw, hh)));
                p.BeginPath(); p.MoveTo(P(-hw + rx, -hh));
                p.ArcTo(P(hw, -hh), P(hw, hh), R); p.ArcTo(P(hw, hh), P(-hw, hh), R); p.ArcTo(P(-hw, hh), P(-hw, -hh), R); p.ArcTo(P(-hw, -hh), P(hw, -hh), R);
                p.ClosePath();
            }
            if (S.RingOn)
                foreach (var ring in new[] { _ring, _ring2 })
                    if (ring.Sample((float)(now / 1000.0), CssPose.Identity, out var rp) && rp.Opacity > 0)
                    {
                        p.strokeColor = new Color(RingC.r, RingC.g, RingC.b, rp.Opacity); p.lineWidth = box.Units(3 * rp.ScaleX);
                        p.BeginPath(); p.Arc(box.Map(S.RingX, S.RingY), box.Units(S.RingR * rp.ScaleX), Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                    }
            foreach (var b in S.Bursts)
            {
                double t = (now - b.T0) / b.Life, k = t * b.Life / 1000; if (t < 0 || t >= 1) continue;
                float o = (float)Math.Round(1 - t * t, 2);
                foreach (var q in b.Ps)
                {
                    double X = b.X + q.Vx * k, Y = b.Y + q.Vy * k + 300 * k * k;
                    var col = Css.ToColor(q.C, Color.white); p.fillColor = new Color(col.r, col.g, col.b, o);
                    if (q.Sh == 0) { p.BeginPath(); p.Arc(box.Map(X, Y), box.Units(q.R), Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); }
                    else if (q.Sh == 1) { RoundRect(X, Y, q.R * 2, q.R * 1.2, q.R * 0.6, q.Rot + t * 500); p.Fill(); }
                    else { Star4(X, Y, q.R, 0); p.Fill(); }
                }
            }
            foreach (var pf in S.Puffs)
            {
                double t = (now - pf.T0) / 700; if (t < 0 || t >= 1) continue;
                p.fillColor = new Color(PuffC.r, PuffC.g, PuffC.b, (float)Math.Round((1 - t) * 0.5, 2));
                foreach (int sg in new[] { -1, 1 })
                    for (int i = 0; i < 3; i++)
                    {
                        double X = pf.X + sg * (36 + i * 24) * IntroScript.EaseOut(t), R = (9 + i * 4) * (0.6 + t * 0.8);
                        p.BeginPath(); p.Arc(box.Map(X, pf.Y - 4 - i * 2 - t * 9), box.Units(R), Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                    }
            }
            foreach (var sp in S.Sparks)
            {
                int n = sp.Trail.Count;
                for (int i = 0; i < n; i++)
                {
                    double k = (i + 1) / (double)n; var c = i % 3 != 0 ? TrailA : Color.white;
                    p.fillColor = new Color(c.r, c.g, c.b, (float)Math.Round(k * 0.85, 2));
                    var q = sp.Trail[i];
                    p.BeginPath(); p.Arc(box.Map(q.x + Math.Sin(i * 2.3 + sp.T * 20) * 2.5, q.y + Math.Cos(i * 1.7 + sp.T * 17) * 2), box.Units(0.8 + k * 2.4), Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                }
                p.fillColor = SparkC; Star4(sp.X, sp.Y, 5.5, sp.T * 360); p.Fill();
            }
        }

        // ---------------------------------------------------------------- the end

        // intro.js finish(): Mittens and the camera as the menu has them, api.finish() (the menu draws its own scene under
        // the intro, its creature, its HUD), then the intro's layers fade (.7 s ease) and go
        void Finish()
        {
            _finishing = true;
            Drain();
            var host = _star.Host;
            host.style.opacity = StyleKeyword.Null; host.style.translate = StyleKeyword.Null; host.style.scale = StyleKeyword.Null; host.style.transformOrigin = StyleKeyword.Null;
            _star.Filter?.Invoke(null);
            _world.style.translate = StyleKeyword.Null; _world.style.scale = StyleKeyword.Null; _world.style.transformOrigin = StyleKeyword.Null;
            _stageEl.RemoveFromHierarchy();
            _hit.RemoveFromHierarchy(); _black.RemoveFromHierarchy();
            // the scene is the menu's again (x.draw()): the night's house and its door light go at once
            _sa.style.display = DisplayStyle.None; if (_door != null) _door.style.display = DisplayStyle.None;
            // fading, the back layer is a group of its own (an opacity under 1 isolates it): its halos and panes show as
            // they are, not screen / soft-light over the house
            foreach (var r in Rooms)
                foreach (var (kind, list, v) in new[] { ("halo", _halos[r], _script.Halo[r]), ("pane", _panes[r], _script.Pane[r]) })
                {
                    if (!_plain.TryGetValue(r + "/" + kind, out var plain)) continue;
                    foreach (var e in list) if (e != null) e.style.visibility = Visibility.Hidden;
                    plain.style.display = v > 0 ? DisplayStyle.Flex : DisplayStyle.None; plain.style.opacity = Mathf.Min(1f, (float)v);
                }
            Finished?.Invoke();
            var fade = new VisualElement[] { _low, _veilBox, _high, _back, _front, _rain, _ov };
            foreach (var e in fade) e.pickingMode = PickingMode.Ignore;
            _ov.pickingMode = PickingMode.Ignore; _skip.pickingMode = PickingMode.Ignore;
            Tween.Run(_view, 700f, (e, t) => { foreach (var el in fade) el.style.opacity = 1f - e; }, Easing.Ease);
            _view.schedule.Execute(() => { foreach (var el in fade) el.RemoveFromHierarchy(); _tick?.Pause(); }).StartingIn(800);
        }

        /// <summary>The sounds it will play, made ahead (one a frame) so none is made in the middle of the show.</summary>
        void Warm()
        {
            var list = new List<IntroScript.Voice[]>(IntroScript.KnownSounds());
            int i = 0;
            _view.schedule.Execute(() => { if (i < list.Count) IntroSynth.Clip(list[i++]); }).Every(0).Until(() => i >= list.Count || _finishing);
        }
    }
}
