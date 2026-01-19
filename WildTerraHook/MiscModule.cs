using UnityEngine;
using System;
using System.Reflection;

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA ---
        private bool _eternalDay = false;
        private bool _brightPlayer = false;
        private bool _noFog = false;
        private bool _noclip = false;

        // --- KAMERA CONFIG ---
        private bool _cameraMods = false;
        private float _camAngleMin = 10f;
        private float _camAngleMax = 89f;
        private float _targetAngle = 45f;
        private string _camMaxDistStr = "150";

        // --- RENDER CONFIG ---
        private string _renderDistStr = "2000";
        private bool _renderMods = false;

        // --- ZMIENNE POMOCNICZE ---
        private float _defaultMaxDist = -1f;
        private float _defaultZoomSpeed = -1f;
        private GameObject _playerLightObj;

        // Cache
        private MonoBehaviour _activeCameraScript;
        private bool _isWTRPG = false;
        private float _cacheTimer = 0f;

        // NoClip State
        private bool _wasNoclipActive = false;

        // --- GUI ---
        private Vector2 _scrollPos;

        public void DrawMenu()
        {
            // Usunięto sztywne Height, aby okno było skalowalne
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            GUILayout.Label("<b>--- RENDEROWANIE & POGODA ---</b>");

            _eternalDay = GUILayout.Toggle(_eternalDay, "Wieczny Dzień (12:00)");
            _noFog = GUILayout.Toggle(_noFog, "Usuń Mgłę (No Fog)");

            GUILayout.BeginHorizontal();
            _renderMods = GUILayout.Toggle(_renderMods, "Wymuś Zasięg:");
            _renderDistStr = GUILayout.TextField(_renderDistStr, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("<b>--- KAMERA ---</b>");

            if (_activeCameraScript != null)
                GUILayout.Label($"Cam: {_activeCameraScript.GetType().Name} [OK]");

            GUILayout.BeginHorizontal();
            _cameraMods = GUILayout.Toggle(_cameraMods, "Wymuś Zoom:");
            _camMaxDistStr = GUILayout.TextField(_camMaxDistStr, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.Label($"Kąt Kamery: {_targetAngle:F0}");
            _targetAngle = GUILayout.HorizontalSlider(_targetAngle, _camAngleMin, _camAngleMax);

            GUILayout.Space(10);
            GUILayout.Label("<b>--- GRACZ ---</b>");

            bool newLight = GUILayout.Toggle(_brightPlayer, "Latarka Gracza");
            if (newLight != _brightPlayer)
            {
                _brightPlayer = newLight;
                TogglePlayerLight(_brightPlayer);
            }

            GUI.backgroundColor = _noclip ? Color.red : Color.white;
            if (GUILayout.Button(_noclip ? "NOCLIP: WŁĄCZONY (WASD)" : "Włącz NoClip (Przechodzenie)"))
            {
                _noclip = !_noclip;
            }
            GUI.backgroundColor = Color.white;
            if (_noclip) GUILayout.Label("<color=yellow>Użyj WASD + Shift/Ctrl</color>");

            GUILayout.EndScrollView();
        }

        public void Update()
        {
            if (_eternalDay) ForceDayTime();
            if (_noFog) DisableFog();
            if (_cameraMods) ApplyCameraSettings();
            if (_renderMods) ApplyRenderSettings();

            HandleNoClip();
        }

        // --- LOGIKA NOCLIP (Refleksja) ---
        private void HandleNoClip()
        {
            var player = global::Player.localPlayer;
            if (player == null) return;

            if (_noclip)
            {
                SetComponentEnabled(player.gameObject, "UnityEngine.AI.NavMeshAgent", false);
                SetComponentEnabled(player.gameObject, "UnityEngine.Collider", false);

                _wasNoclipActive = true;

                float speed = 10.0f * Time.deltaTime;
                if (Input.GetKey(KeyCode.LeftShift)) speed *= 3f;

                Vector3 moveDir = Vector3.zero;
                Transform camTrans = Camera.main.transform;
                Vector3 fwd = camTrans.forward;
                Vector3 right = camTrans.right;
                fwd.y = 0; right.y = 0;
                fwd.Normalize(); right.Normalize();

                if (Input.GetKey(KeyCode.W)) moveDir += fwd;
                if (Input.GetKey(KeyCode.S)) moveDir -= fwd;
                if (Input.GetKey(KeyCode.A)) moveDir -= right;
                if (Input.GetKey(KeyCode.D)) moveDir += right;
                if (Input.GetKey(KeyCode.Space)) moveDir.y += 1f;
                if (Input.GetKey(KeyCode.LeftControl)) moveDir.y -= 1f;

                if (moveDir != Vector3.zero) player.transform.position += moveDir * speed;
            }
            else
            {
                if (_wasNoclipActive)
                {
                    SetComponentEnabled(player.gameObject, "UnityEngine.AI.NavMeshAgent", true);
                    SetComponentEnabled(player.gameObject, "UnityEngine.Collider", true);
                    _wasNoclipActive = false;
                }
            }
        }

        private void SetComponentEnabled(GameObject go, string typeName, bool state)
        {
            Component c = go.GetComponent(typeName);
            if (c != null)
            {
                PropertyInfo prop = c.GetType().GetProperty("enabled");
                if (prop != null && prop.CanWrite) prop.SetValue(c, state, null);
            }
        }

        // --- LOGIKA RENDEROWANIA ---
        private void ApplyRenderSettings()
        {
            if (float.TryParse(_renderDistStr, out float dist))
            {
                if (Camera.main != null) Camera.main.farClipPlane = dist;
                SetTerrainDistance(dist);
            }
        }

        private void SetTerrainDistance(float dist)
        {
            Type terrainType = Type.GetType("UnityEngine.Terrain, UnityEngine.TerrainModule");
            if (terrainType == null) terrainType = Type.GetType("UnityEngine.Terrain, UnityEngine");

            if (terrainType != null)
            {
                var activeProp = terrainType.GetProperty("activeTerrains", BindingFlags.Public | BindingFlags.Static);
                if (activeProp != null)
                {
                    var terrains = activeProp.GetValue(null, null) as Array;
                    if (terrains != null)
                    {
                        foreach (object t in terrains)
                        {
                            SetProp(t, "treeDistance", dist);
                            SetProp(t, "detailObjectDistance", dist);
                            SetProp(t, "basemapDistance", dist);
                            SetProp(t, "heightmapPixelError", 1.0f);
                        }
                    }
                }
            }
        }

        private void SetProp(object obj, string name, object val)
        {
            if (obj == null) return;
            var prop = obj.GetType().GetProperty(name);
            if (prop != null && prop.CanWrite) prop.SetValue(obj, val, null);
        }

        // --- LOGIKA MGŁY ---
        private void DisableFog()
        {
            RenderSettings.fog = false;
            RenderSettings.fogDensity = 0.0f;
            RenderSettings.fogStartDistance = 100000f;
            RenderSettings.fogEndDistance = 200000f;
            RenderSettings.fogMode = FogMode.Linear;
        }

        // --- LOGIKA KAMERY ---
        private void ApplyCameraSettings()
        {
            if (_activeCameraScript == null || Time.time > _cacheTimer)
            {
                FindCameraScript();
                _cacheTimer = Time.time + 1.0f;
            }

            if (_activeCameraScript != null && float.TryParse(_camMaxDistStr, out float maxDist))
            {
                if (_isWTRPG)
                {
                    var rpgCam = _activeCameraScript as JohnStairs.RCC.ThirdPerson.WTRPGCamera;
                    if (rpgCam != null)
                    {
                        if (_defaultMaxDist == -1f) _defaultMaxDist = rpgCam.MaxDistance;
                        rpgCam.MaxDistance = maxDist;
                        rpgCam.ZoomSensitivity = 60f;
                        rpgCam.RotationYMin = _targetAngle;
                        rpgCam.RotationYMax = _targetAngle;
                    }
                }
                else
                {
                    var mmoCam = _activeCameraScript as global::CameraMMO;
                    if (mmoCam != null)
                    {
                        if (_defaultMaxDist == -1f) _defaultMaxDist = mmoCam.maxDistance;
                        mmoCam.maxDistance = maxDist;
                        mmoCam.zoomSpeedMouse = 5.0f;
                        mmoCam.xMinAngle = -_targetAngle;
                        mmoCam.xMaxAngle = _targetAngle;
                    }
                }
            }
        }

        private void FindCameraScript()
        {
            var rpg = JohnStairs.RCC.ThirdPerson.WTRPGCamera.instance;
            if (rpg == null) rpg = UnityEngine.Object.FindObjectOfType<JohnStairs.RCC.ThirdPerson.WTRPGCamera>();
            if (rpg != null) { _activeCameraScript = rpg; _isWTRPG = true; return; }

            var mmo = UnityEngine.Object.FindObjectOfType<global::CameraMMO>();
            if (mmo != null) { _activeCameraScript = mmo; _isWTRPG = false; return; }
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
                        rpgCam.RotationYMin = 55f;
                        rpgCam.RotationYMax = 55f;
                    }
                }
                else
                {
                    var mmoCam = _activeCameraScript as global::CameraMMO;
                    if (mmoCam != null) mmoCam.maxDistance = _defaultMaxDist;
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