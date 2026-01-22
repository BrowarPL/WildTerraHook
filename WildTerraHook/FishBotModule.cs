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

        // Pola UI (Obrazki)
        private FieldInfo _fBtnDragImg;
        private FieldInfo _fBtnPullImg;
        private FieldInfo _fBtnStrikeImg;

        // Zmienna decyzyjna (Szukamy po typie FishingUse lub int)
        private FieldInfo _fTargetActionEnum; // Typ: FishingUse
        private FieldInfo _fTargetIndexInt;   // Typ: int (jako fallback)

        // Debugger (Do wyświetlania wartości na żywo)
        private List<FieldInfo> _debugEnumFields = new List<FieldInfo>();
        private List<FieldInfo> _debugIntFields = new List<FieldInfo>();

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

                // 1. UI Images (Pola te istnieją na pewno, bo używa ich ColorBot)
                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);

                if (_fBtnDragImg == null)
                {
                    _status = "KRYTYCZNE: Nie znaleziono przycisków w UI.";
                    return;
                }

                // 2. SKANOWANIE TYPÓW (Szukamy "Prawdy")
                _debugEnumFields.Clear();
                _debugIntFields.Clear();

                foreach (var f in uiType.GetFields(flags))
                {
                    // Szukamy pola typu FishingUse (to najlepszy kandydat)
                    if (f.FieldType == typeof(global::FishingUse))
                    {
                        _debugEnumFields.Add(f);
                        // Preferujemy nazwy sugerujące "Current", "Target", "Required"
                        string n = f.Name.ToLower();
                        if (n.Contains("current") || n.Contains("target") || n.Contains("req") || n.Contains("action"))
                            _fTargetActionEnum = f;
                    }
                    // Szukamy intów (jako pomocnicze)
                    else if (f.FieldType == typeof(int))
                    {
                        _debugIntFields.Add(f);
                        // Typowe nazwy dla indeksu przycisku
                        string n = f.Name.ToLower();
                        if (n.Contains("correct") || n.Contains("success") || n.Contains("active"))
                            _fTargetIndexInt = f;
                    }
                }

                // Jeśli nie znaleziono idealnegoandydata, bierzemy pierwszy z brzegu FishingUse
                if (_fTargetActionEnum == null && _debugEnumFields.Count > 0)
                    _fTargetActionEnum = _debugEnumFields[0];

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
                // Zbieramy przyciski
                Button btnDrag = GetButtonFromImageField(_fBtnDragImg, ui);
                Button btnPull = GetButtonFromImageField(_fBtnPullImg, ui);
                Button btnStrike = GetButtonFromImageField(_fBtnStrikeImg, ui);

                Button targetBtn = null;
                string debugInfo = "";

                // STRATEGIA 1: Użycie Enum FishingUse (Najbardziej pewna)
                if (_fTargetActionEnum != null)
                {
                    global::FishingUse action = (global::FishingUse)_fTargetActionEnum.GetValue(ui);
                    debugInfo += $"Enum[{_fTargetActionEnum.Name}]: {action} ";

                    switch (action)
                    {
                        case global::FishingUse.DragOut: targetBtn = btnDrag; break;
                        case global::FishingUse.Pull: targetBtn = btnPull; break;
                        case global::FishingUse.Strike: targetBtn = btnStrike; break;
                    }
                }
                // STRATEGIA 2: Użycie Int (0, 1, 2)
                else if (_fTargetIndexInt != null)
                {
                    int idx = (int)_fTargetIndexInt.GetValue(ui);
                    debugInfo += $"Int[{_fTargetIndexInt.Name}]: {idx} ";

                    switch (idx)
                    {
                        case 0: targetBtn = btnDrag; break;
                        case 1: targetBtn = btnPull; break;
                        case 2: targetBtn = btnStrike; break;
                    }
                }
                // DEBUG: Jeśli nic nie wybrano, wyświetl podgląd zmiennych
                else
                {
                    StringBuilder sb = new StringBuilder("Szukam... ");
                    foreach (var f in _debugEnumFields) sb.Append($"{f.Name}={f.GetValue(ui)} ");
                    foreach (var f in _debugIntFields) sb.Append($"{f.Name}={f.GetValue(ui)} ");
                    _status = sb.ToString();
                    return;
                }

                // AKCJA
                if (targetBtn != null && targetBtn.gameObject.activeSelf)
                {
                    _status = $"Cel: {targetBtn.name} | {debugInfo}";

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
                    // Jeśli przycisk nie jest aktywny, może to być faza oczekiwania
                    _status = $"Czekam... | {debugInfo}";
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
                Button targetBtn = null;

                // Powtórzenie logiki wyboru
                if (_fTargetActionEnum != null)
                {
                    global::FishingUse action = (global::FishingUse)_fTargetActionEnum.GetValue(ui);
                    switch (action)
                    {
                        case global::FishingUse.DragOut: targetBtn = GetButtonFromImageField(_fBtnDragImg, ui); break;
                        case global::FishingUse.Pull: targetBtn = GetButtonFromImageField(_fBtnPullImg, ui); break;
                        case global::FishingUse.Strike: targetBtn = GetButtonFromImageField(_fBtnStrikeImg, ui); break;
                    }
                }
                else if (_fTargetIndexInt != null)
                {
                    int idx = (int)_fTargetIndexInt.GetValue(ui);
                    switch (idx)
                    {
                        case 0: targetBtn = GetButtonFromImageField(_fBtnDragImg, ui); break;
                        case 1: targetBtn = GetButtonFromImageField(_fBtnPullImg, ui); break;
                        case 2: targetBtn = GetButtonFromImageField(_fBtnStrikeImg, ui); break;
                    }
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
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Memory Bot (Jaskinie)</b>");

            bool newVal = DrawWideToggle(ConfigManager.MemFish_Enabled, "Włącz (Memory)");
            if (newVal != ConfigManager.MemFish_Enabled)
            {
                ConfigManager.MemFish_Enabled = newVal;
                if (newVal) ConfigManager.ColorFish_Enabled = false;
                ConfigManager.Save();
            }

            if (ConfigManager.MemFish_Enabled)
            {
                bool esp = DrawWideToggle(ConfigManager.MemFish_ShowESP, "Pokaż ESP (Fiolet)");
                if (esp != ConfigManager.MemFish_ShowESP) { ConfigManager.MemFish_ShowESP = esp; ConfigManager.Save(); }

                bool auto = DrawWideToggle(ConfigManager.MemFish_AutoPress, "Auto Klikanie");
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
            GUILayout.EndVertical();
        }

        private bool DrawWideToggle(bool val, string text)
        {
            // Pomocnicza metoda do rysowania przycisków na całą dostępną szerokość wertykalnego boxa
            return GUILayout.Toggle(val, text, "button", GUILayout.Height(30));
        }
    }
}