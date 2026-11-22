// intro.json, written by tools/unity-port/export-intro.mjs: the intro's pictures (Resources/Screens/Intro) and where
// each goes, measured in the game on the Galaxy A15.
//   - World pieces: px = x, y, width, height in the scene picture's own pixel grid (the 1000 x 460 drawing at the
//     A15's scale, 2348 x 1080: scene units -> picture px = unit * sceneScale + shift). MenuIntro lays a box over the
//     screen exactly where the menu's scene picture goes (cover, centred, on the bottom) and these in it.
//   - Overlay pieces: px on the A15's screen (2340 x 1080) and box, the element's own box in dp (a caption's, a
//     letter's: what their transforms turn about).
using System;
using System.Collections.Generic;
using System.Globalization;
using PlushCrush.Core.Rig;
using UnityEngine;

namespace PlushCrush.App.MainMenu
{
    public sealed class IntroLayout
    {
        public const string Folder = "Screens/Intro/";

        public sealed class Piece
        {
            public string Image; public int[] Px; public float[] Box, Span, At;
            public Texture2D Texture => _tex != null ? _tex : (_tex = Load(Image));
            Texture2D _tex;
        }

        public sealed class Season
        {
            public string Night, Morning;
            public Piece Doorlight;
            public readonly Dictionary<string, Piece> Cuts = new Dictionary<string, Piece>();
            public readonly Dictionary<string, Piece[]> Halos = new Dictionary<string, Piece[]>(), Panes = new Dictionary<string, Piece[]>();
            public float[] DawnKeys;
        }

        public sealed class Peek { public string Key, Room; public int Wi; public float Cx, Sz; public Piece Pic; public float[] At; }
        public sealed class Reference { public string Name, Season; public double T, SkipAt = double.NaN; }

        public int[] ScenePixels; public float SceneScale, Shift;
        public (float at, Color c)[] Veil, Dawn;
        public readonly Dictionary<string, Season> Seasons = new Dictionary<string, Season>();
        public Piece Moon, BoxFace, BoxShadow, Glow, Skip, Tap;
        public Color BoxInside, FlapL, FlapR, Stroke; public float StrokeWidth = 2;
        public Peek[] Peeks;
        public readonly Dictionary<string, Piece> Captions = new Dictionary<string, Piece>();
        /// <summary>Each room's halos and panes as they are (normal blending): how they show while the intro's layers
        /// fade at the end (the back layer is then a group of its own: its blend modes no longer reach the house).</summary>
        public readonly Dictionary<string, Piece> PlainHalos = new Dictionary<string, Piece>(), PlainPanes = new Dictionary<string, Piece>();
        public Piece[] Logo;
        public double ScriptTapBox, ScriptStart; public double[][] ScriptSquish;
        public Reference[] References;

        public static bool Exists => Resources.Load<TextAsset>(Folder + "intro") != null;

        public static Texture2D Load(string image)
        {
            if (string.IsNullOrEmpty(image)) return null;
            var t = Resources.Load<Texture2D>(Folder + image);
            if (t == null) Debug.LogWarning("[PlushCrush] Intro picture missing: Resources/" + Folder + image);
            return t;
        }

        static IntroLayout _it;
        public static IntroLayout Get()
        {
            if (_it != null) return _it;
            var ta = Resources.Load<TextAsset>(Folder + "intro");
            if (ta == null) return null;
            try { _it = Parse((Dictionary<string, object>)MiniJson.Parse(ta.text)); }
            catch (Exception e) { Debug.LogError("[PlushCrush] intro.json unreadable: " + e); }
            return _it;
        }

