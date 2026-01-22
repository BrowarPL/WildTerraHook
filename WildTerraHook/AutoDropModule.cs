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

        // NOWOŚĆ: Pole do ręcznego wpisania nazwy
        private string _manualMethodName = "";

        private List<string> _allGameItems = new List<string>();
        private float _lastCacheTime = 0f;
        private float _debugTimer = 0f;

        private MethodInfo _dropMethod;
        private bool _initReflection = false;

        public void Update()
        {
            if (!ConfigManager.Drop_Enabled) return;

            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null || player.inventory == null) return;

            // Inicjalizacja (ponowna, jeśli user zmienił nazwę ręczną)
            if (!_initReflection)
            {
                InitDropMethod(player);
                _initReflection = true;
            }

            if (Time.time < _nextDropTime) return;

            if (ConfigManager.Drop_Debug && Time.time > _debugTimer)
            {
                Debug.Log($"[AutoDrop] Skanowanie plecaka... (Metoda: {(_dropMethod != null ? _dropMethod.Name : "BRAK")})");
                _debugTimer = Time.time + 5.0f;
            }

            if (_dropMethod == null) return;

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
                        _dropMethod.Invoke(player, new object[] { i, slot.amount });

                        if (ConfigManager.Drop_Debug)
                            Debug.Log($"[AutoDrop] WYRZUCANIE: {itemName} (Slot: {i})");

                        _nextDropTime = Time.time + ConfigManager.Drop_Delay;
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AutoDrop] Wyjątek przy dropowaniu: {ex.Message}");
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
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                // 1. Jeśli użytkownik podał nazwę ręcznie, szukamy jej w pierwszej kolejności
                if (!string.IsNullOrEmpty(_manualMethodName))
                {
                    Debug.Log($"[AutoDrop] Szukam ręcznej metody: {_manualMethodName}...");
                    foreach (var m in methods)
                    {
                        if (m.Name.Equals(_manualMethodName, StringComparison.OrdinalIgnoreCase))
                        {
                            _dropMethod = m;
                            Debug.Log($"[AutoDrop] Znalazłem metodę ręczną: {m.Name}");
                            return;
                        }
                    }
                    Debug.LogError($"[AutoDrop] Nie znaleziono metody o nazwie: {_manualMethodName}");
                }

                // 2. Automatyczne szukanie (tylko jeśli nie podano ręcznej lub nie znaleziono)
                if (_dropMethod == null)
                {
                    foreach (var m in methods)
                    {
                        // Szukamy metody DropItem, CmdDropItem, itp.
                        if (m.Name.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var pars = m.GetParameters();
                            // Standard: (int index, int amount)
                            if (pars.Length == 2 && pars[0].ParameterType == typeof(int) && pars[1].ParameterType == typeof(int))
                            {
                                if (m.Name.StartsWith("Cmd"))
                                {
                                    _dropMethod = m;
                                    Debug.Log($"[AutoDrop] Auto-wykryto metodę: {m.Name}");
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.LogError($"[AutoDrop] Init Error: {ex.Message}"); }
        }

        private void RunDiagnostics()
        {
            Debug.Log("--- DIAGNOSTYKA AUTO DROP ---");
            if (global::Player.localPlayer == null) { Debug.LogError("Brak gracza!"); return; }

            Type t = typeof(global::WTPlayer);
            Debug.Log($"Skanowanie klasy: {t.Name}");

            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            foreach (var m in methods)
            {
                // Wypisz wszystko co ma Drop, Remove, Destroy lub Trash w nazwie
                if (m.Name.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Remove", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Trash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Destroy", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string pars = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray());
                    Debug.Log($"METODA: {m.Name} (Parametry: {pars})");
                }
            }
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

            // NOWOŚĆ: Pole do wpisania nazwy metody
            GUILayout.BeginHorizontal();
            GUILayout.Label("Metoda (Opcjonalne):", GUILayout.Width(120));
            string newMethod = GUILayout.TextField(_manualMethodName);
            if (newMethod != _manualMethodName)
            {
                _manualMethodName = newMethod;
                _initReflection = false; // Wymuś re-init przy zmianie
            }
            GUILayout.EndHorizontal();

            if (ConfigManager.Drop_Debug)
            {
                if (GUILayout.Button("DIAGNOSTYKA (Wypisz metody do konsoli)"))
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

            // Wyszukiwarka
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

            // Lista aktywna
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