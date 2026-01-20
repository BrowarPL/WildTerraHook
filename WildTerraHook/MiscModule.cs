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
        public bool ZoomHackEnabled = false;

        // --- KONFIGURACJA LATARKI ---
        // Zmieniono domyślne wartości na życzenie: Zasięg 1000, Moc 2
        public float LightIntensity = 2.0f;
        public float LightRange = 1000f;
        private GameObject _lightObject;

        // --- KONFIGURACJA KAMERY ---
        public float CameraFov = 60f;
        public float MaxZoomDistance = 60f; // Wartość startowa suwaka

        private float _defaultFov = 60f;
        private bool _defaultsInitialized = false;

        // Status do wyświetlania w menu (dla debugowania)
        private string _cameraStatus = "Szukanie...";

        // Cache typów (Reflection) - aby nie szukać ich w kółko
        private Type _wtrpgCameraType;
        private Type _mmoCameraType;
        private float _cacheTimer = 0f;

        // Obiekty kamer
        private UnityEngine.Object _activeCameraObject;
        private bool _isRpgMode = false;

        // Metoda wywoływana w każdej klatce przez MainHack (Update)
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Eternal Day (Wersja działająca: SetTime)
            if (EternalDayEnabled)
            {
                ApplyEternalDay();
            }

            // 2. No Fog (Wersja działająca: Agresywne RenderSettings)
            if (NoFogEnabled)
            {
                ApplyNoFog();
            }

            // 3. Bright Player (Latarka)
            HandleBrightPlayer();

            // 4. Obsługa Kamery (Zoom Hack + FOV)
            HandleCamera();
        }

        // --- POGODA ---

        private void ApplyEternalDay()
        {
            if (global::EnviroSky.instance != null)
            {
                int years = global::EnviroSky.instance.GameTime.Years;
                int days = global::EnviroSky.instance.GameTime.Days;
                // Ustawiamy sztywno 12:00
                global::EnviroSky.instance.SetTime(years, days, 12, 0, 0);
            }
        }

        private void ApplyNoFog()
        {
            RenderSettings.fog = false;
            RenderSettings.fogDensity = 0.0f;
            RenderSettings.fogStartDistance = 100000f;
            RenderSettings.fogEndDistance = 200000f;
            RenderSettings.fogMode = FogMode.Linear;
        }

        // --- LATARKA ---

        private void HandleBrightPlayer()
        {
            if (global::Player.localPlayer == null) return;

            if (BrightPlayerEnabled)
            {
                if (_lightObject == null)
                {
                    _lightObject = new GameObject("HackLight");
                    _lightObject.transform.SetParent(global::Player.localPlayer.transform);
                    _lightObject.transform.localPosition = new Vector3(0, 10f, 0);

                    var l = _lightObject.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = Color.white;
                    l.shadows = LightShadows.None;
                }

                // Aktualizacja na żywo
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

        // --- KAMERA (ZOOM HACK NAPRAWA) ---

        private void HandleCamera()
        {
            // Odświeżanie cache co 1s
            if (Time.time > _cacheTimer)
            {
                RefreshCameraCache();
                _cacheTimer = Time.time + 1.0f;
            }

            // Obsługa FOV
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

            // Aplikowanie Zoom Hacka
            if (ZoomHackEnabled && _activeCameraObject != null)
            {
                ApplyZoomLogic();
            }
        }

        private void RefreshCameraCache()
        {
            // 1. Inicjalizacja Typów (tylko raz)
            if (_wtrpgCameraType == null)
                _wtrpgCameraType = Type.GetType("JohnStairs.RCC.ThirdPerson.WTRPGCamera, Assembly-CSharp");

            if (_mmoCameraType == null)
                _mmoCameraType = Type.GetType("CameraMMO, Assembly-CSharp");

            // 2. Próba znalezienia instancji RPG (Priority)
            if (_wtrpgCameraType != null)
            {
                // Próbujemy pobrać pole 'instance'
                var instanceField = _wtrpgCameraType.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceField != null)
                {
                    var instance = instanceField.GetValue(null) as UnityEngine.Object;
                    if (instance != null)
                    {
                        _activeCameraObject = instance;
                        _isRpgMode = true;
                        _cameraStatus = "<color=green>WTRPG: Wykryto</color>";
                        return;
                    }
                }

                // Fallback: FindObjectOfType
                var obj = UnityEngine.Object.FindObjectOfType(_wtrpgCameraType);
                if (obj != null)
                {
                    _activeCameraObject = obj;
                    _isRpgMode = true;
                    _cameraStatus = "<color=green>WTRPG: Znaleziono (Scena)</color>";
                    return;
                }
            }

            // 3. Próba znalezienia instancji MMO
            if (_mmoCameraType != null)
            {
                var obj = UnityEngine.Object.FindObjectOfType(_mmoCameraType);
                if (obj != null)
                {
                    _activeCameraObject = obj;
                    _isRpgMode = false;
                    _cameraStatus = "<color=cyan>MMO: Wykryto</color>";
                    return;
                }
            }

            _activeCameraObject = null;
            _cameraStatus = "<color=red>Nie znaleziono kamery</color>";
        }

        private void ApplyZoomLogic()
        {
            try
            {
                if (_isRpgMode && _wtrpgCameraType != null)
                {
                    // Ustawiamy MaxDistance
                    var prop = _wtrpgCameraType.GetProperty("MaxDistance");
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(_activeCameraObject, MaxZoomDistance, null);
                    }

                    // Ustawiamy Czułość
                    var sensProp = _wtrpgCameraType.GetProperty("ZoomSensitivity");
                    if (sensProp != null && sensProp.CanWrite)
                    {
                        sensProp.SetValue(_activeCameraObject, 60f, null);
                    }
                }
                else if (!_isRpgMode && _mmoCameraType != null)
                {
                    // CameraMMO używa pola maxDistance
                    var field = _mmoCameraType.GetField("maxDistance");
                    if (field != null)
                    {
                        field.SetValue(_activeCameraObject, MaxZoomDistance);
                    }
                }
            }
            catch (Exception ex)
            {
                _cameraStatus = $"Błąd Zoom: {ex.Message}";
            }
        }

        // --- GUI MENU ---

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Misc Options (Różne)</b>");

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
                LightRange = GUILayout.HorizontalSlider(LightRange, 50f, 2000f);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            // Zoom Hack
            ZoomHackEnabled = GUILayout.Toggle(ZoomHackEnabled, "Zoom Hack");
            GUILayout.Label($"Status: {_cameraStatus}"); // Informacja debugowa

            if (ZoomHackEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Max Zoom: {MaxZoomDistance:F0}", GUILayout.Width(100));
                // Suwak do 200 zgodnie z prośbą
                MaxZoomDistance = GUILayout.HorizontalSlider(MaxZoomDistance, 20f, 200f);
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
                MaxZoomDistance = 60f;
                LightIntensity = 2.0f;
                LightRange = 1000f;
            }

            GUILayout.EndVertical();
        }
    }
}