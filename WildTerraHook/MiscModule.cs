using UnityEngine;
using System;
using System.Collections.Generic;
using JohnStairs.RCC.ThirdPerson;

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA ---
        public bool EternalDayEnabled = false;
        public bool BrightPlayerEnabled = false;
        public bool FullbrightEnabled = false;
        public bool NoFogEnabled = false;
        public bool ZoomHackEnabled = false;

        // AGGRO
        public bool MobAggroEnabled = false;
        public float AggroRadius = 10.0f; // Domyślny promień (można regulować)

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

        // --- AGGRO CACHE ---
        private List<global::WTMob> _mobCache = new List<global::WTMob>();
        private float _mobScanTimer = 0f;

        // Metoda UPDATE
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            if (EternalDayEnabled && global::EnviroSky.instance != null)
                global::EnviroSky.instance.SetTime(global::EnviroSky.instance.GameTime.Years, global::EnviroSky.instance.GameTime.Days, 12, 0, 0);

            if (NoFogEnabled) ApplyNoFog();

            HandleFullbright();
            HandleBrightPlayer();
            HandleFov();
            if (ZoomHackEnabled) HandleZoomHack();

            // Cache mobów co 1 sekunda dla Aggro Radius
            if (MobAggroEnabled && Time.time > _mobScanTimer)
            {
                ScanMobs();
                _mobScanTimer = Time.time + 1.0f;
            }
        }

        // Metoda Rysowania (wołana z MainHack.OnGUI)
        public void OnGUI()
        {
            if (MobAggroEnabled)
            {
                DrawAggroCircles();
            }
        }

        private void ScanMobs()
        {
            _mobCache.Clear();
            try
            {
                var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                foreach (var mob in mobs)
                {
                    if (mob != null && mob.health > 0) _mobCache.Add(mob);
                }
            }
            catch { }
        }

        private void DrawAggroCircles()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (var mob in _mobCache)
            {
                if (mob == null || mob.health <= 0) continue;

                // Sprawdź czy to mob agresywny (uproszczone po nazwie/typie)
                // W WildTerra zazwyczaj "agresywny" oznacza Boss, LargeFox, Bear itp.
                // Rysujemy dla wszystkich mobów, żeby gracz wiedział
                // Można dodać filtrację, ale "Aggro Radius" przydaje się do wszystkiego co atakuje.

                float radius = AggroRadius;
                // Opcjonalnie: Spróbuj pobrać prawdziwy zasięg z moba przez Reflection
                // var r = mob.GetType().GetField("aggroRange"); if (r != null) radius = (float)r.GetValue(mob);

                DrawCircle(cam, mob.transform.position, radius, Color.red);
            }
        }

        private void DrawCircle(Camera cam, Vector3 position, float radius, Color color)
        {
            // Rysowanie okręgu z odcinków
            int segments = 24;
            float angle = 0f;
            float angleStep = 360f / segments;

            Vector3 prevPos = Vector3.zero;
            bool prevVisible = false;

            for (int i = 0; i <= segments; i++)
            {
                float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
                float z = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;

                Vector3 point = position + new Vector3(x, 0.5f, z); // 0.5f nad ziemią
                Vector3 screenPos = cam.WorldToScreenPoint(point);

                bool isVisible = screenPos.z > 0;

                if (i > 0 && isVisible && prevVisible)
                {
                    // Rysuj linię 2D
                    DrawLine(new Vector2(prevPos.x, Screen.height - prevPos.y), new Vector2(screenPos.x, Screen.height - screenPos.y), color, 1.5f);
                }

                prevPos = screenPos;
                prevVisible = isVisible;
                angle += angleStep;
            }
        }

        // Helper do rysowania linii w OnGUI
        private static Texture2D _lineTex;
        private void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width)
        {
            if (_lineTex == null) { _lineTex = new Texture2D(1, 1); _lineTex.SetPixel(0, 0, Color.white); _lineTex.Apply(); }

            float angle = Mathf.Rad2Deg * Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x);
            float length = Vector2.Distance(pointA, pointB);

            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.color = color;
            GUI.DrawTexture(new Rect(pointA.x, pointA.y, length, width), _lineTex);
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(-angle, pointA);
        }

        private void ApplyNoFog()
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 20000f;
            RenderSettings.fogEndDistance = 30000f;
            RenderSettings.fogDensity = 0.0f;
            RenderSettings.fog = true;
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

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.Get("MISC_LANG_SEL"), GUILayout.Width(120));
            if (GUILayout.Button("English", ConfigManager.Language == "en" ? GUI.skin.box : GUI.skin.button)) ChangeLanguage("en");
            if (GUILayout.Button("Polski", ConfigManager.Language == "pl" ? GUI.skin.box : GUI.skin.button)) ChangeLanguage("pl");
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // AGGRO RADIUS
            MobAggroEnabled = GUILayout.Toggle(MobAggroEnabled, Localization.Get("MISC_AGGRO_TITLE"));
            if (MobAggroEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_AGGRO_RANGE")}: {AggroRadius:F1}", GUILayout.Width(140));
                AggroRadius = GUILayout.HorizontalSlider(AggroRadius, 1f, 30f);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(5);

            EternalDayEnabled = GUILayout.Toggle(EternalDayEnabled, Localization.Get("MISC_ETERNAL_DAY"));
            NoFogEnabled = GUILayout.Toggle(NoFogEnabled, Localization.Get("MISC_NO_FOG"));
            FullbrightEnabled = GUILayout.Toggle(FullbrightEnabled, Localization.Get("MISC_FULLBRIGHT"));

            GUILayout.Space(5);

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
                AggroRadius = 10.0f;
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