using UnityEngine;

namespace WildTerraHook
{
    public class MainHack : MonoBehaviour
    {
        // --- MODUŁY ---
        private ResourceEspModule _espModule;
        private AutoLootModule _lootModule;
        private MiscModule _miscModule;
        private FishBotModule _fishBotModule;

        // --- UI ---
        private bool _showMenu = true;
        private Rect _windowRect;
        private bool _isInitialized = false;

        // --- ZAKŁADKI ---
        private string[] _tabNames = { "ESP", "Fish Bot", "Auto Loot", "Misc" };
        private int _currentTab = 0;

        public void Start()
        {
            Localization.Init();
            ConfigManager.Load();

            // Wczytaj zapisaną pozycję
            _windowRect = new Rect(ConfigManager.Menu_X, ConfigManager.Menu_Y, ConfigManager.Menu_W, ConfigManager.Menu_H);
            // Jeśli wysokość jest 0 (domyślna), ustaw minimalną startową
            if (_windowRect.height < 50) _windowRect.height = 0; // 0 = auto-height w GUILayout.Window

            _currentTab = ConfigManager.Menu_Tab; // Wczytaj ostatnią zakładkę
            _isInitialized = true;

            _espModule = new ResourceEspModule();
            _lootModule = new AutoLootModule();
            _miscModule = new MiscModule();
            _fishBotModule = new FishBotModule();
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                _showMenu = !_showMenu;
            }

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
            _fishBotModule.Update();
        }

        public void OnGUI()
        {
            if (!_isInitialized) return;

            // Rysuj ESP
            _espModule.DrawESP();

            if (_showMenu)
            {
                // GUI SCALING
                Matrix4x4 oldMatrix = GUI.matrix;
                float scale = ConfigManager.Menu_Scale;
                if (scale < 0.5f) scale = 0.5f;

                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

                // Używamy GUILayout.Window aby okno dopasowywało się do zawartości
                _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, Localization.Get("MENU_TITLE"), GUILayout.MinWidth(300));

                GUI.matrix = oldMatrix;
            }
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();

            // Nagłówek
            GUILayout.Label(Localization.Get("MENU_TOGGLE_INFO"), CenteredLabel());
            GUILayout.Space(5);

            // ZAKŁADKI (Toolbar)
            int newTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30));
            if (newTab != _currentTab)
            {
                _currentTab = newTab;
                ConfigManager.Menu_Tab = _currentTab; // Zapisz zmianę
                // Resetujemy wysokość przy zmianie zakładki, aby okno się "zwinęło" do nowej treści
                _windowRect.height = 0;
            }

            GUILayout.Space(10);

            // ZAWARTOŚĆ ZAKŁADKI
            switch (_currentTab)
            {
                case 0: // ESP
                    _espModule.DrawMenu();
                    break;
                case 1: // Fish Bot
                    _fishBotModule.DrawMenu();
                    break;
                case 2: // Loot
                    _lootModule.DrawMenu();
                    break;
                case 3: // Misc
                    DrawMiscTab();
                    break;
            }

            GUILayout.Space(10);
            GUILayout.EndVertical();

            // Uchwyt zmiany rozmiaru (opcjonalny, ale przydatny)
            DrawResizer();

            // Drag window
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            // Zapisz pozycję
            if (GUI.changed || Input.GetMouseButtonUp(0)) SaveWindowConfig();
        }

        private void DrawMiscTab()
        {
            // Rysujemy Misc Module
            _miscModule.DrawMenu();

            GUILayout.Space(10);
            GUILayout.Label("<b>UI SETTINGS</b>", GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Scale: {ConfigManager.Menu_Scale:F1}x", GUILayout.Width(80));
            float newScale = GUILayout.HorizontalSlider(ConfigManager.Menu_Scale, 0.8f, 2.0f);
            if (Mathf.Abs(newScale - ConfigManager.Menu_Scale) > 0.05f)
            {
                ConfigManager.Menu_Scale = newScale;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawResizer()
        {
            Vector2 resizeHandleSize = new Vector2(20, 20);
            Rect r = GUILayoutUtility.GetRect(resizeHandleSize.x, resizeHandleSize.y, GUIStyle.none);
            // Pozycjonujemy w prawym dolnym rogu okna (obliczone przez GUILayout)
            Rect resizeRect = new Rect(_windowRect.width - resizeHandleSize.x, _windowRect.height - resizeHandleSize.y, resizeHandleSize.x, resizeHandleSize.y);

            GUI.Box(resizeRect, "◢", ResizeLabelStyle());

            Event e = Event.current;
            if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition))
            {
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && resizeRect.Contains(e.mousePosition))
            {
                _windowRect.width += e.delta.x;
                // Pozwalamy zmieniać wysokość, ale GUILayout i tak ją wymusi jeśli treść jest większa
                _windowRect.height += e.delta.y;
                if (_windowRect.width < 300) _windowRect.width = 300;
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