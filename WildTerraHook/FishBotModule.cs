using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;

namespace WildTerraHook
{
    public class FishBotModule
    {
        private float _actionTimer = 0f;
        private Texture2D _boxTexture;
        private string _status = "Init";
        private bool _reflectionInit = false;

        // Pola UI (Obrazki, z których pobierzemy przyciski)
        private FieldInfo _fBtnDragImg;
        private FieldInfo _fBtnPullImg;
        private FieldInfo _fBtnStrikeImg;

        // Pola Danych (logika gry)
        private FieldInfo _fFishActions;
        private FieldInfo _fBiteUse;

        public void Update()
        {
            if (!ConfigManager.MemFish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();

            if (fishingUI == null || !fishingUI.gameObject.activeSelf)
            {
                _status = "Czekam na UI...";
                return;
            }

            if (!_reflectionInit) InitReflection(fishingUI);
            ProcessFishing(fishingUI);
        }

        private void InitReflection(object uiObj)
        {
            try
            {
                Type uiType = uiObj.GetType();
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // 1. Pobieramy OBRAZKI przycisków (bo te pola istnieją na pewno)
                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);

                // 2. Pobieramy listę akcji
                _fFishActions = uiType.GetField("fishActions", flags);

                // 3. Szukamy pola typu FishingUse w klasie FishBite
                Type biteType = typeof(global::FishBite);
                foreach (var field in biteType.GetFields(flags))
                {
                    if (field.FieldType == typeof(global::FishingUse))
                    {
                        _fBiteUse = field;
                        break;
                    }
                }

                // Fallback nazwy
                if (_fBiteUse == null)
                    _fBiteUse = biteType.GetField("use", flags) ?? biteType.GetField("action", flags);

                if (_fBtnDragImg == null || _fBtnPullImg == null || _fBtnStrikeImg == null)
                    _status = "Błąd: Brak pól Image";
                else if (_fFishActions == null)
                    _status = "Błąd: Brak listy akcji";
                else if (_fBiteUse == null)
                    _status = "Błąd: Nieznana struktura FishBite";
                else
                    _reflectionInit = true;
            }
            catch (Exception ex)
            {
                _status = "Ref Crash: " + ex.Message;
            }
        }

        // Pomocnicza metoda do wyciągania przycisku z pola Image
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
                var actionsList = _fFishActions.GetValue(ui) as System.Collections.IList;
                if (actionsList == null || actionsList.Count == 0)
                {
                    _status = "Czekam na rybę...";
                    return;
                }

                object currentBite = actionsList[actionsList.Count - 1];
                global::FishingUse requiredAction = (global::FishingUse)_fBiteUse.GetValue(currentBite);

                // Pobieramy przyciski z obrazków
                Button btnDrag = GetButtonFromImageField(_fBtnDragImg, ui);
                Button btnPull = GetButtonFromImageField(_fBtnPullImg, ui);
                Button btnStrike = GetButtonFromImageField(_fBtnStrikeImg, ui);

                Button targetBtn = null;
                switch (requiredAction)
                {
                    case global::FishingUse.DragOut: targetBtn = btnDrag; break;
                    case global::FishingUse.Pull: targetBtn = btnPull; break;
                    case global::FishingUse.Strike: targetBtn = btnStrike; break;
                }

                if (targetBtn != null && targetBtn.gameObject.activeSelf)
                {
                    _status = $"Cel: {targetBtn.name} ({requiredAction})";

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
                    _status = "Błąd celu (Button null?)";
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
                var actionsList = _fFishActions.GetValue(ui) as System.Collections.IList;
                if (actionsList == null || actionsList.Count == 0) return;

                object currentBite = actionsList[actionsList.Count - 1];
                global::FishingUse requiredAction = (global::FishingUse)_fBiteUse.GetValue(currentBite);

                Button targetBtn = null;
                switch (requiredAction)
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
                _boxTexture.SetPixel(0, 0, new Color(1f, 0f, 1f, 0.5f)); // Fioletowy
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
            GUILayout.Label("<b>Memory Bot (Jaskinie)</b>");

            bool newVal = GUILayout.Toggle(ConfigManager.MemFish_Enabled, "Włącz (Memory)");
            if (newVal != ConfigManager.MemFish_Enabled)
            {
                ConfigManager.MemFish_Enabled = newVal;
                if (newVal) ConfigManager.ColorFish_Enabled = false;
                ConfigManager.Save();
            }

            if (ConfigManager.MemFish_Enabled)
            {
                bool esp = GUILayout.Toggle(ConfigManager.MemFish_ShowESP, "Pokaż ESP (Fiolet)");
                if (esp != ConfigManager.MemFish_ShowESP) { ConfigManager.MemFish_ShowESP = esp; ConfigManager.Save(); }

                bool auto = GUILayout.Toggle(ConfigManager.MemFish_AutoPress, "Auto Klikanie");
                if (auto != ConfigManager.MemFish_AutoPress) { ConfigManager.MemFish_AutoPress = auto; ConfigManager.Save(); }

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
}