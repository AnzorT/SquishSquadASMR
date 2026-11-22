// layout.json, written by tools/unity-port/export-level-pass.mjs: the Level Pass's pieces and the page's own layout
// numbers, measured in the game on the Galaxy A15 (832 x 384 dp, 2.8125 px per dp).
//   - Layout numbers are the page's "design px" (an 852 x 393 box scaled by page.k = 384 / 393); Unity units are dp,
//     so a design number is multiplied by k.
//   - A picture's `off` is in A15 px from the top-left of the box of the element it belongs to (the page's boxes
//     aren't on the pixel grid); `size` is its size in px. Placed at the nearest whole px, it lands where the page
//     draws it.
// Also: the pieces that build these (Pictures, Stretch, PassText).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.LevelPass
{
    [Serializable]
    public sealed class LevelPassLayout
    {
        public const string Folder = "Screens/LevelPass/";

        public ScreenSpec screen;
        public PageSpec page;
        public LevelPassState standIn; // the player the pictures and the reference screenshots show
        public Pic background;
        public HeaderSpec header;
        public TrackSpec track;
        public PanelSpec panel;
        public FlySpec fly;

        [Serializable] public sealed class ScreenSpec { public float width, height, scale; }
        [Serializable] public sealed class PageSpec { public float k, designHeight; }

        [Serializable]
        public sealed class HeaderSpec
        {
            public float left, top, right, height, gap;
            public BoxPic back, title;
            public PlaqueSpec claimAll, lv, coins;
        }

        /// <summary>A plaque in the header: a row of its own content between paddings, its frame behind.</summary>
        [Serializable]
        public sealed class PlaqueSpec
        {
            public float height, padLeft, padRight, gap;
            public TextSpec text, text2;
            public Stretch frame;
            public BarSpec bar;
            public BoxPic icon;
        }

        [Serializable] public sealed class BarSpec { public float width, height; public Stretch track, fill; }

        [Serializable]
        public sealed class TrackSpec
        {
            public float top, height, col, x0, endPad, railTop, railHeight, nodeTop, nodeSize, youTop, youSize;
            public float[] pointerBox;
            public Stretch rail, fill;
            public YouSpec you;
            public NodesSpec nodes;
        }

        /// <summary>The "you" frame and its pointer (a picture per sub-pixel phase), the creature in it.</summary>
        [Serializable]
        public sealed class YouSpec
        {
            public float[] phases;
            public Pic[] frame, pointer;
            public NamedPic[] creatures;
        }

        [Serializable]
        public sealed class NodesSpec
        {
            public float[] phases;
            public PhasedPics[] looks, badges; // looks: brown, brown-glow, brown-sel, gold, gold-glow, gold-sel, gold-cur, gold-cur-sel
            public TextSpec brownText, goldText;
            public float curScale;
            public BobSpec bob;
        }

        [Serializable] public sealed class PhasedPics { public string key; public Pic[] pics; }
        [Serializable] public sealed class BobSpec { public string name, easing; public float duration, y; }

        [Serializable]
        public sealed class PanelSpec
        {
            public float left, right, top, height, padLeft, padTop, padRight, gap, itemsHeight;
            public Stretch frame;
            public ColumnSpec column;
            public CardSpec card;
        }

        [Serializable]
        public sealed class ColumnSpec
        {
            public float width, letteringHeight, subTop, subHeight, actTop, actWidth, actHeight, iconSize, iconGap, actIconTop;
            public TextSpec sub, actText;
            public Pic claim, actFrame, check, @lock;
            public NamedPic[] lettering; // key "n-gold" / "n-white"
        }

        [Serializable]
        public sealed class CardSpec
        {
            public float basis, gap, padTop, padX, padBottom, artHeight, titleTop, titleHeight, descTop, tagTop, noneDescTop, noneDescHeight, roomAspect;
            public TextSpec desc;
            public Stretch bg;
            public NamedPic glow;
            public NamedPic[] tags, chests, plainChests, piles, rooms, titles;
            public ChestFit chestFit;
            public DescLines[] descLines;
        }

        /// <summary>A chest picture's box: its height a share of the art's, CSS % margins (of the art's width).</summary>
        [Serializable] public sealed class ChestFit { public float heightPct, marginTopPct, marginBottomPct; }

        /// <summary>A description as the browser broke it into lines, for a card text width (design px).</summary>
        [Serializable] public sealed class DescLines { public string key; public float width; public string[] lines; }

        [Serializable]
        public sealed class FlySpec
        {
            public NamedPic[] icons;
            public int count;
            public float start, every, duration, scale, fadeDelay, fadeDuration, removeAt;
            public string easing, fadeEasing;
            public float[] jitter;
        }

        static LevelPassLayout _loaded;

        /// <summary>layout.json (read once). Null when the exporter hasn't been run.</summary>
        public static LevelPassLayout Load()
        {
            if (_loaded != null) return _loaded;
            var json = Resources.Load<TextAsset>(Folder + "layout");
            if (json == null) return null;
            try { _loaded = JsonUtility.FromJson<LevelPassLayout>(json.text); }
            catch (Exception e) { Debug.LogError("[PlushCrush] Level Pass layout.json unreadable: " + e.Message); }
            return _loaded;
        }

        /// <summary>The player the pictures and the reference screenshots show (a fresh copy each time).</summary>
        public static LevelPassState StandIn()
        {
            var json = Resources.Load<TextAsset>(Folder + "layout");
            if (json == null) return new LevelPassState();
            var copy = JsonUtility.FromJson<LevelPassLayout>(json.text);
            return copy != null && copy.standIn != null ? copy.standIn : new LevelPassState();
        }

        public static NamedPic Find(NamedPic[] list, string key)
        {
            if (list != null) foreach (var p in list) if (p.key == key) return p;
            return null;
        }

        public static PhasedPics Find(PhasedPics[] list, string key)
        {
            if (list != null) foreach (var p in list) if (p.key == key) return p;
            return null;
        }
    }

    /// <summary>A picture: its place relative to its element's box (A15 px) and its size (px).</summary>
    [Serializable]
    public class Pic
    {
        public string image;
        public float[] off;
        public int[] size;
    }

    /// <summary>A picture of a value (a level's lettering, a title, a chest...): key, and its element's box (design px).</summary>
    [Serializable]
    public class NamedPic : Pic
    {
        public string key;
        public float w, h;
    }

    [Serializable] public sealed class BoxPic { public float[] box; public Pic pic; }

    /// <summary>A piece whose width changes. "slice": left cap, a middle strip stretched, right cap. "crop": the left
    /// part cut to length from one drawn long (the wood's stripes stay put), right cap. off: the picture's top-left
    /// from the box's (px); ext: how far its right edge is past the box's (px).</summary>
    [Serializable]
    public sealed class Stretch
    {
        public string mode;
        public Pic left, mid, right, body;
        public float[] off;
        public float ext;
    }

    [Serializable]
    public sealed class TextSpec
    {
        public string font, color;
        public float weight, size, letterSpacing, lineHeight;
        public ShadowSpec[] shadows;
    }

    [Serializable] public sealed class ShadowSpec { public string color; public float x, y, blur; }

    /// <summary>Puts the exported pictures on the A15's pixel grid.</summary>
    public static class Pictures
    {
        public const float K = 2.8125f; // A15 px per dp

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Tex(string image)
        {
            if (string.IsNullOrEmpty(image)) return null;
            if (!Cache.TryGetValue(image, out var tex) || tex == null)
            {
                tex = Resources.Load<Texture2D>(LevelPassLayout.Folder + image);
                if (tex == null) Debug.LogWarning("[PlushCrush] Level Pass picture missing: Resources/" + LevelPassLayout.Folder + image);
                Cache[image] = tex;
            }
            return tex;
        }

        /// <summary>The nearest whole A15 pixel, in dp.</summary>
        public static float Snap(float dp) => Mathf.Round(dp * K) / K;

        public static VisualElement Abs(string name, float left, float top, float width, float height)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = left; e.style.top = top;
            e.style.width = width; e.style.height = height;
            return e;
        }

        static VisualElement Img(string image, float left, float top, float width, float height)
        {
            var e = Abs(image, left, top, width, height);
            e.style.backgroundImage = new StyleBackground(Tex(image));
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            return e;
        }

        /// <summary>A picture for an element whose box is at (boxX, boxY) dp, in a parent whose top-left is at
        /// (parentX, parentY) dp (both in the same space); on the nearest whole px.</summary>
        public static VisualElement Place(Pic p, float boxX, float boxY, float parentX = 0f, float parentY = 0f)
        {
            float x = Snap(boxX + p.off[0] / K), y = Snap(boxY + p.off[1] / K);
            return Img(p.image, x - parentX, y - parentY, p.size[0] / K, p.size[1] / K);
        }

        /// <summary>A piece that stretches, over a box (dp) at (boxX, boxY) of width boxW, in a parent at
        /// (parentX, parentY).</summary>
        public static VisualElement Stretched(Stretch st, float boxX, float boxY, float boxW, float parentX = 0f, float parentY = 0f)
        {
            float x0 = Snap(boxX + st.off[0] / K), y0 = Snap(boxY + st.off[1] / K), x1 = Snap(boxX + boxW + st.ext / K);
            float h = st.right.size[1] / K, rw = st.right.size[0] / K, w = Mathf.Max(0f, x1 - x0);
            var holder = Abs("stretch", x0 - parentX, y0 - parentY, w, h);
            if (st.mode == "crop")
            {
                // the long picture's left part, cut to length (a clip), then the right cap
                var clip = Abs("crop", 0, 0, Mathf.Max(0f, w - rw), h);
                clip.style.overflow = Overflow.Hidden;
                clip.Add(Img(st.body.image, 0, 0, st.body.size[0] / K, st.body.size[1] / K));
                holder.Add(clip);
                holder.Add(Img(st.right.image, w - rw, 0, rw, h));
                return holder;
            }
            float lw = st.left.size[0] / K;
            if (w < lw + rw)
            {
                // shorter than its two caps: half of each (the page's rounded ends meet)
                float half = Snap(w / 2f);
                var l = Abs("left", 0, 0, half, h); l.style.overflow = Overflow.Hidden;
                l.Add(Img(st.left.image, 0, 0, lw, h));
                var r = Abs("right", half, 0, Mathf.Max(0f, w - half), h); r.style.overflow = Overflow.Hidden;
                r.Add(Img(st.right.image, (w - half) - rw, 0, rw, h));
                holder.Add(l); holder.Add(r);
                return holder;
            }
            holder.Add(Img(st.left.image, 0, 0, lw, h));
            holder.Add(Img(st.mid.image, lw, 0, w - lw - rw, h)); // a strip of the uniform middle, stretched
            holder.Add(Img(st.right.image, w - rw, 0, rw, h));
            return holder;
        }
    }

    /// <summary>Text the page draws as text: the same font, size, colour, letter spacing and hard shadows (each a copy
    /// of it, moved), in a box of the page's line height with the glyphs centred in it. Sizes in design px × k.</summary>
    public sealed class PassText : VisualElement
    {
        readonly List<Label> _labels = new List<Label>();

        public PassText(TextSpec spec, float k, string text, float height, TextAnchor align)
        {
            name = "text";
            pickingMode = PickingMode.Ignore;
            style.height = height;
            string font = Fonts.For(spec.font, spec.weight);
            if (spec.shadows != null)
                foreach (var s in spec.shadows)
                {
                    var sh = MakeLabel(spec, k, font, text, Css.ToColor(s.color), align);
                    sh.style.position = Position.Absolute;
                    sh.style.left = s.x * k; sh.style.top = s.y * k;
                    sh.style.right = -s.x * k; sh.style.bottom = -s.y * k;
                    Add(sh);
                }
            var main = MakeLabel(spec, k, font, text, Css.ToColor(spec.color, Color.white), align);
            main.style.height = height;
            Add(main); // last: over its shadows, and the one that gives the box its width
        }

        Label MakeLabel(TextSpec spec, float k, string font, string text, Color color, TextAnchor align)
        {
            var l = new Label(text).Use(font);
            l.pickingMode = PickingMode.Ignore;
            l.style.fontSize = spec.size * k;
            l.style.letterSpacing = spec.letterSpacing * k;
            l.style.color = color;
            l.style.unityTextAlign = align;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            _labels.Add(l);
            return l;
        }

        public string Text
        {
            set { foreach (var l in _labels) l.text = value; }
        }
    }
}
