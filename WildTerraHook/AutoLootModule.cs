using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using System.Collections;

namespace WildTerraHook
{
    public class AutoLootModule
    {
        // --- USTAWIENIA ---
        public bool Enabled = false;
        public bool DebugMode = false;
        public float Delay = 0.2f;

        // --- DANE UI ---
        private List<string> _allItemsCache = new List<string>();
        private string _searchFilter = "";
        private string _newProfileName = "";

        private Vector2 _scrollProfiles;
        private Vector2 _scrollWhite;
        private Vector2 _scrollAll;
        private Vector2 _scrollDebug;

        // --- DANE LOOT ---
        private float _lootTimer = 0f;
        private string _status = "Idle";
        private List<string> _debugDetectedItems = new List<string>();

        // --- UPDATE ---
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            if (!IsPanelActive(containerUI))
            {
                if (_status.Contains("Loot") || _status.Contains("Widzę")) _status = "Czekam na okno...";
                if (_debugDetectedItems.Count > 0) _debugDetectedItems.Clear();
                return;
            }

            if (DebugMode || (Enabled && Time.time > _lootTimer))
            {
                ProcessContainer(containerUI);
            }
        }

        private void ProcessContainer(global::WTUIContainer ui)
        {
            try
            {
                _debugDetectedItems.Clear();

                Transform panel = ui.transform.Find("WTContainerPanel");
                if (panel == null || !panel.gameObject.activeSelf) return;

                Transform content = RecursiveFindChild(panel, "Content");
                if (content == null)
                {
                    _status = "Błąd: Brak Content";
                    return;
                }

                var dataSlots = GetDataSlots(ui);
                if (dataSlots == null) return;

                // Znajdź przyciski (aktywne)
                var uiSlots = panel.GetComponentsInChildren<global::WTUIContainerSlot>(false);
                bool lootedSomething = false;

                // Pobierz aktualną whitelistę z ConfigManagera
                List<string> currentWhitelist = ConfigManager.GetActiveList();

                foreach (var slotComp in uiSlots)
                {
                    if (slotComp.dragAndDropable == null) continue;

                    int realIndex;
                    if (!int.TryParse(slotComp.dragAndDropable.name, out realIndex)) continue;

                    string itemName = GetItemNameFromData(dataSlots, realIndex);

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        if (DebugMode) _debugDetectedItems.Add($"[{realIndex}] {itemName}");

                        if (Enabled && !lootedSomething && currentWhitelist.Contains(itemName))
                        {
                            if (slotComp.button != null && slotComp.button.interactable)
                            {
                                _status = $"Loot: {itemName}";
                                slotComp.button.onClick.Invoke();
                                lootedSomething = true;
                            }
                        }
                    }
                }

                if (lootedSomething) _lootTimer = Time.time + Delay;
            }
            catch (Exception ex)
            {
                _status = "Error: " + ex.Message;
            }
        }

        // --- GUI ---

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_TITLE")}</b>");

            // Górny panel: Włącznik, Debug, Delay
            GUILayout.BeginHorizontal();
            Enabled = GUILayout.Toggle(Enabled, Localization.Get("LOOT_ENABLE"), GUILayout.Width(150));
            DebugMode = GUILayout.Toggle(DebugMode, "Debug", GUILayout.Width(70));
            GUILayout.Label($"{Localization.Get("LOOT_DELAY")}: {Delay:F2}s", GUILayout.Width(80));
            Delay = GUILayout.HorizontalSlider(Delay, 0.05f, 1.0f);
            GUILayout.EndHorizontal();

            GUILayout.Label($"{Localization.Get("LOOT_STATUS")}: {_status}");

            if (DebugMode) DrawDebugSection();

            GUILayout.Space(5);

            // TRZY KOLUMNY
            GUILayout.BeginHorizontal();

            // 1. ZARZĄDZANIE PROFILAMI
            DrawProfileManager();

            // 2. ZAWARTOŚĆ PROFILU
            DrawCurrentProfileContent();

            // 3. WSZYSTKIE ITEMY
            DrawAllItemsList();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawDebugSection()
        {
            GUILayout.Label("<b>--- WYKRYTE ---</b>");
            _scrollDebug = GUILayout.BeginScrollView(_scrollDebug, "box", GUILayout.Height(80));
            if (_debugDetectedItems.Count > 0)
                foreach (var s in _debugDetectedItems) GUILayout.Label(s);
            else
                GUILayout.Label("...");
            GUILayout.EndScrollView();
        }

        private void DrawProfileManager()
        {
            GUILayout.BeginVertical("box", GUILayout.Width(170));
            GUILayout.Label($"<b>{Localization.Get("LOOT_PROFILES")}</b>");

            // Tworzenie nowego
            GUILayout.BeginHorizontal();
            _newProfileName = GUILayout.TextField(_newProfileName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                if (!string.IsNullOrEmpty(_newProfileName) && !ConfigManager.LootProfiles.ContainsKey(_newProfileName))
                {
                    ConfigManager.LootProfiles.Add(_newProfileName, new List<string>());
                    ConfigManager.ActiveProfile = _newProfileName; // Auto przełącz
                    ConfigManager.Save();
                    _newProfileName = "";
                }
            }
            GUILayout.EndHorizontal();

            // Lista profili
            _scrollProfiles = GUILayout.BeginScrollView(_scrollProfiles, GUILayout.Height(250));

            // Kopia kluczy do iteracji
            var profileNames = new List<string>(ConfigManager.LootProfiles.Keys);

            foreach (var profile in profileNames)
            {
                // Styl aktywnego profilu
                GUIStyle style = (profile == ConfigManager.ActiveProfile) ? GUI.skin.box : GUI.skin.label;

                GUILayout.BeginHorizontal(style);

                // Przycisk wyboru
                if (GUILayout.Button(profile, GUI.skin.label))
                {
                    ConfigManager.ActiveProfile = profile;
                    ConfigManager.Save();
                }

                // Przycisk usuwania (nie pozwól usunąć jedynego/ostatniego)
                if (ConfigManager.LootProfiles.Count > 1)
                {
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        ConfigManager.LootProfiles.Remove(profile);
                        // Jeśli usunęliśmy aktywny, przełącz na inny
                        if (ConfigManager.ActiveProfile == profile)
                            ConfigManager.ActiveProfile = ConfigManager.LootProfiles.Keys.First();
                        ConfigManager.Save();
                    }
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawCurrentProfileContent()
        {
            GUILayout.BeginVertical("box", GUILayout.Width(190));
            GUILayout.Label($"<b>{Localization.Get("LOOT_HEADER_WHITE")}</b>");

            List<string> currentList = ConfigManager.GetActiveList();

            _scrollWhite = GUILayout.BeginScrollView(_scrollWhite, GUILayout.Height(280));

            // Kopia listy do bezpiecznej modyfikacji
            var listCopy = new List<string>(currentList);
            foreach (var item in listCopy)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    currentList.Remove(item);
                    ConfigManager.Save();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawAllItemsList()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_HEADER_ALL")}</b>");

            GUILayout.BeginHorizontal();
            _searchFilter = GUILayout.TextField(_searchFilter);
            if (GUILayout.Button("R", GUILayout.Width(25))) RefreshAllItems();
            GUILayout.EndHorizontal();

            _scrollAll = GUILayout.BeginScrollView(_scrollAll, GUILayout.Height(280));

            if (_allItemsCache.Count == 0)
            {
                if (GUILayout.Button(Localization.Get("LOOT_BTN_REFRESH"))) RefreshAllItems();
            }
            else
            {
                List<string> currentList = ConfigManager.GetActiveList();

                foreach (var item in _allItemsCache)
                {
                    if (!string.IsNullOrEmpty(_searchFilter) && item.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    // Jeśli item już jest na aktywnej liście, nie pokazuj przycisku dodania (lub wyszarz)
                    bool alreadyAdded = currentList.Contains(item);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(item);

                    if (!alreadyAdded)
                    {
                        if (GUILayout.Button("+", GUILayout.Width(25)))
                        {
                            currentList.Add(item);
                            ConfigManager.Save();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        // --- POMOCNICY (BEZ ZMIAN) ---

        private Array GetDataSlots(global::WTUIContainer ui)
        {
            try
            {
                var f = ui.GetType().GetField("slots", BindingFlags.Public | BindingFlags.Instance);
                return (f != null) ? f.GetValue(ui) as Array : null;
            }
            catch { return null; }
        }

        private string GetItemNameFromData(Array dataSlots, int index)
        {
            try
            {
                if (index < 0 || index >= dataSlots.Length) return null;
                object slotObj = dataSlots.GetValue(index);
                if (slotObj == null) return null;

                var fAmount = slotObj.GetType().GetField("amount");
                if (fAmount != null)
                {
                    int amount = (int)fAmount.GetValue(slotObj);
                    if (amount <= 0) return null;
                }

                var fItem = slotObj.GetType().GetField("item");
                if (fItem == null) return null;
                object itemObj = fItem.GetValue(slotObj);

                var pData = itemObj.GetType().GetProperty("data");
                if (pData == null) return null;

                object scriptableItem = pData.GetValue(itemObj, null);
                if (scriptableItem == null) return null;

                var pName = scriptableItem.GetType().GetProperty("name");
                if (pName != null) return pName.GetValue(scriptableItem, null) as string;

                return null;
            }
            catch { return null; }
        }

        private bool IsPanelActive(global::WTUIContainer ui)
        {
            try
            {
                var fPanel = ui.GetType().GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fPanel != null)
                {
                    var panelObj = fPanel.GetValue(ui) as GameObject;
                    return panelObj != null && panelObj.activeSelf;
                }
            }
            catch { }
            return false;
        }

        private Transform RecursiveFindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
                Transform found = RecursiveFindChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        private void RefreshAllItems()
        {
            _allItemsCache.Clear();
            _status = "Skanowanie...";
            try
            {
                var scriptables = Resources.FindObjectsOfTypeAll<global::WTScriptableItem>();
                foreach (var s in scriptables) if (s != null && !string.IsNullOrEmpty(s.name) && !_allItemsCache.Contains(s.name)) _allItemsCache.Add(s.name);

                if (global::Player.localPlayer != null)
                {
                    var invField = global::Player.localPlayer.GetType().GetField("inventory");
                    if (invField != null)
                    {
                        var invList = invField.GetValue(global::Player.localPlayer) as IEnumerable;
                        if (invList != null)
                        {
                            foreach (var slotObj in invList)
                            {
                                try
                                {
                                    var fItem = slotObj.GetType().GetField("item");
                                    if (fItem == null) continue;
                                    var itemObj = fItem.GetValue(slotObj);
                                    var pData = itemObj.GetType().GetProperty("data");
                                    if (pData == null) continue;
                                    var data = pData.GetValue(itemObj, null);
                                    if (data == null) continue;
                                    var pName = data.GetType().GetProperty("name");
                                    if (pName != null)
                                    {
                                        string n = pName.GetValue(data, null) as string;
                                        if (!string.IsNullOrEmpty(n) && !_allItemsCache.Contains(n)) _allItemsCache.Add(n);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                _allItemsCache.Sort();
                _status = $"Gotowe ({_allItemsCache.Count})";
            }
            catch (Exception ex)
            {
                _status = "Błąd listy";
                Debug.LogError(ex.Message);
            }
        }
    }
}