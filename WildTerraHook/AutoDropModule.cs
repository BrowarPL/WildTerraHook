using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;

namespace WildTerraHook
{
    public class AutoDropModule
    {
        private float _nextDropTime = 0f;
        private string _searchQuery = "";
        private Vector2 _scrollPos;
        private Vector2 _activeListScrollPos;
        private Vector2 _savedProfilesScroll;
        private string _newProfileName = "";

        private List<string> _allGameItems = new List<string>();
        private float _lastCacheTime = 0f;

        private MethodInfo _dropMethod;
        private bool _initReflection = false;

        public void Update()
        {
            if (!ConfigManager.Drop_Enabled) return;

            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null || player.inventory == null) return;

            if (!_initReflection)
            {
                InitDropMethod(player);
                _initReflection = true;
            }

            if (_dropMethod == null) return;
            if (Time.time < _nextDropTime) return;

            List<string> blackList = ConfigManager.GetCombinedActiveDropList();
            if (blackList.Count == 0) return;

            for (int i = 0; i < player.inventory.Count; i++)
            {
                var slot = player.inventory[i];
                if (slot.amount <= 0) continue;

                string itemName = "Unknown";
                try
                {
                    if (slot.item.data != null) itemName = slot.item.data.name;
                    else itemName = slot.item.name;
                }
                catch { continue; }

                if (string.IsNullOrEmpty(itemName)) continue;

                bool shouldDrop = false;
                foreach (string badItem in blackList)
                {
                    if (itemName.IndexOf(badItem, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldDrop = true;
                        break;
                    }
                }

                if (shouldDrop)
                {
                    try
                    {
                        // Wywołanie: CmdDropInventoryItem(index)
                        _dropMethod.Invoke(player, new object[] { i });

                        if (ConfigManager.Drop_Debug)
                            Debug.Log($"[AutoDrop] Wyrzucono: {itemName} (Slot: {i})");

                        _nextDropTime = Time.time + ConfigManager.Drop_Delay;
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AutoDrop] Wyjątek dropowania: {ex.Message}");
                    }
                }
            }
        }

