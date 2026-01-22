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
        private FieldInfo _fCurrentBites; // UI: fishActions

        // --- LOGIKA (REGUŁY) ---
        private object _rulesSourceObj;   // Obiekt, w którym znaleźliśmy listę (UI lub Overview)
        private FieldInfo _fRulesList;    // Pole listy reguł
        private FieldInfo _fRuleBite;     // Pole "Input" w regule
        private FieldInfo _fRuleUse;      // Pole "Output" w regule

        // Cache Typów
        private Type _tFishBite;
        private Type _tFishingUse;

        public void Update()
        {
            if (!ConfigManager.MemFish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();

            if (fishingUI == null || !fishingUI.gameObject.activeSelf)
            {
                _status = "Oczekiwanie na okno...";
                _reflectionInit = false; // Reset przy zamknięciu okna, żeby odświeżyć referencje
                return;
            }

            if (!_reflectionInit) InitReflection(fishingUI);
            ProcessFishing(fishingUI);
        }

        private void InitReflection(global::WTUIFishingActions ui)
        {
            try
            {
                // Cache typów enumów (dla pewności)
                _tFishBite = typeof(global::FishBite);
                _tFishingUse = typeof(global::FishingUse);

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // 1. UI Images (Pewniaki)
                Type uiType = ui.GetType();
                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);
                _fCurrentBites = uiType.GetField("fishActions", flags);

                if (_fBtnDragImg == null || _fCurrentBites == null)
                {
                    _status = "BŁĄD: Zmiana nazw w UI.";
                    return;
                }

                // 2. SZUKANIE LISTY REGUŁ (UI lub Overview)
                // Najpierw szukamy w UI
                if (ScanForRules(ui, uiType, flags))
                {
                    _rulesSourceObj = ui;
                }
                else
                {
                    // Jeśli nie ma w UI, szukamy w Overview
                    var overview = UnityEngine.Object.FindObjectOfType<global::WTFishingOverview>();
                    if (overview != null)
                    {
                        if (ScanForRules(overview, overview.GetType(), flags))
                        {
                            _rulesSourceObj = overview;
                        }
                    }

                    // Fallback: Szukamy w WTWorldFishing (jeśli istnieje taka klasa/obiekt)
                    if (_rulesSourceObj == null)
                    {
                        var worldFishing = UnityEngine.Object.FindObjectOfType<global::WTWorldFishing>();
                        if (worldFishing != null)
                        {
                            if (ScanForRules(worldFishing, worldFishing.GetType(), flags))
                                _rulesSourceObj = worldFishing;
                        }
                    }
                }

                if (_rulesSourceObj != null)
                {
                    _reflectionInit = true;
                    Debug.Log($"[FishBot] Znaleziono reguły w: {_rulesSourceObj.GetType().Name}.{_fRulesList.Name}");
                }
                else
                {
                    _status = "BŁĄD: Nie znaleziono listy FishAction w UI ani Overview!";
                }
            }
            catch (Exception ex)
            {
                _status = "Ref Crash: " + ex.Message;
            }
        }

        private bool ScanForRules(object target, Type type, BindingFlags flags)
        {
            foreach (var field in type.GetFields(flags))
            {
                // Szukamy List<T>
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Type itemType = field.FieldType.GetGenericArguments()[0];
                    // Sprawdzamy czy nazwa typu elementu zawiera "Action" (np. FishAction)
                    if (itemType.Name.Contains("Action"))
                    {
                        // Sprawdzamy wnętrze elementu listy
                        FieldInfo bite = null;
                        FieldInfo use = null;

                        foreach (var f in itemType.GetFields(flags))
                        {
                            // Szukamy po typach (najpewniej)
                            if (f.FieldType == _tFishBite) bite = f;
                            if (f.FieldType == _tFishingUse) use = f;

                            // Fallback po nazwach (jeśli typy to int)
                            if (bite == null && (f.Name.ToLower().Contains("bite") || f.Name.ToLower() == "action")) bite = f;
                            if (use == null && (f.Name.ToLower().Contains("use") || f.Name.ToLower().Contains("correct"))) use = f;
                        }

                        if (bite != null && use != null)
                        {
                            _fRulesList = field;
                            _fRuleBite = bite;
                            _fRuleUse = use;
                            return true;
                        }
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
                // A. Pobierz aktualne zachowanie ryby
                var bitesList = _fCurrentBites.GetValue(ui) as IList;
                if (bitesList == null || bitesList.Count == 0)
                {
                    _status = "Czekam na rybę...";
                    return;
                }
                // Ostatnia akcja (FishBite)
                object currentBiteObj = bitesList[bitesList.Count - 1];
                int currentBiteInt = Convert.ToInt32(currentBiteObj); // Rzutowanie Enum -> int

                // B. Pobierz listę reguł z znalezionego źródła
                var rulesList = _fRulesList.GetValue(_rulesSourceObj) as IList;
                if (rulesList == null)
                {
                    _status = "Lista reguł jest null!";
                    return;
                }

                // C. Dopasowanie
                global::FishingUse requiredReaction = global::FishingUse.None;
                bool match = false;

                foreach (var rule in rulesList)
                {
                    // Odczytaj FishBite z reguły
                    object rBite = _fRuleBite.GetValue(rule);
                    int rBiteInt = Convert.ToInt32(rBite);

                    if (rBiteInt == currentBiteInt)
                    {
                        // Mamy parę! Odczytaj FishingUse
                        requiredReaction = (global::FishingUse)_fRuleUse.GetValue(rule);
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    _status = $"Nieznana reguła dla: {currentBiteObj}";
                    return;
                }

                // D. Wykonanie
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
                    _status = $"[Auto] Ryba: {currentBiteObj} => Klik: {requiredReaction}";

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
                    _status = $"Cel: {requiredReaction} (Nieaktywny)";
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
            GUI.Label(new Rect(x, y - 20, w, 20), "HIT!", style);
        }

        public void DrawMenu()
        {
            // Opcje rysowane w GUI modułu
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