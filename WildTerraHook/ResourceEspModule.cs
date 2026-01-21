using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

namespace WildTerraHook
{
    public class ResourceEspModule
    {
        // --- GŁÓWNE PRZEŁĄCZNIKI ---
        public bool EspEnabled = false;
        public bool ShowBoxes = true;
        public bool ShowOutlines = false; // "Glow"

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

        // --- LISTY SZCZEGÓŁOWE ---
        private Dictionary<string, bool> _miningToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _gatheringToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lumberToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _godsendToggles = new Dictionary<string, bool>();

        private string[] _ignoreKeywords = { "Anvil", "Table", "Bench", "Rack", "Stove", "Kiln", "Furnace", "Chair", "Bed", "Chest", "Box", "Crate", "Basket", "Fence", "Wall", "Floor", "Roof", "Window", "Door", "Gate", "Sign", "Decor", "Torch", "Lamp", "Rug", "Carpet", "Pillar", "Beam", "Stairs", "Foundation", "Road", "Path", "Walkway" };

        // --- DANE CACHE ---
        private List<CachedObject> _cachedObjects = new List<CachedObject>();
        private float _lastScanTime = 0f;
        private float _scanInterval = 1.0f;

        // --- REFLECTION CACHE ---
        private Type _outlineType;
        private Type _outlineEffectType;
        private FieldInfo _outlineColorField;
        private PropertyInfo _outlineEnabledProp;
        private bool _reflectionInitialized = false;

        // Style
        private GUIStyle _styleLabel;
        private GUIStyle _styleBackground;
        private Texture2D _bgTexture;
        private Texture2D _boxTexture;
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
            public int OutlineColorIndex;
        }

