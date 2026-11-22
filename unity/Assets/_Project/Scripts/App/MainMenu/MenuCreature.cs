// What the menu asks of its creature (the rig in the page: RiveRig, menu.js walk()): play a move, turn side-on
// (spinOff, degrees: -90 faces left, 90 right), and walk in place while the menu moves it.
using PlushCrush.App.Motion;
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
}
