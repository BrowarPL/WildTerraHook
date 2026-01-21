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
        public float Delay = 0.2f;

        // --- DANE ---
        private List<string> _allItemsCache = new List<string>();
        private string _searchFilter = "";
        private Vector2 _scrollWhite;
        private Vector2 _scrollAll;
        private float _lootTimer = 0f;
        private string _status = "Idle";

        // --- UPDATE ---
        public void Update()
        {
            if (!Enabled) return;
            if (global::Player.localPlayer == null) return;

            // Sprawdzamy, czy kontener jest widoczny
            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null || !IsContainerVisible(containerUI))
            {
                if (_status.StartsWith("Loot")) _status = "Czekam na okno...";
                return;
            }

            if (Time.time < _lootTimer) return;

            // Uruchamiamy logikę "Button Invoke"
            if (ProcessVisibleSlots())
            {
                _lootTimer = Time.time + Delay;
            }
        }

        private bool ProcessVisibleSlots()
        {
            try
            {
                // Znajdź wszystkie aktywne sloty kontenera na scenie
                // WTUIContainerSlot to klasa obsługująca wyświetlanie itemka w oknie lootowania
                var slots = UnityEngine.Object.FindObjectsOfType<global::WTUIContainerSlot>();

                foreach (var slot in slots)
                {
                    // Ignoruj nieaktywne (np. z puli, niewidoczne)
                    if (slot == null || !slot.gameObject.activeInHierarchy) continue;

                    // Pobierz nazwę przedmiotu ze slotu
                    string itemName = GetItemNameFromSlot(slot);

                    if (!string.IsNullOrEmpty(itemName) && ConfigManager.AutoLootList.Contains(itemName))
                    {
                        // Sprawdź czy slot ma przycisk
                        if (slot.button != null && slot.button.interactable)
                        {
                            _status = $"Loot: {itemName}";

                            // KLUCZOWY MOMENT: Wymuszamy kliknięcie przycisku
                            slot.button.onClick.Invoke();

                            return true; // Jeden item na cykl (Delay)
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _status = "Błąd: " + ex.Message;
            }

            return false;
        }

        private string GetItemNameFromSlot(global::WTUIContainerSlot slot)
        {
            try
            {
                // W Wild Terra 2, dane przedmiotu często siedzą w Tooltipie podpiętym pod slot
                // lub w polu, które nie jest publicznie wystawione w prosty sposób.
                // Używamy Reflection, by wyciągnąć 'itemSlot' -> 'item' -> 'data' -> 'name'

                object itemSlotObj = null;

                // Metoda A: Sprawdź pole 'tooltip' (WTUIShowToolTip), które ma 'itemSlot'
                if (slot.tooltip != null)
                {
                    var fSlot = slot.tooltip.GetType().GetField("itemSlot", BindingFlags.Public | BindingFlags.Instance);
                    if (fSlot != null) itemSlotObj = fSlot.GetValue(slot.tooltip);
                }

                // Metoda B: Jeśli A zawiodło, spróbuj znaleźć pole w samym slocie (np. przez dziedziczenie)
                if (itemSlotObj == null)
                {
                    // Szukamy czegokolwiek co wygląda na ItemSlot w polach slotu
                    var fields = slot.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var f in fields)
                    {
                        if (f.Name.ToLower().Contains("item") || f.Name.ToLower().Contains("slot"))
                        {
                            var val = f.GetValue(slot);
                            if (val != null && val.GetType().Name.Contains("ItemSlot"))
                            {
                                itemSlotObj = val;
                                break;
                            }
                        }
                    }
                }

                if (itemSlotObj != null)
                {
                    // Mamy obiekt ItemSlot, teraz kopiemy głębiej: ItemSlot -> Item -> ItemTemplate -> Name

                    // 1. Sprawdź ilość (Amount), żeby nie klikać pustych
                    var fAmount = itemSlotObj.GetType().GetField("amount");
                    if (fAmount != null)
                    {
                        int amount = (int)fAmount.GetValue(itemSlotObj);
                        if (amount <= 0) return null;
                    }

                    // 2. Pobierz Item
                    var fItem = itemSlotObj.GetType().GetField("item");
                    if (fItem != null)
                    {
                        var itemVal = fItem.GetValue(itemSlotObj);
                        if (itemVal != null)
                        {
                            // 3. Pobierz Data/Template
                            var fData = itemVal.GetType().GetField("data");
                            if (fData != null)
                            {
                                var dataVal = fData.GetValue(itemVal);
                                if (dataVal != null)
                                {
                                    // 4. Pobierz Name
                                    var fName = dataVal.GetType().GetField("name"); // field name
                                    if (fName != null) return fName.GetValue(dataVal) as string;

                                    var pName = dataVal.GetType().GetProperty("name"); // property Name (czasem z dużej)
                                    if (pName != null) return pName.GetValue(dataVal, null) as string;

                                    var pNameUpper = dataVal.GetType().GetProperty("Name");
                                    if (pNameUpper != null) return pNameUpper.GetValue(dataVal, null) as string;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private bool IsContainerVisible(global::WTUIContainer ui)
        {
            try
            {
                var method = ui.GetType().GetMethod("IsOpen");
                if (method != null) return (bool)method.Invoke(ui, null);

                var panelField = ui.GetType().GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (panelField != null)
                {
                    GameObject panel = panelField.GetValue(ui) as GameObject;
                    return panel != null && panel.activeSelf;
                }
            }
            catch { }
            return true;
        }

        // --- GUI (BEZ ZMIAN) ---

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_TITLE")} (Button Mode)</b>");

            Enabled = GUILayout.Toggle(Enabled, Localization.Get("LOOT_ENABLE"));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Localization.Get("LOOT_DELAY")}: {Delay:F2}s", GUILayout.Width(100));
            Delay = GUILayout.HorizontalSlider(Delay, 0.05f, 1.0f);
            GUILayout.EndHorizontal();

            GUILayout.Label($"{Localization.Get("LOOT_STATUS")}: {_status}");
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            // Whitelista
            GUILayout.BeginVertical("box", GUILayout.Width(180));
            GUILayout.Label($"<b>{Localization.Get("LOOT_HEADER_WHITE")}</b>");
            _scrollWhite = GUILayout.BeginScrollView(_scrollWhite, GUILayout.Height(250));
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

            _scrollAll = GUILayout.BeginScrollView(_scrollAll, GUILayout.Height(250));
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
                            foreach (var slot in invList)
                            {
                                // Używamy tej samej logiki do wyciągania nazwy co przy zbieraniu, żeby nazwy pasowały
                                // Musimy jednak odtworzyć strukturę Reflection, bo slot tutaj to czysty obiekt danych, a nie UI
                                var fItem = slot.GetType().GetField("item");
                                if (fItem != null)
                                {
                                    var itm = fItem.GetValue(slot);
                                    if (itm != null)
                                    {
                                        var fData = itm.GetType().GetField("data");
                                        if (fData != null)
                                        {
                                            var data = fData.GetValue(itm);
                                            if (data != null)
                                            {
                                                var fName = data.GetType().GetField("name");
                                                if (fName != null)
                                                {
                                                    string n = fName.GetValue(data) as string;
                                                    if (!string.IsNullOrEmpty(n) && !_allItemsCache.Contains(n)) _allItemsCache.Add(n);
                                                }
                                            }
                                        }
                                    }
                                }
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