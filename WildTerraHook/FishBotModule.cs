using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace WildTerraHook
{
    public class FishBotModule
    {
        private float _actionTimer = 0f;
        private Texture2D _boxTexture;
        private string _status = "Init";
        private bool _reflectionInit = false;

        private FieldInfo _fButtons;
        private FieldInfo _fCorrectIndex;
        private Type _fishingUiType;

        public void Update()
        {
            if (!ConfigManager.MemFish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();
            if (fishingUI == null || !fishingUI.gameObject.activeSelf) return;

            if (!_reflectionInit) InitReflection(fishingUI);
            ProcessFishing(fishingUI);
        }

        private void InitReflection(object uiObj)
        {
            try
            {
                _fishingUiType = uiObj.GetType();
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                _fButtons = _fishingUiType.GetField("buttons", flags) ?? _fishingUiType.GetField("slots", flags);
                _fCorrectIndex = _fishingUiType.GetField("correctButtonId", flags)
                              ?? _fishingUiType.GetField("correctIndex", flags)
                              ?? _fishingUiType.GetField("currentSuccessIndex", flags);

                _reflectionInit = true;
            }
            catch { }
        }

        private void ProcessFishing(global::WTUIFishingActions ui)
        {
            if (_fButtons == null || _fCorrectIndex == null)
            {
                _status = "Błąd Reflection";
                return;
            }

            try
            {
                var buttonsObj = _fButtons.GetValue(ui);
                if (buttonsObj == null) return;

                List<Button> btnList = new List<Button>();
                if (buttonsObj is Button[]) btnList.AddRange((Button[])buttonsObj);
                else if (buttonsObj is List<Button>) btnList.AddRange((List<Button>)buttonsObj);

                int correctId = Convert.ToInt32(_fCorrectIndex.GetValue(ui));

                if (correctId >= 0 && correctId < btnList.Count)
                {
                    var targetBtn = btnList[correctId];
                    if (targetBtn != null && targetBtn.gameObject.activeSelf)
                    {
                        _status = "Target: " + targetBtn.name;

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
                }
            }
            catch (Exception ex) { _status = "Err: " + ex.Message; }
        }

        public void DrawESP()
        {
            if (!ConfigManager.MemFish_Enabled || !ConfigManager.MemFish_ShowESP) return;
            if (!_reflectionInit) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();
            if (fishingUI == null || !fishingUI.gameObject.activeSelf) return;

            try
            {
                var buttonsObj = _fButtons.GetValue(fishingUI);
                if (buttonsObj == null) return;

                List<Button> btnList = new List<Button>();
                if (buttonsObj is Button[]) btnList.AddRange((Button[])buttonsObj);
                else if (buttonsObj is List<Button>) btnList.AddRange((List<Button>)buttonsObj);

                int correctId = Convert.ToInt32(_fCorrectIndex.GetValue(fishingUI));

                if (correctId >= 0 && correctId < btnList.Count)
                {
                    DrawBoxOnButton(btnList[correctId]);
                }
            }
            catch { }
        }

        private void DrawBoxOnButton(Button btn)
        {
            if (btn == null) return;
            Vector3[] corners = new Vector3[4];
            btn.GetComponent<RectTransform>().GetWorldCorners(corners);

            float x = corners[0].x;
            float y = Screen.height - corners[1].y;
            float w = corners[2].x - corners[0].x;
            float h = corners[1].y - corners[0].y;

            if (_boxTexture == null)
            {
                _boxTexture = new Texture2D(1, 1);
                _boxTexture.SetPixel(0, 0, new Color(1, 0, 1, 0.4f)); // Fiolet
                _boxTexture.Apply();
            }

            GUI.DrawTexture(new Rect(x, y, w, h), _boxTexture);
        }

        public void DrawMenu()
        {
            bool newVal = GUILayout.Toggle(ConfigManager.MemFish_ShowESP, "Pokaż ESP (Fiolet)");
            if (newVal != ConfigManager.MemFish_ShowESP) { ConfigManager.MemFish_ShowESP = newVal; ConfigManager.Save(); }

            newVal = GUILayout.Toggle(ConfigManager.MemFish_AutoPress, "Auto Klikanie");
            if (newVal != ConfigManager.MemFish_AutoPress) { ConfigManager.MemFish_AutoPress = newVal; ConfigManager.Save(); }

            if (ConfigManager.MemFish_AutoPress)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Reakcja: {ConfigManager.MemFish_ReactionTime:F2}s");
                float newF = GUILayout.HorizontalSlider(ConfigManager.MemFish_ReactionTime, 0.1f, 1.0f);
                if (Math.Abs(newF - ConfigManager.MemFish_ReactionTime) > 0.01f) { ConfigManager.MemFish_ReactionTime = newF; ConfigManager.Save(); }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"Status: {_status}");
        }
    }
}