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
        // Rect jest teraz inicjalizowany z ConfigManager
        private Rect _windowRect;
        private bool _isInitialized = false;

        public void Start()
        {
            Localization.Init();
            ConfigManager.Load();

            // Wczytaj zapisaną pozycję/rozmiar
            _windowRect = new Rect(ConfigManager.Menu_X, ConfigManager.Menu_Y, ConfigManager.Menu_W, ConfigManager.Menu_H);
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
                SaveWindowConfig(); // Zapisz przy wyjściu
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

            // Rysuj ESP (bez skalowania, musi pasować do świata 3D)
            _espModule.DrawESP();

            if (_showMenu)
            {
                // --- GUI SCALING LOGIC ---
                // Zapisz oryginalną macierz
                Matrix4x4 oldMatrix = GUI.matrix;

                // Ustaw skalę
                float scale = ConfigManager.Menu_Scale;
                if (scale < 0.5f) scale = 0.5f; // Zabezpieczenie

                // Skaluj względem punktu (0,0) - ale musimy przesunąć okno logicznie
                // W OnGUI, GUI.Window działa specyficznie ze skalowaniem.
                // Najlepsza metoda to skalowanie globalne:
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

                // Rysuj okno (używając Rect, który będzie przeskalowany wizualnie)
                _windowRect = GUI.Window(0, _windowRect, DrawWindow, Localization.Get("MENU_TITLE"));

                // Przywróć macierz dla innych elementów (np. kursorów Unity)
                GUI.matrix = oldMatrix;

                // Ograniczenia ekranu (opcjonalne)
                // ClampWindowToScreen();
            }
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.Label(Localization.Get("MENU_TOGGLE_INFO"), CenteredLabel());
            GUILayout.Space(5);

            // --- SUWAK SKALI INTERFEJSU ---
            GUILayout.BeginHorizontal();
            GUILayout.Label($"UI Scale: {ConfigManager.Menu_Scale:F1}x", GUILayout.Width(100));
            float newScale = GUILayout.HorizontalSlider(ConfigManager.Menu_Scale, 0.8f, 2.5f);
            if (Mathf.Abs(newScale - ConfigManager.Menu_Scale) > 0.05f)
            {
                ConfigManager.Menu_Scale = newScale;
                // Save nie tutaj, bo będzie lagować przy przesuwaniu. Zapis przy zamknięciu/zmianie karty.
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();

            // Kolumna 1
            GUILayout.BeginVertical(GUILayout.Width(250));
            _espModule.DrawMenu();
            GUILayout.Space(10);
            _fishBotModule.DrawMenu();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Kolumna 2
            GUILayout.BeginVertical(GUILayout.Width(230));
            _lootModule.DrawMenu();
            GUILayout.Space(10);
            _miscModule.DrawMenu();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // Uchwyt zmiany rozmiaru (musi być narysowany NA KONIEC okna)
            DrawResizer();

            // Drag window (nagłówek)
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            // Zapisz pozycję/rozmiar przy każdej zmianie (lub użyj timera/Event.current.type == Repaint)
            if (GUI.changed) SaveWindowConfig();
        }

        private void DrawResizer()
        {
            // Rysujemy w prawym dolnym rogu okna
            Vector2 resizeHandleSize = new Vector2(20, 20);
            Rect resizeRect = new Rect(_windowRect.width - resizeHandleSize.x, _windowRect.height - resizeHandleSize.y, resizeHandleSize.x, resizeHandleSize.y);

            // Rysuj ikonkę (trójkąt w rogu)
            GUI.Box(resizeRect, "◢", ResizeLabelStyle());

            // Obsługa myszy
            Event e = Event.current;
            if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition))
            {
                e.Use(); // Przechwyć kliknięcie
            }
            else if (e.type == EventType.MouseDrag && resizeRect.Contains(e.mousePosition))
            {
                _windowRect.width += e.delta.x;
                _windowRect.height += e.delta.y;

                // Minimalny rozmiar
                if (_windowRect.width < 400) _windowRect.width = 400;
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
            // ConfigManager.Save() wywołujemy rzadziej, np. przy zamknięciu
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