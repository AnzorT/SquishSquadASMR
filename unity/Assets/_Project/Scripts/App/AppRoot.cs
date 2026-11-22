// The app's root, the Unity equivalent of App.js: one persistent object holding one UI Toolkit panel, with one screen
// shown at a time (ScreenRouter) plus layers above it for popups and toasts. It builds itself when Play starts, in
// any scene, so there is nothing to wire by hand in the Editor.
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App
{
    public sealed class AppRoot : MonoBehaviour
    {
        // The React Native app lays out in dp on a Galaxy A15 held landscape: 832 x 384 dp. The panel uses the same
        // units, scaled by the screen's height, so on the A15 (2340 x 1080 px) one UI unit is exactly one dp.
        public const int RefWidth = PlushCrush.Core.AppInfo.RefWidthDp;
        public const int RefHeight = PlushCrush.Core.AppInfo.RefHeightDp;

        public static AppRoot Instance { get; private set; }
        public ScreenRouter Router { get; private set; }
        public VisualElement PopupLayer { get; private set; }
        public VisualElement Root { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("PlushCrush App");
            DontDestroyOnLoad(go);
            go.AddComponent<AppRoot>();
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            EnsureCamera();

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "PlushCrush Panel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/PlushCrushTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(RefWidth, RefHeight);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f; // match height, like dp on a landscape phone
            panel.clearColor = false;

            // UIDocument attaches when enabled, so give it its panel while the object is still inactive.
            var uiGo = new GameObject("UI");
            uiGo.SetActive(false);
            uiGo.transform.SetParent(transform, false);
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            uiGo.SetActive(true);

            Root = doc.rootVisualElement;
            Root.style.flexGrow = 1;

            var screenLayer = FullLayer("screens");
            PopupLayer = FullLayer("popups");
            PopupLayer.pickingMode = PickingMode.Ignore;
            Root.Add(screenLayer);
            Root.Add(PopupLayer);
            ReferenceOverlay.Install(Root); // editor-only tool, drawn above everything (see ReferenceOverlay.cs)

            Router = new ScreenRouter(this, screenLayer);
            // The Main Menu once its pictures are exported (tools/unity-port/export-menu.mjs); the setup check before.
            // The launch's first menu plays the game intro (App.js: once a launch, start data `intro`).
            if (MainMenu.MenuLayout.Exists) Router.Show(new MainMenu.MainMenuScreen(new MainMenu.MenuState { Intro = true }));
            else Router.Show(new SetupCheckScreen());
        }

        void Update()
        {
            // Android's back button arrives as Escape (legacy input) when the Input System isn't the only backend.
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) Router.Back();
#endif
        }

        /// <summary>A short message at the top of the screen.</summary>
        public void Toast(string text, float seconds = 2f)
        {
            var t = new Label(text).Use(Fonts.Nunito800);
            t.pickingMode = PickingMode.Ignore;
            t.style.position = Position.Absolute;
            t.style.top = 10;
            t.style.left = Length.Percent(50);
            t.style.translate = new Translate(Length.Percent(-50), 0);
            t.style.backgroundColor = new Color(0.1f, 0.05f, 0.15f, 0.85f);
            t.style.color = Color.white;
            t.style.fontSize = 13;
            t.style.paddingLeft = 14; t.style.paddingRight = 14; t.style.paddingTop = 6; t.style.paddingBottom = 6;
            t.style.borderTopLeftRadius = 14; t.style.borderTopRightRadius = 14;
            t.style.borderBottomLeftRadius = 14; t.style.borderBottomRightRadius = 14;
            PopupLayer.Add(t);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn((long)(seconds * 1000));
        }

        static VisualElement FullLayer(string name)
        {
            var e = new VisualElement { name = name };
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0;
            return e;
        }

        static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x2A, 0x16, 0x08, 0xFF); // the app's splash color
            cam.orthographic = true;
            DontDestroyOnLoad(cam.gameObject);
        }
    }
}
