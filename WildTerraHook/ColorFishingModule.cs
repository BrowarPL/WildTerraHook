using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine.AI;

namespace WildTerraHook
{
    public class ColorFishingModule
    {
        private Texture2D _boxTexture;

        private enum BotState
        {
            WAITING_FOR_MANUAL,
            FISHING,
            COMBAT_PREPARE,
            COMBAT_FIGHT,
            BUTCHERING,
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

        // ID Skilli
        private int _fishingSkillIndex = -1;
        private int _combatSkillIndex = -1;
        private int _skinningSkillIndex = 33; // Sztywne ID 33

        // Reflection
        private MethodInfo _actionTargetMethod;
        private MethodInfo _skillToPointMethod;
        private MethodInfo _abilityToPointMethod;
        private MethodInfo _useItemMethod;
        private MethodInfo _itemActionMethod;
        private MethodInfo _setTargetMethod;
        private MethodInfo _cmdUseEntityMethod;
        private FieldInfo _uiPanelField;

        private MonoBehaviour _butcherTarget;
        private string _debugMsg = "";
        private object _actionEquipEnum;

        public ColorFishingModule()
        {
            _debugMsg = Localization.Get("FISH_DEBUG_WAIT");
            _boxTexture = new Texture2D(1, 1);
            _boxTexture.SetPixel(0, 0, new Color(1, 0, 0, 0.3f));
            _boxTexture.Apply();
        }

        public void OnDisable()
        {
            _hasCalibration = false;
            _currentState = BotState.WAITING_FOR_MANUAL;
            _fishingSkillIndex = -1;
            _combatSkillIndex = -1;
            _skinningSkillIndex = 33; // Reset na 33
            _butcherTarget = null;
            Debug.Log("[BOT] RESET.");
        }

        public void Update()
        {
            // Opcjonalny inspektor pod "P" (przydatny w razie problemów)
            if (Input.GetKeyDown(KeyCode.P)) DebugMouseHover();

            if (!ConfigManager.ColorFish_Enabled) return;

            var wtPlayer = global::Player.localPlayer as global::WTPlayer;
            var fishingUI = global::WTUIFishingActions.instance;

            if (wtPlayer == null) return;

            if (_actionTargetMethod == null) ScanMethods(wtPlayer);
            if (_fishingSkillIndex == -1 || _combatSkillIndex == -1) FindSkills(wtPlayer);

            bool isFishing = wtPlayer.IsFishing();
            bool isUI = IsUIReal();

            // --- KALIBRACJA ---
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
                    Debug.Log($"[BOT] Skalibrowano. Punkt: {_savedCastPoint}");
                }
                else
                {
                    _debugMsg = Localization.Get("FISH_DEBUG_MANUAL");
                    return;
                }
            }

            // --- WYKRYWANIE ZAGROŻENIA ---
            if (_hasCalibration && (_currentState == BotState.FISHING || _currentState == BotState.CASTING_ROD))
            {
                if (wtPlayer.target != null && wtPlayer.target.health > 0)
                {
                    string tName = wtPlayer.target.name.ToLower();
                    if (tName.Contains("crab") || tName.Contains("krab") || tName.Contains("mimic"))
                    {
                        Debug.LogWarning("[BOT] KRAB ATAKUJE!");
                        _currentState = BotState.COMBAT_PREPARE;
                        _stateTimer = Time.time;
                        return;
                    }
                }
            }

            // --- TIMEOUT ŁOWIENIA ---
            if (_currentState == BotState.FISHING && Time.time > _stateTimer)
            {
                if (isFishing && !isUI) { wtPlayer.CmdFishingUse(global::FishingUse.DragOut); ForceRecast(); return; }
                if (!isFishing && !isUI) { ForceRecast(); return; }
            }

            string targetName = (wtPlayer.target != null) ? wtPlayer.target.name : "BRAK";
            _debugMsg = $"Stan: {_currentState} | SkinID: {_skinningSkillIndex}";

            switch (_currentState)
            {
                case BotState.FISHING: HandleFishing(wtPlayer, fishingUI); break;
                case BotState.COMBAT_PREPARE: HandleCombatPrepare(wtPlayer); break;
                case BotState.COMBAT_FIGHT: HandleCombatFight(wtPlayer); break;
                case BotState.BUTCHERING: HandleButchering(wtPlayer); break;
                case BotState.REQUIPPING: HandleRequipping(wtPlayer); break;
                case BotState.CASTING_ROD: HandleCastingRod(wtPlayer); break;
                case BotState.CAST_COOLDOWN: HandleCastCooldown(wtPlayer); break;
            }
        }

