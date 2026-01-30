using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        // Debugging
        private bool _debugMode = true;

        private Queue<int> _stackQueue = new Queue<int>();

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
                    if (_debugMode) Debug.Log("[QuickStack] Zakończono przenoszenie.");
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
                    btnObj.name = "btnQuickStack";

                    Button btnComp = btnObj.GetComponent<Button>();
                    btnComp.onClick.RemoveAllListeners();
                    btnComp.onClick.AddListener(OnQuickStackClick);

                    Text txt = btnObj.GetComponentInChildren<Text>();
                    if (txt != null) txt.text = Localization.Get("QS_BUTTON");

                    RectTransform rt = btnObj.GetComponent<RectTransform>();
                    rt.anchoredPosition += new Vector2(0, 40);

                    _injectedButton = btnObj;
                }
            }
            catch (System.Exception ex) { Debug.LogError("[QuickStack] Button inject error: " + ex.Message); }
        }

        private void OnQuickStackClick()
        {
            if (_debugMode) Debug.Log("[QuickStack] Kliknięto przycisk!");

            if (_isStacking) return;

            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;

            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            // Inicjalizacja metody przy pierwszym użyciu (aby znaleźć właściwą nazwę)
            if (!_reflectionInit)
            {
                InitMoveMethod(player);
                _reflectionInit = true;
            }

            HashSet<int> containerItemHashes = GetContainerContents(containerUI);
            if (containerItemHashes == null || containerItemHashes.Count == 0)
            {
                if (_debugMode) Debug.Log("[QuickStack] Skrzynia jest pusta lub nie udało się odczytać zawartości.");
                return;
            }

            _stackQueue.Clear();
            for (int i = 0; i < player.inventory.Count; i++)
            {
                var slot = player.inventory[i];
                if (slot.amount > 0 && slot.item.data != null)
                {
                    int hash = slot.item.hash;
                    if (containerItemHashes.Contains(hash))
                    {
                        _stackQueue.Enqueue(i);
                    }
                }
            }

            if (_stackQueue.Count > 0)
            {
                _isStacking = true;
                _nextMoveTime = Time.time;
                if (_debugMode) Debug.Log($"[QuickStack] Znaleziono {_stackQueue.Count} pasujących przedmiotów. Rozpoczynam stackowanie...");
            }
            else
            {
                if (_debugMode) Debug.Log("[QuickStack] Brak pasujących przedmiotów w ekwipunku.");
            }
        }

        private HashSet<int> GetContainerContents(global::WTUIContainer ui)
        {
            HashSet<int> hashes = new HashSet<int>();
            try
            {
                var fSlots = ui.GetType().GetField("slots", BindingFlags.Public | BindingFlags.Instance);
                if (fSlots == null)
                {
                    if (_debugMode) Debug.LogError("[QuickStack] Nie znaleziono pola 'slots' w WTUIContainer.");
                    return null;
                }

                var slotsArray = fSlots.GetValue(ui) as Array;
                if (slotsArray == null) return null;

                foreach (object slotObj in slotsArray)
                {
                    if (slotObj == null) continue;
                    var fItem = slotObj.GetType().GetField("item");
                    if (fItem != null)
                    {
                        object itemVal = fItem.GetValue(slotObj);
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
            catch (System.Exception ex) { Debug.LogError("[QuickStack] Błąd odczytu skrzyni: " + ex.Message); return null; }
            return hashes;
        }

        private void ExecuteMove(int inventorySlotIndex)
        {
            var player = global::Player.localPlayer;
            if (player == null) return;

            if (_moveItemMethod != null)
            {
                try
                {
                    var pars = _moveItemMethod.GetParameters();
                    if (pars.Length == 1)
                    {
                        _moveItemMethod.Invoke(player, new object[] { inventorySlotIndex });
                    }
                    else if (pars.Length == 2)
                    {
                        // Sprawdzamy typ drugiego parametru
                        if (pars[1].ParameterType == typeof(int))
                        {
                            // (index, amount) -> Przenosimy wszystko (max int)
                            _moveItemMethod.Invoke(player, new object[] { inventorySlotIndex, int.MaxValue });
                        }
                        else
                        {
                            // Może to być (index, ContainerId)? Wtedy 0?
                            _moveItemMethod.Invoke(player, new object[] { inventorySlotIndex, 0 });
                        }
                    }
                    if (_debugMode) Debug.Log($"[QuickStack] Wysłano komendę dla slotu {inventorySlotIndex}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[QuickStack] Błąd wykonania ruchu: " + ex.Message);
                }
            }
            else
            {
                if (_debugMode) Debug.LogError("[QuickStack] Metoda przenoszenia nie została znaleziona!");
            }
        }

        private void InitMoveMethod(object player)
        {
            var methods = player.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            // Lista potencjalnych nazw (priorytetowo Twoja sugestia)
            string[] candidates = {
                "CmdMoveItemFromInventoryToContainer", // Twoja sugestia
                "CmdMoveInventoryItemToContainer",
                "CmdStoreItem",
                "CmdDepositItem",
                "CmdMoveItem",
                "CmdContainerMove"
            };

            foreach (string name in candidates)
            {
                foreach (var m in methods)
                {
                    if (m.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        _moveItemMethod = m;
                        Debug.LogWarning($"[QuickStack] ZNALEZIONO METODĘ: {m.Name}");
                        return;
                    }
                }
            }

            // Jeśli nie znaleziono po nazwie, szukaj heurystycznie
            Debug.Log("[QuickStack] Szukam metody heurystycznie...");
            foreach (var m in methods)
            {
                if (m.Name.StartsWith("Cmd") &&
                   (m.Name.Contains("Container") || m.Name.Contains("Store") || m.Name.Contains("Deposit")) &&
                   !m.Name.Contains("Get")) // 'Get' to zazwyczaj branie ze skrzyni
                {
                    _moveItemMethod = m;
                    Debug.LogWarning($"[QuickStack] ZNALEZIONO HEURYSTYCZNIE: {m.Name}");
                    return;
                }
            }

            Debug.LogError("[QuickStack] NIE ZNALEZIONO ŻADNEJ METODY DO PRZENOSZENIA!");
        }

        // --- SKANER DO DIAGNOSTYKI ---
        private void ScanAndPrintMethods()
        {
            var player = global::Player.localPlayer;
            if (player == null) return;

            var methods = player.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            Debug.LogWarning("--- [QuickStack DEBUG] LISTA KOMEND ---");
            foreach (var m in methods)
            {
                // Filtrujemy tylko ciekawe metody (zaczynające się na Cmd i związane z przedmiotami)
                if (m.Name.StartsWith("Cmd") && (m.Name.Contains("Item") || m.Name.Contains("Container") || m.Name.Contains("Inv")))
                {
                    string paramInfo = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
                    Debug.Log($"> {m.Name}({paramInfo})");
                }
            }
            Debug.LogWarning("--- KONIEC LISTY ---");
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

                // Przycisk Diagnostyczny
                GUILayout.Space(5);
                if (GUILayout.Button("SCAN METHODS (Check Console F1)"))
                {
                    ScanAndPrintMethods();
                }
            }
        }
    }
}