        public ResourceEspModule()
        {
            InitializeLists();
            InitReflection();
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

        private void InitReflection()
        {
            try
            {
                // Próba załadowania typów bezpośrednio z Assembly-CSharp
                // Jeśli nazwa jest inna (np. QuickOutline), to trzeba tu zmienić. 
                // Ale "cakeslice" to standard w WT2.
                _outlineType = Type.GetType("cakeslice.Outline, Assembly-CSharp");
                _outlineEffectType = Type.GetType("cakeslice.OutlineEffect, Assembly-CSharp");

                if (_outlineType != null)
                {
                    _outlineColorField = _outlineType.GetField("color");
                    _outlineEnabledProp = _outlineType.GetProperty("enabled") ?? typeof(Behaviour).GetProperty("enabled");
                }
                _reflectionInitialized = true;
            }
            catch { }
        }

        public void Update()
        {
            if (!EspEnabled) return;
            if (!_reflectionInitialized) InitReflection();

            // Kontroluj główny efekt na kamerze
            if (ShowOutlines) CheckCameraEffect();

            if (Time.time - _lastScanTime > _scanInterval)
            {
                ScanObjects();
                _lastScanTime = Time.time;
            }
        }

        // --- ZARZĄDZANIE EFEKTEM KAMERY ---
        // Poprawka: Nie dodajemy nowego na siłę, jeśli gra już go ma.
        // Jeśli dodamy drugi, to shadery zwariują.
        private void CheckCameraEffect()
        {
            if (_outlineEffectType == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            // Szukamy istniejącego
            Component effect = cam.GetComponent(_outlineEffectType);

            // Jeśli nie ma, to znaczy że gra nie używa go w tej scenie -> Dodajemy
            if (effect == null)
            {
                effect = cam.gameObject.AddComponent(_outlineEffectType);
                // Domyślna konfiguracja (musi być, bo nowy komponent ma puste pola)
                SetField(effect, "lineThickness", 1.5f);
                SetField(effect, "lineIntensity", 3.0f);
                SetField(effect, "fillAmount", 0.0f);
                SetField(effect, "additiveRendering", false);
                SetField(effect, "backfaceCulling", true);
            }

            // Upewniamy się że jest włączony
            if (effect != null)
            {
                var p = _outlineEffectType.GetProperty("enabled") ?? typeof(Behaviour).GetProperty("enabled");
                if (p != null && (bool)p.GetValue(effect, null) == false)
                {
                    p.SetValue(effect, true, null);
                }

                // Aktualizujemy kolory (zawsze, żeby były zgodne z Configiem)
                // cakeslice używa 0, 1, 2
                SetField(effect, "lineColor0", ConfigManager.Colors.MobAggressive);
                SetField(effect, "lineColor1", ConfigManager.Colors.MobPassive);
                SetField(effect, "lineColor2", ConfigManager.Colors.ResMining);
            }
        }

        private void SetField(object obj, string name, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(name);
            if (f != null) f.SetValue(obj, value);
        }

        // --- SKANOWANIE ---

        private void ScanObjects()
        {
            if (!EspEnabled) { _cachedObjects.Clear(); return; }
            if (!_showResources && !_showMobs) { _cachedObjects.Clear(); return; }

            Vector3 playerPos = Vector3.zero;
            if (global::Player.localPlayer != null) playerPos = global::Player.localPlayer.transform.position;
            else if (Camera.main != null) playerPos = Camera.main.transform.position;

            List<CachedObject> newCache = new List<CachedObject>();

            try
            {
                // RESOURCES
                if (_showResources)
                {
                    List<string> activeMining = GetActiveKeys(_miningToggles);
                    List<string> activeGather = GetActiveKeys(_gatheringToggles);
                    List<string> activeLumber = GetActiveKeys(_lumberToggles);
                    List<string> activeGodsend = GetActiveKeys(_godsendToggles);

                    var objects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();

                    foreach (var obj in objects)
                    {
                        if (obj == null) continue;
                        if ((obj.transform.position - playerPos).sqrMagnitude > (_maxDistance * _maxDistance))
                        {
                            DisableOutlineRecursive(obj.gameObject);
                            continue;
                        }

                        string name = obj.name;
                        if (IsIgnored(name)) continue;

                        bool matched = false;
                        if (_showMining && CheckList(name, activeMining, obj, ConfigManager.Colors.ResMining, newCache, false, 2)) matched = true;
                        else if (_showGathering && CheckList(name, activeGather, obj, ConfigManager.Colors.ResGather, newCache, false, 1)) matched = true;
                        else if (_showLumber && CheckList(name, activeLumber, obj, ConfigManager.Colors.ResLumber, newCache, false, 2)) matched = true;
                        else if (_showGodsend && CheckList(name, activeGodsend, obj, new Color(0.8f, 0f, 1f), newCache, false, 0)) matched = true;

                        if (!matched && _showOthers && !name.Contains("Player") && !name.Contains("Character"))
                        {
                            AddToCache(newCache, obj.gameObject, obj.transform.position, obj.transform, name, Color.white, "", false, 0f, 2);
                            matched = true;
                        }

                        if (!matched) DisableOutlineRecursive(obj.gameObject);
                    }
                }

                // MOBS
                if (_showMobs)
                {
                    var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                    foreach (var mob in mobs)
                    {
                        if (mob != null && mob.health > 0)
                        {
                            if ((mob.transform.position - playerPos).sqrMagnitude > (_maxDistance * _maxDistance))
                            {
                                DisableOutlineRecursive(mob.gameObject);
                                continue;
                            }
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

        private bool CheckList(string objName, List<string> activeKeys, global::WTObject obj, Color color, List<CachedObject> cache, bool isMob, int outlineColorIndex)
        {
            if (activeKeys.Count == 0) return false;
            foreach (var key in activeKeys)
            {
                if (objName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddToCache(cache, obj.gameObject, obj.transform.position, obj.transform, key, color, "", isMob, 0f, outlineColorIndex);
                    return true;
                }
            }
            return false;
        }

        private void ProcessMob(global::WTMob mob, List<CachedObject> cache)
        {
            string name = mob.name;

            // HP handling
            int hp = mob.health;
            int maxHp = hp;
            try
            {
                var f = mob.GetType().GetField("healthMax");
                if (f != null) maxHp = (int)f.GetValue(mob);
            }
            catch { }
            string hpStr = $" [HP: {hp}/{maxHp}]";

            // Height for Box
            float height = 1.8f;
            try { var col = mob.GetComponent<Collider>(); if (col != null) height = col.bounds.size.y; } catch { }

            bool isAggro = name.Contains("LargeFox") || name.Contains("Boss") || name.Contains("King") || name.Contains("Elite") || name.Contains("Bear") || name.Contains("Wolf");
            bool isPassive = name.Contains("Hare") || name.Contains("Deer") || name.Contains("Stag") || name.Contains("Cow") || name.Contains("Sheep");

            int outlineColor = 0;
            Color textColor = Color.red;
            string label = name;
            bool show = false;

            if (isAggro)
            {
                if (_showAggressive) { textColor = ConfigManager.Colors.MobAggressive; label = "[!] " + name; outlineColor = 0; show = true; }
            }
            else if (isPassive)
            {
                if (_showPassive) { textColor = ConfigManager.Colors.MobPassive; outlineColor = 1; show = true; }
            }
            else
            {
                if (_showRetaliating) { textColor = ConfigManager.Colors.MobFleeing; outlineColor = 0; show = true; }
            }

            if (show) AddToCache(cache, mob.gameObject, mob.transform.position, mob.transform, label, textColor, hpStr, true, height, outlineColor);
            else DisableOutlineRecursive(mob.gameObject);
        }

        private void AddToCache(List<CachedObject> cache, GameObject go, Vector3 pos, Transform tr, string label, Color col, string hp, bool isMob, float h, int outIdx)
        {
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
                OutlineColorIndex = outIdx
            });

            // Aktywacja Glow (Rekurencyjnie na renderery)
            ApplyOutlineRecursive(go, outIdx);
        }

        // --- GLOW APPLICATION (Recursive) ---
        // Outline musi być na obiekcie z Rendererem, nie na pustym rodzicu!
        private void ApplyOutlineRecursive(GameObject root, int colorIndex)
        {
            if (!ShowOutlines) { DisableOutlineRecursive(root); return; }
            if (_outlineType == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>();

            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                if (rend is ParticleSystemRenderer) continue; // Ignoruj cząsteczki

                GameObject targetGo = rend.gameObject;

                try
                {
                    Component outline = targetGo.GetComponent(_outlineType);

                    // Jeśli nie ma, dodajemy
                    if (outline == null) outline = targetGo.AddComponent(_outlineType);

                    if (outline != null)
                    {
                        // Ustaw kolor
                        if (_outlineColorField != null)
                        {
                            // Sprawdź czy kolor się zmienił, żeby nie spamować
                            int currCol = (int)_outlineColorField.GetValue(outline);
                            if (currCol != colorIndex) _outlineColorField.SetValue(outline, colorIndex);
                        }

                        // Upewnij się że włączone
                        if (_outlineEnabledProp != null)
                        {
                            bool isEnabled = (bool)_outlineEnabledProp.GetValue(outline, null);
                            if (!isEnabled)
                            {
                                _outlineEnabledProp.SetValue(outline, true, null);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void DisableOutlineRecursive(GameObject root)
        {
            if (_outlineType == null) return;
            // Pobieramy wszystkie outline'y w dzieciach (nawet wyłączone)
            var outlines = root.GetComponentsInChildren(_outlineType, true);

            foreach (var outline in outlines)
            {
                try
                {
                    if (_outlineEnabledProp != null)
                    {
                        if ((bool)_outlineEnabledProp.GetValue(outline, null) == true)
                            _outlineEnabledProp.SetValue(outline, false, null);
                    }
                }
                catch { }
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

        // --- GUI ---
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

                GUILayout.BeginHorizontal();
                ShowBoxes = GUILayout.Toggle(ShowBoxes, "Box ESP");
                ShowOutlines = GUILayout.Toggle(ShowOutlines, "Game Highlight (Glow)");
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                _showResources = GUILayout.Toggle(_showResources, $"<b>{Localization.Get("ESP_RES_TITLE")}</b>");
                if (_showResources)
                {
                    GUILayout.BeginHorizontal(); GUILayout.Space(10); GUILayout.BeginVertical();
                    if (_showMining = GUILayout.Toggle(_showMining, Localization.Get("ESP_CAT_MINING"))) DrawDictionary(_miningToggles);
                    if (_showGathering = GUILayout.Toggle(_showGathering, Localization.Get("ESP_CAT_GATHER"))) DrawDictionary(_gatheringToggles);
                    if (_showLumber = GUILayout.Toggle(_showLumber, Localization.Get("ESP_CAT_LUMBER"))) DrawDictionary(_lumberToggles);
                    GUILayout.Space(5);
                    if (_showGodsend = GUILayout.Toggle(_showGodsend, Localization.Get("ESP_CAT_GODSEND"))) DrawDictionary(_godsendToggles);
                    GUILayout.Space(5);
                    _showOthers = GUILayout.Toggle(_showOthers, Localization.Get("ESP_CAT_OTHERS"));
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
            foreach (var key in keys) dict[key] = GUILayout.Toggle(dict[key], key);
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
        }

        public void DrawESP()
        {
            if (!EspEnabled) return;
            CreateStyles();
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 originPos = cam.transform.position;
            if (global::Player.localPlayer != null) originPos = global::Player.localPlayer.transform.position;
            float screenW = Screen.width;
            float screenH = Screen.height;

            foreach (var obj in _cachedObjects)
            {
                Vector3 currentPos = (obj.Transform != null) ? obj.Transform.position : obj.Position;
                float dist = Vector3.Distance(originPos, currentPos);
                if (dist > _maxDistance) continue;

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
                    if (obj.IsMob && ShowBoxes)
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
                }
            }
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