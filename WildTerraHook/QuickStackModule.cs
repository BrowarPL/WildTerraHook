using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using Mirror;
using System.Linq;

namespace WildTerraHook
{
    public class QuickStackModule
    {
        private GameObject _injectedButton;
        private Image _buttonImage;
        private Color _originalColor = Color.white;

        private MethodInfo _moveMethod;
        private MethodInfo _sortMethod;
        private FieldInfo _currentTabField;
        private bool _reflectionInit = false;

        private bool _isStacking = false;
        private float _nextMoveTime = 0f;
        private bool _shouldSortAfter = false;
        private int _targetSortTab = 0;

        private float _flashEndTime = 0f;
        private bool _isFlashing = false;

        private struct MoveRequest
        {
            public int InvIndex;
            public int ContainerIndex;
        }

        private class ChestSlotInfo
        {
            public int SlotIndex; // Globalny indeks (uwzględnia offset zakładki)
            public int Hash;
            public int Amount;
            public int MaxStack;
            public bool IsFull => Amount >= MaxStack && MaxStack > 0;
        }

        private Queue<MoveRequest> _stackQueue = new Queue<MoveRequest>();

        public void Update()
        {
            if (!ConfigManager.QuickStack_Enabled)
            {
                if (_injectedButton != null) UnityEngine.Object.Destroy(_injectedButton);
                return;
            }

            var containerUI = global::WTUIContainer.instance;

            if (containerUI == null || !IsPanelActive(containerUI))
            {
                _isStacking = false;
                _stackQueue.Clear();
                return;
            }

            if (_injectedButton == null)
            {
                InjectButton(containerUI);
            }

            if (_isFlashing)
            {
                if (Time.time > _flashEndTime)
                {
                    if (_buttonImage != null) _buttonImage.color = _originalColor;
                    _isFlashing = false;
                }
            }

            if (_isStacking && Time.time > _nextMoveTime)
            {
                if (_stackQueue.Count > 0)
                {
                    MoveRequest req = _stackQueue.Dequeue();
                    ExecuteMove(req.InvIndex, req.ContainerIndex);
                    _nextMoveTime = Time.time + ConfigManager.QuickStack_Delay;
                }
                else
                {
                    _isStacking = false;

                    if (_shouldSortAfter)
                    {
                        ExecuteSort(_targetSortTab);
                        _shouldSortAfter = false;
                        Debug.Log($"[Auto Stack] Wysłano Auto Sort dla zakładki {_targetSortTab}.");
                    }
                    Debug.Log("[Auto Stack] Zakończono.");
                }
            }
        }

        private void InjectButton(global::WTUIContainer ui)
        {
            try
            {
                var panelField = ui.GetType().GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (panelField == null) return;
                var panelObj = panelField.GetValue(ui) as GameObject;
                if (panelObj == null) return;

                Button[] buttons = panelObj.GetComponentsInChildren<Button>(true);
                Button sourceButton = null;

                foreach (var b in buttons)
                {
                    if (b.GetComponent<global::WTUIContainerSlot>() == null && b.transform.parent.name != "Slots")
                    {
                        sourceButton = b;
                        break;
                    }
                }

                if (sourceButton != null)
                {
                    GameObject btnObj = UnityEngine.Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
                    btnObj.name = "btnAutoStack";

                    Button btnComp = btnObj.GetComponent<Button>();
                    btnComp.onClick.RemoveAllListeners();
                    btnComp.onClick.AddListener(OnQuickStackClick);

                    _buttonImage = btnObj.GetComponent<Image>();
                    if (_buttonImage != null) _originalColor = _buttonImage.color;

                    Text txt = btnObj.GetComponentInChildren<Text>();
                    if (txt != null) txt.text = "Auto Stack";

                    RectTransform rt = btnObj.GetComponent<RectTransform>();
                    rt.anchoredPosition += new Vector2(0, 45);

                    _injectedButton = btnObj;
                }
            }
            catch { }
        }

        private void TriggerFlash(Color color)
        {
            if (_buttonImage != null)
            {
                _buttonImage.color = color;
                _flashEndTime = Time.time + 1.5f;
                _isFlashing = true;
            }
        }

