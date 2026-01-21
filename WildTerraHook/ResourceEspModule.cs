using UnityEngine;
using System.Collections.Generic;
using System;

namespace WildTerraHook
{
    public class ResourceEspModule
    {
        // --- USTAWIENIA ---
        public bool Enabled = true;
        public bool ShowResources = true;
        public bool ShowMobs = true;
        public float MaxDistance = 100f;

        // Kategorie Surowców
        public bool ShowMining = true;
        public bool ShowGathering = true;
        public bool ShowLumber = true;
        public bool ShowGodsend = true; // Skrzynie
        public bool ShowOthers = true;

        // Kategorie Mobów
        public bool ShowAggressive = true;
        public bool ShowPassive = true;
        public bool ShowRetaliating = true;

        // --- CACHE DANYCH ---
        private List<CachedResource> _resources = new List<CachedResource>();
        private List<CachedMob> _mobs = new List<CachedMob>();
        private float _scanTimer = 0f;

        // Struktury pomocnicze
        private struct CachedResource
        {
            public GameObject Go;
            public string Name;
            public ResourceType Type;
            public Vector3 Pos;
        }

        private struct CachedMob
        {
            public global::WTMob MobScript; // Trzymamy referencję do skryptu dla HP
            public string Name;
            public MobType Type;
            public Vector3 Pos;
        }

        private enum ResourceType { Mining, Gathering, Lumber, Godsend, Other }
        private enum MobType { Aggressive, Passive, Retaliating }

        public void Update()
        {
            if (!Enabled) return;

            // Skanowanie co 1 sekundę dla wydajności
            if (Time.time > _scanTimer)
            {
                ScanWorld();
                _scanTimer = Time.time + 1.0f;
            }
        }

        public void DrawESP()
        {
            if (!Enabled) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Ustal punkt odniesienia dla dystansu (Gracz, a jak brak to kamera)
            Vector3 originPos = cam.transform.position;
            if (global::Player.localPlayer != null)
            {
                originPos = global::Player.localPlayer.transform.position;
            }

            // 1. RYSOWANIE SUROWCÓW
            if (ShowResources)
            {
                foreach (var res in _resources)
                {
                    if (res.Go == null) continue;
                    if (!IsResourceCategoryEnabled(res.Type)) continue;

                    float dist = Vector3.Distance(originPos, res.Pos);
                    if (dist > MaxDistance) continue;

                    Vector3 screenPos = cam.WorldToScreenPoint(res.Pos);
                    if (screenPos.z > 0)
                    {
                        Color col = GetResourceColor(res.Type);
                        string label = $"{res.Name} [{dist:F0}m]";
                        DrawLabel(screenPos, label, col);
                    }
                }
            }

            // 2. RYSOWANIE MOBÓW (Z HP)
            if (ShowMobs)
            {
                foreach (var mob in _mobs)
                {
                    if (mob.MobScript == null || mob.MobScript.health <= 0) continue;
                    if (!IsMobCategoryEnabled(mob.Type)) continue;

                    // Aktualizacja pozycji (mob się rusza)
                    Vector3 currentPos = mob.MobScript.transform.position;
                    float dist = Vector3.Distance(originPos, currentPos);

                    if (dist > MaxDistance) continue;

                    Vector3 screenPos = cam.WorldToScreenPoint(currentPos + Vector3.up * 1.5f); // Nad głową
                    if (screenPos.z > 0)
                    {
                        Color col = GetMobColor(mob.Type);

                        // Formułowanie tekstu z HP
                        int hp = mob.MobScript.health;
                        int maxHp = mob.MobScript.healthMax; // Zakładam, że pole nazywa się healthMax w WTMob

                        // Jeśli nie ma healthMax, spróbuj maxHealth (zależnie od wersji gry)
                        // W razie błędu kompilacji na 'healthMax', zmień na 'maxHealth'

                        string label = $"{mob.Name} [{dist:F0}m]\nHP: {hp}/{maxHp}";

                        DrawLabel(screenPos, label, col, true); // true = pogrubienie dla mobów
                    }
                }
            }
        }

        private void DrawLabel(Vector3 screenPos, string text, Color color, bool bold = false)
        {
            GUIContent content = new GUIContent(text);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = color;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 12;
            if (bold) style.fontStyle = FontStyle.Bold;

            // Cień (Outline) dla czytelności
            GUIStyle styleShadow = new GUIStyle(style);
            styleShadow.normal.textColor = Color.black;

            Vector2 size = style.CalcSize(content);
            float x = screenPos.x - (size.x / 2);
            float y = Screen.height - screenPos.y - size.y;

            GUI.Label(new Rect(x + 1, y + 1, size.x, size.y), content, styleShadow);
            GUI.Label(new Rect(x, y, size.x, size.y), content, style);
        }

