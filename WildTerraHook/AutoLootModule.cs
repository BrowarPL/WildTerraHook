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
        public bool DebugMode = false; // NOWOŚĆ: Pokaże co widzi bot
        public float Delay = 0.2f;

        // --- DANE ---
        private List<string> _allItemsCache = new List<string>();
        private string _searchFilter = "";
        private Vector2 _scrollWhite;
        private Vector2 _scrollAll;
        private Vector2 _scrollDebug;

        private float _lootTimer = 0f;
        private string _status = "Idle";
        private List<string> _debugDetectedItems = new List<string>(); // Lista do podglądu

        // --- UPDATE ---
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Sprawdź instancję
            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            // 2. Sprawdź widoczność panelu
            if (!IsPanelActive(containerUI))
            {
                if (_status.Contains("Loot") || _status.Contains("Widzę")) _status = "Czekam na okno...";
                _debugDetectedItems.Clear();
                return;
            }

            // Odświeżaj debug info co klatkę (jeśli włączone) lub lootuj co Delay
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

                // 1. Znajdź kontener ze slotami (Content)
                // Ścieżka w hierarchii: WTContainerPanel -> Scroll View -> ContentBackground -> Viewport -> Content
                // Używamy bezpiecznego szukania rekurencyjnego "Content"
                Transform panel = ui.transform.Find("WTContainerPanel");
                if (panel == null || !panel.gameObject.activeSelf) return;

                Transform content = RecursiveFindChild(panel, "Content");
                if (content == null)
                {
                    _status = "Błąd: Nie znaleziono Content";
                    return;
                }

                // 2. Pobierz "prawdziwe" dane (tablica ItemSlot[] slots) z UI
                var dataSlots = GetDataSlots(ui); // To jest tablica danych
                if (dataSlots == null) return;

                bool lootedSomething = false;

                // 3. Iteruj po wizualnych slotach (dzieciach Content)
                foreach (Transform child in content)
                {
                    if (!child.gameObject.activeSelf) continue;

                    var slotComp = child.GetComponent<global::WTUIContainerSlot>();
                    if (slotComp == null) continue;

                    // Odczytaj ID slotu (gra zapisuje indeks tablicy w nazwie obiektu drag&drop)
                    if (slotComp.dragAndDropable == null) continue;

                    int realIndex;
                    if (!int.TryParse(slotComp.dragAndDropable.name, out realIndex)) continue;

                    // Pobierz dane przedmiotu z tablicy danych używając indeksu
                    string itemName = GetItemNameFromData(dataSlots, realIndex);

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        // Dodaj do podglądu Debug
                        if (DebugMode) _debugDetectedItems.Add($"[{realIndex}] {itemName}");

                        // LOGIKA LOOTOWANIA
                        if (Enabled && !lootedSomething && ConfigManager.AutoLootList.Contains(itemName))
                        {
                            if (slotComp.button != null && slotComp.button.interactable)
                            {
                                _status = $"Loot: {itemName}";
                                slotComp.button.onClick.Invoke();
                                lootedSomething = true; // Zbieramy jeden, przerywamy pętlę
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

        // --- POMOCNICY DANYCH ---

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
                object slotObj = dataSlots.GetValue(index); // ItemSlot
                if (slotObj == null) return null;

                // ItemSlot -> Item item
                var fItem = slotObj.GetType().GetField("item");
                if (fItem == null) return null;
                object itemObj = fItem.GetValue(slotObj); // Item
                if (itemObj == null) return null;

                // Item -> ItemTemplate data -> string name
                var fData = itemObj.GetType().GetField("data");
                if (fData == null) return null;
                object dataObj = fData.GetValue(itemObj); // ItemTemplate
                if (dataObj == null) return null;

                // Pobierz nazwę (name)
                var fName = dataObj.GetType().GetField("name");
                if (fName != null) return fName.GetValue(dataObj) as string;

                var pName = dataObj.GetType().GetProperty("name");
                if (pName != null) return pName.GetValue(dataObj, null) as string;
            }
            catch { }
            return null;
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

            // SEKCJA DEBUGOWANIA (WYKRYTE PRZEDMIOTY)
            if (DebugMode)
            {
                GUILayout.Label("<b>--- WYKRYTE W OKNIE ---</b>");
                _scrollDebug = GUILayout.BeginScrollView(_scrollDebug, GUILayout.Height(100), "box");
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

                // Dodajemy też z ekwipunku gracza, żeby mieć pewność
                if (global::Player.localPlayer != null)
                {
                    var invField = global::Player.localPlayer.GetType().GetField("inventory");
                    if (invField != null)
                    {
                        var invList = invField.GetValue(global::Player.localPlayer) as IEnumerable;
                        if (invList != null)
                        {
                            // Używamy prostego parsowania, bo nie chcemy tu pisać całej logiki od nowa
                            // Zakładam, że WTScriptableItem pokrywa 99%
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