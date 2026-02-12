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
        private AutoPetFeederModule _feederModule;
        private AutoActionModule _autoActionModule;
        private SkillBarUnlockerModule _skillUnlockerModule;
        private ObjectManagerModule _objManagerModule;
        private FastAttackModule _fastAttackModule;
        private DungeonHelperModule _dungeonModule;

        private bool _showMenu = true;
        private Rect _windowRect;
        private bool _isInitialized = false;

        private int _currentTab = 0;

        public void Start()
        {
            Localization.Init();
            ConfigManager.Load();

            _currentTab = ConfigManager.Menu_Tab;

            // --- [POPRAWKA] BEZPIECZNE INICJOWANIE OKNA ---
            float startW = 400;
            float startH = 300;

            if (_currentTab >= 0 && _currentTab < ConfigManager.TabWidths.Length)
            {
                startW = ConfigManager.TabWidths[_currentTab];
                startH = ConfigManager.TabHeights[_currentTab];
            }

            // Zabezpieczenie: Jeśli zapisana szerokość/wysokość jest większa niż ekran - zmniejsz ją
            if (startW > Screen.width) startW = Screen.width - 50;
            if (startH > Screen.height) startH = Screen.height - 50;

            // Zabezpieczenie minimalne
            if (startW < 300) startW = 300;
            if (startH < 200) startH = 200;

            _windowRect = new Rect(ConfigManager.Menu_X, ConfigManager.Menu_Y, startW, startH);

            // Zabezpieczenie pozycji (żeby belka nie uciekła poza ekran)
            if (_windowRect.x < 0) _windowRect.x = 0;
            if (_windowRect.y < 0) _windowRect.y = 0;
            // ----------------------------------------------

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
            _feederModule = new AutoPetFeederModule();
            _autoActionModule = new AutoActionModule();
            _miscModule.ActionModuleRef = _autoActionModule;
            _skillUnlockerModule = new SkillBarUnlockerModule();
            _objManagerModule = new ObjectManagerModule();
            _fastAttackModule = new FastAttackModule();
            _dungeonModule = new DungeonHelperModule();

            _espModule.SetPersistentModule(_persistentModule);
            _consoleModule = new DebugConsoleModule();
            Debug.Log("[MainHack] Hook załadowany pomyślnie.");

            _isInitialized = true;
        }

        public void OnDestroy()
        {
            SaveWindowConfig();
            ConfigManager.Save();

            if (_espModule != null) ConfigManager.Esp_Enabled = false;
            if (_persistentModule != null) _persistentModule.ClearCache();
            if (_consoleModule != null) _consoleModule.Shutdown();
            if (_quickStackModule != null) _quickStackModule.OnDestroy();
        }

        public void Update()
        {
            if (_showMenu) BlockInputIfOverWindow();

            if (Input.GetKeyDown(KeyCode.Insert)) _showMenu = !_showMenu;

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Loader.Unload();
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
            _feederModule.Update();
            _autoActionModule.Update();
            _skillUnlockerModule.Update();
            _objManagerModule.Update();
            _fastAttackModule.Update();
            _dungeonModule.Update();
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
            _quickStackModule.OnGUI();
            _dungeonModule.OnGUI();

            if (_showMenu)
            {
                Matrix4x4 oldMatrix = GUI.matrix;
                float scale = ConfigManager.Menu_Scale;
                if (scale < 0.5f) scale = 0.5f;

                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

                // Rysowanie okna z minimalnymi wymiarami
                _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, Localization.Get("MENU_TITLE"), GUILayout.MinWidth(400), GUILayout.MinHeight(300));

                // [POPRAWKA] Upewnij się, że okno nie ucieka poza ekran podczas rysowania
                _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - 50);
                _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - 50);
                _windowRect.width = Mathf.Clamp(_windowRect.width, 300, Screen.width);
                _windowRect.height = Mathf.Clamp(_windowRect.height, 200, Screen.height);

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
                Localization.Get("MENU_TAB_OBJECTS"),
                Localization.Get("MENU_TAB_CONSOLE")
            };

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            int newTab = GUILayout.Toolbar(_currentTab, tabNames, GUILayout.Height(30), GUILayout.Width(_windowRect.width * 0.96f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (newTab != _currentTab)
            {
                // Zapisz rozmiar starej zakładki
                if (_currentTab >= 0 && _currentTab < ConfigManager.TabWidths.Length)
                {
                    ConfigManager.TabWidths[_currentTab] = _windowRect.width;
                    ConfigManager.TabHeights[_currentTab] = _windowRect.height;
                }

                _currentTab = newTab;
                ConfigManager.Menu_Tab = _currentTab;

                // Wczytaj rozmiar nowej (Z ZABEZPIECZENIEM)
                if (_currentTab >= 0 && _currentTab < ConfigManager.TabWidths.Length)
                {
                    float w = ConfigManager.TabWidths[_currentTab];
                    float h = ConfigManager.TabHeights[_currentTab];

                    // Resetuj absurdalne wartości
                    if (w > Screen.width || w < 300) w = 500;
                    if (h > Screen.height || h < 200) h = 400;

                    _windowRect.width = w;
                    _windowRect.height = h;
                }
            }

            GUILayout.Space(10);

            // Scrollview dla całej zawartości okna, na wypadek gdyby elementy nie mieściły się w oknie
            // To zapobiegnie "rozpychaniu" okna przez zawartość
            // (Opcjonalne, ale bezpieczniejsze. W MiscModule mamy już wewnętrzne scrolle, więc tam zadziała dobrze)

            switch (_currentTab)
            {
                case 0: _espModule.DrawMenu(); break;
                case 1: DrawFishingTab(); break;
                case 2: _lootModule.DrawMenu(); break;
                case 3: _dropModule.DrawMenu(); break;
                case 4: DrawMiscTab(); break;
                case 5: DrawCombatTab(); break;
                case 6: _objManagerModule.DrawMenu(); break;
                case 7: _consoleModule.DrawMenu(); break;
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

        private void DrawMiscTab()
        {
            _miscModule.DrawMenu(
                () => _quickStackModule.DrawMenu(),
                () => _buildingModule.DrawMenu(),
                () => _persistentModule?.DrawMenu()
            );
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
                GUILayout.Label("<i>Memory Bot disabled</i>", CenteredLabel());
            }
            GUILayout.EndVertical();
        }

        // --- POPRAWIONY RESIZER ---
        private void DrawResizer()
        {
            Vector2 resizeHandleSize = new Vector2(20, 20);
            Rect resizeRect = new Rect(_windowRect.width - resizeHandleSize.x, _windowRect.height - resizeHandleSize.y, resizeHandleSize.x, resizeHandleSize.y);
            GUI.Box(resizeRect, "◢", ResizeLabelStyle());

            Event e = Event.current;
            if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition)) e.Use();
            else if (e.type == EventType.MouseDrag && resizeRect.Contains(e.mousePosition))
            {
                float newWidth = _windowRect.width + e.delta.x;
                float newHeight = _windowRect.height + e.delta.y;

                // [POPRAWKA] Ograniczenie maksymalnego rozmiaru do wielkości ekranu
                newWidth = Mathf.Clamp(newWidth, 300, Screen.width);
                newHeight = Mathf.Clamp(newHeight, 200, Screen.height);

                _windowRect.width = newWidth;
                _windowRect.height = newHeight;
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