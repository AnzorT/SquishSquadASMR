// A live creature in the UI: the game's rig (RigEngine: moves, blinks, looks; RigRender: its outlines; RigDisplay +
// RigDrawer: drawn on the GPU) in a VisualElement the size of the rig's 100 x 100 box (the game's rig <svg>). The
// drawing reaches past the box (ears, hops, the tail, the shadow), so its texture covers a larger area round it, placed
// on whole screen pixels so the UI shows it without resampling.
//
// RigLibrary loads what the rig needs once: its moves (Rig/rig-states.json, 12 MB, parsed on a worker thread), each
// creature's geometry (Rig/geo-<key>.json) and the fabric grain (Rig/fabric.png), all exported from the game by
// tools/unity-port/rig/record-rig.mjs.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlushCrush.Core.Rig;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.Rig
{
    public static class RigLibrary
    {
        static Task<RigData> _loading;
        static RigData _data;
        static readonly Dictionary<string, RigGeo> _geo = new Dictionary<string, RigGeo>();
        static Shader _shader; static Texture2D _grain;

        /// <summary>Starts loading the moves (once). Ready says when they're in.</summary>
        public static void Preload()
        {
            if (_loading != null || _data != null) return;
            var ta = Resources.Load<TextAsset>("Rig/rig-states");
            if (ta == null) { Debug.LogWarning("RigLibrary: Rig/rig-states.json is missing (run tools/unity-port/rig/record-rig.mjs)"); return; }
            string json = ta.text;
            Resources.UnloadAsset(ta);
            _loading = Task.Run(() => RigData.FromJson(json));
        }

        public static RigData Data
        {
            get
            {
                if (_data == null && _loading != null && _loading.IsCompleted)
                {
                    if (_loading.IsFaulted) { Debug.LogError("RigLibrary: " + _loading.Exception); _loading = null; }
                    else _data = _loading.Result;
                }
                return _data;
            }
        }

        public static bool Ready => Data != null;

        /// <summary>The creature's geometry, or null when it hasn't been exported.</summary>
        public static RigGeo Geo(string key)
        {
            if (_geo.TryGetValue(key, out var g)) return g;
            var ta = Resources.Load<TextAsset>("Rig/geo-" + key);
            g = ta != null ? RigGeo.FromJson(ta.text) : null;
            _geo[key] = g;
            return g;
        }

        public static Shader Shader => _shader != null ? _shader : (_shader = Shader.Find("Hidden/PlushCrush/RigFill"));
        public static Texture2D Grain => _grain != null ? _grain : (_grain = Resources.Load<Texture2D>("Rig/fabric"));

        /// <summary>Why this creature can't be drawn live yet (null when it can).</summary>
        public static string Unsupported(string key)
        {
            var g = Geo(key);
            if (g == null) return "no geometry exported for " + key;
            return RigRender.Unported(g);
        }
    }

    /// <summary>Runs the live rigs every frame (after the game's Update, before the UI draws).</summary>
    sealed class RigTicker : MonoBehaviour
    {
        static RigTicker _it;
        static readonly List<RigView> Views = new List<RigView>();

        public static void Add(RigView v)
        {
            if (_it == null) { var go = new GameObject("RigTicker") { hideFlags = HideFlags.HideAndDontSave }; DontDestroyOnLoad(go); _it = go.AddComponent<RigTicker>(); }
            if (!Views.Contains(v)) Views.Add(v);
        }

        public static void Remove(RigView v) => Views.Remove(v);

        void LateUpdate()
        {
            for (int i = Views.Count - 1; i >= 0; i--)
            {
                try { Views[i].Tick(); }
                catch (Exception e) { Debug.LogException(e); Views.RemoveAt(i); }
            }
        }
    }

    public sealed class RigView : VisualElement
    {
        // the area drawn round the 100 x 100 box, in rig units (a hop, the tail, the ears and the shadow fit)
        const float X0 = -40, Y0 = -45, X1 = 140, Y1 = 115;

        public readonly string Key;
        public readonly RigEngine Engine;
        readonly RigRender _render;
        readonly RigDisplay _display;
        readonly RigFx _fx = new RigFx();
        RigDrawer _drawer;
        readonly VisualElement _img;
        readonly System.Random _rnd = new System.Random();
        double _last = -1;
        bool _attached;

        /// <summary>Screen pixels per UI unit (the panel's scale); set by whoever places the view.</summary>
        public float PixelsPerUnit = 2.8125f;

        public RigView(string key)
        {
            Key = key;
            pickingMode = PickingMode.Ignore;
            var geo = RigLibrary.Geo(key) ?? throw new InvalidOperationException("RigView: no geometry for " + key);
            var data = RigLibrary.Data ?? throw new InvalidOperationException("RigView: the rig's moves aren't loaded yet");
            double now = Now();
            Engine = new RigEngine(data, key, now, () => _rnd.NextDouble());
            _render = new RigRender(geo, key);
            Engine.FromM = _render.FromM;
            _display = new RigDisplay(RigTree.FromGeo(geo, _render), _render) { Fx = _fx };
            // mounting runs one frame (dt 0.016), as the game's mount() does
            var P = Engine.Tick(now, 0.016); _render.Render(P, Engine, now); _last = now;

            _img = new VisualElement { name = "rig-drawing", pickingMode = PickingMode.Ignore };
            _img.style.position = Position.Absolute;
            Add(_img);
            RegisterCallback<AttachToPanelEvent>(_ => { _attached = true; RigTicker.Add(this); });
            RegisterCallback<DetachFromPanelEvent>(_ => { _attached = false; RigTicker.Remove(this); _drawer?.Dispose(); _drawer = null; _img.style.backgroundImage = StyleKeyword.None; });
            RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        static double Now() => Time.realtimeSinceStartupAsDouble * 1000.0;
        /// <summary>The rig's clock (ms), for touches and moves.</summary>
        public static double Clock => Now();

        /// <summary>A CSS filter on the creature (menu.js light(): the night's brightness / saturate / sepia) as a colour
        /// matrix; null: none.</summary>
        public Matrix4x4? ColorFilter { get => _filter; set { _filter = value; if (_drawer != null) _drawer.ColorMatrix = value; } }
        Matrix4x4? _filter;

        /// <summary>The drawing's texture: on whole screen pixels, X0..X1 x Y0..Y1 rig units round the box. Measured
        /// without the transforms of the boxes it's in (a camera move, the intro's grow-out-of-the-box): the drawing is
        /// made for the box at rest, and a transform moves and scales it like any picture.</summary>
        void Place()
        {
            var r = LayoutRect(this);
            if (!(r.width > 1)) return;
            float k = r.width / 100f * PixelsPerUnit;                  // screen pixels per rig unit
            float bx = r.x * PixelsPerUnit, by = r.y * PixelsPerUnit;  // the box's corner on screen, pixels
            int px0 = Mathf.FloorToInt(bx + X0 * k), py0 = Mathf.FloorToInt(by + Y0 * k), px1 = Mathf.CeilToInt(bx + X1 * k), py1 = Mathf.CeilToInt(by + Y1 * k);
            _img.style.left = (px0 - bx) / PixelsPerUnit; _img.style.top = (py0 - by) / PixelsPerUnit;
            _img.style.width = (px1 - px0) / PixelsPerUnit; _img.style.height = (py1 - py0) / PixelsPerUnit;
            if (_drawer == null) _drawer = new RigDrawer(RigLibrary.Shader, RigLibrary.Grain) { ColorMatrix = _filter };
            _drawer.Resize(px1 - px0, py1 - py0, k, bx - px0, by - py0);
            _display.PxPerUnit = k;
            _img.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_drawer.Output));
        }

        static Rect LayoutRect(VisualElement e)
        {
            var p = Vector2.zero;
            for (var v = e; v != null; v = v.hierarchy.parent) p += v.layout.position;
            return new Rect(p, e.layout.size);
        }

        /// <summary>One frame: the rig's tick (dt as the game's loop: 1 ms .. 50 ms), its outlines, the drawing.</summary>
        internal void Tick()
        {
            if (!_attached) return;
            double now = Now();
            double dt = Math.Max(0.001, Math.Min(0.05, (now - _last) / 1000.0)); _last = now;
            var P = Engine.Tick(now, dt);
            _render.Render(P, Engine, now);
            _fx.Build(Engine.CurName, now - Engine.CurT0, _render.TopW);
            if (_drawer == null || _drawer.Output == null) return;
            _display.Build();
            _drawer.Draw(_display.Cmds);
            _img.MarkDirtyRepaint();
        }

        public void Play(string move) => Engine.Play(move, Now());
    }
}
