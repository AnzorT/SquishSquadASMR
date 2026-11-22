// PlushCrush > Game Park, in Play mode: the Game Park overlay on its own, to check it before the menu shows it after
// the walk to the park. The same stand-in games and creature the exporter and the reference screenshots use
// (tools/unity-port/export-game-park.mjs GAMES, Mittens).
//   Show over the current screen    over whatever screen is up
//   Show alone (...)                over flat grey, like Reference/game/park-overlay-alone*.jpg (Reference Overlay,
//                                   with Freeze animations on: transitions shown finished, the creature at rest)
using System.Collections.Generic;
using PlushCrush.App;
using PlushCrush.App.MainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.EditorTools
{
    static class GameParkDebugMenu
    {
        const string HostName = "game-park-debug";

        static List<GameParkGame> Games(bool locked) => new List<GameParkGame>
        {
            GameParkGame.SquishTime(),
            new GameParkGame("squishdoku", "Squishdoku", "Level 37 / 600", !locked),
            new GameParkGame("sort", "Squish Sort", "Level 4 / 50", !locked),
            new GameParkGame("stack", "Squish Stack", "Level 9 / 50 · ★ 12", !locked),
            new GameParkGame("hop", "Squish Hop", "Level 3 / 100", !locked),
        };

        [MenuItem("PlushCrush/Game Park/Show over the current screen")]
        static void OverScreen() => Show(false, false, -1);

        [MenuItem("PlushCrush/Game Park/Show alone (Squish Time)")]
        static void Alone() => Show(true, false, 0);

        [MenuItem("PlushCrush/Game Park/Show alone (Squish Stack)")]
        static void AloneStack() => Show(true, false, 3);

        [MenuItem("PlushCrush/Game Park/Show alone (locked games)")]
        static void AloneLocked() => Show(true, true, 1);

        [MenuItem("PlushCrush/Game Park/Close")]
        static void Close() => Remove();

        [MenuItem("PlushCrush/Game Park/Show over the current screen", true)]
        [MenuItem("PlushCrush/Game Park/Show alone (Squish Time)", true)]
        [MenuItem("PlushCrush/Game Park/Show alone (Squish Stack)", true)]
        [MenuItem("PlushCrush/Game Park/Show alone (locked games)", true)]
        [MenuItem("PlushCrush/Game Park/Close", true)]
        static bool Playing() => EditorApplication.isPlaying && AppRoot.Instance != null;

        static void Show(bool alone, bool locked, int start)
        {
            Remove();
            var app = AppRoot.Instance;
            if (!GameParkLayout.Exists) { app.Toast("Game Park pictures missing: run tools/unity-port/export-game-park.mjs"); return; }
            // over the current screen (under the popups), or on its own over grey
            var screen = app.Router.Current != null ? app.Router.Current.View : null;
            var parent = alone || screen == null ? app.Root : screen;
            var host = new VisualElement { name = HostName, pickingMode = alone ? PickingMode.Position : PickingMode.Ignore };
            host.style.position = Position.Absolute;
            host.style.left = 0; host.style.top = 0; host.style.right = 0; host.style.bottom = 0;
            if (alone) host.style.backgroundColor = new Color(127f / 255f, 127f / 255f, 127f / 255f, 1f);   // #7f7f7f
            var overlay = new GameParkOverlay(Games(locked), "mittens", "Mittens", false, start);
            overlay.Back += () =>
            {
                Debug.Log("[Game Park] back: the menu walks back to the house");
                host.schedule.Execute(() => host.RemoveFromHierarchy()).StartingIn(440);   // (the overlay goes at 420 ms)
            };
            overlay.Play += id => { Debug.Log("[Game Park] play " + id); app.Toast("PLAY: " + id); };
            overlay.CreatureMove += move => Debug.Log("[Game Park] the creature: " + move);
            overlay.Sound += s => Debug.Log("[Game Park] sound: " + s);
            host.Add(overlay);
            if (alone) parent.Insert(Mathf.Max(0, parent.IndexOf(app.PopupLayer)), host);
            else parent.Add(host);
        }

        static void Remove()
        {
            var app = AppRoot.Instance;
            if (app == null) return;
            var old = app.Root.Q<VisualElement>(HostName);
            while (old != null) { old.RemoveFromHierarchy(); old = app.Root.Q<VisualElement>(HostName); }
        }
    }
}
