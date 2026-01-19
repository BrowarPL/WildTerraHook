using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WildTerraHook
{
    public class ResourceEspModule
    {
        // --- MENU (Domyślnie Wyłączone) ---
        private bool _showResources = false;
        private bool _showMining = false;
        private bool _showGathering = false;
        private bool _showLumber = false;
        private bool _showGodsend = false; // Nowa kategoria: Skrzynki
        private bool _showOthers = false;  // Śmietniczek

        private bool _showMobs = false;
        private bool _showAggressive = false;
        private bool _showRetaliating = false;
        private bool _showPassive = false;

        // Słowniki surowców
        private Dictionary<string, bool> _miningToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _gatheringToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lumberToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _godsendToggles = new Dictionary<string, bool>();

        // Zbiór znanych nazw (żeby nie dublować w Others)
        private HashSet<string> _knownResources = new HashSet<string>();

        // --- LISTY MOBÓW ---
        private List<string> _passiveNames = new List<string>() {
            "Hare", "Deer", "Stag", "Cow", "Chicken", "Sheep", "Pig",
            "Crow", "Seagull"
        };

        private List<string> _retaliatingNames = new List<string>() {
            "Fox", "Goat", "Silver Fox", "Boar", "Moose", "Horse",
            "Ancient ent", "Ancient Ent", "Ent"
        };

        // Ignorowane obiekty ("Śmieci")
        private string[] _ignoreKeywords = {
            "Anvil", "Table", "Bench", "Rack", "Stove", "Kiln", "Furnace",
            "Chair", "Bed", "Chest", "Box", "Crate", "Basket", "Fence",
            "Wall", "Floor", "Roof", "Window", "Door", "Gate", "Sign", "Decor",
            "Torch", "Lamp", "Rug", "Carpet", "Pillar", "Beam", "Stairs", "Foundation",
            "Road", "Path", "Walkway"
        };

        // Cache
        private List<CachedObject> _cachedObjects = new List<CachedObject>();
        private float _lastScanTime = 0f;
        private float _scanInterval = 0.1f; // 100ms

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
            // Mining
            string[] mining = { "Rock", "Copper", "Tin", "Limestone", "Coal", "Sulfur", "Iron", "Marblestone", "Arsenic", "Zuperit", "Mortuus", "Sangit" };
            foreach (var s in mining) { _miningToggles[s] = false; AddToKnown(s); }

            // Gathering (Zaktualizowane nazwy wewnętrzne)
            string[] gathering = {
                "Wild root", "Boletus", "Chanterelles", "Morels", "MushroomRussulas",
                "MushroomAmanitaGrey", "MushroomAmanitaRed", "WoodPile", "Stone pile",
                "Wild cereals", "Blueberry", "Nest", "NettlePlant", "Clay", "Hazel",
                "Greenary", "Lingonberry", "Beehive", "Swamp thorn", "Mountain sage",
                "Wolf berries", "Chelidonium", "Sand", "Strawberry"
            };
            foreach (var s in gathering) { _gatheringToggles[s] = false; AddToKnown(s); }

            // Lumber
            string[] lumber = { "Apple tree", "Snag", "Birch", "Grave tree", "Stump", "Pine", "Maple", "Poplar", "Spruce", "Dried tree", "Oak", "Grim tree", "Infected grim tree" };
            foreach (var s in lumber) { _lumberToggles[s] = false; AddToKnown(s); }

            // Godsend (Skrzynki)
            string[] godsend = { "Godsend" }; // Szuka wszystkiego co ma "Godsend" w nazwie
            foreach (var s in godsend) { _godsendToggles[s] = false; AddToKnown(s); }
        }

        private void AddToKnown(string s)
        {
            _knownResources.Add(s);
            _knownResources.Add(s.Replace(" ", "")); // Wersja bez spacji
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
                // 1. SUROWCE + OTHERS
                if (_showResources)
                {
                    var objects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
                    foreach (var obj in objects)
                    {
                        if (obj == null) continue;
                        string name = obj.name;

                        // Pomijamy Moby i Graczy
                        if (obj is global::WTMob) continue;
                        if (name.Contains("Player") || name.Contains("Character")) continue;

                        // Filtr śmieci
                        if (IsIgnored(name)) continue;

                        bool matched = false;

                        // Standardowe kategorie
                        if (_showMining && CheckAndAddResource(name, obj.transform.position, _miningToggles, Color.gray)) matched = true;
                        else if (_showGathering && CheckAndAddResource(name, obj.transform.position, _gatheringToggles, Color.green)) matched = true;
                        else if (_showLumber && CheckAndAddResource(name, obj.transform.position, _lumberToggles, new Color(0.6f, 0.3f, 0f))) matched = true;

                        // Godsend (Fioletowy)
                        else if (_showGodsend && CheckAndAddResource(name, obj.transform.position, _godsendToggles, new Color(0.8f, 0f, 1f))) matched = true;

                        // Kategoria OTHERS (Tylko jeśli nie znaleziono wyżej i włączona)
                        if (!matched && _showOthers)
                        {
                            if (!IsKnownResource(name))
                            {
                                AddCache(obj.transform.position, name, Color.white);
                            }
                        }
                    }
                }

                // 2. MOBY
                if (_showMobs)
                {
                    var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                    foreach (var mob in mobs)
                    {
                        if (mob == null || mob.health <= 0) continue;
                        ProcessMob(mob);
                    }
                }
            }
            catch { }
        }

        private void ProcessMob(global::WTMob mob)
        {
            string name = mob.name;
            Vector3 pos = mob.transform.position;

            if (MatchesList(name, _passiveNames))
            {
                if (_showPassive) AddCache(pos, name, Color.cyan);
                return;
            }

            if (MatchesList(name, _retaliatingNames))
            {
                if (_showRetaliating) AddCache(pos, name, Color.yellow);
                return;
            }

            // Wszystko inne -> Aggressive
            if (_showAggressive)
            {
                string prefix = "";
                if (name.Contains("Boss") || name.Contains("King")) prefix = "[BOSS] ";
                else if (name.Contains("Elite") || name.Contains("Leader")) prefix = "[ELITE] ";
                AddCache(pos, prefix + name, Color.red);
            }
        }

        private bool MatchesList(string name, List<string> list)
        {
            foreach (var entry in list)
                if (name.IndexOf(entry, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private bool IsIgnored(string name)
        {
            foreach (var ignore in _ignoreKeywords)
                if (name.IndexOf(ignore, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private bool IsKnownResource(string name)
        {
            foreach (var known in _knownResources)
            {
                if (name.IndexOf(known, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private bool CheckAndAddResource(string objName, Vector3 pos, Dictionary<string, bool> toggles, Color color)
        {
            foreach (var pair in toggles)
            {
                if (pair.Value)
                {
                    if (ContainsIgnoreCase(objName, pair.Key))
                    {
                        AddCache(pos, pair.Key, color);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool ContainsIgnoreCase(string source, string toCheck)
        {
            return source.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddCache(Vector3 pos, string label, Color col)
        {
            _cachedObjects.Add(new CachedObject { Position = pos, Label = label, Color = col });
        }

        // --- GUI ---
        private void CreateStyles()
        {
            if (_bgTexture == null)
            {
                _bgTexture = new Texture2D(1, 1);
                _bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
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
                _styleLabel.normal.background = _bgTexture;
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

                GUILayout.Space(5);
                // Godsend przed Others
                if (_showGodsend = GUILayout.Toggle(_showGodsend, "Godsend (Chests)")) DrawDictionary(_godsendToggles);

                GUILayout.Space(5);
                _showOthers = GUILayout.Toggle(_showOthers, "Others (Uncategorized)");

                GUILayout.EndVertical(); GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            _showMobs = GUILayout.Toggle(_showMobs, "<b>MOBS</b>");
            if (_showMobs)
            {
                GUILayout.BeginHorizontal(); GUILayout.Space(15); GUILayout.BeginVertical();
                _showAggressive = GUILayout.Toggle(_showAggressive, "Aggressive (Bosses/Elites/Others)");
                _showRetaliating = GUILayout.Toggle(_showRetaliating, "Retaliating (Horse/Ent/Goat/Fox)");
                _showPassive = GUILayout.Toggle(_showPassive, "Non-Aggressive (Deer/Hare/Crow)");
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

        public void DrawESP()
        {
            CreateStyles();
            Camera cam = Camera.main;
            if (cam == null) return;

            float screenW = Screen.width;
            float screenH = Screen.height;
            Vector3 camPos = cam.transform.position;

            foreach (var obj in _cachedObjects)
            {
                float dist = Vector3.Distance(camPos, obj.Position);
                if (dist > 250) continue;

                Vector3 screenPos = cam.WorldToScreenPoint(obj.Position);
                bool isBehind = screenPos.z < 0;

                bool isOffScreen = isBehind ||
                                   screenPos.x < 0 || screenPos.x > screenW ||
                                   screenPos.y < 0 || screenPos.y > screenH;

                if (isOffScreen)
                {
                    if (isBehind) { screenPos.x *= -1; screenPos.y *= -1; }

                    Vector3 screenCenter = new Vector3(screenW / 2, screenH / 2, 0);
                    screenPos -= screenCenter;

                    float angle = Mathf.Atan2(screenPos.y, screenPos.x);
                    angle -= 90 * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = -Mathf.Sin(angle);

                    float m = cos / sin;
                    Vector3 screenBounds = screenCenter;
                    screenBounds.x -= 20; screenBounds.y -= 20;

                    if (cos > 0) screenPos = new Vector3(screenBounds.y / m, screenBounds.y, 0);
                    else screenPos = new Vector3(-screenBounds.y / m, -screenBounds.y, 0);

                    if (screenPos.x > screenBounds.x) screenPos = new Vector3(screenBounds.x, screenBounds.x * m, 0);
                    else if (screenPos.x < -screenBounds.x) screenPos = new Vector3(-screenBounds.x, -screenBounds.x * m, 0);

                    screenPos += screenCenter;
                }

                screenPos.y = screenH - screenPos.y;

                float w = 160; float h = 22;
                Rect r = new Rect(screenPos.x - w / 2, screenPos.y - h / 2, w, h);

                GUI.Label(r, $"{obj.Label} [{dist:F0}m]", _styleLabel);
            }
        }
    }
}