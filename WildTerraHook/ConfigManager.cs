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
        // Słownik: Nazwa Profilu -> Lista Przedmiotów
        public static Dictionary<string, List<string>> LootProfiles = new Dictionary<string, List<string>>();
        public static string ActiveProfile = "Default";

        private static string _folderPath;
        private static string _filePath;

        static ConfigManager()
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook");
            _filePath = Path.Combine(_folderPath, "config.txt");

            // Inicjalizacja domyślnego profilu
            if (LootProfiles.Count == 0)
            {
                LootProfiles["Default"] = new List<string>();
                ActiveProfile = "Default";
            }

            Load();
        }

        public static List<string> GetActiveList()
        {
            if (!LootProfiles.ContainsKey(ActiveProfile))
            {
                // Fallback jeśli aktywny profil zniknął
                if (LootProfiles.Count > 0) ActiveProfile = LootProfiles.Keys.First();
                else
                {
                    ActiveProfile = "Default";
                    LootProfiles["Default"] = new List<string>();
                }
            }
            return LootProfiles[ActiveProfile];
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);

                using (StreamWriter sw = new StreamWriter(_filePath))
                {
                    sw.WriteLine($"Language={Language}");
                    sw.WriteLine($"ActiveProfile={ActiveProfile}");

                    // Zapis kolorów
                    sw.WriteLine($"MobAggressive={ColorToString(Colors.MobAggressive)}");
                    sw.WriteLine($"MobPassive={ColorToString(Colors.MobPassive)}");
                    sw.WriteLine($"MobFleeing={ColorToString(Colors.MobFleeing)}");
                    sw.WriteLine($"ResLumber={ColorToString(Colors.ResLumber)}");
                    sw.WriteLine($"ResMining={ColorToString(Colors.ResMining)}");
                    sw.WriteLine($"ResGather={ColorToString(Colors.ResGather)}");

                    // Zapis Profili
                    // Format: Profile:Nazwa=Item1;Item2;Item3
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

                // Tymczasowe czyszczenie profili przed wczytaniem (żeby nie dublować przy reloadzie)
                // Ale zostawiamy instancję, jeśli to pierwszy run
                bool profilesLoaded = false;

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;

                    string[] parts = line.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    if (key == "Language") Language = value;
                    else if (key == "ActiveProfile") ActiveProfile = value;

                    // Ładowanie Profilu
                    else if (key.StartsWith("Profile:"))
                    {
                        if (!profilesLoaded)
                        {
                            LootProfiles.Clear();
                            profilesLoaded = true;
                        }

                        string profileName = key.Substring(8); // Ucinamy "Profile:"
                        List<string> items = new List<string>();
                        if (!string.IsNullOrEmpty(value))
                        {
                            items.AddRange(value.Split(';'));
                        }
                        LootProfiles[profileName] = items;
                    }
                    // Kolory
                    else if (key == "MobAggressive") Colors.MobAggressive = StringToColor(value);
                    else if (key == "MobPassive") Colors.MobPassive = StringToColor(value);
                    else if (key == "MobFleeing") Colors.MobFleeing = StringToColor(value);
                    else if (key == "ResLumber") Colors.ResLumber = StringToColor(value);
                    else if (key == "ResMining") Colors.ResMining = StringToColor(value);
                    else if (key == "ResGather") Colors.ResGather = StringToColor(value);
                }

                // Bezpiecznik po załadowaniu
                if (LootProfiles.Count == 0) LootProfiles["Default"] = new List<string>();
                if (!LootProfiles.ContainsKey(ActiveProfile)) ActiveProfile = LootProfiles.Keys.First();
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