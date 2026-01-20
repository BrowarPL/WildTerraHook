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

        // --- LATARKA (Parametry domyślne na życzenie: 2.0 / 1000) ---
        public float LightIntensity = 2.0f;
        public float LightRange = 1000f;
        private GameObject _lightObject;

        // --- KAMERA (Zoom Hack) ---
        public float MaxZoomDistance = 60f; // Startowa wartość na suwaku (Standard gry to ~15-40)
        public float CameraAngle = 45f;     // Kąt widzenia (góra/dół)

        // Cache (Mechanizm refleksji, aby kod był odporny na błędy using)
        private float _cacheTimer = 0f;
        private MonoBehaviour _activeCameraScript;
        private bool _isWTRPG = false;
        private Type _rpgType;
        private Type _mmoType;

        // Informacja dla użytkownika w menu
        private string _camStatus = "Szukanie...";

        // Metoda UPDATE (wywoływana z MainHack)
        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            // 1. Eternal Day (Wersja działająca: SetTime)
            if (EternalDayEnabled)
            {
                if (global::EnviroSky.instance != null)
                {
                    int years = global::EnviroSky.instance.GameTime.Years;
                    int days = global::EnviroSky.instance.GameTime.Days;
                    global::EnviroSky.instance.SetTime(years, days, 12, 0, 0);
                }
            }

            // 2. No Fog (Wersja działająca: Agresywne RenderSettings)
            if (NoFogEnabled)
            {
                RenderSettings.fog = false;
                RenderSettings.fogDensity = 0.0f;
                RenderSettings.fogStartDistance = 100000f;
                RenderSettings.fogEndDistance = 200000f;
                RenderSettings.fogMode = FogMode.Linear;
            }

            // 3. Latarka (Bright Player)
            HandleBrightPlayer();

            // 4. Zoom Hack i Kąt Kamery
            HandleCameraLogic();
        }

        // --- LOGIKA KAMERY (Twoja logika + Reflection) ---
        private void HandleCameraLogic()
        {
            // Co 1 sekundę próbujemy odświeżyć referencję do kamery, jeśli jej nie mamy
            if (_activeCameraScript == null || Time.time > _cacheTimer)
            {
                FindCameraScript();
                _cacheTimer = Time.time + 1.0f;
            }

            // Jeśli mamy kamerę i włączony Zoom Hack -> Aplikujemy wartości
            if (ZoomHackEnabled && _activeCameraScript != null)
            {
                try
                {
                    if (_isWTRPG)
                    {
                        // Obsługa: JohnStairs.RCC.ThirdPerson.WTRPGCamera
                        // Ustawiamy MaxDistance
                        SetProp(_activeCameraScript, "MaxDistance", MaxZoomDistance);

                        // Zwiększamy czułość zoomu, żeby szybciej oddalać
                        SetProp(_activeCameraScript, "ZoomSensitivity", 60f);

                        // Odblokowujemy kąty patrzenia (góra/dół)
                        SetProp(_activeCameraScript, "RotationYMin", CameraAngle);
                        SetProp(_activeCameraScript, "RotationYMax", CameraAngle);
                    }
                    else
                    {
                        // Obsługa: CameraMMO
                        // CameraMMO używa pól publicznych (Fields), nie właściwości (Properties)
                        SetField(_activeCameraScript, "maxDistance", MaxZoomDistance);
                        SetField(_activeCameraScript, "zoomSpeedMouse", 5.0f);
                        SetField(_activeCameraScript, "xMinAngle", -CameraAngle);
                        SetField(_activeCameraScript, "xMaxAngle", CameraAngle);
                    }
                }
                catch { }
            }
        }

        private void FindCameraScript()
        {
            // Inicjalizacja typów (raz)
            if (_rpgType == null) _rpgType = Type.GetType("JohnStairs.RCC.ThirdPerson.WTRPGCamera, Assembly-CSharp");
            if (_mmoType == null) _mmoType = Type.GetType("CameraMMO, Assembly-CSharp");

            _activeCameraScript = null;
            _camStatus = "<color=red>Brak kamery</color>";

            // 1. Sprawdzamy RPG Camera (Priorytet: instance, potem FindObject)
            if (_rpgType != null)
            {
                // Próba pobrania statycznej instancji 'instance'
                var instanceProp = _rpgType.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null)
                {
                    var inst = instanceProp.GetValue(null) as MonoBehaviour;
                    if (inst != null)
                    {
                        _activeCameraScript = inst;
                        _isWTRPG = true;
                        _camStatus = "<color=green>RPG Cam (Instance)</color>";
                        return;
                    }
                }

                // Fallback: Szukanie na scenie
                var obj = UnityEngine.Object.FindObjectOfType(_rpgType) as MonoBehaviour;
                if (obj != null)
                {
                    _activeCameraScript = obj;
                    _isWTRPG = true;
                    _camStatus = "<color=yellow>RPG Cam (Found)</color>";
                    return;
                }
            }

            // 2. Sprawdzamy MMO Camera
            if (_mmoType != null)
            {
                var obj = UnityEngine.Object.FindObjectOfType(_mmoType) as MonoBehaviour;
                if (obj != null)
                {
                    _activeCameraScript = obj;
                    _isWTRPG = false;
                    _camStatus = "<color=cyan>MMO Cam</color>";
                    return;
                }
            }
        }

        // --- POMOCNICY REFLECTION (Unikanie błędów kompilacji) ---
        private void SetProp(object obj, string name, object val)
        {
            if (obj == null) return;
            var prop = obj.GetType().GetProperty(name);
            if (prop != null && prop.CanWrite) prop.SetValue(obj, val, null);
        }

        private void SetField(object obj, string name, object val)
        {
            if (obj == null) return;
            var field = obj.GetType().GetField(name);
            if (field != null) field.SetValue(obj, val);
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
                    _lightObject.transform.localPosition = new Vector3(0, 5f, 0); // 5m nad graczem

                    var l = _lightObject.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = Color.white;
                    l.shadows = LightShadows.None;
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

        // --- RYSOWANIE MENU ---
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
            ZoomHackEnabled = GUILayout.Toggle(ZoomHackEnabled, "Zoom Hack & Kąt");
            GUILayout.Label($"Status Kamery: {_camStatus}"); // Debug info dla Ciebie

            if (ZoomHackEnabled)
            {
                // Suwak Zoomu (Dystans)
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Max Zoom: {MaxZoomDistance:F0}", GUILayout.Width(80));
                MaxZoomDistance = GUILayout.HorizontalSlider(MaxZoomDistance, 20f, 200f);
                GUILayout.EndHorizontal();

                // Suwak Kąta (Ważne dla odblokowania kamery!)
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Max Kąt: {CameraAngle:F0}", GUILayout.Width(80));
                CameraAngle = GUILayout.HorizontalSlider(CameraAngle, 10f, 89f);
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Reset Domyślne"))
            {
                MaxZoomDistance = 40f;
                CameraAngle = 45f;
                LightIntensity = 2.0f;
                LightRange = 1000f;
            }

            GUILayout.EndVertical();
        }
    }
}