using UnityEngine;
using System;
using System.Reflection;

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA ---
        public bool EternalDayEnabled = false;
        public bool BrightPlayerEnabled = false;
        public bool NoFogEnabled = false;

        // Usunięto: NoClip (zgodnie z poleceniem - server side check)

        // --- KAMERA CONFIG ---
        public float CameraFov = 60f;
        private float _defaultFov = 60f;
        private bool _fovInitialized = false;

        // Obiekt światła dla funkcji BrightPlayer
        private GameObject _lightObject;

        // Metoda wywoływana w każdej klatce przez MainHack
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Obsługa Wiecznego Dnia (EnviroSky)
            if (EternalDayEnabled)
            {
                if (global::EnviroSky.instance != null)
                {
                    // Ustawiamy godzinę na 12:00 (południe)
                    global::EnviroSky.instance.GameTime.Hours = 12f;
                }
            }

            // 2. Obsługa Braku Mgły
            if (NoFogEnabled)
            {
                // Wyłączamy mgłę w ustawieniach renderowania
                RenderSettings.fog = false;

                // Opcjonalnie: Jeśli gra używa EnviroSky do mgły
                if (global::EnviroSky.instance != null)
                {
                    global::EnviroSky.instance.Fog.fogDensity = 0f;
                }
            }

            // 3. Obsługa Rozjaśnienia Gracza (Latarka)
            HandleBrightPlayer();

            // 4. Obsługa Kamery (FOV)
            HandleCamera();
        }

        private void HandleBrightPlayer()
        {
            if (global::Player.localPlayer == null) return;

            if (BrightPlayerEnabled)
            {
                // Jeśli światło nie istnieje, stwórz je
                if (_lightObject == null)
                {
                    _lightObject = new GameObject("HackLight");
                    _lightObject.transform.SetParent(global::Player.localPlayer.transform);
                    // Umieść światło nieco nad graczem
                    _lightObject.transform.localPosition = new Vector3(0, 2.5f, 0);

                    var l = _lightObject.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 40f;      // Zwiększony zasięg
                    l.intensity = 2.0f; // Zwiększona jasność
                    l.color = Color.white;
                    l.shadows = LightShadows.None; // Brak cieni dla wydajności
                }
            }
            else
            {
                // Jeśli wyłączono opcję, usuń obiekt światła
                if (_lightObject != null)
                {
                    UnityEngine.Object.Destroy(_lightObject);
                    _lightObject = null;
                }
            }
        }

        private void HandleCamera()
        {
            if (Camera.main != null)
            {
                // Inicjalizacja domyślnego FOV przy pierwszym uruchomieniu
                if (!_fovInitialized)
                {
                    _defaultFov = Camera.main.fieldOfView;
                    // Jeśli zapisane FOV jest dziwne (np. 0), ustaw domyślne
                    if (CameraFov < 10) CameraFov = _defaultFov;
                    _fovInitialized = true;
                }

                // Zastosuj zmianę tylko jeśli jest różnica, aby nie nadpisywać innych efektów gry ciągle
                if (Math.Abs(Camera.main.fieldOfView - CameraFov) > 0.1f)
                {
                    Camera.main.fieldOfView = CameraFov;
                }
            }
        }

        // Metoda do rysowania menu w MainHack
        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Misc Options (Różne)</b>");

            // Przełączniki
            EternalDayEnabled = GUILayout.Toggle(EternalDayEnabled, "Eternal Day (Zawsze Dzień)");
            BrightPlayerEnabled = GUILayout.Toggle(BrightPlayerEnabled, "Bright Player (Latarka)");
            NoFogEnabled = GUILayout.Toggle(NoFogEnabled, "No Fog (Brak Mgły)");

            GUILayout.Space(10);

            // Suwak kamery
            GUILayout.Label($"Camera FOV (Kąt widzenia): {CameraFov:F0}");
            CameraFov = GUILayout.HorizontalSlider(CameraFov, 30f, 150f);

            if (GUILayout.Button("Reset FOV"))
            {
                CameraFov = _defaultFov;
            }

            GUILayout.EndVertical();
        }
    }
}