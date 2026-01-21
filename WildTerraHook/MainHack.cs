using UnityEngine;
using System.Collections;

namespace WildTerraHook
{
    public class MainHack : MonoBehaviour
    {
        // --- MODUŁY ---
        private ResourceEspModule _espModule;
        private MiscModule _miscModule;
        private ColorFishingModule _fishingModule;
        private AutoLootModule _lootModule;

        // --- GUI ---
        private Rect _menuRect = new Rect(20, 20, 600, 600);

        private bool _showMenu = true;
        private bool _globalUiVisible = true;

        private int _selectedTab = 0;
        private string[] _tabNames = { "ESP", "Fishing", "Loot", "Misc" };

        public void Start()
        {
            ConfigManager.Load();
            Localization.Init();

            _espModule = new ResourceEspModule();
            _miscModule = new MiscModule();
            _fishingModule = new ColorFishingModule();
            _lootModule = new AutoLootModule();
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                _globalUiVisible = !_globalUiVisible;
            }

            if (_globalUiVisible && Input.GetKeyDown(KeyCode.Insert))
            {
                _showMenu = !_showMenu;
            }

            // Update modułów
            _espModule.Update();
            _miscModule.Update();
            _fishingModule.Update();
            _lootModule.Update();
        }

        public void OnGUI()
        {
            if (!_globalUiVisible) return;

            // Rysowanie na ekranie
            _espModule.DrawESP();
            _fishingModule.OnGUI();
            _miscModule.OnGUI(); // NOWOŚĆ: Rysowanie okręgów agresji

            // Menu
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
                case 0: _espModule.DrawMenu(); break;
                case 1: _fishingModule.DrawMenu(); break;
                case 2: _lootModule.DrawMenu(); break;
                case 3: _miscModule.DrawMenu(); break;
            }

            GUI.DragWindow();
        }
    }
}