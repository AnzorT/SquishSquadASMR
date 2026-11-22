// PlushCrush menu items for Play mode: jump between the ported screens.
using PlushCrush.App;
using UnityEditor;

namespace PlushCrush.EditorTools
{
    static class DebugMenu
    {
        [MenuItem("PlushCrush/Screens/Main Menu")]
        static void MainMenu() => AppRoot.Instance.Router.Show(new PlushCrush.App.MainMenu.MainMenuScreen());

        // the states the reference screenshots show (tools/unity-port/capture-references.mjs STATES), to compare with
        // game/menu-L10-snowy-bao.jpg and game/menu-L27-sunny-tutti.jpg in the Reference Overlay
        [MenuItem("PlushCrush/Screens/Main Menu: level 10, snowy, Bao")]
        static void MenuL10() => AppRoot.Instance.Router.Show(new PlushCrush.App.MainMenu.MainMenuScreen(new PlushCrush.App.MainMenu.MenuState
        {
            Level = 10, Season = "snowy", CreatureKey = "bao", Coins = 2450, Gems = 75, XpInto = 40, XpNeed = 110, Claimable = 2,
        }));

        [MenuItem("PlushCrush/Screens/Main Menu: level 27, sunny, Tutti")]
        static void MenuL27() => AppRoot.Instance.Router.Show(new PlushCrush.App.MainMenu.MainMenuScreen(new PlushCrush.App.MainMenu.MenuState
        {
            Level = 27, Season = "sunny", CreatureKey = "tutti", Coins = 125000, Gems = 3, XpInto = 230, XpNeed = 280, Claimable = 0,
        }));

        [MenuItem("PlushCrush/Screens/Setup Check")]
        static void SetupCheck() => AppRoot.Instance.Router.Show(new SetupCheckScreen(), remember: true);

        [MenuItem("PlushCrush/Screens/Main Menu", true)]
        [MenuItem("PlushCrush/Screens/Main Menu: level 10, snowy, Bao", true)]
        [MenuItem("PlushCrush/Screens/Main Menu: level 27, sunny, Tutti", true)]
        [MenuItem("PlushCrush/Screens/Setup Check", true)]
        static bool Playing() => EditorApplication.isPlaying && AppRoot.Instance != null;
    }
}
