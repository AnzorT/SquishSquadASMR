// CSS transitions, replayed in Unity with the browser's own rules (CSS Transitions Level 1, "Starting of transitions"),
// on top of CssAnimation.cs's timing functions:
//   - a property set to a new value moves there from where it is now: after transition-delay, over
//     transition-duration, along transition-timing-function;
//   - set again while it moves, it starts over from where it is;
//   - set back to where the running one started (a class added, then taken off), the way back is shortened as browsers
//     shorten it (the "reversing shortening factor"): it takes as long as the way there had taken so far;
//   - changing the transition's settings (Duration, Delay, Timing) leaves a running transition as it is: they're for
//     the next one, as in CSS.
// A value is a few floats (a transform's x, y, scale x, scale y, rotation; an opacity; a width...), all moving together
// along the same curve, compared exactly.
using System;
using UnityEngine;

namespace PlushCrush.App.Motion
{
    public sealed class CssTransition
    {
        /// <summary>transition-duration and transition-delay (seconds), transition-timing-function.</summary>
        public float Duration, Delay;
        public Func<float, float> Timing;

        float[] _from, _to, _reverseStart;
        float _start, _end, _shortening = 1f;
        Func<float, float> _ease;   // the running transition's own timing function
        bool _running;

        public CssTransition(float[] value, float duration = 0f, float delay = 0f, string timing = "ease")
        {
            _to = Copy(value);
            _from = Copy(value);
            _reverseStart = Copy(value);
            Duration = duration;
            Delay = delay;
            Timing = Easing.Parse(timing);
            _ease = Timing;
        }

        public CssTransition(float value, float duration = 0f, float delay = 0f, string timing = "ease")
            : this(new[] { value }, duration, delay, timing) { }

        /// <summary>The value it's going to (its after-change style).</summary>
        public float[] Target => _to;

        public bool Running(float now) => _running && now < _end;

        /// <summary>The value at a time (seconds, the same clock Set was given). Time far ahead: the target.</summary>
        public float[] Value(float now)
        {
            if (!_running || now >= _end) return _to;
            if (now <= _start) return _from;
            float e = _ease((now - _start) / (_end - _start));
            var v = new float[_to.Length];
            for (int i = 0; i < v.Length; i++) v[i] = Mathf.LerpUnclamped(_from[i], _to[i], e);
            return v;
        }

        public float Value1(float now) => Value(now)[0];

        /// <summary>The property's value changes (a class added or taken off, an inline style set) at time now.</summary>
        public void Set(float[] target, float now)
        {
            if (Same(target, _to)) return;                     // the end value doesn't change: nothing to do
            var current = Value(now);
            float combined = Mathf.Max(Duration, 0f) + Delay;
            if (Running(now))
            {
                // already there, or no transition any more: the running one is cancelled, the value is the new one
                if (Same(current, target) || combined <= 0f) { Jump(target); return; }
                if (Same(_reverseStart, target))
                {
                    // back to where it came from: shortened by how far along it had got
                    float p = _end > _start ? Mathf.Clamp01((now - _start) / (_end - _start)) : 1f;
                    float shortening = Mathf.Clamp01(Mathf.Abs(_ease(p) * _shortening + 1f - _shortening));
                    float start = now + (Delay >= 0f ? Delay : Delay * shortening);
                    Begin(current, target, start, start + Duration * shortening, _to, shortening);
                    return;
                }
                Begin(current, target, now + Delay, now + Delay + Duration, current, 1f);
                return;
            }
            if (combined <= 0f) { Jump(target); return; }
            Begin(current, target, now + Delay, now + Delay + Duration, current, 1f);
        }

        public void Set(float target, float now) => Set(new[] { target }, now);

        /// <summary>No transition: the value is this at once (as when the page sets it before it was ever drawn).</summary>
        public void Jump(float[] value)
        {
            _running = false;
            _to = Copy(value);
            _from = Copy(value);
            _reverseStart = Copy(value);
            _shortening = 1f;
        }

        public void Jump(float value) => Jump(new[] { value });

        void Begin(float[] from, float[] to, float start, float end, float[] reverseStart, float shortening)
        {
            _from = Copy(from);
            _to = Copy(to);
            _reverseStart = Copy(reverseStart);
            _start = start;
            _end = end;
            _shortening = shortening;
            _ease = Timing ?? Easing.Ease;
            _running = true;
        }

        static float[] Copy(float[] v)
        {
            var c = new float[v.Length];
            Array.Copy(v, c, v.Length);
            return c;
        }

        static bool Same(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i] - b[i]) > 1e-5f) return false;
            return true;
        }
    }
}
