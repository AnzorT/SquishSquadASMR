// PlushCrush > Screens > Level Pass: the pass with the stand-in player its pictures and the reference screenshots
// show (layout.json standIn), to check it against Reference/game/level-pass*.jpg with the Reference Overlay.
using PlushCrush.App;
using PlushCrush.App.LevelPass;
using UnityEditor;

namespace PlushCrush.EditorTools
{
    static class LevelPassDebugMenu
    {
        [MenuItem("PlushCrush/Screens/Level Pass (as level-pass.jpg)")]
        static void Open() => Show(0);

        [MenuItem("PlushCrush/Screens/Level Pass, level 8 picked (as level-pass-L8.jpg)")]
        static void OpenLevel8() => Show(8);

        static void Show(int select)
        {
            var state = LevelPassLayout.StandIn();
            state.select = select;
            AppRoot.Instance.Router.Show(new LevelPassScreen(state), remember: true);
        }

        [MenuItem("PlushCrush/Screens/Level Pass (as level-pass.jpg)", true)]
        [MenuItem("PlushCrush/Screens/Level Pass, level 8 picked (as level-pass-L8.jpg)", true)]
        static bool Playing() => EditorApplication.isPlaying && AppRoot.Instance != null;
    }
}
