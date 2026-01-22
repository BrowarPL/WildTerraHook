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

        // --- DANE ---
        private FieldInfo _fCurrentBites; // To co robi ryba teraz (List<FishBite>)

        // --- REGUŁY (Klucz do Jaskini) ---
        // Szukamy dowolnej listy, której elementy mają strukturę { FishBite, FishingUse }
        private FieldInfo _fRulesList;
        private FieldInfo _fRuleBite;     // Pole wewnątrz elementu listy: FishBite
        private FieldInfo _fRuleUse;      // Pole wewnątrz elementu listy: FishingUse

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

                // 1. UI Images (Pola istnieją na pewno)
                _fBtnDragImg = uiType.GetField("dragOutActionButtonImage", flags);
                _fBtnPullImg = uiType.GetField("pullActionButtonImage", flags);
                _fBtnStrikeImg = uiType.GetField("strikeActionButtonImage", flags);

                // 2. Aktualna akcja ryby
                _fCurrentBites = uiType.GetField("fishActions", flags);

                if (_fCurrentBites == null) { _status = "Błąd: Brak fishActions"; return; }

                // 3. SKANER STRUKTURALNY (Szukamy listy reguł)
                bool foundRules = false;

                // Przeglądamy wszystkie pola w UI
                foreach (var field in uiType.GetFields(flags))
                {
                    Type fType = field.FieldType;
                    Type itemType = null;

                    // Sprawdzamy czy to Lista lub Tablica
                    if (fType.IsGenericType && fType.GetGenericTypeDefinition() == typeof(List<>))
                        itemType = fType.GetGenericArguments()[0];
                    else if (fType.IsArray)
                        itemType = fType.GetElementType();

                    if (itemType != null)
                    {
                        // Analizujemy co siedzi w środku tej listy
                        FieldInfo biteF = null;
                        FieldInfo useF = null;

                        foreach (var subF in itemType.GetFields(flags))
                        {
                            if (subF.FieldType == typeof(global::FishBite)) biteF = subF;
                            if (subF.FieldType == typeof(global::FishingUse)) useF = subF;
                        }

                        // Jeśli element listy zawiera OBA typy (CoRybaRobi i CoKliknąć), to znaleźliśmy Reguły!
                        if (biteF != null && useF != null)
                        {
                            _fRulesList = field;
                            _fRuleBite = biteF;
                            _fRuleUse = useF;
                            foundRules = true;
                            Debug.Log($"[FishBot] Znaleziono reguły w polu: {field.Name} (Typ: {itemType.Name})");
                            break;
                        }
                    }
                }

                if (!foundRules)
                {
                    _status = "BŁĄD: Nie znaleziono listy reguł w pamięci!";
                }
                else
                {
                    _reflectionInit = true;
                    _status = "Gotowy (Reguły załadowane)";
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
                // A. Pobierz co robi ryba (ostatnia akcja z listy)
                var bitesList = _fCurrentBites.GetValue(ui) as IList;
                if (bitesList == null || bitesList.Count == 0)
                {
                    _status = "Czekam na rybę...";
                    return;
                }
                object currentBiteObj = bitesList[bitesList.Count - 1];

                // Konwertujemy Enum na int dla łatwego porównania
                int currentBiteInt = Convert.ToInt32(currentBiteObj);


                // B. Pobierz listę reguł (instrukcja obsługi)
                var rulesList = _fRulesList.GetValue(ui) as IList;
                if (rulesList == null)
                {
                    _status = "Lista reguł jest pusta!";
                    return;
                }

                // C. Dopasuj regułę
                global::FishingUse requiredReaction = global::FishingUse.None;
                bool foundMatch = false;

                foreach (var rule in rulesList)
                {
                    object ruleBiteObj = _fRuleBite.GetValue(rule);
                    int ruleBiteInt = Convert.ToInt32(ruleBiteObj);

                    // Jeśli ta reguła opisuje aktualny ruch ryby...
                    if (ruleBiteInt == currentBiteInt)
                    {
                        // ...to pobierz przypisaną reakcję
                        requiredReaction = (global::FishingUse)_fRuleUse.GetValue(rule);
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    _status = $"Brak instrukcji dla ruchu: {currentBiteObj}";
                    return;
                }

                // D. Wykonaj
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
                    _status = $"[MEMORY] Ryba: {currentBiteObj} -> Klik: {requiredReaction}";

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
                // Powtórzenie logiki wyszukiwania dla ESP
                var bitesList = _fCurrentBites.GetValue(ui) as IList;
                if (bitesList == null || bitesList.Count == 0) return;

                int currentBiteInt = Convert.ToInt32(bitesList[bitesList.Count - 1]);
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
            // Przyciski w menu Memory Bota (logikę wyboru bota zostawiamy w MainHack, tutaj tylko opcje)

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