        // --- 1. ZMIANA BRONI ---
        private void HandleCombatPrepare(global::WTPlayer player)
        {
            Debug.Log("[BOT] Wyciągam broń (Equip Slot 0)...");
            if (_itemActionMethod != null && _actionEquipEnum != null)
            {
                try { _itemActionMethod.Invoke(player, new object[] { 0, _actionEquipEnum, 0 }); } catch { }
            }
            else if (_useItemMethod != null)
            {
                try { _useItemMethod.Invoke(player, new object[] { 0 }); } catch { }
            }
            _currentState = BotState.COMBAT_FIGHT;
            _stateTimer = Time.time + 0.8f;
        }

        // --- 2. WALKA ---
        private void HandleCombatFight(global::WTPlayer player)
        {
            if (Time.time < _stateTimer) return;

            if (player.IsFishing())
            {
                player.CmdFishingUse(global::FishingUse.DragOut);
                _currentState = BotState.COMBAT_PREPARE;
                return;
            }

            bool isDead = (player.target == null || player.target.health <= 0);

            if (isDead)
            {
                Debug.Log($"[BOT] Walka zakończona. Szukam trupa.");
                _butcherTarget = null;
                _currentState = BotState.BUTCHERING;
                _stateTimer = Time.time + 0.5f;
                return;
            }

            player.transform.LookAt(player.target.transform.position);

            if (player.target.netIdentity != null)
            {
                if (_setTargetMethod != null) try { _setTargetMethod.Invoke(player, new object[] { player.target.netIdentity }); } catch { }

                if (_abilityToPointMethod != null)
                {
                    try { _abilityToPointMethod.Invoke(player, new object[] { _combatSkillIndex, player.target.transform.position }); } catch { }
                }
                else
                {
                    player.CmdUseSkill(_combatSkillIndex);
                }
            }
            _stateTimer = Time.time + 0.6f;
        }