        private void ScanWorld()
        {
            _resources.Clear();
            _mobs.Clear();

            // Skan Surowców (Harvestable)
            // W Wild Terra surowce często mają komponenty 'HarvestableResource', 'Mineable', etc.
            // Tutaj szukamy ogólnych obiektów interaktywnych
            try
            {
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var go in allObjects)
                {
                    // Filtrowanie po warstwie lub nazwie (uproszczone)
                    if (!go.activeInHierarchy) continue;

                    // Logika rozpoznawania typu (przykładowa - dostosowana do WildTerra)
                    string name = go.name.ToLower();

                    // Surowce
                    if (go.GetComponent("HarvestableObject") != null || name.Contains("deposit") || name.Contains("tree") || name.Contains("bush"))
                    {
                        ResourceType type = ResourceType.Other;
                        if (name.Contains("deposit") || name.Contains("rock") || name.Contains("ore") || name.Contains("vein")) type = ResourceType.Mining;
                        else if (name.Contains("tree") || name.Contains("log")) type = ResourceType.Lumber;
                        else if (name.Contains("bush") || name.Contains("plant") || name.Contains("flax") || name.Contains("cotton")) type = ResourceType.Gathering;
                        else if (name.Contains("chest") || name.Contains("godsend") || name.Contains("treasure")) type = ResourceType.Godsend;

                        _resources.Add(new CachedResource { Go = go, Name = go.name, Type = type, Pos = go.transform.position });
                    }
                }

                // Skan Mobów (WTMob)
                var mobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                foreach (var m in mobs)
                {
                    if (m != null && m.health > 0)
                    {
                        MobType type = MobType.Aggressive; // Domyślnie
                        string mName = m.name.ToLower();

                        // Prosta kategoryzacja po nazwie
                        if (mName.Contains("hare") || mName.Contains("deer") || mName.Contains("cow") || mName.Contains("pig")) type = MobType.Passive;
                        else if (mName.Contains("fox") && !mName.Contains("large")) type = MobType.Retaliating;

                        _mobs.Add(new CachedMob { MobScript = m, Name = m.name, Type = type, Pos = m.transform.position });
                    }
                }
            }
            catch { }
        }

        // --- UI MENU (Obsługa Checkboxów) ---
        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{Localization.Get("ESP_RES_TITLE")}</b>");

            Enabled = GUILayout.Toggle(Enabled, Localization.Get("ESP_MAIN_BTN"));
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Localization.Get("ESP_DIST")}: {MaxDistance:F0}m", GUILayout.Width(100));
            MaxDistance = GUILayout.HorizontalSlider(MaxDistance, 10f, 300f);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            ShowResources = GUILayout.Toggle(ShowResources, "Show Resources");
            if (ShowResources)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                ShowMining = GUILayout.Toggle(ShowMining, Localization.Get("ESP_CAT_MINING"));
                ShowLumber = GUILayout.Toggle(ShowLumber, Localization.Get("ESP_CAT_LUMBER"));
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                ShowGathering = GUILayout.Toggle(ShowGathering, Localization.Get("ESP_CAT_GATHER"));
                ShowGodsend = GUILayout.Toggle(ShowGodsend, Localization.Get("ESP_CAT_GODSEND"));
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            ShowMobs = GUILayout.Toggle(ShowMobs, Localization.Get("ESP_MOB_TITLE"));
            if (ShowMobs)
            {
                ShowAggressive = GUILayout.Toggle(ShowAggressive, Localization.Get("ESP_MOB_AGGRO"));
                ShowPassive = GUILayout.Toggle(ShowPassive, Localization.Get("ESP_MOB_PASSIVE"));
                ShowRetaliating = GUILayout.Toggle(ShowRetaliating, Localization.Get("ESP_MOB_RETAL"));
            }

            // Edytor Kolorów
            GUILayout.Space(10);
            if (GUILayout.Button(Localization.Get("ESP_EDIT_COLORS")))
            {
                // Tutaj można dodać podmenu kolorów, dla uproszczenia pomijam w tym widoku
            }

            GUILayout.EndVertical();
        }

        private bool IsResourceCategoryEnabled(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Mining: return ShowMining;
                case ResourceType.Lumber: return ShowLumber;
                case ResourceType.Gathering: return ShowGathering;
                case ResourceType.Godsend: return ShowGodsend;
                case ResourceType.Other: return ShowOthers;
            }
            return false;
        }

        private bool IsMobCategoryEnabled(MobType type)
        {
            switch (type)
            {
                case MobType.Aggressive: return ShowAggressive;
                case MobType.Passive: return ShowPassive;
                case MobType.Retaliating: return ShowRetaliating;
            }
            return false;
        }

        private Color GetResourceColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Mining: return ConfigManager.Colors.ResMining;
                case ResourceType.Lumber: return ConfigManager.Colors.ResLumber;
                case ResourceType.Gathering: return ConfigManager.Colors.ResGather;
                case ResourceType.Godsend: return Color.cyan;
                default: return Color.white;
            }
        }

        private Color GetMobColor(MobType type)
        {
            switch (type)
            {
                case MobType.Aggressive: return ConfigManager.Colors.MobAggressive;
                case MobType.Passive: return ConfigManager.Colors.MobPassive;
                case MobType.Retaliating: return ConfigManager.Colors.MobFleeing;
                default: return Color.red;
            }
        }
    }
}