// The Main Menu, put back together from the pieces the game itself drew (tools/unity-port/export-menu.mjs):
// the scene, the creature and its name plate, the weather (drawn here, as menu.js does), then the HUD.
// Every piece goes where layout.json says, measured in the game on the Galaxy A15, and keeps its distance to the
// same screen edges the page's own layout does on other screen shapes.
//
// The pictures follow the player's state (MenuState: creature, level, season, coins, gems, XP, prizes waiting); the HUD
// moves as in the game (MenuHud); PLAY walks to the Game Park (EnterPark). Still to come: the creature's rig (it's a
// still picture here) and real player data (Firebase).
using System.Collections.Generic;
using UnityEngine;
using PlushCrush.App.Motion;
using UnityEngine.UIElements;

namespace PlushCrush.App.MainMenu
{
    /// <summary>What the menu shows. Stands in for the player's data until Firebase is wired in.</summary>
    public sealed class MenuState
    {
        public string CreatureKey = "mittens";
        public int Level = 1;
        public long Coins = 100;
        public long Gems = 600;
        /// <summary>XP into the current level and needed for the next (state.xp); no XP ring without them.</summary>
        public float? XpInto, XpNeed;
        /// <summary>Prizes waiting on the Level Pass (state.road's claimable ones): the crest's badge.</summary>
        public int Claimable;
        /// <summary>sunny, rainy or snowy; null = by the month, as main-menu-scene.js autoSeason() does.
        /// (The React Native app can also set it from Firestore config/weather: wired with Firebase.)</summary>
        public string Season;

        public string SeasonNow()
        {
            if (Season == "sunny" || Season == "rainy" || Season == "snowy") return Season;
            int mo = System.DateTime.Now.Month - 1; // JS months: 0 = January
            return mo == 11 || mo <= 1 ? "snowy" : mo >= 4 && mo <= 8 ? "sunny" : "rainy";
        }
    }

    public sealed class MainMenuScreen : AppScreen
    {
        readonly MenuState _state;
        MenuLayout _layout;
        readonly Dictionary<string, Label[]> _texts = new Dictionary<string, Label[]>();
        // the coin and gem pills grow with their number (.mm-pill: flex, its width is the text's), and the coin pill
        // moves left as the gem pill grows (they're in one row, right-aligned)
        sealed class Pill { public VisualElement El, TextBox; public float Right, Width, TextRight, TextWidth, Delta; public string RefText; }
        readonly Dictionary<string, Pill> _pills = new Dictionary<string, Pill>();

        public MainMenuScreen(MenuState state = null) { _state = state ?? new MenuState(); }

        // the page's layers: the world (scene, vignette, the Game Park's picture, creature, name plate: the camera pans
        // it), the weather over it (fixed to the screen), the HUD on top (fades while the creature walks)
        VisualElement _world, _hud, _vignette, _creatureEl, _nameEl, _park;
        IMenuCreature _creature;
        bool _busy;
        ParkWalk _walk;
        sealed class ParkWalk { public float W, X0, X1; public bool Leaving; }

