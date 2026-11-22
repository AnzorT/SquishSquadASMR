// layout.json of the Game Park overlay, written by tools/unity-port/export-game-park.mjs: where each piece goes,
// measured in the game itself on the Galaxy A15 (832 x 384 dp, 2.8125 px per dp), in A15 screen pixels, with every
// card measured while it is the centre one. Pictures sit next to it in Resources/Screens/GamePark.
using System;
using UnityEngine;

namespace PlushCrush.App.MainMenu
{
    [Serializable]
    public sealed class GameParkLayout
    {
        public const string Folder = "Screens/GamePark/";

        public MenuLayout.ScreenSpec screen;
        public DefaultsSpec defaults;
        public PageSpec page;
        public BoxSpec car, slot;
        public Pic dim;
        public HeaderSpec header;
        public Pic rays, halo;
        public ArrowsSpec arrows;
        public DotsSpec dots;
        public CardSpec[] cards;
        public ChipSpec chip;
        public TextsSpec text;
        public StarsSpec stars;
        public ToastSpec toast;

        /// <summary>The state the pictures were taken in: the creature on Squish Time's card, the centre card.</summary>
        [Serializable] public sealed class DefaultsSpec { public string creature; public int cur; }

        /// <summary>The page's own numbers: its scale k (screen height / 393), the carousel's left edge (carL) and top,
        /// the card width CW and the slot's height, all in the page's px (menu.js showGames).</summary>
        [Serializable] public sealed class PageSpec { public float k, designHeight, carL, carTop, CW, slotHeight; }

        /// <summary>An element's box in A15 px (fractional: the page's boxes are).</summary>
        [Serializable] public sealed class BoxSpec { public float[] box; }

        /// <summary>A picture and where it goes (A15 px: x, y, width, height). bright: its brightness(1.35) twin.</summary>
        [Serializable]
        public sealed class Pic
        {
            public string image, bright;
            public int[] px;
            public float[] offset, box;
        }

        [Serializable] public sealed class HeaderSpec { public float[] box; public Pic back, title; }

        [Serializable]
        public sealed class ArrowsSpec
        {
            public Pic left, right;
            public float[] leftBox, rightBox;
            public float offOpacity;
        }

        /// <summary>The dots: the current one and the others, both drawn 18 wide (9-sliced to their width: the slice
        /// keeps `slice` px whole at each end); the row's box when the first is current; the page's sizes (px).</summary>
        [Serializable]
        public sealed class DotsSpec
        {
            public Pic on, off;
            public int slice;
            public float[] row;
            public float wOn, wOff, h, gap, bottom;
        }

        [Serializable]
        public sealed class CardSpec
        {
            public string id, name;
            public bool open;
            public Pic frame, shade, @lock;
            public WinSpec win;
            public Pic locked;       // the window when the game is locked (saturate(.2) brightness(.7))
            public GoSpec go;
            public DescSpec desc;
            public BestSpec best;
            public SquishSpec squish;
        }

        [Serializable] public sealed class WinSpec { public float[] box; public float radius; public string image, bright; public int[] px; }

        [Serializable] public sealed class GoSpec { public float[] box; public Pic play, dim, bright, locked, lockedDim, lockedBright; }

        [Serializable] public sealed class DescSpec { public string text; public float[] box; public float maxWidth; public LineSpec[] lines; }
        [Serializable] public sealed class LineSpec { public string text; public float[] box; }
        [Serializable] public sealed class BestSpec { public string text; public float[] box, textBox; }

        [Serializable]
        public sealed class SquishSpec
        {
            public float[] imgBox;      // the creature's <img> box: it squishes from its bottom centre
            public Pic lineL, lineR;
            public CreatureSpec creature;
            public ClipSpec clip;
        }

        [Serializable] public sealed class CreatureSpec { public string key, image, bright; public int[] px; }

        /// <summary>Frybo's card: the clip's first frame (a window picture) and the clip (a VideoClip in Resources).</summary>
        [Serializable] public sealed class ClipSpec { public string creature, image, bright, video; public int[] px, size; }

        [Serializable] public sealed class ChipSpec { public string image, bright; public int[] px; public float[] box; public int slice; }

        [Serializable] public sealed class TextsSpec { public TextStyle desc, best, toast; }

        /// <summary>A text's CSS, in the page's px (Unity multiplies sizes by k, as the page's scale(k) does).</summary>
        [Serializable]
        public sealed class TextStyle
        {
            public string font, color, background;
            public float weight, size, letterSpacing, lineHeight, border;
            public MenuLayout.ShadowSpec[] shadows;
            public float[] padding;   // top, right, bottom, left
        }

        /// <summary>The ★ in the line's and the chip's style (the fonts have none: the browser's fallback drawing of
        /// it, box = its advance and line), and a space's advance in each style (A15 px).</summary>
        [Serializable] public sealed class StarsSpec { public Pic desc, best; public float descSpace, bestSpace; }

        [Serializable]
        public sealed class ToastSpec
        {
            public string image, sample;
            public int[] px;
            public float[] box, textBox;
            public int slice;
            public float bottom, maxWidth;
        }

        /// <summary>Squish Time's picture per creature (creatures.json; the pictures are generated, not in git).</summary>
        [Serializable] public sealed class CreatureIndex { public CreatureItem[] items; }
        [Serializable] public sealed class CreatureItem { public string key, name; public int[] px; }

        public CardSpec Card(string id)
        {
            if (cards != null) foreach (var c in cards) if (c != null && c.id == id) return c;
            return null;
        }

        /// <summary>JsonUtility fills a missing object with an empty one: a picture is there when it has a name.</summary>
        public static bool Has(Pic p) => p != null && !string.IsNullOrEmpty(p.image) && p.px != null && p.px.Length == 4;

        static GameParkLayout _cached;
        static CreatureIndex _creatures;

        public static GameParkLayout Load()
        {
            if (_cached != null) return _cached;
            var json = Resources.Load<TextAsset>(Folder + "layout");
            if (json == null) return null;
            try { _cached = JsonUtility.FromJson<GameParkLayout>(json.text); }
            catch (Exception e) { Debug.LogError("[PlushCrush] Game Park layout.json unreadable: " + e.Message); return null; }
            return _cached;
        }

        public static bool Exists => Resources.Load<TextAsset>(Folder + "layout") != null;

        public static CreatureItem FindCreature(string key)
        {
            if (_creatures == null)
            {
                var json = Resources.Load<TextAsset>(Folder + "creatures");
                try { _creatures = json != null ? JsonUtility.FromJson<CreatureIndex>(json.text) : null; }
                catch (Exception e) { Debug.LogWarning("[PlushCrush] Game Park creatures.json unreadable: " + e.Message); }
                if (_creatures == null) _creatures = new CreatureIndex();
            }
            if (_creatures.items != null) foreach (var it in _creatures.items) if (it != null && it.key == key) return it;
            return null;
        }

        public static Texture2D Picture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var tex = Resources.Load<Texture2D>(Folder + name);
            if (tex == null) Debug.LogWarning("[PlushCrush] Game Park picture missing: Resources/" + Folder + name);
            return tex;
        }
    }
}
