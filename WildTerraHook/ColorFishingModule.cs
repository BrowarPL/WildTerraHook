using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;

namespace WildTerraHook
{
    public class ColorFishingModule
    {
        // --- TEXTURES FOR ESP ---
        private Texture2D _boxTexture;

        private enum BotState
        {
            WAITING_FOR_MANUAL,
            FISHING,
            COMBAT_PREPARE,
            COMBAT_FIGHT,
            REQUIPPING,
            CASTING_ROD,
            CAST_COOLDOWN
        }

        private BotState _currentState = BotState.WAITING_FOR_MANUAL;

        private int _lastActionCount = -1;
        private float _castStartTime = 0f;
        private float _stateTimer = -1f;

        private Vector3 _savedCastPoint;
        private Vector3 _savedPlayerPos;
        private Quaternion _savedPlayerRot;
        private bool _hasCalibration = false;

        private int _castAttempts = 0;

        private int _fishingSkillIndex = -1;
        private int _combatSkillIndex = -1;

        private MethodInfo _actionTargetMethod;
        private MethodInfo _skillToPointMethod;
        private MethodInfo _abilityToPointMethod;
        private MethodInfo _useItemMethod;
        private MethodInfo _setTargetMethod;
        private FieldInfo _uiPanelField;

        private string _debugMsg = "";

        public ColorFishingModule()
        {
            _debugMsg = Localization.Get("FISH_DEBUG_WAIT");
        }

        public void OnDisable()
        {
            _hasCalibration = false;
            _currentState = BotState.WAITING_FOR_MANUAL;
            _fishingSkillIndex = -1;
            _combatSkillIndex = -1;
            Debug.Log("[BOT] RESET.");
        }

