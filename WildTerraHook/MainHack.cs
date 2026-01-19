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
    }

    public class Loader { public static void Init() { GameObject g = new GameObject("WT2_GlobalHook"); g.AddComponent<MainHack>(); UnityEngine.Object.DontDestroyOnLoad(g); } }

    public class MainHack : MonoBehaviour
    {
        private PlayerAnalyzer _analyzer = new PlayerAnalyzer();
        private ColorFishingModule _colorBot = new ColorFishingModule();
        private ResourceEspModule _esp = new ResourceEspModule();

        private Rect _menuRect = new Rect(30, 30, 240, 240);
        private Rect _analyzerRect = new Rect(280, 30, 450, 600);
        private Rect _debugRect = new Rect(30, 280, 350, 200);
        private Rect _espRect = new Rect(500, 30, 320, 500);

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

            _esp.DrawESP();
        }

        void DrawMenu(int id)
        {
            if (GUILayout.Button("ANALIZATOR", GUILayout.Height(30))) Settings.ShowAnalyzer = !Settings.ShowAnalyzer;

            if (GUILayout.Button("ESP MENU", GUILayout.Height(30))) Settings.ShowEspMenu = !Settings.ShowEspMenu;

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