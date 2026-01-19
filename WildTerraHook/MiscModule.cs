using UnityEngine;
using System;
using System.Reflection;

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA ---
        private bool _cameraUnlock = false;
        private bool _eternalDay = false;
        private bool _brightPlayer = false;
        private bool _noFog = false;

        // --- ZMIENNE POMOCNICZE ---
        private float _defaultMaxDist = -1f;
        private float _defaultZoomSpeed = -1f;
        private GameObject _playerLightObj;

        // Cache
        private MonoBehaviour _activeCameraScript;
        private bool _isWTRPG = false;
        private float _cacheTimer = 0f;

        // --- GUI ---
        private Vector2 _scrollPos;

        public void DrawMenu()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(250));

            GUILayout.Label("<b>INNE FUNKCJE</b>");

            // Status Kamery (Debug dla Ciebie)
            if (_activeCameraScript != null)
                GUILayout.Label($"Cam: {_activeCameraScript.GetType().Name} [OK]");
            else
                GUILayout.Label("Kamera: Szukanie...");

            // 1. KAMERA
            bool newCam = GUILayout.Toggle(_cameraUnlock, "Odblokuj Zoom (Max 150)");
            if (newCam != _cameraUnlock)
            {
                _cameraUnlock = newCam;
                if (!_cameraUnlock) RevertCameraUnlock();
            }

            // 2. WIECZNY DZIEŃ
            _eternalDay = GUILayout.Toggle(_eternalDay, "Wieczny Dzień (Godz. 12:00)");

            // 3. NO FOG
            _noFog = GUILayout.Toggle(_noFog, "Usuń Mgłę (No Fog)");

            // 4. ŚWIATŁO GRACZA (Twoje ustawienia)
            bool newLight = GUILayout.Toggle(_brightPlayer, "Latarka Gracza");
            if (newLight != _brightPlayer)
            {
                _brightPlayer = newLight;
                TogglePlayerLight(_brightPlayer);
            }

            GUILayout.EndScrollView();
        }

        public void Update()
        {
            if (_eternalDay) ForceDayTime();
            if (_noFog) DisableFog(); // Wywoływane co klatkę

            if (_cameraUnlock)
            {
                ApplyCameraUnlock();
            }
        }

        // --- LOGIKA MGŁY (POPRAWIONA) ---
        private void DisableFog()
        {
            // 1. Wyłączamy flagę (Enviro może to nadpisać)
            RenderSettings.fog = false;

            // 2. Zerujemy gęstość
            RenderSettings.fogDensity = 0.0f;

            // 3. KLUCZOWE: Odsuwamy mgłę na 100km. 
            // Nawet jak Enviro włączy mgłę (fog=true), to nie będzie jej widać.
            RenderSettings.fogStartDistance = 100000f;
            RenderSettings.fogEndDistance = 200000f;

            // 4. Wymuszamy tryb Linear, który respektuje powyższe dystanse
            RenderSettings.fogMode = FogMode.Linear;
        }

        // --- LOGIKA KAMERY ---
        private void ApplyCameraUnlock()
        {
            if (_activeCameraScript == null || Time.time > _cacheTimer)
            {
                FindCameraScript();
                _cacheTimer = Time.time + 1.0f;
            }

            if (_activeCameraScript != null)
            {
                if (_isWTRPG)
                {
                    var rpgCam = _activeCameraScript as JohnStairs.RCC.ThirdPerson.WTRPGCamera;
                    if (rpgCam != null)
                    {
                        // Zapisz domyślne
                        if (_defaultMaxDist == -1f)
                        {
                            _defaultMaxDist = rpgCam.MaxDistance;
                            _defaultZoomSpeed = rpgCam.ZoomSensitivity;
                        }

                        // Hack: Zwiększamy dystans i czułość (żeby szybciej oddalać)
                        if (rpgCam.MaxDistance < 150f)
                        {
                            rpgCam.MaxDistance = 150f;
                            rpgCam.ZoomSensitivity = 60f; // Szybki scroll
                        }
                    }
                }
                else
                {
                    // Fallback dla starej kamery
                    var mmoCam = _activeCameraScript as global::CameraMMO;
                    if (mmoCam != null)
                    {
                        if (_defaultMaxDist == -1f)
                        {
                            _defaultMaxDist = mmoCam.maxDistance;
                            _defaultZoomSpeed = mmoCam.zoomSpeedMouse;
                        }

                        if (mmoCam.maxDistance < 150f)
                        {
                            mmoCam.maxDistance = 150f;
                            mmoCam.zoomSpeedMouse = 5.0f;
                        }
                    }
                }
            }
        }

        private void FindCameraScript()
        {
            // Próbujemy znaleźć instancję WTRPGCamera (używana w nowszych wersjach gry)
            var rpg = JohnStairs.RCC.ThirdPerson.WTRPGCamera.instance;
            if (rpg == null) rpg = UnityEngine.Object.FindObjectOfType<JohnStairs.RCC.ThirdPerson.WTRPGCamera>();

            if (rpg != null)
            {
                _activeCameraScript = rpg;
                _isWTRPG = true;
                return;
            }

            // Próbujemy znaleźć CameraMMO (starsze wersje / fallback)
            var mmo = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();
            if (mmo != null)
            {
                _activeCameraScript = mmo;
                _isWTRPG = false;
                return;
            }
        }

        private void RevertCameraUnlock()
        {
            if (_activeCameraScript != null && _defaultMaxDist != -1f)
            {
                if (_isWTRPG)
                {
                    var rpgCam = _activeCameraScript as JohnStairs.RCC.ThirdPerson.WTRPGCamera;
                    if (rpgCam != null)
                    {
                        rpgCam.MaxDistance = _defaultMaxDist;
                        rpgCam.ZoomSensitivity = _defaultZoomSpeed;
                    }
                }
                else
                {
                    var mmoCam = _activeCameraScript as global::CameraMMO;
                    if (mmoCam != null)
                    {
                        mmoCam.maxDistance = _defaultMaxDist;
                        mmoCam.zoomSpeedMouse = _defaultZoomSpeed;
                    }
                }
            }
            _defaultMaxDist = -1f;
        }

        // --- LOGIKA POGODY ---
        private void ForceDayTime()
        {
            if (global::EnviroSky.instance != null)
            {
                int years = global::EnviroSky.instance.GameTime.Years;
                int days = global::EnviroSky.instance.GameTime.Days;
                global::EnviroSky.instance.SetTime(years, days, 12, 0, 0);
            }
        }

        // --- LOGIKA ŚWIATŁA (Twoja wersja) ---
        private void TogglePlayerLight(bool enable)
        {
            var player = global::Player.localPlayer;
            if (player == null) return;

            if (enable)
            {
                if (_playerLightObj == null)
                {
                    _playerLightObj = new GameObject("HackLight");
                    _playerLightObj.transform.SetParent(player.transform);
                    _playerLightObj.transform.localPosition = new Vector3(0, 10, 0);

                    Light l = _playerLightObj.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 200f;
                    l.intensity = 2.0f;
                    l.color = Color.white;
                    l.shadows = LightShadows.None;
                }
                _playerLightObj.SetActive(true);
            }
            else
            {
                if (_playerLightObj != null) _playerLightObj.SetActive(false);
            }
        }
    }
}