        protected override VisualElement Build()
        {
            var root = new VisualElement { name = "main-menu" };
            root.style.backgroundColor = new Color32(0x24, 0x12, 0x3f, 0xFF); // the page's own background
            root.style.overflow = Overflow.Hidden;

            _layout = MenuLayout.Load();
            if (_layout == null)
            {
                root.Add(new Label("Main Menu pictures missing: run tools/unity-port/export-menu.mjs"));
                return root;
            }
            float W = _layout.screen.width, H = _layout.screen.height;

            _world = new VisualElement { name = "world", pickingMode = PickingMode.Ignore };
            Fill(_world);
            root.Add(_world);

            // 1. the scene: the whole drawing, cropped like preserveAspectRatio="xMidYMax slice"; its vignette over it
            var scene = new VisualElement { name = "scene", pickingMode = PickingMode.Ignore };
            CoverScene(scene, ScenePicture());
            _world.Add(scene);
            if (!string.IsNullOrEmpty(_layout.scene.vignette))
            {
                _vignette = new VisualElement { name = "vignette", pickingMode = PickingMode.Ignore };
                CoverScene(_vignette, MenuLayout.Picture(_layout.scene.vignette));
                _world.Add(_vignette);
            }

            // 2. the creature and its name plate, 3. the weather, 4. the HUD
            foreach (var part in _layout.parts) if (part.layer == "world") AddPart(_world, part, W, H);
            _creatureEl = _world.Q("creature");
            _nameEl = _world.Q("nameplate");
            // the stage (moved by the walk) holds the picture (bobbed by the walk), as .mm-stage holds the rig's box
            VisualElement pic = null;
            if (_creatureEl != null)
            {
                pic = new VisualElement { name = "creature-picture", pickingMode = PickingMode.Ignore };
                Fill(pic);
                pic.style.backgroundImage = _creatureEl.style.backgroundImage;
                pic.style.backgroundSize = _creatureEl.style.backgroundSize;
                _creatureEl.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                _creatureEl.Add(pic);
            }
            // until the rig is ported: the still picture (it turns and walks in the page); the stage is 169 dp tall
            _creature = new StillCreature(pic, 169f);
            root.Add(new MenuWeather(_layout.weather, _state.SeasonNow(), W, H));
            if (MenuBirds.Shown(_state.SeasonNow(), _state.Level)) root.Add(new MenuBirds());

            _hud = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            Fill(_hud);
            root.Add(_hud);
            // the moving HUD pieces (crest, PLAY) when hud.json is there; their still pictures otherwise
            var hudSpec = HudSpec.Load();
            var hud = hudSpec != null ? new MenuHud(hudSpec) : null;
            foreach (var part in _layout.parts)
            {
                if (part.layer == "world") continue;
                if (hud != null && part.name == "play") { AddPlay(_hud, hud, hudSpec, part, W, H); continue; }
                if (hud != null && part.name == "crest") { AddCrest(_hud, hud, hudSpec, W, H); continue; }
                if (hud != null && (part.name == "crest-level" || part.name.StartsWith("crest-spark"))) continue; // in the crest
                AddPart(_hud, part, W, H);
                foreach (var t in _layout.texts) if (t.parent == part.name) AddText(_hud, t, W, H);
            }
            hud?.Run(root);

            foreach (var t in _layout.texts) RegisterPill(t, W);
            SetNumber("coins", _state.Coins);
            SetNumber("gems", _state.Gems);
            root.RegisterCallback<AttachToPanelEvent>(_ => root.schedule.Execute(RelayoutPills));
            return root;
        }

        // A pill's picture stretches in its middle only (9-slice): its ends (the icon, the rounded caps) stay as drawn.
        void RegisterPill(MenuLayout.TextSpec t, float W)
        {
            var el = _hud.Q(t.parent);
            var box = _hud.Q("text-" + t.key);
            var part = System.Array.Find(_layout.parts, p => p.name == t.parent);
            if (el == null || box == null || part == null || part.px == null) return;
            const float K = 2.8125f;
            float x = part.px[0] / K, w = part.px[2] / K;
            int left = Mathf.RoundToInt(t.x * K - part.px[0]), right = Mathf.RoundToInt(part.px[0] + part.px[2] - (t.x + t.w) * K);
            el.style.unitySliceLeft = Mathf.Max(0, left);
            el.style.unitySliceRight = Mathf.Max(0, right);
            el.style.unitySliceTop = 0; el.style.unitySliceBottom = 0;
            el.style.unitySliceScale = 1f / K;
            _pills[t.key] = new Pill { El = el, TextBox = box, Right = W - (x + w), Width = w, TextRight = W - (t.x + t.w), TextWidth = t.w, RefText = t.text };
        }

