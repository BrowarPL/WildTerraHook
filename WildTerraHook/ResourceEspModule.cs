using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WildTerraHook
{
    public class ResourceEspModule
    {
        // --- GŁÓWNE PRZEŁĄCZNIKI ---
        public bool EspEnabled = false;

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
        private float _maxDistance = 150f; // Suwak dystansu rysowania

        // --- LISTY SZCZEGÓŁOWE ---
        private Dictionary<string, bool> _miningToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _gatheringToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lumberToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _godsendToggles = new Dictionary<string, bool>();

        // Cache nazw do ignorowania (dla optymalizacji)
        private string[] _ignoreKeywords = { "Anvil", "Table", "Bench", "Rack", "Stove", "Kiln", "Furnace", "Chair", "Bed", "Chest", "Box", "Crate", "Basket", "Fence", "Wall", "Floor", "Roof", "Window", "Door", "Gate", "Sign", "Decor", "Torch", "Lamp", "Rug", "Carpet", "Pillar", "Beam", "Stairs", "Foundation", "Road", "Path", "Walkway" };

        // --- DANE CACHE ---
        private List<CachedObject> _cachedObjects = new List<CachedObject>();
        private float _lastScanTime = 0f;
        private float _scanInterval = 1.5f; // Skanuj rzadziej żeby nie lagowało (1.5s)

        // Style
        private GUIStyle _styleLabel;
        private GUIStyle _styleBackground;
        private Texture2D _bgTexture;
        private Vector2 _scrollPos;

        private struct CachedObject
        {
            public Vector3 Position;
            public string Label;
            public Color Color;
            public string HpText; // Dodano dla HP
        }

        public ResourceEspModule()
        {
            InitializeLists();
        }

        private void InitializeLists()
        {
            // Górnictwo
            string[] mining = { "Rock", "Copper", "Tin", "Limestone", "Coal", "Sulfur", "Iron", "Marblestone", "Arsenic", "Zuperit", "Mortuus", "Sangit" };
            foreach (var s in mining) _miningToggles[s] = false;

            // Zbieractwo
            string[] gathering = { "Wild root", "Boletus", "Chanterelles", "Morels", "MushroomRussulas", "MushroomAmanitaGrey", "MushroomAmanitaRed", "WoodPile", "Stone pile", "Wild cereals", "Blueberry", "Nest", "NettlePlant", "Clay", "Hazel", "Greenary", "Lingonberry", "Beehive", "Swamp thorn", "Mountain sage", "Wolf berries", "Chelidonium", "Sand", "Strawberry" };
            foreach (var s in gathering) _gatheringToggles[s] = false;

            // Drwalnictwo
            string[] lumber = { "Apple tree", "Snag", "Birch", "Grave tree", "Stump", "Pine", "Maple", "Poplar", "Spruce", "Dried tree", "Oak", "Grim tree", "Infected grim tree" };
            foreach (var s in lumber) _lumberToggles[s] = false;

            // Skarby
            string[] godsend = { "Godsend" };
            foreach (var s in godsend) _godsendToggles[s] = false;
        }

        public void Update()
        {
            if (EspEnabled && Time.time - _lastScanTime > _scanInterval)
            {
                ScanObjects();
                _lastScanTime = Time.time;
            }
        }

        private void ScanObjects()
        {
            if (!EspEnabled) { _cachedObjects.Clear(); return; }
            if (!_showResources && !_showMobs) { _cachedObjects.Clear(); return; }

            // Pobierz pozycję gracza raz, dla optymalizacji
            Vector3 playerPos = Vector3.zero;
            if (global::Player.localPlayer != null) playerPos = global::Player.localPlayer.transform.position;
            else if (Camera.main != null) playerPos = Camera.main.transform.position;

            List<CachedObject> newCache = new List<CachedObject>();

            try
            {
                // --- SUROWCE (WTObject) ---
                if (_showResources)
                {
                    // OPTYMALIZACJA: Zbuduj listę aktywnych słów kluczowych PRZED pętlą
                    // Dzięki temu nie iterujemy po słowniku dla każdego z 1000 obiektów
                    List<string> activeMining = GetActiveKeys(_miningToggles);
                    List<string> activeGather = GetActiveKeys(_gatheringToggles);
                    List<string> activeLumber = GetActiveKeys(_lumberToggles);
                    List<string> activeGodsend = GetActiveKeys(_godsendToggles);

                    // Znajdź wszystkie obiekty (to jest kosztowne, dlatego robimy rzadko)
                    var objects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();

                    foreach (var obj in objects)
                    {
                        if (obj == null) continue;

                        // Szybki test dystansu (kwadratowy jest szybszy niż Distance)
                        float distSqr = (obj.transform.position - playerPos).sqrMagnitude;
                        if (distSqr > (_maxDistance * _maxDistance)) continue; // Ignoruj dalekie

                        string name = obj.name;

                        // Szybkie odrzucanie śmieci
                        if (IsIgnored(name)) continue;

                        bool matched = false;

                        // Sprawdzamy tylko aktywne kategorie
                        if (_showMining && CheckList(name, activeMining, obj.transform.position, ConfigManager.Colors.ResMining, newCache)) matched = true;
                        else if (_showGathering && CheckList(name, activeGather, obj.transform.position, ConfigManager.Colors.ResGather, newCache)) matched = true;
                        else if (_showLumber && CheckList(name, activeLumber, obj.transform.position, ConfigManager.Colors.ResLumber, newCache)) matched = true;
                        else if (_showGodsend && CheckList(name, activeGodsend, obj.transform.position, new Color(0.8f, 0f, 1f), newCache)) matched = true;

                        // Jeśli włączone "Inne" i nie znaleziono w listach
                        if (!matched && _showOthers && !name.Contains("Player"))
                        {
                            // Tutaj ostrożnie, żeby nie zalać ekranu
                            // newCache.Add(new CachedObject { Position = obj.transform.position, Label = name, Color = Color.white, HpText = "" });
                        }
                    }
                }

                // --- MOBY (WTMob) ---
                if (_showMobs)
                {
                    var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                    foreach (var mob in mobs)
                    {
                        if (mob != null && mob.health > 0)
                        {
                            // Szybki test dystansu
                            if ((mob.transform.position - playerPos).sqrMagnitude > (_maxDistance * _maxDistance)) continue;

                            ProcessMob(mob, newCache);
                        }
                    }
                }
            }
            catch { }

            _cachedObjects = newCache;
        }

        private List<string> GetActiveKeys(Dictionary<string, bool> dict)
        {
            List<string> active = new List<string>();
            foreach (var kvp in dict) if (kvp.Value) active.Add(kvp.Key);
            return active;
        }

        private bool CheckList(string objName, List<string> activeKeys, Vector3 pos, Color color, List<CachedObject> cache)
        {
            if (activeKeys.Count == 0) return false;
            foreach (var key in activeKeys)
            {
                if (objName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cache.Add(new CachedObject { Position = pos, Label = key, Color = color, HpText = "" });
                    return true;
                }
            }
            return false;
        }

        private void ProcessMob(global::WTMob mob, List<CachedObject> cache)
        {
            string name = mob.name;
            Vector3 pos = mob.transform.position;

            // Pobieranie HP
            int hp = mob.health;
            int maxHp = mob.healthMax; // W niektórych wersjach może być maxHealth
            string hpStr = $" [HP: {hp}/{maxHp}]";

            bool isAggro = name.Contains("LargeFox") || name.Contains("Boss") || name.Contains("King") || name.Contains("Elite") || name.Contains("Bear") || name.Contains("Wolf");
            bool isPassive = name.Contains("Hare") || name.Contains("Deer") || name.Contains("Stag") || name.Contains("Cow") || name.Contains("Sheep");

            if (isAggro)
            {
                if (_showAggressive) cache.Add(new CachedObject { Position = pos, Label = "[!] " + name, Color = ConfigManager.Colors.MobAggressive, HpText = hpStr });
            }
            else if (isPassive)
            {
                if (_showPassive) cache.Add(new CachedObject { Position = pos, Label = name, Color = ConfigManager.Colors.MobPassive, HpText = hpStr });
            }
            else // Retaliating / Other
            {
                if (_showRetaliating) cache.Add(new CachedObject { Position = pos, Label = name, Color = ConfigManager.Colors.MobFleeing, HpText = hpStr });
            }
        }

        private bool IsIgnored(string name)
        {
            // Szybki check
            if (name.Length < 3) return true;
            foreach (var ignore in _ignoreKeywords)
                if (name.IndexOf(ignore, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void CreateStyles()
        {
            if (_bgTexture == null) { _bgTexture = new Texture2D(1, 1); _bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.7f)); _bgTexture.Apply(); }
            if (_styleBackground == null) { _styleBackground = new GUIStyle(); _styleBackground.normal.background = _bgTexture; }
            if (_styleLabel == null)
            {
                _styleLabel = new GUIStyle();
                _styleLabel.normal.textColor = Color.white;
                _styleLabel.alignment = TextAnchor.MiddleCenter;
                _styleLabel.fontSize = 11;
                _styleLabel.fontStyle = FontStyle.Bold;
                // Usunąłem tło z labela żeby było czytelniej przy wielu obiektach, dodamy cień
            }
        }

        public void DrawMenu()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(450));

            EspEnabled = GUILayout.Toggle(EspEnabled, $"<b>{Localization.Get("ESP_MAIN_BTN")}</b>");
            GUILayout.Space(5);

            if (EspEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.Get("ESP_DIST")}: {_maxDistance:F0}m", GUILayout.Width(100));
                _maxDistance = GUILayout.HorizontalSlider(_maxDistance, 20f, 300f);
                GUILayout.EndHorizontal();
                GUILayout.Space(5);

                _showResources = GUILayout.Toggle(_showResources, $"<b>{Localization.Get("ESP_RES_TITLE")}</b>");
                if (_showResources)
                {
                    GUILayout.BeginHorizontal(); GUILayout.Space(10); GUILayout.BeginVertical();

                    if (_showMining = GUILayout.Toggle(_showMining, Localization.Get("ESP_CAT_MINING"))) DrawDictionary(_miningToggles);
                    if (_showGathering = GUILayout.Toggle(_showGathering, Localization.Get("ESP_CAT_GATHER"))) DrawDictionary(_gatheringToggles);
                    if (_showLumber = GUILayout.Toggle(_showLumber, Localization.Get("ESP_CAT_LUMBER"))) DrawDictionary(_lumberToggles);

                    GUILayout.Space(5);
                    if (_showGodsend = GUILayout.Toggle(_showGodsend, Localization.Get("ESP_CAT_GODSEND"))) DrawDictionary(_godsendToggles);

                    GUILayout.EndVertical(); GUILayout.EndHorizontal();
                }

                GUILayout.Space(10);

                _showMobs = GUILayout.Toggle(_showMobs, $"<b>{Localization.Get("ESP_MOB_TITLE")}</b>");
                if (_showMobs)
                {
                    GUILayout.BeginHorizontal(); GUILayout.Space(10); GUILayout.BeginVertical();
                    _showAggressive = GUILayout.Toggle(_showAggressive, Localization.Get("ESP_MOB_AGGRO"));
                    _showRetaliating = GUILayout.Toggle(_showRetaliating, Localization.Get("ESP_MOB_RETAL"));
                    _showPassive = GUILayout.Toggle(_showPassive, Localization.Get("ESP_MOB_PASSIVE"));
                    GUILayout.EndVertical(); GUILayout.EndHorizontal();
                }

                GUILayout.Space(15);

                if (GUILayout.Button(_showColorMenu ? Localization.Get("ESP_HIDE_COLORS") : Localization.Get("ESP_EDIT_COLORS")))
                {
                    _showColorMenu = !_showColorMenu;
                    if (!_showColorMenu) ConfigManager.Save();
                }

                if (_showColorMenu) DrawColorSettings();
            }

            GUILayout.EndScrollView();
        }

        private void DrawColorSettings()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("ESP_EDIT_COLORS")}</b>");

            GUILayout.Label("-- Moby --");
            DrawColorPicker(Localization.Get("COLOR_MOB_AGGRO"), ref ConfigManager.Colors.MobAggressive);
            DrawColorPicker(Localization.Get("COLOR_MOB_PASSIVE"), ref ConfigManager.Colors.MobPassive);
            DrawColorPicker(Localization.Get("COLOR_MOB_FLEE"), ref ConfigManager.Colors.MobFleeing);

            GUILayout.Space(5);
            GUILayout.Label("-- Surowce --");
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
            foreach (var key in keys) dict[key] = GUILayout.Toggle(dict[key], key);
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
        }

        public void DrawESP()
        {
            if (!EspEnabled) return;
            CreateStyles();
            Camera cam = Camera.main;
            if (cam == null) return;

            // Punkt odniesienia: GRACZ (jeśli istnieje), w p.p. Kamera
            Vector3 originPos = cam.transform.position;
            if (global::Player.localPlayer != null) originPos = global::Player.localPlayer.transform.position;

            float screenW = Screen.width; float screenH = Screen.height;

            foreach (var obj in _cachedObjects)
            {
                // Oblicz dystans od GRACZA
                float dist = Vector3.Distance(originPos, obj.Position);

                // Sprawdź suwak
                if (dist > _maxDistance) continue;

                Vector3 screenPos = cam.WorldToScreenPoint(obj.Position + Vector3.up * 1.5f);
                bool isBehind = screenPos.z < 0;

                // Proste odrzucanie obiektów poza ekranem (dla wydajności GUI)
                if (isBehind) continue; // Nie rysujemy strzałek, bo to robi bałagan w izometrii

                // Rysuj tylko jeśli na ekranie
                if (screenPos.x > 0 && screenPos.x < screenW && screenPos.y > 0 && screenPos.y < screenH)
                {
                    screenPos.y = screenH - screenPos.y;

                    string text = $"{obj.Label} [{dist:F0}m]{obj.HpText}";

                    // Rysowanie z cieniem (Outline)
                    GUIContent content = new GUIContent(text);
                    Vector2 size = _styleLabel.CalcSize(content);
                    Rect r = new Rect(screenPos.x - size.x / 2, screenPos.y - size.y / 2, size.x, size.y);

                    // Cień
                    GUIStyle shadowStyle = new GUIStyle(_styleLabel);
                    shadowStyle.normal.textColor = Color.black;
                    GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, shadowStyle);

                    // Właściwy tekst
                    _styleLabel.normal.textColor = obj.Color;
                    GUI.Label(r, text, _styleLabel);
                }
            }
        }
    }
}