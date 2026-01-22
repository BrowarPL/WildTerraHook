using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WildTerraHook
{
    public class ColorFishingModule
    {
        private float _actionTimer = 0f;
        private Texture2D _boxTexture;
        private string _status = "Idle";

        public void Update()
        {
            if (!ConfigManager.ColorFish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();
            if (fishingUI == null || !fishingUI.gameObject.activeSelf) return;

            ProcessFishing(fishingUI);
        }

        private void ProcessFishing(global::WTUIFishingActions ui)
        {
            var buttons = ui.GetComponentsInChildren<Button>();
            if (buttons == null || buttons.Length == 0) return;

            foreach (var btn in buttons)
            {
                if (!btn.gameObject.activeSelf || !btn.interactable) continue;

                var img = btn.GetComponent<Image>();
                if (img != null && IsGreen(img.color))
                {
                    _status = "Target: " + btn.name;

                    if (ConfigManager.ColorFish_AutoPress && Time.time > _actionTimer)
                    {
                        btn.onClick.Invoke();
                        float delay = ConfigManager.ColorFish_ReactionTime + UnityEngine.Random.Range(0.05f, 0.15f);
                        _actionTimer = Time.time + delay;
                    }
                }
            }
        }

        private bool IsGreen(Color c)
        {
            return c.g > 0.5f && c.r < 0.5f && c.b < 0.5f;
        }

        public void DrawESP()
        {
            if (!ConfigManager.ColorFish_Enabled || !ConfigManager.ColorFish_ShowESP) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();
            if (fishingUI == null || !fishingUI.gameObject.activeSelf) return;

            var buttons = fishingUI.GetComponentsInChildren<Button>();
            foreach (var btn in buttons)
            {
                var img = btn.GetComponent<Image>();
                if (img != null && IsGreen(img.color) && btn.gameObject.activeSelf)
                {
                    DrawBoxOnButton(btn);
                }
            }
        }

        private void DrawBoxOnButton(Button btn)
        {
            Vector3[] corners = new Vector3[4];
            btn.GetComponent<RectTransform>().GetWorldCorners(corners);

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

        public void DrawMenu()
        {
            // Ten moduł jest teraz rysowany bezpośrednio w MainHack w zakładce
            // Ale zostawiamy metodę pomocniczą
            bool newVal = GUILayout.Toggle(ConfigManager.ColorFish_ShowESP, "Pokaż ESP (Zielony)");
            if (newVal != ConfigManager.ColorFish_ShowESP) { ConfigManager.ColorFish_ShowESP = newVal; ConfigManager.Save(); }

            newVal = GUILayout.Toggle(ConfigManager.ColorFish_AutoPress, "Auto Klikanie");
            if (newVal != ConfigManager.ColorFish_AutoPress) { ConfigManager.ColorFish_AutoPress = newVal; ConfigManager.Save(); }

            if (ConfigManager.ColorFish_AutoPress)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Reakcja: {ConfigManager.ColorFish_ReactionTime:F2}s");
                float newF = GUILayout.HorizontalSlider(ConfigManager.ColorFish_ReactionTime, 0.1f, 1.0f);
                if (Math.Abs(newF - ConfigManager.ColorFish_ReactionTime) > 0.01f) { ConfigManager.ColorFish_ReactionTime = newF; ConfigManager.Save(); }
                GUILayout.EndHorizontal();
            }
        }
    }
}