        // --- 3. SKÓROWANIE (Używa Delay z ConfigManager) ---
        private void HandleButchering(global::WTPlayer player)
        {
            // A. Szukanie trupa
            if (_butcherTarget == null || _butcherTarget.GetComponent<NetworkIdentity>() == null)
            {
                if (Time.time > _stateTimer)
                {
                    _butcherTarget = FindNearbyDeadCrab(player);
                    if (_butcherTarget == null)
                    {
                        if (Time.time - _stateTimer > 5.0f)
                        {
                            Debug.LogWarning("[BOT] Nie znaleziono trupa. Powrót.");
                            _currentState = BotState.REQUIPPING;
                        }
                        return;
                    }
                }
                else return;
            }

            // B. Podejdź
            float dist = Vector3.Distance(player.transform.position, _butcherTarget.transform.position);

            if (dist > 1.5f)
            {
                NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.stoppingDistance = 1.0f;
                    agent.SetDestination(_butcherTarget.transform.position);
                }
                return;
            }
            else
            {
                NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh && !agent.isStopped)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
                player.transform.LookAt(_butcherTarget.transform.position);
            }

            // C. Interakcja
            if (Time.time > _stateTimer)
            {
                var ni = _butcherTarget.GetComponent<NetworkIdentity>();
                if (ni == null) ni = _butcherTarget.GetComponentInParent<NetworkIdentity>();

                if (ni != null)
                {
                    // Ustaw cel
                    if (_setTargetMethod != null) try { _setTargetMethod.Invoke(player, new object[] { ni }); } catch { }
                    if (_actionTargetMethod != null) try { _actionTargetMethod.Invoke(player, new object[] { ni }); } catch { }

                    // Używamy ZNANEGO ID 33
                    if (_skinningSkillIndex != -1)
                    {
                        Debug.Log($"[BOT] Używam Skinning (ID 33). Czekam {ConfigManager.ColorFish_ButcherDelay:F1}s...");
                        player.CmdUseSkill(33);

                        // Używamy opóźnienia z Configa
                        _stateTimer = Time.time + ConfigManager.ColorFish_ButcherDelay;
                    }
                    else
                    {
                        // Fallback Action 24
                        Debug.LogWarning("[BOT] Brak ID Skinningu. Używam akcji 24.");
                        if (_cmdUseEntityMethod != null)
                        {
                            try { _cmdUseEntityMethod.Invoke(player, new object[] { ni, 24 }); } catch { }
                        }
                        _stateTimer = Time.time + 2.0f;
                    }
                }
                else
                {
                    _butcherTarget = null;
                }
            }
        }

        private MonoBehaviour FindNearbyDeadCrab(global::WTPlayer player)
        {
            Collider[] hits = Physics.OverlapSphere(player.transform.position, 8.0f);
            foreach (var hit in hits)
            {
                var ni = hit.GetComponentInParent<NetworkIdentity>();
                if (ni == null) continue;

                var entity = ni.GetComponent<MonoBehaviour>();
                if (entity != null)
                {
                    string n = entity.name.ToLower();
                    if (n.Contains("crab") || n.Contains("krab") || n.Contains("mimic"))
                    {
                        if (IsDead(entity)) return entity;
                    }
                }
            }
            return null;
        }

        private bool IsDead(MonoBehaviour mob)
        {
            try
            {
                var f = mob.GetType().GetField("_health", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (f != null) { int hp = (int)f.GetValue(mob); return hp <= 0; }
            }
            catch { }
            return true;
        }

        // --- RESZTA METOD ---

        private void HandleRequipping(global::WTPlayer player)
        {
            if (Time.time < _stateTimer) return;
            if (_hasCalibration)
            {
                player.transform.position = _savedPlayerPos;
                player.transform.rotation = _savedPlayerRot;
            }

            if (_itemActionMethod != null && _actionEquipEnum != null)
            {
                try { _itemActionMethod.Invoke(player, new object[] { _fishingSkillIndex, _actionEquipEnum, 0 }); } catch { }
            }
            else
            {
                player.CmdUseSkill(_fishingSkillIndex);
            }

            _currentState = BotState.CASTING_ROD;
            _stateTimer = Time.time + 2.0f;
            _castAttempts = 0;
        }

        private void HandleCastingRod(global::WTPlayer player)
        {
            if (Time.time < _stateTimer) return;
            if (player.IsFishing() && IsUIReal()) { EnterFishingState(); return; }

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
            if (!player.IsFishing()) { ForceRecast(); return; }
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

        private void ForceRecast()
        {
            _currentState = BotState.CASTING_ROD;
            _stateTimer = Time.time + 1.0f;
            _castAttempts = 0;
        }

        private void EnterFishingState()
        {
            _currentState = BotState.FISHING;
            _castStartTime = Time.time;
            _lastActionCount = -1;
        }

        private void ScanMethods(global::WTPlayer player)
        {
            var methods = player.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            foreach (var m in methods)
            {
                if (m.Name == "CmdSetActionTarget") _actionTargetMethod = m;
                if (m.Name == "CmdSetTarget") _setTargetMethod = m;
                if (m.Name == "CmdAbilitySkillToPoint") _abilityToPointMethod = m;
                if (m.Name == "CmdSkillToPoint") _skillToPointMethod = m;
                if (m.Name == "CmdUseInventoryItem") _useItemMethod = m;
                if (m.Name == "CmdInventoryItemAction") _itemActionMethod = m;
                if (m.Name == "CmdUseEntity") _cmdUseEntityMethod = m;
            }
            _uiPanelField = typeof(global::WTUIFishingActions).GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                Type enumType = Type.GetType("ItemActionType, Assembly-CSharp");
                if (enumType != null) _actionEquipEnum = Enum.Parse(enumType, "Equip");
                else _actionEquipEnum = 1;
            }
            catch { _actionEquipEnum = 1; }
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
                    if (isBasic && !sName.Contains("throw")) _combatSkillIndex = i;
                }
            }
            if (_fishingSkillIndex == -1) _fishingSkillIndex = 8;
            if (_combatSkillIndex == -1) _combatSkillIndex = 0;

            _skinningSkillIndex = 33;
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

        private void DebugMouseHover()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var ni = hit.collider.GetComponentInParent<NetworkIdentity>();
                if (ni != null) Debug.Log($"[INSPEKTOR] Trafiono: {hit.collider.name} | ID: {ni.netId}");
                else Debug.Log($"[INSPEKTOR] Trafiono: {hit.collider.name} (Brak ID)");
            }
        }

        public void DrawESP()
        {
            if (!ConfigManager.ColorFish_Enabled || !ConfigManager.ColorFish_ShowESP) return;
            var fishingUI = global::WTUIFishingActions.instance;

            if (_butcherTarget != null)
            {
                Vector3 pos = Camera.main.WorldToScreenPoint(_butcherTarget.transform.position);
                if (pos.z > 0)
                {
                    GUI.color = Color.magenta;
                    GUI.Label(new Rect(pos.x, Screen.height - pos.y, 100, 20), "TRUP");
                    GUI.color = Color.white;
                }
            }

            if (fishingUI == null || !IsUIReal()) return;
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
                    DrawBoxOnRect(img.rectTransform);
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
            GUI.DrawTexture(new Rect(x, y, w, h), _boxTexture);
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

                GUILayout.Space(5);
                GUILayout.Label($"<b>Butcher Delay: {ConfigManager.ColorFish_ButcherDelay:F1}s</b>");
                float newDelay = GUILayout.HorizontalSlider(ConfigManager.ColorFish_ButcherDelay, 1.0f, 10.0f);
                if (Math.Abs(newDelay - ConfigManager.ColorFish_ButcherDelay) > 0.1f)
                {
                    ConfigManager.ColorFish_ButcherDelay = newDelay;
                    ConfigManager.Save();
                }
            }
        }
    }
}