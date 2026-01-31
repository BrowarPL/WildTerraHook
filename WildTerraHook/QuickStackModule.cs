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
        private MethodInfo _cmdMoveItem;
        private FieldInfo _playerInventoryField;
        private bool _reflectionInit = false;

        // --- Cache UI & Optymalizacja ---
        private RectTransform _containerWindowRect;
        private FieldInfo _uiPanelField; // Cache pola panelu (zamiast szukania co klatkę)
        private bool _cachedIsVisible = false; // Cache wyniku widoczności

        // --- Stan ---
        private bool _isStacking = false;

        // --- CLEANUP ---
        public void OnDestroy()
        {
            if (_floatingButton != null)
            {
                UnityEngine.Object.Destroy(_floatingButton);
                _floatingButton = null;
            }
        }

        // --- Struktury ---
        private class VirtualSlot
        {
            public int Index;
            public int ItemHash;
            public int Amount;
            public bool IsEmpty => ItemHash == 0 || Amount <= 0;
            public VirtualSlot Clone() => new VirtualSlot { Index = Index, ItemHash = ItemHash, Amount = Amount };
        }

        public void Update()
        {
            if (!ConfigManager.QuickStack_Enabled) return;

            // Inicjalizacja refleksji (tylko raz)
            if (!_reflectionInit) InitReflection();

            // Oblicz widoczność RAZ na klatkę
            _cachedIsVisible = CheckVisibilityOptimized();

            // Aktualizacja przycisku w oparciu o obliczoną widoczność
            UpdateFloatingButton();

            if (Input.GetKeyDown(_hotkey) && !_isStacking)
            {
                StartStacking();
            }
        }

        // --- GUI (Lekkie, korzysta z cache) ---
        public void OnGUI()
        {
            if (!ConfigManager.QuickStack_Enabled) return;

            // Używamy zmiennej obliczonej w Update, zamiast liczyć od nowa
            if (_cachedIsVisible)
            {
                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.normal.textColor = _isStacking ? Color.yellow : Color.green;
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;

                if (GUI.Button(new Rect(Screen.width - 130, 80, 120, 30), _isStacking ? "PRACA..." : "QS (APPEND)", style))
                {
                    if (!_isStacking) StartStacking();
                }
            }
        }

        public void DrawMenu()
        {
            GUILayout.Label("<b>Quick Stack v38 (FPS Fix)</b>");
            bool en = GUILayout.Toggle(ConfigManager.QuickStack_Enabled, Localization.Get("QS_ENABLE") ?? "Enable");
            if (en != ConfigManager.QuickStack_Enabled) { ConfigManager.QuickStack_Enabled = en; ConfigManager.Save(); }

            if (ConfigManager.QuickStack_Enabled)
            {
                GUILayout.Label($"Speed: {ConfigManager.QuickStack_Delay:F2}s");
                float newD = GUILayout.HorizontalSlider(ConfigManager.QuickStack_Delay, 0.05f, 0.5f);
                if (System.Math.Abs(newD - ConfigManager.QuickStack_Delay) > 0.05f) { ConfigManager.QuickStack_Delay = newD; ConfigManager.Save(); }
                GUILayout.Label(_isStacking ? "Status: Przenoszenie..." : "Status: Gotowy");
            }
        }

        private void InitReflection()
        {
            if (WTPlayer.localPlayer == null) return;
            try
            {
                var type = WTPlayer.localPlayer.GetType();
                _cmdMoveItem = GetMethodRecursive(type, "OnDragAndDrop_InventorySlot_ContainerSlot");
                _playerInventoryField = GetFieldRecursive(type, "inventory");

                // Cache pola panelu dla UI Container (szukamy raz)
                var containerType = typeof(WTUIContainer); // Zakładam, że klasa jest dostępna, bo używamy jej w FindObjectOfType
                _uiPanelField = GetFieldRecursive(containerType, "panel");

                if (_cmdMoveItem != null) _reflectionInit = true;
            }
            catch (System.Exception e) { Debug.LogError($"[QS] Init Error: {e.Message}"); }
        }

        // --- OPTYMALIZACJA WIDOCZNOŚCI ---
        private bool CheckVisibilityOptimized()
        {
            // 1. Używamy Singletona zamiast FindObjectOfType (Ogromna różnica wydajności)
            var ui = global::WTUIContainer.instance;

            if (ui == null) return false;
            if (!ui.gameObject.activeInHierarchy) return false;

            // 2. Używamy zcache'owanego pola (zamiast szukać go refleksją co klatkę)
            if (_uiPanelField != null)
            {
                var panelObj = _uiPanelField.GetValue(ui) as GameObject;
                if (panelObj != null) return panelObj.activeSelf;
            }

            return true;
        }

        private void StartStacking()
        {
            if (WTPlayer.localPlayer == null) return;
            WTPlayer.localPlayer.StartCoroutine(AppendStackRoutine());
        }

        private IEnumerator AppendStackRoutine()
        {
            _isStacking = true;

            if (_playerInventoryField == null) { _isStacking = false; yield break; }
            if (!_cachedIsVisible) { _isStacking = false; yield break; }

            var uiContainer = global::WTUIContainer.instance;
            if (uiContainer == null) { _isStacking = false; yield break; }

            object chestSlotsList = GetFieldOrPropRecursive(uiContainer, "slots");

            List<VirtualSlot> pSlots = ScanCollection(_playerInventoryField.GetValue(WTPlayer.localPlayer));
            List<VirtualSlot> cSlots = ScanCollection(chestSlotsList);

            var simulatedChest = cSlots.Select(x => x.Clone()).ToList();
            var chestHashes = simulatedChest.Where(x => !x.IsEmpty).Select(x => x.ItemHash).Distinct().ToList();

            foreach (var pItem in pSlots)
            {
                if (pItem.IsEmpty) continue;
                if (!chestHashes.Contains(pItem.ItemHash)) continue;

                VirtualSlot targetSlot = null;
                var sameItems = simulatedChest.Where(x => x.ItemHash == pItem.ItemHash && !x.IsEmpty).ToList();
                int lastIndex = -1;

                if (sameItems.Count > 0) lastIndex = sameItems.Max(x => x.Index);

                if (lastIndex != -1) targetSlot = simulatedChest.FirstOrDefault(x => x.IsEmpty && x.Index > lastIndex);
                if (targetSlot == null) targetSlot = simulatedChest.FirstOrDefault(x => x.IsEmpty);

                if (targetSlot != null)
                {
                    bool success = false;
                    if (_cmdMoveItem != null)
                    {
                        try
                        {
                            int[] args = new int[] { pItem.Index, targetSlot.Index };
                            _cmdMoveItem.Invoke(WTPlayer.localPlayer, new object[] { args });
                            success = true;
                        }
                        catch { }
                    }

                    if (success)
                    {
                        targetSlot.ItemHash = pItem.ItemHash;
                        targetSlot.Amount = pItem.Amount;
                        yield return new WaitForSeconds(Mathf.Max(0.05f, ConfigManager.QuickStack_Delay));
                    }
                }
                else break;
            }

            _isStacking = false;
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

            if (slotObj != null)
            {
                var type = slotObj.GetType();
                var fAmt = type.GetField("amount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fAmt != null) amount = (int)fAmt.GetValue(slotObj);

                if (amount <= 0) return new VirtualSlot { Index = index, ItemHash = 0, Amount = 0 };

                var fItem = type.GetField("item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fItem != null)
                {
                    object itemObj = fItem.GetValue(slotObj);
                    if (itemObj != null)
                    {
                        var tI = itemObj.GetType();
                        var fHash = tI.GetField("hash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fHash != null) hash = (int)fHash.GetValue(itemObj);

                        if (hash == 0)
                        {
                            var fInfo = tI.GetField("info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (fInfo != null)
                            {
                                object info = fInfo.GetValue(itemObj);
                                if (info != null)
                                {
                                    var fId = info.GetType().GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (fId != null) hash = (int)fId.GetValue(info);
                                }
                            }
                        }
                    }
                }
            }
            return new VirtualSlot { Index = index, ItemHash = hash, Amount = amount };
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

            // Pobieranie RectTransform tylko jeśli konieczne (optymalizacja)
            if (_containerWindowRect == null && ui != null)
            {
                if (_uiPanelField != null)
                {
                    var panelObj = _uiPanelField.GetValue(ui) as GameObject;
                    if (panelObj != null) _containerWindowRect = panelObj.GetComponent<RectTransform>();
                }
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

        private MethodInfo GetMethodRecursive(System.Type type, string methodName)
        {
            while (type != null && type != typeof(object))
            {
                var m = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (m != null) return m;
                type = type.BaseType;
            }
            return null;
        }
    }
}