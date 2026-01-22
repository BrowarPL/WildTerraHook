using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class AutoLootModule
    {
        private float _nextLootTime = 0f;
        private string _searchQuery = "";
        private Vector2 _scrollPos;
        private Vector2 _activeListScrollPos;
        private Vector2 _savedProfilesScroll;
        private string _newProfileName = "";

        private List<string> _allGameItems = new List<string>();
        private float _lastCacheTime = 0f;

        public void Update()
        {
            if (!ConfigManager.Loot_Enabled) return;
            if (Time.time < _nextLootTime) return;

            if (global::Player.localPlayer == null) return;

            List<string> whiteList = ConfigManager.GetCombinedActiveList();
            if (whiteList.Count == 0) return;

            var items = Object.FindObjectsOfType<global::DroppedItem>();
            foreach (var item in items)
            {
                if (item == null || item.item == null || item.item.item == null) continue;

                float dist = Vector3.Distance(global::Player.localPlayer.transform.position, item.transform.position);
                if (dist > 5.0f) continue;

                string itemName = "";
                if (item.item.item.data != null) itemName = item.item.item.data.name;
                else itemName = item.item.item.name;

                bool shouldLoot = false;
                foreach (string wanted in whiteList)
                {
                    if (itemName.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldLoot = true;
                        break;
                    }
                }

                if (shouldLoot)
                {
                    global::Player.localPlayer.CmdPickUpItem(item.netId);
                    if (ConfigManager.Loot_Debug) Debug.Log($"[AutoLoot] Podniesiono: {itemName}");
                    _nextLootTime = Time.time + ConfigManager.Loot_Delay;
                    return;
                }
            }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>AUTO LOOT (Whitelist)</b>");

            GUILayout.BeginHorizontal();
            bool newVal = GUILayout.Toggle(ConfigManager.Loot_Enabled, " Włącz Auto Loot");
            if (newVal != ConfigManager.Loot_Enabled) { ConfigManager.Loot_Enabled = newVal; ConfigManager.Save(); }

            bool debugVal = GUILayout.Toggle(ConfigManager.Loot_Debug, " Debug Logi");
            if (debugVal != ConfigManager.Loot_Debug) { ConfigManager.Loot_Debug = debugVal; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Opóźnienie: {ConfigManager.Loot_Delay:F2}s");
            float newDelay = GUILayout.HorizontalSlider(ConfigManager.Loot_Delay, 0.1f, 2.0f);
            if (Mathf.Abs(newDelay - ConfigManager.Loot_Delay) > 0.01f) { ConfigManager.Loot_Delay = newDelay; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Dynamiczna wysokość
            float listHeight = Mathf.Max(100, ConfigManager.Menu_H * 0.25f);

            GUILayout.Label("<b>Szukaj i Dodaj:</b>");
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
                var matches = _allGameItems.Where(x => x.IndexOf(_searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0).Take(20);

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

            GUILayout.Label("<b>Lista Aktywna:</b>");
            var combinedList = ConfigManager.GetCombinedActiveList();

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

                if (GUILayout.Button("Wyczyść Listę"))
                {
                    ClearAllLootLists();
                    ConfigManager.Save();
                }
            }

            GUILayout.Space(5);
            DrawProfileManager();
            GUILayout.EndVertical();
        }

        private void AddItemToActiveProfiles(string item)
        {
            foreach (var profName in ConfigManager.ActiveProfiles)
            {
                if (!ConfigManager.LootProfiles.ContainsKey(profName))
                    ConfigManager.LootProfiles[profName] = new List<string>();

                if (!ConfigManager.LootProfiles[profName].Contains(item))
                    ConfigManager.LootProfiles[profName].Add(item);
            }
        }

        private void RemoveItemFromActiveProfiles(string item)
        {
            foreach (var profName in ConfigManager.ActiveProfiles)
            {
                if (ConfigManager.LootProfiles.ContainsKey(profName))
                    ConfigManager.LootProfiles[profName].Remove(item);
            }
        }

        private void ClearAllLootLists()
        {
            foreach (var profName in ConfigManager.ActiveProfiles)
            {
                if (ConfigManager.LootProfiles.ContainsKey(profName))
                    ConfigManager.LootProfiles[profName].Clear();
            }
        }

        private void DrawProfileManager()
        {
            GUILayout.Label("<b>Profile:</b>");
            _savedProfilesScroll = GUILayout.BeginScrollView(_savedProfilesScroll, GUILayout.Height(80));
            foreach (var profileKey in ConfigManager.LootProfiles.Keys.ToList())
            {
                GUILayout.BeginHorizontal();
                bool isActive = ConfigManager.ActiveProfiles.Contains(profileKey);
                bool newActive = GUILayout.Toggle(isActive, profileKey);
                if (newActive != isActive)
                {
                    if (newActive) ConfigManager.ActiveProfiles.Add(profileKey);
                    else ConfigManager.ActiveProfiles.Remove(profileKey);
                    ConfigManager.Save();
                }

                GUILayout.FlexibleSpace();
                // Można usunąć Default
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    ConfigManager.LootProfiles.Remove(profileKey);
                    ConfigManager.ActiveProfiles.Remove(profileKey);

                    if (ConfigManager.LootProfiles.Count == 0)
                    {
                        ConfigManager.LootProfiles.Add("Default", new List<string>());
                        ConfigManager.ActiveProfiles.Add("Default");
                    }
                    ConfigManager.Save();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _newProfileName = GUILayout.TextField(_newProfileName, GUILayout.Width(120));
            if (GUILayout.Button("Utwórz"))
            {
                if (!string.IsNullOrEmpty(_newProfileName) && !ConfigManager.LootProfiles.ContainsKey(_newProfileName))
                {
                    ConfigManager.LootProfiles.Add(_newProfileName, new List<string>());
                    ConfigManager.ActiveProfiles.Add(_newProfileName);
                    _newProfileName = "";
                    ConfigManager.Save();
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}