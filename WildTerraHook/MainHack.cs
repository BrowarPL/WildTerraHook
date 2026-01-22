using UnityEngine;

namespace WildTerraHook
{
    public class MainHack : MonoBehaviour
    {
        private ResourceEspModule _espModule;
        private AutoLootModule _lootModule;
        private MiscModule _miscModule;
        private ColorFishingModule _colorFishModule;
        private FishBotModule _memFishModule;

        private bool _showMenu = true;
        private Rect _windowRect;
        private bool _isInitialized = false;

        private string[] _tabNames = { "ESP", "Fishing", "Auto Loot", "Misc" };
        private int _currentTab = 0;

        public void Start()
        {
            Localization.Init();
            ConfigManager.Load();

            _windowRect = new Rect(ConfigManager.Menu_X, ConfigManager.Menu_Y, ConfigManager.Menu_W, ConfigManager.Menu_H);
            if (_windowRect.height < 50) _windowRect.height = 0;
            if (_windowRect.width < 400) _windowRect.width = 450; // Minimalna startowa szerokość

            _currentTab = ConfigManager.Menu_Tab;
            _isInitialized = true;

            _espModule = new ResourceEspModule();
            _lootModule = new AutoLootModule();
            _miscModule = new MiscModule();
            _colorFishModule = new ColorFishingModule();
            _memFishModule = new FishBotModule();
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
            _miscModule.Update();
            _colorFishModule.Update();
            _memFishModule.Update();
        }

        public void OnGUI()
        {
            if (!_isInitialized) return;

            _espModule.DrawESP();
            _colorFishModule.DrawESP();
            _memFishModule.DrawESP();

            if (_showMenu)
            {
                Matrix4x4 oldMatrix = GUI.matrix;
                float scale = ConfigManager.Menu_Scale;
                if (scale < 0.5f) scale = 0.5f;

                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

                // GUILayout.Window automatycznie dopasuje wysokość do zawartości
                _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, Localization.Get("MENU_TITLE"), GUILayout.MinWidth(400));

                GUI.matrix = oldMatrix;
            }
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(Localization.Get("MENU_TOGGLE_INFO"), CenteredLabel());
            GUILayout.Space(10);

            // --- SZEROKIE ZAKŁADKI (90% szerokości) ---
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            float width = _windowRect.width * 0.9f;
            int newTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30), GUILayout.Width(width));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (newTab != _currentTab)
            {
                _currentTab = newTab;
                ConfigManager.Menu_Tab = _currentTab;
                _windowRect.height = 0; // Reset wysokości dla auto-fit
            }

            GUILayout.Space(15);

            switch (_currentTab)
            {
                case 0: _espModule.DrawMenu(); break;
                case 1: DrawFishingTab(); break;
                case 2: _lootModule.DrawMenu(); break;
                case 3: DrawMiscTab(); break;
            }

            GUILayout.Space(10);
            GUILayout.EndVertical();

            DrawResizer();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            // Zapisz tylko przy puszczeniu myszki lub zmianie layoutu
            if (GUI.changed || Input.GetMouseButtonUp(0)) SaveWindowConfig();
        }

        private void DrawFishingTab()
        {
            // Wrapper dla ColorBota
            bool colorEn = ConfigManager.ColorFish_Enabled;
            if (DrawWideToggle(ref colorEn, "Color Bot (Standard)"))
            {
                ConfigManager.ColorFish_Enabled = colorEn;
                if (colorEn) ConfigManager.MemFish_Enabled = false;
                ConfigManager.Save();
            }

            if (ConfigManager.ColorFish_Enabled)
            {
                GUILayout.BeginVertical("box");
                _colorFishModule.DrawMenu();
                GUILayout.EndVertical();
            }

            GUILayout.Space(10);

            // Wrapper dla MemoryBota (DrawMenu wewnątrz modułu też zostało poprawione w FishBotModule.cs)
            // Ale tutaj wywołujemy tylko DrawMenu modułu, który sam rysuje nagłówek i toggle
            // Aby zachować spójność, w FishBotModule.cs użyłem metody ToggleBtn która rysuje szeroko.

            // Tutaj po prostu wywołujemy moduł, który sam ogarnia swoje UI
            _memFishModule.DrawMenu();
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

        // Pomocnicza metoda do rysowania szerokiego Toggle w stylu przycisku
        private bool DrawWideToggle(ref bool value, string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool ret = GUILayout.Toggle(value, text, "button", GUILayout.Width(_windowRect.width * 0.9f), GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return ret;
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
                if (_windowRect.width < 400) _windowRect.width = 400;
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