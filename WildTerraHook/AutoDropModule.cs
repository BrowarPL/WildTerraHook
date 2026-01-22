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

        // --- REFLECTION ---
        private MethodInfo _dropMethod;
        private bool _initReflection = false;

        public void Update()
        {
            if (!ConfigManager.Drop_Enabled) return;

            // Pobierz gracza
            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;
            if (player.inventory == null) return;

            // Inicjalizacja metody (szukamy raz)
            if (!_initReflection) InitDropMethod(player);
            if (_dropMethod == null) return;

            if (Time.time < _nextDropTime) return;

            List<string> blackList = ConfigManager.GetCombinedActiveDropList();
            if (blackList.Count == 0) return;

            // Iteracja po plecaku
            for (int i = 0; i < player.inventory.Count; i++)
            {
                var slot = player.inventory[i];
                if (slot.amount <= 0) continue;

                string itemName = "";
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
                        _dropMethod.Invoke(player, new object[] { i, slot.amount });

                        if (ConfigManager.Drop_Debug)
                            Debug.Log($"[AutoDrop] Wyrzucono: {itemName} (Slot: {i})");

                        _nextDropTime = Time.time + ConfigManager.Drop_Delay;
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AutoDrop] Błąd dropowania: {ex.Message}");
                    }
                }
            }
        }

        private void InitDropMethod(object playerInstance)
        {
            try
            {
                Type type = playerInstance.GetType();
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                foreach (var m in methods)
                {
                    // Szukamy metody DropItem, CmdDropItem, itp.
                    if (m.Name.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var pars = m.GetParameters();
                        // Wymagana sygnatura: (int index, int amount)
                        if (pars.Length == 2 && pars[0].ParameterType == typeof(int) && pars[1].ParameterType == typeof(int))
                        {
                            if (m.Name.StartsWith("Cmd")) // Preferujemy Cmd (sieciowe)
                            {
                                _dropMethod = m;
                                Debug.Log($"[AutoDrop] Znalazłem metodę: {m.Name}");
                                _initReflection = true;
                                return;
                            }
                        }
                    }
                }
                _initReflection = true; // Oznaczamy jako sprawdzone, nawet jak nie znaleziono
            }
            catch (Exception ex) { Debug.LogError($"[AutoDrop] Init Error: {ex.Message}"); }
        }

        // --- DIAGNOSTYKA (Do kliknięcia w menu) ---
        private void RunDiagnostics()
        {
            Debug.Log("--- DIAGNOSTYKA AUTO DROP ---");
            if (global::Player.localPlayer == null) { Debug.LogError("Brak gracza (WTPlayer)!"); return; }

            Type t = typeof(global::WTPlayer);
            Debug.Log($"Skanowanie klasy: {t.Name}");

            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            bool foundAny = false;

            foreach (var m in methods)
            {
                if (m.Name.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string pars = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray());
                    Debug.Log($"Kandydat: {m.Name}({pars})");
                    foundAny = true;
                }
            }

            if (!foundAny) Debug.LogError("Nie znaleziono ŻADNEJ metody ze słowem 'Drop'!");
            Debug.Log("--- KONIEC DIAGNOSTYKI ---");
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>AUTO DROP (Blacklist)</b>");

            GUILayout.BeginHorizontal();
            bool newVal = GUILayout.Toggle(ConfigManager.Drop_Enabled, " WŁĄCZ");
            if (newVal != ConfigManager.Drop_Enabled) { ConfigManager.Drop_Enabled = newVal; ConfigManager.Save(); }

            bool debugVal = GUILayout.Toggle(ConfigManager.Drop_Debug, " Debug Mode");
            if (debugVal != ConfigManager.Drop_Debug) { ConfigManager.Drop_Debug = debugVal; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            // Przycisk diagnostyczny (widoczny tylko w debugu)
            if (ConfigManager.Drop_Debug)
            {
                if (GUILayout.Button("DIAGNOSTYKA: Wypisz Metody (Zobacz w Console)"))
                {
                    RunDiagnostics();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Opóźnienie: {ConfigManager.Drop_Delay:F2}s");
            float newDelay = GUILayout.HorizontalSlider(ConfigManager.Drop_Delay, 0.1f, 2.0f);
            if (Mathf.Abs(newDelay - ConfigManager.Drop_Delay) > 0.01f) { ConfigManager.Drop_Delay = newDelay; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUILayout.Space(5);

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
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(120));

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

                if (GUILayout.Button($"[+] Wymuś dodanie: \"{_searchQuery}\""))
                {
                    AddItemToActiveProfiles(_searchQuery);
                    _searchQuery = "";
                    ConfigManager.Save();
                }

                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUILayout.Space(5);

            GUILayout.Label("<b>Aktywna Lista:</b>");
            var combinedList = ConfigManager.GetCombinedActiveDropList();

            if (combinedList.Count == 0)
            {
                GUILayout.Label("<i>(Lista pusta)</i>");
            }
            else
            {
                GUILayout.BeginVertical(GUI.skin.box);
                _activeListScrollPos = GUILayout.BeginScrollView(_activeListScrollPos, GUILayout.Height(150));

                foreach (var item in combinedList)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(item);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Usuń", GUILayout.Width(60)))
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
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
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
                if (profileKey != "Default")
                {
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        ConfigManager.DropProfiles.Remove(profileKey);
                        ConfigManager.ActiveDropProfiles.Remove(profileKey);
                        ConfigManager.Save();
                    }
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