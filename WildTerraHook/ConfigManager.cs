using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WildTerraHook
{
    public static class ConfigManager
    {
        public static class Colors
        {
            public static Color MobAggressive = Color.red;
            public static Color MobPassive = Color.green;
            public static Color MobFleeing = new Color(1f, 0.64f, 0f);
            public static Color ResLumber = new Color(0.6f, 0.4f, 0.2f);
            public static Color ResMining = Color.gray;
            public static Color ResGather = Color.white;
        }

        // --- USTAWIENIA OGÓLNE ---
        public static string Language = "en";

        // --- AUTO LOOT PROFILES ---
        public static Dictionary<string, List<string>> LootProfiles = new Dictionary<string, List<string>>();

        // ZBIÓR AKTYWNYCH PROFILI (Multi-select)
        public static HashSet<string> ActiveProfiles = new HashSet<string>();

        private static string _folderPath;
        private static string _filePath;

        static ConfigManager()
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook");
            _filePath = Path.Combine(_folderPath, "config.txt");

            // Inicjalizacja domyślnego
            if (LootProfiles.Count == 0)
            {
                LootProfiles["Default"] = new List<string>();
                ActiveProfiles.Add("Default");
            }

            Load();
        }

        // Metoda łącząca wszystkie itemy z włączonych profili w jedną listę dla bota
        public static List<string> GetCombinedActiveList()
        {
            HashSet<string> combined = new HashSet<string>();

            foreach (var profName in ActiveProfiles)
            {
                if (LootProfiles.ContainsKey(profName))
                {
                    foreach (var item in LootProfiles[profName])
                    {
                        combined.Add(item);
                    }
                }
            }
            return combined.ToList();
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);

                using (StreamWriter sw = new StreamWriter(_filePath))
                {
                    sw.WriteLine($"Language={Language}");

                    // Zapis aktywnych profili po przecinku
                    string activeStr = string.Join(",", ActiveProfiles);
                    sw.WriteLine($"ActiveProfiles={activeStr}");

                    sw.WriteLine($"MobAggressive={ColorToString(Colors.MobAggressive)}");
                    sw.WriteLine($"MobPassive={ColorToString(Colors.MobPassive)}");
                    sw.WriteLine($"MobFleeing={ColorToString(Colors.MobFleeing)}");
                    sw.WriteLine($"ResLumber={ColorToString(Colors.ResLumber)}");
                    sw.WriteLine($"ResMining={ColorToString(Colors.ResMining)}");
                    sw.WriteLine($"ResGather={ColorToString(Colors.ResGather)}");

                    foreach (var kvp in LootProfiles)
                    {
                        string items = string.Join(";", kvp.Value.Where(x => !string.IsNullOrEmpty(x)));
                        sw.WriteLine($"Profile:{kvp.Key}={items}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WildTerraHook] Błąd zapisu configu: {ex.Message}");
            }
        }

        public static void Load()
        {
            if (!File.Exists(_filePath)) { Save(); return; }

            try
            {
                string[] lines = File.ReadAllLines(_filePath);
                bool profilesLoaded = false;

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;

                    string[] parts = line.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    if (key == "Language") Language = value;
                    else if (key == "ActiveProfiles")
                    {
                        ActiveProfiles.Clear();
                        if (!string.IsNullOrEmpty(value))
                        {
                            foreach (var p in value.Split(','))
                                if (!string.IsNullOrEmpty(p)) ActiveProfiles.Add(p);
                        }
                    }
                    else if (key.StartsWith("Profile:"))
                    {
                        if (!profilesLoaded) { LootProfiles.Clear(); profilesLoaded = true; }
                        string profileName = key.Substring(8);
                        List<string> items = new List<string>();
                        if (!string.IsNullOrEmpty(value)) items.AddRange(value.Split(';'));
                        LootProfiles[profileName] = items;
                    }
                    else if (key == "MobAggressive") Colors.MobAggressive = StringToColor(value);
                    else if (key == "MobPassive") Colors.MobPassive = StringToColor(value);
                    else if (key == "MobFleeing") Colors.MobFleeing = StringToColor(value);
                    else if (key == "ResLumber") Colors.ResLumber = StringToColor(value);
                    else if (key == "ResMining") Colors.ResMining = StringToColor(value);
                    else if (key == "ResGather") Colors.ResGather = StringToColor(value);
                }

                if (LootProfiles.Count == 0) LootProfiles["Default"] = new List<string>();
                if (ActiveProfiles.Count == 0 && LootProfiles.ContainsKey("Default")) ActiveProfiles.Add("Default");
            }
            catch { }
        }

        private static string ColorToString(Color c)
        {
            return $"{c.r.ToString(CultureInfo.InvariantCulture)},{c.g.ToString(CultureInfo.InvariantCulture)},{c.b.ToString(CultureInfo.InvariantCulture)},{c.a.ToString(CultureInfo.InvariantCulture)}";
        }

        private static Color StringToColor(string s)
        {
            try
            {
                string[] split = s.Split(',');
                if (split.Length >= 3)
                {
                    float r = float.Parse(split[0], CultureInfo.InvariantCulture);
                    float g = float.Parse(split[1], CultureInfo.InvariantCulture);
                    float b = float.Parse(split[2], CultureInfo.InvariantCulture);
                    float a = split.Length > 3 ? float.Parse(split[3], CultureInfo.InvariantCulture) : 1f;
                    return new Color(r, g, b, a);
                }
            }
            catch { }
            return Color.white;
        }
    }
}