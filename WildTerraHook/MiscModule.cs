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
        public float LightIntensity = 2.0f;
        public float LightRange = 100f;
        private GameObject _lightObject;

        // --- KONFIGURACJA KAMERY ---
        public float CameraFov = 60f;
        public float MaxZoomDistance = 60f;

        private float _defaultFov = 60f;
        private bool _defaultsInitialized = false;

        // Cache dla kamery (używamy MonoBehaviour, aby nie musieć importować JohnStairs...)
        private MonoBehaviour _rpgCameraScript;
        private MonoBehaviour _mmoCameraScript;
        private float _cacheTimer = 0f;

        // Metoda wywoływana w każdej klatce przez MainHack (Update)
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Eternal Day (Logika z Twojego działającego pliku)
            if (EternalDayEnabled)
            {
                ApplyEternalDay();
            }

            // 2. No Fog (Logika z Twojego działającego pliku)
            if (NoFogEnabled)
            {
                ApplyNoFog();
            }

            // 3. Bright Player (Latarka z suwakami)
            HandleBrightPlayer();

            // 4. Obsługa Kamery (Zoom Hack + FOV)
            HandleCamera();
        }

        // --- POGODA ---

        private void ApplyEternalDay()
        {
            // Używamy dokładnie tej metody co w Twoim kodzie: SetTime
            if (global::EnviroSky.instance != null)
            {
                int years = global::EnviroSky.instance.GameTime.Years;
                int days = global::EnviroSky.instance.GameTime.Days;
                // Ustawiamy godzinę 12:00:00
                global::EnviroSky.instance.SetTime(years, days, 12, 0, 0);
            }
        }

        private void ApplyNoFog()
        {
            // Agresywne usuwanie mgły (wartości z Twojego kodu)
            RenderSettings.fog = false;
            RenderSettings.fogDensity = 0.0f;
            RenderSettings.fogStartDistance = 100000f;
            RenderSettings.fogEndDistance = 200000f;
            RenderSettings.fogMode = FogMode.Linear;
        }

        // --- KAMERA ---

        private void HandleCamera()
        {
            // Odświeżanie cache co 1 sekundę (szukanie skryptów kamery)
            if (Time.time > _cacheTimer)
            {
                FindCameraScripts();
                _cacheTimer = Time.time + 1.0f;
            }

            // Obsługa FOV (Kąt widzenia)
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

            // Obsługa Zoom Hacka
            if (ZoomHackEnabled)
            {
                ApplyZoomHack();
            }
        }

        private void FindCameraScripts()
        {
            // Szukamy skryptu RPG (JohnStairs) po nazwie typu, aby uniknąć błędów using
            if (_rpgCameraScript == null)
            {
                foreach (var obj in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                {
                    if (obj.GetType().FullName.Contains("WTRPGCamera"))
                    {
                        _rpgCameraScript = obj;
                        break;
                    }
                }
            }

            // Szukamy skryptu MMO (CameraMMO)
            if (_mmoCameraScript == null)
            {
                _mmoCameraScript = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();
            }
        }

        private void ApplyZoomHack()
        {
            // Logika dla kamery RPG (WTRPGCamera)
            if (_rpgCameraScript != null)
            {
                // Używamy Reflection, aby ustawić 'MaxDistance' bez importowania namespace JohnStairs
                try
                {
                    var prop = _rpgCameraScript.GetType().GetProperty("MaxDistance");
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(_rpgCameraScript, MaxZoomDistance, null);
                    }

                    // Opcjonalnie: Zwiększenie czułości zoomu
                    var sensProp = _rpgCameraScript.GetType().GetProperty("ZoomSensitivity");
                    if (sensProp != null) sensProp.SetValue(_rpgCameraScript, 60f, null);
                }
                catch { }
            }

            // Logika dla kamery MMO (CameraMMO)
            if (_mmoCameraScript != null)
            {
                // CameraMMO ma publiczne pole maxDistance (mała litera)
                try
                {
                    var field = _mmoCameraScript.GetType().GetField("maxDistance");
                    if (field != null)
                    {
                        field.SetValue(_mmoCameraScript, MaxZoomDistance);
                    }
                }
                catch { }
            }
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
                    // Pozycja nad głową (Y=10) jak w Twoim kodzie
                    _lightObject.transform.localPosition = new Vector3(0, 10f, 0);

                    var l = _lightObject.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = Color.white;
                    l.shadows = LightShadows.None;
                }

                // Aktualizacja parametrów na żywo
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

        // --- MENU GUI ---

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Misc Options (Różne)</b>");

            // Eternal Day
            EternalDayEnabled = GUILayout.Toggle(EternalDayEnabled, "Eternal Day (Zawsze Dzień 12:00)");

            // No Fog
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
                LightRange = GUILayout.HorizontalSlider(LightRange, 50f, 1000f);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            // Zoom Hack
            ZoomHackEnabled = GUILayout.Toggle(ZoomHackEnabled, "Zoom Hack (Odblokuj Kamerę)");
            if (ZoomHackEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Max Zoom: {MaxZoomDistance:F0}", GUILayout.Width(100));
                // Suwak do 200, tak jak chciałeś
                MaxZoomDistance = GUILayout.HorizontalSlider(MaxZoomDistance, 20f, 200f);
                GUILayout.EndHorizontal();
            }

            // FOV
            GUILayout.BeginHorizontal();
            GUILayout.Label($"FOV: {CameraFov:F0}", GUILayout.Width(60));
            CameraFov = GUILayout.HorizontalSlider(CameraFov, 30f, 120f);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Przywróć Domyślne"))
            {
                CameraFov = _defaultFov;
                MaxZoomDistance = 40f;
                LightIntensity = 2.0f;
                LightRange = 100f;
            }

            GUILayout.EndVertical();
        }
    }
}