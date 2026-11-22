// The game intro on the menu (webgame/app/menu.js startIntro, "the game intro"): played on the app's first menu of a
// launch when the start data asks (MenuState.Intro: src/game/GameScreen.js's `intro`, App.js asks once a launch), over
// the menu's own world, with what menu.js's api gives intro.js:
//   - the HUD and the name plate fade out while it plays (back at the end) and the menu is busy (no taps), its weather
//     hidden (the intro has its own: .sq-intro .sq-wx);
//   - Mittens is its star: the menu's own creature when he's the player's, else his own in the creature's spot for the
//     story (x.preview), the player's creature back at the end;
//   - light(): the night is a CSS filter on him (darker and cooler at first, warmer with every room lit, none by day);
//   - finish(): the menu's own scene under the intro's layers as they fade, its creature (who plays 'happy'), its HUD.
// IntroPhase tells the app "start", "awake" (the third squish of Mittens: App.js starts its music there) and "done"
// (App.js holds the daily spin, the streak and the level-up card until then). The phone's back skips it (OnBack).
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.MainMenu
{
    public sealed partial class MenuState
    {
        /// <summary>The start data's `intro`: play the game intro when the menu is up (the app's first menu of a launch).</summary>
        public bool Intro;
        /// <summary>Debug (PlushCrush > Intro): play it again (even after this launch's), the reference run, held at a
        /// moment.</summary>
        public MenuIntro.Options IntroOptions;
    }

    public sealed partial class MainMenuScreen
    {
        static bool _introDone;   // (SquishApp._introDone: once a launch)
        MenuIntro _intro;
        VisualElement _introHost;   // Mittens' own spot when he isn't the player's creature

        /// <summary>"start", "awake" (the app's music may start), "done" (the menu is the menu again).</summary>
        public event Action<string> IntroPhase;

        void MaybeIntro(VisualElement root)
        {
            if (!_state.Intro || (_introDone && _state.IntroOptions == null)) return;
            var L = IntroLayout.Get();
            if (L == null) { Debug.LogWarning("[PlushCrush] The intro's pictures are missing: run tools/unity-port/export-intro.mjs"); return; }
            if (_creatureEl == null) return;
            _introDone = true;
            _busy = true;
            FadeHud(false);
            if (_weather != null) _weather.style.display = DisplayStyle.None;
            if (_birds != null) _birds.style.display = DisplayStyle.None;
            _intro = new MenuIntro(root, _world, MittensHost(out var star), star, _state.SeasonNow(), L, _state.IntroOptions);
            _intro.Phase += p => { Debug.Log("[PlushCrush] intro: " + p); IntroPhase?.Invoke(p); };
            _intro.Finished += IntroFinished;
        }

        // Mittens for the story: the menu's own live creature when he's the player's, else his own in the same spot
        // (the default creature picture of layout.json is his)
        VisualElement MittensHost(out MenuIntro.Star star)
        {
            if (_state.CreatureKey == "mittens" && _creature is LiveCreature own)
            {
                star = new MenuIntro.Star { Host = _creatureEl, StageBox = () => StageBoxIn(_creatureSpec), Creature = own, Rig = () => own.Rig, Filter = own.Filter };
                return _creatureEl;
            }
            var spec = Array.Find(_layout.parts, p => p.name == "creature");
            float W = _layout.screen.width, H = _layout.screen.height;
            var host = _introHost = new VisualElement { name = "intro-mittens", pickingMode = PickingMode.Ignore };
            Place(host, spec.anchor, spec, W, H);
            if (spec.sceneAt != null && spec.sceneAt.Length == 2) FollowScene(_world, host, spec);
            var pic = new VisualElement { name = "intro-mittens-picture", pickingMode = PickingMode.Ignore };
            Fill(pic);
            pic.style.backgroundImage = new StyleBackground(MenuLayout.Picture(spec.image));
            pic.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            host.Add(pic);
            _world.Insert(_world.IndexOf(_creatureEl) + 1, host);
            _creatureEl.style.display = DisplayStyle.None;
            if (LiveCreature.Possible("mittens"))
            {
                var m = new LiveCreature(host, pic, "mittens", PlaceStageIn(spec));
                star = new MenuIntro.Star { Host = host, StageBox = () => StageBoxIn(spec), Creature = m, Rig = () => m.Rig, Filter = m.Filter };
            }
            else star = new MenuIntro.Star { Host = host, StageBox = () => StageBoxIn(spec), Creature = new StillCreature(pic, 169f), Rig = () => null, Filter = _ => { } };
            return host;
        }

        // api.finish(): the menu's scene is under the fading intro already; its creature, HUD and weather come back
        void IntroFinished()
        {
            if (_introHost != null) { _introHost.RemoveFromHierarchy(); _introHost = null; }
            _creatureEl.style.display = DisplayStyle.Flex;
            _busy = false;
            FadeHud(true);
            if (_weather != null) _weather.style.display = DisplayStyle.Flex;
            if (_birds != null) _birds.style.display = DisplayStyle.Flex;
            _creature.Play("happy");
        }

        /// <summary>The intro, while it plays (null otherwise).</summary>
        public MenuIntro Intro => _intro != null && _intro.Running ? _intro : null;
    }
}
