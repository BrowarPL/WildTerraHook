using UnityEngine;
using System;
using JohnStairs.RCC.ThirdPerson; // Wymagane do działania Zoom Hacka

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA GŁÓWNE ---
        public bool EternalDayEnabled = false;
        public bool BrightPlayerEnabled = false; // Latarka (lokalna)
        public bool FullbrightEnabled = false;   // Jasność globalna (Ambient)
        public bool NoFogEnabled = false;
        public bool ZoomHackEnabled = false;

        // --- LATARKA (Parametry domyślne: 2.0 / 1000) ---
        public float LightIntensity = 2.0f;
        public float LightRange = 1000f;
        private GameObject _lightObject;

        // --- KAMERA (Zoom & FOV) ---
        public float CameraFov = 60f;

        // Suwaki Zoom Hacka
        public float MaxZoomLimit = 100f;   // Limit oddalenia (wpływa też na kąt przy ziemi)
        public float CameraAngle = 45f;     // Kąt patrzenia góra/dół (domyślnie 45)
        public float ZoomSpeed = 60f;       // Czułość kółka myszy

        private float _defaultFov = 60f;
        private bool _defaultsInitialized = false;

        // --- CACHE ---
        private WTRPGCamera _rpgCamera;
        private global::CameraMMO _mmoCamera;
        private float _cacheTimer = 0f;
        private string _debugInfo = "Szukanie...";

        // Metoda UPDATE
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Eternal Day
            if (EternalDayEnabled && global::EnviroSky.instance != null)
            {
                global::EnviroSky.instance.SetTime(
                    global::EnviroSky.instance.GameTime.Years,
                    global::EnviroSky.instance.GameTime.Days,
                    12, 0, 0);
            }

            // 2. No Fog
            if (NoFogEnabled)
            {
                RenderSettings.fog = false;
                RenderSettings.fogDensity = 0.0f;
                RenderSettings.fogStartDistance = 100000f;
                RenderSettings.fogEndDistance = 200000f;
            }

            // 3. Fullbright (Globalna Jasność) - NOWOŚĆ
            if (FullbrightEnabled)
            {
                // Ustawiamy światło otoczenia na czystą biel, co eliminuje cienie i ciemność
                RenderSettings.ambientLight = Color.white;
                // Opcjonalnie można wymusić tryb Flat, jeśli gra używa Trilight/Skybox
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            }

            // 4. Latarka (Lokalna)
            HandleBrightPlayer();

            // 5. FOV
            HandleFov();

            // 6. Zoom Hack
            if (ZoomHackEnabled)
            {
                HandleZoomHack();
            }
        }

        private void HandleFov()
        {
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
        }

        private void HandleZoomHack()
        {
            if (Time.time > _cacheTimer)
            {
                FindCameras();
                _cacheTimer = Time.time + 1.0f;
            }

            // Obsługa RPG Camera (WTRPGCamera)
            if (_rpgCamera != null)
            {
                // 1. Ustawienie limitu
                _rpgCamera.MaxDistance = MaxZoomLimit;

                // 2. Odblokowanie kątów (góra/dół)
                _rpgCamera.RotationYMin = CameraAngle;
                _rpgCamera.RotationYMax = CameraAngle;

                // 3. Czułość zoomu
                _rpgCamera.ZoomSensitivity = ZoomSpeed;

                // 4. FIX: Wyłączamy tryb rozmowy z NPC
                _rpgCamera.ReturnZoomFromNPC();
            }
            // Obsługa MMO Camera (CameraMMO)
            else if (_mmoCamera != null)
            {
                _mmoCamera.maxDistance = MaxZoomLimit;
                _mmoCamera.xMinAngle = -CameraAngle;
                _mmoCamera.xMaxAngle = CameraAngle;
                _mmoCamera.zoomSpeedMouse = 5.0f;
            }
        }

        private void FindCameras()
        {
            _rpgCamera = null;
            _mmoCamera = null;

            if (WTRPGCamera.instance != null)
            {
                _rpgCamera = WTRPGCamera.instance;
                _debugInfo = "RPG (Instance)";
                return;
            }

            _rpgCamera = UnityEngine.Object.FindObjectOfType<WTRPGCamera>();
            if (_rpgCamera != null)
            {
                _debugInfo = "RPG (Find)";
                return;
            }

            _mmoCamera = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();
            if (_mmoCamera != null)
            {
                _debugInfo = "MMO Camera";
                return;
            }

            _debugInfo = "Brak Kamery";
        }

        private void HandleBrightPlayer()
        {
            if (BrightPlayerEnabled)
            {
                if (_lightObject == null)
                {
                    _lightObject = new GameObject("HackLight");
                    _lightObject.transform.SetParent(global::Player.localPlayer.transform);
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

            // Pogoda i Światło
            EternalDayEnabled = GUILayout.Toggle(EternalDayEnabled, "Eternal Day (12:00)");
            NoFogEnabled = GUILayout.Toggle(NoFogEnabled, "No Fog (Usuń Mgłę)");
            FullbrightEnabled = GUILayout.Toggle(FullbrightEnabled, "Fullbright (Jasność Otoczenia)"); // Nowy toggle

            GUILayout.Space(5);

            // Latarka
            BrightPlayerEnabled = GUILayout.Toggle(BrightPlayerEnabled, "Bright Player (Latarka)");
            if (BrightPlayerEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Moc: {LightIntensity:F1}", GUILayout.Width(60));
                LightIntensity = GUILayout.HorizontalSlider(LightIntensity, 1f, 5f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Zasięg: {LightRange:F0}", GUILayout.Width(60));
                LightRange = GUILayout.HorizontalSlider(LightRange, 50f, 2000f);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            // Zoom Hack
            ZoomHackEnabled = GUILayout.Toggle(ZoomHackEnabled, "Zoom Hack (Odblokuj)");
            if (ZoomHackEnabled)
            {
                // 1. Limit Zoomu
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Limit Zoomu: {MaxZoomLimit:F0}", GUILayout.Width(100));
                MaxZoomLimit = GUILayout.HorizontalSlider(MaxZoomLimit, 20f, 200f);
                GUILayout.EndHorizontal();

                // 2. Kąt
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Kąt (45 def): {CameraAngle:F0}", GUILayout.Width(100));
                CameraAngle = GUILayout.HorizontalSlider(CameraAngle, 10f, 89f);
                GUILayout.EndHorizontal();

                // 3. Czułość Zoomu
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Czułość: {ZoomSpeed:F0}", GUILayout.Width(100));
                ZoomSpeed = GUILayout.HorizontalSlider(ZoomSpeed, 10f, 200f);
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
                MaxZoomLimit = 100f;
                CameraAngle = 45f;
                ZoomSpeed = 60f;
                LightIntensity = 2.0f;
                LightRange = 1000f;
            }

            GUILayout.EndVertical();
        }
    }
}