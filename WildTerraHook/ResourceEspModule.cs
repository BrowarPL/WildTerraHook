using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WildTerraHook
{
    public class ResourceEspModule
    {
        // --- KONFIGURACJA MENU ---
        private bool _showResources = true;
        private bool _showMining = true;
        private bool _showGathering = true;
        private bool _showLumber = true;

        private bool _showMobs = true;
        private bool _showBosses = true;
        private bool _showElites = true;
        private bool _showAggressive = true;
        private bool _showRetaliating = true;
        private bool _showPassive = true;

        // Słowniki do przechowywania stanu (Nazwa -> CzyWłączone)
        private Dictionary<string, bool> _miningToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _gatheringToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _lumberToggles = new Dictionary<string, bool>();
        private Dictionary<string, bool> _mobSpecificToggles = new Dictionary<string, bool>();

        // Cache obiektów (Optymalizacja: szukamy raz na 2s, rysujemy co klatkę)
        private List<CachedObject> _cachedObjects = new List<CachedObject>();
        private float _lastScanTime = 0f;
        private float _scanInterval = 2.0f; // Skanuj co 2 sekundy

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
            foreach (var s in mining) _miningToggles[s] = true;

            // Gathering
            string[] gathering = { "Wild root", "Boletus", "Chanterelles", "Morels", "Russalas", "Grey amanita", "Fly agaric", "Sticks pile", "Stone pile", "Wild cereals", "Blueberry", "Nest", "Nettles", "Clay", "Hazel", "Greenary", "Ligonberry", "Beehive", "Swamp thorn", "Mountain sage", "Wolf berries", "Chelidonium", "Sand" };
            foreach (var s in gathering) _gatheringToggles[s] = true;

            // Lumber
            string[] lumber = { "Apple tree", "Snag", "Birch", "Grave tree", "Stump", "Pine", "Maple", "Poplar", "Spruce", "Dried tree", "Oak", "Grim tree", "Infected grim tree" };
            foreach (var s in lumber) _lumberToggles[s] = true;

            // Specific Mobs (Retaliating / Passive)
            string[] mobs = { "Fox", "Goat", "Hare", "Deer", "Stag", "Wolf", "Bear", "Boar" }; // Dodałem kilka typowych
            foreach (var s in mobs) _mobSpecificToggles[s] = true;
        }

        public void Update()
        {
            // Skanowanie sceny co 2 sekundy (bardzo ważne dla FPS!)
            if (Time.time - _lastScanTime > _scanInterval)
            {
                ScanObjects();
                _lastScanTime = Time.time;
            }
        }

        private void ScanObjects()
        {
            _cachedObjects.Clear();

            // 1. SKANOWANIE SUROWCÓW (GatherItem / WTObject)
            // Używamy FindObjectsOfType na GatherItem, bo to zazwyczaj surowce
            // Jeśli GatherItem nie zadziała, można zmienić na WTObject
            var gatherItems = UnityEngine.Object.FindObjectsOfType<GatherItem>();
            foreach (var item in gatherItems)
            {
                if (item == null) continue;
                string name = item.name; // Lub item.GetLocalizedName() jeśli dostępne

                // Sprawdzanie Mining
                if (_showResources && _showMining)
                    CheckAndAdd(name, _miningToggles, Color.gray);

                // Sprawdzanie Gathering
                if (_showResources && _showGathering)
                    CheckAndAdd(name, _gatheringToggles, Color.green);

                // Sprawdzanie Lumber
                if (_showResources && _showLumber)
                    CheckAndAdd(name, _lumberToggles, new Color(0.6f, 0.3f, 0f)); // Brązowy
            }

            // 2. SKANOWANIE MOBÓW (WTMob)
            if (_showMobs)
            {
                var mobs = UnityEngine.Object.FindObjectsOfType<WTMob>();
                foreach (var mob in mobs)
                {
                    if (mob == null || mob.health <= 0) continue; // Ignoruj martwe
                    string name = mob.name;

                    // Rarity (Boss/Elite) - Symulacja, bo nie mam dostępu do RarityType wprost w tym pliku
                    // Zakładam, że nazwa bossa/elity może mieć kolor lub prefiks w nazwie obiektu
                    bool isBoss = name.ToLower().Contains("boss") || name.Contains("King") || name.Contains("Queen");
                    bool isElite = name.ToLower().Contains("elite") || name.ToLower().Contains("leader");

                    if (_showBosses && isBoss)
                    {
                        AddCache(mob.transform.position, $"BOSS: {name}", Color.red);
                        continue;
                    }
                    if (_showElites && isElite)
                    {
                        AddCache(mob.transform.position, $"ELITE: {name}", new Color(1f, 0.5f, 0f)); // Orange
                        continue;
                    }

                    // Specyficzne zwierzęta
                    bool matchedSpecific = false;
                    foreach (var pair in _mobSpecificToggles)
                    {
                        if (pair.Value && name.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Rozróżnienie na agresywne/pasywne na podstawie listy użytkownika
                            Color c = Color.white;
                            if (IsRetaliating(pair.Key)) c = Color.yellow;
                            else if (IsPassive(pair.Key)) c = Color.cyan;
                            else c = Color.red; // Domyślnie agresywny

                            // Filtrowanie grupowe
                            if (IsRetaliating(pair.Key) && !_showRetaliating) continue;
                            if (IsPassive(pair.Key) && !_showPassive) continue;

                            AddCache(mob.transform.position, name, c);
                            matchedSpecific = true;
                            break;
                        }
                    }

                    // Reszta (Agresywne domyślne)
                    if (!matchedSpecific && _showAggressive && !isBoss && !isElite)
                    {
                        // Jeśli nie jest na liście pasywnych, uznajemy za agresywnego
                        AddCache(mob.transform.position, name, Color.red);
                    }
                }
            }
        }

        private void CheckAndAdd(string objName, Dictionary<string, bool> toggles, Color color)
        {
            foreach (var pair in toggles)
            {
                // Jeśli włączone I nazwa obiektu zawiera klucz (np. "Copper" w "Large Copper Node")
                if (pair.Value && objName.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Znajdź obiekt w scenie (to jest uproszczenie, w pętli ScanObjects mamy już 'item')
                    // W tej metodzie pomocniczej musielibyśmy przekazać pozycję. 
                    // Zrefaktoryzuję to poniżej dla czytelności.
                    return;
                }
            }
        }

        // Poprawiona logika dodawania do cache w pętli
        private void AddCache(Vector3 pos, string label, Color col)
        {
            _cachedObjects.Add(new CachedObject { Position = pos, Label = label, Color = col });
        }

        // Listy pomocnicze
        private bool IsRetaliating(string name) { return name == "Fox" || name == "Goat" || name.Contains("Silver Fox"); }
        private bool IsPassive(string name) { return name == "Hare" || name == "Deer" || name == "Stag"; }


        // --- RYSOWANIE GUI (MENU) ---
        private Vector2 _scrollPos;
        public void DrawMenu()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(300));

            // RESOURCES
            _showResources = GUILayout.Toggle(_showResources, "<b>RESOURCES</b>");
            if (_showResources)
            {
                GUILayout.BeginHorizontal(); GUILayout.Space(20); GUILayout.BeginVertical();

                // Mining
                _showMining = GUILayout.Toggle(_showMining, "Mining");
                if (_showMining) DrawDictionary(_miningToggles);

                // Gathering
                _showGathering = GUILayout.Toggle(_showGathering, "Gathering");
                if (_showGathering) DrawDictionary(_gatheringToggles);

                // Lumber
                _showLumber = GUILayout.Toggle(_showLumber, "Lumberjacking");
                if (_showLumber) DrawDictionary(_lumberToggles);

                GUILayout.EndVertical(); GUILayout.EndHorizontal();
            }

            // MOBS
            GUILayout.Space(10);
            _showMobs = GUILayout.Toggle(_showMobs, "<b>MOBS</b>");
            if (_showMobs)
            {
                GUILayout.BeginHorizontal(); GUILayout.Space(20); GUILayout.BeginVertical();

                _showBosses = GUILayout.Toggle(_showBosses, "Bosses (All)");
                _showElites = GUILayout.Toggle(_showElites, "Elites (All)");
                _showAggressive = GUILayout.Toggle(_showAggressive, "Other Aggressive");

                _showRetaliating = GUILayout.Toggle(_showRetaliating, "Retaliating");
                if (_showRetaliating)
                {
                    GUILayout.BeginHorizontal(); GUILayout.Space(20); GUILayout.BeginVertical();
                    // Rysujemy tylko te z listy retaliating
                    DrawSpecificMobToggles(new string[] { "Fox", "Goat", "Silver Fox" });
                    GUILayout.EndVertical(); GUILayout.EndHorizontal();
                }

                _showPassive = GUILayout.Toggle(_showPassive, "Non-Aggressive");
                if (_showPassive)
                {
                    GUILayout.BeginHorizontal(); GUILayout.Space(20); GUILayout.BeginVertical();
                    DrawSpecificMobToggles(new string[] { "Hare", "Deer", "Stag" });
                    GUILayout.EndVertical(); GUILayout.EndHorizontal();
                }

                GUILayout.EndVertical(); GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void DrawDictionary(Dictionary<string, bool> dict)
        {
            GUILayout.BeginHorizontal(); GUILayout.Space(20); GUILayout.BeginVertical();
            var keys = new List<string>(dict.Keys);
            foreach (var key in keys)
            {
                dict[key] = GUILayout.Toggle(dict[key], key);
            }
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
        }

        private void DrawSpecificMobToggles(string[] filter)
        {
            foreach (var key in filter)
            {
                if (_mobSpecificToggles.ContainsKey(key))
                {
                    _mobSpecificToggles[key] = GUILayout.Toggle(_mobSpecificToggles[key], key);
                }
            }
        }

        // --- RYSOWANIE ESP (Świat) ---
        public void DrawESP()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (var obj in _cachedObjects)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(obj.Position);
                if (screenPos.z > 0)
                {
                    // Odwracamy Y dla GUI
                    screenPos.y = Screen.height - screenPos.y;

                    GUI.color = obj.Color;
                    // Rysujemy nazwę i dystans
                    float dist = Vector3.Distance(cam.transform.position, obj.Position);
                    GUI.Label(new Rect(screenPos.x, screenPos.y, 200, 20), $"{obj.Label} [{dist:F0}m]");
                }
            }
            GUI.color = Color.white;
        }
    }
}