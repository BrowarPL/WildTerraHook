using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class FishBotModule
    {
        // --- Cache Zmiennych ---
        private float _actionTimer = 0f;
        private Type _fishingUiType;
        private FieldInfo _fButtons;      // Lista przycisków na ekranie
        private FieldInfo _fCorrectIndex; // Indeks poprawnego przycisku (hipoteza 1)
        private FieldInfo _fCurrentAction; // Obecna wymagana akcja (hipoteza 2)

        private bool _reflectionInit = false;
        private string _debugStatus = "Init";

        // --- GUI ---
        private Texture2D _boxTexture;
        private GUIStyle _styleBox;

        public void Update()
        {
            if (!ConfigManager.Fish_Enabled) return;
            if (global::Player.localPlayer == null) return;

            // Sprawdź czy okno łowienia istnieje
            // WTUIFishingActions to zazwyczaj Singleton lub łatwy do znalezienia obiekt
            var fishingUI = UnityEngine.Object.FindObjectOfType<global::WTUIFishingActions>();

            if (fishingUI == null || !fishingUI.gameObject.activeSelf)
            {
                _debugStatus = "Brak UI";
                return;
            }

            if (!_reflectionInit) InitReflection(fishingUI);

            ProcessFishing(fishingUI);
        }

        private void InitReflection(object uiObj)
        {
            try
            {
                _fishingUiType = uiObj.GetType();
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Próba znalezienia listy przycisków (buttons, slots etc.)
                _fButtons = _fishingUiType.GetField("buttons", flags) ?? _fishingUiType.GetField("slots", flags);

                // Próba znalezienia zmiennej "Poprawny Wybór"
                // W WT2 często jest to 'correctId', 'currentStep', 'targetAction'
                _fCorrectIndex = _fishingUiType.GetField("correctButtonId", flags)
                              ?? _fishingUiType.GetField("correctIndex", flags)
                              ?? _fishingUiType.GetField("currentSuccessIndex", flags);

                // Alternatywa: Sprawdź "Current Action" w danych
                _fCurrentAction = _fishingUiType.GetField("currentAction", flags);

                _reflectionInit = true;
            }
            catch (Exception ex)
            {
                _debugStatus = "Ref Error: " + ex.Message;
            }
        }

        private void ProcessFishing(global::WTUIFishingActions ui)
        {
            if (_fButtons == null)
            {
                _debugStatus = "Nie znaleziono przycisków (Reflection Fail)";
                return;
            }

            try
            {
                // Pobierz listę/tablicę przycisków z UI
                var buttonsObj = _fButtons.GetValue(ui);
                if (buttonsObj == null) return;

                // Konwersja na listę obiektów (zakładamy że to Button[] lub List<Button>)
                List<Button> btnList = new List<Button>();
                if (buttonsObj is Button[]) btnList.AddRange((Button[])buttonsObj);
                else if (buttonsObj is List<Button>) btnList.AddRange((List<Button>)buttonsObj);
                else if (buttonsObj is Transform[])
                {
                    // Fallback jeśli to tablica Transformów
                    foreach (var t in (Transform[])buttonsObj)
                    {
                        var b = t.GetComponent<Button>();
                        if (b) btnList.Add(b);
                    }
                }

                int winningIndex = -1;

                // --- LOGIKA SZUKANIA POPRAWNEGO ---

                // METODA 1: Jeśli mamy pole CorrectIndex
                if (_fCorrectIndex != null)
                {
                    winningIndex = Convert.ToInt32(_fCorrectIndex.GetValue(ui));
                }
                // METODA 2: "Bruteforce Logic" - Analiza stanu przycisku
                // Czasami gra oznacza poprawny przycisk ustawiając mu specyficzną nazwę, tag lub (niewidoczny) kolor w skrypcie
                else
                {
                    for (int i = 0; i < btnList.Count; i++)
                    {
                        var btn = btnList[i];
                        if (btn == null || !btn.gameObject.activeSelf) continue;

                        // Sprawdź czy przycisk ma przypisany skrypt akcji
                        // W WT2 często jest komponent 'FishAction' na przycisku
                        var action = btn.GetComponent<global::FishAction>(); // Przykładowa nazwa z listy plików
                        if (action != null)
                        {
                            // Sprawdź wewnątrz akcji, czy jest "isCorrect" lub podobne
                            // Tutaj używamy dynamic, żeby nie bawić się w kolejne reflection fields
                            try
                            {
                                dynamic dAction = action;
                                if (dAction.isCorrect == true || dAction.IsCorrect == true)
                                {
                                    winningIndex = i;
                                    break;
                                }
                            }
                            catch { }
                        }

                        // Fallback: Szukamy po kolorze (nawet jeśli alpha jest 0, kolor bazowy może być zielony)
                        var img = btn.GetComponent<Image>();
                        if (img != null && IsGreenish(img.color))
                        {
                            winningIndex = i;
                            break;
                        }
                    }
                }

                // --- REAKCJA ---

                if (winningIndex >= 0 && winningIndex < btnList.Count)
                {
                    Button winningBtn = btnList[winningIndex];
                    _debugStatus = $"CEL: [{winningIndex}] {winningBtn.name}";

                    // 1. Rysuj Box (ESP)
                    if (ConfigManager.Fish_ShowESP)
                    {
                        DrawBoxOnButton(winningBtn);
                    }

                    // 2. Kliknij (BOT)
                    if (ConfigManager.Fish_AutoPress && Time.time > _actionTimer)
                    {
                        if (winningBtn.interactable)
                        {
                            winningBtn.onClick.Invoke();
                            // Dodaj losowość do czasu reakcji żeby nie dostać bana
                            float randomDelay = UnityEngine.Random.Range(0.05f, 0.15f);
                            _actionTimer = Time.time + ConfigManager.Fish_ReactionTime + randomDelay;
                        }
                    }
                }
                else
                {
                    _debugStatus = "Szukam celu...";
                }
            }
            catch (Exception ex)
            {
                _debugStatus = "Proc Error: " + ex.Message;
            }
        }

        private bool IsGreenish(Color c)
        {
            // Sprawdza czy kolor jest "bardziej zielony niż czerwony/niebieski"
            // Działa nawet jak GUI ukrywa kolor (np. nakłada szary overlay), ale bazowy kolor Image jest ustawiony
            return c.g > 0.5f && c.r < 0.5f && c.b < 0.5f;
        }

        private void DrawBoxOnButton(Button btn)
        {
            // Pobierz pozycję przycisku na ekranie
            Vector3[] corners = new Vector3[4];
            btn.GetComponent<RectTransform>().GetWorldCorners(corners);

            // Konwersja do GUI space
            // WorldCorners dla UI są już w ScreenSpace zazwyczaj (jeśli Canvas jest Overlay)
            // Ale musimy odwrócić Y

            float x = corners[0].x;
            float y = Screen.height - corners[1].y; // Top-Left Y (Unity GUI Y is inverted vs RectTransform)
            float w = corners[2].x - corners[0].x;
            float h = corners[1].y - corners[0].y;

            if (_boxTexture == null)
            {
                _boxTexture = new Texture2D(1, 1);
                _boxTexture.SetPixel(0, 0, new Color(0, 1, 0, 0.4f)); // Półprzezroczysty zielony
                _boxTexture.Apply();
            }

            GUI.DrawTexture(new Rect(x, y, w, h), _boxTexture);

            // Opcjonalnie: Napis
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.normal.textColor = Color.green;
            s.fontSize = 20;
            s.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x, y - 25, w, 25), "HIT!", s);
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Fish Bot (Cave/Normal)</b>");

            bool newVal = GUILayout.Toggle(ConfigManager.Fish_Enabled, "Włącz FishBota");
            if (newVal != ConfigManager.Fish_Enabled) { ConfigManager.Fish_Enabled = newVal; ConfigManager.Save(); }

            if (ConfigManager.Fish_Enabled)
            {
                newVal = GUILayout.Toggle(ConfigManager.Fish_ShowESP, "Pokaż podpowiedź (ESP)");
                if (newVal != ConfigManager.Fish_ShowESP) { ConfigManager.Fish_ShowESP = newVal; ConfigManager.Save(); }

                newVal = GUILayout.Toggle(ConfigManager.Fish_AutoPress, "Auto Klikanie (Bot)");
                if (newVal != ConfigManager.Fish_AutoPress) { ConfigManager.Fish_AutoPress = newVal; ConfigManager.Save(); }

                if (ConfigManager.Fish_AutoPress)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Reakcja: {ConfigManager.Fish_ReactionTime:F2}s");
                    float newF = GUILayout.HorizontalSlider(ConfigManager.Fish_ReactionTime, 0.1f, 1.0f);
                    if (Math.Abs(newF - ConfigManager.Fish_ReactionTime) > 0.01f) { ConfigManager.Fish_ReactionTime = newF; ConfigManager.Save(); }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Label($"Status: {_debugStatus}");
            }
            GUILayout.EndVertical();
        }
    }
}