        public void Update()
        {
            if (!ConfigManager.ColorFish_Enabled) return;

            var wtPlayer = global::Player.localPlayer as global::WTPlayer;
            var fishingUI = global::WTUIFishingActions.instance;

            if (wtPlayer == null) return;
            if (_actionTargetMethod == null) ScanMethods(wtPlayer);

            if (_fishingSkillIndex == -1 || _combatSkillIndex == -1) FindSkills(wtPlayer);

            bool isFishing = wtPlayer.IsFishing();
            bool isUI = IsUIReal();

            if (!_hasCalibration)
            {
                if (isFishing)
                {
                    Vector3 aimPoint = wtPlayer.GetSkillTargetPoint();
                    if (aimPoint == Vector3.zero)
                        aimPoint = wtPlayer.transform.position + (wtPlayer.transform.forward * 4.0f);

                    _savedCastPoint = aimPoint;
                    _savedPlayerPos = wtPlayer.transform.position;
                    _savedPlayerRot = wtPlayer.transform.rotation;
                    _hasCalibration = true;

                    EnterFishingState();
                    Debug.Log($"[BOT] {Localization.Get("FISH_DEBUG_CALIB")} {_savedCastPoint}");
                }
                else
                {
                    _debugMsg = Localization.Get("FISH_DEBUG_MANUAL");
                    return;
                }
            }

            if (_hasCalibration && (_currentState == BotState.FISHING || _currentState == BotState.CASTING_ROD))
            {
                if (Time.time > _stateTimer && wtPlayer.target != null && wtPlayer.target.name.IndexOf("crab", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (wtPlayer.target.health > 0)
                    {
                        Debug.Log("[BOT] ATAK!");
                        _currentState = BotState.COMBAT_PREPARE;
                        _stateTimer = Time.time + 0.1f;
                        return;
                    }
                }
            }

            if (_currentState == BotState.FISHING && Time.time > _stateTimer)
            {
                if (isFishing && !isUI)
                {
                    wtPlayer.CmdFishingUse(global::FishingUse.DragOut);
                    ForceRecast();
                    return;
                }
                if (!isFishing && !isUI)
                {
                    ForceRecast();
                    return;
                }
            }

            string targetName = (wtPlayer.target != null) ? wtPlayer.target.name : Localization.Get("FISH_NONE");
            _debugMsg = $"{Localization.Get("FISH_STATE")}: {_currentState} | {Localization.Get("FISH_TIMER")}: {Mathf.Max(0, _stateTimer - Time.time):F1}s\n" +
                        $"{Localization.Get("FISH_ATT_ID")}: {_combatSkillIndex} | {Localization.Get("FISH_ROD_ID")}: {_fishingSkillIndex}\n" +
                        $"{Localization.Get("FISH_TARGET")}: {targetName}";

            switch (_currentState)
            {
                case BotState.FISHING: HandleFishing(wtPlayer, fishingUI); break;
                case BotState.COMBAT_PREPARE: HandleCombatPrepare(wtPlayer); break;
                case BotState.COMBAT_FIGHT: HandleCombatFight(wtPlayer); break;
                case BotState.REQUIPPING: HandleRequipping(wtPlayer); break;
                case BotState.CASTING_ROD: HandleCastingRod(wtPlayer); break;
                case BotState.CAST_COOLDOWN: HandleCastCooldown(wtPlayer); break;
            }
        }

        public void DrawESP()
        {
            if (!ConfigManager.ColorFish_Enabled || !ConfigManager.ColorFish_ShowESP) return;

            var fishingUI = global::WTUIFishingActions.instance;
            if (fishingUI == null) return;

            if (!IsUIReal()) return;

            try
            {
                Color32 success = GetField<Color32>(fishingUI, "successButtonColor");
                CheckAndDrawESP(fishingUI, "dragOutActionButtonImage", success);
                CheckAndDrawESP(fishingUI, "pullActionButtonImage", success);
                CheckAndDrawESP(fishingUI, "strikeActionButtonImage", success);
            }
            catch { }
        }

        private void CheckAndDrawESP(object ui, string fName, Color32 target)
        {
            var img = GetField<Image>(ui, fName);
            if (img != null && img.gameObject.activeInHierarchy)
            {
                Color32 c = img.color;
                if (c.r == target.r && c.g == target.g && c.b == target.b)
                {
                    DrawBoxOnRect(img.rectTransform);
                }
            }
        }

        private void DrawBoxOnRect(RectTransform rect)
        {
            if (rect == null) return;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float x = corners[0].x;
            float y = Screen.height - corners[1].y;
            float w = corners[2].x - corners[0].x;
            float h = corners[1].y - corners[0].y;

            if (_boxTexture == null)
            {
                _boxTexture = new Texture2D(1, 1);
                _boxTexture.SetPixel(0, 0, new Color(0, 1, 0, 0.4f));
                _boxTexture.Apply();
            }

            GUI.DrawTexture(new Rect(x, y, w, h), _boxTexture);
        }

        private void ScanMethods(global::WTPlayer player)
        {
            var methods = player.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                if (m.Name == "CmdSetActionTarget") _actionTargetMethod = m;
                if (m.Name == "CmdSetTarget") _setTargetMethod = m;
                if (m.Name == "CmdAbilitySkillToPoint") _abilityToPointMethod = m;
                if (m.Name == "CmdSkillToPoint") _skillToPointMethod = m;
                if (m.Name == "CmdUseInventoryItem") _useItemMethod = m;
            }
            _uiPanelField = typeof(global::WTUIFishingActions).GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void FindSkills(global::WTPlayer player)
        {
            if (player.skills == null) return;

            for (int i = 0; i < player.skills.Count; i++)
            {
                var skillData = player.skills[i].data;
                if (skillData == null) continue;
                string sName = skillData.name.ToLower();

                if (sName.Contains("fishing")) _fishingSkillIndex = i;

                if (!sName.Contains("fishing") && !sName.Contains("passive") && !sName.Contains("armor"))
                {
                    bool isBasic = sName.Contains("attack") || sName.Contains("hit") || sName.Contains("slash") || sName.Contains("strike");
                    bool isSpecial = sName.Contains("throw") || sName.Contains("rage") || sName.Contains("taunt") || sName.Contains("shout");

                    if (isBasic && !isSpecial) _combatSkillIndex = i;
                }
            }

            if (_fishingSkillIndex == -1) _fishingSkillIndex = 8;
            if (_combatSkillIndex == -1) _combatSkillIndex = 0;
        }

        private bool IsUIReal()
        {
            if (global::WTUICastBar.instance != null && global::WTUICastBar.instance.IsShow()) return true;
            if (global::WTUIFishingActions.instance != null && _uiPanelField != null)
            {
                try
                {
                    GameObject panel = _uiPanelField.GetValue(global::WTUIFishingActions.instance) as GameObject;
                    if (panel != null && panel.activeSelf) return true;
                }
                catch { }
            }
            return false;
        }

        private void EnterFishingState()
        {
            _currentState = BotState.FISHING;
            _castStartTime = Time.time;
            _lastActionCount = -1;
        }

        private void ForceRecast()
        {
            _currentState = BotState.CASTING_ROD;
            _stateTimer = Time.time + 1.0f;
            _castAttempts = 0;
        }

        private void HandleCombatPrepare(global::WTPlayer player)
        {
            if (Time.time < _stateTimer) return;
            if (_useItemMethod != null) _useItemMethod.Invoke(player, new object[] { 0 });
            _currentState = BotState.COMBAT_FIGHT;
            _stateTimer = Time.time + 1.0f;
        }

        private void HandleCombatFight(global::WTPlayer player)
        {
            if (Time.time < _stateTimer) return;
            if (player.target == null || player.target.health <= 0)
            {
                _currentState = BotState.REQUIPPING;
                _stateTimer = Time.time + 0.2f;
                return;
            }
            player.transform.LookAt(player.target.transform.position);

            if (player.target.netIdentity != null)
            {
                if (_setTargetMethod != null)
                    _setTargetMethod.Invoke(player, new object[] { player.target.netIdentity });

                if (_abilityToPointMethod != null)
                    _abilityToPointMethod.Invoke(player, new object[] { _combatSkillIndex, player.target.transform.position });
                else
                    player.CmdUseSkill(_combatSkillIndex);
            }
            _stateTimer = Time.time + 0.8f;
        }

        private void HandleRequipping(global::WTPlayer player)
        {
            if (Time.time < _stateTimer) return;
            if (_hasCalibration)
            {
                player.transform.position = _savedPlayerPos;
                player.transform.rotation = _savedPlayerRot;
            }
            player.CmdUseSkill(_fishingSkillIndex);
            _currentState = BotState.CASTING_ROD;
            _stateTimer = Time.time + 2.0f;
            _castAttempts = 0;
        }

        private void HandleCastingRod(global::WTPlayer player)
        {
            if (Time.time < _stateTimer) return;
            if (player.IsFishing() && IsUIReal())
            {
                EnterFishingState();
                return;
            }

            if (_castAttempts < 5)
            {
                if (_hasCalibration) player.transform.rotation = _savedPlayerRot;
                if (_abilityToPointMethod != null)
                    _abilityToPointMethod.Invoke(player, new object[] { _fishingSkillIndex, _savedCastPoint });
                else
                    player.CmdUseSkill(_fishingSkillIndex);

                _castAttempts++;
                _currentState = BotState.CAST_COOLDOWN;
                _stateTimer = Time.time + 1.0f;
            }
            else
            {
                _currentState = BotState.REQUIPPING;
                _stateTimer = Time.time + 1.0f;
                _castAttempts = 0;
            }
        }

        private void HandleCastCooldown(global::WTPlayer player)
        {
            if (Time.time < _stateTimer) return;
            if (player.IsFishing()) EnterFishingState();
            else _currentState = BotState.CASTING_ROD;
        }

        private void HandleFishing(global::WTPlayer player, global::WTUIFishingActions ui)
        {
            if (_castStartTime == 0) _castStartTime = Time.time;
            if (!player.IsFishing())
            {
                ForceRecast();
                return;
            }
            if (ui == null) return;

            if (Time.time - _castStartTime > 2.0f)
            {
                var actions = GetField<List<global::FishBite>>(ui, "fishActions");
                if (actions == null || actions.Count == 0)
                {
                    if (Time.time - _castStartTime > ConfigManager.ColorFish_Timeout)
                    {
                        player.CmdFishingUse(global::FishingUse.DragOut);
                        return;
                    }
                }
                else
                {
                    if (actions.Count != _lastActionCount)
                    {
                        _lastActionCount = actions.Count;
                        _castStartTime = Time.time;
                        Color32 success = GetField<Color32>(ui, "successButtonColor");
                        if (IsActive(ui, "dragOutActionButtonImage", success)) player.CmdFishingUse(global::FishingUse.DragOut);
                        else if (IsActive(ui, "pullActionButtonImage", success)) player.CmdFishingUse(global::FishingUse.Pull);
                        else if (IsActive(ui, "strikeActionButtonImage", success)) player.CmdFishingUse(global::FishingUse.Strike);
                    }
                }
            }
        }

        private bool IsActive(object ui, string fName, Color32 target)
        {
            var img = GetField<Image>(ui, fName);
            if (img == null) return false;
            Color32 c = img.color;
            return c.r == target.r && c.g == target.g && c.b == target.b;
        }

        private T GetField<T>(object obj, string name)
        {
            var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return (f != null) ? (T)f.GetValue(obj) : default(T);
        }

        public void DrawMenu()
        {
            GUILayout.Label($"<b>{Localization.Get("FISH_TITLE")}</b>");

            bool newVal = GUILayout.Toggle(ConfigManager.ColorFish_Enabled, Localization.Get("FISH_ENABLE"));
            if (newVal != ConfigManager.ColorFish_Enabled)
            {
                ConfigManager.ColorFish_Enabled = newVal;
                if (newVal) ConfigManager.MemFish_Enabled = false;
                if (!newVal) OnDisable();
                ConfigManager.Save();
            }

            if (ConfigManager.ColorFish_Enabled)
            {
                bool esp = GUILayout.Toggle(ConfigManager.ColorFish_ShowESP, Localization.Get("FISH_SHOW_ESP"));
                if (esp != ConfigManager.ColorFish_ShowESP) { ConfigManager.ColorFish_ShowESP = esp; ConfigManager.Save(); }

                GUILayout.Label($"{Localization.Get("FISH_TIMEOUT")}: {ConfigManager.ColorFish_Timeout:F0}s");
                float newT = GUILayout.HorizontalSlider(ConfigManager.ColorFish_Timeout, 10f, 60f);
                if (Math.Abs(newT - ConfigManager.ColorFish_Timeout) > 1f) { ConfigManager.ColorFish_Timeout = newT; ConfigManager.Save(); }
                GUILayout.Label($"<b>{_debugMsg}</b>");
            }
        }
    }
}