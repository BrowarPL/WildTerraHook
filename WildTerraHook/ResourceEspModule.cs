using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace WildTerraHook
{
    public class ResourceEspModule
    {
        // --- MENU (Domyślnie Wyłączone) ---
        private bool _showResources = false;
        private bool _showMining = false;
        private bool _showGathering = false;
        private bool _showLumber = false;

        private bool _showMobs = false;
        private bool _showBosses = false;
        private bool _showElites = false;
        private bool _showAggressive = false;
        private bool _showRetaliating = false;
        private bool _showPassive = false;

        // Słowniki
        private Dictionary<string, bool> _miningToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _gatheringToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lumberToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _mobSpecificToggles = new Dictionary<string, bool>();

        // Ignorowane (Śmieci, które mogą mieć w nazwie "Iron" itp.)
        private string[] _ignoreKeywords = {
            "Anvil", "Table", "Bench", "Rack", "Stove", "Kiln", "Furnace",
            "Chair", "Bed", "Chest", "Box", "Crate", "Basket", "Fence",
            "Wall", "Floor", "Roof", "Window", "Door", "Gate", "Sign", "Decor"
        };

        // Cache
        private List<CachedObject> _cachedObjects = new List<CachedObject>();
        private float _lastScanTime = 0f;
        private float _scanInterval = 0.1f; // 100ms (płynne odświeżanie)

        // GUI
        private GUIStyle _styleLabel;
        private GUIStyle _styleBackground;
        private Texture2D _bgTexture;

        private struct CachedObject
        {
            public Vector3 Position;
            public string Label;
            public Color Color;
        }

        public ResourceEspModule()
        {
            InitializeLists();
        }

        private void InitializeLists()
        {
            string[] mining = { "Rock", "Copper", "Tin", "Limestone", "Coal", "Sulfur", "Iron", "Marblestone", "Arsenic", "Zuperit", "Mortuus", "Sangit" };
            foreach (var s in mining) _miningToggles[s] = false;

            // Dodano "GreyAmanita" bez spacji do sprawdzania w logice
            string[] gathering = { "Wild root", "Boletus", "Chanterelles", "Morels", "Russalas", "Grey amanita", "Fly agaric", "Sticks pile", "Stone pile", "Wild cereals", "Blueberry", "Nest", "Nettles", "Clay", "Hazel", "Greenary", "Ligonberry", "Beehive", "Swamp thorn", "Mountain sage", "Wolf berries", "Chelidonium", "Sand" };
            foreach (var s in gathering) _gatheringToggles[s] = false;

            string[] lumber = { "Apple tree", "Snag", "Birch", "Grave tree", "Stump", "Pine", "Maple", "Poplar", "Spruce", "Dried tree", "Oak", "Grim tree", "Infected grim tree" };
            foreach (var s in lumber) _lumberToggles[s] = false;

            string[] mobs = { "Fox", "Goat", "Hare", "Deer", "Stag", "Wolf", "Bear", "Boar", "Silver Fox" };
            foreach (var s in mobs) _mobSpecificToggles[s] = false;
        }

        public void Update()
        {
            if (Time.time - _lastScanTime > _scanInterval)
            {
                ScanObjects();
                _lastScanTime = Time.time;
            }
        }

        private void ScanObjects()
        {
            _cachedObjects.Clear();
            if (!_showResources && !_showMobs) return;

            try
            {
                // 1. SUROWCE -> Szukamy global::WTObject (nie GatherItem!)
                if (_showResources)
                {
                    var objects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
                    foreach (var obj in objects)
                    {
                        if (obj == null) continue;
                        string name = obj.name;

                        if (IsIgnored(name)) continue;

                        if (_showMining) CheckAndAdd(name, obj.transform.position, _miningToggles, Color.gray);
                        if (_showGathering) CheckAndAdd(name, obj.transform.position, _gatheringToggles, Color.green);
                        if (_showLumber) CheckAndAdd(name, obj.transform.position, _lumberToggles, new Color(0.6f, 0.3f, 0f));
                    }
                }

                // 2. MOBY -> Szukamy global::WTMob
                if (_showMobs)
                {
                    var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                    foreach (var mob in mobs)
                    {
                        if (mob == null || mob.health <= 0) continue;

                        string name = mob.name;
                        bool isBoss = name.ToLower().Contains("boss") || name.Contains("King") || name.Contains("Queen");
                        bool isElite = name.ToLower().Contains("elite") || name.ToLower().Contains("leader");

                        if (_showBosses && isBoss) { AddCache(mob.transform.position, $"[BOSS] {name}", Color.red); continue; }
                        if (_showElites && isElite) { AddCache(mob.transform.position, $"[ELITE] {name}", new Color(1f, 0.5f, 0f)); continue; }

                        bool matched = false;
                        foreach (var pair in _mobSpecificToggles)
                        {
                            if (pair.Value && ContainsIgnoreCase(name, pair.Key))
                            {
                                Color c = Color.red;
                                if (IsRetaliating(pair.Key))
                                {
                                    if (!_showRetaliating) { matched = true; break; }
                                    c = Color.yellow;
                                }
                                else if (IsPassive(pair.Key))
                                {
                                    if (!_showPassive) { matched = true; break; }
                                    c = Color.cyan;
                                }

                                AddCache(mob.transform.position, name, c);
                                matched = true;
                                break;
                            }
                        }

                        if (!matched && _showAggressive && !isBoss && !isElite)
                        {
                            AddCache(mob.transform.position, name, Color.red);
                        }
                    }
                }
            }
            catch { }
        }

        private bool IsIgnored(string name)
        {
            foreach (var ignore in _ignoreKeywords)
                if (name.IndexOf(ignore, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void CheckAndAdd(string objName, Vector3 pos, Dictionary<string, bool> toggles, Color color)
        {
            foreach (var pair in toggles)
            {
                if (pair.Value)
                {
                    // Szukamy normalnie oraz bez spacji (fix dla Grey Amanita -> GreyAmanita)
                    if (ContainsIgnoreCase(objName, pair.Key) || ContainsIgnoreCase(objName, pair.Key.Replace(" ", "")))
                    {
                        AddCache(pos, pair.Key, color);
                        return;
                    }
                }
            }
        }

        private bool ContainsIgnoreCase(string source, string toCheck)
        {
            return source.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddCache(Vector3 pos, string label, Color col)
        {
            _cachedObjects.Add(new CachedObject { Position = pos, Label = label, Color = col });
        }

        private bool IsRetaliating(string name) { return name == "Fox" || name == "Goat" || name.Contains("Silver Fox") || name == "Boar"; }
        private bool IsPassive(string name) { return name == "Hare" || name == "Deer" || name == "Stag"; }

        // --- RYSOWANIE GUI ---
        private void CreateStyles()
        {
            if (_bgTexture == null)
            {
                _bgTexture = new Texture2D(1, 1);
                _bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.7f)); // Ciemne tło (70% czerni)
                _bgTexture.Apply();
            }
            if (_styleBackground == null)
            {
                _styleBackground = new GUIStyle();
                _styleBackground.normal.background = _bgTexture;
            }
            if (_styleLabel == null)
            {
                _styleLabel = new GUIStyle();
                _styleLabel.normal.textColor = Color.white;
                _styleLabel.alignment = TextAnchor.MiddleCenter;
                _styleLabel.fontSize = 11;
                _styleLabel.fontStyle = FontStyle.Bold;
            }
        }

        private Vector2 _scrollPos;
        public void DrawMenu()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(400));

            _showResources = GUILayout.Toggle(_showResources, "<b>RESOURCES</b>");
            if (_showResources)
            {
                GUILayout.BeginHorizontal(); GUILayout.Space(15); GUILayout.BeginVertical();
                if (_showMining = GUILayout.Toggle(_showMining, "Mining")) DrawDictionary(_miningToggles);
                if (_showGathering = GUILayout.Toggle(_showGathering, "Gathering")) DrawDictionary(_gatheringToggles);
                if (_showLumber = GUILayout.Toggle(_showLumber, "Lumberjacking")) DrawDictionary(_lumberToggles);
                GUILayout.EndVertical(); GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            _showMobs = GUILayout.Toggle(_showMobs, "<b>MOBS</b>");
            if (_showMobs)
            {
                GUILayout.BeginHorizontal(); GUILayout.Space(15); GUILayout.BeginVertical();
                _showBosses = GUILayout.Toggle(_showBosses, "Bosses");
                _showElites = GUILayout.Toggle(_showElites, "Elites");
                _showAggressive = GUILayout.Toggle(_showAggressive, "Aggressive (Rest)");

                _showRetaliating = GUILayout.Toggle(_showRetaliating, "Retaliating");
                if (_showRetaliating) DrawSpecificMobToggles(new string[] { "Fox", "Silver Fox", "Goat", "Boar" });

                _showPassive = GUILayout.Toggle(_showPassive, "Non-Aggressive");
                if (_showPassive) DrawSpecificMobToggles(new string[] { "Hare", "Deer", "Stag" });
                GUILayout.EndVertical(); GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawDictionary(Dictionary<string, bool> dict)
        {
            GUILayout.BeginHorizontal(); GUILayout.Space(10); GUILayout.BeginVertical();
            var keys = new List<string>(dict.Keys);
            foreach (var key in keys) dict[key] = GUILayout.Toggle(dict[key], key);
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
        }

        private void DrawSpecificMobToggles(string[] filter)
        {
            GUILayout.BeginHorizontal(); GUILayout.Space(10); GUILayout.BeginVertical();
            foreach (var key in filter)
            {
                if (_mobSpecificToggles.ContainsKey(key))
                    _mobSpecificToggles[key] = GUILayout.Toggle(_mobSpecificToggles[key], key);
            }
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
        }

        public void DrawESP()
        {
            CreateStyles();
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (var obj in _cachedObjects)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(obj.Position);
                if (screenPos.z > 0)
                {
                    screenPos.y = Screen.height - screenPos.y;
                    float dist = Vector3.Distance(cam.transform.position, obj.Position);

                    if (dist < 150)
                    {
                        float w = 120; float h = 20;
                        Rect r = new Rect(screenPos.x - w / 2, screenPos.y - h / 2, w, h);
                        GUI.Box(r, GUIContent.none, _styleBackground);
                        _styleLabel.normal.textColor = obj.Color;
                        GUI.Label(r, $"{obj.Label} [{dist:F0}m]", _styleLabel);
                    }
                }
            }
        }
    }
}