        // the width each number needs beyond the one the pictures were taken with (both measured by Unity's own text)
        void RelayoutPills()
        {
            foreach (var kv in _pills)
            {
                var p = kv.Value;
                if (!_texts.TryGetValue(kv.Key, out var labels) || labels.Length == 0 || labels[labels.Length - 1].panel == null) continue;
                var main = labels[labels.Length - 1];
                float now = main.MeasureTextSize(main.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                float then = main.MeasureTextSize(p.RefText, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                p.Delta = now - then;
            }
            // right to left along the row: gems, then coins (moved left by however much the gems pill grew)
            float shift = 0f;
            foreach (var key in new[] { "gems", "coins" })
            {
                if (!_pills.TryGetValue(key, out var p)) continue;
                p.El.style.right = p.Right + shift; p.El.style.width = p.Width + p.Delta;
                p.TextBox.style.right = p.TextRight + shift; p.TextBox.style.width = p.TextWidth + p.Delta;
                shift += p.Delta;
            }
        }

        static void CoverScene(VisualElement e, Texture2D tex)
        {
            Fill(e);
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            e.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
        }

        // ---------------- PLAY: the walk to the Game Park and back (menu.js enterPark / leavePark) ----------------

        // the scene on this screen: xMidYMax slice of 1000 x 460 (main-menu.js geo)
        void Geo(out float W, out float H, out float s, out float ox, out float oy)
        {
            var r = View.layout;
            W = r.width; H = r.height;
            s = Mathf.Max(W / 1000f, H / 460f); ox = (W - 1000f * s) / 2f; oy = H - 460f * s;
        }

        // the HUD, the tap areas and the name plate fade (.mm-fade: opacity .35 s ease; .mm-gone takes taps away)
        void FadeHud(bool on)
        {
            foreach (var e in new[] { _hud, _nameEl })
                if (e != null) Tween.Fade(e, on ? 1f : 0f, 350f, Easing.Ease);
        }

        void EnterPark()
        {
            if (_busy || _walk != null) return;
            _busy = true;
            Geo(out float W, out _, out float s, out float ox, out _);
            const float PAD = 160f; // where the creature stands in the park (the house's pad is at 500)
            var w = _walk = new ParkWalk { W = W, X0 = ox + 500f * s, X1 = ox + PAD * s };
            FadeHud(false);
            _creature.Play("idle");
            ShowParkPicture();
            if (_vignette != null) Tween.Fade(_vignette, 0f, 450f, Easing.Ease);
            // turn left (380 ms), walk left while the camera follows (2300 ms), turn back (380 ms)
            Tween.Run(View, 380f, (e, t) => _creature.SpinOff(-90f * e), Tween.EIO, () =>
            {
                _creature.Walking(true, -90f);
                Tween.Run(View, 2300f, (e, t) =>
                {
                    float scr = w.X0 + (w.X1 - w.X0) * Tween.EOUT(t), cam = w.W * Tween.EIOC(Mathf.Clamp01((t - 0.06f) / 0.94f));
                    _world.style.translate = new Translate(cam, 0);
                    if (_creatureEl != null) _creatureEl.style.translate = new Translate(scr - w.X0 - cam, 0);
                }, null, () =>
                {
                    _creature.Walking(false, 0f);
                    Tween.Run(View, 380f, (e, t) => _creature.SpinOff(-90f * (1f - e)), Tween.EIO, () =>
                    {
                        _creature.SpinOff(0f);
                        _creature.Play("happy");
                        ShowGames();
                    });
                });
            });
        }

        void LeavePark()
        {
            var w = _walk;
            if (w == null || w.Leaving) return;
            w.Leaving = true;
            HideGames();
            _creature.Play("idle");
            Tween.Run(View, 340f, (e, t) => _creature.SpinOff(90f * e), Tween.EIO, () =>
            {
                _creature.Walking(true, 90f);
                Tween.Run(View, 2100f, (e, t) =>
                {
                    float scr = w.X1 + (w.X0 - w.X1) * Tween.EIO(t), cam = w.W * (1f - Tween.EIOC(Mathf.Clamp01(t / 0.94f)));
                    _world.style.translate = new Translate(cam, 0);
                    if (_creatureEl != null) _creatureEl.style.translate = new Translate(scr - w.X0 - cam, 0);
                }, null, () =>
                {
                    _creature.Walking(false, 0f);
                    Tween.Run(View, 340f, (e, t) => _creature.SpinOff(90f * (1f - e)), Tween.EIO, () =>
                    {
                        _creature.SpinOff(0f);
                        _world.style.translate = new Translate(0, 0);
                        if (_creatureEl != null) _creatureEl.style.translate = new Translate(0, 0);
                        if (_vignette != null) Tween.Fade(_vignette, 1f, 450f, Easing.Ease);
                        _park?.RemoveFromHierarchy(); _park = null; // its seam tree would show at the house's left edge
                        _walk = null; _busy = false;
                        FadeHud(true);
                        _creature.Play("happy");
                    });
                });
            });
        }

        // The Game Park's picture, a screen's width to the left of the house, in the world (it pans with it).
        void ShowParkPicture()
        {
            if (_park != null || _layout.park == null) return;
            int L = Mathf.Clamp(_state.Level, 1, 30);
            string season = _state.SeasonNow(), key = $"L{L}-{season}";
            Texture2D tex = null; float d = _layout.park.d, top = _layout.park.top;
            var d0 = _layout.defaults;
            if (d0 == null || L != d0.level || season != d0.season)
            {
                var item = MenuLayout.Parks.Find(key);
                var gen = item != null ? MenuLayout.Optional("MenuParks/park-" + key) : null;
                if (gen != null) { tex = gen; d = item.d; top = item.top; }
            }
            if (tex == null) tex = MenuLayout.Picture(_layout.park.image);
            if (tex == null) return;

            Geo(out float W, out float H, out float s, out float ox, out float oy);
            float k = 1f / _layout.park.sceneScale;                       // scene units per picture pixel
            float houseW = _layout.scene.pixels[0] * k * s, houseLeft = (W - houseW) / 2f;
            _park = new VisualElement { name = "park", pickingMode = PickingMode.Ignore };
            _park.style.position = Position.Absolute;
            _park.style.left = houseLeft - W + d * s;
            _park.style.top = oy + top * s;
            _park.style.width = tex.width * k * s;
            _park.style.height = tex.height * k * s;
            _park.style.backgroundImage = new StyleBackground(tex);
            _park.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            // over the house's scene and vignette, under the creature (menu.js: inserted after the scene)
            int at = _vignette != null ? _world.IndexOf(_vignette) + 1 : 1;
            _world.Insert(at, _park);
        }

        // the games carousel over the park (GameParkOverlay): wired in when it's ported
        void ShowGames() { }
        void HideGames() { }

        public override bool OnBack()
        {
            if (_walk != null) { LeavePark(); return true; }
            return false;
        }

        void AddCrest(VisualElement root, MenuHud hud, HudSpec spec, float W, float H)
        {
            // the level's number: its own picture and box (Levels/index.json), the default's otherwise
            var levelPart = System.Array.Find(_layout.parts, p => p.value == "level");
            Texture2D num = null; int[] numPx = null;
            if (levelPart != null)
            {
                num = PictureFor(levelPart, out var spec2);
                numPx = spec2.px;
            }
            float? xp = _state.XpInto.HasValue && _state.XpNeed.HasValue && _state.XpNeed.Value > 0
                ? _state.XpInto.Value / _state.XpNeed.Value : (float?)null;
            var crest = hud.BuildCrest(num, numPx, xp, _state.Claimable);
            Place(crest, "bottom-right", Box(spec.crest.box), W, H);
            crest.RegisterCallback<ClickEvent>(_ => Tapped("crest"));
            root.Add(crest);
        }

        void AddPlay(VisualElement root, MenuHud hud, HudSpec spec, MenuLayout.PartSpec part, float W, float H)
        {
            var play = hud.BuildPlay(MenuLayout.Picture(part.image), part.px);
            Place(play, part.anchor, Box(spec.play.box), W, H);
            play.RegisterCallback<ClickEvent>(_ => Tapped("play"));
            root.Add(play);
        }

        static MenuLayout.PartSpec Box(int[] px) => new MenuLayout.PartSpec { px = px };

        // The scene for the player's level (the drawing stops changing at 30) and the season; the level 1 rainy one,
        // always in the project, when the generated ones haven't been made (tools/unity-port/export-menu-states.mjs).
        Texture2D ScenePicture()
        {
            int L = Mathf.Clamp(_state.Level, 1, 30);
            var d = _layout.defaults;
            if (d != null && L == d.level && _state.SeasonNow() == d.season) return MenuLayout.Picture(_layout.scene.image);
            return MenuLayout.Optional($"MenuScenes/scene-L{L}-{_state.SeasonNow()}") ?? MenuLayout.Picture(_layout.scene.image);
        }

        void AddPart(VisualElement root, MenuLayout.PartSpec part, float W, float H)
        {
            // pieces that depend on a value: one picture per value, each with its own box (from that value's index)
            var tex = PictureFor(part, out var spec);
            if (tex == null) return;

            var e = new VisualElement { name = part.name };
            Place(e, spec.anchor, spec, W, H);
            if (spec.anchor == "scene" && spec.sceneAt != null && spec.sceneAt.Length == 2) FollowScene(root, e, spec);
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            if (IsButton(part.name)) e.RegisterCallback<ClickEvent>(_ => Tapped(part.name));
            else e.pickingMode = PickingMode.Ignore;
            root.Add(e);
        }

        Texture2D PictureFor(MenuLayout.PartSpec part, out MenuLayout.PartSpec spec)
        {
            spec = part;
            MenuLayout.ValueItem item = null;
            Texture2D tex = null;
            switch (part.value)
            {
                case "nameplate":
                    item = MenuLayout.Names.Find(_state.CreatureKey);
                    tex = item != null ? MenuLayout.Optional(MenuLayout.Folder + "Names/nameplate-" + item.key) : null;
                    break;
                case "level":
                    item = MenuLayout.Levels.Find(_state.Level.ToString());
                    tex = item != null ? MenuLayout.Optional(MenuLayout.Folder + "Levels/crest-level-" + item.key) : null;
                    break;
                case "creature-still":
                    // until the rig is ported: the creature as the menu shows it at rest (generated, not in git).
                    // The default creature keeps the picture taken at the reference screenshots' moment.
                    if (_layout.defaults != null && _state.CreatureKey == _layout.defaults.creature) break;
                    item = MenuLayout.Creatures.Find(_state.CreatureKey);
                    tex = item != null ? MenuLayout.Optional("MenuCreatures/creature-" + item.key) : null;
                    break;
            }
            if (tex != null && item != null && item.px != null && item.px.Length == 4)
            {
                spec = new MenuLayout.PartSpec { name = part.name, anchor = part.anchor, layer = part.layer, value = part.value,
                    px = item.px, sceneAt = item.sceneAt ?? part.sceneAt };
                return tex;
            }
            return MenuLayout.Picture(part.image); // the default state's picture
        }

        // Text the page draws as text (it changes): the same font, size, color and hard shadows, in the same box.
        void AddText(VisualElement root, MenuLayout.TextSpec t, float W, float H)
        {
            var box = new VisualElement { name = "text-" + t.key, pickingMode = PickingMode.Ignore };
            var spec = new MenuLayout.PartSpec { x = t.x, y = t.y, w = t.w, h = t.h };
            Place(box, t.anchor, spec, W, H);
            var font = Fonts.For(t.font, t.weight);
            var labels = new List<Label>();
            // shadows first (behind), each a copy of the text moved by its offset
            if (t.shadows != null)
                foreach (var s in t.shadows)
                    labels.Add(TextLabel(box, t, font, Css.ToColor(s.color), s.x, s.y));
            labels.Add(TextLabel(box, t, font, Css.ToColor(t.color, Color.white), 0, 0));
            _texts[t.key] = labels.ToArray();
            root.Add(box);
        }

        static Label TextLabel(VisualElement box, MenuLayout.TextSpec t, string font, Color color, float dx, float dy)
        {
            var l = new Label(t.text).Use(font);
            l.pickingMode = PickingMode.Ignore;
            l.style.position = Position.Absolute;
            l.style.left = dx; l.style.top = dy;
            l.style.height = t.h;
            l.style.fontSize = t.size;
            l.style.letterSpacing = t.letterSpacing;
            l.style.color = color;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            box.Add(l);
            return l;
        }

        public void SetNumber(string key, long value)
        {
            if (!_texts.TryGetValue(key, out var labels)) return;
            string s = value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); // fmt(): toLocaleString('en-US')
            foreach (var l in labels) l.text = s;
            if (View != null && View.panel != null) RelayoutPills();
        }

        // A box in layout.json (measured on the A15), kept at the same distance to the edges named by its anchor.
        static void Place(VisualElement e, string anchor, MenuLayout.PartSpec p, float W, float H)
        {
            float x = p.x, y = p.y, w = p.w, h = p.h;
            if (p.px != null && p.px.Length == 4) // whole screen pixels on the A15: exact, no resampling
            {
                const float K = 2.8125f;
                x = p.px[0] / K; y = p.px[1] / K; w = p.px[2] / K; h = p.px[3] / K;
            }
            e.style.position = Position.Absolute;
            e.style.width = w;
            e.style.height = h;
            var a = (anchor ?? "top-left").Split('-');
            string v = a[0], hz = a.Length > 1 ? a[1] : "left";
            if (v == "bottom") e.style.bottom = H - (y + h); else e.style.top = y;
            if (hz == "right") e.style.right = W - (x + w);
            else if (hz == "center") { e.style.left = Length.Percent(50); e.style.marginLeft = x - W / 2f; }
            else e.style.left = x;
        }

        // Standing in the scene: where the scene's crop (xMidYMax slice of 1000 x 460) puts its point on this screen.
        static void FollowScene(VisualElement root, VisualElement e, MenuLayout.PartSpec p)
        {
            float w = p.px != null && p.px.Length == 4 ? p.px[2] / 2.8125f : p.w;
            float h = p.px != null && p.px.Length == 4 ? p.px[3] / 2.8125f : p.h;
            void Layout()
            {
                float Wu = root.layout.width, Hu = root.layout.height;
                if (float.IsNaN(Wu) || Wu <= 0 || Hu <= 0) return;
                float s = Mathf.Max(Wu / 1000f, Hu / 460f), sx0 = (Wu - 1000f * s) / 2f, sy0 = Hu - 460f * s;
                e.style.left = sx0 + p.sceneAt[0] * s - w / 2f;
                e.style.top = sy0 + p.sceneAt[1] * s - h;
                e.style.right = StyleKeyword.Auto; e.style.bottom = StyleKeyword.Auto; e.style.marginLeft = 0;
            }
            root.RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }

        static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0;
        }

        static bool IsButton(string name) =>
            name.StartsWith("tab-") || name.StartsWith("orb-") || name.StartsWith("pill-") || name == "play" || name == "crest";

        void Tapped(string what)
        {
            if (_busy) return; // (.mm-gone: no taps while the HUD is faded)
            if (what == "play") { EnterPark(); return; }
            // Phase 2 wires these to the real screens as they're ported.
            App.Toast(what switch
            {
                "tab-house" => "The house comes in phase 5",
                "tab-characters" => "Characters comes in phase 3",
                "tab-shop" or "pill-coins" or "pill-gems" => "The Shop comes in phase 3",
                "orb-trophies" => "Achievements come in phase 3",
                "orb-settings" => "Settings come in phase 3",
                "crest" => "The Level Pass comes in phase 2",
                _ => what,
            });
        }
    }
}
