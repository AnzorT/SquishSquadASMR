// CSS animations, replayed in Unity with the browser's own rules, so the game's @keyframes play exactly as they did:
//   - timing functions: cubic-bezier (solved like browsers do), the named ones (ease, ease-in, ...), steps(),
//     applied per keyframe interval (a keyframe's own animation-timing-function wins over the animation's);
//   - duration, delay (negative too), iteration count (fractional, infinite), direction (alternate...), fill mode;
//   - transform = translate(x, y) rotate(a) scale(sx, sy) in that order, around transform-origin; opacity.
// Values not given in a keyframe are taken from the element's base (as CSS does for missing properties).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.Motion
{
    public static class Easing
    {
        public static Func<float, float> CubicBezier(float x1, float y1, float x2, float y2)
        {
            // as in browsers (WebKit's UnitBezier): Newton's method on x(t), bisection when that stalls
            float cx = 3f * x1, bx = 3f * (x2 - x1) - cx, ax = 1f - cx - bx;
            float cy = 3f * y1, by = 3f * (y2 - y1) - cy, ay = 1f - cy - by;
            float X(float t) => ((ax * t + bx) * t + cx) * t;
            float Y(float t) => ((ay * t + by) * t + cy) * t;
            float dX(float t) => (3f * ax * t + 2f * bx) * t + cx;
            return x =>
            {
                if (x <= 0f) return 0f;
                if (x >= 1f) return 1f;
                float t = x;
                for (int i = 0; i < 8; i++)
                {
                    float e = X(t) - x;
                    if (Mathf.Abs(e) < 1e-6f) return Y(t);
                    float d = dX(t);
                    if (Mathf.Abs(d) < 1e-6f) break;
                    t -= e / d;
                }
                float lo = 0f, hi = 1f; t = x;
                for (int i = 0; i < 30; i++)
                {
                    float v = X(t);
                    if (Mathf.Abs(v - x) < 1e-6f) break;
                    if (x > v) lo = t; else hi = t;
                    t = (lo + hi) / 2f;
                }
                return Y(t);
            };
        }

        public static readonly Func<float, float> Linear = x => x;
        public static readonly Func<float, float> Ease = CubicBezier(0.25f, 0.1f, 0.25f, 1f);
        public static readonly Func<float, float> EaseIn = CubicBezier(0.42f, 0f, 1f, 1f);
        public static readonly Func<float, float> EaseOut = CubicBezier(0f, 0f, 0.58f, 1f);
        public static readonly Func<float, float> EaseInOut = CubicBezier(0.42f, 0f, 0.58f, 1f);

        public static Func<float, float> Steps(int n, bool jumpStart = false) =>
            x => Mathf.Clamp01((jumpStart ? Mathf.Ceil(x * n) : Mathf.Floor(x * n)) / n);

        /// <summary>A CSS timing function as written: "ease", "linear", "cubic-bezier(.2,.9,.25,1)", "steps(4)".</summary>
        public static Func<float, float> Parse(string css)
        {
            if (string.IsNullOrEmpty(css)) return Ease;
            css = css.Trim();
            switch (css)
            {
                case "linear": return Linear;
                case "ease": return Ease;
                case "ease-in": return EaseIn;
                case "ease-out": return EaseOut;
                case "ease-in-out": return EaseInOut;
                case "step-start": return Steps(1, true);
                case "step-end": return Steps(1);
            }
            int o = css.IndexOf('('), c = css.LastIndexOf(')');
            if (o > 0 && c > o)
            {
                var a = css.Substring(o + 1, c - o - 1).Split(',');
                if (css.StartsWith("cubic-bezier") && a.Length == 4)
                    return CubicBezier(F(a[0]), F(a[1]), F(a[2]), F(a[3]));
                if (css.StartsWith("steps") && a.Length >= 1)
                    return Steps(int.Parse(a[0].Trim()), a.Length > 1 && (a[1].Trim() == "start" || a[1].Trim() == "jump-start"));
            }
            Debug.LogWarning("[PlushCrush] Unknown timing function: " + css);
            return Ease;
        }

        static float F(string s) => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>One keyframe: its offset (0..1) and the values it sets (null = not set here).</summary>
    public sealed class CssKeyframe
    {
        public float Offset;
        public float? X, Y, Rotate, ScaleX, ScaleY, Opacity;
        public Func<float, float> Easing; // this keyframe's animation-timing-function (to the next one); null = the animation's

        public CssKeyframe(float offset) { Offset = offset; }
        public CssKeyframe Move(float x, float y) { X = x; Y = y; return this; }
        public CssKeyframe Rot(float deg) { Rotate = deg; return this; }
        public CssKeyframe Scale(float s) { ScaleX = s; ScaleY = s; return this; }
        public CssKeyframe Scale(float sx, float sy) { ScaleX = sx; ScaleY = sy; return this; }
        public CssKeyframe Fade(float o) { Opacity = o; return this; }
        public CssKeyframe Ease(string css) { Easing = Motion.Easing.Parse(css); return this; }
    }

    public enum AnimFill { None, Forwards, Backwards, Both }
    public enum AnimDirection { Normal, Reverse, Alternate, AlternateReverse }

    public sealed class CssAnimation
    {
        public readonly List<CssKeyframe> Frames = new List<CssKeyframe>();
        public float Duration = 1f, Delay;
        public float Iterations = 1f; // float.PositiveInfinity = infinite
        public AnimDirection Dir = AnimDirection.Normal;
        public AnimFill Fill = AnimFill.None;
        public Func<float, float> Timing = Easing.Ease;

        public CssAnimation Key(CssKeyframe k) { Frames.Add(k); Frames.Sort((a, b) => a.Offset.CompareTo(b.Offset)); return this; }

        /// <summary>The values at a time since the animation started (seconds), or false when it shows the base style.</summary>
        public bool Sample(float time, CssPose base0, out CssPose p)
        {
            p = base0;
            if (Frames.Count == 0 || Duration <= 0f) return false;
            float local = time - Delay;
            float total = float.IsInfinity(Iterations) ? float.PositiveInfinity : Duration * Iterations;
            float iterTime; int iter;
            if (local < 0f)
            {
                if (Fill != AnimFill.Backwards && Fill != AnimFill.Both) return false;
                iterTime = 0f; iter = 0;
            }
            else if (local >= total)
            {
                if (Fill != AnimFill.Forwards && Fill != AnimFill.Both) return false;
                // the end of the last iteration (a fractional count ends part-way)
                float it = Iterations;
                iter = Mathf.Max(0, Mathf.CeilToInt(it) - 1);
                float frac = it - Mathf.Floor(it);
                iterTime = frac > 0f ? frac * Duration : Duration;
            }
            else
            {
                iter = Mathf.FloorToInt(local / Duration);
                iterTime = local - iter * Duration;
            }
            float progress = Mathf.Clamp01(iterTime / Duration);
            bool reverse = Dir == AnimDirection.Reverse
                           || (Dir == AnimDirection.Alternate && iter % 2 == 1)
                           || (Dir == AnimDirection.AlternateReverse && iter % 2 == 0);
            if (reverse) progress = 1f - progress;

            p.X = Prop(progress, k => k.X, base0.X);
            p.Y = Prop(progress, k => k.Y, base0.Y);
            p.Rotate = Prop(progress, k => k.Rotate, base0.Rotate);
            p.ScaleX = Prop(progress, k => k.ScaleX, base0.ScaleX);
            p.ScaleY = Prop(progress, k => k.ScaleY, base0.ScaleY);
            p.Opacity = Prop(progress, k => k.Opacity, base0.Opacity);
            return true;
        }

        // CSS: each property moves between the keyframes that set it. Where no keyframe sets it at 0% or 100%, CSS makes
        // one from the element's own value (the base), eased by the animation's timing function. Each interval is
        // eased by the timing function of the keyframe it starts from.
        float Prop(float progress, Func<CssKeyframe, float?> get, float baseValue)
        {
            float prevOff = 0f, prevVal = baseValue;
            Func<float, float> prevEase = Timing;
            foreach (var k in Frames)
            {
                var v = get(k);
                if (v == null) continue;
                if (k.Offset <= progress)
                {
                    prevOff = k.Offset; prevVal = v.Value; prevEase = k.Easing ?? Timing;
                    continue;
                }
                return Between(prevOff, prevVal, k.Offset, v.Value, prevEase, progress);
            }
            if (prevOff >= 1f) return prevVal;
            return Between(prevOff, prevVal, 1f, baseValue, prevEase, progress); // towards the implicit 100%
        }

        static float Between(float aOff, float aVal, float bOff, float bVal, Func<float, float> ease, float progress)
        {
            float span = bOff - aOff;
            float t = span > 1e-6f ? (progress - aOff) / span : 1f;
            return Mathf.LerpUnclamped(aVal, bVal, ease(Mathf.Clamp01(t)));
        }
    }

    /// <summary>The transformable state of an element.</summary>
    public struct CssPose
    {
        public float X, Y, Rotate, ScaleX, ScaleY, Opacity;
        public static CssPose Identity => new CssPose { ScaleX = 1f, ScaleY = 1f, Opacity = 1f };

        public void ApplyTo(VisualElement e)
        {
            e.style.translate = new Translate(X, Y);
            e.style.rotate = new Rotate(Angle.Degrees(Rotate));
            e.style.scale = new Scale(new Vector2(ScaleX, ScaleY));
            e.style.opacity = Opacity;
        }
    }

    /// <summary>Plays CSS animations on an element, all together (later ones win per property, as in CSS).
    /// Autostart: from when the element is attached (a CSS animation that's always on). Otherwise it waits for
    /// Trigger(), as when the page adds a class (the menu's flourish). While ReferenceOverlay.FreezeAnimations is on,
    /// it's held at its first frame, as the reference screenshots hold it (inReference: whether it was running then).</summary>
    public sealed class CssAnimator
    {
        readonly VisualElement _e;
        readonly List<CssAnimation> _anims = new List<CssAnimation>();
        readonly bool _autostart, _inReference;
        CssPose _base = CssPose.Identity;
        float _start = -1f;
        IVisualElementScheduledItem _tick;

        public CssAnimator(VisualElement e, string transformOrigin = null, bool autostart = true, bool inReference = true)
        {
            _e = e;
            _autostart = autostart;
            _inReference = inReference;
            if (transformOrigin != null) e.style.transformOrigin = Origin(transformOrigin);
            e.RegisterCallback<AttachToPanelEvent>(_ => { if (_autostart) _start = Now; _tick ??= e.schedule.Execute(Update).Every(0); });
        }

        public CssAnimator(VisualElement e, Vector2 originPx, bool autostart = true, bool inReference = true)
            : this(e, null, autostart, inReference)
        {
            e.style.transformOrigin = new TransformOrigin(new Length(originPx.x), new Length(originPx.y));
        }

        public CssAnimator Base(CssPose p) { _base = p; _base.ApplyTo(_e); return this; }
        public CssAnimator Play(CssAnimation a) { _anims.Add(a); return this; }
        public void Trigger() { _start = Now; }

        /// <summary>A pose that wins over the animations while set, as a CSS :active rule with animation:none does.</summary>
        public CssPose? Hold { get; set; }
        public void Stop() { _anims.Clear(); _base.ApplyTo(_e); }

        static float Now => Time.realtimeSinceStartup;

        void Update()
        {
            if (Hold.HasValue) { Hold.Value.ApplyTo(_e); return; }
            float t;
            if (ReferenceOverlay.FreezeAnimations) { if (!_inReference) { _base.ApplyTo(_e); return; } t = 0f; }
            else if (_start < 0f) { _base.ApplyTo(_e); return; }
            else t = Now - _start;
            var p = _base;
            foreach (var a in _anims)
                if (a.Sample(t, p, out var q)) p = q;
            p.ApplyTo(_e);
        }

        /// <summary>CSS transform-origin: "50% 100%", "center", "12px 30px", "left top".</summary>
        public static TransformOrigin Origin(string css)
        {
            var parts = css.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Length L(string s)
            {
                switch (s)
                {
                    case "left": case "top": return Length.Percent(0);
                    case "center": return Length.Percent(50);
                    case "right": case "bottom": return Length.Percent(100);
                }
                if (s.EndsWith("%")) return Length.Percent(float.Parse(s.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture));
                return new Length(float.Parse(s.Replace("px", ""), System.Globalization.CultureInfo.InvariantCulture));
            }
            if (parts.Length == 1) return new TransformOrigin(L(parts[0]), Length.Percent(50));
            // keywords may come in either order ("top left")
            string h = parts[0], v = parts[1];
            if (h == "top" || h == "bottom" || v == "left" || v == "right") { var t = h; h = v; v = t; }
            return new TransformOrigin(L(h), L(v));
        }
    }
}
