using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
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

        // AGGRO
        public bool MobAggroEnabled = false;
        public float DefaultAggroRadius = 10.0f; // Fallback
        public bool ShowPassiveRanges = true; // Czy pokazywać zasięg wzroku pasywnych?

        // DEBUGGER
        private string _debugMobInfo = "";
        private Vector2 _scrollDebug;
        private bool _showDebugInfo = false;

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
        private struct CachedMob
        {
            public global::WTMob Mob;
            public float Range;
            public bool IsAggressive;
        }
        private List<CachedMob> _mobCache = new List<CachedMob>();
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

            if (MobAggroEnabled && Time.time > _mobScanTimer)
            {
                ScanMobsSmart();
                _mobScanTimer = Time.time + 1.0f;
            }
        }

        // Metoda Rysowania
        public void OnGUI()
        {
            if (MobAggroEnabled)
            {
                DrawAggroCircles();
            }
        }

        private void ScanMobsSmart()
        {
            _mobCache.Clear();
            try
            {
                var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                foreach (var mob in mobs)
                {
                    if (mob != null && mob.health > 0)
                    {
                        // 1. Ustalanie agresywności (Heurystyka po nazwie lub klasie)
                        bool isAggro = IsMobAggressive(mob);

                        // 2. Pobieranie zasięgu
                        float detectedRange = GetRealAggroRange(mob, isAggro);

                        // Fallback tylko jeśli wykryto 0
                        float finalRange = detectedRange > 0.5f ? detectedRange : DefaultAggroRadius;

                        _mobCache.Add(new CachedMob { Mob = mob, Range = finalRange, IsAggressive = isAggro });
                    }
                }
            }
            catch { }
        }

        private bool IsMobAggressive(global::WTMob mob)
        {
            string name = mob.name.ToLower();
            // Lista pasywnych/płochliwych
            if (name.Contains("hare") || name.Contains("deer") || name.Contains("stag") ||
               (name.Contains("fox") && !name.Contains("large")) || name.Contains("pig") || name.Contains("sheep") || name.Contains("cow"))
                return false;

            return true; // Domyślnie zakładamy że wszystko inne chce nas zabić
        }

        private float GetRealAggroRange(global::WTMob mob, bool isAggressive)
        {
            // Słowa kluczowe których szukamy
            // Dla agresywnych: aggro, attack, detect
            // Dla pasywnych: sight, view, flee, run
            var keywords = isAggressive
                ? new string[] { "aggro", "attack", "detect", "radius", "range" }
                : new string[] { "sight", "view", "flee", "run", "detect", "radius", "range" };

            try
            {
                // Krok 1: Komponenty specyficzne (EntityAggro)
                var aggroComp = mob.GetComponent("EntityAggro");
                if (aggroComp != null)
                {
                    float val = ScanObjectForFloat(aggroComp, keywords);
                    if (val > 0) return val;
                }

                // Krok 2: Główna klasa (WTMob / Monster)
                float mobVal = ScanObjectForFloat(mob, keywords);
                if (mobVal > 0) return mobVal;

                // Krok 3: Dane (Data / Template / Settings)
                // WTMob często ma pole 'data' lub 'settings' typu ScriptableObject
                var dataObj = GetFieldObject(mob, "data") ?? GetFieldObject(mob, "template") ?? GetFieldObject(mob, "settings");
                if (dataObj != null)
                {
                    float dataVal = ScanObjectForFloat(dataObj, keywords);
                    if (dataVal > 0) return dataVal;
                }
            }
            catch { }

            return 0f;
        }

        private float ScanObjectForFloat(object obj, string[] keywords)
        {
            if (obj == null) return 0f;
            Type type = obj.GetType();

            // Szukamy wszystkich pól float
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(float) || f.FieldType == typeof(int))
                {
                    string name = f.Name.ToLower();
                    foreach (var kw in keywords)
                    {
                        if (name.Contains(kw))
                        {
                            float val = Convert.ToSingle(f.GetValue(obj));
                            // Odrzucamy wartości nierealne (np. 0 albo > 100, chyba że to boss)
                            if (val > 0.5f && val < 200f) return val;
                        }
                    }
                }
            }
            return 0f;
        }

        private object GetFieldObject(object parent, string name)
        {
            var f = parent.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            return f != null ? f.GetValue(parent) : null;
        }

        // --- DEBUGGER ---
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

            if (nearest == null) { _debugMobInfo = "Brak mobów w pobliżu."; return; }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>Mob: {nearest.name}</b> (Dist: {minDist:F1})");
            sb.AppendLine($"Type: {nearest.GetType().Name}");

            // Analiza Moba
            sb.AppendLine("- Pola Moba:");
            DumpFloatFields(nearest, sb);

            // Analiza Komponentów
            var comps = nearest.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null || c is Transform || c is Collider) continue; // Pomiń nudne
                sb.AppendLine($"- Komponent: {c.GetType().Name}");
                DumpFloatFields(c, sb);
            }

            // Analiza Data/Template (jeśli istnieje)
            var data = GetFieldObject(nearest, "data");
            if (data != null)
            {
                sb.AppendLine($"- DATA ({data.GetType().Name}):");
                DumpFloatFields(data, sb);
            }

            _debugMobInfo = sb.ToString();
        }

        private void DumpFloatFields(object obj, StringBuilder sb)
        {
            var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(float) || f.FieldType == typeof(int))
                {
                    try
                    {
                        float val = Convert.ToSingle(f.GetValue(obj));
                        if (val > 0) sb.AppendLine($"   {f.Name} = {val}");
                    }
                    catch { }
                }
            }
        }

        // --- RYSOWANIE ---

        private void DrawAggroCircles()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (var item in _mobCache)
            {
                if (item.Mob == null || item.Mob.health <= 0) continue;
                if (!ShowPassiveRanges && !item.IsAggressive) continue;

                Color col = item.IsAggressive ? Color.red : Color.green;
                DrawCircle(cam, item.Mob.transform.position, item.Range, col);
            }
        }

        private void DrawCircle(Camera cam, Vector3 position, float radius, Color color)
        {
            int segments = 32;
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
                    DrawLine(new Vector2(prevPos.x, Screen.height - prevPos.y), new Vector2(screenPos.x, Screen.height - screenPos.y), color, 2f);

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

            // AGGRO
            MobAggroEnabled = GUILayout.Toggle(MobAggroEnabled, Localization.Get("MISC_AGGRO_TITLE"));
            if (MobAggroEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_AGGRO_RANGE")}: {DefaultAggroRadius:F1}", GUILayout.Width(140));
                DefaultAggroRadius = GUILayout.HorizontalSlider(DefaultAggroRadius, 1f, 30f);
                GUILayout.EndHorizontal();

                // INSPEKTOR MOBA
                if (GUILayout.Button("Zbadaj Najbliższego Moba (Debug)"))
                {
                    AnalyzeNearestMob();
                    _showDebugInfo = true;
                }
            }

            // OKNO DEBUGOWE
            if (_showDebugInfo && !string.IsNullOrEmpty(_debugMobInfo))
            {
                GUILayout.Label("--- Mob Debug Info ---");
                _scrollDebug = GUILayout.BeginScrollView(_scrollDebug, "box", GUILayout.Height(150));
                GUILayout.TextArea(_debugMobInfo);
                GUILayout.EndScrollView();
                if (GUILayout.Button("Zamknij Debug")) _showDebugInfo = false;
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
                DefaultAggroRadius = 10.0f;
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