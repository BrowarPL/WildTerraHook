using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

namespace WildTerraHook
{
    public class ResourceEspModule
    {
        // --- USTAWIENIA ---
        public bool EspEnabled = false;
        public bool ShowBoxes = true;
        public bool ShowXRay = true;
        public bool ShowCastBars = true;
        public bool DebugCastVars = false; // "Deep Scan" Debugger

        // Kategorie
        private bool _showResources = false;
        private bool _showMining = false;
        private bool _showGathering = false;
        private bool _showLumber = false;
        private bool _showGodsend = false;
        private bool _showOthers = false;

        private bool _showMobs = false;
        private bool _showAggressive = false;
        private bool _showRetaliating = false;
        private bool _showPassive = false;

        private bool _showColorMenu = false;
        private float _maxDistance = 150f;

        // Toggle Lists
        private Dictionary<string, bool> _miningToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _gatheringToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lumberToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _godsendToggles = new Dictionary<string, bool>();

        private string[] _ignoreKeywords = { "Anvil", "Table", "Bench", "Rack", "Stove", "Kiln", "Furnace", "Chair", "Bed", "Chest", "Box", "Crate", "Basket", "Fence", "Wall", "Floor", "Roof", "Window", "Door", "Gate", "Sign", "Decor", "Torch", "Lamp", "Rug", "Carpet", "Pillar", "Beam", "Stairs", "Foundation", "Road", "Path", "Walkway" };

        // Cache
        private List<CachedObject> _cachedObjects = new List<CachedObject>();
        private float _lastScanTime = 0f;
        private float _scanInterval = 1.0f;

        // X-RAY & MAT
        private Material _xrayMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        // --- CAST BAR LOGIC ---
        // Tu wpiszemy poprawne dane jak je znajdziemy debugerem
        private string _foundComponentName = "";
        private string _foundFieldName = "";

        // GUI
        private GUIStyle _styleLabel;
        private GUIStyle _styleBackground;
        private Texture2D _bgTexture;
        private Texture2D _boxTexture;
        private Texture2D _castBarTexture;
        private Texture2D _castBackgroundTexture;
        private Vector2 _scrollPos;
        private Vector2 _debugScroll;

        private struct CachedObject
        {
            public GameObject GameObject;
            public Vector3 Position;
            public Transform Transform;
            public string Label;
            public Color Color;
            public string HpText;
            public bool IsMob;
            public float Height;
            public bool ShouldGlow;
            public Renderer[] Renderers;
        }

        public ResourceEspModule()
        {
            InitializeLists();
            LoadFromConfig();
            CreateXRayMaterial();
        }

        private void InitializeLists()
        {
            string[] mining = { "Rock", "Copper", "Tin", "Limestone", "Coal", "Sulfur", "Iron", "Marblestone", "Arsenic", "Zuperit", "Mortuus", "Sangit" };
            foreach (var s in mining) _miningToggles[s] = false;

            string[] gathering = { "Wild root", "Boletus", "Chanterelles", "Morels", "MushroomRussulas", "MushroomAmanitaGrey", "MushroomAmanitaRed", "WoodPile", "Stone pile", "Wild cereals", "Blueberry", "Nest", "NettlePlant", "Clay", "Hazel", "Greenary", "Lingonberry", "Beehive", "Swamp thorn", "Mountain sage", "Wolf berries", "Chelidonium", "Sand", "Strawberry" };
            foreach (var s in gathering) _gatheringToggles[s] = false;

            string[] lumber = { "Apple tree", "Snag", "Birch", "Grave tree", "Stump", "Pine", "Maple", "Poplar", "Spruce", "Dried tree", "Oak", "Grim tree", "Infected grim tree" };
            foreach (var s in lumber) _lumberToggles[s] = false;

            string[] godsend = { "Godsend" };
            foreach (var s in godsend) _godsendToggles[s] = false;
        }

