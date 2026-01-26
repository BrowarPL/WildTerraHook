using UnityEngine;
using System;
using JohnStairs.RCC.ThirdPerson;

namespace WildTerraHook
{
    public class MiscModule
    {
        private GameObject _lightObject;
        private float _defaultFov = 60f;
        private bool _defaultsInitialized = false;
        private float _originalRenderDist = 500f;
        private ShadowQuality _originalShadows;
        private bool _fullbrightActive = false;
        private WTRPGCamera _rpgCamera;
        private global::CameraMMO _mmoCamera;
        private float _cacheTimer = 0f;
        private float _lastButcherTime = 0f;
        private float _butcherInterval = 0.5f;

        public void Update()
        {
            if (global::Player.localPlayer == null) return;

            if (!_defaultsInitialized && Camera.main != null)
            {
                _defaultFov = Camera.main.fieldOfView;
                _originalRenderDist = Camera.main.farClipPlane;
                _defaultsInitialized = true;
            }

            if (Camera.main != null)
            {
                if (Math.Abs(Camera.main.farClipPlane - ConfigManager.Misc_RenderDistance) > 1f)
                {
                    Camera.main.farClipPlane = ConfigManager.Misc_RenderDistance;
                }
            }

            if (ConfigManager.Misc_EternalDay && global::EnviroSky.instance != null)
                global::EnviroSky.instance.SetTime(global::EnviroSky.instance.GameTime.Years, global::EnviroSky.instance.GameTime.Days, 12, 0, 0);

            if (ConfigManager.Misc_NoFog) ApplyNoFog();

            HandleFullbright();
            HandleBrightPlayer();
            HandleFov();
            if (ConfigManager.Misc_ZoomHack) HandleZoomHack();

            if (ConfigManager.Misc_AutoButcher && Time.time - _lastButcherTime > _butcherInterval)
            {
                _lastButcherTime = Time.time;
                AutoButcherLoop();
            }
        }

