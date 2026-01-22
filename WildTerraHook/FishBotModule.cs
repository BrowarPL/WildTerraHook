using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using System.Collections;

namespace WildTerraHook
{
    public class FishBotModule
    {
        private float _actionTimer = 0f;
        private Texture2D _boxTexture;
        private string _status = "Init";
        private bool _reflectionInit = false;

        // Pola UI (Obrazki)
        private FieldInfo _fBtnDragImg;
        private FieldInfo _fBtnPullImg;
        private FieldInfo _fBtnStrikeImg;

        // Lista akcji
        private FieldInfo _fFishActions;

        public void Update()
        {
            if (!ConfigManager.MemFish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();

            if (fishingUI == null || !fishingUI.gameObject.activeSelf)
            {
                _status = "Oczekiwanie na okno...";
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

                // 1. UI Images - Te pola istnieją w WTUIFishingActions
                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);

                // 2. Lista akcji - To jest lista typu List<FishBite> (gdzie FishBite to Enum : byte)
                _fFishActions = uiType.GetField("fishActions", flags);

                if (_fBtnDragImg == null || _fFishActions == null)
                    _status = "BŁĄD STRUKTURY GRY (Refleksja)";
                else
                    _reflectionInit = true;
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
                // Pobierz listę
                var list = _fFishActions.GetValue(ui) as IList;

                if (list == null || list.Count == 0)
                {
                    _status = "Oczekiwanie na rybę...";
                    return;
                }

                // Ostatni element to aktualna akcja
                object currentActionObj = list[list.Count - 1];

                // Ponieważ FishBite to Enum (byte), możemy to rzutować na int
                int actionId = Convert.ToInt32(currentActionObj);

                // Pobierz przyciski
                Button btnDrag = GetButtonFromImageField(_fBtnDragImg, ui);
                Button btnPull = GetButtonFromImageField(_fBtnPullImg, ui);
                Button btnStrike = GetButtonFromImageField(_fBtnStrikeImg, ui);

                Button targetBtn = null;
                string actionName = actionId.ToString();

                // MAPOWANIE: Enum zazwyczaj idzie w kolejności definicji.
                // 0 = DragOut, 1 = Pull, 2 = Strike (Standardowo w tej grze)

                switch (actionId)
                {
                    case 0: // DragOut
                        targetBtn = btnDrag;
                        actionName = "Drag (0)";
                        break;
                    case 1: // Pull
                        targetBtn = btnPull;
                        actionName = "Pull (1)";
                        break;
                    case 2: // Strike
                        targetBtn = btnStrike;
                        actionName = "Strike (2)";
                        break;
                    default:
                        _status = $"Nieznane ID akcji: {actionId}";
                        return;
                }

                if (targetBtn != null && targetBtn.gameObject.activeSelf)
                {
                    _status = $"Cel: {actionName}";

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
                    _status = $"Przycisk dla {actionName} nieaktywny";
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
                var list = _fFishActions.GetValue(ui) as IList;
                if (list == null || list.Count == 0) return;

                int actionId = Convert.ToInt32(list[list.Count - 1]);
                Button targetBtn = null;

                switch (actionId)
                {
                    case 0: targetBtn = GetButtonFromImageField(_fBtnDragImg, ui); break;
                    case 1: targetBtn = GetButtonFromImageField(_fBtnPullImg, ui); break;
                    case 2: targetBtn = GetButtonFromImageField(_fBtnStrikeImg, ui); break;
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
            GUI.Label(new Rect(x, y - 20, w, 20), "HIT!", style);
        }

        // Ta metoda rysuje TYLKO opcje (nagłówek i toggle rysuje MainHack)
        public void DrawMenu()
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