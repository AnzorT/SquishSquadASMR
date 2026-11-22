// The Main Menu's moving HUD pieces, built from hud.json (tools/unity-port/export-menu-hud.mjs) and animated as
// webgame/app/menu.js does:
//   every 5.2 s a flourish (setInterval 5200): the crest at once, PLAY 1.3 s later, each 0.9 s ease-in-out —
//     crest: the whole crest floats (sqFloat), the wings flap (sqFlapL / sqFlapR), the star flips (sqStarFlip), a
//     shine crosses the medal (sqShine), the sparkles pulse (sqTw, each with its delay);
//     PLAY: its glow pulses (sqGlowO) and a glint sweeps across it (sqSweep).
// Every layer sits where the page draws it (A15 pixels in hud.json, turned into dp here).
using System;
using System.Collections.Generic;
using PlushCrush.App.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.MainMenu
{
    [Serializable]
    public sealed class HudSpec
    {
        public CrestSpec crest;
        public PlaySpec play;
        public FlourishSpec flourish;

        [Serializable] public sealed class ImageSpec { public string image; public int[] px; public float opacity = 1f; }

        [Serializable]
        public sealed class LayerSpec
        {
            public string name, image;
            public int[] px;
            public float[] pivot;
            public ImageSpec shadow;
        }

        [Serializable]
        public sealed class CrestSpec
        {
            public int[] box;
            public float unit;
            public FloatSpec @float;
            public LayerSpec[] layers;
            public ShineSpec shine;
            public ImageSpec glow;
            public SparkSpec[] sparks;
            public RingSpec ring;
            public BadgeSpec[] badges;
        }

        [Serializable]
        public sealed class RingSpec
        {
            public ImageSpec track;
            public MenuLayout.ValueItem[] fills; // key = percent 1..100
            public FillInSpec fillIn;
        }

        [Serializable] public sealed class FillInSpec { public float delay, duration; public string easing; }
        [Serializable] public sealed class BadgeSpec { public string key, image; public int[] px; }

        [Serializable] public sealed class FloatSpec { public float[] origin; }
        [Serializable] public sealed class ShineSpec { public float[] clipCenter; public float clipRadius, from, to; }
        [Serializable] public sealed class SparkSpec { public string image; public int[] px; public float delay; }

        [Serializable]
        public sealed class PlaySpec
        {
            public int[] box;
            public float radius;
            public ImageSpec glow;
            public GlintSpec glint;
        }

        [Serializable] public sealed class GlintSpec { public string image; public int[] px; public float from, to; }
        [Serializable] public sealed class FlourishSpec { public float every, crestAt, playAt, duration; public string easing; }

        public static HudSpec Load()
        {
            var json = Resources.Load<TextAsset>(MenuLayout.Folder + "hud");
            return json == null ? null : JsonUtility.FromJson<HudSpec>(json.text);
        }
    }

    public sealed class MenuHud
    {
        const float K = 2.8125f; // A15 px per dp

        readonly HudSpec _spec;
        readonly List<CssAnimator> _crestAnims = new List<CssAnimator>(), _playAnims = new List<CssAnimator>();
        public VisualElement Crest { get; private set; }
        public VisualElement Play { get; private set; }

        public MenuHud(HudSpec spec) { _spec = spec; }

        static float D(float px) => px / K;

        static VisualElement Picture(string image, int[] px, float opacity = 1f, string name = null)
        {
            var e = new VisualElement { name = name ?? image, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = D(px[0]); e.style.top = D(px[1]);
            e.style.width = D(px[2]); e.style.height = D(px[3]);
            e.style.backgroundImage = new StyleBackground(MenuLayout.Picture(image));
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            e.style.opacity = opacity;
            return e;
        }

        CssAnimation Flourish(params CssKeyframe[] keys)
        {
            var a = new CssAnimation { Duration = _spec.flourish.duration, Timing = Easing.Parse(_spec.flourish.easing) };
            foreach (var k in keys) a.Key(k);
            return a;
        }

        // ---------------- the crest ----------------

        /// <summary>The crest, in a box the size of its own (.sq-cin), to be placed where layout.json's crest is.
        /// levelPicture/levelPx: the level number's picture and its box in A15 px.</summary>
        /// <summary>The XP ring's first drawing in the app's life fills in (menu.js: ringShown); later ones don't.</summary>
        static bool _ringShown;

        public VisualElement BuildCrest(Texture2D levelPicture, int[] levelPx, float? xpFraction = null, int claimable = 0)
        {
            var c = _spec.crest;
            var root = new VisualElement { name = "crest" };
            root.style.position = Position.Absolute;
            root.style.width = D(c.box[2]); root.style.height = D(c.box[3]);

            root.Add(Picture(c.glow.image, c.glow.px, c.glow.opacity, "crest-glow"));

            // the floating part: everything of the crest's SVG
            var cin = new VisualElement { name = "crest-cin", pickingMode = PickingMode.Ignore };
            cin.style.position = Position.Absolute;
            cin.style.left = 0; cin.style.top = 0; cin.style.width = D(c.box[2]); cin.style.height = D(c.box[3]);
            root.Add(cin);
            var cinAnim = new CssAnimator(cin, new Vector2(D(c.@float.origin[0]), D(c.@float.origin[1])), autostart: false);
            _crestAnims.Add(cinAnim);
            // pressed: .sq-crest:active .sq-cin{animation:none;transform:scale(.92)} (instant, while the finger is down)
            Pressed(root, down => cinAnim.Hold = down ? new CssPose { ScaleX = 0.92f, ScaleY = 0.92f, Opacity = 1f } : (CssPose?)null);
            cinAnim
                .Play(Flourish( // sqFloat{0%,100%{translateY(0) scale(1)} 40%{translateY(-4px) scale(1.05)}}
                    new CssKeyframe(0f).Move(0, 0).Scale(1f),
                    new CssKeyframe(0.4f).Move(0, -4f).Scale(1.05f),
                    new CssKeyframe(1f).Move(0, 0).Scale(1f)));

            // shadows first (one per layer, under the whole crest), then the layers, in the SVG's order
            var layerEls = new Dictionary<string, VisualElement>();
            var shadowEls = new Dictionary<string, VisualElement>();
            foreach (var L in c.layers)
                if (L.shadow != null && L.shadow.image != null) { var s = Picture(L.shadow.image, L.shadow.px, 1f, "shadow-" + L.name); cin.Add(s); shadowEls[L.name] = s; }
            foreach (var L in c.layers)
            {
                if (L.name == "shine") { cin.Add(Shine(L)); continue; }
                var e = Picture(L.image, L.px, 1f, "crest-" + L.name);
                cin.Add(e);
                layerEls[L.name] = e;
                if (L.name == "medalBack" && xpFraction.HasValue && c.ring != null) AddRing(cin, c.ring, Mathf.Clamp01(xpFraction.Value));
            }
            if (levelPicture != null && levelPx != null)
            {
                var n = new VisualElement { name = "crest-level", pickingMode = PickingMode.Ignore };
                n.style.position = Position.Absolute;
                n.style.left = D(levelPx[0] - c.box[0]); n.style.top = D(levelPx[1] - c.box[1]);
                n.style.width = D(levelPx[2]); n.style.height = D(levelPx[3]);
                n.style.backgroundImage = new StyleBackground(levelPicture);
                n.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                cin.Add(n);
            }

            // wings flap from their shoulders, the star flips on its centre: the layer and its shadow together
            foreach (var L in c.layers)
            {
                if (L.pivot == null || L.pivot.Length != 2) continue;
                CssKeyframe[] keys;
                if (L.name == "wingL") keys = Flap(17f, -5f, 12f);        // sqFlapL
                else if (L.name == "wingR") keys = Flap(-17f, 5f, -12f);  // sqFlapR
                else if (L.name == "star") keys = new[] { new CssKeyframe(0f).Scale(1f, 1f), new CssKeyframe(0.5f).Scale(-1f, 1f), new CssKeyframe(1f).Scale(1f, 1f) }; // sqStarFlip
                else continue;
                if (layerEls.TryGetValue(L.name, out var layerEl)) Spin(layerEl, L.px, L.pivot, keys);
                if (L.shadow != null && shadowEls.TryGetValue(L.name, out var shadowEl)) Spin(shadowEl, L.shadow.px, L.pivot, keys);
            }

            // the sparkles (outside the floating part): see-through between flourishes, .35 -> 1 -> .35 in one (sqTw)
            foreach (var s in c.sparks)
            {
                var e = Picture(s.image, s.px, 0f, "crest-spark");
                root.Add(e);
                var a = Flourish(new CssKeyframe(0f).Fade(0.35f), new CssKeyframe(0.5f).Fade(1f), new CssKeyframe(1f).Fade(0.35f));
                a.Delay = s.delay;
                _crestAnims.Add(new CssAnimator(e, autostart: false).Base(new CssPose { ScaleX = 1, ScaleY = 1, Opacity = 0f }).Play(a));
            }
            // the prizes waiting on the Level Pass (it doesn't float)
            if (claimable > 0 && c.badges != null)
            {
                string key = claimable > 9 ? "9+" : claimable.ToString();
                var b = Array.Find(c.badges, x => x.key == key);
                if (b != null) root.Add(Picture(b.image, b.px, 1f, "crest-badge"));
            }
            Crest = root;
            return root;
        }

        // The XP ring: its track, and the arc of the nearest whole percent (a picture per percent). On the first
        // drawing in the app's life it fills in from nothing: 0.3 s, then 1.4 s along cubic-bezier(.2,.9,.25,1).
        void AddRing(VisualElement cin, HudSpec.RingSpec ring, float f)
        {
            cin.Add(Picture(ring.track.image, ring.track.px, 1f, "crest-ring-track"));
            var arc = new VisualElement { name = "crest-ring", pickingMode = PickingMode.Ignore };
            arc.style.position = Position.Absolute;
            cin.Add(arc);
            void Show(float frac)
            {
                int pc = Mathf.RoundToInt(frac * 100f);
                var item = pc > 0 ? Array.Find(ring.fills, x => x.key == pc.ToString()) : null;
                if (item == null) { arc.style.display = DisplayStyle.None; return; }
                arc.style.display = DisplayStyle.Flex;
                arc.style.left = D(item.px[0]); arc.style.top = D(item.px[1]);
                arc.style.width = D(item.px[2]); arc.style.height = D(item.px[3]);
                arc.style.backgroundImage = new StyleBackground(MenuLayout.Picture("Hud/crest-ring-" + item.key));
                arc.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }
            if (_ringShown || ring.fillIn == null || ReferenceOverlay.FreezeAnimations) { Show(f); _ringShown = true; return; }
            _ringShown = true;
            Show(0f);
            var ease = Easing.Parse(ring.fillIn.easing);
            float t0 = -1f;
            IVisualElementScheduledItem tick = null;
            tick = arc.schedule.Execute(() =>
            {
                if (t0 < 0f) t0 = Time.realtimeSinceStartup;
                float t = (Time.realtimeSinceStartup - t0 - ring.fillIn.delay) / ring.fillIn.duration;
                Show(f * ease(Mathf.Clamp01(t)));
                if (t >= 1f) tick.Pause();
            }).Every(0);
        }

        // turns around the pivot (crest px), whatever box the picture has
        void Spin(VisualElement e, int[] px, float[] pivot, CssKeyframe[] keys)
        {
            var origin = new Vector2(D(pivot[0] - px[0]), D(pivot[1] - px[1]));
            _crestAnims.Add(new CssAnimator(e, origin, autostart: false).Play(Flourish(keys)));
        }

        static CssKeyframe[] Flap(float a25, float a50, float a75) => new[]
        {
            new CssKeyframe(0f).Rot(0f), new CssKeyframe(0.25f).Rot(a25), new CssKeyframe(0.5f).Rot(a50),
            new CssKeyframe(0.75f).Rot(a75), new CssKeyframe(1f).Rot(0f),
        };

        // the shine: a white band crossing the medal, clipped to its circle; parked off to the left between flourishes
        VisualElement Shine(HudSpec.LayerSpec L)
        {
            var sh = _spec.crest.shine;
            float r = D(sh.clipRadius), cx = D(sh.clipCenter[0]), cy = D(sh.clipCenter[1]);
            var clip = new VisualElement { name = "crest-shine-clip", pickingMode = PickingMode.Ignore };
            clip.style.position = Position.Absolute;
            clip.style.left = cx - r; clip.style.top = cy - r; clip.style.width = 2 * r; clip.style.height = 2 * r;
            clip.style.borderTopLeftRadius = r; clip.style.borderTopRightRadius = r;
            clip.style.borderBottomLeftRadius = r; clip.style.borderBottomRightRadius = r;
            clip.style.overflow = Overflow.Hidden;
            var band = Picture(L.image, new[] { 0, 0, L.px[2], L.px[3] }, 1f, "crest-shine");
            band.style.left = D(L.px[0]) - (cx - r); band.style.top = D(L.px[1]) - (cy - r);
            clip.Add(band);
            var a = Flourish(new CssKeyframe(0f).Move(D(sh.from), 0), new CssKeyframe(1f).Move(D(sh.to), 0)); // sqShine
            _crestAnims.Add(new CssAnimator(band, autostart: false).Base(new CssPose { X = D(sh.from), ScaleX = 1, ScaleY = 1, Opacity = 1 }).Play(a));
            return clip;
        }

        // ---------------- PLAY ----------------

        /// <summary>PLAY: its glow, the button's picture, the glint; in a box the size of the button.</summary>
        public VisualElement BuildPlay(Texture2D playPicture, int[] playPx)
        {
            var p = _spec.play;
            var root = new VisualElement { name = "play" };
            root.style.position = Position.Absolute;
            root.style.width = D(p.box[2]); root.style.height = D(p.box[3]);

            var glow = Picture(p.glow.image, p.glow.px, p.glow.opacity, "play-glow");
            root.Add(glow);
            _playAnims.Add(new CssAnimator(glow, autostart: false)
                .Base(new CssPose { ScaleX = 1, ScaleY = 1, Opacity = p.glow.opacity })
                .Play(Flourish(new CssKeyframe(0f).Fade(0.15f), new CssKeyframe(0.5f).Fade(0.85f), new CssKeyframe(1f).Fade(0.15f)))); // sqGlowO

            // the button and its glint (the glow is the card's: it stays put when PLAY is pressed)
            var press = new VisualElement { name = "play-press", pickingMode = PickingMode.Ignore };
            press.style.position = Position.Absolute;
            press.style.left = 0; press.style.top = 0; press.style.right = 0; press.style.bottom = 0;
            root.Add(press);
            // pressed: .mm-play:active{transform:translateY(3px)} (instant)
            Pressed(root, down => press.style.translate = new Translate(0, down ? 3f : 0f));

            if (playPicture != null && playPx != null)
            {
                var b = new VisualElement { name = "play-button", pickingMode = PickingMode.Ignore };
                b.style.position = Position.Absolute;
                b.style.left = D(playPx[0] - p.box[0]); b.style.top = D(playPx[1] - p.box[1]);
                b.style.width = D(playPx[2]); b.style.height = D(playPx[3]);
                b.style.backgroundImage = new StyleBackground(playPicture);
                b.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                press.Add(b);
            }

            // the glint, inside the button's rounded box (.sq-glint: inset 0, border-radius inherit, overflow hidden)
            var clip = new VisualElement { name = "play-glint-clip", pickingMode = PickingMode.Ignore };
            clip.style.position = Position.Absolute;
            clip.style.left = 0; clip.style.top = 0; clip.style.right = 0; clip.style.bottom = 0;
            float rad = D(p.radius);
            clip.style.borderTopLeftRadius = rad; clip.style.borderTopRightRadius = rad;
            clip.style.borderBottomLeftRadius = rad; clip.style.borderBottomRightRadius = rad;
            clip.style.overflow = Overflow.Hidden;
            var band = Picture(p.glint.image, p.glint.px, 1f, "play-glint");
            clip.Add(band);
            press.Add(clip);
            _playAnims.Add(new CssAnimator(band, autostart: false)
                .Base(new CssPose { X = D(p.glint.from), ScaleX = 1, ScaleY = 1, Opacity = 1 })
                .Play(Flourish(new CssKeyframe(0f).Move(D(p.glint.from), 0), new CssKeyframe(1f).Move(D(p.glint.to), 0)))); // sqSweep
            Play = root;
            return root;
        }

        // CSS :active: on while a finger (or the mouse button) is down on it, off when it lifts or leaves
        static void Pressed(VisualElement e, Action<bool> set)
        {
            e.RegisterCallback<PointerDownEvent>(_ => set(true));
            e.RegisterCallback<PointerUpEvent>(_ => set(false));
            e.RegisterCallback<PointerLeaveEvent>(_ => set(false));
            e.RegisterCallback<PointerCancelEvent>(_ => set(false));
        }

        // ---------------- the flourish timer ----------------

        /// <summary>Start the flourish timer on an element of the screen (stops with it).</summary>
        public void Run(VisualElement owner)
        {
            var f = _spec.flourish;
            long every = (long)(f.every * 1000f);
            owner.schedule.Execute(() =>
            {
                Trigger(_crestAnims, f.crestAt);
                Trigger(_playAnims, f.playAt);
            }).StartingIn(every).Every(every);

            void Trigger(List<CssAnimator> anims, float at)
            {
                if (at <= 0f) { foreach (var a in anims) a.Trigger(); return; }
                owner.schedule.Execute(() => { foreach (var a in anims) a.Trigger(); }).StartingIn((long)(at * 1000f));
            }
        }
    }
}
