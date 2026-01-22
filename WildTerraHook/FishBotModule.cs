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

        // Pola UI (przyciski)
        private FieldInfo _fBtnDrag;
        private FieldInfo _fBtnPull;
        private FieldInfo _fBtnStrike;

        // Pola Danych (logika gry)
        private FieldInfo _fFishActions; // List<FishBite>

        public void Update()
        {
            if (!ConfigManager.MemFish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();

            // Jeśli UI nie istnieje lub jest wyłączone, resetujemy stan
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
                Type type = uiObj.GetType();
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // 1. Pobieramy przyciski po ich konkretnych nazwach w kodzie gry
                _fBtnDrag = type.GetField("dragOutActionButton", flags);
                _fBtnPull = type.GetField("pullActionButton", flags);
                _fBtnStrike = type.GetField("strikeActionButton", flags);

                // 2. Pobieramy listę akcji (to są dane z serwera, co trzeba kliknąć)
                _fFishActions = type.GetField("fishActions", flags);

                if (_fBtnDrag == null || _fBtnPull == null || _fBtnStrike == null)
                    _status = "Błąd: Nie znaleziono przycisków";
                else if (_fFishActions == null)
                    _status = "Błąd: Nie znaleziono listy akcji";
                else
                    _reflectionInit = true;
            }
            catch (Exception ex)
            {
                _status = "Ref Crash: " + ex.Message;
            }
        }

        private void ProcessFishing(global::WTUIFishingActions ui)
        {
            if (!_reflectionInit) return;

            try
            {
                // Pobierz listę aktywnych 'ugryzień' (akcji do wykonania)
                var actionsList = _fFishActions.GetValue(ui) as List<global::FishBite>;

                if (actionsList == null || actionsList.Count == 0)
                {
                    _status = "Czekam na rybę...";
                    return;
                }

                // Ostatnia akcja na liście to ta aktualna
                global::FishBite currentBite = actionsList[actionsList.Count - 1];

                // Pobierz przyciski z UI
                Button btnDrag = _fBtnDrag.GetValue(ui) as Button;
                Button btnPull = _fBtnPull.GetValue(ui) as Button;
                Button btnStrike = _fBtnStrike.GetValue(ui) as Button;

                Button targetBtn = null;

                // Wybierz przycisk na podstawie typu akcji z pamięci
                switch (currentBite.action)
                {
                    case global::FishingUse.DragOut:
                        targetBtn = btnDrag;
                        break;
                    case global::FishingUse.Pull:
                        targetBtn = btnPull;
                        break;
                    case global::FishingUse.Strike:
                        targetBtn = btnStrike;
                        break;
                }

                if (targetBtn != null && targetBtn.gameObject.activeSelf)
                {
                    _status = $"Cel: {targetBtn.name} ({currentBite.action})";

                    // AUTO PRESS
                    if (ConfigManager.MemFish_AutoPress && Time.time > _actionTimer)
                    {
                        if (targetBtn.interactable)
                        {
                            targetBtn.onClick.Invoke();
                            // Losowy czas reakcji dla bezpieczeństwa
                            float delay = ConfigManager.MemFish_ReactionTime + UnityEngine.Random.Range(0.05f, 0.15f);
                            _actionTimer = Time.time + delay;
                        }
                    }
                }
                else
                {
                    _status = "Błąd celu";
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
                // Powtórzenie logiki wyboru przycisku dla ESP
                var actionsList = _fFishActions.GetValue(ui) as List<global::FishBite>;
                if (actionsList == null || actionsList.Count == 0) return;

                global::FishBite currentBite = actionsList[actionsList.Count - 1];

                Button targetBtn = null;
                switch (currentBite.action)
                {
                    case global::FishingUse.DragOut: targetBtn = _fBtnDrag.GetValue(ui) as Button; break;
                    case global::FishingUse.Pull: targetBtn = _fBtnPull.GetValue(ui) as Button; break;
                    case global::FishingUse.Strike: targetBtn = _fBtnStrike.GetValue(ui) as Button; break;
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
            // Pobieramy RectTransform, żeby znać pozycję na ekranie
            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt == null) return;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            // Konwersja WorldCorners na współrzędne GUI (Y odwrócone)
            float x = corners[0].x;
            float y = Screen.height - corners[1].y;
            float w = corners[2].x - corners[0].x;
            float h = corners[1].y - corners[0].y;

            if (_boxTexture == null)
            {
                _boxTexture = new Texture2D(1, 1);
                _boxTexture.SetPixel(0, 0, new Color(1f, 0f, 1f, 0.5f)); // Fioletowy (Magenta) dla Memory Bota
                _boxTexture.Apply();
            }

            GUI.DrawTexture(new Rect(x, y, w, h), _boxTexture);

            // Opcjonalny napis nad ramką
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
                // Wyłączamy drugi bot, żeby się nie gryzły
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