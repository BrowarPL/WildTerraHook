using UnityEngine;
using System;
using System.Collections;

namespace WildTerraHook
{
    public class ColorFishingModule
    {
        // --- USTAWIENIA (Zamiast zewnętrznego Settings) ---
        public bool Enabled = false;
        public float Sensitivity = 0.15f; // Czułość wykrywania zmian koloru
        public float ScanSize = 50f;      // Wielkość obszaru skanowania
        public float ClickDelay = 2.0f;   // Czas odczekania po kliknięciu

        // --- STAN WEWNĘTRZNY ---
        private Texture2D _screenTex;
        private Color[] _pixels;
        private float _lastColorAvg;
        private bool _isFishing = false;
        private float _waitTimer = 0f;
        private Rect _scanRect;

        // Inicjalizacja
        public ColorFishingModule()
        {
            _screenTex = new Texture2D(1, 1);
        }

        public void Update()
        {
            if (!Enabled) return;

            // Logika timera (np. przerwa po zarzuceniu wędki)
            if (Time.time < _waitTimer) return;

            // Obliczamy obszar skanowania (środek ekranu)
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;
            _scanRect = new Rect(centerX - ScanSize / 2, centerY - ScanSize / 2, ScanSize, ScanSize);

            // Wykonujemy analizę (symulacja w Update, choć lepiej w Coroutine - tutaj uproszczone)
            // W prawdziwym hacku screenshotowanie robi się rzadziej lub przez ReadPixels w OnPostRender
            // Tutaj tylko logika decyzyjna
        }

        // Ta metoda jest wywoływana przez MainHack w OnGUI
        public void OnGUI()
        {
            if (!Enabled) return;

            // Rysowanie ramki pokazującej gdzie bot "patrzy"
            GUI.color = Color.green;
            GUI.Box(_scanRect, ""); // Pusty box jako ramka
            GUI.Label(new Rect(_scanRect.x, _scanRect.y - 20, 100, 20), $"FishBot: {(_isFishing ? "Wait" : "Scan")}");
            GUI.color = Color.white;

            // Tutaj faktyczna analiza koloru (musi być w OnGUI dla ReadPixels lub Textur)
            if (Event.current.type == EventType.Repaint && Time.time > _waitTimer)
            {
                ProcessScreenColor();
            }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Color Fish Bot</b>");

            Enabled = GUILayout.Toggle(Enabled, "Włącz FishBota");

            GUILayout.Label($"Czułość: {Sensitivity:F2}");
            Sensitivity = GUILayout.HorizontalSlider(Sensitivity, 0.01f, 0.5f);

            GUILayout.Label($"Obszar: {ScanSize:F0} px");
            ScanSize = GUILayout.HorizontalSlider(ScanSize, 10f, 200f);

            GUILayout.Label($"Opóźnienie: {ClickDelay:F1}s");
            ClickDelay = GUILayout.HorizontalSlider(ClickDelay, 0.5f, 5.0f);

            GUILayout.EndVertical();
        }

        private void ProcessScreenColor()
        {
            try
            {
                // Odczyt pikseli z centrum ekranu
                // UWAGA: ReadPixels czyta z lewego dolnego rogu, GUI ma 0,0 w lewym górnym. Trzeba przeliczyć Y.
                float glY = Screen.height - _scanRect.yMax;

                // Tworzymy teksturę jeśli rozmiar się zmienił
                if (_screenTex.width != (int)_scanRect.width)
                    _screenTex = new Texture2D((int)_scanRect.width, (int)_scanRect.height, TextureFormat.RGB24, false);

                _screenTex.ReadPixels(new Rect(_scanRect.x, glY, _scanRect.width, _scanRect.height), 0, 0);
                _screenTex.Apply();

                // Oblicz średnią jasność/kolor
                Color[] pix = _screenTex.GetPixels();
                float currentAvg = 0f;
                foreach (var p in pix) currentAvg += (p.r + p.g + p.b);
                currentAvg /= pix.Length;

                // Wykryj nagłą zmianę (spławik zatonął / pojawiła się ikona)
                float delta = Mathf.Abs(currentAvg - _lastColorAvg);

                if (_lastColorAvg > 0 && delta > Sensitivity)
                {
                    // WYKRYTO BRANIE!
                    CatchFish();
                }

                _lastColorAvg = currentAvg;
            }
            catch { }
        }

        private void CatchFish()
        {
            _waitTimer = Time.time + ClickDelay;
            // Tutaj wyślij input, np. Spację lub F
            // Ponieważ nie mamy dostępu do Input simulatora w czystym Unity bez unsafe/user32.dll,
            // używamy metody gry jeśli dostępna, lub prostego logu na razie.

            // Przykład użycia funkcji gry (jeśli dostępna):
            // if (Player.localPlayer) Player.localPlayer.DoFishingAction();

            Debug.Log("[FishBot] Branie wykryte! Klik!");
        }
    }
}