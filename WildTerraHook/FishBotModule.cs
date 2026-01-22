using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System;
using System.Linq;
using System.Text;

namespace WildTerraHook
{
    public class FishBotModule
    {
        private float _actionTimer = 0f;
        private Texture2D _boxTexture;
        private string _status = "Init";
        private bool _reflectionInit = false;

        // --- UI ---
        private FieldInfo _fBtnDragImg;
        private FieldInfo _fBtnPullImg;
        private FieldInfo _fBtnStrikeImg;
        private FieldInfo _fCurrentBites;

        // --- REGUŁY ---
        private object _rulesSourceObj;
        private FieldInfo _fRulesList;
        private FieldInfo _fRuleBite;
        private FieldInfo _fRuleUse;

        public void Update()
        {
            if (!ConfigManager.MemFish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();

            if (fishingUI == null || !fishingUI.gameObject.activeSelf)
            {
                _status = Localization.Get("MEMFISH_STATUS_WAIT_WIN");
                _reflectionInit = false;
                return;
            }

            if (!_reflectionInit) InitReflection(fishingUI);
            ProcessFishing(fishingUI);
        }

        private void InitReflection(global::WTUIFishingActions ui)
        {
            try
            {
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                Type uiType = ui.GetType();

                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);
                _fCurrentBites = uiType.GetField("fishActions", flags);

                if (_fBtnDragImg == null || _fCurrentBites == null)
                {
                    _status = Localization.Get("MEMFISH_STATUS_CRIT");
                    return;
                }

                List<object> scanTargets = new List<object>();
                scanTargets.Add(ui);

                var overview = UnityEngine.Object.FindObjectOfType<global::WTUIFishingOverview>();
                if (overview != null) scanTargets.Add(overview);

                var worldFishing = UnityEngine.Object.FindObjectOfType<global::WTWorldFishing>();
                if (worldFishing != null) scanTargets.Add(worldFishing);

                bool found = false;
                foreach (var target in scanTargets)
                {
                    if (DeepScanForRules(target, flags))
                    {
                        _rulesSourceObj = target;
                        found = true;
                        Debug.Log($"[FishBot] SUKCES! Reguły znalezione w: {target.GetType().Name}.{_fRulesList.Name}");
                        break;
                    }
                }

                if (found)
                {
                    _reflectionInit = true;
                    _status = Localization.Get("MEMFISH_STATUS_READY");
                }
                else
                {
                    string targetsStr = string.Join(", ", scanTargets.Select(x => x.GetType().Name).ToArray());
                    _status = $"{Localization.Get("MEMFISH_STATUS_ERROR")}: {targetsStr}";
                }
            }
            catch (Exception ex)
            {
                _status = "Ref Crash: " + ex.Message;
            }
        }

        private bool DeepScanForRules(object target, BindingFlags flags)
        {
            if (target == null) return false;
            Type type = target.GetType();

            foreach (var field in type.GetFields(flags))
            {
                Type fType = field.FieldType;

                Type itemType = null;
                if (fType.IsGenericType && fType.GetGenericTypeDefinition() == typeof(List<>))
                    itemType = fType.GetGenericArguments()[0];
                else if (fType.IsArray)
                    itemType = fType.GetElementType();

                if (itemType != null && !itemType.IsPrimitive && itemType != typeof(string))
                {
                    FieldInfo biteF = null;
                    FieldInfo useF = null;

                    foreach (var subF in itemType.GetFields(flags))
                    {
                        if (subF.FieldType == typeof(global::FishBite)) biteF = subF;
                        if (subF.FieldType == typeof(global::FishingUse)) useF = subF;
                    }

                    if (biteF != null && useF != null)
                    {
                        _fRulesList = field;
                        _fRuleBite = biteF;
                        _fRuleUse = useF;
                        return true;
                    }
                }
            }
            return false;
        }

        private Button GetButtonFromImageField(FieldInfo field, object instance)
        {
            if (field == null || instance == null) return null;
            var img = field.GetValue(instance) as Image;
            return img != null ? img.GetComponent<Button>() : null;
        }

