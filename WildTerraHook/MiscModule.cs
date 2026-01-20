using UnityEngine;
using System;
// Importujemy przestrzeń nazw kamery RPG - to klucz do działania Zoom Hacka
using JohnStairs.RCC.ThirdPerson;

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA GŁÓWNE ---
        public bool EternalDayEnabled = false;
        public bool BrightPlayerEnabled = false;
        public bool NoFogEnabled = false;
        public bool ZoomHackEnabled = false;

        // --- LATARKA (Parametry: 2.0 / 1000) ---
        public float LightIntensity = 2.0f;
        public float LightRange = 1000f;
        private GameObject _lightObject;

        // --- KAMERA (Zoom & FOV) ---
        public float CameraFov = 60f;
        public float MaxZoomDistance = 60f; // Standardowo gra ma ok. 15-40
        public float CameraAngle = 80f;     // Kąt patrzenia (góra/dół) - im więcej, tym luźniejsza kamera

        private float _defaultFov = 60f;
        private bool _defaultsInitialized = false;

        // --- CACHE (Bezpośrednie typy) ---
        private WTRPGCamera _rpgCamera;
        private global::CameraMMO _mmoCamera;
        private float _cacheTimer = 0f;
        private string _debugInfo = "Init...";

        // Metoda wywoływana w każdej klatce przez MainHack
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Eternal Day
            if (EternalDayEnabled)
            {
                if (global::EnviroSky.instance != null)
                {
                    global::EnviroSky.instance.SetTime(
                        global::EnviroSky.instance.GameTime.Years,
                        global::EnviroSky.instance.GameTime.Days,
                        12, 0, 0);
                }
            }

            // 2. No Fog
            if (NoFogEnabled)
            {
                RenderSettings.fog = false;
                RenderSettings.fogDensity = 0.0f;
                RenderSettings.fogStartDistance = 100000f;
                RenderSettings.fogEndDistance = 200000f;
            }

            // 3. Latarka
            HandleBrightPlayer();

            // 4. Kamera (FOV & Zoom)
            HandleCamera();
        }

        private void HandleCamera()
        {
            // A. Szukanie kamery (co 1 sekundę lub jeśli null)
            if (Time.time > _cacheTimer)
            {
                FindCameras();
                _cacheTimer = Time.time + 1.0f;
            }

            // B. Obsługa FOV
            if (Camera.main != null)
            {
                if (!_defaultsInitialized)
                {
                    _defaultFov = Camera.main.fieldOfView;
                    if (CameraFov < 10) CameraFov = _defaultFov;
                    _defaultsInitialized = true;
                }

                if (Math.Abs(Camera.main.fieldOfView - CameraFov) > 0.1f)
                {
                    Camera.main.fieldOfView = CameraFov;
                }
            }

            // C. Obsługa Zoom Hacka (Bezpośrednie przypisanie)
            if (ZoomHackEnabled)
            {
                // Tryb RPG (WTRPGCamera)
                if (_rpgCamera != null)
                {
                    // To są właściwości (Properties) - bezpośredni dostęp jest pewniejszy niż Reflection
                    _rpgCamera.MaxDistance = MaxZoomDistance;

                    // Odblokowanie kątów (żeby móc patrzeć pionowo w dół z daleka)
                    _rpgCamera.RotationYMin = CameraAngle;
                    _rpgCamera.RotationYMax = CameraAngle;

                    // Czułość kółka myszy
                    _rpgCamera.ZoomSensitivity = 60f;
                }
                // Tryb MMO (CameraMMO)
                else if (_mmoCamera != null)
                {
                    // To są pola publiczne (Fields)
                    _mmoCamera.maxDistance = MaxZoomDistance;
                    _mmoCamera.xMinAngle = -CameraAngle;
                    _mmoCamera.xMaxAngle = CameraAngle;
                    _mmoCamera.zoomSpeedMouse = 5.0f;
                }
            }
        }

        private void FindCameras()
        {
            _rpgCamera = null;
            _mmoCamera = null;

            // 1. Próba znalezienia RPG Camery (Singleton)
            if (WTRPGCamera.instance != null)
            {
                _rpgCamera = WTRPGCamera.instance;
                _debugInfo = "RPG (Instance)";
                return;
            }

            // 2. Próba znalezienia na scenie (fallback)
            _rpgCamera = UnityEngine.Object.FindObjectOfType<WTRPGCamera>();
            if (_rpgCamera != null)
            {
                _debugInfo = "RPG (Find)";
                return;
            }

            // 3. Próba znalezienia MMO Camery
            _mmoCamera = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();
            if (_mmoCamera != null)
            {
                _debugInfo = "MMO Camera";
                return;
            }

            _debugInfo = "Nie znaleziono";
        }

        private void HandleBrightPlayer()
        {
            if (BrightPlayerEnabled)
            {
                if (_lightObject == null)
                {
                    _lightObject = new GameObject("HackLight");
                    _lightObject.transform.SetParent(global::Player.localPlayer.transform);
                    // Światło wysoko nad graczem dla lepszego oświetlenia terenu
                    _lightObject.transform.localPosition = new Vector3(0, 5f, 0);

                    var l = _lightObject.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.shadows = LightShadows.None;
                    l.color = Color.white;
                }

                var lightComp = _lightObject.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.intensity = LightIntensity;
                    lightComp.range = LightRange;
                }

                if (!_lightObject.activeSelf) _lightObject.SetActive(true);
            }
            else
            {
                if (_lightObject != null)
                {
                    UnityEngine.Object.Destroy(_lightObject);
                    _lightObject = null;
                }
            }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>Misc Options</b> [{_debugInfo}]");

            // Eternal Day & No Fog
            EternalDayEnabled = GUILayout.Toggle(EternalDayEnabled, "Eternal Day (12:00)");
            NoFogEnabled = GUILayout.Toggle(NoFogEnabled, "No Fog (Usuń Mgłę)");

            GUILayout.Space(5);

            // Bright Player
            BrightPlayerEnabled = GUILayout.Toggle(BrightPlayerEnabled, "Bright Player (Latarka)");
            if (BrightPlayerEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Moc: {LightIntensity:F1}", GUILayout.Width(60));
                LightIntensity = GUILayout.HorizontalSlider(LightIntensity, 1f, 5f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Zasięg: {LightRange:F0}", GUILayout.Width(60));
                LightRange = GUILayout.HorizontalSlider(LightRange, 50f, 2000f); // Zasięg do 2000
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            // Zoom Hack
            ZoomHackEnabled = GUILayout.Toggle(ZoomHackEnabled, "Zoom Hack (Odblokuj)");
            if (ZoomHackEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Max Zoom: {MaxZoomDistance:F0}", GUILayout.Width(80));
                MaxZoomDistance = GUILayout.HorizontalSlider(MaxZoomDistance, 20f, 200f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Kąt: {CameraAngle:F0}", GUILayout.Width(80));
                CameraAngle = GUILayout.HorizontalSlider(CameraAngle, 10f, 89f);
                GUILayout.EndHorizontal();
            }

            // FOV
            GUILayout.BeginHorizontal();
            GUILayout.Label($"FOV: {CameraFov:F0}", GUILayout.Width(60));
            CameraFov = GUILayout.HorizontalSlider(CameraFov, 30f, 120f);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Reset Domyślne"))
            {
                CameraFov = _defaultFov;
                MaxZoomDistance = 40f;
                CameraAngle = 80f;
                LightIntensity = 2.0f;
                LightRange = 1000f;
            }

            GUILayout.EndVertical();
        }
    }
}