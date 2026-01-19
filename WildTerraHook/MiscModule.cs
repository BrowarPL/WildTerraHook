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

        // --- ZMIENNE POMOCNICZE ---
        private float _defaultMaxDist = -1f;
        private float _defaultZoomSpeed = -1f;
        private GameObject _playerLightObj;

        // Cache
        private MonoBehaviour _activeCameraScript; // Przechowuje WTRPGCamera lub CameraMMO
        private bool _isWTRPG = false;
        private float _cacheTimer = 0f;

        // --- GUI ---
        private Vector2 _scrollPos;

        public void DrawMenu()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));

            GUILayout.Label("<b>INNE FUNKCJE</b>");

            // Status Kamery
            if (_activeCameraScript != null)
                GUILayout.Label($"Kamera: {_activeCameraScript.GetType().Name} [OK]");
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

            // 3. ŚWIATŁO GRACZA
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

            if (_cameraUnlock)
            {
                ApplyCameraUnlock();
            }
        }

        // --- LOGIKA KAMERY ---
        private void ApplyCameraUnlock()
        {
            // 1. Znajdź kamerę (co 1s)
            if (_activeCameraScript == null || Time.time > _cacheTimer)
            {
                FindCameraScript();
                _cacheTimer = Time.time + 1.0f;
            }

            if (_activeCameraScript != null)
            {
                if (_isWTRPG)
                {
                    // Obsługa WTRPGCamera (Namespace: JohnStairs.RCC.ThirdPerson)
                    var rpgCam = _activeCameraScript as JohnStairs.RCC.ThirdPerson.WTRPGCamera;
                    if (rpgCam != null)
                    {
                        // Zapisz domyślne
                        if (_defaultMaxDist == -1f) _defaultMaxDist = rpgCam.MaxDistance;

                        // Hack
                        if (rpgCam.MaxDistance < 150f)
                        {
                            rpgCam.MaxDistance = 150f;
                            // Opcjonalnie: Zwiększ czułość
                            rpgCam.ZoomSensitivity = 30f;
                        }
                    }
                }
                else
                {
                    // Obsługa CameraMMO (Fallback)
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
            // Próba 1: WTRPGCamera (Najbardziej prawdopodobna)
            var rpg = JohnStairs.RCC.ThirdPerson.WTRPGCamera.instance;
            if (rpg == null) rpg = UnityEngine.Object.FindObjectOfType<JohnStairs.RCC.ThirdPerson.WTRPGCamera>();

            if (rpg != null)
            {
                _activeCameraScript = rpg;
                _isWTRPG = true;
                return;
            }

            // Próba 2: CameraMMO
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
                    if (rpgCam != null) rpgCam.MaxDistance = _defaultMaxDist;
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
            // Reset flag
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

        // --- LOGIKA ŚWIATŁA ---
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