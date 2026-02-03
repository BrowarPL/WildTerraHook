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
        private PersistentWorldModule _persistentModule;
        private AutoHealModule _healModule;
        private QuickStackModule _quickStackModule;
        private BuildingModule _buildingModule;

        private bool _showMenu = true;
        private Rect _windowRect;
        private bool _isInitialized = false;

        private int _currentTab = 0;

        public void Start()
        {
            Localization.Init();
            ConfigManager.Load();

            _currentTab = ConfigManager.Menu_Tab;

            if (_currentTab >= 0 && _currentTab < ConfigManager.TabWidths.Length)
            {
                float w = ConfigManager.TabWidths[_currentTab];
                float h = ConfigManager.TabHeights[_currentTab];
                if (w < 250) w = 250;
                if (h < 200) h = 200;
                _windowRect = new Rect(ConfigManager.Menu_X, ConfigManager.Menu_Y, w, h);
            }
            else
            {
                _windowRect = new Rect(ConfigManager.Menu_X, ConfigManager.Menu_Y, 400, 300);
            }

            _espModule = new ResourceEspModule();
            _lootModule = new AutoLootModule();
            _dropModule = new AutoDropModule();
            _miscModule = new MiscModule();
            _colorFishModule = new ColorFishingModule();
            _memFishModule = new FishBotModule();
            _persistentModule = new PersistentWorldModule();
            _healModule = new AutoHealModule();
            _quickStackModule = new QuickStackModule();
            _buildingModule = new BuildingModule();

            _espModule.SetPersistentModule(_persistentModule);
            _consoleModule = new DebugConsoleModule();
            Debug.Log("[MainHack] Hook załadowany pomyślnie.");

            _isInitialized = true;

            try
            {
                // Szukamy typu ItemActionType w Assembly gry
                System.Type enumType = System.Type.GetType("ItemActionType, Assembly-CSharp");

                if (enumType != null)
                {
                    Debug.LogWarning("=== LISTA AKCJI (ItemActionType) ===");
                    System.Array values = System.Enum.GetValues(enumType);
                    foreach (object val in values)
                    {
                        // Używamy Convert.ToInt32 zamiast (int)val, aby uniknąć błędu rzutowania
                        int intValue = System.Convert.ToInt32(val);
                        Debug.Log($"Action: {val.ToString()} = {intValue}");
                    }
                    Debug.LogWarning("===================================");
                }
                else
                {
                    Debug.LogError("Nie znaleziono typu ItemActionType!");
                }
            }
            catch (System.Exception e) { Debug.LogError("Błąd dumpowania: " + e.Message); }
        }

        public void OnDestroy()
        {
            // Zapisz config przed śmiercią
            SaveWindowConfig();
            ConfigManager.Save();

            if (_espModule != null)
            {
                bool wasEnabled = ConfigManager.Esp_Enabled;
                ConfigManager.Esp_Enabled = false;
                _espModule.Update(); // Force cleanup
                ConfigManager.Esp_Enabled = wasEnabled;
            }
            if (_miscModule != null)
            {
                bool wasBright = ConfigManager.Misc_BrightPlayer;
                bool wasFull = ConfigManager.Misc_Fullbright;
                ConfigManager.Misc_BrightPlayer = false;
                ConfigManager.Misc_Fullbright = false;
                _miscModule.Update(); // Force cleanup
                ConfigManager.Misc_BrightPlayer = wasBright;
                ConfigManager.Misc_Fullbright = wasFull;
            }
            if (_persistentModule != null) _persistentModule.ClearCache();
            if (_consoleModule != null) _consoleModule.Shutdown();

            // CLEANUP Quick Stack Button
            if (_quickStackModule != null) _quickStackModule.OnDestroy();
        }

        public void Update()
        {
            if (_showMenu) BlockInputIfOverWindow();

            if (Input.GetKeyDown(KeyCode.Insert)) _showMenu = !_showMenu;

            // --- PEŁNY EJECT ---
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Loader.Unload(); // To zniszczy ten GameObject i wywoła OnDestroy
                return;
            }

            _espModule.Update();
            _lootModule.Update();
            _dropModule.Update();
            _miscModule.Update();
            _colorFishModule.Update();
            _persistentModule.Update();
            _healModule.Update();
            _quickStackModule.Update();
            _buildingModule.Update();
        }

        private void BlockInputIfOverWindow()
        {
            if (ConfigManager.Menu_Scale <= 0) return;
            Vector2 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            mousePos /= ConfigManager.Menu_Scale;

            if (_windowRect.Contains(mousePos))
            {
                if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0) ||
                    Input.GetMouseButton(1) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonUp(1))
                {
                    Input.ResetInputAxes();
                }
            }
        }

        public void OnGUI()
        {
            if (!_isInitialized) return;

            _espModule.DrawESP();
            _colorFishModule.DrawESP();

            // GUI modułów (np. przycisk QS)
            _quickStackModule.OnGUI();

            if (_showMenu)
            {
                Matrix4x4 oldMatrix = GUI.matrix;
                float scale = ConfigManager.Menu_Scale;
                if (scale < 0.5f) scale = 0.5f;

                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

                _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, Localization.Get("MENU_TITLE"), GUILayout.MinWidth(250), GUILayout.MinHeight(200));

                GUI.matrix = oldMatrix;
            }
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(Localization.Get("MENU_TOGGLE_INFO"), CenteredLabel());
            GUILayout.Space(5);

            string[] tabNames = {
                Localization.Get("MENU_TAB_ESP"),
                Localization.Get("MENU_TAB_FISH"),
                Localization.Get("MENU_TAB_LOOT"),
                Localization.Get("MENU_TAB_DROP"),
                Localization.Get("MENU_TAB_MISC"),
                Localization.Get("MENU_TAB_COMBAT"),
                Localization.Get("MENU_TAB_CONSOLE")
            };

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            int newTab = GUILayout.Toolbar(_currentTab, tabNames, GUILayout.Height(30), GUILayout.Width(_windowRect.width * 0.96f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (newTab != _currentTab)
            {
                if (_currentTab >= 0 && _currentTab < ConfigManager.TabWidths.Length)
                {
                    ConfigManager.TabWidths[_currentTab] = _windowRect.width;
                    ConfigManager.TabHeights[_currentTab] = _windowRect.height;
                }

                _currentTab = newTab;
                ConfigManager.Menu_Tab = _currentTab;

                if (_currentTab >= 0 && _currentTab < ConfigManager.TabWidths.Length)
                {
                    float w = ConfigManager.TabWidths[_currentTab];
                    float h = ConfigManager.TabHeights[_currentTab];
                    if (w < 250) w = 350;
                    if (h < 200) h = 300;
                    _windowRect.width = w;
                    _windowRect.height = h;
                }
            }

            GUILayout.Space(10);

            switch (_currentTab)
            {
                case 0: _espModule.DrawMenu(); break;
                case 1: DrawFishingTab(); break;
                case 2: _lootModule.DrawMenu(); break;
                case 3: _dropModule.DrawMenu(); break;
                case 4: DrawMiscTab(); break;
                case 5: DrawCombatTab(); break;
                case 6: _consoleModule.DrawMenu(); break;
            }

            GUILayout.Space(10);
            GUILayout.EndVertical();

            DrawResizer();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            if (GUI.changed || Input.GetMouseButtonUp(0))
            {
                SaveWindowConfig();
            }
        }

        private void DrawCombatTab()
        {
            GUILayout.Space(10);
            GUILayout.Label("___________________________________________________");
            GUILayout.Space(10);
            _healModule.DrawMenu();
        }

        private void DrawFishingTab()
        {
            GUILayout.BeginVertical("box");
            bool colorEn = GUILayout.Toggle(ConfigManager.ColorFish_Enabled, " <b>" + Localization.Get("FISH_COLOR_BOT_TOGGLE") + "</b>");
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
            else
            {
                GUILayout.Label("<i>Memory Bot disabled (Work in Progress)</i>", CenteredLabel());
            }
            GUILayout.EndVertical();
        }

        private void DrawMiscTab()
        {
            _miscModule.DrawMenu();
            GUILayout.Space(10);

            // --- QUICK STACK MENU ---
            _quickStackModule.DrawMenu();
            GUILayout.Space(10);
            // ------------------------
            _buildingModule.DrawMenu();
            GUILayout.Space(10);

            if (_persistentModule != null) _persistentModule.DrawMenu();
            GUILayout.Space(10);
            GUILayout.Label("<b>" + Localization.Get("MISC_UI_HEADER") + "</b>", GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Localization.Get("MISC_UI_SCALE")}: {ConfigManager.Menu_Scale:F1}x", GUILayout.Width(80));
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
                if (_windowRect.width < 250) _windowRect.width = 250;
                if (_windowRect.height < 200) _windowRect.height = 200;
                e.Use();
            }
        }

        private void SaveWindowConfig()
        {
            ConfigManager.Menu_X = _windowRect.x;
            ConfigManager.Menu_Y = _windowRect.y;
            ConfigManager.Menu_W = _windowRect.width;
            ConfigManager.Menu_H = _windowRect.height;

            if (_currentTab >= 0 && _currentTab < ConfigManager.TabWidths.Length)
            {
                ConfigManager.TabWidths[_currentTab] = _windowRect.width;
                ConfigManager.TabHeights[_currentTab] = _windowRect.height;
            }
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