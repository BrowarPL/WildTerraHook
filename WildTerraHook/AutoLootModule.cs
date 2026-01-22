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
        // --- DANE UI ---
        private List<string> _allItemsCache = new List<string>();
        private string _searchFilter = "";
        private string _newProfileName = "";
        private string _editingProfile = "Default";

        private Vector2 _scrollProfiles;
        private Vector2 _scrollWhite;
        private Vector2 _scrollAll;
        private Vector2 _scrollDebug;

        private float _lootTimer = 0f;
        private string _status = "Idle";
        private List<string> _debugDetectedItems = new List<string>();

        private MethodInfo _cmdGetItemMethod;
        private bool _reflectionInit = false;

        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            var containerUI = global::WTUIContainer.instance;
            if (containerUI == null) return;

            if (!IsPanelActive(containerUI))
            {
                if (_status.Contains("Loot") || _status.Contains("Widzę")) _status = Localization.Get("LOOT_WAITING");
                if (_debugDetectedItems.Count > 0) _debugDetectedItems.Clear();
                return;
            }

            if (ConfigManager.Loot_Debug || (ConfigManager.Loot_Enabled && Time.time > _lootTimer))
            {
                ProcessContainer(containerUI);
            }
        }

        private void ProcessContainer(global::WTUIContainer ui)
        {
            try
            {
                _debugDetectedItems.Clear();

                Transform panel = ui.transform.Find("WTContainerPanel");
                if (panel == null || !panel.gameObject.activeSelf) return;

                Transform content = RecursiveFindChild(panel, "Content");
                if (content == null) { _status = $"{Localization.Get("LOOT_ERROR")} Content"; return; }

                var dataSlots = GetDataSlots(ui);
                if (dataSlots == null) return;

                var uiSlots = panel.GetComponentsInChildren<global::WTUIContainerSlot>(false);
                bool lootedSomething = false;

                List<string> activeItems = ConfigManager.GetCombinedActiveList();

                foreach (var slotComp in uiSlots)
                {
                    if (slotComp.dragAndDropable == null) continue;

                    int realIndex;
                    if (!int.TryParse(slotComp.dragAndDropable.name, out realIndex)) continue;

                    string itemName = GetItemNameFromData(dataSlots, realIndex);

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        if (ConfigManager.Loot_Debug) _debugDetectedItems.Add($"[{realIndex}] {itemName}");

                        if (ConfigManager.Loot_Enabled && !lootedSomething && activeItems.Contains(itemName))
                        {
                            if (slotComp.button != null && slotComp.button.interactable)
                            {
                                _status = $"Loot (Btn): {itemName}";
                                slotComp.button.onClick.Invoke();
                                lootedSomething = true;
                            }
                            else
                            {
                                _status = $"Loot (Cmd): {itemName}";
                                SendLootCommand(realIndex);
                                lootedSomething = true;
                            }
                        }
                    }
                }

                if (lootedSomething) _lootTimer = Time.time + ConfigManager.Loot_Delay;
            }
            catch (Exception ex) { _status = $"{Localization.Get("LOOT_ERROR")}: {ex.Message}"; }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_TITLE")}</b>");

            GUILayout.BeginHorizontal();
            bool newVal = GUILayout.Toggle(ConfigManager.Loot_Enabled, Localization.Get("LOOT_ENABLE"), GUILayout.Width(150));
            if (newVal != ConfigManager.Loot_Enabled) { ConfigManager.Loot_Enabled = newVal; ConfigManager.Save(); }

            newVal = GUILayout.Toggle(ConfigManager.Loot_Debug, Localization.Get("LOOT_DEBUG"), GUILayout.Width(100));
            if (newVal != ConfigManager.Loot_Debug) { ConfigManager.Loot_Debug = newVal; ConfigManager.Save(); }

            GUILayout.Label($"{Localization.Get("LOOT_DELAY")}: {ConfigManager.Loot_Delay:F2}s", GUILayout.Width(120));
            float newDelay = GUILayout.HorizontalSlider(ConfigManager.Loot_Delay, 0.05f, 1.0f);
            if (Math.Abs(newDelay - ConfigManager.Loot_Delay) > 0.01f) { ConfigManager.Loot_Delay = newDelay; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            GUILayout.Label($"{Localization.Get("LOOT_STATUS")}: {_status}");

            if (ConfigManager.Loot_Debug) DrawDebugSection();

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            DrawProfileManager();
            DrawEditingProfileContent();
            DrawAllItemsList();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawDebugSection()
        {
            GUILayout.Label($"<b>{Localization.Get("LOOT_DETECTED")}</b>");
            _scrollDebug = GUILayout.BeginScrollView(_scrollDebug, "box", GUILayout.Height(80));
            if (_debugDetectedItems.Count > 0) foreach (var s in _debugDetectedItems) GUILayout.Label(s);
            else GUILayout.Label("...");
            GUILayout.EndScrollView();
        }

        private void DrawProfileManager()
        {
            GUILayout.BeginVertical("box", GUILayout.Width(170));
            GUILayout.Label($"<b>{Localization.Get("LOOT_PROFILES")}</b>");

            GUILayout.BeginHorizontal();
            _newProfileName = GUILayout.TextField(_newProfileName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                if (!string.IsNullOrEmpty(_newProfileName) && !ConfigManager.LootProfiles.ContainsKey(_newProfileName))
                {
                    ConfigManager.LootProfiles.Add(_newProfileName, new List<string>());
                    ConfigManager.ActiveProfiles.Add(_newProfileName);
                    _editingProfile = _newProfileName;
                    ConfigManager.Save();
                    _newProfileName = "";
                }
            }
            GUILayout.EndHorizontal();

            _scrollProfiles = GUILayout.BeginScrollView(_scrollProfiles, GUILayout.Height(250));

            var profileNames = new List<string>(ConfigManager.LootProfiles.Keys);
            foreach (var profile in profileNames)
            {
                GUILayout.BeginHorizontal("box");

                bool isActive = ConfigManager.ActiveProfiles.Contains(profile);
                bool newActive = GUILayout.Toggle(isActive, "", GUILayout.Width(20));
                if (newActive != isActive)
                {
                    if (newActive) ConfigManager.ActiveProfiles.Add(profile);
                    else ConfigManager.ActiveProfiles.Remove(profile);
                    ConfigManager.Save();
                }

                GUIStyle nameStyle = (profile == _editingProfile) ? GUI.skin.label : GUI.skin.label;
                string label = profile == _editingProfile ? $"> {profile}" : profile;

                if (GUILayout.Button(label, nameStyle)) _editingProfile = profile;

                if (ConfigManager.LootProfiles.Count > 1)
                {
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        ConfigManager.LootProfiles.Remove(profile);
                        ConfigManager.ActiveProfiles.Remove(profile);
                        if (_editingProfile == profile) _editingProfile = ConfigManager.LootProfiles.Keys.First();
                        ConfigManager.Save();
                    }
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawEditingProfileContent()
        {
            GUILayout.BeginVertical("box", GUILayout.Width(190));
            GUILayout.Label($"<b>{Localization.Get("LOOT_EDITING")}: {_editingProfile}</b>");

            List<string> editingList = null;
            if (ConfigManager.LootProfiles.ContainsKey(_editingProfile))
                editingList = ConfigManager.LootProfiles[_editingProfile];
            else
            {
                if (ConfigManager.LootProfiles.Count > 0) _editingProfile = ConfigManager.LootProfiles.Keys.First();
                return;
            }

            _scrollWhite = GUILayout.BeginScrollView(_scrollWhite, GUILayout.Height(280));

            var listCopy = new List<string>(editingList);
            foreach (var item in listCopy)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(item);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    editingList.Remove(item);
                    ConfigManager.Save();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawAllItemsList()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("LOOT_HEADER_ALL")}</b>");

            GUILayout.BeginHorizontal();
            _searchFilter = GUILayout.TextField(_searchFilter);
            if (GUILayout.Button("R", GUILayout.Width(25))) RefreshAllItems();
            GUILayout.EndHorizontal();

            _scrollAll = GUILayout.BeginScrollView(_scrollAll, GUILayout.Height(280));

            if (_allItemsCache.Count == 0)
            {
                if (GUILayout.Button(Localization.Get("LOOT_BTN_REFRESH"))) RefreshAllItems();
            }
            else
            {
                List<string> editingList = null;
                if (ConfigManager.LootProfiles.ContainsKey(_editingProfile))
                    editingList = ConfigManager.LootProfiles[_editingProfile];

                foreach (var item in _allItemsCache)
                {
                    if (!string.IsNullOrEmpty(_searchFilter) && item.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    bool alreadyAdded = (editingList != null && editingList.Contains(item));

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(item);

                    if (!alreadyAdded && editingList != null)
                    {
                        if (GUILayout.Button("+", GUILayout.Width(25)))
                        {
                            editingList.Add(item);
                            ConfigManager.Save();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        // --- POMOCNICY (BEZ ZMIAN) ---
        private Array GetDataSlots(global::WTUIContainer ui)
        {
            try
            {
                var f = ui.GetType().GetField("slots", BindingFlags.Public | BindingFlags.Instance);
                return (f != null) ? f.GetValue(ui) as Array : null;
            }
            catch { return null; }
        }

        private string GetItemNameFromData(Array dataSlots, int index)
        {
            try
            {
                if (index < 0 || index >= dataSlots.Length) return null;
                object slotObj = dataSlots.GetValue(index);
                if (slotObj == null) return null;

                var fAmount = slotObj.GetType().GetField("amount");
                if (fAmount != null)
                {
                    int amount = (int)fAmount.GetValue(slotObj);
                    if (amount <= 0) return null;
                }

                var fItem = slotObj.GetType().GetField("item");
                if (fItem == null) return null;
                object itemObj = fItem.GetValue(slotObj);

                var pData = itemObj.GetType().GetProperty("data");
                if (pData == null) return null;

                object scriptableItem = pData.GetValue(itemObj, null);
                if (scriptableItem == null) return null;

                var pName = scriptableItem.GetType().GetProperty("name");
                if (pName != null) return pName.GetValue(scriptableItem, null) as string;

                return null;
            }
            catch { return null; }
        }

        private void SendLootCommand(int index)
        {
            try
            {
                var player = global::Player.localPlayer;
                if (!_reflectionInit)
                {
                    _cmdGetItemMethod = player.GetType().GetMethod("CmdGetFromContainerToInventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _reflectionInit = true;
                }
                if (_cmdGetItemMethod != null) _cmdGetItemMethod.Invoke(player, new object[] { index });
            }
            catch { }
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

        private void RefreshAllItems()
        {
            _allItemsCache.Clear();
            _status = Localization.Get("LOOT_SCANNING");
            try
            {
                var scriptables = Resources.FindObjectsOfTypeAll<global::WTScriptableItem>();
                foreach (var s in scriptables) if (s != null && !string.IsNullOrEmpty(s.name) && !_allItemsCache.Contains(s.name)) _allItemsCache.Add(s.name);
                _allItemsCache.Sort();
                _status = $"{Localization.Get("LOOT_READY")} ({_allItemsCache.Count})";
            }
            catch (Exception ex)
            {
                _status = Localization.Get("LOOT_ERROR");
                Debug.LogError(ex.Message);
            }
        }
    }
}