        private void OnQuickStackClick()
        {
            if (_isStacking) return;

            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;

            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            if (!_reflectionInit)
            {
                InitReflection(player, containerUI);
                _reflectionInit = true;
            }

            int currentTab = GetCurrentTab(containerUI);

            // 1. Analiza skrzyni
            List<ChestSlotInfo> occupiedSlots = new List<ChestSlotInfo>();
            Queue<int> emptySlots = new Queue<int>();
            HashSet<int> uniqueItemTypes = new HashSet<int>();

            // Przekazujemy currentTab, aby obliczyć prawidłowy offset
            if (!AnalyzeContainerDetailed(containerUI, occupiedSlots, emptySlots, uniqueItemTypes, currentTab))
            {
                Debug.Log("[Auto Stack] Błąd odczytu kontenera.");
                return;
            }

            _stackQueue.Clear();
            _shouldSortAfter = false;
            _targetSortTab = currentTab;
            bool noSpace = false;

            // --- SCENARIUSZ A: Jeden rodzaj przedmiotu -> Bulk Dump + Sort ---
            if (uniqueItemTypes.Count == 1)
            {
                Debug.Log($"[Auto Stack] Wykryto jeden typ przedmiotu (Tab: {currentTab}) -> Tryb Auto Sort.");
                int targetHash = uniqueItemTypes.First();

                for (int i = 0; i < player.inventory.Count; i++)
                {
                    var invSlot = player.inventory[i];
                    if (invSlot.amount <= 0 || invSlot.item.data == null || invSlot.item.hash != targetHash) continue;

                    var partialSlot = occupiedSlots.FirstOrDefault(s => s.Hash == targetHash && !s.IsFull);

                    int maxStack = invSlot.item.data.maxStack;
                    if (partialSlot != null && partialSlot.MaxStack <= 0) partialSlot.MaxStack = maxStack;

                    if (partialSlot != null)
                    {
                        _stackQueue.Enqueue(new MoveRequest { InvIndex = i, ContainerIndex = partialSlot.SlotIndex });

                        int space = partialSlot.MaxStack - partialSlot.Amount;
                        int moved = Mathf.Min(space, invSlot.amount);
                        partialSlot.Amount += moved;
                    }
                    else if (emptySlots.Count > 0)
                    {
                        int targetIndex = emptySlots.Dequeue();
                        _stackQueue.Enqueue(new MoveRequest { InvIndex = i, ContainerIndex = targetIndex });

                        occupiedSlots.Add(new ChestSlotInfo
                        {
                            SlotIndex = targetIndex,
                            Hash = targetHash,
                            Amount = invSlot.amount,
                            MaxStack = maxStack
                        });
                    }
                    else
                    {
                        noSpace = true;
                    }
                }

                if (_stackQueue.Count > 0) _shouldSortAfter = true;
            }
            // --- SCENARIUSZ B: Wiele rodzajów -> Inteligentne układanie ---
            else
            {
                Debug.Log($"[Auto Stack] Wiele typów (Tab: {currentTab}) -> Tryb Smart Fill.");

                for (int i = 0; i < player.inventory.Count; i++)
                {
                    var invSlot = player.inventory[i];
                    if (invSlot.amount <= 0 || invSlot.item.data == null) continue;

                    int hash = invSlot.item.hash;

                    if (uniqueItemTypes.Contains(hash))
                    {
                        int maxStack = invSlot.item.data.maxStack;
                        int remainingAmount = invSlot.amount;

                        // 1. Dopełnij istniejące
                        var matches = occupiedSlots.Where(s => s.Hash == hash && !s.IsFull).ToList();

                        foreach (var match in matches)
                        {
                            match.MaxStack = maxStack;

                            int space = match.MaxStack - match.Amount;
                            if (space > 0)
                            {
                                _stackQueue.Enqueue(new MoveRequest { InvIndex = i, ContainerIndex = match.SlotIndex });

                                int toMove = Mathf.Min(space, remainingAmount);
                                match.Amount += toMove;
                                remainingAmount -= toMove;

                                if (remainingAmount <= 0) break;
                            }
                        }

                        // 2. Nowy slot
                        if (remainingAmount > 0)
                        {
                            if (emptySlots.Count > 0)
                            {
                                int targetEmpty = emptySlots.Dequeue();
                                _stackQueue.Enqueue(new MoveRequest { InvIndex = i, ContainerIndex = targetEmpty });

                                occupiedSlots.Add(new ChestSlotInfo
                                {
                                    SlotIndex = targetEmpty,
                                    Hash = hash,
                                    Amount = remainingAmount,
                                    MaxStack = maxStack
                                });
                            }
                            else
                            {
                                noSpace = true;
                            }
                        }
                    }
                }
            }

            if (noSpace) TriggerFlash(Color.red);
            else if (_stackQueue.Count > 0) TriggerFlash(Color.green);

            if (_stackQueue.Count > 0)
            {
                _isStacking = true;
                _nextMoveTime = Time.time;
            }
        }

