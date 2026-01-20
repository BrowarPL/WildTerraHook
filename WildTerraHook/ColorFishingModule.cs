using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

// UWAGA: Mirror może nie być dostępny bezpośrednio, używamy global::
// using Mirror; 

namespace WildTerraHook
{
    public class ColorFishingModule
    {
        // --- KONFIGURACJA (Dostosowane do ConfigManager) ---
        public bool Enabled = false;          // Odpowiednik Settings.AutoFishColor
        public float FishingTimeout = 10.0f;  // Czas oczekiwania na branie

        // --- STANY ---
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

        // --- ZMIENNE POMOCNICZE ---
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

        // --- REFLECTION CACHE ---
        private MethodInfo _actionTargetMethod;
        private MethodInfo _skillToPointMethod;
        private MethodInfo _abilityToPointMethod;
        private MethodInfo _useItemMethod;
        private MethodInfo _setTargetMethod;
        private FieldInfo _uiPanelField;

        private string _debugMsg = "ZARZUĆ RĘCZNIE...";

        // --- METODY PUBLICZNE DLA MAINHACK ---

        public void OnDisable()
        {
            _hasCalibration = false;
            _currentState = BotState.WAITING_FOR_MANUAL;
            _fishingSkillIndex = -1;
            _combatSkillIndex = -1;
        }

        public void Update()
        {
            // Jeśli bot wyłączony w menu
            if (!Enabled) return;

            var wtPlayer = global::Player.localPlayer as global::WTPlayer;

            // Pobieramy instancję UI przez Reflection lub Static (zależnie od gry)
            // Zakładam, że WTUIFishingActions ma statyczną instancję 'instance'
            var fishingUI = global::WTUIFishingActions.instance;

            if (wtPlayer == null) return;
            if (_actionTargetMethod == null) ScanMethods(wtPlayer);

            if (_fishingSkillIndex == -1 || _combatSkillIndex == -1) FindSkills(wtPlayer);

            bool isFishing = wtPlayer.IsFishing();
            bool isUI = IsUIReal();

            // KALIBRACJA: Gracz musi zarzucić wędkę raz ręcznie
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
                    _debugMsg = $"[BOT] Skalibrowano! Cel: {_savedCastPoint}";
                }
                else
                {
                    _debugMsg = "ZARZUĆ RĘCZNIE ABY ROZPOCZĄĆ!";
                    return;
                }
            }

            // OBRONA: Wykrywanie agresywnego moba (np. kraba)
            if (_hasCalibration && (_currentState == BotState.FISHING || _currentState == BotState.CASTING_ROD))
            {
                if (Time.time > _stateTimer && wtPlayer.target != null && wtPlayer.target.name.IndexOf("crab", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (wtPlayer.target.health > 0)
                    {
                        Debug.Log("[BOT] ATAK! Przerywam.");
                        _currentState = BotState.COMBAT_PREPARE;
                        _stateTimer = Time.time + 0.1f;
                        return;
                    }
                }
            }

            // AWARYJNE PRZERYWANIE
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

            // DEBUG INFO
            _debugMsg = $"STAN: {_currentState}\nTimer: {Mathf.Max(0, _stateTimer - Time.time):F1}s\n" +
                        $"Atak ID: {_combatSkillIndex} | Wędka ID: {_fishingSkillIndex}\n" +
                        $"Cel: {(wtPlayer.target != null ? wtPlayer.target.name : "Brak")}";

            // MASZYNA STANÓW
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

        // Metoda do rysowania w MainHack.OnGUI (opcjonalna, np. status)
        public void OnGUI()
        {
            if (Enabled && _hasCalibration)
            {
                GUI.Label(new Rect(Screen.width / 2 - 100, 50, 200, 60), _debugMsg);
            }
        }

        // Metoda do rysowania w MainHack.DrawMenu (konfiguracja)
        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Fish Bot (Smart)</b>");

            bool newEnabled = GUILayout.Toggle(Enabled, "Włącz Bota (Zarzuć ręcznie)");
            if (newEnabled != Enabled)
            {
                Enabled = newEnabled;
                if (!Enabled) OnDisable(); // Reset przy wyłączeniu
            }

            GUILayout.Label($"Timeout (s): {FishingTimeout:F1}");
            FishingTimeout = GUILayout.HorizontalSlider(FishingTimeout, 5f, 30f);

            GUILayout.TextArea(_debugMsg); // Podgląd statusu w menu

            GUILayout.EndVertical();
        }

        // --- METODY POMOCNICZE (REFLEKSJA / LOGIKA) ---

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
                    if (Time.time - _castStartTime > FishingTimeout)
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
    }
}