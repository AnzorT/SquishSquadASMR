// menu.js's tween(ms, fn, ease): fn(eased, t) every frame for ms milliseconds (t linear 0..1), then done.
// And its easing curves, by their names there.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.Motion
{
    public static class Tween
    {
        public static readonly Func<float, float> EIO = t => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;   // quad in-out
        public static readonly Func<float, float> EIOC = t => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; // cubic in-out
        public static readonly Func<float, float> EOUT = t => 1f - (1f - t) * (1f - t);                                      // quad out

        /// <summary>Runs on owner's scheduler (stops if it leaves the screen). Returns a handle to stop it early.</summary>
        public static IVisualElementScheduledItem Run(VisualElement owner, float ms, Action<float, float> fn, Func<float, float> ease = null, Action done = null)
        {
            float t0 = -1f;
            IVisualElementScheduledItem item = null;
            item = owner.schedule.Execute(() =>
            {
                if (t0 < 0f) t0 = Time.realtimeSinceStartup;
                float t = ms <= 0f ? 1f : Mathf.Min(1f, (Time.realtimeSinceStartup - t0) * 1000f / ms);
                fn(ease != null ? ease(t) : t, t);
                if (t >= 1f) { item.Pause(); done?.Invoke(); }
            }).Every(0);
            return item;
        }

        /// <summary>A CSS transition on opacity: from its current value to `to` over ms with the given timing.</summary>
        public static IVisualElementScheduledItem Fade(VisualElement e, float to, float ms, Func<float, float> timing = null, Action done = null)
        {
            float from = e.resolvedStyle.opacity;
            return Run(e, ms, (k, _) => e.style.opacity = Mathf.LerpUnclamped(from, to, k), timing ?? Easing.Ease, done);
        }
    }
}
