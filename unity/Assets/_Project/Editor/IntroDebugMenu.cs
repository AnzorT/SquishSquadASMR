// PlushCrush > Intro, in Play mode: the game intro on the Main Menu (MenuIntro.cs), again and again.
//   Play                         as at a launch, today's weather (or the one picked)
//   Play the reference run       intro.json's script: the box tapped, Mittens squished three times and TAP TO START
//                                at the moments the reference frames were taken (export-intro.mjs)
//   Reference moments...         a window listing Reference/game/intro-*.jpg: each opens the menu with the reference
//                                run held at that frame's moment, to check it with PlushCrush > Reference Overlay
//                                (Mittens, the rain and the sparkles still move: the frames are for the house, the box,
//                                the captions, the logo)
//   Skip                         as the Skip button (or the phone's back)
using PlushCrush.App;
using PlushCrush.App.MainMenu;
using UnityEditor;
using UnityEngine;

namespace PlushCrush.EditorTools
{
    static class IntroDebugMenu
    {
        [MenuItem("PlushCrush/Intro/Play")] static void Play() => Show(null, new MenuIntro.Options());
        [MenuItem("PlushCrush/Intro/Play (rainy)")] static void PlayRainy() => Show("rainy", new MenuIntro.Options());
        [MenuItem("PlushCrush/Intro/Play (snowy)")] static void PlaySnowy() => Show("snowy", new MenuIntro.Options());
        [MenuItem("PlushCrush/Intro/Play (sunny)")] static void PlaySunny() => Show("sunny", new MenuIntro.Options());
        [MenuItem("PlushCrush/Intro/Play the reference run")] static void PlayScripted() => Show("rainy", new MenuIntro.Options { Scripted = true });
        [MenuItem("PlushCrush/Intro/Reference moments...")] static void Moments() => EditorWindow.GetWindow<IntroMomentsWindow>("Intro moments");
        [MenuItem("PlushCrush/Intro/Skip")]
        static void Skip() { if (AppRoot.Instance.Router.Current is MainMenuScreen m && m.Intro != null) m.Intro.Skip(); }

        [MenuItem("PlushCrush/Intro/Play", true)]
        [MenuItem("PlushCrush/Intro/Play (rainy)", true)]
        [MenuItem("PlushCrush/Intro/Play (snowy)", true)]
        [MenuItem("PlushCrush/Intro/Play (sunny)", true)]
        [MenuItem("PlushCrush/Intro/Play the reference run", true)]
        [MenuItem("PlushCrush/Intro/Skip", true)]
        static bool Playing() => EditorApplication.isPlaying && AppRoot.Instance != null;

        public static void Show(string season, MenuIntro.Options o)
        {
            if (!IntroLayout.Exists) { AppRoot.Instance.Toast("Intro pictures missing: run tools/unity-port/export-intro.mjs"); return; }
            AppRoot.Instance.Router.Show(new MainMenuScreen(new MenuState { Intro = true, IntroOptions = o, Season = season }));
        }
    }

    sealed class IntroMomentsWindow : EditorWindow
    {
        Vector2 _scroll;

        void OnGUI()
        {
            var L = IntroLayout.Get();
            if (L == null) { EditorGUILayout.HelpBox("No intro.json: run tools/unity-port/export-intro.mjs", MessageType.Warning); return; }
            if (!EditorApplication.isPlaying) EditorGUILayout.HelpBox("Press Play, then pick a moment. Lay its picture over it with PlushCrush > Reference Overlay.", MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var r in L.References)
            {
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                    if (GUILayout.Button(r.Name + "  (" + (r.T / 1000.0).ToString("0.00") + " s)"))
                        IntroDebugMenu.Show(r.Season, new MenuIntro.Options { Scripted = true, HoldAt = r.T, SkipAt = double.IsNaN(r.SkipAt) ? (double?)null : r.SkipAt });
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
