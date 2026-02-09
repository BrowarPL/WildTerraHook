using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using System.IO;
using System;
using System.Globalization;

namespace WildTerraHook
{
    public class ObjectManagerModule
    {
        private float _lastScanTime = 0f;
        private List<ScannedObject> _nearbyObjects = new List<ScannedObject>();
        private Vector2 _scrollPos;
        private string _searchFilter = "";

        // Baza danych w pamięci
        private List<RuntimeObjectData> _runtimeData = new List<RuntimeObjectData>();
        private string _dbPath;

        private bool _scanRequested = false;

        private const float POSITION_TOLERANCE = 0.5f;
        private const float STACK_TOLERANCE = 0.2f;

        public ObjectManagerModule()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "WildTerraHook");

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            // Zmieniamy rozszerzenie na .txt, bo to już nie json
            _dbPath = Path.Combine(folder, "named_objects_db.txt");
            LoadDatabase();
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.L)) DebugMatching();

            if (_scanRequested)
            {
                ScanNearby();
                _scanRequested = false;
                return;
            }

            if (_nearbyObjects.Any(x => x.IsEditing)) return;

            if (Time.time - _lastScanTime < 1.0f) return;
            _lastScanTime = Time.time;
            ScanNearby();
        }

        private void ScanNearby()
        {
            var player = Player.localPlayer;
            if (player == null) return;

            _nearbyObjects.Clear();
            float range = ConfigManager.ObjectManager_Radius;
            bool hideNoActions = ConfigManager.ObjectManager_HideNoActions;

            Collider[] hits = Physics.OverlapSphere(player.transform.position, range);
            List<WTObject> validObjects = new List<WTObject>();

            foreach (var hit in hits)
            {
                var wtObj = hit.GetComponentInParent<WTObject>();
                if (wtObj != null && !validObjects.Contains(wtObj))
                {
                    if (!wtObj.enabled) continue;

                    if (hideNoActions)
                    {
                        if (wtObj.actionSkills == null || wtObj.actionSkills.Count == 0) continue;
                        if (!wtObj.actionSkills.Any(s => s != null)) continue;
                    }

                    validObjects.Add(wtObj);
                }
            }

            foreach (var wtObj in validObjects)
            {
                int stableIndex = CalculateStableIndex(wtObj, validObjects);
                string customName = GetSavedName(wtObj, stableIndex);

                _nearbyObjects.Add(new ScannedObject
                {
                    Obj = wtObj,
                    Distance = Vector3.Distance(player.transform.position, wtObj.transform.position),
                    DisplayName = string.IsNullOrEmpty(customName) ? wtObj.GetLocalizedName() : customName,
                    OriginalName = wtObj.GetLocalizedName(),
                    InternalName = wtObj.name,
                    StableIndex = stableIndex,
                    WorldId = wtObj.worldId
                });
            }

            _nearbyObjects.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        private int CalculateStableIndex(WTObject subject, List<WTObject> allObjects)
        {
            var siblings = allObjects.Where(x =>
                x.name == subject.name &&
                Vector3.Distance(x.transform.position, subject.transform.position) < STACK_TOLERANCE
            ).ToList();

            if (siblings.Count <= 1) return 0;
            siblings.Sort((a, b) => a.worldId.CompareTo(b.worldId));
            return siblings.IndexOf(subject);
        }

        public void DrawMenu()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.Get("OBJ_RADIUS") + $": {ConfigManager.ObjectManager_Radius:F0}m");
            ConfigManager.ObjectManager_Radius = GUILayout.HorizontalSlider(ConfigManager.ObjectManager_Radius, 2f, 20f);
            GUILayout.EndHorizontal();

            bool hideEmpty = GUILayout.Toggle(ConfigManager.ObjectManager_HideNoActions, " " + Localization.Get("OBJ_HIDE_EMPTY"));
            if (hideEmpty != ConfigManager.ObjectManager_HideNoActions)
            {
                ConfigManager.ObjectManager_HideNoActions = hideEmpty;
                ConfigManager.Save();
                _scanRequested = true;
            }

            _searchFilter = GUILayout.TextField(_searchFilter);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            if (_nearbyObjects.Count == 0)
            {
                GUILayout.Label(Localization.Get("OBJ_NO_OBJECTS"));
            }

            var objectsToDraw = _nearbyObjects.ToList();

            foreach (var item in objectsToDraw)
            {
                if (!string.IsNullOrEmpty(_searchFilter) && !item.DisplayName.ToLower().Contains(_searchFilter.ToLower())) continue;
                DrawObjectItem(item);
            }

            GUILayout.EndScrollView();
        }

        private void DrawObjectItem(ScannedObject item)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();

            if (item.IsEditing)
            {
                item.EditName = GUILayout.TextField(item.EditName, GUILayout.Width(150));

                if (GUILayout.Button("OK", GUILayout.Width(35)))
                {
                    SaveName(item.Obj, item.StableIndex, item.EditName);
                    item.DisplayName = item.EditName;
                    item.IsEditing = false;
                    _scanRequested = true;
                }
            }
            else
            {
                string debugInfo = $" <color=#888888>[ID:{item.WorldId} R:{item.StableIndex}]</color>";
                string label = $"<b>{item.DisplayName}</b>{debugInfo} <size=10>({item.OriginalName})</size>";

                if (GUILayout.Button(label, GUI.skin.label))
                {
                    item.IsEditing = true;
                    item.EditName = item.DisplayName;
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{item.Distance:F1}m");
            GUILayout.EndHorizontal();

            if (item.Obj.actionSkills != null && item.Obj.actionSkills.Count > 0)
            {
                GUILayout.BeginHorizontal();
                foreach (var skill in item.Obj.actionSkills)
                {
                    if (skill == null) continue;
                    string btnName = skill.name;
                    try { btnName = skill.GetLocalizedName(); } catch { }
                    if (GUILayout.Button(btnName, GUILayout.Height(20))) ExecuteAction(item.Obj, skill);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("<size=10><i>Brak dostępnych akcji</i></size>");
            }
            GUILayout.EndVertical();
        }

        private void ExecuteAction(WTObject obj, ScriptableSkill skill)
        {
            var player = Player.localPlayer as WTPlayer;
            if (player == null) return;
            try { player.WorldObjectTryAction(obj, skill); }
            catch (Exception ex) { Debug.LogError($"[ObjectManager] Błąd: {ex.Message}"); }
        }

        // --- ZAPIS / ODCZYT (CUSTOM TEXT FORMAT) ---

        private string GetSavedName(WTObject obj, int stableIndex)
        {
            var data = _runtimeData.FirstOrDefault(x =>
                Vector3.Distance(x.Position, obj.transform.position) < POSITION_TOLERANCE &&
                x.OriginalName == obj.name &&
                x.RankIndex == stableIndex);

            return data?.CustomName;
        }

        private void SaveName(WTObject obj, int stableIndex, string newName)
        {
            // Aktualizacja pamięci
            _runtimeData.RemoveAll(x =>
                Vector3.Distance(x.Position, obj.transform.position) < POSITION_TOLERANCE &&
                x.OriginalName == obj.name &&
                x.RankIndex == stableIndex
            );

            _runtimeData.Add(new RuntimeObjectData
            {
                Position = obj.transform.position,
                OriginalName = obj.name,
                RankIndex = stableIndex,
                CustomName = newName
            });

            SaveDatabase();
        }

        private void SaveDatabase()
        {
            try
            {
                List<string> lines = new List<string>();

                // FORMAT: X|Y|Z|OriginalName|RankIndex|CustomName
                foreach (var item in _runtimeData)
                {
                    string line = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4}|{5}",
                        item.Position.x,
                        item.Position.y,
                        item.Position.z,
                        item.OriginalName,
                        item.RankIndex,
                        item.CustomName);
                    lines.Add(line);
                }

                File.WriteAllLines(_dbPath, lines);
                Debug.Log($"[ObjectManager] Zapisano {lines.Count} linii do: {_dbPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ObjectManager] BŁĄD ZAPISU TXT: " + e.Message);
            }
        }

        private void LoadDatabase()
        {
            if (File.Exists(_dbPath))
            {
                try
                {
                    _runtimeData.Clear();
                    string[] lines = File.ReadAllLines(_dbPath);

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;

                        string[] parts = line.Split('|');
                        if (parts.Length >= 6)
                        {
                            float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                            float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                            float z = float.Parse(parts[2], CultureInfo.InvariantCulture);
                            string orgName = parts[3];
                            int rank = int.Parse(parts[4]);
                            string custName = parts[5];

                            _runtimeData.Add(new RuntimeObjectData
                            {
                                Position = new Vector3(x, y, z),
                                OriginalName = orgName,
                                RankIndex = rank,
                                CustomName = custName
                            });
                        }
                    }
                    Debug.Log($"[ObjectManager] Wczytano {_runtimeData.Count} obiektów z TXT.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ObjectManager] BŁĄD ODCZYTU TXT: {e.Message}");
                }
            }
            else
            {
                // Jeśli nie ma pliku .txt, sprawdź stary .json i spróbuj skonwertować (opcjonalne, ale miłe)
                // W tym przypadku po prostu zaczynamy od zera.
                Debug.Log($"[ObjectManager] Tworzenie nowej bazy: {_dbPath}");
            }
        }

        private void DebugMatching()
        {
            Debug.LogWarning("=== OBJECT MANAGER DEBUG ===");
            foreach (var saved in _runtimeData)
            {
                Debug.Log($"DB: '{saved.CustomName}' @ {saved.Position} (R:{saved.RankIndex})");
            }
            Debug.LogWarning("============================");
        }

        private class RuntimeObjectData
        {
            public Vector3 Position;
            public string OriginalName;
            public int RankIndex;
            public string CustomName;
        }

        private class ScannedObject
        {
            public WTObject Obj;
            public float Distance;
            public string DisplayName;
            public string OriginalName;
            public string InternalName;
            public bool IsEditing;
            public string EditName;
            public int StableIndex;
            public int WorldId;
        }
    }
}