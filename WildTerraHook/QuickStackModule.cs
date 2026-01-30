using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace WildTerraHook
{
    public class QuickStackModule
    {
        private GameObject _injectedButton;
        private MethodInfo _moveItemMethod;
        private bool _reflectionInit = false;
        private bool _isStacking = false;
        private float _nextMoveTime = 0f;

        // Kolejka do przenoszenia: List<int> slotIndexes
        private Queue<int> _stackQueue = new Queue<int>();

        public void Update()
        {
            if (!ConfigManager.QuickStack_Enabled)
            {
                if (_injectedButton != null) UnityEngine.Object.Destroy(_injectedButton);
                return;
            }

            var containerUI = global::WTUIContainer.instance;

            // Sprawdź czy UI kontenera jest aktywne
            if (containerUI == null || !IsPanelActive(containerUI))
            {
                _isStacking = false;
                _stackQueue.Clear();
                return;
            }

            // Wstrzykuj przycisk
            if (_injectedButton == null)
            {
                InjectButton(containerUI);
            }

            // Obsługa kolejki przenoszenia (z delayem)
            if (_isStacking && Time.time > _nextMoveTime)
            {
                if (_stackQueue.Count > 0)
                {
                    int slotIndex = _stackQueue.Dequeue();
                    ExecuteMove(slotIndex);
                    _nextMoveTime = Time.time + ConfigManager.QuickStack_Delay;
                }
                else
                {
                    _isStacking = false;
                    Debug.Log("[QuickStack] Zakończono.");
                }
            }
        }

        private void InjectButton(global::WTUIContainer ui)
        {
            try
            {
                // Szukamy panelu wewnątrz UI
                var panelField = ui.GetType().GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (panelField == null) return;
                var panelObj = panelField.GetValue(ui) as GameObject;
                if (panelObj == null) return;

                // Szukamy przycisku "Sort" lub "TakeAll" do sklonowania
                // Zazwyczaj są gdzieś w hierarchii. Szukamy po komponentach Button.
                Button[] buttons = panelObj.GetComponentsInChildren<Button>(true);
                Button sourceButton = null;

                foreach (var b in buttons)
                {
                    // Szukamy przycisku, który nie jest slotem
                    if (b.GetComponent<global::WTUIContainerSlot>() == null && b.transform.parent.name != "Slots")
                    {
                        sourceButton = b;
                        break;
                    }
                }

                if (sourceButton != null)
                {
                    GameObject btnObj = UnityEngine.Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
                    btnObj.name = "btnQuickStack";

                    // Usuwamy stare listenery
                    Button btnComp = btnObj.GetComponent<Button>();
                    btnComp.onClick.RemoveAllListeners();
                    btnComp.onClick.AddListener(OnQuickStackClick);

                    // Zmieniamy tekst
                    Text txt = btnObj.GetComponentInChildren<Text>();
                    if (txt != null) txt.text = Localization.Get("QS_BUTTON");

                    // Pozycjonowanie (przesuwamy trochę w prawo/lewo od oryginału)
                    RectTransform rt = btnObj.GetComponent<RectTransform>();
                    rt.anchoredPosition += new Vector2(0, 40); // Przesuwamy w górę, żeby nie zasłaniał

                    _injectedButton = btnObj;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[QuickStack] Błąd wstrzykiwania przycisku: " + ex.Message);
            }
        }

        private void OnQuickStackClick()
        {
            if (_isStacking) return;

            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;

            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            // 1. Pobierz zawartość skrzyni (Hashset nazw/ID przedmiotów)
            HashSet<int> containerItemHashes = GetContainerContents(containerUI);
            if (containerItemHashes == null) return;

            // 2. Skanuj plecak i dodaj pasujące do kolejki
            _stackQueue.Clear();
            for (int i = 0; i < player.inventory.Count; i++)
            {
                var slot = player.inventory[i];
                if (slot.amount > 0 && slot.item.data != null)
                {
                    int hash = slot.item.hash; // Item struct ma pole hash
                    if (containerItemHashes.Contains(hash))
                    {
                        _stackQueue.Enqueue(i);
                    }
                }
            }

            if (_stackQueue.Count > 0)
            {
                _isStacking = true;
                _nextMoveTime = Time.time; // Start od razu
                Debug.Log($"[QuickStack] Rozpoczynam przenoszenie {_stackQueue.Count} przedmiotów...");
            }
            else
            {
                Debug.Log("[QuickStack] Brak przedmiotów do stackowania.");
            }
        }

        private HashSet<int> GetContainerContents(global::WTUIContainer ui)
        {
            HashSet<int> hashes = new HashSet<int>();
            try
            {
                var fSlots = ui.GetType().GetField("slots", BindingFlags.Public | BindingFlags.Instance);
                if (fSlots == null) return null;

                var slotsArray = fSlots.GetValue(ui) as Array; // Array of ItemSlot?
                if (slotsArray == null) return null;

                foreach (object slotObj in slotsArray)
                {
                    if (slotObj == null) continue;
                    // Refleksja do pola 'item' w ItemSlot
                    var fItem = slotObj.GetType().GetField("item");
                    if (fItem != null)
                    {
                        object itemVal = fItem.GetValue(slotObj); // To jest struktura Item
                        if (itemVal != null)
                        {
                            var fHash = itemVal.GetType().GetField("hash");
                            if (fHash != null)
                            {
                                int hash = (int)fHash.GetValue(itemVal);
                                if (hash != 0) hashes.Add(hash);
                            }
                        }
                    }
                }
            }
            catch { return null; }
            return hashes;
        }

        private void ExecuteMove(int inventorySlotIndex)
        {
            var player = global::Player.localPlayer;
            if (player == null) return;

            if (!_reflectionInit)
            {
                InitMoveMethod(player);
                _reflectionInit = true;
            }

            if (_moveItemMethod != null)
            {
                try
                {
                    // Metoda zazwyczaj przyjmuje (int inventoryIndex)
                    // Lub (int inventoryIndex, int amount)
                    var pars = _moveItemMethod.GetParameters();
                    if (pars.Length == 1)
                        _moveItemMethod.Invoke(player, new object[] { inventorySlotIndex });
                    else if (pars.Length == 2)
                        _moveItemMethod.Invoke(player, new object[] { inventorySlotIndex, 9999 }); // Max amount
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[QuickStack] Błąd wykonania ruchu: " + ex.Message);
                }
            }
        }

        private void InitMoveMethod(object player)
        {
            var methods = player.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            // Szukamy metody CmdMoveInventoryItemToContainer, CmdStoreItem itp.
            foreach (var m in methods)
            {
                if (m.Name.Equals("CmdMoveInventoryItemToContainer", System.StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Equals("CmdStoreItem", System.StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Equals("CmdDepositItem", System.StringComparison.OrdinalIgnoreCase))
                {
                    _moveItemMethod = m;
                    return;
                }
            }

            // Fallback: Szukamy po ItemActionType.Store w CmdInventoryItemAction
            // Ale to wymagałoby innej logiki wywołania. Na razie szukamy dedykowanej metody Move.
            // Jeśli nie znajdziemy, spróbujemy znaleźć cokolwiek z "Container" i "Inventory" w nazwie.
            foreach (var m in methods)
            {
                if (m.Name.Contains("Container") && m.Name.Contains("Inventory") && m.Name.StartsWith("Cmd"))
                {
                    // Wyklucz Get (to jest lootowanie)
                    if (!m.Name.Contains("Get"))
                    {
                        _moveItemMethod = m;
                        return;
                    }
                }
            }
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

        public void DrawMenu()
        {
            // Ta metoda będzie wywoływana w MiscModule lub MainHack
            GUILayout.Label("<b>Quick Stack</b>");
            bool en = GUILayout.Toggle(ConfigManager.QuickStack_Enabled, Localization.Get("QS_ENABLE"));
            if (en != ConfigManager.QuickStack_Enabled) { ConfigManager.QuickStack_Enabled = en; ConfigManager.Save(); }

            if (ConfigManager.QuickStack_Enabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("QS_DELAY")}: {ConfigManager.QuickStack_Delay:F2}s");
                float newD = GUILayout.HorizontalSlider(ConfigManager.QuickStack_Delay, 0.1f, 1.5f);
                if (Math.Abs(newD - ConfigManager.QuickStack_Delay) > 0.05f) { ConfigManager.QuickStack_Delay = newD; ConfigManager.Save(); }
                GUILayout.EndHorizontal();
            }
        }
    }
}