        // ---- reading ----
        static Dictionary<string, object> O(object o) => o as Dictionary<string, object>;
        static List<object> A(object o) => o as List<object>;
        static object G(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
        static float F(object o) => o is double d ? (float)d : 0f;
        static string S(object o) => o as string;
        static float[] Fs(object o) { var a = A(o); if (a == null) return null; var r = new float[a.Count]; for (int i = 0; i < r.Length; i++) r[i] = F(a[i]); return r; }
        static int[] Is(object o) { var a = A(o); if (a == null) return null; var r = new int[a.Count]; for (int i = 0; i < r.Length; i++) r[i] = Mathf.RoundToInt(F(a[i])); return r; }
        static Color Col(object o) => Css.ToColor(S(o), Color.magenta);

        static Piece P(object o)
        {
            var d = O(o); if (d == null) return null;
            return new Piece { Image = S(G(d, "image")), Px = Is(G(d, "px")), Box = Fs(G(d, "box")), Span = Fs(G(d, "span")), At = Fs(G(d, "at")) };
        }

        static (float, Color)[] Stops(object o)
        {
            var a = A(o); var r = new (float, Color)[a.Count];
            for (int i = 0; i < r.Length; i++) { var s = A(a[i]); r[i] = (F(s[0]), Col(s[1])); }
            return r;
        }

        static IntroLayout Parse(Dictionary<string, object> j)
        {
            var L = new IntroLayout();
            var sc = O(G(j, "scene"));
            L.ScenePixels = Is(G(sc, "pixels")); L.SceneScale = F(G(sc, "sceneScale")); L.Shift = F(G(sc, "shift"));
            L.Veil = Stops(G(j, "veil")); L.Dawn = Stops(G(j, "dawn"));
            foreach (var kv in O(G(j, "seasons")))
            {
                var d = O(kv.Value); var s = new Season { Night = S(G(d, "night")), Morning = S(G(d, "morning")), Doorlight = P(G(d, "doorlight")), DawnKeys = Fs(G(d, "dawnKeys")) };
                foreach (var c in O(G(d, "cuts"))) s.Cuts[c.Key] = P(c.Value);
                foreach (var (name, dict) in new[] { ("halos", s.Halos), ("panes", s.Panes) })
                    foreach (var c in O(G(d, name))) { var a = A(c.Value); var ps = new Piece[a.Count]; for (int i = 0; i < ps.Length; i++) ps[i] = P(a[i]); dict[c.Key] = ps; }
                L.Seasons[kv.Key] = s;
            }
            L.Moon = P(G(j, "moon")); L.Glow = P(G(j, "glow")); L.Skip = P(G(j, "skip")); L.Tap = P(G(j, "tap"));
            var box = O(G(j, "box"));
            L.BoxFace = P(G(box, "face")); L.BoxShadow = P(G(box, "shadow"));
            L.BoxInside = Col(G(box, "inside")); L.FlapL = Col(G(box, "flapL")); L.FlapR = Col(G(box, "flapR")); L.Stroke = Col(G(box, "stroke")); L.StrokeWidth = F(G(box, "strokeWidth"));
            var pk = A(G(j, "peeks")); L.Peeks = new Peek[pk.Count];
            for (int i = 0; i < pk.Count; i++)
            {
                var d = O(pk[i]);
                L.Peeks[i] = new Peek { Key = S(G(d, "key")), Room = S(G(d, "room")), Wi = (int)F(G(d, "wi")), Cx = F(G(d, "cx")), Sz = F(G(d, "sz")), At = Fs(G(d, "at")),
                    Pic = new Piece { Image = S(G(d, "image")), Px = Is(G(d, "px")) } };
            }
            foreach (var c in O(G(j, "captions"))) L.Captions[c.Key] = P(c.Value);
            var plain = O(G(j, "plain"));
            if (plain != null)
            {
                foreach (var c in O(G(plain, "halo")) ?? new Dictionary<string, object>()) L.PlainHalos[c.Key] = P(c.Value);
                foreach (var c in O(G(plain, "pane")) ?? new Dictionary<string, object>()) L.PlainPanes[c.Key] = P(c.Value);
            }
            var lg = A(G(j, "logo")); L.Logo = new Piece[lg.Count]; for (int i = 0; i < lg.Count; i++) L.Logo[i] = P(lg[i]);
            var sp = O(G(j, "script"));
            L.ScriptTapBox = F(G(sp, "tapBox")); L.ScriptStart = F(G(sp, "start"));
            var sq = A(G(sp, "squish")); L.ScriptSquish = new double[sq.Count][];
            for (int i = 0; i < sq.Count; i++) { var f = Fs(sq[i]); L.ScriptSquish[i] = new double[] { f[0], f[1] }; }
            var refs = A(G(j, "references")); L.References = new Reference[refs.Count];
            for (int i = 0; i < refs.Count; i++)
            {
                var d = O(refs[i]);
                L.References[i] = new Reference { Name = S(G(d, "name")), Season = S(G(d, "season")), T = F(G(d, "t")), SkipAt = G(d, "skipAt") is double k ? k : double.NaN };
            }
            return L;
        }

        public static string Num(float v) => v.ToString(CultureInfo.InvariantCulture);
    }
}