        private void InitDropMethod(object playerInstance)
        {
            _dropMethod = null;
            try
            {
                Type type = playerInstance.GetType();
                // Szukamy konkretnie CmdDropInventoryItem
                _dropMethod = type.GetMethod("CmdDropInventoryItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (_dropMethod != null)
                {
                    Debug.Log("[AutoDrop] Metoda znaleziona: CmdDropInventoryItem");
                }
                else
                {
                    Debug.LogError("[AutoDrop] Błąd: Nie znaleziono CmdDropInventoryItem!");
                }
            }
            catch (Exception ex) { Debug.LogError($"[AutoDrop] Init Error: {ex.Message}"); }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>AUTO DROP (Blacklist)</b>");

            GUILayout.BeginHorizontal();
            bool newVal = GUILayout.Toggle(ConfigManager.Drop_Enabled, " WŁĄCZ");
            if (newVal != ConfigManager.Drop_Enabled) { ConfigManager.Drop_Enabled = newVal; ConfigManager.Save(); }

            bool debugVal = GUILayout.Toggle(ConfigManager.Drop_Debug, " Debug Logi");
            if (debugVal != ConfigManager.Drop_Debug) { ConfigManager.Drop_Debug = debugVal; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Opóźnienie: {ConfigManager.Drop_Delay:F2}s");
            float newDelay = GUILayout.HorizontalSlider(ConfigManager.Drop_Delay, 0.1f, 2.0f);
            if (Mathf.Abs(newDelay - ConfigManager.Drop_Delay) > 0.01f) { ConfigManager.Drop_Delay = newDelay; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Dynamiczna wysokość list (np. 25% okna)
            float listHeight = Mathf.Max(100, ConfigManager.Menu_H * 0.25f);

            // SEKCJ 1: Dodawanie
            GUILayout.Label("<b>Dodaj do Blacklisty:</b>");
            _searchQuery = GUILayout.TextField(_searchQuery);

            if (Time.time - _lastCacheTime > 5.0f && global::ScriptableItem.dict != null)
            {
                _allGameItems = global::ScriptableItem.dict.Values.Select(x => x.name).ToList();
                _lastCacheTime = Time.time;
            }

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                GUILayout.BeginVertical("box");
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(listHeight));

                var matches = _allGameItems.Where(x => x.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0).Take(20);

                foreach (var item in matches)
                {
                    if (GUILayout.Button($"Dodaj: {item}"))
                    {
                        AddItemToActiveProfiles(item);
                        _searchQuery = "";
                        ConfigManager.Save();
                    }
                }

                if (GUILayout.Button($"[+] Dodaj ręcznie: \"{_searchQuery}\""))
                {
                    AddItemToActiveProfiles(_searchQuery);
                    _searchQuery = "";
                    ConfigManager.Save();
                }

                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            GUILayout.Space(5);

            // SEKCJA 2: Lista Aktywna
            GUILayout.Label("<b>Lista Wyrzucanych Przedmiotów:</b>");
            var combinedList = ConfigManager.GetCombinedActiveDropList();

            if (combinedList.Count == 0)
            {
                GUILayout.Label("<i>(Lista jest pusta)</i>");
            }
            else
            {
                GUILayout.BeginVertical(GUI.skin.box);
                _activeListScrollPos = GUILayout.BeginScrollView(_activeListScrollPos, GUILayout.Height(listHeight));

                foreach (var item in combinedList)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(item);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("X", GUILayout.Width(30)))
                    {
                        RemoveItemFromActiveProfiles(item);
                        ConfigManager.Save();
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();
                GUILayout.EndVertical();

                if (GUILayout.Button("Wyczyść Całą Listę"))
                {
                    ClearAllDropLists();
                    ConfigManager.Save();
                }
            }

            GUILayout.Space(5);
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
                    ConfigManager.DropProfiles[profName].Remove(item);
            }
        }

        private void ClearAllDropLists()
        {
            foreach (var profName in ConfigManager.ActiveDropProfiles)
            {
                if (ConfigManager.DropProfiles.ContainsKey(profName))
                    ConfigManager.DropProfiles[profName].Clear();
            }
        }

        private void DrawProfileManager()
        {
            GUILayout.Label("<b>Zarządzanie Profilami:</b>");

            // Stała wysokość dla profili (są zazwyczaj krótkie)
            _savedProfilesScroll = GUILayout.BeginScrollView(_savedProfilesScroll, GUILayout.Height(80));
            foreach (var profileKey in ConfigManager.DropProfiles.Keys.ToList())
            {
                GUILayout.BeginHorizontal();
                bool isActive = ConfigManager.ActiveDropProfiles.Contains(profileKey);
                bool newActive = GUILayout.Toggle(isActive, profileKey);

                if (newActive != isActive)
                {
                    if (newActive) ConfigManager.ActiveDropProfiles.Add(profileKey);
                    else ConfigManager.ActiveDropProfiles.Remove(profileKey);
                    ConfigManager.Save();
                }

                GUILayout.FlexibleSpace();
                // Teraz można usunąć nawet Default
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    ConfigManager.DropProfiles.Remove(profileKey);
                    ConfigManager.ActiveDropProfiles.Remove(profileKey);

                    // Zabezpieczenie: jeśli usunęliśmy wszystko, dodaj nowy Default
                    if (ConfigManager.DropProfiles.Count == 0)
                    {
                        ConfigManager.DropProfiles.Add("Default", new List<string>());
                        ConfigManager.ActiveDropProfiles.Add("Default");
                    }
                    ConfigManager.Save();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _newProfileName = GUILayout.TextField(_newProfileName, GUILayout.Width(120));
            if (GUILayout.Button("Utwórz Profil"))
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