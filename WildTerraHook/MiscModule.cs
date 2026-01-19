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
        private global::CameraMMO _cachedCam;
        private float _cacheTimer = 0f;

        // --- GUI ---
        private Vector2 _scrollPos;

        public void DrawMenu()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));

            GUILayout.Label("<b>INNE FUNKCJE</b>");

            // Status Kamery (Debug)
            if (_cachedCam != null)
                GUILayout.Label($"Kamera: OK | Zoom: {_cachedCam.distance:F1}/{_cachedCam.maxDistance:F1}");
            else
                GUILayout.Label("Kamera: Szukanie...");

            // 1. KAMERA
            bool newCam = GUILayout.Toggle(_cameraUnlock, "Odblokuj Zoom (Max 150)");
            if (newCam != _cameraUnlock)
            {
                _cameraUnlock = newCam;
                if (!_cameraUnlock) RevertCameraUnlock(); // Reset przy wyłączeniu
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
            // Odśwież cache co 1s jeśli zgubiono kamerę
            if (_cachedCam == null || Time.time > _cacheTimer)
            {
                // PRÓBA 1: FindObjectOfType
                _cachedCam = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();

                // PRÓBA 2: Camera.main (Pewniejsza)
                if (_cachedCam == null && Camera.main != null)
                {
                    _cachedCam = Camera.main.GetComponent<global::CameraMMO>();
                }

                _cacheTimer = Time.time + 1.0f;
            }

            if (_cachedCam != null)
            {
                // Zapisz wartości domyślne (tylko raz)
                if (_defaultMaxDist == -1f)
                {
                    _defaultMaxDist = _cachedCam.maxDistance;
                    _defaultZoomSpeed = _cachedCam.zoomSpeedMouse;
                }

                // Aplikuj hack (Wymuś max dystans i prędkość)
                if (_cachedCam.maxDistance < 150f)
                {
                    _cachedCam.maxDistance = 150f;
                    _cachedCam.zoomSpeedMouse = 10.0f; // Bardzo szybki zoom
                }
            }
        }

        private void RevertCameraUnlock()
        {
            if (_cachedCam != null && _defaultMaxDist != -1f)
            {
                _cachedCam.maxDistance = _defaultMaxDist;
                _cachedCam.zoomSpeedMouse = _defaultZoomSpeed;

                // Przywróć dystans jeśli jesteśmy za daleko
                if (_cachedCam.distance > _defaultMaxDist)
                    _cachedCam.distance = _defaultMaxDist;
            }
        }

        // --- LOGIKA POGODY ---
        private void ForceDayTime()
        {
            if (global::EnviroSky.instance != null)
            {
                // SetTime wymaga intów
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
                    _playerLightObj.transform.localPosition = new Vector3(0, 10, 0); // Wyżej dla lepszego zasięgu

                    Light l = _playerLightObj.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 200f;       // Większy zasięg
                    l.intensity = 2.0f;  // Jaśniej
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