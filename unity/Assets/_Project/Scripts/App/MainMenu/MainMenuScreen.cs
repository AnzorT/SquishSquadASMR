// The Main Menu, put back together from the pieces the game itself drew (tools/unity-port/export-menu.mjs):
// the scene, the creature and its name plate, the weather (drawn here, as menu.js does), then the HUD.
// Every piece goes where layout.json says, measured in the game on the Galaxy A15, and keeps its distance to the
// same screen edges the page's own layout does on other screen shapes.
//
// Phase 1 (this): the menu still, at the reference's state (level 1, rainy, Mittens, 100 coins, 600 gems).
// Phase 2: the creature's rig, the crest and PLAY animations, PLAY -> Game Park, real player data.
using System.Collections.Generic;
using UnityEngine;
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

        public MainMenuScreen(MenuState state = null) { _state = state ?? new MenuState(); }

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

            // 1. the scene: the whole drawing, cropped like preserveAspectRatio="xMidYMax slice"
            var scene = new VisualElement { name = "scene", pickingMode = PickingMode.Ignore };
            Fill(scene);
            scene.style.backgroundImage = new StyleBackground(ScenePicture());
            scene.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            scene.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            scene.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
            root.Add(scene);

            // 2. the world (creature, name plate, name), 3. the weather over it, 4. the HUD on top
            foreach (var part in _layout.parts) if (part.layer == "world") AddPart(root, part, W, H);
            root.Add(new MenuWeather(_layout.weather, _state.SeasonNow(), W, H));
            foreach (var part in _layout.parts)
            {
                if (part.layer == "world") continue;
                AddPart(root, part, W, H);
                foreach (var t in _layout.texts) if (t.parent == part.name) AddText(root, t, W, H);
            }

            SetNumber("coins", _state.Coins);
            SetNumber("gems", _state.Gems);
            return root;
        }

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
            string s = value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            foreach (var l in labels) l.text = s;
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
            // Phase 2 wires these to the real screens as they're ported.
            App.Toast(what switch
            {
                "play" => "PLAY: the Game Park comes in phase 2",
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
