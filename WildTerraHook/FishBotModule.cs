using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
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

        // Pola UI
        private FieldInfo _fBtnDragImg;
        private FieldInfo _fBtnPullImg;
        private FieldInfo _fBtnStrikeImg;
        private FieldInfo _fFishActions;

        // Dynamiczne uchwyty do danych FishBite
        private FieldInfo _fBiteUseField;     // Jeśli to pole
        private PropertyInfo _pBiteUseProp;   // Jeśli to właściwość

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

                // 1. UI Images (Pola te istnieją na pewno w WTUIFishingActions)
                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);
                _fFishActions = uiType.GetField("fishActions", flags);

                if (_fBtnDragImg == null || _fFishActions == null)
                {
                    _status = "Błąd: Zmiana nazw w UI gry.";
                    return;
                }

                // 2. Analiza klasy FishBite
                Type biteType = typeof(global::FishBite);

                // Szukamy pola lub właściwości typu FishingUse (enum)
                foreach (var field in biteType.GetFields(flags))
                {
                    if (field.FieldType == typeof(global::FishingUse)) { _fBiteUseField = field; break; }
                }

                if (_fBiteUseField == null)
                {
                    foreach (var prop in biteType.GetProperties(flags))
                    {
                        if (prop.PropertyType == typeof(global::FishingUse)) { _pBiteUseProp = prop; break; }
                    }
                }

                // Fallback po nazwach (gdyby typ był inny, np. int)
                if (_fBiteUseField == null && _pBiteUseProp == null)
                {
                    _fBiteUseField = biteType.GetField("action", flags) ?? biteType.GetField("use", flags) ?? biteType.GetField("type", flags);
                }

                if (_fBiteUseField != null || _pBiteUseProp != null)
                {
                    _reflectionInit = true;
                    _status = "Gotowy (Refleksja OK)";
                }
                else
                {
                    // DEBUGGER STRUKTURY: Jeśli nie znaleziono, wypisz co jest w środku
                    StringBuilder sb = new StringBuilder("Błąd struktury FishBite! Dostępne: ");
                    foreach (var f in biteType.GetFields(flags)) sb.Append($"F:{f.Name}({f.FieldType.Name}) ");
                    foreach (var p in biteType.GetProperties(flags)) sb.Append($"P:{p.Name} ");
                    _status = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                _status = "Ref Crash: " + ex.Message;
            }
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
                var actionsList = _fFishActions.GetValue(ui) as System.Collections.IList;
                if (actionsList == null || actionsList.Count == 0)
                {
                    _status = "Czekam na rybę...";
                    return;
                }

                object currentBite = actionsList[actionsList.Count - 1];
                global::FishingUse requiredAction = global::FishingUse.None;

                // Pobieramy wartość akcji (z pola lub właściwości)
                if (_fBiteUseField != null)
                    requiredAction = (global::FishingUse)_fBiteUseField.GetValue(currentBite);
                else if (_pBiteUseProp != null)
                    requiredAction = (global::FishingUse)_pBiteUseProp.GetValue(currentBite, null);

                // Pobieramy przyciski
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
                    _status = $"Nieznany cel dla akcji: {requiredAction}";
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
                global::FishingUse requiredAction = global::FishingUse.None;

                if (_fBiteUseField != null) requiredAction = (global::FishingUse)_fBiteUseField.GetValue(currentBite);
                else if (_pBiteUseProp != null) requiredAction = (global::FishingUse)_pBiteUseProp.GetValue(currentBite, null);

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

                // Pole statusu, które pokaże listę pól w przypadku błędu
                GUILayout.Label($"Status: {_status}", GUILayout.ExpandWidth(true));
            }
        }
    }
}