        private bool AnalyzeContainerDetailed(global::WTUIContainer ui, List<ChestSlotInfo> filledSlots, Queue<int> emptySlots, HashSet<int> uniqueTypes, int currentTab)
        {
            try
            {
                var fSlots = ui.GetType().GetField("slots", BindingFlags.Public | BindingFlags.Instance);
                if (fSlots == null) return false;

                var slotsArray = fSlots.GetValue(ui) as System.Array;
                if (slotsArray == null) return false;

                // --- FIX: OBLICZANIE OFFSETU DLA ZAKŁADEK ---
                // Zakładamy, że UI pokazuje całą stronę. Jeśli tab=1, to globalIndex = localIndex + (1 * length).
                int pageSize = slotsArray.Length;
                int globalOffset = currentTab * pageSize;
                // --------------------------------------------

                int localIndex = 0;
                foreach (object slotObj in slotsArray)
                {
                    if (slotObj == null) continue;

                    int realGlobalIndex = localIndex + globalOffset; // Kluczowa poprawka

                    bool isEmpty = true;
                    int hash = 0;
                    int amount = 0;
                    int maxStack = 9999;

                    var fItem = slotObj.GetType().GetField("item");
                    var fAmount = slotObj.GetType().GetField("amount");

                    if (fItem != null && fAmount != null)
                    {
                        amount = (int)fAmount.GetValue(slotObj);
                        object itemVal = fItem.GetValue(slotObj);

                        if (amount > 0 && itemVal != null)
                        {
                            var fHash = itemVal.GetType().GetField("hash");
                            if (fHash != null)
                            {
                                hash = (int)fHash.GetValue(itemVal);
                                if (hash != 0) isEmpty = false;
                            }
                        }
                    }

                    if (isEmpty)
                    {
                        emptySlots.Enqueue(realGlobalIndex);
                    }
                    else
                    {
                        uniqueTypes.Add(hash);
                        filledSlots.Add(new ChestSlotInfo
                        {
                            SlotIndex = realGlobalIndex, // Zapisujemy POPRAWNY globalny indeks
                            Hash = hash,
                            Amount = amount,
                            MaxStack = maxStack
                        });
                    }
                    localIndex++;
                }
                return true;
            }
            catch (System.Exception ex) { Debug.LogError("[QuickStack] Analyze Error: " + ex.Message); return false; }
        }

        private void ExecuteMove(int fromIdx, int toIdx)
        {
            var player = global::Player.localPlayer;
            if (player == null || _moveMethod == null) return;

            try
            {
                // CmdInventoryContainer(int inventoryIndex, int containerIndex)
                _moveMethod.Invoke(player, new object[] { fromIdx, toIdx });
            }
            catch { }
        }

        private void ExecuteSort(int tabIndex)
        {
            var player = global::Player.localPlayer;
            if (player == null || _sortMethod == null) return;

            try
            {
                _sortMethod.Invoke(player, new object[] { tabIndex });
            }
            catch { }
        }

        private void InitReflection(object player, object uiContainer)
        {
            var methods = player.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            foreach (var m in methods)
            {
                if (m.Name.Equals("CmdInventoryContainer", System.StringComparison.OrdinalIgnoreCase))
                    _moveMethod = m;
                else if (m.Name.Equals("CmdContainerSort", System.StringComparison.OrdinalIgnoreCase))
                    _sortMethod = m;
            }

            // Szukamy pola z indeksem zakładki
            var uiFields = uiContainer.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in uiFields)
            {
                // Szukamy pola int o nazwie sugerującej zakładkę
                if ((f.Name.Contains("Category") || f.Name.Contains("Tab")) && f.FieldType == typeof(int))
                {
                    // W Twoim pliku jest 'currentCategoryIndex' (niewidoczne w snippecie, ale standard w WT)
                    // Lub 'tabsPanel' które steruje widokiem.
                    // Spróbujemy znaleźć cokolwiek co wygląda na index.
                    // Najczęstsza nazwa w WT2: "currentCategoryIndex"
                    if (f.Name.IndexOf("Index", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _currentTabField = f;
                        break;
                    }
                }
            }
        }

        private int GetCurrentTab(global::WTUIContainer ui)
        {
            if (_currentTabField != null)
            {
                try { return (int)_currentTabField.GetValue(ui); } catch { }
            }
            // Fallback: Spróbuj znaleźć Toggle, który jest włączony w tabsPanel
            try
            {
                var fTabs = ui.GetType().GetField("tabsPanel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fTabs != null)
                {
                    var tabsObj = fTabs.GetValue(ui) as GameObject;
                    if (tabsObj != null)
                    {
                        var toggles = tabsObj.GetComponentsInChildren<Toggle>();
                        for (int i = 0; i < toggles.Length; i++)
                        {
                            if (toggles[i].isOn) return i;
                        }
                    }
                }
            }
            catch { }

            return 0;
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
            GUILayout.Label("<b>Auto Stack</b>");
            bool en = GUILayout.Toggle(ConfigManager.QuickStack_Enabled, Localization.Get("QS_ENABLE"));
            if (en != ConfigManager.QuickStack_Enabled) { ConfigManager.QuickStack_Enabled = en; ConfigManager.Save(); }

            if (ConfigManager.QuickStack_Enabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("QS_DELAY")}: {ConfigManager.QuickStack_Delay:F2}s");
                float newD = GUILayout.HorizontalSlider(ConfigManager.QuickStack_Delay, 0.1f, 1.5f);
                if (System.Math.Abs(newD - ConfigManager.QuickStack_Delay) > 0.05f) { ConfigManager.QuickStack_Delay = newD; ConfigManager.Save(); }
                GUILayout.EndHorizontal();
            }
        }
    }
}