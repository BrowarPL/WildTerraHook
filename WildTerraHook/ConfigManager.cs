using System;
using System.IO;
using System.Globalization;
using UnityEngine;

namespace WildTerraHook
{
    public static class ConfigManager
    {
        // Struktura przechowująca kolory
        public static class Colors
        {
            public static Color MobAggressive = Color.red;
            public static Color MobPassive = Color.green;
            public static Color MobFleeing = new Color(1f, 0.64f, 0f); // Orange

            public static Color ResLumber = new Color(0.6f, 0.4f, 0.2f); // Brown
            public static Color ResMining = Color.gray;
            public static Color ResGather = Color.white;
        }

        // --- USTAWIENIA OGÓLNE ---
        public static string Language = "en"; // Domyślnie angielski (pl/en)

        private static string _folderPath;
        private static string _filePath;

        static ConfigManager()
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook");
            _filePath = Path.Combine(_folderPath, "config.txt");

            Load();
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);

                using (StreamWriter sw = new StreamWriter(_filePath))
                {
                    // Zapis języka
                    sw.WriteLine($"Language={Language}");

                    // Zapis kolorów
                    sw.WriteLine($"MobAggressive={ColorToString(Colors.MobAggressive)}");
                    sw.WriteLine($"MobPassive={ColorToString(Colors.MobPassive)}");
                    sw.WriteLine($"MobFleeing={ColorToString(Colors.MobFleeing)}");

                    sw.WriteLine($"ResLumber={ColorToString(Colors.ResLumber)}");
                    sw.WriteLine($"ResMining={ColorToString(Colors.ResMining)}");
                    sw.WriteLine($"ResGather={ColorToString(Colors.ResGather)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WildTerraHook] Błąd zapisu configu: {ex.Message}");
            }
        }

        public static void Load()
        {
            if (!File.Exists(_filePath))
            {
                Save(); // Zapisz domyślne
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(_filePath);
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;

                    string[] parts = line.Split('=');
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    // Ładowanie języka
                    if (key == "Language")
                    {
                        Language = value;
                        continue;
                    }

                    // Ładowanie kolorów
                    Color col = StringToColor(value);
                    if (key == "MobAggressive") Colors.MobAggressive = col;
                    else if (key == "MobPassive") Colors.MobPassive = col;
                    else if (key == "MobFleeing") Colors.MobFleeing = col;
                    else if (key == "ResLumber") Colors.ResLumber = col;
                    else if (key == "ResMining") Colors.ResMining = col;
                    else if (key == "ResGather") Colors.ResGather = col;
                }
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