using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WildTerraHook
{
    public class ResourceEspModule
    {
        private PersistentWorldModule _persistentModule;
        private bool _showColorMenu = false;

        // Toggle Lists
        private Dictionary<string, bool> _miningToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _gatheringToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lumberToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _godsendToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _dungeonsToggles = new Dictionary<string, bool>();

        private string[] _ignoreKeywords = {
            "Anvil", "Table", "Bench", "Rack", "Stove", "Kiln", "Furnace",
            "Chair", "Bed", "Chest", "Box", "Crate", "Basket", "Fence",
            "Wall", "Floor", "Roof", "Window", "Door", "Gate", "Sign",
            "Decor", "Torch", "Lamp", "Rug", "Carpet", "Pillar", "Beam",
            "Stairs", "Foundation", "Road", "Path", "Walkway"
        };

        private List<CachedObject> _cachedObjects = new List<CachedObject>();
        private float _lastScanTime = 0f;
        private float _scanInterval = 0.5f;

        private Dictionary<int, int> _maxHealthCache = new Dictionary<int, int>();
        private Material _xrayMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

        private GUIStyle _styleLabel;
        private GUIStyle _styleBackground;
        private Texture2D _bgTexture;
        private Texture2D _boxTexture;

        // --- FIX: Dodano zmienną do obsługi suwaka ---
        private Vector2 _scrollPos;

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
            public bool IsGhost;
        }

        public ResourceEspModule()
        {
            InitializeLists();
            LoadFromConfig();
            CreateXRayMaterial();

            _boxTexture = new Texture2D(1, 1);
            _boxTexture.SetPixel(0, 0, Color.white);
            _boxTexture.Apply();

            _styleLabel = new GUIStyle();
            _styleLabel.normal.textColor = Color.white;
            _styleLabel.fontSize = 12;
            _styleLabel.alignment = TextAnchor.MiddleCenter;
            _styleLabel.fontStyle = FontStyle.Bold;

            _styleBackground = new GUIStyle();
            _styleBackground.normal.background = _boxTexture;
        }

        public void SetPersistentModule(PersistentWorldModule module)
        {
            _persistentModule = module;
        }

        private void InitializeLists()
        {
            string[] mining = { "Rock", "Copper", "Tin", "Limestone", "Coal", "Sulfur", "Iron", "Marblestone", "Arsenic", "Zuperit", "Mortuus", "Sangit" };
            foreach (var s in mining) _miningToggles[s] = false;

            string[] gathering = { "WildRoot", "Boletus", "Chanterelles", "Morels", "MushroomRussulas", "MushroomAmanitaGrey", "MushroomAmanitaRed", "WoodPile", "StonePile", "WildCereals", "Blueberry", "Nest", "NettlePlant", "Clay", "Hazel", "Greenary", "Lingonberry", "Beehive", "SwampThornRootPlant", "MountainSagePlant", "WolfBerriesBush", "Chelidonium", "Sand", "Strawberry" };
            foreach (var s in gathering) _gatheringToggles[s] = false;

            string[] lumber = { "AppleTree", "Snag", "Birch", "GraveTree", "Stump", "Pine", "Maple", "Poplar", "Spruce", "DriedTree", "Oak", "GrimTree", "Infected grim tree" };
            foreach (var s in lumber) _lumberToggles[s] = false;

            string[] godsend = { "Godsend", "PlagueSkeletonsCorpses", "PlagueAbandinedResources" };
            foreach (var s in godsend) _godsendToggles[s] = false;

            string[] dungeons = { "DarkForestDunheonEnter", "HideoutEnter" };
            foreach (var s in dungeons) _dungeonsToggles[s] = false;
        }

        private void LoadFromConfig()
        {
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Mining, _miningToggles);
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Gather, _gatheringToggles);
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Lumber, _lumberToggles);
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Godsend, _godsendToggles);
            ConfigManager.DeserializeToggleList(ConfigManager.Esp_List_Dungeons, _dungeonsToggles);
        }

        private void SyncToConfig()
        {
            ConfigManager.Esp_List_Mining = ConfigManager.SerializeToggleList(_miningToggles);
            ConfigManager.Esp_List_Gather = ConfigManager.SerializeToggleList(_gatheringToggles);
            ConfigManager.Esp_List_Lumber = ConfigManager.SerializeToggleList(_lumberToggles);
            ConfigManager.Esp_List_Godsend = ConfigManager.SerializeToggleList(_godsendToggles);
            ConfigManager.Esp_List_Dungeons = ConfigManager.SerializeToggleList(_dungeonsToggles);
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
            if (!ConfigManager.Esp_Enabled) { ClearAllXRay(); return; }
            if (Time.time - _lastScanTime > _scanInterval) { ScanObjects(); _lastScanTime = Time.time; }
        }

        private void ScanObjects()
        {
            Vector3 playerPos = Vector3.zero;
            if (global::Player.localPlayer != null) playerPos = global::Player.localPlayer.transform.position;
            else if (Camera.main != null) playerPos = Camera.main.transform.position;

            List<CachedObject> newCache = new List<CachedObject>();
            HashSet<Renderer> currentRenderers = new HashSet<Renderer>();
            HashSet<int> activeMobIds = new HashSet<int>();

            try
            {
                if (ConfigManager.Esp_ShowResources)
                {
                    List<string> activeMining = GetActiveKeys(_miningToggles);
                    List<string> activeGather = GetActiveKeys(_gatheringToggles);
                    List<string> activeLumber = GetActiveKeys(_lumberToggles);
                    List<string> activeGodsend = GetActiveKeys(_godsendToggles);
                    List<string> activeDungeons = GetActiveKeys(_dungeonsToggles);

                    // 1. LIVE OBJECTS (Zasoby)
                    var objects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
                    foreach (var obj in objects)
                    {
                        if (obj == null) continue;
                        if ((obj.transform.position - playerPos).sqrMagnitude > (ConfigManager.Esp_Distance * ConfigManager.Esp_Distance)) continue;
                        if (IsIgnored(obj.name)) continue;
                        ProcessResource(obj.gameObject, obj.name, obj.transform.position, obj.transform, activeMining, activeGather, activeLumber, activeGodsend, activeDungeons, newCache, false);
                    }

                    // 2. PERSISTENT GHOSTS (Tylko zasoby)
                    if (_persistentModule != null && ConfigManager.Persistent_Enabled && _persistentModule.ResourceGhosts.Count > 0)
                    {
                        foreach (var ghost in _persistentModule.ResourceGhosts.Values)
                        {
                            if (ghost.VisualObj != null && ghost.VisualObj.activeSelf)
                            {
                                if ((ghost.Position - playerPos).sqrMagnitude > (ConfigManager.Esp_Distance * ConfigManager.Esp_Distance)) continue;
                                ProcessResource(ghost.VisualObj, ghost.Name, ghost.Position, ghost.VisualObj.transform, activeMining, activeGather, activeLumber, activeGodsend, activeDungeons, newCache, true);
                            }
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
                            activeMobIds.Add(mob.GetInstanceID());
                        }
                    }
                }
            }
            catch { }

            // Cleanup HP cache
            List<int> toRemoveHP = new List<int>();
            foreach (var key in _maxHealthCache.Keys) if (!activeMobIds.Contains(key)) toRemoveHP.Add(key);
            foreach (var k in toRemoveHP) _maxHealthCache.Remove(k);

            // Cleanup Renderers
            foreach (var item in newCache) if (item.Renderers != null) foreach (var r in item.Renderers) currentRenderers.Add(r);

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
            else ClearAllXRay();

            _cachedObjects = newCache;
        }

        private void GetMobInfo(string name, out Color color, out string label, out bool show)
        {
            bool isPassive = name.Contains("Hare") || name.Contains("Deer") || name.Contains("Stag") || name.Contains("Cow") || name.Contains("Sheep") || name.Contains("Crow") || name.Contains("Seagull");
            bool isRetal = (name.Contains("Fox") && !name.Contains("LargeFox") && !name.Contains("FoxLarge")) || name.Contains("Horse") || name.Contains("Goat") || (name.Contains("Boar") && !name.Contains("ZombieBoar"));

            color = ConfigManager.Colors.MobAggressive;
            label = name;
            show = false;

            if (isPassive) { if (ConfigManager.Esp_Mob_Passive) { color = ConfigManager.Colors.MobPassive; show = true; } }
            else if (isRetal) { if (ConfigManager.Esp_Mob_Retal) { color = ConfigManager.Colors.MobFleeing; show = true; } }
            else { if (ConfigManager.Esp_Mob_Aggro) { color = ConfigManager.Colors.MobAggressive; label = "[!] " + name; show = true; } }
        }

        private void ProcessMob(global::WTMob mob, List<CachedObject> cache)
        {
            int hp = mob.health;
            int id = mob.GetInstanceID();
            if (!_maxHealthCache.ContainsKey(id)) _maxHealthCache[id] = hp;
            else if (hp > _maxHealthCache[id]) _maxHealthCache[id] = hp;
            int maxHp = _maxHealthCache[id];
            string hpStr = $" [HP: {hp}/{maxHp}]";

            float height = 1.8f;
            try { var mobCol = mob.GetComponent<Collider>(); if (mobCol != null) height = mobCol.bounds.size.y; } catch { }

            GetMobInfo(mob.name, out Color col, out string label, out bool show);

            if (show) AddToCache(cache, mob.gameObject, mob.transform.position, mob.transform, label, col, hpStr, true, height, true, false);
        }

        private void ProcessResource(GameObject go, string name, Vector3 pos, Transform tr, List<string> mining, List<string> gather, List<string> lumber, List<string> godsend, List<string> dungeons, List<CachedObject> cache, bool isGhost)
        {
            string prefix = isGhost ? "[C] " : "";
            bool matched = false;
            if (ConfigManager.Esp_Cat_Mining && CheckList(name, mining, go, pos, tr, ConfigManager.Colors.ResMining, cache, false, prefix)) matched = true;
            else if (ConfigManager.Esp_Cat_Gather && CheckList(name, gather, go, pos, tr, ConfigManager.Colors.ResGather, cache, false, prefix)) matched = true;
            else if (ConfigManager.Esp_Cat_Lumber && CheckList(name, lumber, go, pos, tr, ConfigManager.Colors.ResLumber, cache, false, prefix)) matched = true;
            else if (ConfigManager.Esp_Cat_Godsend && CheckList(name, godsend, go, pos, tr, new Color(0.8f, 0f, 1f), cache, false, prefix)) matched = true;
            else if (ConfigManager.Esp_Cat_Dungeons && CheckList(name, dungeons, go, pos, tr, new Color(1f, 0.5f, 0f), cache, false, prefix)) matched = true;

            if (!matched && ConfigManager.Esp_Cat_Others && !name.Contains("Player") && !name.Contains("Character") && !isGhost)
            {
                AddToCache(cache, go, pos, tr, name, Color.white, "", false, 0f, false, isGhost);
            }
        }

        private bool CheckList(string objName, List<string> activeKeys, GameObject go, Vector3 pos, Transform tr, Color color, List<CachedObject> cache, bool isMob, string prefix)
        {
            if (activeKeys.Count == 0) return false;
            foreach (var key in activeKeys)
            {
                if (objName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Color finalColor = color;
                    if (prefix != "") finalColor.a = 0.7f;
                    AddToCache(cache, go, pos, tr, prefix + key, finalColor, "", isMob, 0f, true, prefix != "");
                    return true;
                }
            }
            return false;
        }

        private void AddToCache(List<CachedObject> cache, GameObject go, Vector3 pos, Transform tr, string label, Color col, string hp, bool isMob, float h, bool glow, bool isGhost)
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
                Renderers = rends,
                IsGhost = isGhost
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

        private void ClearAllXRay()
        {
            foreach (var kvp in _originalMaterials) if (kvp.Key != null) kvp.Key.materials = kvp.Value;
            _originalMaterials.Clear();
        }

        private void RestoreOriginal(Renderer[] renderers)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (_originalMaterials.ContainsKey(r))
                {
                    r.materials = _originalMaterials[r];
                    _originalMaterials.Remove(r);
                }
            }
        }

        private bool IsIgnored(string name)
        {
            for (int i = 0; i < _ignoreKeywords.Length; i++)
            {
                if (name.IndexOf(_ignoreKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private List<string> GetActiveKeys(Dictionary<string, bool> dict)
        {
            List<string> list = new List<string>();
            foreach (var kvp in dict) if (kvp.Value) list.Add(kvp.Key);
            return list;
        }

        public void DrawESP()
        {
            if (!ConfigManager.Esp_Enabled) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (var item in _cachedObjects)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(item.Position + Vector3.up * item.Height);
                if (screenPos.z > 0)
                {
                    float dist = Vector3.Distance(cam.transform.position, item.Position);
                    DrawESPLabel(screenPos, item.Label + item.HpText, dist, item.Color, item.IsMob, item.Height, item.Position, cam);
                }
            }
        }

        private void DrawESPLabel(Vector3 screenPos, string text, float dist, Color color, bool isMob, float height, Vector3 worldPos, Camera cam)
        {
            string fullText = $"{text} [{dist:F0}m]";
            GUIContent content = new GUIContent(fullText);
            Vector2 size = _styleLabel.CalcSize(content);

            Vector2 centerBottomPos = new Vector2(screenPos.x, Screen.height - screenPos.y);

            if (ConfigManager.Esp_ShowBoxes)
            {
                // Boxy 2D
                float boxHeight = (1.8f / dist) * Screen.height; // Przybliżona skala
                if (boxHeight < 20) boxHeight = 20;
                float boxWidth = boxHeight * 0.6f;
                Rect boxRect = new Rect(centerBottomPos.x - boxWidth / 2, centerBottomPos.y - boxHeight, boxWidth, boxHeight);
                DrawBoxOutline(boxRect, color, 1f);
            }

            Rect r = new Rect(centerBottomPos.x - size.x / 2, centerBottomPos.y - size.y - (ConfigManager.Esp_ShowBoxes ? 10 : 0), size.x, size.y);
            Rect bgRect = new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4);
            GUI.Box(bgRect, GUIContent.none, _styleBackground);
            _styleLabel.normal.textColor = color;
            GUI.Label(r, fullText, _styleLabel);
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
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.color = color;
            GUI.DrawTexture(new Rect(pointA.x, pointA.y, length, width), _boxTexture);
            GUI.matrix = matrix;
            GUI.color = Color.white;
        }

        public void DrawMenu()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>RESOURCE ESP</b>", GUILayout.Width(150));
            if (GUILayout.Button(ConfigManager.Esp_Enabled ? "ENABLED" : "DISABLED"))
            {
                ConfigManager.Esp_Enabled = !ConfigManager.Esp_Enabled;
                ConfigManager.Save();
            }
            GUILayout.EndHorizontal();

            if (!ConfigManager.Esp_Enabled) return;

            GUILayout.Space(5);
            GUILayout.Label($"{Localization.Get("ESP_DIST")}: {ConfigManager.Esp_Distance:F0}m");
            float newDist = GUILayout.HorizontalSlider(ConfigManager.Esp_Distance, 50f, 500f);
            if (Mathf.Abs(newDist - ConfigManager.Esp_Distance) > 1f) { ConfigManager.Esp_Distance = newDist; ConfigManager.Save(); }

            GUILayout.Space(5);
            ConfigManager.Esp_ShowBoxes = GUILayout.Toggle(ConfigManager.Esp_ShowBoxes, " Show Boxes");

            GUILayout.Space(5);
            DrawCategoryToggle("Mining", ref ConfigManager.Esp_Cat_Mining, _miningToggles);
            DrawCategoryToggle("Gathering", ref ConfigManager.Esp_Cat_Gather, _gatheringToggles);
            DrawCategoryToggle("Lumber", ref ConfigManager.Esp_Cat_Lumber, _lumberToggles);
            DrawCategoryToggle("Godsend", ref ConfigManager.Esp_Cat_Godsend, _godsendToggles);
            DrawCategoryToggle("Dungeons", ref ConfigManager.Esp_Cat_Dungeons, _dungeonsToggles);

            bool others = GUILayout.Toggle(ConfigManager.Esp_Cat_Others, " Show Others");
            if (others != ConfigManager.Esp_Cat_Others) { ConfigManager.Esp_Cat_Others = others; ConfigManager.Save(); }

            GUILayout.Space(5);
            bool showMobs = GUILayout.Toggle(ConfigManager.Esp_ShowMobs, " Show Mobs");
            if (showMobs != ConfigManager.Esp_ShowMobs) { ConfigManager.Esp_ShowMobs = showMobs; ConfigManager.Save(); }

            GUILayout.Space(10);
            if (GUILayout.Button(_showColorMenu ? "Hide Item Lists" : "Edit Item Lists"))
            {
                _showColorMenu = !_showColorMenu;
            }

            if (_showColorMenu)
            {
                // --- FIX: SCROLL VIEW ZAMIAST ROZSZERZANIA OKNA ---
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(300));

                DrawFilterList("Mining Items", _miningToggles);
                DrawFilterList("Gather Items", _gatheringToggles);
                DrawFilterList("Lumber Items", _lumberToggles);
                DrawFilterList("Godsend Items", _godsendToggles);
                DrawFilterList("Dungeon Items", _dungeonsToggles);

                GUILayout.EndScrollView();
                // --------------------------------------------------

                if (GUI.changed) SyncToConfig();
            }
        }

        private void DrawCategoryToggle(string title, ref bool toggle, Dictionary<string, bool> list)
        {
            bool newVal = GUILayout.Toggle(toggle, $" {title} ({list.Count})");
            if (newVal != toggle)
            {
                toggle = newVal;
                ConfigManager.Save();
            }
        }

        private void DrawFilterList(string title, Dictionary<string, bool> list)
        {
            if (list.Count == 0) return;
            GUILayout.Label($"<b>{title}</b>");
            var keys = new List<string>(list.Keys);
            foreach (var key in keys)
            {
                bool val = list[key];
                bool set = GUILayout.Toggle(val, " " + key);
                if (set != val) list[key] = set;
            }
            GUILayout.Space(5);
        }
    }
}