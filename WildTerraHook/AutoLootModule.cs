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
        public float Delay = 0.15f;

        // --- DANE ---
        private List<string> _allItemsCache = new List<string>();
        private string _searchFilter = "";
        private Vector2 _scrollWhite;
        private Vector2 _scrollAll;
        private Vector2 _scrollDebug;

        private float _lootTimer = 0f;
        private string _status = "Idle";
        private List<string> _debugDetectedItems = new List<string>();

        // --- CACHE ---
        private MethodInfo _cmdGetItemMethod;
        private bool _reflectionInit = false;

        // --- UPDATE ---
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Sprawdź instancję
            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            // 2. Sprawdź widoczność panelu (czy okno jest otwarte)
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

                // 1. Pobierz dane slotów (tablica ItemSlot[])
                // Używamy Reflection, aby dostać się do pola 'slots'
                var dataSlots = GetDataSlots(ui);
                if (dataSlots == null)
                {
                    _status = "Błąd: Brak danych slotów";
                    return;
                }

                // 2. Znajdź aktywne przyciski slotów w UI (WTUIContainerSlot)
                // Szukamy w dzieciach panelu, żeby nie złapać slotów z innych okien
                var panel = GetPanel(ui);
                if (panel == null) return;

                var uiSlots = panel.GetComponentsInChildren<global::WTUIContainerSlot>(false); // false = tylko aktywne

                bool lootedSomething = false;

                foreach (var slotComp in uiSlots)
                {
                    // Odczytaj ID slotu z nazwy (mechanika gry: dragAndDropable.name = index)
                    if (slotComp.dragAndDropable == null) continue;

                    int realIndex;
                    if (!int.TryParse(slotComp.dragAndDropable.name, out realIndex)) continue;

                    // Pobierz nazwę przedmiotu z DANYCH (ItemSlot)
                    string itemName = GetItemNameFromData(dataSlots, realIndex);

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        if (DebugMode) _debugDetectedItems.Add($"[{realIndex}] {itemName}");

                        if (Enabled && !lootedSomething && ConfigManager.AutoLootList.Contains(itemName))
                        {
                            // PRÓBA 1: Kliknij przycisk
                            if (slotComp.button != null && slotComp.button.interactable)
                            {
                                _status = $"Loot (Btn): {itemName}";
                                slotComp.button.onClick.Invoke();
                                lootedSomething = true;
                            }
                            // PRÓBA 2: Wyślij komendę (jeśli przycisk nie działa)
                            else
                            {
                                _status = $"Loot (Cmd): {itemName}";
                                SendLootCommand(realIndex);
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
                Debug.LogError("[AutoLoot] " + ex.ToString());
            }
        }

        // --- REFLECTION FIX (Kluczowa poprawka) ---

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

                // 1. ItemSlot (Struct)
                object slotObj = dataSlots.GetValue(index);
                if (slotObj == null) return null;

                // Sprawdź ilość (amount)
                var fAmount = slotObj.GetType().GetField("amount");
                if (fAmount != null)
                {
                    int amount = (int)fAmount.GetValue(slotObj);
                    if (amount <= 0) return null; // Pusty slot
                }

                // 2. Item (Field w ItemSlot)
                var fItem = slotObj.GetType().GetField("item");
                if (fItem == null) return null;
                object itemObj = fItem.GetValue(slotObj);

                // 3. Data (PROPERTY w Item, to był błąd wcześniej!)
                // Item ma właściwość 'data' zwracającą ScriptableItem
                var pData = itemObj.GetType().GetProperty("data");
                if (pData == null) return null;

                object scriptableItem = pData.GetValue(itemObj, null);
                if (scriptableItem == null) return null;

                // 4. Name (Property w ScriptableObject)
                var pName = scriptableItem.GetType().GetProperty("name");
                if (pName != null) return pName.GetValue(scriptableItem, null) as string;

                return null;
            }
            catch (Exception) { return null; }
        }

        private void SendLootCommand(int index)
        {
            try
            {
                var player = global::Player.localPlayer;
                if (!_reflectionInit)
                {
                    // Szukamy: CmdGetFromContainerToInventory(int)
                    _cmdGetItemMethod = player.GetType().GetMethod("CmdGetFromContainerToInventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _reflectionInit = true;
                }

                if (_cmdGetItemMethod != null)
                {
                    _cmdGetItemMethod.Invoke(player, new object[] { index });
                }
            }
            catch { }
        }

        private GameObject GetPanel(global::WTUIContainer ui)
        {
            try
            {
                var fPanel = ui.GetType().GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fPanel != null) return fPanel.GetValue(ui) as GameObject;
            }
            catch { }
            return null;
        }

        private bool IsPanelActive(global::WTUIContainer ui)
        {
            var panel = GetPanel(ui);
            return panel != null && panel.activeSelf;
        }

        // --- GUI ---

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_TITLE")}</b>");

            Enabled = GUILayout.Toggle(Enabled, Localization.Get("LOOT_ENABLE"));
            DebugMode = GUILayout.Toggle(DebugMode, "Tryb Debugowania (Pokaż Itemki)");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Localization.Get("LOOT_DELAY")}: {Delay:F2}s", GUILayout.Width(100));
            Delay = GUILayout.HorizontalSlider(Delay, 0.05f, 1.0f);
            GUILayout.EndHorizontal();

            GUILayout.Label($"{Localization.Get("LOOT_STATUS")}: {_status}");

            if (DebugMode)
            {
                GUILayout.Label("<b>--- WYKRYTE W OKNIE ---</b>");
                _scrollDebug = GUILayout.BeginScrollView(_scrollDebug, "box", GUILayout.Height(100));
                if (_debugDetectedItems.Count > 0)
                {
                    foreach (var s in _debugDetectedItems) GUILayout.Label(s);
                }
                else
                {
                    GUILayout.Label("(Otwórz kontener aby zobaczyć)");
                }
                GUILayout.EndScrollView();
                GUILayout.Space(10);
            }

            GUILayout.BeginHorizontal();

            // Whitelista
            GUILayout.BeginVertical("box", GUILayout.Width(180));
            GUILayout.Label($"<b>{Localization.Get("LOOT_HEADER_WHITE")}</b>");
            _scrollWhite = GUILayout.BeginScrollView(_scrollWhite, GUILayout.Height(200));
            var listCopy = new List<string>(ConfigManager.AutoLootList);
            foreach (var item in listCopy)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    ConfigManager.AutoLootList.Remove(item);
                    ConfigManager.Save();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Lista Wszystkich
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_HEADER_ALL")}</b>");

            GUILayout.BeginHorizontal();
            _searchFilter = GUILayout.TextField(_searchFilter);
            if (GUILayout.Button(Localization.Get("LOOT_BTN_REFRESH"), GUILayout.Width(60))) RefreshAllItems();
            GUILayout.EndHorizontal();

            _scrollAll = GUILayout.BeginScrollView(_scrollAll, GUILayout.Height(200));
            if (_allItemsCache.Count == 0)
            {
                if (GUILayout.Button(Localization.Get("LOOT_BTN_REFRESH"))) RefreshAllItems();
            }
            else
            {
                foreach (var item in _allItemsCache)
                {
                    if (!string.IsNullOrEmpty(_searchFilter) && item.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (ConfigManager.AutoLootList.Contains(item)) continue;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(item);
                    if (GUILayout.Button(Localization.Get("LOOT_BTN_ADD"), GUILayout.Width(40)))
                    {
                        ConfigManager.AutoLootList.Add(item);
                        ConfigManager.Save();
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void RefreshAllItems()
        {
            _allItemsCache.Clear();
            _status = "Skanowanie bazy...";
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