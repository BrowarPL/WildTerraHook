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

        // [MEMORY BOT] - Zatrzymany
        private FishBotModule _memFishModule;

        private bool _showMenu = true;
        private Rect _windowRect;
        private bool _isInitialized = false;

        private string[] _tabNames = { "ESP", "Fishing", "Auto Loot", "Auto Drop", "Misc" };
        private int _currentTab = 0;

        public void Start()
        {
            Localization.Init();
            ConfigManager.Load();

            _windowRect = new Rect(ConfigManager.Menu_X, ConfigManager.Menu_Y, ConfigManager.Menu_W, ConfigManager.Menu_H);
            if (_windowRect.width < 350) _windowRect.width = 450;
            if (_windowRect.height < 50) _windowRect.height = 0;

            _currentTab = ConfigManager.Menu_Tab;
            _isInitialized = true;

            _espModule = new ResourceEspModule();
            _lootModule = new AutoLootModule();
            _dropModule = new AutoDropModule();
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
                // BLOKADA KLIKNIĘĆ W GRZE
                // Jeśli myszka jest nad oknem, "zjadamy" event, żeby gra go nie widziała
                BlockInputInMenu();

                Matrix4x4 oldMatrix = GUI.matrix;
                float scale = ConfigManager.Menu_Scale;
                if (scale < 0.5f) scale = 0.5f;

                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

                _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, Localization.Get("MENU_TITLE"), GUILayout.MinWidth(450));

                GUI.matrix = oldMatrix;
            }
        }

        private void BlockInputInMenu()
        {
            // Przeliczamy pozycję myszki (w OnGUI Y jest odwrócone względem Input.mousePosition)
            Vector2 mousePos = Event.current.mousePosition;

            // Sprawdzamy, czy myszka jest wewnątrz prostokąta okna (uwzględniając skalę)
            // Uproszczone sprawdzenie na oryginalnym Rect, bo GUI.Window sam zarządza focusowaniem,
            // ale musimy wymusić 'Eat' dla eventów myszy.

            // Uwaga: ConfigManager.Menu_Scale wpływa na renderowanie, ale Rect pozostaje w "logicznych" jednostkach.
            // Dla pewności sprawdzamy Contains na _windowRect.

            if (_windowRect.Contains(mousePos))
            {
                // Jeśli to kliknięcie lub scroll, zablokuj propagację do gry
                if (Event.current.type == EventType.MouseDown ||
                    Event.current.type == EventType.MouseUp ||
                    Event.current.type == EventType.ScrollWheel)
                {
                    // To zapobiega 'przepuszczaniu' kliknięć, ale Unity GUI i tak je obsłuży wewnątrz Window
                    // Ważne: wywołujemy to PRZED narysowaniem okna (co robimy w OnGUI),
                    // ale GUI.Window jest specyficzne.
                    // W praktyce w Unity IMGUI: GUI.Window zjada eventy automatycznie JEŚLI jest focusowane.
                    // Wymuszenie focusa:
                    GUI.FocusWindow(0);
                }
            }
        }

        private void DrawWindow(int windowID)
        {
            // Zjadanie eventu, aby nie przebijało na świat (dodatkowe zabezpieczenie)
            if (Event.current.type == EventType.MouseDown)
            {
                Event.current.Use();
            }

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
                _windowRect.height = 0;
            }

            GUILayout.Space(10);

            switch (_currentTab)
            {
                case 0: _espModule.DrawMenu(); break;
                case 1: DrawFishingTab(); break;
                case 2: _lootModule.DrawMenu(); break;
                case 3: _dropModule.DrawMenu(); break;
                case 4: DrawMiscTab(); break;
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
                if (_windowRect.width < 350) _windowRect.width = 350;
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