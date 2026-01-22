using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WildTerraHook
{
    public class AutoDropModule
    {
        private float _nextDropTime = 0f;
        private string _searchQuery = "";
        private Vector2 _scrollPos;
        private Vector2 _savedProfilesScroll;
        private string _newProfileName = "";

        private List<string> _allGameItems = new List<string>();
        private float _lastCacheTime = 0f;

        // --- REFLECTION DO DROPWANIA ---
        private MethodInfo _dropMethod;
        private bool _initReflection = false;

        public void Update()
        {
            if (!ConfigManager.Drop_Enabled) return;
            if (Time.time < _nextDropTime) return;

            // Pobierz gracza
            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;

            // Pobierz inwentarz
            if (player.inventory == null) return;

            // Inicjalizacja metody dropowania (szukamy metody CmdDropItem lub podobnej)
            if (!_initReflection)
            {
                // Szukamy metody w WTPlayer, która ma "Drop" w nazwie i bierze 2 inty (slotIndex, amount)
                var methods = typeof(global::WTPlayer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in methods)
                {
                    if (m.Name.IndexOf("Drop", System.StringComparison.OrdinalIgnoreCase) >= 0 && m.Name.StartsWith("Cmd"))
                    {
                        var pars = m.GetParameters();
                        if (pars.Length == 2 && pars[0].ParameterType == typeof(int) && pars[1].ParameterType == typeof(int))
                        {
                            _dropMethod = m;
                            Debug.Log($"[AutoDrop] Znaleziono metodę dropowania: {m.Name}");
                            break;
                        }
                    }
                }
                _initReflection = true;
            }

            if (_dropMethod == null) return; // Nie znaleziono metody, nie możemy działać

            List<string> blackList = ConfigManager.GetCombinedActiveDropList();
            if (blackList.Count == 0) return;

            // Iteracja po plecaku
            for (int i = 0; i < player.inventory.Count; i++)
            {
                // FIX: ItemSlot to struct, nie może być null. Pobieramy go przez wartość.
                var slot = player.inventory[i];

                // FIX: Sprawdzamy czy slot jest pusty po ilości (Amount > 0)
                if (slot.amount <= 0) continue;

                // FIX: Pobieramy nazwę z wnętrza struktury Item.
                // Struktura to zazwyczaj: ItemSlot -> Item -> name
                string itemName = "";
                try
                {
                    itemName = slot.item.name;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(itemName)) continue;

                // Sprawdzanie blacklisty
                bool shouldDrop = false;
                foreach (string badItem in blackList)
                {
                    if (itemName.IndexOf(badItem, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldDrop = true;
                        break;
                    }
                }

                if (shouldDrop)
                {
                    try
                    {
                        // WYWOŁANIE PRZEZ REFLECTION (omija błąd kompilacji CS1061)
                        // Wywołujemy np. CmdDropItem(i, slot.amount)
                        _dropMethod.Invoke(player, new object[] { i, slot.amount });

                        if (ConfigManager.Drop_Debug)
                            Debug.Log($"[AutoDrop] Wyrzucono: {itemName} (x{slot.amount}) ze slotu {i}");

                        _nextDropTime = Time.time + ConfigManager.Drop_Delay;
                        return; // Wyrzucamy tylko jeden przedmiot na cykl (bezpieczeństwo)
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[AutoDrop] Błąd podczas wyrzucania: {ex.Message}");
                    }
                }
            }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>AUTO DROP (Blacklist)</b>");

            bool newVal = GUILayout.Toggle(ConfigManager.Drop_Enabled, " Włącz Auto Drop");
            if (newVal != ConfigManager.Drop_Enabled) { ConfigManager.Drop_Enabled = newVal; ConfigManager.Save(); }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Opóźnienie: {ConfigManager.Drop_Delay:F2}s");
            float newDelay = GUILayout.HorizontalSlider(ConfigManager.Drop_Delay, 0.1f, 2.0f);
            if (Mathf.Abs(newDelay - ConfigManager.Drop_Delay) > 0.01f) { ConfigManager.Drop_Delay = newDelay; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            bool debugVal = GUILayout.Toggle(ConfigManager.Drop_Debug, " Debug Mode (Logi)");
            if (debugVal != ConfigManager.Drop_Debug) { ConfigManager.Drop_Debug = debugVal; ConfigManager.Save(); }

            GUILayout.Space(10);

            GUILayout.Label("<b>Dodaj do Blacklisty:</b>");
            _searchQuery = GUILayout.TextField(_searchQuery);

            // Pobieranie listy przedmiotów z gry (Cache co 5s)
            if (Time.time - _lastCacheTime > 5.0f && global::ScriptableItem.dict != null)
            {
                _allGameItems = global::ScriptableItem.dict.Values.Select(x => x.name).ToList();
                _lastCacheTime = Time.time;
            }

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(100));
                var matches = _allGameItems.Where(x => x.IndexOf(_searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0).Take(20);

                foreach (var item in matches)
                {
                    if (GUILayout.Button(item))
                    {
                        AddItemToActiveProfiles(item);
                        _searchQuery = "";
                        ConfigManager.Save();
                    }
                }
                GUILayout.EndScrollView();
            }

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
            GUILayout.Label("<b>Aktywna Blacklista:</b>");
            var combinedList = ConfigManager.GetCombinedActiveDropList();

            if (combinedList.Count == 0)
            {
                GUILayout.Label("(Pusta lista - nic nie wyrzucam)");
            }
            else
            {
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
            DrawProfileManager();
            GUILayout.EndVertical();
        }

        private void AddItemToActiveProfiles(string item)
        {
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
            _savedProfilesScroll = GUILayout.BeginScrollView(_savedProfilesScroll, GUILayout.Height(80));
            foreach (var profileKey in ConfigManager.DropProfiles.Keys.ToList())
            {
                bool isActive = ConfigManager.ActiveDropProfiles.Contains(profileKey);
                bool newActive = GUILayout.Toggle(isActive, profileKey);
                if (newActive != isActive)
                {
                    if (newActive) ConfigManager.ActiveDropProfiles.Add(profileKey);
                    else ConfigManager.ActiveDropProfiles.Remove(profileKey);
                    ConfigManager.Save();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _newProfileName = GUILayout.TextField(_newProfileName, GUILayout.Width(100));
            if (GUILayout.Button("Nowy Profil"))
            {
                if (!string.IsNullOrEmpty(_newProfileName) && !ConfigManager.DropProfiles.ContainsKey(_newProfileName))
                {
                    ConfigManager.DropProfiles.Add(_newProfileName, new List<string>());
                    ConfigManager.ActiveDropProfiles.Add(_newProfileName);
                    _newProfileName = "";
                    ConfigManager.Save();
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}