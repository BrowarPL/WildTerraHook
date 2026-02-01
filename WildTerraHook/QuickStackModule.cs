using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WildTerraHook
{
    public class QuickStackModule
    {
        // --- Konfiguracja ---
        private KeyCode _hotkey = KeyCode.F8;
        private GameObject _floatingButton;

        // --- Cache Metod ---
        private MethodInfo _cmdPush; // CmdInventoryContainer
        private MethodInfo _cmdPull; // CmdGetFromContainerToInventory

        private FieldInfo _playerInventoryField;
        private bool _reflectionInit = false;

        // --- Cache UI ---
        private RectTransform _containerWindowRect;
        private FieldInfo _uiPanelField;
        private bool _cachedIsVisible = false;

        // --- Stan ---
        private bool _isStacking = false;
        private string _statusInfo = "Gotowy";

        // --- Struktury ---
        private class VirtualSlot
        {
            public int Index;
            public int ItemHash;
            public int Amount;
            public int MaxStack;

            public bool IsEmpty => ItemHash == 0 || Amount <= 0;
            public VirtualSlot Clone() => new VirtualSlot { Index = Index, ItemHash = ItemHash, Amount = Amount, MaxStack = MaxStack };
        }

        public void Update()
        {
            if (!ConfigManager.QuickStack_Enabled) return;
            if (!_reflectionInit) InitReflection();

            _cachedIsVisible = CheckVisibilityOptimized();
            UpdateFloatingButton();

            if (Input.GetKeyDown(_hotkey) && !_isStacking)
            {
                StartStacking();
            }
        }

        // --- GUI ---
        public void OnGUI()
        {
            if (!ConfigManager.QuickStack_Enabled) return;

            if (_cachedIsVisible)
            {
                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.normal.textColor = _isStacking ? Color.yellow : Color.green;
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;

                if (GUI.Button(new Rect(Screen.width - 130, 80, 120, 30), _isStacking ? "PRACA..." : "QS (LOOP)", style))
                {
                    if (!_isStacking) StartStacking();
                }
            }
        }

        public void DrawMenu()
        {
            GUILayout.Label("<b>Quick Stack v47 (Loop Until Clean)</b>");
            bool en = GUILayout.Toggle(ConfigManager.QuickStack_Enabled, Localization.Get("QS_ENABLE") ?? "Enable");
            if (en != ConfigManager.QuickStack_Enabled) { ConfigManager.QuickStack_Enabled = en; ConfigManager.Save(); }

            if (ConfigManager.QuickStack_Enabled)
            {
                int msDelay = (int)(ConfigManager.QuickStack_Delay * 1000);
                GUILayout.Label($"Opóźnienie (Ping): {msDelay}ms");

                float newD = GUILayout.HorizontalSlider(ConfigManager.QuickStack_Delay, 0.1f, 1.0f);
                if (System.Math.Abs(newD - ConfigManager.QuickStack_Delay) > 0.01f)
                {
                    ConfigManager.QuickStack_Delay = newD;
                    ConfigManager.Save();
                }

                GUILayout.Label($"Status: {_statusInfo}");
            }
        }

        private void InitReflection()
        {
            if (WTPlayer.localPlayer == null) return;
            try
            {
                var type = WTPlayer.localPlayer.GetType();
                _cmdPush = type.GetMethod("CmdInventoryContainer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _cmdPull = type.GetMethod("CmdGetFromContainerToInventory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _playerInventoryField = GetFieldRecursive(type, "inventory");

                var containerType = typeof(WTUIContainer);
                _uiPanelField = GetFieldRecursive(containerType, "panel");

                if (_cmdPush != null && _cmdPull != null) _reflectionInit = true;
            }
            catch (System.Exception e) { Debug.LogError($"[QS] Init Error: {e.Message}"); }
        }

        private void StartStacking()
        {
            if (WTPlayer.localPlayer == null) return;
            WTPlayer.localPlayer.StartCoroutine(LoopUntilDoneRoutine());
        }

        // --- GŁÓWNA PĘTLA (LOOP UNTIL DONE) ---
        private IEnumerator LoopUntilDoneRoutine()
        {
            _isStacking = true;
            int safetyCounter = 0; // Zabezpieczenie przed nieskończoną pętlą
            const int MAX_LOOPS = 10;

            if (_playerInventoryField == null || !_cachedIsVisible) { _isStacking = false; yield break; }
            var uiContainer = global::WTUIContainer.instance;

            // PĘTLA GŁÓWNA: Powtarzaj dopóki są pasujące przedmioty
            while (true)
            {
                safetyCounter++;
                if (safetyCounter > MAX_LOOPS)
                {
                    _statusInfo = "Przerwano (Limit)";
                    Debug.LogWarning("[QS] Osiągnięto limit powtórzeń. Przerywanie, aby uniknąć zawieszenia.");
                    break;
                }

                _statusInfo = $"Cykl {safetyCounter}...";

                object chestSlotsList = GetFieldOrPropRecursive(uiContainer, "slots");

                // 1. Skanowanie (Zawsze świeże dane na początku cyklu)
                var pSlots = ScanCollection(_playerInventoryField.GetValue(WTPlayer.localPlayer));
                var cSlots = ScanCollection(chestSlotsList);

                // Dedukcja MaxStack
                Dictionary<int, int> realMaxStacks = new Dictionary<int, int>();
                foreach (var s in pSlots.Concat(cSlots))
                {
                    if (s.IsEmpty) continue;
                    if (!realMaxStacks.ContainsKey(s.ItemHash)) realMaxStacks[s.ItemHash] = s.Amount;
                    else if (s.Amount > realMaxStacks[s.ItemHash]) realMaxStacks[s.ItemHash] = s.Amount;
                }

                // Sprawdź, czy jest w ogóle co robić
                var chestHashes = new HashSet<int>(cSlots.Where(x => !x.IsEmpty).Select(x => x.ItemHash));
                var itemsToProcess = pSlots.Where(x => !x.IsEmpty && chestHashes.Contains(x.ItemHash))
                                           .Select(x => x.ItemHash)
                                           .Distinct()
                                           .ToList();

                // WARUNEK WYJŚCIA: Jeśli lista do przetworzenia jest pusta -> Koniec
                if (itemsToProcess.Count == 0)
                {
                    _statusInfo = "Czysto!";
                    break;
                }

                // 2. Przetwarzanie przedmiotów (Logika z V46)
                foreach (int hash in itemsToProcess)
                {
                    // Odśwież stan skrzyni (symulacja lokalna dla pętli)
                    cSlots = ScanCollection(chestSlotsList);

                    // --- KROK A: PULL ---
                    int limit = realMaxStacks.ContainsKey(hash) ? realMaxStacks[hash] : 1;

                    VirtualSlot originSlot = cSlots.LastOrDefault(x => x.ItemHash == hash && x.Amount < limit);
                    if (originSlot == null) originSlot = cSlots.LastOrDefault(x => x.ItemHash == hash);

                    int originIndex = -1;

                    if (originSlot != null && _cmdPull != null)
                    {
                        originIndex = originSlot.Index;
                        _cmdPull.Invoke(WTPlayer.localPlayer, new object[] { originIndex });
                        yield return new WaitForSeconds(ConfigManager.QuickStack_Delay);
                    }

                    // --- KROK B: SCAN INV ---
                    var currentInv = ScanCollection(_playerInventoryField.GetValue(WTPlayer.localPlayer));
                    var myItemsToSend = currentInv.Where(x => x.ItemHash == hash && !x.IsEmpty)
                                                  .OrderByDescending(x => x.Amount)
                                                  .ToList();

                    if (myItemsToSend.Count == 0) continue;

                    // --- KROK C: PUSH SEQUENCE ---
                    bool originFilled = false;
                    var simulatedChest = ScanCollection(chestSlotsList);

                    foreach (var itemToSend in myItemsToSend)
                    {
                        int targetIndex = -1;

                        // 1. Fill Origin
                        if (originIndex != -1 && !originFilled)
                        {
                            targetIndex = originIndex;
                            originFilled = true;

                            var simSlot = simulatedChest.FirstOrDefault(x => x.Index == targetIndex);
                            if (simSlot != null) { simSlot.ItemHash = hash; simSlot.Amount = limit; }
                        }
                        else
                        {
                            // 2. Append
                            int lastOccupiedIdx = -1;
                            var existing = simulatedChest.Where(x => x.ItemHash == hash && !x.IsEmpty).ToList();
                            if (existing.Count > 0) lastOccupiedIdx = existing.Max(x => x.Index);

                            VirtualSlot target = simulatedChest.FirstOrDefault(x => x.IsEmpty && x.Index > lastOccupiedIdx);
                            if (target == null) target = simulatedChest.FirstOrDefault(x => x.IsEmpty);

                            if (target != null)
                            {
                                targetIndex = target.Index;
                                target.ItemHash = hash;
                                target.Amount = itemToSend.Amount;
                            }
                        }

                        if (targetIndex != -1 && _cmdPush != null)
                        {
                            _cmdPush.Invoke(WTPlayer.localPlayer, new object[] { itemToSend.Index, targetIndex });
                            yield return new WaitForSeconds(ConfigManager.QuickStack_Delay);
                        }
                        else
                        {
                            break; // Skrzynia pełna
                        }
                    }
                }

                // Mała przerwa przed kolejną iteracją pętli while, aby UI odetchnęło
                yield return new WaitForSeconds(ConfigManager.QuickStack_Delay);
            }

            _isStacking = false;
        }

        public void OnDestroy()
        {
            if (_floatingButton != null)
            {
                UnityEngine.Object.Destroy(_floatingButton);
                _floatingButton = null;
            }
        }

        private bool CheckVisibilityOptimized()
        {
            var ui = global::WTUIContainer.instance;
            if (ui == null) return false;
            if (!ui.gameObject.activeInHierarchy) return false;
            if (_uiPanelField != null)
            {
                var panelObj = _uiPanelField.GetValue(ui) as GameObject;
                if (panelObj != null) return panelObj.activeSelf;
            }
            return true;
        }

        private void UpdateFloatingButton()
        {
            if (!_cachedIsVisible)
            {
                if (_floatingButton != null) _floatingButton.SetActive(false);
                _containerWindowRect = null;
                return;
            }

            var ui = global::WTUIContainer.instance;
            if (_containerWindowRect == null && ui != null && _uiPanelField != null)
            {
                var panelObj = _uiPanelField.GetValue(ui) as GameObject;
                if (panelObj != null) _containerWindowRect = panelObj.GetComponent<RectTransform>();
            }

            if (_floatingButton == null && ui != null) CreateFloatingButton(ui.transform.root);

            if (_floatingButton != null && _containerWindowRect != null)
            {
                _floatingButton.SetActive(true);
                Vector3 windowPos = _containerWindowRect.position;
                float xOffset = (_containerWindowRect.rect.width / 2) * _containerWindowRect.lossyScale.x;
                float yOffset = (_containerWindowRect.rect.height / 2) * _containerWindowRect.lossyScale.y;
                _floatingButton.transform.position = new Vector3(windowPos.x + xOffset - 40, windowPos.y + yOffset - 40, 0);
                _floatingButton.transform.SetAsLastSibling();
            }
        }

        private void CreateFloatingButton(Transform canvasRoot)
        {
            _floatingButton = new GameObject("QuickStack_Floater");
            _floatingButton.transform.SetParent(canvasRoot, false);
            var img = _floatingButton.AddComponent<Image>();
            img.color = new Color(0f, 1f, 0f, 1f);
            var btn = _floatingButton.AddComponent<Button>();
            btn.onClick.AddListener(() => StartStacking());
            var rt = _floatingButton.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40, 40);
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(_floatingButton.transform, false);
            var txt = textObj.AddComponent<Text>();
            txt.text = "QS";
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.resizeTextForBestFit = true;
            txt.color = Color.black;
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.offsetMin = Vector2.zero; textRt.offsetMax = Vector2.zero;
        }

        private List<VirtualSlot> ScanCollection(object collectionObj)
        {
            List<VirtualSlot> result = new List<VirtualSlot>();
            IEnumerable enumerable = collectionObj as IEnumerable;
            if (enumerable == null) return result;
            int index = 0;
            foreach (object itemSlotObj in enumerable)
            {
                result.Add(ParseItemSlot(itemSlotObj, index));
                index++;
            }
            return result;
        }

        private VirtualSlot ParseItemSlot(object slotObj, int index)
        {
            int hash = 0;
            int amount = 0;
            int max = 0;

            if (slotObj != null)
            {
                var type = slotObj.GetType();
                var fAmt = type.GetField("amount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fAmt != null) amount = (int)fAmt.GetValue(slotObj);

                if (amount <= 0) return new VirtualSlot { Index = index, ItemHash = 0, Amount = 0, MaxStack = 1 };

                var fItem = type.GetField("item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fItem != null)
                {
                    object itemObj = fItem.GetValue(slotObj);
                    if (itemObj != null)
                    {
                        var tI = itemObj.GetType();
                        var fHash = tI.GetField("hash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fHash != null) hash = (int)fHash.GetValue(itemObj);

                        var fInfo = tI.GetField("info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fInfo != null)
                        {
                            object info = fInfo.GetValue(itemObj);
                            if (info != null)
                            {
                                var tInfo = info.GetType();
                                var fMax = tInfo.GetField("maxStack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (fMax != null) max = (int)fMax.GetValue(info);
                                else
                                {
                                    var pMax = tInfo.GetProperty("maxStack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (pMax != null) max = (int)pMax.GetValue(info, null);
                                }

                                if (hash == 0)
                                {
                                    var fId = tInfo.GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (fId != null) hash = (int)fId.GetValue(info);
                                }
                            }
                        }
                    }
                }
            }

            if (max == 0) max = 1;

            return new VirtualSlot { Index = index, ItemHash = hash, Amount = amount, MaxStack = max };
        }

        private object GetFieldOrPropRecursive(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f.GetValue(obj);
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (p != null) return p.GetValue(obj, null);
                t = t.BaseType;
            }
            return null;
        }
        private FieldInfo GetFieldRecursive(System.Type type, string name)
        {
            while (type != null && type != typeof(object))
            {
                var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }
    }
}