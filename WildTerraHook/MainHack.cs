using UnityEngine;
using System;

namespace WildTerraHook
{
    public static class Settings
    {
        public static bool ShowMenu = false;
        public static bool ShowAnalyzer = false;
        public static bool AutoFishColor = false;
        public static float FishingTimeout = 10f;
        public static KeyCode MenuKey = KeyCode.Delete;

        public static bool ShowEspMenu = false;
        public static bool ShowMiscMenu = false; // Nowe menu
    }

    public class Loader { public static void Init() { GameObject g = new GameObject("WT2_GlobalHook"); g.AddComponent<MainHack>(); UnityEngine.Object.DontDestroyOnLoad(g); } }

    public class MainHack : MonoBehaviour
    {
        private PlayerAnalyzer _analyzer = new PlayerAnalyzer();
        private ColorFishingModule _colorBot = new ColorFishingModule();
        private ResourceEspModule _esp = new ResourceEspModule();
        private MiscModule _misc = new MiscModule(); // Nowy moduł

        private Rect _menuRect = new Rect(30, 30, 240, 280); // Zwiększona wysokość
        private Rect _analyzerRect = new Rect(280, 30, 450, 600);
        private Rect _debugRect = new Rect(30, 320, 350, 200);
        private Rect _espRect = new Rect(500, 30, 320, 500);
        private Rect _miscRect = new Rect(830, 30, 250, 250); // Okno Misc

        public static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { var t = assembly.GetType(name); if (t != null) return t; }
            return null;
        }

        void Update()
        {
            if (Input.GetKeyDown(Settings.MenuKey)) { Settings.ShowMenu = !Settings.ShowMenu; Cursor.visible = Settings.ShowMenu; }
            if (Settings.ShowAnalyzer) _analyzer.Update();

            _colorBot.Update();
            _esp.Update();
            _misc.Update(); // Aktualizacja Misc (Zoom/Day)

            if (Input.GetKeyDown(KeyCode.End)) UnityEngine.Object.Destroy(this.gameObject);
        }

        void OnGUI()
        {
            if (Settings.ShowMenu) _menuRect = GUI.Window(10, _menuRect, DrawMenu, "<b>WILD TERRA 2 HOOK</b>");
            if (Settings.ShowAnalyzer) _analyzerRect = GUI.Window(11, _analyzerRect, _analyzer.DrawWindow, "<b>ANALIZATOR</b>");

            if (Settings.AutoFishColor)
                _debugRect = GUI.Window(12, _debugRect, _colorBot.DrawDebugWindow, "<b>STATUS BOTA</b>");

            if (Settings.ShowEspMenu)
                _espRect = GUI.Window(13, _espRect, (id) => { _esp.DrawMenu(); GUI.DragWindow(); }, "<b>ESP SETTINGS</b>");

            // Nowe okno Misc
            if (Settings.ShowMiscMenu)
                _miscRect = GUI.Window(14, _miscRect, (id) => { _misc.DrawMenu(); GUI.DragWindow(); }, "<b>INNE FUNKCJE</b>");

            _esp.DrawESP();
        }

        void DrawMenu(int id)
        {
            if (GUILayout.Button("ANALIZATOR", GUILayout.Height(30))) Settings.ShowAnalyzer = !Settings.ShowAnalyzer;

            if (GUILayout.Button("ESP MENU", GUILayout.Height(30))) Settings.ShowEspMenu = !Settings.ShowEspMenu;

            // Przycisk Misc
            if (GUILayout.Button("INNE FUNKCJE", GUILayout.Height(30))) Settings.ShowMiscMenu = !Settings.ShowMiscMenu;

            GUILayout.Space(10);

            GUI.backgroundColor = Settings.AutoFishColor ? Color.green : Color.red;
            if (GUILayout.Button("AUTO-FISH + WALKA", GUILayout.Height(45)))
            {
                Settings.AutoFishColor = !Settings.AutoFishColor;
                if (!Settings.AutoFishColor) _colorBot.OnDisable();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            if (GUILayout.Button("WYŁĄCZ (END)")) UnityEngine.Object.Destroy(this.gameObject);
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }
    }
}