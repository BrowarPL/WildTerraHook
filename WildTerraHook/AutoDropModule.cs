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

        private List<string> _allGameItems = new List<string>();
        private float _lastCacheTime = 0f;

        public void Update()
        {
            if (!ConfigManager.Drop_Enabled) return;
            if (Time.time < _nextDropTime) return;

            // Rzutujemy na WTPlayer, aby mieć dostęp do CmdDropItem
            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;

            if (player.inventory == null) return;

            List<string> blackList = ConfigManager.GetCombinedActiveDropList();
            if (blackList.Count == 0) return;

            for (int i = 0; i < player.inventory.Count; i++)
            {
                // ItemSlot jest strukturą (struct), więc nie może być null
                // Sprawdzamy zawartość slotu
                var slot = player.inventory[i];

                // FIX: Sprawdź czy przedmiot w slocie istnieje
                if (slot.item == null || slot.amount <= 0) continue;

                // FIX: Pobierz nazwę z wewnętrznego obiektu item
                string itemName = slot.item.name;
                if (string.IsNullOrEmpty(itemName)) continue;

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
                        // FIX: Używamy rzutowanego gracza (WTPlayer)
                        player.CmdDropItem(i, slot.amount);

                        if (ConfigManager.Drop_Debug)
                            Debug.Log($"[AutoDrop] Wyrzucono: {itemName} (x{slot.amount}) ze slotu {i}");

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

            bool newVal = GUILayout.Toggle(ConfigManager.Drop_Enabled, " Włącz Auto Drop");
            if (newVal != ConfigManager.Drop_Enabled) { ConfigManager.Drop_Enabled = newVal; ConfigManager.Save(); }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Opóźnienie: {ConfigManager.Drop_Delay:F2}s");
            float newDelay = GUILayout.HorizontalSlider(ConfigManager.Drop_Delay, 0.1f, 2.0f);
            if (Mathf.Abs(newDelay - ConfigManager.Drop_Delay) > 0.01f) { ConfigManager.Drop_Delay = newDelay; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label("<b>Dodaj do Blacklisty:</b>");
            _searchQuery = GUILayout.TextField(_searchQuery);

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