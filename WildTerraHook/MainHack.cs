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
        public static bool ShowMiscMenu = false;
    }

    public class Loader { public static void Init() { GameObject g = new GameObject("WT2_GlobalHook"); g.AddComponent<MainHack>(); UnityEngine.Object.DontDestroyOnLoad(g); } }

    public class MainHack : MonoBehaviour
    {
        private PlayerAnalyzer _analyzer = new PlayerAnalyzer();
        private ColorFishingModule _colorBot = new ColorFishingModule();
        private ResourceEspModule _esp = new ResourceEspModule();
        private MiscModule _misc = new MiscModule();

        // Domyślne rozmiary (można je teraz zmieniać w grze)
        private Rect _menuRect = new Rect(30, 30, 250, 350);
        private Rect _analyzerRect = new Rect(290, 30, 500, 600);
        private Rect _debugRect = new Rect(30, 400, 400, 200);
        private Rect _espRect = new Rect(550, 30, 350, 500);
        private Rect _miscRect = new Rect(910, 30, 300, 400);

        // Zmienne do obsługi resize
        private bool _isResizing = false;
        private int _resizingWindowId = -1;
        private Vector2 _resizeStartMouse;
        private Rect _resizeStartRect;

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
            _misc.Update();

            if (Input.GetKeyDown(KeyCode.End)) UnityEngine.Object.Destroy(this.gameObject);
        }

        void OnGUI()
        {
            // 1. MENU GŁÓWNE
            if (Settings.ShowMenu)
                _menuRect = GUI.Window(10, _menuRect, (id) => {
                    DrawMenu(id);
                    HandleResize(ref _menuRect, id);
                }, "<b>WILD TERRA 2 HOOK</b>");

            // 2. ANALIZATOR
            if (Settings.ShowAnalyzer)
                _analyzerRect = GUI.Window(11, _analyzerRect, (id) => {
                    _analyzer.DrawWindow(id);
                    HandleResize(ref _analyzerRect, id);
                }, "<b>ANALIZATOR</b>");

            // 3. STATUS BOTA (AutoFish)
            if (Settings.AutoFishColor)
                _debugRect = GUI.Window(12, _debugRect, (id) => {
                    _colorBot.DrawDebugWindow(id);
                    HandleResize(ref _debugRect, id);
                }, "<b>STATUS BOTA</b>");

            // 4. ESP SETTINGS
            if (Settings.ShowEspMenu)
                _espRect = GUI.Window(13, _espRect, (id) => {
                    _esp.DrawMenu();
                    // DragWindow tylko za belkę tytułową (20px), żeby nie kolidowało z elementami
                    GUI.DragWindow(new Rect(0, 0, 10000, 20));
                    HandleResize(ref _espRect, id);
                }, "<b>ESP SETTINGS</b>");

            // 5. INNE FUNKCJE
            if (Settings.ShowMiscMenu)
                _miscRect = GUI.Window(14, _miscRect, (id) => {
                    _misc.DrawMenu();
                    GUI.DragWindow(new Rect(0, 0, 10000, 20));
                    HandleResize(ref _miscRect, id);
                }, "<b>INNE FUNKCJE</b>");

            _esp.DrawESP();
        }

        // --- UNIWERSALNA FUNKCJA SKALOWANIA OKNA ---
        private void HandleResize(ref Rect windowRect, int windowID)
        {
            // Rysujemy uchwyt w prawym dolnym rogu (wizualnie)
            Rect handleRect = new Rect(windowRect.width - 20, windowRect.height - 20, 20, 20);
            GUI.Box(handleRect, "◢", GUIStyle.none);

            Event e = Event.current;

            // Rozpoczęcie skalowania
            if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
            {
                _isResizing = true;
                _resizingWindowId = windowID;
                _resizeStartMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
                _resizeStartRect = windowRect;
                e.Use();
            }

            // W trakcie skalowania (tylko dla aktywnego ID)
            if (_isResizing && _resizingWindowId == windowID)
            {
                if (e.type == EventType.MouseDrag)
                {
                    Vector2 currentMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    float deltaX = currentMouse.x - _resizeStartMouse.x;
                    float deltaY = currentMouse.y - _resizeStartMouse.y;

                    windowRect.width = Mathf.Max(150, _resizeStartRect.width + deltaX);
                    windowRect.height = Mathf.Max(100, _resizeStartRect.height + deltaY);
                }
                else if (e.type == EventType.MouseUp)
                {
                    _isResizing = false;
                    _resizingWindowId = -1;
                }
            }
        }

        void DrawMenu(int id)
        {
            if (GUILayout.Button("ANALIZATOR", GUILayout.Height(30))) Settings.ShowAnalyzer = !Settings.ShowAnalyzer;
            if (GUILayout.Button("ESP MENU", GUILayout.Height(30))) Settings.ShowEspMenu = !Settings.ShowEspMenu;
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

            // Drag całego okna za belkę
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
    }
}