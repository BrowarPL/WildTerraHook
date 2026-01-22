using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class AutoDropModule
    {
        private float _nextDropTime = 0f;
        private string _searchQuery = "";
        private Vector2 _scrollPos;
        private Vector2 _savedProfilesScroll;
        private string _newProfileName = "";

        // Lista wszystkich przedmiotów do podpowiedzi w wyszukiwarce
        private List<string> _allGameItems = new List<string>();
        private float _lastCacheTime = 0f;

        public void Update()
        {
            if (!ConfigManager.Drop_Enabled) return;
            if (Time.time < _nextDropTime) return;

            if (global::Player.localPlayer == null) return;
            var player = global::WTPlayer.localPlayer;

            // Sprawdzamy ekwipunek
            if (player.inventory == null) return;

            // Pobieramy aktywną blacklistę
            List<string> blackList = ConfigManager.GetCombinedActiveDropList();
            if (blackList.Count == 0) return;

            // Iterujemy przez sloty ekwipunku
            // Robimy to od końca lub bezpiecznie, ale tutaj wystarczy znaleźć jeden item na cykl
            for (int i = 0; i < player.inventory.Count; i++)
            {
                var item = player.inventory[i];
                if (item == null || string.IsNullOrEmpty(item.name)) continue;

                // Sprawdź czy przedmiot jest na czarnej liście (po nazwie)
                // Używamy Contains, aby łatwiej dopasowywać, lub Equals dla precyzji.
                // Tutaj zakładam Equals (exact match) lub Contains, zależnie od preferencji.
                // Dla bezpieczeństwa: exact match lub "zawiera" z Configu.

                bool shouldDrop = false;
                foreach (string badItem in blackList)
                {
                    if (item.name.IndexOf(badItem, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldDrop = true;
                        break;
                    }
                }

                if (shouldDrop)
                {
                    // WYRZUCAMY!
                    // Metoda do wyrzucania: Zazwyczaj player.CmdDropItem(index, amount) 
                    // lub player.CmdRemoveItem(index, amount).
                    // Sprawdzając WTPlayer: zazwyczaj jest to CmdDropItem.
                    try
                    {
                        player.CmdDropItem(i, item.amount);

                        if (ConfigManager.Drop_Debug)
                            Debug.Log($"[AutoDrop] Wyrzucono: {item.name} (x{item.amount}) ze slotu {i}");

                        // Resetujemy timer i przerywamy pętlę (jeden drop na cykl dla bezpieczeństwa)
                        _nextDropTime = Time.time + ConfigManager.Drop_Delay;
                        return;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[AutoDrop] Błąd dropowania: {ex.Message}");
                    }
                }
            }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>AUTO DROP (Blacklist)</b>");

            // Toggle On/Off
            bool newVal = GUILayout.Toggle(ConfigManager.Drop_Enabled, " Włącz Auto Drop");
            if (newVal != ConfigManager.Drop_Enabled) { ConfigManager.Drop_Enabled = newVal; ConfigManager.Save(); }

            // Delay slider
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Opóźnienie: {ConfigManager.Drop_Delay:F2}s");
            float newDelay = GUILayout.HorizontalSlider(ConfigManager.Drop_Delay, 0.1f, 2.0f);
            if (Mathf.Abs(newDelay - ConfigManager.Drop_Delay) > 0.01f) { ConfigManager.Drop_Delay = newDelay; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- SEKCJA DODAWANIA PRZEDMIOTÓW ---
            GUILayout.Label("<b>Dodaj do Blacklisty:</b>");
            _searchQuery = GUILayout.TextField(_searchQuery);

            // Lista podpowiedzi (filtrowanie przedmiotów w grze)
            // Cache'ujemy listę raz na jakiś czas, żeby nie muliło
            if (Time.time - _lastCacheTime > 5.0f && global::ScriptableItem.dict != null)
            {
                _allGameItems = global::ScriptableItem.dict.Values.Select(x => x.name).ToList();
                _lastCacheTime = Time.time;
            }

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(100));

                // Filtrujemy
                var matches = _allGameItems.Where(x => x.IndexOf(_searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0).Take(20);

                foreach (var item in matches)
                {
                    if (GUILayout.Button(item))
                    {
                        AddItemToActiveProfiles(item);
                        _searchQuery = ""; // Wyczyść po dodaniu
                        ConfigManager.Save();
                    }
                }
                GUILayout.EndScrollView();
            }

            // Przycisk "Dodaj to co wpisałem" (jeśli nie znaleziono na liście, ale user chce wpisać ręcznie)
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                if (GUILayout.Button($"Dodaj ręcznie: '{_searchQuery}'"))
                {
                    AddItemToActiveProfiles(_searchQuery);
                    _searchQuery = "";
                    ConfigManager.Save();
                }
            }

            GUILayout.Space(10);

            // --- LISTA AKTYWNYCH BLOKAD ---
            GUILayout.Label("<b>Aktywna Blacklista:</b>");
            var combinedList = ConfigManager.GetCombinedActiveDropList();

            if (combinedList.Count == 0)
            {
                GUILayout.Label("(Pusta lista - nic nie wyrzucam)");
            }
            else
            {
                // Wyświetlamy listę z opcją usuwania
                // Uwaga: Usuwanie z "Combined" jest trudne, bo nie wiemy z którego profilu usunąć.
                // Dlatego usuwamy ze wszystkich aktywnych profili.

                // Używamy małego scrolla jeśli lista długa
                GUILayout.BeginVertical(GUI.skin.box);
                foreach (var item in combinedList)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(item);
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        RemoveItemFromActiveProfiles(item);
                        ConfigManager.Save();
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(10);

            // --- ZARZĄDZANIE PROFILAMI ---
            DrawProfileManager();

            GUILayout.EndVertical();
        }

        private void AddItemToActiveProfiles(string item)
        {
            // Dodajemy do wszystkich aktywnych profili (zazwyczaj jest jeden 'Default')
            foreach (var profName in ConfigManager.ActiveDropProfiles)
            {
                if (!ConfigManager.DropProfiles.ContainsKey(profName))
                    ConfigManager.DropProfiles[profName] = new List<string>();

                if (!ConfigManager.DropProfiles[profName].Contains(item))
                    ConfigManager.DropProfiles[profName].Add(item);
            }
        }

        private void RemoveItemFromActiveProfiles(string item)
        {
            foreach (var profName in ConfigManager.ActiveDropProfiles)
            {
                if (ConfigManager.DropProfiles.ContainsKey(profName))
                {
                    ConfigManager.DropProfiles[profName].Remove(item);
                }
            }
        }

        private void DrawProfileManager()
        {
            GUILayout.Label("<b>Profile Drop:</b>");

            // Lista profili (checkboxy)
            _savedProfilesScroll = GUILayout.BeginScrollView(_savedProfilesScroll, GUILayout.Height(80));
            foreach (var profileKey in ConfigManager.DropProfiles.Keys.ToList())
            {
                bool isActive = ConfigManager.ActiveDropProfiles.Contains(profileKey);
                bool newActive = GUILayout.Toggle(isActive, profileKey);
                if (newActive != isActive)
                {
                    if (newActive) ConfigManager.ActiveDropProfiles.Add(profileKey);
                    else ConfigManager.ActiveDropProfiles.Remove(profileKey);

                    // Zawsze musi być przynajmniej jeden aktywny? Niekoniecznie, ale lepiej tak.
                    ConfigManager.Save();
                }
            }
            GUILayout.EndScrollView();

            // Tworzenie nowego
            GUILayout.BeginHorizontal();
            _newProfileName = GUILayout.TextField(_newProfileName, GUILayout.Width(100));
            if (GUILayout.Button("Nowy Profil"))
            {
                if (!string.IsNullOrEmpty(_newProfileName) && !ConfigManager.DropProfiles.ContainsKey(_newProfileName))
                {
                    ConfigManager.DropProfiles.Add(_newProfileName, new List<string>());
                    ConfigManager.ActiveDropProfiles.Add(_newProfileName); // Auto-aktywacja
                    _newProfileName = "";
                    ConfigManager.Save();
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}