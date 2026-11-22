// PlushCrush menu items for Play mode: jump between the ported screens.
using PlushCrush.App;
using UnityEditor;

namespace PlushCrush.EditorTools
{
    static class DebugMenu
    {
        [MenuItem("PlushCrush/Screens/Main Menu")]
        static void MainMenu() => AppRoot.Instance.Router.Show(new PlushCrush.App.MainMenu.MainMenuScreen());

        [MenuItem("PlushCrush/Screens/Setup Check")]
        static void SetupCheck() => AppRoot.Instance.Router.Show(new SetupCheckScreen(), remember: true);

        [MenuItem("PlushCrush/Screens/Main Menu", true)]
        [MenuItem("PlushCrush/Screens/Setup Check", true)]
        static bool Playing() => EditorApplication.isPlaying && AppRoot.Instance != null;
    }
}
