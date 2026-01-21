using UnityEngine;
using UnityEngine.EventSystems; // Wymagane do symulacji kliknięć
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;

namespace WildTerraHook
{
    public class AutoLootModule
    {
        // --- USTAWIENIA ---
        public bool Enabled = false;
        public float Delay = 0.3f; // Zmniejszyłem lekko domyślny czas dla szybszego zbierania

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

            // Sprawdź czy okno kontenera jest otwarte
            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null || !IsContainerVisible(containerUI))
            {
                if (_status.StartsWith("Biorę") || _status.StartsWith("Kliknięto")) _status = "Czekam na okno...";
                return;
            }

            if (Time.time < _lootTimer) return;

            // Logika Lootowania
            if (TryLootItem(containerUI))
            {
                _lootTimer = Time.time + Delay;
            }
        }

        private bool TryLootItem(global::WTUIContainer ui)
        {
            try
            {
                // Szukamy kolekcji slotów w UI przez Reflection
                var fields = ui.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var list = field.GetValue(ui) as System.Collections.IList;
                        if (list != null && list.Count > 0)
                        {
                            object firstItem = list[0];
                            // Sprawdzamy czy to lista slotów
                            if (firstItem.GetType().Name.Contains("Slot"))
                            {
                                foreach (object slot in list)
                                {
                                    if (ProcessSlot(slot)) return true; // Przeniesiono jeden przedmiot, czekamy (Delay)
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _status = "Błąd: " + ex.Message; }

            return false;
        }

        private bool ProcessSlot(object slotObj)
        {
            // Próba pobrania pola 'item' lub 'data' ze slotu
            var itemField = slotObj.GetType().GetField("item", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (itemField == null) itemField = slotObj.GetType().GetField("data", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            if (itemField != null)
            {
                var item = itemField.GetValue(slotObj);
                if (item != null)
                {
                    string itemName = GetItemName(item);

                    // Sprawdzamy czy przedmiot jest na Whiteliście
                    if (!string.IsNullOrEmpty(itemName) && ConfigManager.AutoLootList.Contains(itemName))
                    {
                        _status = $"Biorę: {itemName}";

                        // --- METODA 1: Symulacja Prawego Kliknięcia (PointerClick) ---
                        // To jest najbardziej uniwersalna metoda w Unity UI.
                        // Prawy przycisk myszy w Wild Terra zazwyczaj przenosi item.
                        try
                        {
                            if (EventSystem.current != null)
                            {
                                var pointerData = new PointerEventData(EventSystem.current);
                                pointerData.button = PointerEventData.InputButton.Right; // Symulujemy PRAWY przycisk

                                var clickMethod = slotObj.GetType().GetMethod("OnPointerClick");
                                if (clickMethod != null)
                                {
                                    clickMethod.Invoke(slotObj, new object[] { pointerData });
                                    _status = $"Kliknięto (Prawy): {itemName}";
                                    return true;
                                }
                            }
                        }
                        catch { }

                        // --- METODA 2: Bezpośrednie wywołanie OnClick (jeśli PointerClick zawiedzie) ---
                        var methods = slotObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var m in methods)
                        {
                            if (m.Name.Equals("OnClick") || m.Name.Equals("OnQuickAction") || m.Name.Equals("OnDoubleClick"))
                            {
                                m.Invoke(slotObj, null);
                                _status = $"Metoda {m.Name}: {itemName}";
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private string GetItemName(object itemObj)
        {
            try
            {
                Type t = itemObj.GetType();

                var fName = t.GetField("name");
                if (fName != null) return fName.GetValue(itemObj) as string;

                var pName = t.GetProperty("Name");
                if (pName != null) return pName.GetValue(itemObj, null) as string;

                var fTemplate = t.GetField("template");
                if (fTemplate != null)
                {
                    var tmpl = fTemplate.GetValue(itemObj);
                    if (tmpl != null) return GetItemName(tmpl);
                }

                var fEnt = t.GetField("EntityName");
                if (fEnt != null) return fEnt.GetValue(itemObj) as string;
            }
            catch { }
            return null;
        }

        private bool IsContainerVisible(global::WTUIContainer ui)
        {
            try
            {
                var method = ui.GetType().GetMethod("IsShow");
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

        // --- GUI ---

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_TITLE")}</b>");

            Enabled = GUILayout.Toggle(Enabled, Localization.Get("LOOT_ENABLE"));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Localization.Get("LOOT_DELAY")}: {Delay:F2}s", GUILayout.Width(100));
            Delay = GUILayout.HorizontalSlider(Delay, 0.1f, 2.0f);
            GUILayout.EndHorizontal();

            GUILayout.Label($"{Localization.Get("LOOT_STATUS")}: {_status}");
            GUILayout.Space(10);

            // Dwie kolumny: Whitelista | Wszystkie Itemki
            GUILayout.BeginHorizontal();

            // KOLUMNA 1: WHITELISTA
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

            // KOLUMNA 2: WYSZUKIWARKA I LISTA
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_HEADER_ALL")}</b>");

            GUILayout.BeginHorizontal();
            _searchFilter = GUILayout.TextField(_searchFilter);
            if (GUILayout.Button(Localization.Get("LOOT_BTN_REFRESH"), GUILayout.Width(60)))
            {
                RefreshAllItems();
            }
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
                    if (!string.IsNullOrEmpty(_searchFilter) &&
                        item.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

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
            int count = 0;

            try
            {
                // Skanowanie WTScriptableItem
                var scriptables = Resources.FindObjectsOfTypeAll<global::WTScriptableItem>();
                foreach (var s in scriptables)
                {
                    if (s != null && !string.IsNullOrEmpty(s.name))
                    {
                        if (!_allItemsCache.Contains(s.name)) _allItemsCache.Add(s.name);
                    }
                }

                // Skanowanie Inventory
                if (global::WTUIInventory.instance != null)
                {
                    var inv = global::WTUIInventory.instance;
                    var fields = inv.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var f in fields)
                    {
                        if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            var list = f.GetValue(inv) as System.Collections.IList;
                            if (list != null)
                            {
                                foreach (var slot in list)
                                {
                                    var iField = slot.GetType().GetField("item");
                                    if (iField != null)
                                    {
                                        var itm = iField.GetValue(slot);
                                        if (itm != null)
                                        {
                                            string n = GetItemName(itm);
                                            if (!string.IsNullOrEmpty(n) && !_allItemsCache.Contains(n)) _allItemsCache.Add(n);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                count = _allItemsCache.Count;
                _allItemsCache.Sort();
                _status = $"Znaleziono {count} przed.";
            }
            catch (Exception ex)
            {
                _status = "Błąd skanowania";
                Debug.LogError("[AutoLoot] " + ex.Message);
            }
        }
    }
}