        private void LoadFromConfig()
        {
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Mining, _miningToggles);
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Gather, _gatheringToggles);
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Lumber, _lumberToggles);
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Godsend, _godsendToggles);
            ShowCastBars = ConfigManager.Esp_ShowCastBars;
        }

        private void SyncToConfig()
        {
            ConfigManager.Esp_List_Mining = ConfigManager.SerializeToggleList(_miningToggles);
            ConfigManager.Esp_List_Gather = ConfigManager.SerializeToggleList(_gatheringToggles);
            ConfigManager.Esp_List_Lumber = ConfigManager.SerializeToggleList(_lumberToggles);
            ConfigManager.Esp_List_Godsend = ConfigManager.SerializeToggleList(_godsendToggles);
            ConfigManager.Esp_ShowCastBars = ShowCastBars;
            ConfigManager.Save();
        }

        private void CreateXRayMaterial()
        {
            Shader shader = Shader.Find("GUI/Text Shader");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _xrayMaterial = new Material(shader);
                _xrayMaterial.SetInt("_ZTest", 8);
                _xrayMaterial.SetInt("_ZWrite", 0);
                _xrayMaterial.SetInt("_Cull", 0);
                _xrayMaterial.renderQueue = 5000;
            }
        }

        public void Update()
        {
            if (!ConfigManager.Esp_Enabled)
            {
                ClearAllXRay();
                return;
            }

            if (Time.time - _lastScanTime > _scanInterval)
            {
                ScanObjects();
                _lastScanTime = Time.time;
            }
        }

        private void ScanObjects()
        {
            Vector3 playerPos = Vector3.zero;
            if (global::Player.localPlayer != null) playerPos = global::Player.localPlayer.transform.position;
            else if (Camera.main != null) playerPos = Camera.main.transform.position;

            List<CachedObject> newCache = new List<CachedObject>();
            HashSet<Renderer> currentRenderers = new HashSet<Renderer>();

            try
            {
                if (ConfigManager.Esp_ShowResources)
                {
                    List<string> activeMining = GetActiveKeys(_miningToggles);
                    List<string> activeGather = GetActiveKeys(_gatheringToggles);
                    List<string> activeLumber = GetActiveKeys(_lumberToggles);
                    List<string> activeGodsend = GetActiveKeys(_godsendToggles);

                    var objects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();

                    foreach (var obj in objects)
                    {
                        if (obj == null) continue;
                        if ((obj.transform.position - playerPos).sqrMagnitude > (ConfigManager.Esp_Distance * ConfigManager.Esp_Distance)) continue;

                        string name = obj.name;
                        if (IsIgnored(name)) continue;

                        bool matched = false;
                        if (ConfigManager.Esp_Cat_Mining && CheckList(name, activeMining, obj, ConfigManager.Colors.ResMining, newCache, false)) matched = true;
                        else if (ConfigManager.Esp_Cat_Gather && CheckList(name, activeGather, obj, ConfigManager.Colors.ResGather, newCache, false)) matched = true;
                        else if (ConfigManager.Esp_Cat_Lumber && CheckList(name, activeLumber, obj, ConfigManager.Colors.ResLumber, newCache, false)) matched = true;
                        else if (ConfigManager.Esp_Cat_Godsend && CheckList(name, activeGodsend, obj, new Color(0.8f, 0f, 1f), newCache, false)) matched = true;

                        if (!matched && ConfigManager.Esp_Cat_Others && !name.Contains("Player") && !name.Contains("Character"))
                        {
                            AddToCache(newCache, obj.gameObject, obj.transform.position, obj.transform, name, Color.white, "", false, 0f, false);
                            matched = true;
                        }
                    }
                }

                if (ConfigManager.Esp_ShowMobs)
                {
                    var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                    foreach (var mob in mobs)
                    {
                        if (mob != null && mob.health > 0)
                        {
                            if ((mob.transform.position - playerPos).sqrMagnitude > (ConfigManager.Esp_Distance * ConfigManager.Esp_Distance)) continue;
                            ProcessMob(mob, newCache);
                        }
                    }
                }
            }
            catch { }

            foreach (var item in newCache)
            {
                if (item.Renderers != null) foreach (var r in item.Renderers) currentRenderers.Add(r);
            }

            List<Renderer> toRemove = new List<Renderer>();
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key == null || !currentRenderers.Contains(kvp.Key))
                {
                    if (kvp.Key != null) kvp.Key.materials = kvp.Value;
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var r in toRemove) _originalMaterials.Remove(r);

            if (ConfigManager.Esp_ShowXRay)
            {
                foreach (var item in newCache)
                {
                    if (item.ShouldGlow) ApplyXRay(item.Renderers, item.Color);
                    else RestoreOriginal(item.Renderers);
                }
            }
            else
            {
                ClearAllXRay();
            }

            _cachedObjects = newCache;
        }

        private void ClearAllXRay()
        {
            foreach (var kvp in _originalMaterials)
            {
                if (kvp.Key != null) kvp.Key.materials = kvp.Value;
            }
            _originalMaterials.Clear();
        }

        private void RestoreOriginal(Renderer[] renderers)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r != null && _originalMaterials.ContainsKey(r))
                {
                    r.materials = _originalMaterials[r];
                    _originalMaterials.Remove(r);
                }
            }
        }

        private List<string> GetActiveKeys(Dictionary<string, bool> dict)
        {
            List<string> active = new List<string>();
            foreach (var kvp in dict) if (kvp.Value) active.Add(kvp.Key);
            return active;
        }

        private bool CheckList(string objName, List<string> activeKeys, global::WTObject obj, Color color, List<CachedObject> cache, bool isMob)
        {
            if (activeKeys.Count == 0) return false;
            foreach (var key in activeKeys)
            {
                if (objName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddToCache(cache, obj.gameObject, obj.transform.position, obj.transform, key, color, "", isMob, 0f, true);
                    return true;
                }
            }
            return false;
        }

        private void ProcessMob(global::WTMob mob, List<CachedObject> cache)
        {
            string name = mob.name;
            int hp = mob.health;
            int maxHp = hp;
            try
            {
                var f = mob.GetType().GetField("healthMax");
                if (f != null) maxHp = (int)f.GetValue(mob);
            }
            catch { }
            string hpStr = $" [HP: {hp}/{maxHp}]";

            float height = 1.8f;
            try { var col = mob.GetComponent<Collider>(); if (col != null) height = col.bounds.size.y; } catch { }

            bool isAggro = name.Contains("LargeFox") || name.Contains("Boss") || name.Contains("King") || name.Contains("Elite") || name.Contains("Bear") || name.Contains("Wolf");
            bool isPassive = name.Contains("Hare") || name.Contains("Deer") || name.Contains("Stag") || name.Contains("Cow") || name.Contains("Sheep");

            Color textColor = Color.red;
            string label = name;
            bool show = false;

            if (isAggro)
            {
                if (ConfigManager.Esp_Mob_Aggro) { textColor = ConfigManager.Colors.MobAggressive; label = "[!] " + name; show = true; }
            }
            else if (isPassive)
            {
                if (ConfigManager.Esp_Mob_Passive) { textColor = ConfigManager.Colors.MobPassive; show = true; }
            }
            else
            {
                if (ConfigManager.Esp_Mob_Retal) { textColor = ConfigManager.Colors.MobFleeing; show = true; }
            }

            if (show)
            {
                AddToCache(cache, mob.gameObject, mob.transform.position, mob.transform, label, textColor, hpStr, true, height, true);
            }
        }

        private void AddToCache(List<CachedObject> cache, GameObject go, Vector3 pos, Transform tr, string label, Color col, string hp, bool isMob, float h, bool glow)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            cache.Add(new CachedObject
            {
                GameObject = go,
                Position = pos,
                Transform = tr,
                Label = label,
                Color = col,
                HpText = hp,
                IsMob = isMob,
                Height = h,
                ShouldGlow = glow,
                Renderers = rends
            });
        }

        private void ApplyXRay(Renderer[] renderers, Color color)
        {
            if (renderers == null || _xrayMaterial == null) return;
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!_originalMaterials.ContainsKey(r)) _originalMaterials[r] = r.sharedMaterials;

                var currentMats = r.materials;
                bool hasXRay = false;
                if (currentMats.Length > 0 && currentMats[currentMats.Length - 1].shader.name == _xrayMaterial.shader.name)
                {
                    Color targetColor = new Color(color.r, color.g, color.b, 0.5f);
                    if (currentMats[currentMats.Length - 1].color != targetColor) currentMats[currentMats.Length - 1].color = targetColor;
                    hasXRay = true;
                }

                if (!hasXRay)
                {
                    Material[] newMats = new Material[currentMats.Length + 1];
                    Array.Copy(currentMats, newMats, currentMats.Length);
                    Material instanceXRay = new Material(_xrayMaterial);
                    instanceXRay.color = new Color(color.r, color.g, color.b, 0.5f);
                    newMats[newMats.Length - 1] = instanceXRay;
                    r.materials = newMats;
                }
            }
        }

        private bool IsIgnored(string name)
        {
            if (name.Length < 3) return true;
            foreach (var ignore in _ignoreKeywords)
                if (name.IndexOf(ignore, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void CreateStyles()
        {
            if (_bgTexture == null) { _bgTexture = new Texture2D(1, 1); _bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.75f)); _bgTexture.Apply(); }
            if (_boxTexture == null) { _boxTexture = new Texture2D(1, 1); _boxTexture.SetPixel(0, 0, Color.white); _boxTexture.Apply(); }
            if (_castBarTexture == null) { _castBarTexture = new Texture2D(1, 1); _castBarTexture.SetPixel(0, 0, ConfigManager.Colors.CastBar); _castBarTexture.Apply(); }
            if (_castBackgroundTexture == null) { _castBackgroundTexture = new Texture2D(1, 1); _castBackgroundTexture.SetPixel(0, 0, Color.black); _castBackgroundTexture.Apply(); }

            if (_styleBackground == null) { _styleBackground = new GUIStyle(); _styleBackground.normal.background = _bgTexture; }
            if (_styleLabel == null)
            {
                _styleLabel = new GUIStyle();
                _styleLabel.normal.textColor = Color.white;
                _styleLabel.alignment = TextAnchor.MiddleCenter;
                _styleLabel.fontSize = 11;
                _styleLabel.fontStyle = FontStyle.Bold;
            }
        }

        public void DrawMenu()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(450));

            bool newVal; float newFloat;

            newVal = GUILayout.Toggle(ConfigManager.Esp_Enabled, $"<b>{Localization.Get("ESP_MAIN_BTN")}</b>");
            if (newVal != ConfigManager.Esp_Enabled) { ConfigManager.Esp_Enabled = newVal; ConfigManager.Save(); }

            GUILayout.Space(5);

            if (ConfigManager.Esp_Enabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("ESP_DIST")}: {ConfigManager.Esp_Distance:F0}m", GUILayout.Width(100));
                newFloat = GUILayout.HorizontalSlider(ConfigManager.Esp_Distance, 20f, 300f);
                if (Math.Abs(newFloat - ConfigManager.Esp_Distance) > 0.1f) { ConfigManager.Esp_Distance = newFloat; ConfigManager.Save(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                newVal = GUILayout.Toggle(ConfigManager.Esp_ShowBoxes, "Box ESP");
                if (newVal != ConfigManager.Esp_ShowBoxes) { ConfigManager.Esp_ShowBoxes = newVal; ConfigManager.Save(); }

                newVal = GUILayout.Toggle(ConfigManager.Esp_ShowXRay, "X-Ray Glow");
                if (newVal != ConfigManager.Esp_ShowXRay) { ConfigManager.Esp_ShowXRay = newVal; ConfigManager.Save(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                newVal = GUILayout.Toggle(ShowCastBars, Localization.Get("ESP_CAST_BARS"));
                if (newVal != ShowCastBars) { ShowCastBars = newVal; SyncToConfig(); }

                DebugCastVars = GUILayout.Toggle(DebugCastVars, "Debug Cast Vars");
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                newVal = GUILayout.Toggle(ConfigManager.Esp_ShowResources, $"<b>{Localization.Get("ESP_RES_TITLE")}</b>");
                if (newVal != ConfigManager.Esp_ShowResources) { ConfigManager.Esp_ShowResources = newVal; ConfigManager.Save(); }

                if (ConfigManager.Esp_ShowResources)
                {
                    GUILayout.BeginHorizontal(); GUILayout.Space(10); GUILayout.BeginVertical();

                    newVal = GUILayout.Toggle(ConfigManager.Esp_Cat_Mining, Localization.Get("ESP_CAT_MINING"));
                    if (newVal != ConfigManager.Esp_Cat_Mining) { ConfigManager.Esp_Cat_Mining = newVal; ConfigManager.Save(); }
                    if (ConfigManager.Esp_Cat_Mining) DrawDictionary(_miningToggles);

                    newVal = GUILayout.Toggle(ConfigManager.Esp_Cat_Gather, Localization.Get("ESP_CAT_GATHER"));
                    if (newVal != ConfigManager.Esp_Cat_Gather) { ConfigManager.Esp_Cat_Gather = newVal; ConfigManager.Save(); }
                    if (ConfigManager.Esp_Cat_Gather) DrawDictionary(_gatheringToggles);

                    newVal = GUILayout.Toggle(ConfigManager.Esp_Cat_Lumber, Localization.Get("ESP_CAT_LUMBER"));
                    if (newVal != ConfigManager.Esp_Cat_Lumber) { ConfigManager.Esp_Cat_Lumber = newVal; ConfigManager.Save(); }
                    if (ConfigManager.Esp_Cat_Lumber) DrawDictionary(_lumberToggles);

                    GUILayout.Space(5);
                    newVal = GUILayout.Toggle(ConfigManager.Esp_Cat_Godsend, Localization.Get("ESP_CAT_GODSEND"));
                    if (newVal != ConfigManager.Esp_Cat_Godsend) { ConfigManager.Esp_Cat_Godsend = newVal; ConfigManager.Save(); }
                    if (ConfigManager.Esp_Cat_Godsend) DrawDictionary(_godsendToggles);

                    GUILayout.Space(5);
                    newVal = GUILayout.Toggle(ConfigManager.Esp_Cat_Others, Localization.Get("ESP_CAT_OTHERS"));
                    if (newVal != ConfigManager.Esp_Cat_Others) { ConfigManager.Esp_Cat_Others = newVal; ConfigManager.Save(); }

                    GUILayout.EndVertical(); GUILayout.EndHorizontal();
                }

                GUILayout.Space(10);

                newVal = GUILayout.Toggle(ConfigManager.Esp_ShowMobs, $"<b>{Localization.Get("ESP_MOB_TITLE")}</b>");
                if (newVal != ConfigManager.Esp_ShowMobs) { ConfigManager.Esp_ShowMobs = newVal; ConfigManager.Save(); }

                if (ConfigManager.Esp_ShowMobs)
                {
                    GUILayout.BeginHorizontal(); GUILayout.Space(10); GUILayout.BeginVertical();
                    newVal = GUILayout.Toggle(ConfigManager.Esp_Mob_Aggro, Localization.Get("ESP_MOB_AGGRO"));
                    if (newVal != ConfigManager.Esp_Mob_Aggro) { ConfigManager.Esp_Mob_Aggro = newVal; ConfigManager.Save(); }
                    newVal = GUILayout.Toggle(ConfigManager.Esp_Mob_Retal, Localization.Get("ESP_MOB_RETAL"));
                    if (newVal != ConfigManager.Esp_Mob_Retal) { ConfigManager.Esp_Mob_Retal = newVal; ConfigManager.Save(); }
                    newVal = GUILayout.Toggle(ConfigManager.Esp_Mob_Passive, Localization.Get("ESP_MOB_PASSIVE"));
                    if (newVal != ConfigManager.Esp_Mob_Passive) { ConfigManager.Esp_Mob_Passive = newVal; ConfigManager.Save(); }
                    GUILayout.EndVertical(); GUILayout.EndHorizontal();
                }

                GUILayout.Space(15);
                if (GUILayout.Button(_showColorMenu ? Localization.Get("ESP_HIDE_COLORS") : Localization.Get("ESP_EDIT_COLORS")))
                {
                    _showColorMenu = !_showColorMenu;
                }
                if (_showColorMenu) DrawColorSettings();
            }
            GUILayout.EndScrollView();
        }

        private void DrawColorSettings()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("ESP_EDIT_COLORS")}</b>");
            DrawColorPicker(Localization.Get("COLOR_MOB_AGGRO"), ref ConfigManager.Colors.MobAggressive);
            DrawColorPicker(Localization.Get("COLOR_MOB_PASSIVE"), ref ConfigManager.Colors.MobPassive);
            DrawColorPicker(Localization.Get("COLOR_MOB_FLEE"), ref ConfigManager.Colors.MobFleeing);
            GUILayout.Space(5);
            DrawColorPicker(Localization.Get("COLOR_RES_MINE"), ref ConfigManager.Colors.ResMining);
            DrawColorPicker(Localization.Get("COLOR_RES_GATHER"), ref ConfigManager.Colors.ResGather);
            DrawColorPicker(Localization.Get("COLOR_RES_LUMB"), ref ConfigManager.Colors.ResLumber);
            if (GUILayout.Button(Localization.Get("ESP_SAVE_COLORS"))) ConfigManager.Save();
            GUILayout.EndVertical();
        }

        private void DrawColorPicker(string label, ref Color col)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120));
            GUI.color = col; GUILayout.Label("█", GUILayout.Width(20)); GUI.color = Color.white;
            GUILayout.BeginVertical();
            col.r = GUILayout.HorizontalSlider(col.r, 0f, 1f);
            col.g = GUILayout.HorizontalSlider(col.g, 0f, 1f);
            col.b = GUILayout.HorizontalSlider(col.b, 0f, 1f);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawDictionary(Dictionary<string, bool> dict)
        {
            GUILayout.BeginHorizontal(); GUILayout.Space(10); GUILayout.BeginVertical();
            var keys = new List<string>(dict.Keys);
            bool changed = false;
            foreach (var key in keys)
            {
                bool v = GUILayout.Toggle(dict[key], key);
                if (v != dict[key]) { dict[key] = v; changed = true; }
            }
            if (changed) SyncToConfig();
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
        }

        public void DrawESP()
        {
            if (!ConfigManager.Esp_Enabled) return;
            CreateStyles();
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 originPos = cam.transform.position;
            if (global::Player.localPlayer != null) originPos = global::Player.localPlayer.transform.position;
            float screenW = Screen.width;
            float screenH = Screen.height;

            CachedObject closestMob = new CachedObject();
            float minMobDist = float.MaxValue;

            foreach (var obj in _cachedObjects)
            {
                Vector3 currentPos = (obj.Transform != null) ? obj.Transform.position : obj.Position;
                float dist = Vector3.Distance(originPos, currentPos);
                if (dist > ConfigManager.Esp_Distance) continue;

                if (DebugCastVars && obj.IsMob && dist < minMobDist) { minMobDist = dist; closestMob = obj; }

                Vector3 screenHead = cam.WorldToScreenPoint(currentPos + Vector3.up * obj.Height);
                Vector3 screenFeet = cam.WorldToScreenPoint(currentPos);

                bool isBehind = screenHead.z < 0;
                bool isOffScreen = isBehind || screenHead.x < 0 || screenHead.x > screenW || screenHead.y < 0 || screenHead.y > screenH;

                if (isOffScreen)
                {
                    Vector3 screenPos = screenHead;
                    if (isBehind) { screenPos.x *= -1; screenPos.y *= -1; }
                    Vector3 screenCenter = new Vector3(screenW / 2, screenH / 2, 0);
                    screenPos -= screenCenter;
                    float angle = Mathf.Atan2(screenPos.y, screenPos.x);
                    angle -= 90 * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle); float sin = -Mathf.Sin(angle);
                    float m = cos / sin;
                    Vector3 screenBounds = screenCenter; screenBounds.x -= 20; screenBounds.y -= 20;
                    if (cos > 0) screenPos = new Vector3(screenBounds.y / m, screenBounds.y, 0);
                    else screenPos = new Vector3(-screenBounds.y / m, -screenBounds.y, 0);
                    if (screenPos.x > screenBounds.x) screenPos = new Vector3(screenBounds.x, screenBounds.x * m, 0);
                    else if (screenPos.x < -screenBounds.x) screenPos = new Vector3(-screenBounds.x, -screenBounds.x * m, 0);
                    screenPos += screenCenter;
                    screenPos.y = screenH - screenPos.y;
                    DrawLabelWithBackground(screenPos, obj.Label, obj.Color);
                }
                else
                {
                    float feetY = screenH - screenFeet.y;
                    float headY = screenH - screenHead.y;
                    if (obj.IsMob && ConfigManager.Esp_ShowBoxes)
                    {
                        float boxHeight = Mathf.Abs(feetY - headY);
                        if (boxHeight < 5) boxHeight = 5;
                        float boxWidth = boxHeight * 0.6f;
                        float boxX = screenFeet.x - boxWidth / 2;
                        float boxY = headY;
                        DrawBoxOutline(new Rect(boxX, boxY, boxWidth, boxHeight), obj.Color, 2f);
                    }

                    string text = $"{obj.Label} [{dist:F0}m]{obj.HpText}";
                    Vector2 textPos = new Vector2(screenHead.x, headY - 15);
                    DrawLabelWithBackground(textPos, text, obj.Color);

                    // Pasek castowania
                    if (obj.IsMob && ShowCastBars)
                    {
                        // Jeśli znamy zmienne, rysuj prawdziwy pasek
                        if (!string.IsNullOrEmpty(_foundFieldName))
                        {
                            DrawRealCastBar(obj, textPos);
                        }
                        // Fallback: Jeśli mob ma Animatora, sprawdź czy gra animację castowania
                        else
                        {
                            DrawAnimatorCastBar(obj, textPos);
                        }
                    }
                }
            }

            if (DebugCastVars && closestMob.GameObject != null) DrawDeepDebugInfo(closestMob);
        }

        private void DrawRealCastBar(CachedObject obj, Vector2 textPos)
        {
            // Tu w przyszłości wstawimy kod do czytania znalezionego pola
        }

        private void DrawAnimatorCastBar(CachedObject obj, Vector2 textPos)
        {
            // Fallback: Wykrywanie animacji
            try
            {
                Animator anim = obj.GameObject.GetComponent<Animator>();
                if (anim == null) return;

                AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);

                // Sprawdź tag lub nazwę stanu (przykładowe słowa kluczowe)
                // W trybie debugera zobaczysz nazwy stanów, to je tu dopiszemy
                bool isCasting = state.IsTag("Cast") || state.IsTag("Skill") || state.IsTag("Attack");

                if (isCasting)
                {
                    float progress = state.normalizedTime % 1.0f;
                    float barW = 60f; float barH = 6f;
                    Rect barRect = new Rect(textPos.x - barW / 2, textPos.y + 20, barW, barH);

                    GUI.DrawTexture(barRect, _castBackgroundTexture);
                    GUI.DrawTexture(new Rect(barRect.x, barRect.y, barW * progress, barH), _castBarTexture);
                }
            }
            catch { }
        }

        // --- DEEP DEBUGGER ---
        private void DrawDeepDebugInfo(CachedObject mob)
        {
            float x = Screen.width - 450;
            float y = 100;
            float w = 440;
            float h = 600;

            GUI.Box(new Rect(x, y, w, h), $"DEBUG: {mob.Label}");
            _debugScroll = GUI.BeginScrollView(new Rect(x, y + 20, w, h - 20), _debugScroll, new Rect(0, 0, w - 20, 2000));

            float currY = 0;

            // 1. Sprawdź Animator
            try
            {
                Animator anim = mob.GameObject.GetComponent<Animator>();
                if (anim != null)
                {
                    AnimatorStateInfo s = anim.GetCurrentAnimatorStateInfo(0);
                    GUI.Label(new Rect(5, currY, 400, 20), $"[Animator] State Hash: {s.shortNameHash}, NormTime: {s.normalizedTime:F2}");
                    currY += 20;
                }
            }
            catch { }

            // 2. Skaner Komponentów
            MonoBehaviour[] scripts = mob.GameObject.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script == null) continue;
                Type t = script.GetType();

                // Ignoruj standardowe Unity
                if (t.Namespace != null && (t.Namespace.StartsWith("UnityEngine") || t.Namespace.StartsWith("System"))) continue;

                GUI.Label(new Rect(5, currY, 400, 20), $"<b>--- {t.Name} ---</b>");
                currY += 20;

                var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(double))
                    {
                        try
                        {
                            object val = f.GetValue(script);
                            // Pokaż tylko jeśli > 0 (żeby odsiać śmieci)
                            if (Convert.ToSingle(val) > 0.001f)
                            {
                                GUI.Label(new Rect(5, currY, 400, 20), $"{f.Name} = {val}");
                                currY += 20;
                            }
                        }
                        catch { }
                    }
                }
            }

            GUI.EndScrollView();
        }

        private void DrawLabelWithBackground(Vector2 centerBottomPos, string text, Color color)
        {
            GUIContent content = new GUIContent(text);
            Vector2 size = _styleLabel.CalcSize(content);
            Rect r = new Rect(centerBottomPos.x - size.x / 2, centerBottomPos.y - size.y, size.x, size.y);
            Rect bgRect = new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4);
            GUI.Box(bgRect, GUIContent.none, _styleBackground);
            _styleLabel.normal.textColor = color;
            GUI.Label(r, text, _styleLabel);
        }

        private void DrawBoxOutline(Rect r, Color color, float thickness)
        {
            DrawLine(new Vector2(r.x, r.y), new Vector2(r.x + r.width, r.y), color, thickness);
            DrawLine(new Vector2(r.x, r.y + r.height), new Vector2(r.x + r.width, r.y + r.height), color, thickness);
            DrawLine(new Vector2(r.x, r.y), new Vector2(r.x, r.y + r.height), color, thickness);
            DrawLine(new Vector2(r.x + r.width, r.y), new Vector2(r.x + r.width, r.y + r.height), color, thickness);
        }

        private void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width)
        {
            if (_boxTexture == null) return;
            float angle = Mathf.Rad2Deg * Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x);
            float length = Vector2.Distance(pointA, pointB);
            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.color = color;
            GUI.DrawTexture(new Rect(pointA.x, pointA.y, length, width), _boxTexture);
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(-angle, pointA);
        }
    }
}