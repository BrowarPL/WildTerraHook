using UnityEngine;
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
        public float Delay = 0.5f;

        // --- DANE ---
        private List<string> _allItemsCache = new List<string>();
        private string _searchFilter = "";
        private Vector2 _scrollWhite;
        private Vector2 _scrollAll;
        private float _lootTimer = 0f;
        private string _status = "Idle";

        // --- CACHE REFLECTION (dla wydajności) ---
        private MethodInfo _containerQuickMoveMethod;
        private bool _reflectionInit = false;

        public void Update()
        {
            if (!Enabled) return;
            if (global::Player.localPlayer == null) return;

            // Sprawdź czy okno kontenera jest otwarte
            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null || !IsContainerVisible(containerUI))
            {
                _status = "Oczekiwanie na okno...";
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
            // WTUIContainer ma listę slotów. Zwykle nazywa się 'slots' lub jest w 'container'
            // Musimy dostać się do listy przedmiotów w kontenerze
            try
            {
                // Próba 1: Publiczne pole 'slots' w WTUIContainer (jeśli istnieje w tej wersji)
                // Zakładam, że UI ma listę slotów typu WTUIContainerSlot lub podobne
                // Użyjmy reflection aby znaleźć kolekcję slotów w UI
                var fields = ui.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    // Szukamy Listy lub Tablicy która może zawierać sloty
                    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var list = field.GetValue(ui) as System.Collections.IList;
                        if (list != null && list.Count > 0)
                        {
                            // Sprawdzamy pierwszy element żeby zobaczyć czy to slot
                            object firstItem = list[0];
                            if (firstItem.GetType().Name.Contains("Slot"))
                            {
                                // Iterujemy po slotach
                                foreach (object slot in list)
                                {
                                    if (ProcessSlot(slot)) return true; // Znaleziono i przeniesiono jeden item
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
            // Musimy wyciągnąć item ze slotu
            // Slot zazwyczaj ma pole 'item' (typu Item) lub 'data'
            var itemField = slotObj.GetType().GetField("item", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (itemField == null) itemField = slotObj.GetType().GetField("data", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            if (itemField != null)
            {
                var item = itemField.GetValue(slotObj);
                if (item != null)
                {
                    // Sprawdzamy nazwę (pole 'name' w klasie Item/ItemTemplate)
                    // W Wild Terra item często ma 'template' lub 'data' z nazwą
                    string itemName = GetItemName(item);

                    if (!string.IsNullOrEmpty(itemName) && ConfigManager.AutoLootList.Contains(itemName))
                    {
                        _status = $"Biorę: {itemName}";

                        // Wywołaj "Quick Move" na tym slocie
                        // Metoda nazywa się zazwyczaj OnClick, OnRightClick, lub QuickMove
                        var methods = slotObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var m in methods)
                        {
                            if (m.Name.Equals("OnQuickAction") || m.Name.Equals("OnDoubleClick") || m.Name.Contains("Quick"))
                            {
                                m.Invoke(slotObj, null);
                                return true;
                            }
                        }

                        // Fallback: Spróbujmy symulować kliknięcie na komponencie Unity UI, jeśli slot nim jest
                        MonoBehaviour mb = slotObj as MonoBehaviour;
                        if (mb != null)
                        {
                            // Tu można by użyć event system, ale metoda wyżej jest pewniejsza dla logiki gry
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
                // Próba 1: Item.name
                var nameProp = itemObj.GetType().GetProperty("name");
                if (nameProp != null) return nameProp.GetValue(itemObj, null) as string;

                var nameField = itemObj.GetType().GetField("name");
                if (nameField != null) return nameField.GetValue(itemObj) as string;

                // Próba 2: Item.template.name (jeśli to instancja)
                var templateField = itemObj.GetType().GetField("template");
                if (templateField != null)
                {
                    var template = templateField.GetValue(itemObj);
                    if (template != null) return GetItemName(template);
                }
            }
            catch { }
            return null;
        }

        private bool IsContainerVisible(global::WTUIContainer ui)
        {
            // Sprawdzenie czy panel jest aktywny
            try
            {
                // Szukamy metody IsShow lub pola panel.activeSelf
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
            return true; // Domyślnie zakładamy że tak, jeśli instancja istnieje (ryzykowne, ale zadziała w update)
        }

        // --- UI ---

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
            GUILayout.BeginVertical("box", GUILayout.Width(160));
            GUILayout.Label($"<b>{Localization.Get("LOOT_HEADER_WHITE")}</b>");

            _scrollWhite = GUILayout.BeginScrollView(_scrollWhite, GUILayout.Height(250));
            // Kopia listy, aby można było usuwać podczas iteracji
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

            // Wyszukiwarka
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
                    // Filtrowanie
                    if (!string.IsNullOrEmpty(_searchFilter) &&
                        item.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    // Nie pokazuj jeśli już jest na liście
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
                // Znajdź wszystkie obiekty typu ItemTemplate (definicje przedmiotów)
                // W Wild Terra 2 klasa może się nazywać global::ItemTemplate
                // Używamy Resources.FindObjectsOfTypeAll żeby znaleźć załadowane assety (nawet nieaktywne)
                var templates = Resources.FindObjectsOfTypeAll<global::ItemTemplate>();

                foreach (var t in templates)
                {
                    if (t != null && !string.IsNullOrEmpty(t.name))
                    {
                        if (!_allItemsCache.Contains(t.name))
                            _allItemsCache.Add(t.name);
                    }
                }

                // Sortowanie alfabetyczne
                _allItemsCache.Sort();
                _status = $"Znaleziono {_allItemsCache.Count} przedmiotów.";
            }
            catch (Exception ex)
            {
                _status = "Błąd skanowania (zła klasa?)";
                Debug.LogError("[AutoLoot] " + ex.Message);
            }
        }
    }
}