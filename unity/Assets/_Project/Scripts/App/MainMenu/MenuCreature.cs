// What the menu asks of its creature (the rig in the page: RiveRig, menu.js walk()): play a move, turn side-on
// (spinOff, degrees: -90 faces left, 90 right), and walk in place while the menu moves it.
using System;
using PlushCrush.App.Motion;
using PlushCrush.App.Rig;
using UnityEngine.UIElements;

namespace PlushCrush.App.MainMenu
{
    public interface IMenuCreature
    {
        void Play(string move);
        void SpinOff(float degrees);
        void Walking(bool on, float facing);
    }

    /// <summary>Until the rig (rive-rig.js / rive-generic.js) is ported: the creature's still picture, as the menu shows
    /// it at rest. It doesn't turn or play moves. Walking, it does what the page itself does when its walking gait is
    /// missing (main-menu.js: .mm-stage.walk > div { animation: mmBob .32s ease-in-out infinite }).</summary>
    public sealed class StillCreature : IMenuCreature
    {
        readonly VisualElement _e;
        readonly CssAnimator _bob;
        bool _walking;

        public StillCreature(VisualElement picture, float stageHeight)
        {
            _e = picture;
            if (_e == null) return;
            // mmBob{0%,100%{translateY(0) rotate(-2deg)} 50%{translateY(-5%) rotate(2deg)}}; -5% of the stage's box
            var bob = new CssAnimation { Duration = 0.32f, Iterations = float.PositiveInfinity, Timing = Easing.EaseInOut };
            bob.Key(new CssKeyframe(0f).Move(0, 0).Rot(-2f));
            bob.Key(new CssKeyframe(0.5f).Move(0, -0.05f * stageHeight).Rot(2f));
            bob.Key(new CssKeyframe(1f).Move(0, 0).Rot(-2f));
            _bob = new CssAnimator(_e, "50% 50%", autostart: false, inReference: false).Play(bob);
        }

        public void Play(string move) { }
        public void SpinOff(float degrees) { }

        public void Walking(bool on, float facing)
        {
            if (_bob == null || on == _walking) return;
            _walking = on;
            if (on) _bob.Trigger(); else _bob.Hold = CssPose.Identity;
            if (on) _bob.Hold = null;
        }
    }

    /// <summary>The creature live: the game's rig (RigView), in the page's stage box. Its still picture shows until the
    /// rig's moves have loaded (a worker thread parses them). Walking, the box bobs as the page's .mm-stage.walk > div
    /// (mmBob), and the menu's turns go to the rig's spinOff, as main-menu.js sets I.spinOff.</summary>
    public sealed class LiveCreature : IMenuCreature
    {
        readonly VisualElement _host, _picture;
        readonly StillCreature _still;
        readonly string _key;
        readonly Action<VisualElement, Action<float>> _place;
        RigView _rig;
        CssAnimator _bob;
        float _spin, _ppu = 2.8125f;
        bool _walking;

        public static bool Possible(string key) => RigLibrary.Unsupported(key) == null;   // (no moves file: the still picture stays)

        public LiveCreature(VisualElement host, VisualElement picture, string key, Action<VisualElement, Action<float>> placeBox)
        {
            _host = host; _picture = picture; _key = key; _place = placeBox;
            _still = new StillCreature(picture, 169f);
            RigLibrary.Preload();
            host.schedule.Execute(TrySwap).Every(50).Until(() => _rig != null);
        }

        void TrySwap()
        {
            if (_rig != null || !RigLibrary.Ready) return;
            var rig = new RigView(_key);
            _place(rig, ppu => { _ppu = ppu; rig.PixelsPerUnit = ppu; });
            rig.PixelsPerUnit = _ppu;
            _host.Add(rig);
            if (_picture != null) _picture.style.display = DisplayStyle.None;
            rig.Engine.SpinOff = _spin;
            rig.ColorFilter = _filter;
            if (_lastMove != null) rig.Play(_lastMove);
            _rig = rig;
            var bob = new CssAnimation { Duration = 0.32f, Iterations = float.PositiveInfinity, Timing = Easing.EaseInOut };
            bob.Key(new CssKeyframe(0f).Move(0, 0).Rot(-2f));
            bob.Key(new CssKeyframe(0.5f).Move(0, -0.05f * 168.96f).Rot(2f));   // -5% of the box (0.44 of the 384-unit-tall UI)
            bob.Key(new CssKeyframe(1f).Move(0, 0).Rot(-2f));
            _bob = new CssAnimator(rig, "50% 50%", autostart: false, inReference: false).Play(bob);
            if (_walking) { _bob.Trigger(); _bob.Hold = null; }
        }

        public void Play(string move) { _lastMove = move; if (_rig != null) _rig.Play(move); }
        string _lastMove;

        /// <summary>The live rig (null until its moves have loaded).</summary>
        public RigView Rig => _rig;

        /// <summary>A CSS filter on the creature (menu.js light(): the intro's night) as a colour matrix; null: none.
        /// The still picture (before the rig is there) only gets its brightness, as a tint.</summary>
        public void Filter(UnityEngine.Matrix4x4? m)
        {
            _filter = m;
            if (_rig != null) _rig.ColorFilter = m;
            if (_picture != null)
            {
                float b = m.HasValue ? (m.Value.m00 + m.Value.m01 + m.Value.m02 + m.Value.m10 + m.Value.m11 + m.Value.m12 + m.Value.m20 + m.Value.m21 + m.Value.m22) / 3f : 1f;
                _picture.style.unityBackgroundImageTintColor = m.HasValue ? new UnityEngine.Color(b, b, b, 1f) : UnityEngine.Color.white;
            }
        }
        UnityEngine.Matrix4x4? _filter;

        public void SpinOff(float degrees) { _spin = degrees; if (_rig != null) _rig.Engine.SpinOff = degrees; }

        public void Walking(bool on, float facing)
        {
            _still.Walking(on, facing);
            if (on == _walking) return;
            _walking = on;
            if (_bob == null) return;
            if (on) { _bob.Trigger(); _bob.Hold = null; } else _bob.Hold = CssPose.Identity;
        }
    }
}
