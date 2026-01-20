using UnityEngine;
using System;
using JohnStairs.RCC.ThirdPerson;

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA GŁÓWNE ---
        public bool EternalDayEnabled = false;
        public bool BrightPlayerEnabled = false;
        public bool FullbrightEnabled = false;
        public bool NoFogEnabled = false;
        public bool ZoomHackEnabled = false;

        // --- LATARKA ---
        public float LightIntensity = 2.0f;
        public float LightRange = 1000f;
        private GameObject _lightObject;

        // --- KAMERA ---
        public float CameraFov = 60f;
        public float MaxZoomLimit = 100f;
        public float CameraAngle = 45f;
        public float ZoomSpeed = 60f;

        private float _defaultFov = 60f;
        private bool _defaultsInitialized = false;

        // --- FULLBRIGHT CACHE ---
        private ShadowQuality _originalShadows;
        private bool _fullbrightActive = false;

        // --- KAMERA CACHE ---
        private WTRPGCamera _rpgCamera;
        private global::CameraMMO _mmoCamera;
        private float _cacheTimer = 0f;

        // Metoda UPDATE
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            if (EternalDayEnabled && global::EnviroSky.instance != null)
                global::EnviroSky.instance.SetTime(global::EnviroSky.instance.GameTime.Years, global::EnviroSky.instance.GameTime.Days, 12, 0, 0);

            if (NoFogEnabled)
            {
                RenderSettings.fog = false;
                RenderSettings.fogDensity = 0.0f;
            }

            HandleFullbright();
            HandleBrightPlayer();
            HandleFov();
            if (ZoomHackEnabled) HandleZoomHack();
        }

        private void HandleFullbright()
        {
            if (FullbrightEnabled)
            {
                if (!_fullbrightActive)
                {
                    _originalShadows = QualitySettings.shadows;
                    _fullbrightActive = true;
                }
                RenderSettings.ambientLight = Color.white;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                QualitySettings.shadows = ShadowQuality.Disable;
            }
            else
            {
                if (_fullbrightActive)
                {
                    QualitySettings.shadows = _originalShadows;
                    _fullbrightActive = false;
                }
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
                    Camera.main.fieldOfView = CameraFov;
            }
        }

        private void HandleZoomHack()
        {
            if (Time.time > _cacheTimer)
            {
                FindCameras();
                _cacheTimer = Time.time + 1.0f;
            }

            if (_rpgCamera != null)
            {
                _rpgCamera.MaxDistance = MaxZoomLimit;
                _rpgCamera.RotationYMin = CameraAngle;
                _rpgCamera.RotationYMax = CameraAngle;
                _rpgCamera.ZoomSensitivity = ZoomSpeed;
                _rpgCamera.ReturnZoomFromNPC();
            }
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
            if (WTRPGCamera.instance != null) { _rpgCamera = WTRPGCamera.instance; return; }
            _rpgCamera = UnityEngine.Object.FindObjectOfType<WTRPGCamera>();
            if (_rpgCamera != null) return;
            _mmoCamera = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();
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
            GUILayout.Label($"<b>{Localization.Get("MISC_TITLE")}</b>");

            // Język
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.Get("MISC_LANG_SEL"), GUILayout.Width(120));
            if (GUILayout.Button("English", ConfigManager.Language == "en" ? GUI.skin.box : GUI.skin.button)) ChangeLanguage("en");
            if (GUILayout.Button("Polski", ConfigManager.Language == "pl" ? GUI.skin.box : GUI.skin.button)) ChangeLanguage("pl");
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // Opcje
            EternalDayEnabled = GUILayout.Toggle(EternalDayEnabled, Localization.Get("MISC_ETERNAL_DAY"));
            NoFogEnabled = GUILayout.Toggle(NoFogEnabled, Localization.Get("MISC_NO_FOG"));
            FullbrightEnabled = GUILayout.Toggle(FullbrightEnabled, Localization.Get("MISC_FULLBRIGHT"));

            GUILayout.Space(5);

            // Latarka
            BrightPlayerEnabled = GUILayout.Toggle(BrightPlayerEnabled, Localization.Get("MISC_BRIGHT_PLAYER"));
            if (BrightPlayerEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_LIGHT_INT")}: {LightIntensity:F1}", GUILayout.Width(120));
                LightIntensity = GUILayout.HorizontalSlider(LightIntensity, 1f, 5f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_LIGHT_RNG")}: {LightRange:F0}", GUILayout.Width(120));
                LightRange = GUILayout.HorizontalSlider(LightRange, 50f, 2000f);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            // Zoom Hack
            ZoomHackEnabled = GUILayout.Toggle(ZoomHackEnabled, Localization.Get("MISC_ZOOM_TITLE"));
            if (ZoomHackEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_ZOOM_LIMIT")}: {MaxZoomLimit:F0}", GUILayout.Width(120));
                MaxZoomLimit = GUILayout.HorizontalSlider(MaxZoomLimit, 20f, 200f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_CAM_ANGLE")}: {CameraAngle:F0}", GUILayout.Width(120));
                CameraAngle = GUILayout.HorizontalSlider(CameraAngle, 10f, 89f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_ZOOM_SENS")}: {ZoomSpeed:F0}", GUILayout.Width(120));
                ZoomSpeed = GUILayout.HorizontalSlider(ZoomSpeed, 10f, 200f);
                GUILayout.EndHorizontal();
            }

            // FOV
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Localization.Get("MISC_FOV")}: {CameraFov:F0}", GUILayout.Width(120));
            CameraFov = GUILayout.HorizontalSlider(CameraFov, 30f, 120f);
            GUILayout.EndHorizontal();

            if (GUILayout.Button(Localization.Get("MISC_RESET")))
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

        private void ChangeLanguage(string lang)
        {
            if (ConfigManager.Language != lang)
            {
                ConfigManager.Language = lang;
                ConfigManager.Save();
                Localization.LoadLanguage(lang);
            }
        }
    }
}