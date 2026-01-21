using UnityEngine;
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
        public float Delay = 0.1f; // Bardzo szybkie zbieranie (100ms)

        // --- DANE ---
        private List<string> _allItemsCache = new List<string>();
        private string _searchFilter = "";
        private Vector2 _scrollWhite;
        private Vector2 _scrollAll;
        private float _lootTimer = 0f;
        private string _status = "Idle";

        // --- CACHE METOD (Reflection) ---
        private MethodInfo _cmdGetItemMethod;
        private bool _reflectionInit = false;

        public void Update()
        {
            if (!Enabled) return;
            if (global::Player.localPlayer == null) return;

            // 1. Sprawdź czy kontener (okno) jest otwarte w danych gry
            // WTUIContainer trzyma dane o otwartym kontenerze w 'instance'
            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            // Sprawdzamy flagę 'IsOpen' lub czy panel jest aktywny
            if (!IsContainerOpen(containerUI))
            {
                if (_status.Contains("Loot")) _status = "Czekam...";
                return;
            }

            if (Time.time < _lootTimer) return;

            // 2. Pobierz sloty bezpośrednio z logiki gry (nie z UI!)
            // WTUIContainer ma publiczne pole: public ItemSlot[] slots;
            var slots = GetSlotsFromContainer(containerUI);
            if (slots == null || slots.Length == 0) return;

            // 3. Iteruj i zbieraj
            if (ProcessSlots(slots))
            {
                _lootTimer = Time.time + Delay;
            }
        }

        private bool ProcessSlots(Array itemSlots)
        {
            // itemSlots to tablica ItemSlot[] (ale używamy Array dla bezpieczenstwa Reflection)
            for (int i = 0; i < itemSlots.Length; i++)
            {
                object slot = itemSlots.GetValue(i);
                if (slot == null) continue;

                // Sprawdź czy slot jest pusty (ItemSlot ma pole 'amount')
                int amount = GetSlotAmount(slot);
                if (amount <= 0) continue;

                // Pobierz nazwę przedmiotu
                string itemName = GetItemNameFromSlot(slot);

                // Sprawdź Whitelistę
                if (!string.IsNullOrEmpty(itemName) && ConfigManager.AutoLootList.Contains(itemName))
                {
                    // ZNALEZIONO PRZEDMIOT -> WYŚLIJ KOMENDĘ
                    LootItemDirectly(i, itemName);
                    return true; // Zbieramy jeden na cykl (Delay), żeby nie floodować serwera
                }
            }
            return false;
        }

        // --- CORE: BEZPOŚREDNIA KOMENDA ---
        private void LootItemDirectly(int index, string name)
        {
            _status = $"Loot (CMD): {name}";

            try
            {
                var player = global::Player.localPlayer;

                // Szukamy metody: public void CmdGetFromContainerToInventory(int containerIndex)
                if (!_reflectionInit)
                {
                    _cmdGetItemMethod = player.GetType().GetMethod(
                        "CmdGetFromContainerToInventory",
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic
                    );
                    _reflectionInit = true;
                }

                if (_cmdGetItemMethod != null)
                {
                    // WYWOŁANIE: Player.localPlayer.CmdGetFromContainerToInventory(index)
                    _cmdGetItemMethod.Invoke(player, new object[] { index });
                }
                else
                {
                    _status = "Błąd: Nie znaleziono CmdGetFromContainerToInventory";
                }
            }
            catch (Exception ex)
            {
                _status = $"Błąd Cmd: {ex.Message}";
            }
        }

        // --- POMOCNICY DANYCH (REFLECTION) ---

        private Array GetSlotsFromContainer(global::WTUIContainer ui)
        {
            try
            {
                var field = ui.GetType().GetField("slots", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(ui) as Array;
                }
            }
            catch { }
            return null;
        }

        private bool IsContainerOpen(global::WTUIContainer ui)
        {
            try
            {
                // Sprawdzamy metodę IsOpen()
                var m = ui.GetType().GetMethod("IsOpen");
                if (m != null) return (bool)m.Invoke(ui, null);

                // Fallback: panel.activeSelf
                var f = ui.GetType().GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    var panel = f.GetValue(ui) as GameObject;
                    return panel != null && panel.activeSelf;
                }
            }
            catch { }
            return false;
        }

        private int GetSlotAmount(object itemSlot)
        {
            try
            {
                var f = itemSlot.GetType().GetField("amount");
                if (f != null) return (int)f.GetValue(itemSlot);
            }
            catch { }
            return 0;
        }

        private string GetItemNameFromSlot(object itemSlot)
        {
            try
            {
                // ItemSlot -> Item item
                var fItem = itemSlot.GetType().GetField("item");
                if (fItem != null)
                {
                    var itemVal = fItem.GetValue(itemSlot);
                    if (itemVal != null)
                    {
                        // Item -> ItemTemplate data -> string name
                        // W grze: item.data.name
                        var fData = itemVal.GetType().GetField("data");
                        if (fData != null)
                        {
                            var dataVal = fData.GetValue(itemVal);
                            if (dataVal != null)
                            {
                                var fName = dataVal.GetType().GetField("name");
                                if (fName != null) return fName.GetValue(dataVal) as string;

                                var pName = dataVal.GetType().GetProperty("name");
                                if (pName != null) return pName.GetValue(dataVal, null) as string;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }


        // --- GUI --- (Bez zmian, tylko obsługa listy)
        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_TITLE")} (Direct Mode)</b>");

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
                // Skanowanie WTScriptableItem (ItemTemplate często dziedziczy po tym lub ScriptableObject)
                // W tej grze ItemTemplate to klasa danych. Spróbujmy znaleźć assety.
                var scriptables = Resources.FindObjectsOfTypeAll<global::WTScriptableItem>();
                foreach (var s in scriptables) if (s != null && !string.IsNullOrEmpty(s.name) && !_allItemsCache.Contains(s.name)) _allItemsCache.Add(s.name);

                // Skanowanie z Inventory gracza (najpewniejsze)
                if (global::Player.localPlayer != null)
                {
                    // Używamy pola 'inventory' z klasy Player (jest to SyncList<ItemSlot>)
                    var invField = global::Player.localPlayer.GetType().GetField("inventory");
                    if (invField != null)
                    {
                        var invList = invField.GetValue(global::Player.localPlayer) as IEnumerable;
                        if (invList != null)
                        {
                            foreach (var slot in invList)
                            {
                                string n = GetItemNameFromSlot(slot); // Używamy tej samej logiki co przy zbieraniu
                                if (!string.IsNullOrEmpty(n) && !_allItemsCache.Contains(n)) _allItemsCache.Add(n);
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