using UnityEngine;
using System.Collections;

namespace WildTerraHook
{
    public class MainHack : MonoBehaviour
    {
        // --- MODUŁY ---
        private PlayerAnalyzer _playerAnalyzer;
        private ResourceEspModule _espModule;
        private MiscModule _miscModule;
        private ColorFishingModule _fishingModule;

        // --- GUI ---
        private Rect _menuRect = new Rect(20, 20, 350, 600);
        private bool _showMenu = true;

        // GLOBALNY PRZEŁĄCZNIK (DELETE)
        private bool _globalUiVisible = true;

        private int _selectedTab = 0;
        private string[] _tabNames = { "ESP", "Player", "Fishing", "Misc" };

        public void Start()
        {
            // Ładowanie konfigu i języka na starcie
            ConfigManager.Load();
            Localization.Init();

            _playerAnalyzer = new PlayerAnalyzer();
            _espModule = new ResourceEspModule();
            _miscModule = new MiscModule();
            _fishingModule = new ColorFishingModule();
        }

        public void Update()
        {
            // 1. Obsługa GLOBALNEGO ukrywania (DELETE)
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                _globalUiVisible = !_globalUiVisible;
            }

            // Jeśli globalne UI jest wyłączone, nie przetwarzaj logiki modułów wizualnych (opcjonalnie)
            // Ale fishing bot czy inne automaty powinny działać w tle, więc Update zostawiamy

            // 2. Obsługa Menu (Insert) - działa tylko gdy Globalne UI jest widoczne
            if (_globalUiVisible && Input.GetKeyDown(KeyCode.Insert))
            {
                _showMenu = !_showMenu;
            }

            // Update modułów
            _playerAnalyzer.Update();
            _espModule.Update(); // Skanowanie obiektów
            _miscModule.Update(); // Fullbright, zoom etc.
            _fishingModule.Update();
        }

        public void OnGUI()
        {
            // Jeśli wyłączono UI klawiszem Delete -> nic nie rysuj
            if (!_globalUiVisible) return;

            // Rysowanie ESP (teksty na ekranie)
            _espModule.DrawESP();
            _fishingModule.OnGUI(); // Rysowanie prostokąta szukania ryb

            // Rysowanie Menu
            if (_showMenu)
            {
                _menuRect = GUILayout.Window(0, _menuRect, DrawMenuWindow, Localization.Get("MENU_TITLE"));
            }
        }

        private void DrawMenuWindow(int windowID)
        {
            GUILayout.Label(Localization.Get("MENU_TOGGLE_INFO"));
            GUILayout.Space(5);

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            GUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0:
                    _espModule.DrawMenu();
                    break;
                case 1:
                    _playerAnalyzer.DrawMenu();
                    break;
                case 2:
                    _fishingModule.DrawMenu();
                    break;
                case 3:
                    _miscModule.DrawMenu();
                    break;
            }

            GUI.DragWindow();
        }
    }
}