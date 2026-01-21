using UnityEngine;
using UnityEngine.EventSystems; // Niezbędne do symulacji kliknięć
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
        public float Delay = 0.2f; // Szybkie zbieranie

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
                if (_status.StartsWith("Biorę") || _status.StartsWith("Klik")) _status = "Czekam na okno...";
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
                                    if (ProcessSlot(slot)) return true; // Przeniesiono jeden przedmiot -> czekamy (Delay)
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
            // 1. Sprawdź co jest w slocie
            var itemField = slotObj.GetType().GetField("item", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (itemField == null) itemField = slotObj.GetType().GetField("data", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            if (itemField != null)
            {
                var item = itemField.GetValue(slotObj);
                if (item != null)
                {
                    string itemName = GetItemName(item);

                    // 2. Jeśli przedmiot jest na liście -> KLIKNIJ
                    if (!string.IsNullOrEmpty(itemName) && ConfigManager.AutoLootList.Contains(itemName))
                    {
                        _status = $"Biorę: {itemName}";

                        // Próbujemy kliknąć slot (jako MonoBehaviour/GameObject)
                        MonoBehaviour mb = slotObj as MonoBehaviour;
                        if (mb != null)
                        {
                            if (SimulateRightClick(mb.gameObject)) return true;
                        }
                    }
                }
            }
            return false;
        }

        // --- NAJWAŻNIEJSZA METODA: Symulacja Prawego Kliknięcia ---
        private bool SimulateRightClick(GameObject target)
        {
            if (EventSystem.current == null) return false;

            // Tworzymy dane zdarzenia (Prawy Przycisk = Quick Move)
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right,
                position = Input.mousePosition // Czasami gra sprawdza pozycję
            };

            // 1. Próba bezpośrednia na obiekcie
            if (ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler))
            {
                _status = "Klik: Direct";
                return true;
            }

            // 2. Próba na dzieciach (często slot ma w środku 'Icon' lub 'Button', który odbiera kliknięcia)
            foreach (Transform child in target.transform)
            {
                if (ExecuteEvents.Execute(child.gameObject, pointerData, ExecuteEvents.pointerClickHandler))
                {
                    _status = "Klik: Child";
                    return true;
                }
                // Jeszcze głębiej (np. Slot -> Background -> Icon)
                foreach (Transform grandChild in child)
                {
                    if (ExecuteEvents.Execute(grandChild.gameObject, pointerData, ExecuteEvents.pointerClickHandler))
                    {
                        _status = "Klik: GrandChild";
                        return true;
                    }
                }
            }

            // 3. Fallback: PointerDown + PointerUp (jeśli gra nie obsługuje Click, a np. Drag/Down)
            if (ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler))
            {
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
                _status = "Klik: Down/Up";
                return true;
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

                // Częsty przypadek: Item -> Template -> Name
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

            // KOLUMNA 2: LISTA PRZEDMIOTÓW
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
                // 1. Skanowanie ScriptableItems (baza danych gry)
                var scriptables = Resources.FindObjectsOfTypeAll<global::WTScriptableItem>();
                foreach (var s in scriptables)
                {
                    if (s != null && !string.IsNullOrEmpty(s.name))
                    {
                        if (!_allItemsCache.Contains(s.name)) _allItemsCache.Add(s.name);
                    }
                }

                // 2. Skanowanie Ekwipunku (pewniak)
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