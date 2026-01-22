using UnityEngine;

namespace WildTerraHook
{
    public class MainHack : MonoBehaviour
    {
        // --- MODUŁY ---
        private ResourceEspModule _espModule;
        private AutoLootModule _lootModule;
        private MiscModule _miscModule;
        private FishBotModule _fishBotModule; // NOWY MODUŁ

        // --- UI ---
        private bool _showMenu = true;
        private Rect _windowRect = new Rect(20, 20, 520, 500); // Nieco szersze okno

        public void Start()
        {
            // Inicjalizacja lokalizacji i configu
            Localization.Init();
            ConfigManager.Load();

            // Inicjalizacja modułów
            _espModule = new ResourceEspModule();
            _lootModule = new AutoLootModule();
            _miscModule = new MiscModule();
            _fishBotModule = new FishBotModule(); // Inicjalizacja FishBota
        }

        public void Update()
        {
            // Przełączanie Menu (Insert)
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                _showMenu = !_showMenu;
            }

            // Awaryjne zamknięcie (Delete) - usuwa Hacka
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                ConfigManager.Save();
                Destroy(this.gameObject);
                return;
            }

            // Aktualizacja modułów
            _espModule.Update();
            _lootModule.Update();
            _miscModule.Update();
            _fishBotModule.Update(); // Update FishBota
        }

        public void OnGUI()
        {
            // Rysowanie ESP (zawsze, jeśli włączone)
            _espModule.DrawESP();

            // Rysowanie Menu
            if (_showMenu)
            {
                _windowRect = GUI.Window(0, _windowRect, DrawWindow, Localization.Get("MENU_TITLE"));
            }
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.Label(Localization.Get("MENU_TOGGLE_INFO"), CenteredLabel());
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();

            // Kolumna 1: ESP & FishBot
            GUILayout.BeginVertical(GUILayout.Width(250));
            _espModule.DrawMenu();
            GUILayout.Space(10);
            _fishBotModule.DrawMenu(); // Menu FishBota
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Kolumna 2: Loot & Misc
            GUILayout.BeginVertical(GUILayout.Width(230));
            _lootModule.DrawMenu();
            GUILayout.Space(10);
            _miscModule.DrawMenu();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private GUIStyle CenteredLabel()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.alignment = TextAnchor.MiddleCenter;
            s.fontSize = 10;
            s.normal.textColor = Color.gray;
            return s;
        }
    }
}