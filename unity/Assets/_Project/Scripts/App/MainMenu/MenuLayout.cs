// layout.json, written by tools/unity-port/export-menu.mjs: where each piece of the Main Menu goes, measured in the
// game itself on the Galaxy A15 (832 x 384 dp, 2.8125 px per dp). Pictures sit next to it in Resources.
using System;
using UnityEngine;

namespace PlushCrush.App.MainMenu
{
    [Serializable]
    public sealed class MenuLayout
    {
        public const string Folder = "Screens/MainMenu/";

        public ScreenSpec screen;
        public DefaultsSpec defaults;
        public SceneSpec scene;
        public ParkSpec park;
        public PartSpec[] parts;
        public TextSpec[] texts;
        public WeatherSpec weather;

        [Serializable] public sealed class ScreenSpec { public float width, height, scale; }

        /// <summary>The state the default pictures show: the reference screenshots' (level 1, rainy, Mittens).</summary>
        [Serializable] public sealed class DefaultsSpec { public string creature, season; public int level; }

        /// <summary>The Game Park's picture (default state): d, top = its left and top edges in scene units, from the
        /// house picture's left edge minus the screen's width, and from the scene's top; sceneScale = px per unit.</summary>
        [Serializable] public sealed class ParkSpec { public string image; public float d, top, sceneScale; }

        [Serializable]
        public sealed class SceneSpec
        {
            public string image, align, season, vignette;
            public float[] viewBox;
            public int[] pixels;
            public float subpixelShift;
            public int level;
        }

        [Serializable]
        public sealed class PartSpec
        {
            public string name, image, anchor, layer, value;
            public float x, y, w, h;
            public int[] px; // the same box in screen pixels on the A15: x, y, width, height
            public float[] sceneAt; // anchor "scene": its bottom-center point in scene units (viewBox 1000 x 460)
        }

        [Serializable]
        public sealed class TextSpec
        {
            public string key, parent, anchor, text, font, color;
            public float x, y, w, h, weight, size, letterSpacing;
            public ShadowSpec[] shadows;
        }

        [Serializable] public sealed class ShadowSpec { public string color; public float x, y, blur; }

        [Serializable]
        public sealed class WeatherSpec
        {
            public string season;
            public float f, bandHeight;
            public int level;
            public int[] bands;
            public RainSpec rain;
            public SnowSpec snow;
            public float[] frozenOffset; // the moving layer's offset in the reference screenshot, in dp
        }

        [Serializable]
        public sealed class RainSpec
        {
            public DropSpec[] drops;
            public float[] line, from, to;
            public string color;
            public float width, ms;
        }

        [Serializable] public sealed class DropSpec { public float x, y, opacity; }

        [Serializable]
        public sealed class SnowSpec
        {
            public FlakeSpec[] flakes;
            public float[] from, to;
            public string color;
            public float ms;
        }

        [Serializable] public sealed class FlakeSpec { public float x, y, r, opacity; }

        /// <summary>Where each picture of a value goes (Names/index.json, Levels/index.json, creatures.json).</summary>
        [Serializable]
        public sealed class ValueIndex
        {
            public ValueItem[] items;
            public ValueItem Find(string key)
            {
                if (items != null) foreach (var it in items) if (it.key == key) return it;
                return null;
            }
        }

        [Serializable]
        public sealed class ValueItem
        {
            public string key, name;
            public int rar;
            public int[] px;
            public float[] sceneAt;
            public float d, top; // parks.json
        }

        static ValueIndex _names, _levels, _creatures, _parks;
        public static ValueIndex Parks => _parks ??= LoadIndex(Folder + "parks");
        public static ValueIndex Names => _names ??= LoadIndex(Folder + "Names/index");
        public static ValueIndex Levels => _levels ??= LoadIndex(Folder + "Levels/index");
        public static ValueIndex Creatures => _creatures ??= LoadIndex(Folder + "creatures");

        static ValueIndex LoadIndex(string path)
        {
            var json = Resources.Load<TextAsset>(path);
            if (json == null) return new ValueIndex();
            try { return JsonUtility.FromJson<ValueIndex>(json.text) ?? new ValueIndex(); }
            catch (Exception e) { Debug.LogWarning("[PlushCrush] " + path + ".json unreadable: " + e.Message); return new ValueIndex(); }
        }

        /// <summary>A picture anywhere under a Resources folder (the generated ones live in Art/Generated/Resources).
        /// Null, quietly, when it isn't there: callers fall back to the committed default.</summary>
        public static Texture2D Optional(string resourcesPath) => Resources.Load<Texture2D>(resourcesPath);

        public static MenuLayout Load()
        {
            var json = Resources.Load<TextAsset>(Folder + "layout");
            if (json == null) return null;
            try { return JsonUtility.FromJson<MenuLayout>(json.text); }
            catch (Exception e) { Debug.LogError("[PlushCrush] Main Menu layout.json unreadable: " + e.Message); return null; }
        }

        public static bool Exists => Resources.Load<TextAsset>(Folder + "layout") != null;

        public static Texture2D Picture(string name)
        {
            var tex = Resources.Load<Texture2D>(Folder + name);
            if (tex == null) Debug.LogWarning("[PlushCrush] Main Menu picture missing: Resources/" + Folder + name);
            return tex;
        }
    }
}
