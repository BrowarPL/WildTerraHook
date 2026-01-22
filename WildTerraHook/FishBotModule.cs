using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using System.Text;
using System.Collections;

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

        // --- DANE GRY ---
        private FieldInfo _fCurrentBites; // "fishActions" - lista aktualnych ruchów ryby (List<FishBite>)

        // --- REGUŁY (KLUCZ DO JASKINI) ---
        private FieldInfo _fRulesList;    // Lista reguł na tę sesję (List<FishAction>)
        private Type _tFishAction;        // Typ klasy FishAction
        private FieldInfo _fRuleBite;     // Pole w FishAction: "Jaki ruch ryby?" (FishBite)
        private FieldInfo _fRuleUse;      // Pole w FishAction: "Jaka reakcja?" (FishingUse)

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

                // 1. UI Images (Przyciski)
                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);

                // 2. Aktualne zachowanie ryby (To co widzi gracz)
                _fCurrentBites = uiType.GetField("fishActions", flags);

                // 3. SZUKANIE LISTY REGUŁ (List<FishAction>)
                // Szukamy pola, które jest Listą i zawiera obiekty klasy 'FishAction'
                foreach (var field in uiType.GetFields(flags))
                {
                    if (field.FieldType.IsGenericType &&
                        field.FieldType.GetGenericTypeDefinition() == typeof(List<>) &&
                        field.FieldType.GetGenericArguments()[0].Name == "FishAction")
                    {
                        _fRulesList = field;
                        _tFishAction = field.FieldType.GetGenericArguments()[0];
                        break;
                    }
                }

                if (_fRulesList == null)
                {
                    _status = "BŁĄD: Nie znaleziono listy reguł (List<FishAction>)!";
                    return;
                }

                // 4. ANALIZA STRUKTURY FishAction
                // Musimy znaleźć w środku pola typu FishBite (Input) i FishingUse (Output)
                foreach (var f in _tFishAction.GetFields(flags))
                {
                    if (f.FieldType.Name == "FishBite") _fRuleBite = f;
                    if (f.FieldType.Name == "FishingUse") _fRuleUse = f;
                }

                if (_fRuleBite == null || _fRuleUse == null)
                {
                    _status = "BŁĄD: Klasa FishAction ma inną strukturę.";
                }
                else
                {
                    _reflectionInit = true;
                    Debug.Log("[FishBot] Reflection Success: Znaleziono reguły gry.");
                }
            }
            catch (Exception ex)
            {
                _status = "Crash: " + ex.Message;
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
                // A. Pobierz aktualny stan (co robi ryba?)
                var bitesList = _fCurrentBites.GetValue(ui) as IList;
                if (bitesList == null || bitesList.Count == 0)
                {
                    _status = "Czekam na rybę...";
                    return;
                }
                // Ostatni element to aktualna akcja ryby (FishBite)
                object currentBiteObj = bitesList[bitesList.Count - 1];
                // Rzutujemy na int, żeby łatwo porównywać (Enumy to inty/byte pod spodem)
                int currentBiteInt = Convert.ToInt32(currentBiteObj);


                // B. Pobierz listę reguł (instrukcja obsługi na tę sesję)
                var rulesList = _fRulesList.GetValue(ui) as IList;
                if (rulesList == null) return;


                // C. Znajdź odpowiednią reakcję w regułach
                global::FishingUse requiredReaction = global::FishingUse.None;
                bool ruleFound = false;

                foreach (var rule in rulesList)
                {
                    // Czy ta reguła dotyczy aktualnego zachowania ryby?
                    object ruleBiteObj = _fRuleBite.GetValue(rule);
                    int ruleBiteInt = Convert.ToInt32(ruleBiteObj);

                    if (ruleBiteInt == currentBiteInt)
                    {
                        // Tak! To jest ta sytuacja. Jaka jest wymagana reakcja?
                        requiredReaction = (global::FishingUse)_fRuleUse.GetValue(rule);
                        ruleFound = true;
                        break;
                    }
                }

                if (!ruleFound)
                {
                    _status = $"Brak reguły dla {currentBiteObj}";
                    return;
                }

                // D. Wykonaj akcję
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
                    _status = $"Znalazłem Regułę! {currentBiteObj} -> {requiredReaction}";

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
                    _status = $"Wymagane: {requiredReaction} (Przycisk nieaktywny)";
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
                // Powtórzenie logiki dla ESP
                var bitesList = _fCurrentBites.GetValue(ui) as IList;
                if (bitesList == null || bitesList.Count == 0) return;

                object currentBiteObj = bitesList[bitesList.Count - 1];
                int currentBiteInt = Convert.ToInt32(currentBiteObj);

                var rulesList = _fRulesList.GetValue(ui) as IList;
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