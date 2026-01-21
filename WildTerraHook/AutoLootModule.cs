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

            // 1. Sprawdź instancję okna kontenera
            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            // 2. Sprawdź czy okno jest fizycznie otwarte (GameObject.activeSelf)
            // W Wild Terra 2 skrypt jest na obiekcie, ale ma dziecko "WTContainerPanel"
            // Używamy bezpiecznej metody sprawdzenia
            if (!IsPanelActive(containerUI))
            {
                if (_status.StartsWith("Loot")) _status = "Czekam na okno...";
                return;
            }

            if (Time.time < _lootTimer) return;

            // 3. Uruchom logikę "Hybrid UI Walker"
            if (ProcessActiveUiSlots(containerUI))
            {
                _lootTimer = Time.time + Delay;
            }
        }

        private bool ProcessActiveUiSlots(global::WTUIContainer ui)
        {
            try
            {
                // Znajdujemy transform 'Content', który trzyma sloty.
                // Ścieżka z kodu gry: "WTContainerPanel/Scroll View/ContentBackground/Viewport/Content"
                // Ale bezpieczniej poszukać komponentu GridLayoutGroup lub po prostu iterować w dół.

                Transform panel = ui.transform.Find("WTContainerPanel");
                if (panel == null || !panel.gameObject.activeSelf) return false;

                // Szukamy kontenera na sloty (Content)
                // Najczęściej jest głęboko w hierarchii scroll view
                Transform content = RecursiveFindChild(panel, "Content");
                if (content == null)
                {
                    _status = "Błąd: Nie znaleziono 'Content'";
                    return false;
                }

                // Iterujemy po wszystkich dzieciach (slotach)
                foreach (Transform child in content)
                {
                    if (!child.gameObject.activeSelf) continue;

                    // Pobieramy komponent slotu
                    var slotComp = child.GetComponent<global::WTUIContainerSlot>();
                    if (slotComp == null) continue;

                    // KLUCZOWE: Odczytujemy ID slotu z dragAndDropable.name (tak robi gra w SetSlot)
                    if (slotComp.dragAndDropable == null) continue;

                    int realIndex;
                    if (!int.TryParse(slotComp.dragAndDropable.name, out realIndex)) continue;

                    // Teraz sprawdzamy dane w tablicy 'slots' używając prawdziwego indeksu
                    string itemName = GetItemNameFromDataArray(ui, realIndex);

                    if (!string.IsNullOrEmpty(itemName) && ConfigManager.AutoLootList.Contains(itemName))
                    {
                        // Sprawdź czy przycisk działa
                        if (slotComp.button != null && slotComp.button.interactable)
                        {
                            _status = $"Loot: {itemName}";

                            // INVOKE! To wywoła delegate przypisany w SetSlot:
                            // Player.localPlayer.CmdGetFromContainerToInventory(index);
                            slotComp.button.onClick.Invoke();

                            return true; // Jeden na cykl
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

        // --- POMOCNICY REFLECTION ---

        private string GetItemNameFromDataArray(global::WTUIContainer ui, int index)
        {
            try
            {
                // Dostęp do tablicy: public ItemSlot[] slots;
                var fSlots = ui.GetType().GetField("slots", BindingFlags.Public | BindingFlags.Instance);
                if (fSlots != null)
                {
                    var array = fSlots.GetValue(ui) as Array;
                    if (array != null && index >= 0 && index < array.Length)
                    {
                        object itemSlotObj = array.GetValue(index);
                        if (itemSlotObj == null) return null;

                        // ItemSlot -> Item item
                        var fItem = itemSlotObj.GetType().GetField("item");
                        if (fItem != null)
                        {
                            var itemVal = fItem.GetValue(itemSlotObj);
                            if (itemVal != null)
                            {
                                // Item -> ItemTemplate data -> string name
                                var fData = itemVal.GetType().GetField("data");
                                if (fData != null)
                                {
                                    var dataVal = fData.GetValue(itemVal);
                                    if (dataVal != null)
                                    {
                                        var fName = dataVal.GetType().GetField("name");
                                        if (fName != null) return fName.GetValue(dataVal) as string;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private bool IsPanelActive(global::WTUIContainer ui)
        {
            try
            {
                // WTUIContainer ma metodę IsOpen(), ale możemy też sprawdzić panel ręcznie
                // private GameObject panel;
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

        // --- GUI --- (STANDARDOWE)

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_TITLE")} (Hybrid)</b>");

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
                                // Używamy Reflection do wyciągnięcia nazwy z item slotu w ekwipunku
                                try
                                {
                                    var fItem = slot.GetType().GetField("item");
                                    if (fItem != null)
                                    {
                                        var itm = fItem.GetValue(slot);
                                        if (itm != null)
                                        {
                                            var fData = itm.GetType().GetField("data");
                                            if (fData != null)
                                            {
                                                var dat = fData.GetValue(itm);
                                                if (dat != null)
                                                {
                                                    var fName = dat.GetType().GetField("name");
                                                    if (fName != null)
                                                    {
                                                        string n = fName.GetValue(dat) as string;
                                                        if (!string.IsNullOrEmpty(n) && !_allItemsCache.Contains(n)) _allItemsCache.Add(n);
                                                    }
                                                }
                                            }
                                        }
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