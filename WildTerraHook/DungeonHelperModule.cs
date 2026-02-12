using UnityEngine;
using UnityEngine.AI; // Potrzebne do nawigacji
using DunGen;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace WildTerraHook
{
    public class DungeonHelperModule
    {
        private string _bossName = "...";
        private string _statusInfo = "";
        private float _scanTimer = 0f;

        // GUI
        private Rect _mapWindowRect = new Rect(200, 200, 400, 550); // Szersze i wyższe
        private Texture2D _pixelTex;
        private Vector2 _listScroll;

        // Tryby wyświetlania
        private bool _showDumpList = false;

        // NAWIGACJA
        private Vector3? _navTargetPos = null;
        private string _navTargetName = "";
        private NavMeshPath _currentPath;
        private float _pathCalcTimer = 0f;

        // Cache mapy
        private struct MapNode
        {
            public Rect rect; // Pozycja 2D na mapie (bez skali)
            public Vector3 centerPos; // Pozycja 3D w świecie
            public Color color;
            public string rawName;
            public bool isGoal;
            public bool isStart;
        }
        private List<MapNode> _cachedMapNodes = new List<MapNode>();
        private Bounds _dungeonBounds;

        // Reflection (tylko bounds, bo depth nie działa)
        private FieldInfo _fieldLocalBounds;

        public DungeonHelperModule()
        {
            _pixelTex = new Texture2D(1, 1);
            _pixelTex.SetPixel(0, 0, Color.white);
            _pixelTex.Apply();

            _mapWindowRect.x = ConfigManager.Dungeon_MapX;
            _mapWindowRect.y = ConfigManager.Dungeon_MapY;
            _currentPath = new NavMeshPath();

            try
            {
                var type = typeof(TilePlacementData);
                _fieldLocalBounds = type.GetField("localBounds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch { }
        }

        public void Update()
        {
            if (!ConfigManager.Dungeon_Enabled) return;

            // Skanowanie co 2 sekundy
            if (Time.time > _scanTimer)
            {
                _scanTimer = Time.time + 2.0f;
                ScanTilesLogic();
            }

            // Odświeżanie ścieżki co 0.5 sekundy (jeśli mamy cel)
            if (_navTargetPos != null && Time.time > _pathCalcTimer)
            {
                _pathCalcTimer = Time.time + 0.5f;
                CalculatePathToTarget();
            }
        }

        public void OnGUI()
        {
            if (!ConfigManager.Dungeon_Enabled) return;

            // HUD Bossa (Góra ekranu)
            if (ConfigManager.Dungeon_ShowBossInfo && !string.IsNullOrEmpty(_bossName) && _bossName != "...")
            {
                GUI.Box(new Rect(Screen.width / 2 - 150, 60, 300, 40), "");
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.UpperCenter;
                style.fontSize = 20;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.yellow; // Złoty dla Goal
                GUI.Label(new Rect(0, 65, Screen.width, 30), $"GOAL: {_bossName}", style);
            }

            // Główne Okno Mapy
            if (ConfigManager.Dungeon_MapEnabled)
            {
                if (_mapWindowRect.x != ConfigManager.Dungeon_MapX) _mapWindowRect.x = ConfigManager.Dungeon_MapX;
                if (_mapWindowRect.y != ConfigManager.Dungeon_MapY) _mapWindowRect.y = ConfigManager.Dungeon_MapY;

                string title = _navTargetPos != null ? $"Nav -> {_navTargetName}" : $"Map ({_cachedMapNodes.Count})";
                _mapWindowRect = GUI.Window(999, _mapWindowRect, DrawMapWindow, title);

                if (_mapWindowRect.x != ConfigManager.Dungeon_MapX || _mapWindowRect.y != ConfigManager.Dungeon_MapY)
                {
                    ConfigManager.Dungeon_MapX = _mapWindowRect.x;
                    ConfigManager.Dungeon_MapY = _mapWindowRect.y;
                    ConfigManager.Save();
                }
            }
        }

        private void ScanTilesLogic()
        {
            Tile[] tiles = Object.FindObjectsOfType<Tile>();
            if (tiles == null || tiles.Length == 0) return;

            _cachedMapNodes.Clear();
            _bossName = "...";

            Vector3 min = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, 0, float.MinValue);

            foreach (Tile tile in tiles)
            {
                if (tile == null) continue;
                try
                {
                    // 1. Obliczanie Bounds (Fallbacki)
                    Bounds worldB = new Bounds(tile.transform.position, Vector3.zero);
                    bool boundsOk = false;

                    // Próba A: Reflection LocalBounds
                    if (tile.Placement != null && _fieldLocalBounds != null)
                    {
                        try
                        {
                            Bounds lb = (Bounds)_fieldLocalBounds.GetValue(tile.Placement);
                            if (lb.size.magnitude > 0.1f) { worldB = TransformBoundsManual(tile.transform, lb); boundsOk = true; }
                        }
                        catch { }
                    }

                    // Próba B: Colliders
                    if (!boundsOk || worldB.size.magnitude < 1f)
                    {
                        var cols = tile.GetComponentsInChildren<Collider>();
                        if (cols.Length > 0)
                        {
                            worldB = cols[0].bounds;
                            for (int i = 1; i < cols.Length; i++) worldB.Encapsulate(cols[i].bounds);
                            boundsOk = true;
                        }
                    }
                    // Próba C: Default
                    if (!boundsOk || worldB.size.magnitude < 1f) worldB = new Bounds(tile.transform.position, new Vector3(15, 5, 15));

                    min = Vector3.Min(min, worldB.min);
                    max = Vector3.Max(max, worldB.max);

                    // 2. Analiza Nazwy (Start/Goal)
                    string rawName = tile.gameObject.name;
                    string lower = rawName.ToLower();
                    bool isGoal = lower.Contains("goal") || lower.Contains("boss") || lower.Contains("end");
                    bool isStart = lower.Contains("start") || lower.Contains("entrance");

                    MapNode node = new MapNode
                    {
                        rect = new Rect(worldB.center.x, worldB.center.z, worldB.size.x, worldB.size.z),
                        centerPos = worldB.center, // Ważne dla nawigacji!
                        rawName = CleanName(rawName),
                        isGoal = isGoal,
                        isStart = isStart,
                        color = Color.gray
                    };

                    if (isStart) node.color = Color.cyan;
                    else if (isGoal)
                    {
                        node.color = Color.yellow;
                        _bossName = node.rawName; // Ustaw Bossa/Cel
                    }
                    else if (lower.Contains("corridor") || lower.Contains("hall")) node.color = new Color(0.4f, 0.4f, 0.4f, 0.5f); // Ciemniejszy szary
                    else node.color = new Color(0.7f, 0.7f, 0.7f, 0.8f); // Pokoje jaśniejsze

                    _cachedMapNodes.Add(node);
                }
                catch { }
            }

            if (_cachedMapNodes.Count > 0)
            {
                _dungeonBounds = new Bounds();
                _dungeonBounds.SetMinMax(min, max);

                // Jeśli nie znaleźliśmy celu po nazwie "Goal", a mamy ustawioną nawigację ręcznie, nie zmieniaj _bossName
            }
        }

        private void CalculatePathToTarget()
        {
            var player = Player.localPlayer;
            if (player == null || _navTargetPos == null) return;

            NavMesh.CalculatePath(player.transform.position, _navTargetPos.Value, NavMesh.AllAreas, _currentPath);
        }

        private void DrawMapWindow(int id)
        {
            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(new Rect(5, 20, _mapWindowRect.width - 10, _mapWindowRect.height - 25), _pixelTex);
            GUI.color = Color.white;

            float mapW = _mapWindowRect.width;
            float mapH = _mapWindowRect.height;
            // Zostawiamy dół na listę, góra na mapę
            float splitY = _showDumpList ? mapH * 0.5f : mapH - 40;

            float centerX = mapW / 2;
            float centerY = (splitY - 20) / 2 + 20;

            if (_cachedMapNodes.Count > 0)
            {
                Vector3 center = _dungeonBounds.center;
                var player = Player.localPlayer;

                // 1. RYSOWANIE KAFELKÓW
                foreach (var node in _cachedMapNodes)
                {
                    float rawX = node.rect.x - center.x;
                    float rawY = -(node.rect.y - center.z);
                    float scale = ConfigManager.Dungeon_MapScale * 1.5f;

                    Rect r = new Rect(
                        centerX + (rawX * scale) - (node.rect.width * scale) / 2,
                        centerY + (rawY * scale) - (node.rect.height * scale) / 2,
                        node.rect.width * scale,
                        node.rect.height * scale
                    );

                    // Clipping
                    if (r.right < 0 || r.x > mapW || r.bottom < 0 || r.y > splitY) continue;

                    GUI.color = node.color;
                    GUI.DrawTexture(r, _pixelTex);

                    // Rysuj X na celu
                    if (_navTargetPos != null && Vector3.Distance(node.centerPos, _navTargetPos.Value) < 1.0f)
                    {
                        GUI.color = Color.red;
                        GUI.Label(new Rect(r.center.x - 5, r.center.y - 10, 20, 20), "X");
                    }
                }

                // 2. RYSOWANIE ŚCIEŻKI (Zielona Linia)
                if (_currentPath != null && _currentPath.status == NavMeshPathStatus.PathComplete)
                {
                    Vector3[] corners = _currentPath.corners;
                    if (corners.Length > 1)
                    {
                        for (int i = 0; i < corners.Length - 1; i++)
                        {
                            Vector3 p1 = corners[i];
                            Vector3 p2 = corners[i + 1];

                            // Konwersja 3D -> 2D Mapy
                            Vector2 m1 = WorldToMap(p1, center, centerX, centerY);
                            Vector2 m2 = WorldToMap(p2, center, centerX, centerY);

                            // Rysowanie Linii
                            DrawLine(m1, m2, Color.green, 2f);
                        }
                    }
                }

                // 3. RYSOWANIE GRACZA
                if (player != null)
                {
                    Vector2 pm = WorldToMap(player.transform.position, center, centerX, centerY);
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(pm.x - 3, pm.y - 3, 6, 6), _pixelTex);

                    // Auto-Center Button
                    if (GUI.Button(new Rect(mapW - 60, 25, 55, 20), "Center")) _dungeonBounds.center = player.transform.position;
                }
            }

            // --- DOLNY PANEL (LISTA I STEROWANIE) ---
            float bottomH = mapH - splitY;

            GUILayout.BeginArea(new Rect(5, splitY, mapW - 10, bottomH - 5));

            // Nagłówek panelu
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showDumpList ? "▼ Hide List" : "▲ Show Tiles List")) _showDumpList = !_showDumpList;
            if (_navTargetPos != null)
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("STOP NAV")) { _navTargetPos = null; _navTargetName = ""; }
                GUI.backgroundColor = Color.white;
            }
            GUILayout.EndHorizontal();

            if (_showDumpList)
            {
                // Sortowanie: Najpierw Goal/Start, potem odległość od gracza
                Vector3 pPos = Player.localPlayer != null ? Player.localPlayer.transform.position : Vector3.zero;

                var sortedList = _cachedMapNodes.OrderByDescending(n => n.isGoal)
                                                .ThenByDescending(n => n.isStart)
                                                .ThenBy(n => Vector3.Distance(n.centerPos, pPos))
                                                .ToList();

                _listScroll = GUILayout.BeginScrollView(_listScroll);
                foreach (var node in sortedList)
                {
                    GUILayout.BeginHorizontal();

                    // Kolor tekstu
                    if (node.isGoal) GUI.color = Color.yellow;
                    else if (node.isStart) GUI.color = Color.cyan;
                    else GUI.color = Color.white;

                    float dist = Vector3.Distance(node.centerPos, pPos);
                    GUILayout.Label($"{node.rawName} ({dist:F0}m)", GUILayout.Width(200));

                    GUI.color = Color.green;
                    if (GUILayout.Button("[NAV]", GUILayout.Width(50)))
                    {
                        _navTargetPos = node.centerPos;
                        _navTargetName = node.rawName;
                        CalculatePathToTarget();
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();
            GUI.color = Color.white;
            GUI.DragWindow();
        }

        // Pomocnicza funkcja do przeliczania współrzędnych
        private Vector2 WorldToMap(Vector3 worldPos, Vector3 center, float cx, float cy)
        {
            float rawX = worldPos.x - center.x;
            float rawY = -(worldPos.z - center.z);
            float scale = ConfigManager.Dungeon_MapScale * 1.5f;
            return new Vector2(cx + (rawX * scale), cy + (rawY * scale));
        }

        // Pomocnicza funkcja do rysowania linii
        private void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            Vector2 d = end - start;
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float l = d.magnitude;

            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(a, start);
            GUI.color = color;
            GUI.DrawTexture(new Rect(start.x, start.y, l, width), _pixelTex);
            GUI.matrix = m;
        }

        private Bounds TransformBoundsManual(Transform t, Bounds localB)
        {
            var center = t.TransformPoint(localB.center);
            float sx = localB.size.x * t.lossyScale.x;
            float sz = localB.size.z * t.lossyScale.z;
            float yRot = t.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(yRot, 90)) < 45 || Mathf.Abs(Mathf.DeltaAngle(yRot, 270)) < 45) { float tmp = sx; sx = sz; sz = tmp; }
            return new Bounds(center, new Vector3(sx, 5, sz));
        }

        private string CleanName(string raw)
        {
            string s = raw.Replace("(Clone)", "").Replace("Tile_", "").Replace("Room_", "").Replace("Boss_", "").Replace("Dungeon_", "").Trim();
            return System.Text.RegularExpressions.Regex.Replace(s, @"[\d-]", string.Empty);
        }
    }
}