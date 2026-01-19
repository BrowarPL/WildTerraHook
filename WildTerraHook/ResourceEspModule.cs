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

        // Słowniki surowców
        private Dictionary<string, bool> _miningToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _gatheringToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lumberToggles = new Dictionary<string, bool>();

        // Listy definicji Mobów (Nazwy do dopasowania)
        // Priorytet: Boss > Elite > Passive > Retaliating > Aggressive

        private List<string> _bossNames = new List<string>() {
            "Giant crab", "Angry black bear", "Ancient ent", "Swamp serpent",
            "Barghest", "Prospector overseer", "Crocodile", "Captive master",
            "Black giant crab", "Giant scorpion", "Entity of Cthulhu", "King", "Queen", "Boss"
        };

        private List<string> _eliteNames = new List<string>() {
            "Zombie tusker", "Large furious reaper", "Huge boar", "Wolf leader",
            "Bear", "Large Dark wolf", "Black scorpion", "Elite", "Leader"
        };

        private List<string> _passiveNames = new List<string>() {
            "Hare", "Deer", "Stag", "Cow", "Chicken", "Sheep", "Pig"
        };

        private List<string> _retaliatingNames = new List<string>() {
            "Fox", "Goat", "Silver Fox", "Boar", "Moose"
        };

        // Ignorowane obiekty ("Śmieci")
        private string[] _ignoreKeywords = {
            "Anvil", "Table", "Bench", "Rack", "Stove", "Kiln", "Furnace",
            "Chair", "Bed", "Chest", "Box", "Crate", "Basket", "Fence",
            "Wall", "Floor", "Roof", "Window", "Door", "Gate", "Sign", "Decor"
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
            public bool IsImportant; // Bossy i Elity będą większe
        }

        public ResourceEspModule()
        {
            InitializeLists();
        }

        private void InitializeLists()
        {
            string[] mining = { "Rock", "Copper", "Tin", "Limestone", "Coal", "Sulfur", "Iron", "Marblestone", "Arsenic", "Zuperit", "Mortuus", "Sangit" };
            foreach (var s in mining) _miningToggles[s] = false;

            string[] gathering = { "Wild root", "Boletus", "Chanterelles", "Morels", "Russalas", "Grey amanita", "Fly agaric", "Sticks pile", "Stone pile", "Wild cereals", "Blueberry", "Nest", "Nettles", "Clay", "Hazel", "Greenary", "Ligonberry", "Beehive", "Swamp thorn", "Mountain sage", "Wolf berries", "Chelidonium", "Sand" };
            foreach (var s in gathering) _gatheringToggles[s] = false;

            string[] lumber = { "Apple tree", "Snag", "Birch", "Grave tree", "Stump", "Pine", "Maple", "Poplar", "Spruce", "Dried tree", "Oak", "Grim tree", "Infected grim tree" };
            foreach (var s in lumber) _lumberToggles[s] = false;
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
                // 1. SUROWCE
                if (_showResources)
                {
                    var objects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
                    foreach (var obj in objects)
                    {
                        if (obj == null) continue;
                        string name = obj.name;

                        if (IsIgnored(name)) continue;

                        if (_showMining) CheckAndAddResource(name, obj.transform.position, _miningToggles, Color.gray);
                        if (_showGathering) CheckAndAddResource(name, obj.transform.position, _gatheringToggles, Color.green);
                        if (_showLumber) CheckAndAddResource(name, obj.transform.position, _lumberToggles, new Color(0.6f, 0.3f, 0f));
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

            // --- KATEGORYZACJA (Priorytety) ---

            // 1. BOSS
            if (MatchesList(name, _bossNames))
            {
                if (_showBosses) AddCache(pos, $"[BOSS] {name}", Color.red, true);
                return; // Znaleziono, koniec
            }

            // 2. ELITE
            if (MatchesList(name, _eliteNames))
            {
                if (_showElites) AddCache(pos, $"[ELITE] {name}", new Color(1f, 0.5f, 0f), true); // Orange
                return;
            }

            // 3. PASSIVE
            if (MatchesList(name, _passiveNames))
            {
                if (_showPassive) AddCache(pos, name, Color.cyan, false);
                return;
            }

            // 4. RETALIATING
            if (MatchesList(name, _retaliatingNames))
            {
                if (_showRetaliating) AddCache(pos, name, Color.yellow, false);
                return;
            }

            // 5. AGGRESSIVE (Wszystko inne)
            if (_showAggressive)
            {
                AddCache(pos, name, Color.red, false);
            }
        }

        private bool MatchesList(string name, List<string> list)
        {
            foreach (var entry in list)
            {
                if (name.IndexOf(entry, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private bool IsIgnored(string name)
        {
            foreach (var ignore in _ignoreKeywords)
                if (name.IndexOf(ignore, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void CheckAndAddResource(string objName, Vector3 pos, Dictionary<string, bool> toggles, Color color)
        {
            foreach (var pair in toggles)
            {
                if (pair.Value)
                {
                    // Szukamy nazwy normalnie i bez spacji (fix dla Grey Amanita)
                    if (ContainsIgnoreCase(objName, pair.Key) || ContainsIgnoreCase(objName, pair.Key.Replace(" ", "")))
                    {
                        AddCache(pos, pair.Key, color, false);
                        return;
                    }
                }
            }
        }

        private bool ContainsIgnoreCase(string source, string toCheck)
        {
            return source.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddCache(Vector3 pos, string label, Color col, bool important)
        {
            _cachedObjects.Add(new CachedObject { Position = pos, Label = label, Color = col, IsImportant = important });
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
                _showAggressive = GUILayout.Toggle(_showAggressive, "Aggressive (Others)");
                _showRetaliating = GUILayout.Toggle(_showRetaliating, "Retaliating");
                _showPassive = GUILayout.Toggle(_showPassive, "Non-Aggressive");
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
            Vector3 camFwd = cam.transform.forward;

            foreach (var obj in _cachedObjects)
            {
                // Oblicz dystans
                float dist = Vector3.Distance(camPos, obj.Position);
                if (dist > 250) continue; // Limit rysowania

                // Zamiana na pozycję ekranową
                Vector3 screenPos = cam.WorldToScreenPoint(obj.Position);
                bool isBehind = screenPos.z < 0;

                // --- OFF-SCREEN LOGIC ---
                // Jeśli obiekt jest za kamerą LUB poza widokiem ekranu
                bool isOffScreen = isBehind ||
                                   screenPos.x < 0 || screenPos.x > screenW ||
                                   screenPos.y < 0 || screenPos.y > screenH;

                if (isOffScreen)
                {
                    // Jeśli jest za nami, odwracamy współrzędne
                    if (isBehind)
                    {
                        screenPos.x *= -1;
                        screenPos.y *= -1;
                    }

                    // Przesuwamy środek układu współrzędnych na środek ekranu do obliczeń
                    Vector3 screenCenter = new Vector3(screenW / 2, screenH / 2, 0);
                    screenPos -= screenCenter;

                    // Znajdujemy kąt
                    float angle = Mathf.Atan2(screenPos.y, screenPos.x);
                    angle -= 90 * Mathf.Deg2Rad;

                    float cos = Mathf.Cos(angle);
                    float sin = -Mathf.Sin(angle);

                    // Pozycja na krawędzi (m = y/x)
                    screenPos = screenCenter + new Vector3(sin * 150, cos * 150); // Wstępny wektor kierunkowy

                    // Dokładne przyklejenie do krawędzi (Clamp)
                    // y = mx + b -> mapowanie wektora na ramkę ekranu
                    float m = cos / sin;

                    Vector3 screenBounds = screenCenter * 0.9f; // Margines 10%

                    // Obliczamy punkt przecięcia z ramką
                    if (cos > 0) screenPos = new Vector3(screenBounds.y / m, screenBounds.y, 0);
                    else screenPos = new Vector3(-screenBounds.y / m, -screenBounds.y, 0);

                    // Jeśli wyszło poza boki X, korygujemy
                    if (screenPos.x > screenBounds.x) screenPos = new Vector3(screenBounds.x, screenBounds.x * m, 0);
                    else if (screenPos.x < -screenBounds.x) screenPos = new Vector3(-screenBounds.x, -screenBounds.x * m, 0);

                    screenPos += screenCenter; // Wracamy do układu ekranu
                }

                // Odwracamy Y dla GUI (Unity Legacy GUI ma 0 na górze)
                screenPos.y = screenH - screenPos.y;

                // Rysowanie
                float w = 140;
                float h = 22;
                if (obj.IsImportant) { w = 160; h = 26; _styleLabel.fontSize = 13; } // Bossy większe
                else { _styleLabel.fontSize = 11; }

                Rect r = new Rect(screenPos.x - w / 2, screenPos.y - h / 2, w, h);

                GUI.Box(r, GUIContent.none, _styleBackground);
                _styleLabel.normal.textColor = obj.Color;
                GUI.Label(r, $"{obj.Label} [{dist:F0}m]", _styleLabel);
            }
        }
    }
}