        private void ProcessFishing(global::WTUIFishingActions ui)
        {
            if (!_reflectionInit) return;

            try
            {
                var bitesList = _fCurrentBites.GetValue(ui) as IList;
                if (bitesList == null || bitesList.Count == 0)
                {
                    _status = Localization.Get("MEMFISH_STATUS_WAIT_FISH");
                    return;
                }

                object currentBiteObj = bitesList[bitesList.Count - 1];
                int currentBiteInt = Convert.ToInt32(currentBiteObj);

                var rulesList = _fRulesList.GetValue(_rulesSourceObj) as IList;
                if (rulesList == null)
                {
                    _status = Localization.Get("MEMFISH_STATUS_EMPTY_RULES");
                    return;
                }

                global::FishingUse requiredReaction = global::FishingUse.None;
                bool matchFound = false;

                foreach (var rule in rulesList)
                {
                    object rBiteVal = _fRuleBite.GetValue(rule);
                    if (Convert.ToInt32(rBiteVal) == currentBiteInt)
                    {
                        requiredReaction = (global::FishingUse)_fRuleUse.GetValue(rule);
                        matchFound = true;
                        break;
                    }
                }

                if (!matchFound)
                {
                    _status = $"{Localization.Get("MEMFISH_STATUS_NO_RULE")} {currentBiteObj}";
                    return;
                }

                Button btnDrag = GetButtonFromImageField(_fBtnDragImg, ui);
                Button btnPull = GetButtonFromImageField(_fBtnPullImg, ui);
                Button btnStrike = GetButtonFromImageField(_fBtnStrikeImg, ui);

                Button targetBtn = null;
                switch (requiredReaction)
                {
                    case global::FishingUse.DragOut: targetBtn = btnDrag; break;
                    case global::FishingUse.Pull: targetBtn = btnPull; break;
                    case global::FishingUse.Strike: targetBtn = btnStrike; break;
                }

                if (targetBtn != null && targetBtn.gameObject.activeSelf)
                {
                    _status = $"[MEMORY] {currentBiteObj} -> {requiredReaction}";

                    if (ConfigManager.MemFish_AutoPress && Time.time > _actionTimer)
                    {
                        if (targetBtn.interactable)
                        {
                            targetBtn.onClick.Invoke();
                            float delay = ConfigManager.MemFish_ReactionTime + UnityEngine.Random.Range(0.05f, 0.15f);
                            _actionTimer = Time.time + delay;
                        }
                    }
                }
                else
                {
                    _status = $"{Localization.Get("FISH_TARGET")}: {requiredReaction} ({Localization.Get("MEMFISH_STATUS_INACTIVE")})";
                }
            }
            catch (Exception ex) { _status = "Run Error: " + ex.Message; }
        }

        public void DrawESP()
        {
            if (!ConfigManager.MemFish_Enabled || !ConfigManager.MemFish_ShowESP) return;
            if (!_reflectionInit) return;

            var ui = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();
            if (ui == null || !ui.gameObject.activeSelf) return;

            try
            {
                var bitesList = _fCurrentBites.GetValue(ui) as IList;
                if (bitesList == null || bitesList.Count == 0) return;

                int currentBiteInt = Convert.ToInt32(bitesList[bitesList.Count - 1]);
                var rulesList = _fRulesList.GetValue(_rulesSourceObj) as IList;
                if (rulesList == null) return;

                global::FishingUse requiredReaction = global::FishingUse.None;
                foreach (var rule in rulesList)
                {
                    if (Convert.ToInt32(_fRuleBite.GetValue(rule)) == currentBiteInt)
                    {
                        requiredReaction = (global::FishingUse)_fRuleUse.GetValue(rule);
                        break;
                    }
                }

                Button targetBtn = null;
                switch (requiredReaction)
                {
                    case global::FishingUse.DragOut: targetBtn = GetButtonFromImageField(_fBtnDragImg, ui); break;
                    case global::FishingUse.Pull: targetBtn = GetButtonFromImageField(_fBtnPullImg, ui); break;
                    case global::FishingUse.Strike: targetBtn = GetButtonFromImageField(_fBtnStrikeImg, ui); break;
                }

                if (targetBtn != null && targetBtn.gameObject.activeSelf)
                {
                    DrawBoxOnButton(targetBtn);
                }
            }
            catch { }
        }

        private void DrawBoxOnButton(Button btn)
        {
            if (btn == null) return;
            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt == null) return;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float x = corners[0].x;
            float y = Screen.height - corners[1].y;
            float w = corners[2].x - corners[0].x;
            float h = corners[1].y - corners[0].y;

            if (_boxTexture == null)
            {
                _boxTexture = new Texture2D(1, 1);
                _boxTexture.SetPixel(0, 0, new Color(1f, 0f, 1f, 0.5f));
                _boxTexture.Apply();
            }

            GUI.DrawTexture(new Rect(x, y, w, h), _boxTexture);

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.magenta;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(x, y - 20, w, 20), "MEMORY HIT", style);
        }

        public void DrawMenu()
        {
            bool esp = GUILayout.Toggle(ConfigManager.MemFish_ShowESP, Localization.Get("MEMFISH_SHOW_ESP"));
            if (esp != ConfigManager.MemFish_ShowESP) { ConfigManager.MemFish_ShowESP = esp; ConfigManager.Save(); }

            bool auto = GUILayout.Toggle(ConfigManager.MemFish_AutoPress, Localization.Get("MEMFISH_AUTO"));
            if (auto != ConfigManager.MemFish_AutoPress) { ConfigManager.MemFish_AutoPress = auto; ConfigManager.Save(); }

            if (ConfigManager.MemFish_AutoPress)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MEMFISH_REACTION")}: {ConfigManager.MemFish_ReactionTime:F2}s");
                float newF = GUILayout.HorizontalSlider(ConfigManager.MemFish_ReactionTime, 0.1f, 1.0f);
                if (Math.Abs(newF - ConfigManager.MemFish_ReactionTime) > 0.01f) { ConfigManager.MemFish_ReactionTime = newF; ConfigManager.Save(); }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"{Localization.Get("LOOT_STATUS")}: {_status}");
        }
    }
}