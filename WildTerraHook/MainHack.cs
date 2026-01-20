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

        // --- GUI ---
        private Rect _menuRect = new Rect(20, 20, 350, 600);
        private bool _showMenu = true;
        private bool _globalUiVisible = true;

        private int _selectedTab = 0;
        // Zaktualizowana lista zakładek
        private string[] _tabNames = { "ESP", "Fishing", "Misc" };

        public void Start()
        {
            ConfigManager.Load();
            Localization.Init();

            _espModule = new ResourceEspModule();
            _miscModule = new MiscModule();
            _fishingModule = new ColorFishingModule();
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
        }

        public void OnGUI()
        {
            if (!_globalUiVisible) return;

            _espModule.DrawESP();
            _fishingModule.OnGUI();

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
                case 0: // ESP
                    _espModule.DrawMenu();
                    break;
                case 1: // Fishing
                    _fishingModule.DrawMenu();
                    break;
                case 2: // Misc
                    _miscModule.DrawMenu();
                    break;
            }

            GUI.DragWindow();
        }
    }
}