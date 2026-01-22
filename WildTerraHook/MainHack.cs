using UnityEngine;

namespace WildTerraHook
{
    public class MainHack : MonoBehaviour
    {
        private ResourceEspModule _espModule;
        private AutoLootModule _lootModule;
        private AutoDropModule _dropModule;
        private MiscModule _miscModule;
        private ColorFishingModule _colorFishModule;
        private FishBotModule _memFishModule;
        private DebugConsoleModule _consoleModule;

        private bool _showMenu = true;
        private Rect _windowRect;
        private bool _isInitialized = false;

        private string[] _tabNames = { "ESP", "Fishing", "Auto Loot", "Auto Drop", "Misc", "CONSOLE" };
        private int _currentTab = 0;

        public void Start()
        {
            Localization.Init();
            ConfigManager.Load();

            _windowRect = new Rect(ConfigManager.Menu_X, ConfigManager.Menu_Y, ConfigManager.Menu_W, ConfigManager.Menu_H);

            // Domyślne wymiary, jeśli to pierwsze uruchomienie (lub błędny config)
            if (_windowRect.width < 450) _windowRect.width = 500;
            if (_windowRect.height < 300) _windowRect.height = 400; // Wyższe startowe okno dla konsoli

            _currentTab = ConfigManager.Menu_Tab;

            _espModule = new ResourceEspModule();
            _lootModule = new AutoLootModule();
            _dropModule = new AutoDropModule();
            _miscModule = new MiscModule();
            _colorFishModule = new ColorFishingModule();
            _memFishModule = new FishBotModule();

            _consoleModule = new DebugConsoleModule();
            Debug.Log("[MainHack] Hook załadowany pomyślnie.");

            _isInitialized = true;
        }

        public void OnDestroy()
        {
            if (_consoleModule != null) _consoleModule.Shutdown();
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Insert)) _showMenu = !_showMenu;
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                SaveWindowConfig();
                ConfigManager.Save();
                Destroy(this.gameObject);
                return;
            }

            _espModule.Update();
            _lootModule.Update();
            _dropModule.Update();
            _miscModule.Update();
            _colorFishModule.Update();
        }

        public void OnGUI()
        {
            if (!_isInitialized) return;

            _espModule.DrawESP();
            _colorFishModule.DrawESP();

            if (_showMenu)
            {
                Matrix4x4 oldMatrix = GUI.matrix;
                float scale = ConfigManager.Menu_Scale;
                if (scale < 0.5f) scale = 0.5f;

                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

                // Aktualizujemy globalne wymiary dla modułów (do skalowania list)
                ConfigManager.Menu_W = _windowRect.width;
                ConfigManager.Menu_H = _windowRect.height;

                _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, Localization.Get("MENU_TITLE"), GUILayout.MinWidth(450), GUILayout.MinHeight(300));

                GUI.matrix = oldMatrix;
            }
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(Localization.Get("MENU_TOGGLE_INFO"), CenteredLabel());
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            int newTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30), GUILayout.Width(_windowRect.width * 0.95f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (newTab != _currentTab)
            {
                _currentTab = newTab;
                ConfigManager.Menu_Tab = _currentTab;
                // USUNIĘTO: _windowRect.height = 0; -> Zapobiega to zmianie rozmiaru okna przy przełączaniu zakładek
            }

            GUILayout.Space(10);

            // Rysowanie zawartości
            // Używamy ScrollView dla całego kontentu, jeśli wyjdzie poza okno, 
            // ale moduły mają własne scrolle, więc tutaj po prostu expand.

            switch (_currentTab)
            {
                case 0: _espModule.DrawMenu(); break;
                case 1: DrawFishingTab(); break;
                case 2: _lootModule.DrawMenu(); break;
                case 3: _dropModule.DrawMenu(); break;
                case 4: DrawMiscTab(); break;
                case 5: _consoleModule.DrawMenu(); break;
            }

            GUILayout.Space(10);
            GUILayout.EndVertical();

            DrawResizer();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
            if (GUI.changed || Input.GetMouseButtonUp(0)) SaveWindowConfig();
        }

        private void DrawFishingTab()
        {
            GUILayout.BeginVertical("box");
            bool colorEn = GUILayout.Toggle(ConfigManager.ColorFish_Enabled, " <b>Color Bot</b> (Standard)");
            if (colorEn != ConfigManager.ColorFish_Enabled)
            {
                ConfigManager.ColorFish_Enabled = colorEn;
                ConfigManager.Save();
            }

            if (ConfigManager.ColorFish_Enabled)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                _colorFishModule.DrawMenu();
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();
        }

        private void DrawMiscTab()
        {
            _miscModule.DrawMenu();
            GUILayout.Space(10);
            GUILayout.Label("<b>UI SETTINGS</b>", GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Scale: {ConfigManager.Menu_Scale:F1}x", GUILayout.Width(80));
            float newScale = GUILayout.HorizontalSlider(ConfigManager.Menu_Scale, 0.8f, 2.0f);
            if (Mathf.Abs(newScale - ConfigManager.Menu_Scale) > 0.05f) ConfigManager.Menu_Scale = newScale;
            GUILayout.EndHorizontal();
        }

        private void DrawResizer()
        {
            Vector2 resizeHandleSize = new Vector2(20, 20);
            Rect resizeRect = new Rect(_windowRect.width - resizeHandleSize.x, _windowRect.height - resizeHandleSize.y, resizeHandleSize.x, resizeHandleSize.y);
            GUI.Box(resizeRect, "◢", ResizeLabelStyle());

            Event e = Event.current;
            if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition)) e.Use();
            else if (e.type == EventType.MouseDrag && resizeRect.Contains(e.mousePosition))
            {
                _windowRect.width += e.delta.x;
                _windowRect.height += e.delta.y;
                if (_windowRect.width < 450) _windowRect.width = 450;
                if (_windowRect.height < 300) _windowRect.height = 300;
                e.Use();
            }
        }

        private void SaveWindowConfig()
        {
            ConfigManager.Menu_X = _windowRect.x;
            ConfigManager.Menu_Y = _windowRect.y;
            ConfigManager.Menu_W = _windowRect.width;
            ConfigManager.Menu_H = _windowRect.height;
        }

        private GUIStyle CenteredLabel()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.alignment = TextAnchor.MiddleCenter;
            s.fontSize = 10;
            s.normal.textColor = Color.gray;
            return s;
        }

        private GUIStyle ResizeLabelStyle()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.alignment = TextAnchor.LowerRight;
            s.fontSize = 14;
            s.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            return s;
        }
    }
}