        public void OnGUI() { }

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
            if (ConfigManager.Misc_Fullbright)
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
                if (Math.Abs(Camera.main.fieldOfView - ConfigManager.Misc_Fov) > 0.1f)
                    Camera.main.fieldOfView = ConfigManager.Misc_Fov;
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
                _rpgCamera.MaxDistance = ConfigManager.Misc_ZoomLimit;
                _rpgCamera.RotationYMin = ConfigManager.Misc_CamAngle;
                _rpgCamera.RotationYMax = ConfigManager.Misc_CamAngle;
                _rpgCamera.ZoomSensitivity = ConfigManager.Misc_ZoomSpeed;
                _rpgCamera.ReturnZoomFromNPC();
            }
            else if (_mmoCamera != null)
            {
                _mmoCamera.maxDistance = ConfigManager.Misc_ZoomLimit;
                _mmoCamera.xMinAngle = -ConfigManager.Misc_CamAngle;
                _mmoCamera.xMaxAngle = ConfigManager.Misc_CamAngle;
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
            if (ConfigManager.Misc_BrightPlayer)
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
                    lightComp.intensity = ConfigManager.Misc_LightIntensity;
                    lightComp.range = ConfigManager.Misc_LightRange;
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

            // RENDER DISTANCE
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Localization.Get("MISC_RENDER_DIST")}: {ConfigManager.Misc_RenderDistance:F0}", GUILayout.Width(150));
            float newDist = GUILayout.HorizontalSlider(ConfigManager.Misc_RenderDistance, 100f, 5000f);
            if (Math.Abs(newDist - ConfigManager.Misc_RenderDistance) > 1f) { ConfigManager.Misc_RenderDistance = newDist; ConfigManager.Save(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            bool newVal;

            newVal = GUILayout.Toggle(ConfigManager.Misc_EternalDay, Localization.Get("MISC_ETERNAL_DAY"));
            if (newVal != ConfigManager.Misc_EternalDay) { ConfigManager.Misc_EternalDay = newVal; ConfigManager.Save(); }

            newVal = GUILayout.Toggle(ConfigManager.Misc_NoFog, Localization.Get("MISC_NO_FOG"));
            if (newVal != ConfigManager.Misc_NoFog) { ConfigManager.Misc_NoFog = newVal; ConfigManager.Save(); }

            newVal = GUILayout.Toggle(ConfigManager.Misc_Fullbright, Localization.Get("MISC_FULLBRIGHT"));
            if (newVal != ConfigManager.Misc_Fullbright) { ConfigManager.Misc_Fullbright = newVal; ConfigManager.Save(); }

            GUILayout.Space(5);

            newVal = GUILayout.Toggle(ConfigManager.Misc_BrightPlayer, Localization.Get("MISC_BRIGHT_PLAYER"));
            if (newVal != ConfigManager.Misc_BrightPlayer) { ConfigManager.Misc_BrightPlayer = newVal; ConfigManager.Save(); }

            if (ConfigManager.Misc_BrightPlayer)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_LIGHT_INT")}: {ConfigManager.Misc_LightIntensity:F1}", GUILayout.Width(120));
                float newInt = GUILayout.HorizontalSlider(ConfigManager.Misc_LightIntensity, 1f, 5f);
                if (Math.Abs(newInt - ConfigManager.Misc_LightIntensity) > 0.1f) { ConfigManager.Misc_LightIntensity = newInt; ConfigManager.Save(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_LIGHT_RNG")}: {ConfigManager.Misc_LightRange:F0}", GUILayout.Width(120));
                float newRng = GUILayout.HorizontalSlider(ConfigManager.Misc_LightRange, 50f, 2000f);
                if (Math.Abs(newRng - ConfigManager.Misc_LightRange) > 1f) { ConfigManager.Misc_LightRange = newRng; ConfigManager.Save(); }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            newVal = GUILayout.Toggle(ConfigManager.Misc_ZoomHack, Localization.Get("MISC_ZOOM_TITLE"));
            if (newVal != ConfigManager.Misc_ZoomHack) { ConfigManager.Misc_ZoomHack = newVal; ConfigManager.Save(); }

            if (ConfigManager.Misc_ZoomHack)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_ZOOM_LIMIT")}: {ConfigManager.Misc_ZoomLimit:F0}", GUILayout.Width(120));
                float newLim = GUILayout.HorizontalSlider(ConfigManager.Misc_ZoomLimit, 20f, 200f);
                if (Math.Abs(newLim - ConfigManager.Misc_ZoomLimit) > 1f) { ConfigManager.Misc_ZoomLimit = newLim; ConfigManager.Save(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_CAM_ANGLE")}: {ConfigManager.Misc_CamAngle:F0}", GUILayout.Width(120));
                float newAng = GUILayout.HorizontalSlider(ConfigManager.Misc_CamAngle, 10f, 89f);
                if (Math.Abs(newAng - ConfigManager.Misc_CamAngle) > 1f) { ConfigManager.Misc_CamAngle = newAng; ConfigManager.Save(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("MISC_ZOOM_SENS")}: {ConfigManager.Misc_ZoomSpeed:F0}", GUILayout.Width(120));
                float newSpd = GUILayout.HorizontalSlider(ConfigManager.Misc_ZoomSpeed, 10f, 200f);
                if (Math.Abs(newSpd - ConfigManager.Misc_ZoomSpeed) > 1f) { ConfigManager.Misc_ZoomSpeed = newSpd; ConfigManager.Save(); }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Localization.Get("MISC_FOV")}: {ConfigManager.Misc_Fov:F0}", GUILayout.Width(120));
            float newFov = GUILayout.HorizontalSlider(ConfigManager.Misc_Fov, 30f, 120f);
            if (Math.Abs(newFov - ConfigManager.Misc_Fov) > 1f) { ConfigManager.Misc_Fov = newFov; ConfigManager.Save(); }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(Localization.Get("MISC_RESET")))
            {
                ConfigManager.Misc_Fov = _defaultFov;
                ConfigManager.Misc_RenderDistance = _originalRenderDist;
                ConfigManager.Misc_ZoomLimit = 100f;
                ConfigManager.Misc_CamAngle = 45f;
                ConfigManager.Misc_ZoomSpeed = 60f;
                ConfigManager.Misc_LightIntensity = 2.0f;
                ConfigManager.Misc_LightRange = 1000f;
                ConfigManager.Save();
            }

            GUILayout.EndVertical();

            bool newVal = GUILayout.Toggle(ConfigManager.Misc_AutoButcher, Localization.Get("MISC_AUTO_BUTCHER"));
            if (newVal != ConfigManager.Misc_AutoButcher)
            {
                ConfigManager.Misc_AutoButcher = newVal;
                ConfigManager.Save();
            }
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
        private void AutoButcherLoop()
        {
            if (global::Player.localPlayer == null || global::Player.localPlayer.myInventory == null) return;

            var inventory = global::Player.localPlayer.myInventory;
            for (int i = 0; i < inventory.slots.Count; i++)
            {
                var slot = inventory.slots[i];
                if (slot != null && slot.item != null && slot.item.template != null && slot.item.template.actions != null)
                {
                    for (int actionIndex = 0; actionIndex < slot.item.template.actions.Count; actionIndex++)
                    {
                        var action = slot.item.template.actions[actionIndex];
                        // Sprawdzamy czy akcja to Butcher (typ akcji dla oprawiania zwierzyny)
                        if (action.type == global::ItemActionType.Butcher)
                        {
                            global::Player.localPlayer.CmdUseItem(i, actionIndex);
                            return; // Wykonujemy jedną akcję na cykl pętli
                        }
                    }
                }
            }
        }
    }
}