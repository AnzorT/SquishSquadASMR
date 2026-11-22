// Touching a live creature, as rive-rig.js's pointer handlers on the rig's box (its `wrap`): the finger's place is
// where the creature looks (I.ptr), a hold of 170 ms squeezes (I.press), a drag of more than 10 dp sideways spins it
// round (I.spinOff, 360° per box width), letting go of a squeeze releases it (the release shot) and of a spin settles
// it to the nearest turn (650 ms). A quick tap does nothing (the game's poke branch is commented out). The handlers'
// listeners (Down, Up, Cancel) run after the rig's own, as listeners added after the rig's do in the page.
//
// And CssFilter: the CSS filter functions a page puts on a creature (brightness, saturate, sepia), as the one colour
// matrix they make (Chrome runs them in sRGB, clamping after each: within these ranges only the end needs it).
using System;
using PlushCrush.Core.Rig;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.Rig
{
    public sealed class RigTouch
    {
        readonly VisualElement _hit, _box;
        readonly Func<RigView> _rig;
        IVisualElementScheduledItem _hold;
        int _pointer = -1;
        float _downX;

        /// <summary>Raised after the rig's own handling (the page's later listeners).</summary>
        public event Action Down, Up, Cancel;

        /// <summary>hit: the element taking the touches; box: the rig's 100 x 100 box (where the finger is measured
        /// from); rig: the live rig (null while it isn't there: the touches still reach the listeners).</summary>
        public RigTouch(VisualElement hit, VisualElement box, Func<RigView> rig)
        {
            _hit = hit; _box = box; _rig = rig;
            hit.RegisterCallback<PointerDownEvent>(OnDown);
            hit.RegisterCallback<PointerMoveEvent>(OnMove);
            hit.RegisterCallback<PointerUpEvent>(OnUp);
            hit.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == _pointer) { Release(); Cancel?.Invoke(); } });
            hit.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == _pointer) { Release(); Cancel?.Invoke(); } });
            hit.RegisterCallback<PointerLeaveEvent>(_ => { var r = _rig(); if (r != null && _pointer < 0) r.Engine.Ptr = null; });
        }

        RigEngine Engine => _rig()?.Engine;

        Vector2 Local(Vector2 panelPos)
        {
            var b = _box.worldBound;
            return new Vector2((panelPos.x - b.x) / Mathf.Max(1e-3f, b.width) * 100f, (panelPos.y - b.y) / Mathf.Max(1e-3f, b.height) * 100f);
        }

        void OnDown(PointerDownEvent e)
        {
            if (_pointer >= 0) return;
            _pointer = e.pointerId; _downX = e.position.x;
            _hit.CapturePointer(e.pointerId);
            var E = Engine;
            if (E != null)
            {
                E.PointerDown(RigView.Clock);
                _hold?.Pause();
                _hold = _hit.schedule.Execute(() => { var E2 = Engine; if (E2 != null && E2.Down && !E2.Drag) E2.HoldStarted(RigView.Clock); }).StartingIn(170);
            }
            Down?.Invoke();
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            var E = Engine;
            if (E == null) return;
            var p = Local(e.position);
            E.Ptr = new double[] { Mathf.Clamp((p.x - 50f) / 42f, -1f, 1f), Mathf.Clamp((p.y - 46f) / 42f, -1f, 1f) };
            if (_pointer != e.pointerId || !E.Down) return;
            float dx = e.position.x - _downX, w = Mathf.Max(1e-3f, _box.worldBound.width);
            if (!E.Drag && !E.Press && Mathf.Abs(dx) > 10f) { _hold?.Pause(); E.DragTo(dx / w); }
            else if (E.Drag) E.DragTo(dx / w);
        }

        void OnUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointer) return;
            Release();
            Up?.Invoke();
            e.StopPropagation();
        }

        void Release()
        {
            _hold?.Pause();
            int p = _pointer; _pointer = -1;
            if (p >= 0 && _hit.HasPointerCapture(p)) _hit.ReleasePointer(p);
            Engine?.PointerUp(RigView.Clock);
        }
    }

    /// <summary>CSS filter functions as a colour matrix (rows r, g, b), as Chrome applies them: brightness, then
    /// saturate, then sepia (the order menu.js writes them).</summary>
    public static class CssFilter
    {
        public static Matrix4x4 Matrix(float brightness, float saturate, float sepia)
        {
            var B = Matrix4x4.Scale(new Vector3(brightness, brightness, brightness));
            float s = saturate;
            var S = Rows(0.213f + 0.787f * s, 0.715f - 0.715f * s, 0.072f - 0.072f * s,
                         0.213f - 0.213f * s, 0.715f + 0.285f * s, 0.072f - 0.072f * s,
                         0.213f - 0.213f * s, 0.715f - 0.715f * s, 0.072f + 0.928f * s);
            float a = 1f - sepia;
            var P = Rows(0.393f + 0.607f * a, 0.769f - 0.769f * a, 0.189f - 0.189f * a,
                         0.349f - 0.349f * a, 0.686f + 0.314f * a, 0.168f - 0.168f * a,
                         0.272f - 0.272f * a, 0.534f - 0.534f * a, 0.131f + 0.869f * a);
            return P * S * B;
        }

        static Matrix4x4 Rows(float a, float b, float c, float d, float e, float f, float g, float h, float i)
        {
            var m = Matrix4x4.identity;
            m.SetRow(0, new Vector4(a, b, c, 0)); m.SetRow(1, new Vector4(d, e, f, 0)); m.SetRow(2, new Vector4(g, h, i, 0));
            return m;
        }
    }
}
