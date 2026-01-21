using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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

        // AGGRO (VISION)
        public bool MobAggroEnabled = false;
        public float ManualRange = 12.0f; // Ręczny zasięg (skoro automat zawodzi)
        public float ManualAngle = 110.0f; // Kąt widzenia (Cone)
        public bool ShowPassive = false; // Czy pokazywać pasywne

        // DEBUGGER
        private string _debugMobInfo = "";
        private Vector2 _scrollDebug;
        private bool _showDebugInfo = false;

        // --- INNE ---
        public float LightIntensity = 2.0f;
        public float LightRange = 1000f;
        private GameObject _lightObject;

        public float CameraFov = 60f;
        public float MaxZoomLimit = 100f;
        public float CameraAngle = 45f;
        public float ZoomSpeed = 60f;

        private float _defaultFov = 60f;
        private bool _defaultsInitialized = false;
        private ShadowQuality _originalShadows;
        private bool _fullbrightActive = false;
        private float _cacheTimer = 0f;

        private WTRPGCamera _rpgCamera;
        private global::CameraMMO _mmoCamera;

        // --- CACHE MOBÓW ---
        private List<global::WTMob> _mobCache = new List<global::WTMob>();
        private float _mobScanTimer = 0f;

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

            // Skanowanie mobów
            if (MobAggroEnabled && Time.time > _mobScanTimer)
            {
                ScanMobs();
                _mobScanTimer = Time.time + 0.5f; // Częstsze odświeżanie dla płynności ruchu
            }
        }

        public void OnGUI()
        {
            if (MobAggroEnabled)
            {
                DrawVisionCones();
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
                    if (mob != null && mob.health > 0)
                    {
                        // Jeśli ukrywamy pasywne, odfiltruj je tutaj
                        if (!ShowPassive && !IsMobAggressive(mob)) continue;
                        _mobCache.Add(mob);
                    }
                }
            }
            catch { }
        }

        private bool IsMobAggressive(global::WTMob mob)
        {
            string name = mob.name.ToLower();
            if (name.Contains("hare") || name.Contains("deer") || name.Contains("stag") ||
               (name.Contains("fox") && !name.Contains("large")) || name.Contains("pig") || name.Contains("sheep") || name.Contains("cow"))
                return false;
            return true;
        }

        // --- RYSOWANIE STOŻKÓW (CONE) ---

        private void DrawVisionCones()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (var mob in _mobCache)
            {
                if (mob == null || mob.health <= 0) continue;

                Color col = IsMobAggressive(mob) ? new Color(1f, 0f, 0f, 0.8f) : new Color(0f, 1f, 0f, 0.5f);

                // Rysujemy stożek widzenia
                DrawFieldOfView(cam, mob.transform, ManualRange, ManualAngle, col);
            }
        }

        private void DrawFieldOfView(Camera cam, Transform t, float radius, float angle, Color color)
        {
            Vector3 pos = t.position;
            Vector3 forward = t.forward;

            // 1. Małe kółko "Słuchu/Węchu" (zawsze 360, mały zasięg np. 2m)
            DrawCircle(cam, pos, 2.0f, color);

            // 2. Stożek widzenia (Vision Cone)
            int segments = 20;
            float startAngle = -angle / 2;
            float endAngle = angle / 2;
            float step = (endAngle - startAngle) / segments;

            Vector3 prevLineEnd = Vector3.zero;
            bool prevVisible = false;

            // Punkt centralny (pozycja moba) na ekranie
            Vector3 screenCenter = cam.WorldToScreenPoint(pos);
            bool centerVisible = screenCenter.z > 0;

            for (int i = 0; i <= segments; i++)
            {
                float currentAngle = startAngle + (step * i);

                // Obracamy wektor 'forward' o dany kąt
                Quaternion rot = Quaternion.AngleAxis(currentAngle, Vector3.up);
                Vector3 dir = rot * forward;
                Vector3 endPoint = pos + (dir * radius);

                Vector3 screenEnd = cam.WorldToScreenPoint(endPoint);
                bool endVisible = screenEnd.z > 0;

                // Rysuj linię od środka do krawędzi (promienie skrajne)
                if (centerVisible && endVisible && (i == 0 || i == segments))
                {
                    DrawLine(new Vector2(screenCenter.x, Screen.height - screenCenter.y),
                             new Vector2(screenEnd.x, Screen.height - screenEnd.y), color, 2f);
                }

                // Rysuj łuk łączący promienie
                if (i > 0 && endVisible && prevVisible)
                {
                    DrawLine(new Vector2(prevLineEnd.x, Screen.height - prevLineEnd.y),
                             new Vector2(screenEnd.x, Screen.height - screenEnd.y), color, 2f);
                }

                prevLineEnd = screenEnd;
                prevVisible = endVisible;
            }
        }

        private void DrawCircle(Camera cam, Vector3 position, float radius, Color color)
        {
            int segments = 24;
            float angleStep = 360f / segments;
            Vector3 prevPos = Vector3.zero;
            bool prevVisible = false;

            for (int i = 0; i <= segments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * angleStep);
                Vector3 point = position + new Vector3(Mathf.Sin(rad) * radius, 0.2f, Mathf.Cos(rad) * radius);
                Vector3 screenPos = cam.WorldToScreenPoint(point);
                bool isVisible = screenPos.z > 0;

                if (i > 0 && isVisible && prevVisible)
                    DrawLine(new Vector2(prevPos.x, Screen.height - prevPos.y), new Vector2(screenPos.x, Screen.height - screenPos.y), color, 1f);

                prevPos = screenPos;
                prevVisible = isVisible;
            }
        }

        private static Texture2D _lineTex;
        private void DrawLine(Vector2 p1, Vector2 p2, Color col, float w)
        {
            if (_lineTex == null) { _lineTex = new Texture2D(1, 1); _lineTex.SetPixel(0, 0, Color.white); _lineTex.Apply(); }
            float angle = Mathf.Rad2Deg * Mathf.Atan2(p2.y - p1.y, p2.x - p1.x);
            float len = Vector2.Distance(p1, p2);
            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.color = col;
            GUI.DrawTexture(new Rect(p1.x, p1.y, len, w), _lineTex);
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(-angle, p1);
        }

        // --- DEBUGGER (Deep Scan) ---
        private void AnalyzeNearestMob()
        {
            if (global::Player.localPlayer == null) return;
            Vector3 myPos = global::Player.localPlayer.transform.position;

            var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
            global::WTMob nearest = null;
            float minDist = 9999f;

            foreach (var m in mobs)
            {
                float d = Vector3.Distance(myPos, m.transform.position);
                if (d < minDist) { minDist = d; nearest = m; }
            }

            if (nearest == null) { _debugMobInfo = "Brak mobów."; return; }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"MOB: {nearest.name} (ID: {nearest.netId})");

            // 1. Pola bezpośrednie
            sb.AppendLine("--- Fields ---");
            DumpValues(nearest, sb);

            // 2. Data / Template (ScriptableObject) - GŁĘBOKIE SKANOWANIE
            sb.AppendLine("--- Data/Template ---");
            object dataObj = GetFieldObject(nearest, "data") ?? GetFieldObject(nearest, "template") ?? GetFieldObject(nearest, "settings");
            if (dataObj != null)
            {
                sb.AppendLine($"Found: {dataObj.GetType().Name}");
                DumpValues(dataObj, sb);
            }
            else
            {
                sb.AppendLine("No 'data/template' field found.");
            }

            _debugMobInfo = sb.ToString();
        }

        private void DumpValues(object obj, StringBuilder sb)
        {
            if (obj == null) return;
            var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var f in fields)
            {
                // Szukamy czegokolwiek co pachnie zasięgiem
                string n = f.Name.ToLower();
                if (f.FieldType == typeof(float) || f.FieldType == typeof(int))
                {
                    // Wypisz wszystko co numeryczne, żeby znaleźć "ukrytą" wartość
                    sb.AppendLine($" {f.Name} = {f.GetValue(obj)}");
                }
            }
        }

        private object GetFieldObject(object parent, string name)
        {
            var f = parent.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            return f != null ? f.GetValue(parent) : null;
        }

        // --- RESZTA FUNKCJI (BEZ ZMIAN) ---

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

            // AGGRO (VISION CONE)
            MobAggroEnabled = GUILayout.Toggle(MobAggroEnabled, Localization.Get("MISC_AGGRO_TITLE") + " (Vision Cone)");
            if (MobAggroEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Zasięg: {ManualRange:F1}m", GUILayout.Width(100));
                ManualRange = GUILayout.HorizontalSlider(ManualRange, 2f, 30f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Kąt (FOV): {ManualAngle:F0}°", GUILayout.Width(100));
                ManualAngle = GUILayout.HorizontalSlider(ManualAngle, 30f, 360f);
                GUILayout.EndHorizontal();

                ShowPassive = GUILayout.Toggle(ShowPassive, "Pokaż Pasywne");

                if (GUILayout.Button("Zbadaj Najbliższego Moba (Deep Debug)"))
                {
                    AnalyzeNearestMob();
                    _showDebugInfo = true;
                }
            }

            if (_showDebugInfo && !string.IsNullOrEmpty(_debugMobInfo))
            {
                GUILayout.Label("--- DANE MOBA ---");
                _scrollDebug = GUILayout.BeginScrollView(_scrollDebug, "box", GUILayout.Height(150));
                GUILayout.TextArea(_debugMobInfo);
                GUILayout.EndScrollView();
                if (GUILayout.Button("Zamknij")) _showDebugInfo = false;
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
                ManualRange = 12.0f;
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