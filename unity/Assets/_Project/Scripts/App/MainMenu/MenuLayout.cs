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
        public SceneSpec scene;
        public PartSpec[] parts;
        public TextSpec[] texts;
        public WeatherSpec weather;

        [Serializable] public sealed class ScreenSpec { public float width, height, scale; }

        [Serializable]
        public sealed class SceneSpec
        {
            public string image, align, season;
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
