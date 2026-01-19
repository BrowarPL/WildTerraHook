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
        private bool _brightPlayer = false; // Dodatkowe światło na graczu

        // --- STARE WARTOŚCI (Do przywrócenia) ---
        private float _defaultMaxDist = -1f;
        private bool _isLightCreated = false;
        private GameObject _playerLightObj;

        // --- GUI ---
        private Vector2 _scrollPos;

        public void DrawMenu()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));

            GUILayout.Label("<b>INNE FUNKCJE</b>");

            // 1. KAMERA
            bool newCam = GUILayout.Toggle(_cameraUnlock, "Odblokuj Zoom Kamery (100m)");
            if (newCam != _cameraUnlock)
            {
                _cameraUnlock = newCam;
                if (_cameraUnlock) ApplyCameraUnlock();
                else RevertCameraUnlock();
            }

            // 2. WIECZNY DZIEŃ
            _eternalDay = GUILayout.Toggle(_eternalDay, "Wieczny Dzień (Godz. 12:00)");

            // 3. ŚWIATŁO GRACZA (Latarka)
            bool newLight = GUILayout.Toggle(_brightPlayer, "Światło Gracza (Latarka)");
            if (newLight != _brightPlayer)
            {
                _brightPlayer = newLight;
                TogglePlayerLight(_brightPlayer);
            }

            GUILayout.EndScrollView();
        }

        public void Update()
        {
            // Utrzymywanie stanu w każdej klatce (dla pewności)
            if (_eternalDay)
            {
                ForceDayTime();
            }

            if (_cameraUnlock)
            {
                // Ciągłe wymuszanie, bo gra może próbować to cofnąć przy zmianie strefy
                ApplyCameraUnlock();
            }
        }

        // --- LOGIKA KAMERY ---
        private void ApplyCameraUnlock()
        {
            var cam = global::CameraMMO.instance; // Używamy static instance jeśli dostępna, lub Find
            if (cam == null) cam = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();

            if (cam != null)
            {
                // Zapisz domyślną wartość tylko raz
                if (_defaultMaxDist == -1f) _defaultMaxDist = cam.maxDistance;

                // Ustaw duży dystans
                if (cam.maxDistance < 100f) cam.maxDistance = 100f;
            }
        }

        private void RevertCameraUnlock()
        {
            var cam = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();
            if (cam != null && _defaultMaxDist != -1f)
            {
                cam.maxDistance = _defaultMaxDist;
            }
        }

        // --- LOGIKA POGODY ---
        private void ForceDayTime()
        {
            if (global::EnviroSky.instance != null)
            {
                // EnviroSky używa internalHour lub GameTime.Hours
                // Ustawiamy sztywno na 12:00
                global::EnviroSky.instance.SetTime(global::EnviroSky.instance.GameTime.Years, global::EnviroSky.instance.GameTime.Days, 12.0f, 0f, 0f);
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
                    _playerLightObj.transform.localPosition = new Vector3(0, 3, 0); // 3m nad głową

                    Light l = _playerLightObj.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 30f;
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