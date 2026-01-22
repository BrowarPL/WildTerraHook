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

        // --- KLUCZOWE ZMIENNE ---
        private FieldInfo _fCorrectId;   // To jest ID przycisku, który gra uważa za poprawny (0, 1, 2)

        // Przyciski UI
        private FieldInfo _fBtnDragImg;  // Button 0 (Drag)
        private FieldInfo _fBtnPullImg;  // Button 1 (Pull)
        private FieldInfo _fBtnStrikeImg; // Button 2 (Strike)

        // Debugger pól (do znalezienia właściwej zmiennej, jeśli 'correctButtonId' zawiedzie)
        private string _debugFieldList = "";

        public void Update()
        {
            if (!ConfigManager.MemFish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();

            if (fishingUI == null || !fishingUI.gameObject.activeSelf)
            {
                _status = "Oczekiwanie na okno łowienia...";
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

                // 1. Pobieramy przyciski (poprzez Image, bo to najpewniejszy punkt zaczepienia w tym UI)
                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);

                // 2. SZUKAMY ZMIENNEJ "CORRECT ID"
                // W WT2 ta zmienna steruje logiką sukcesu. Może nazywać się:
                // correctButtonId, correctIndex, currentSuccessIndex, activeActionIndex
                _fCorrectId = uiType.GetField("correctButtonId", flags)
                           ?? uiType.GetField("correctIndex", flags)
                           ?? uiType.GetField("currentSuccessIndex", flags)
                           ?? uiType.GetField("correctAction", flags);

                // DEBUG: Jeśli nie znaleźliśmy standardowej nazwy, pobieramy listę wszystkich intów
                if (_fCorrectId == null)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var f in uiType.GetFields(flags))
                    {
                        if (f.FieldType == typeof(int)) sb.Append(f.Name + ", ");
                    }
                    _debugFieldList = sb.ToString();
                    _status = "BŁĄD: Nie znaleziono ID. Dostępne inty: " + _debugFieldList;
                }
                else
                {
                    _reflectionInit = true;
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
            return img != null ? img.GetComponent<Button>() : null; // Image jest zazwyczaj na tym samym obiekcie co Button
        }

        private void ProcessFishing(global::WTUIFishingActions ui)
        {
            if (!_reflectionInit) return;

            try
            {
                // Odczytujemy ID poprawnego przycisku z pamięci
                int winningId = (int)_fCorrectId.GetValue(ui);

                // Pobieramy instancje przycisków
                Button btnDrag = GetButtonFromImageField(_fBtnDragImg, ui);   // ID 0 (zazwyczaj)
                Button btnPull = GetButtonFromImageField(_fBtnPullImg, ui);   // ID 1 (zazwyczaj)
                Button btnStrike = GetButtonFromImageField(_fBtnStrikeImg, ui); // ID 2 (zazwyczaj)

                Button targetBtn = null;
                string actionName = "?";

                // Mapowanie ID na przycisk
                // UWAGA: Kolejność może zależeć od wersji gry. Standardowo:
                // 0 = DragOut, 1 = Pull, 2 = Strike.
                // Jeśli bot klika źle, zamienimy te numerki.
                switch (winningId)
                {
                    case 0: targetBtn = btnDrag; actionName = "Drag (0)"; break;
                    case 1: targetBtn = btnPull; actionName = "Pull (1)"; break;
                    case 2: targetBtn = btnStrike; actionName = "Strike (2)"; break;
                    default:
                        _status = $"Czekam... (ID: {winningId})";
                        return; // ID -1 lub inne oznacza brak akcji
                }

                if (targetBtn != null && targetBtn.gameObject.activeSelf)
                {
                    _status = $"Cel Memory: {actionName}";

                    if (ConfigManager.MemFish_AutoPress && Time.time > _actionTimer)
                    {
                        if (targetBtn.interactable)
                        {
                            targetBtn.onClick.Invoke();
                            // Dodajemy losowość, aby uniknąć wykrycia
                            float delay = ConfigManager.MemFish_ReactionTime + UnityEngine.Random.Range(0.05f, 0.15f);
                            _actionTimer = Time.time + delay;
                        }
                    }
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
                int winningId = (int)_fCorrectId.GetValue(ui);
                Button targetBtn = null;

                switch (winningId)
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
            style.fontSize = 14;
            GUI.Label(new Rect(x, y - 25, w, 25), "MEMORY HIT", style);
        }

        public void DrawMenu()
        {
            // Nowy styl przycisków: 90% szerokości
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Memory Bot (Jaskinie)</b>");

            bool newVal = ToggleBtn(ConfigManager.MemFish_Enabled, "WŁĄCZ (Memory)");
            if (newVal != ConfigManager.MemFish_Enabled)
            {
                ConfigManager.MemFish_Enabled = newVal;
                if (newVal) ConfigManager.ColorFish_Enabled = false;
                ConfigManager.Save();
            }

            if (ConfigManager.MemFish_Enabled)
            {
                bool esp = ToggleBtn(ConfigManager.MemFish_ShowESP, "Pokaż ESP (Fiolet)");
                if (esp != ConfigManager.MemFish_ShowESP) { ConfigManager.MemFish_ShowESP = esp; ConfigManager.Save(); }

                bool auto = ToggleBtn(ConfigManager.MemFish_AutoPress, "Auto Klikanie");
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
                if (!string.IsNullOrEmpty(_debugFieldList))
                    GUILayout.Label($"Pola int: {_debugFieldList}");
            }
            GUILayout.EndVertical();
        }

        // Pomocnicza metoda do szerokich przycisków
        private bool ToggleBtn(bool val, string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            // Przycisk na 90% szerokości dostępnego obszaru (wewnątrz Vertical box)
            bool ret = GUILayout.Toggle(val, text, "button", GUILayout.Width(250), GUILayout.Height(25));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return ret;
        }
    }
}