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

        private Rect _menuRect = new Rect(30, 30, 240, 180);
        private Rect _analyzerRect = new Rect(280, 30, 450, 600);
        private Rect _debugRect = new Rect(30, 220, 350, 200);

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
            if (Input.GetKeyDown(KeyCode.End)) UnityEngine.Object.Destroy(this.gameObject);
            _esp.Update();
        }

        void OnGUI()
        {
            if (Settings.ShowMenu) _menuRect = GUI.Window(10, _menuRect, DrawMenu, "<b>WILD TERRA 2 HOOK</b>");
            if (Settings.ShowAnalyzer) _analyzerRect = GUI.Window(11, _analyzerRect, _analyzer.DrawWindow, "<b>ANALIZATOR</b>");

            if (Settings.AutoFishColor)
                _debugRect = GUI.Window(12, _debugRect, _colorBot.DrawDebugWindow, "<b>STATUS BOTA</b>");
            _esp.DrawESP();
        }

        void DrawMenu(int id)
        {
            if (GUILayout.Button("OPEN ESP MENU")) Settings.ShowEspMenu = !Settings.ShowEspMenu;
            if (GUILayout.Button("ANALIZATOR", GUILayout.Height(30))) Settings.ShowAnalyzer = !Settings.ShowAnalyzer;
            GUILayout.Space(10);

            GUI.backgroundColor = Settings.AutoFishColor ? Color.green : Color.red;
            if (GUILayout.Button("AUTO-FISH + WALKA", GUILayout.Height(45)))
            {
                Settings.AutoFishColor = !Settings.AutoFishColor;
                // FAIL-SAFE: Reset przy wyłączeniu
                if (!Settings.AutoFishColor) _colorBot.OnDisable();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            if (GUILayout.Button("WYŁĄCZ (END)")) UnityEngine.Object.Destroy(this.gameObject);
            GUI.DragWindow(new Rect(0, 0, 10000, 25));

        }
    }
}