using UnityEngine;
using System;
using System.Reflection;

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA GŁÓWNE ---
        public bool EternalDayEnabled = false;
        public bool BrightPlayerEnabled = false;
        public bool NoFogEnabled = false;
        public bool ZoomHackEnabled = false;

        // --- LATARKA (Domyślnie: Moc 2, Zasięg 1000) ---
        public float LightIntensity = 2.0f;
        public float LightRange = 1000f;
        private GameObject _lightObject;

        // --- KAMERA (FOV & ZOOM) ---
        public float CameraFov = 60f;
        public float MaxZoomDistance = 60f; // Standardowo ~40, suwak do 200
        public float CameraAngle = 80f;     // Kąt patrzenia (góra/dół), odblokowanie kamery

        private float _defaultFov = 60f;
        private bool _defaultsInitialized = false;

        // Cache Typów i Instancji (Reflection)
        private Type _rpgCameraType;
        private Type _mmoCameraType;
        private MonoBehaviour _cachedCameraScript;
        private bool _isRpgMode = false;
        private float _checkTimer = 0f;

        // --- UPDATE ---
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Eternal Day
            if (EternalDayEnabled && global::EnviroSky.instance != null)
            {
                global::EnviroSky.instance.SetTime(global::EnviroSky.instance.GameTime.Years, global::EnviroSky.instance.GameTime.Days, 12, 0, 0);
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

            // 4. FOV (Przywrócone!)
            HandleFov();

            // 5. Zoom Hack
            if (ZoomHackEnabled)
            {
                HandleZoomHack();
            }
        }

        // --- LOGIKA FOV ---
        private void HandleFov()
        {
            if (Camera.main != null)
            {
                // Zapamiętaj domyślne przy pierwszym uruchomieniu
                if (!_defaultsInitialized)
                {
                    _defaultFov = Camera.main.fieldOfView;
                    if (CameraFov < 10) CameraFov = _defaultFov;
                    _defaultsInitialized = true;
                }

                // Aplikuj FOV jeśli jest inny niż obecny
                if (Math.Abs(Camera.main.fieldOfView - CameraFov) > 0.1f)
                {
                    Camera.main.fieldOfView = CameraFov;
                }
            }
        }

        // --- LOGIKA ZOOM HACK ---
        private void HandleZoomHack()
        {
            // Co 1 sekundę sprawdzamy czy mamy dobrą referencję do kamery
            if (Time.time > _checkTimer)
            {
                FindCamera();
                _checkTimer = Time.time + 1.0f;
            }

            if (_cachedCameraScript != null)
            {
                ApplyZoomValues(_cachedCameraScript);
            }
        }

        private void FindCamera()
        {
            // 1. Inicjalizacja Typów (tylko raz)
            if (_rpgCameraType == null) _rpgCameraType = Type.GetType("JohnStairs.RCC.ThirdPerson.WTRPGCamera, Assembly-CSharp");
            if (_mmoCameraType == null) _mmoCameraType = Type.GetType("CameraMMO, Assembly-CSharp");

            // 2. Próba znalezienia RPG Camera (Priorytet)
            if (_rpgCameraType != null)
            {
                // Sprawdzamy statyczną instancję 'instance'
                var field = _rpgCameraType.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    var instance = field.GetValue(null) as MonoBehaviour;
                    if (instance != null)
                    {
                        _cachedCameraScript = instance;
                        _isRpgMode = true;
                        return;
                    }
                }

                // Fallback: FindObjectOfType
                var obj = UnityEngine.Object.FindObjectOfType(_rpgCameraType) as MonoBehaviour;
                if (obj != null)
                {
                    _cachedCameraScript = obj;
                    _isRpgMode = true;
                    return;
                }
            }

            // 3. Próba znalezienia MMO Camera
            if (_mmoCameraType != null)
            {
                var obj = UnityEngine.Object.FindObjectOfType(_mmoCameraType) as MonoBehaviour;
                if (obj != null)
                {
                    _cachedCameraScript = obj;
                    _isRpgMode = false;
                    return;
                }
            }
        }

        private void ApplyZoomValues(object cameraScript)
        {
            try
            {
                if (_isRpgMode && _rpgCameraType != null)
                {
                    // WTRPGCamera: Ustawiamy MaxDistance
                    var maxDistProp = _rpgCameraType.GetProperty("MaxDistance");
                    if (maxDistProp != null) maxDistProp.SetValue(cameraScript, MaxZoomDistance, null);

                    // WTRPGCamera: Odblokowanie kątów (RotationYMin/Max)
                    var rotMin = _rpgCameraType.GetProperty("RotationYMin");
                    var rotMax = _rpgCameraType.GetProperty("RotationYMax");

                    // Ustawiamy szeroki zakres kątów (np. 80 stopni w górę i w dół)
                    if (rotMin != null) rotMin.SetValue(cameraScript, CameraAngle, null);
                    if (rotMax != null) rotMax.SetValue(cameraScript, CameraAngle, null);

                    // WTRPGCamera: Sensitivity (dla wygody)
                    var zoomSens = _rpgCameraType.GetProperty("ZoomSensitivity");
                    if (zoomSens != null) zoomSens.SetValue(cameraScript, 60f, null);
                }
                else if (!_isRpgMode && _mmoCameraType != null)
                {
                    // CameraMMO: Pola są publiczne (fields), nie properties
                    var distField = _mmoCameraType.GetField("maxDistance");
                    if (distField != null) distField.SetValue(cameraScript, MaxZoomDistance);

                    // CameraMMO: Kąty
                    var minAngle = _mmoCameraType.GetField("xMinAngle");
                    var maxAngle = _mmoCameraType.GetField("xMaxAngle");

                    if (minAngle != null) minAngle.SetValue(cameraScript, -CameraAngle);
                    if (maxAngle != null) maxAngle.SetValue(cameraScript, CameraAngle);
                }
            }
            catch { }
        }

        // --- LATARKA ---
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

        // --- MENU ---
        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Misc Options</b>");

            // Pogoda
            EternalDayEnabled = GUILayout.Toggle(EternalDayEnabled, "Eternal Day (12:00)");
            NoFogEnabled = GUILayout.Toggle(NoFogEnabled, "No Fog (Usuń Mgłę)");

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
            ZoomHackEnabled = GUILayout.Toggle(ZoomHackEnabled, "Zoom Hack (Odblokuj Kamerę)");
            if (ZoomHackEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Max Zoom: {MaxZoomDistance:F0}", GUILayout.Width(80));
                MaxZoomDistance = GUILayout.HorizontalSlider(MaxZoomDistance, 20f, 200f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Kąt (Unlock): {CameraAngle:F0}", GUILayout.Width(80));
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