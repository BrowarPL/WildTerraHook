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
        // --- ŚCIEŻKI ---
        private static string _folderPath;
        private static string _filePath;

        // --- GLOBALNE ---
        public static string Language = "en";

        // --- COLORS ---
        public static class Colors
        {
            public static Color MobAggressive = Color.red;
            public static Color MobPassive = Color.green;
            public static Color MobFleeing = new Color(1f, 0.64f, 0f); // Orange
            public static Color ResLumber = new Color(0.6f, 0.4f, 0.2f); // Brown
            public static Color ResMining = Color.gray;
            public static Color ResGather = Color.white;
        }

        // --- LOOT ---
        public static Dictionary<string, List<string>> LootProfiles = new Dictionary<string, List<string>>();
        public static HashSet<string> ActiveProfiles = new HashSet<string>();
        public static bool Loot_Enabled = false;
        public static float Loot_Delay = 0.2f;
        public static bool Loot_Debug = false;

        // --- FISH BOT (NOWOŚĆ) ---
        public static bool Fish_Enabled = false;
        public static bool Fish_AutoPress = false;
        public static float Fish_ReactionTime = 0.3f;
        public static bool Fish_ShowESP = true;

        // --- MISC ---
        public static bool Misc_EternalDay = false;
        public static bool Misc_NoFog = false;
        public static bool Misc_Fullbright = false;
        public static bool Misc_BrightPlayer = false;
        public static float Misc_LightIntensity = 2.0f;
        public static float Misc_LightRange = 1000f;
        public static bool Misc_ZoomHack = false;
        public static float Misc_ZoomLimit = 100f;
        public static float Misc_CamAngle = 45f;
        public static float Misc_ZoomSpeed = 60f;
        public static float Misc_Fov = 60f;
        public static float Misc_RenderDistance = 500f;

        // --- ESP SETTINGS ---
        public static bool Esp_Enabled = false;
        public static float Esp_Distance = 150f;
        public static bool Esp_ShowBoxes = true;
        public static bool Esp_ShowXRay = true;

        public static bool Esp_ShowResources = false;
        public static bool Esp_ShowMobs = false;

        public static bool Esp_Cat_Mining = false;
        public static bool Esp_Cat_Gather = false;
        public static bool Esp_Cat_Lumber = false;
        public static bool Esp_Cat_Godsend = false;
        public static bool Esp_Cat_Others = false;

        public static bool Esp_Mob_Aggro = false;
        public static bool Esp_Mob_Retal = false;
        public static bool Esp_Mob_Passive = false;

        public static string Esp_List_Mining = "";
        public static string Esp_List_Gather = "";
        public static string Esp_List_Lumber = "";
        public static string Esp_List_Godsend = "";

        static ConfigManager()
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook");
            _filePath = Path.Combine(_folderPath, "config.txt");

            if (LootProfiles.Count == 0)
            {
                LootProfiles["Default"] = new List<string>();
                ActiveProfiles.Add("Default");
            }
            Load();
        }

        public static List<string> GetCombinedActiveList()
        {
            HashSet<string> combined = new HashSet<string>();
            foreach (var profName in ActiveProfiles)
            {
                if (LootProfiles.ContainsKey(profName))
                    foreach (var item in LootProfiles[profName]) combined.Add(item);
            }
            return combined.ToList();
        }

        public static string SerializeToggleList(Dictionary<string, bool> dict)
        {
            List<string> active = new List<string>();
            foreach (var kvp in dict) if (kvp.Value) active.Add(kvp.Key);
            return string.Join(",", active);
        }

        public static void DeserializeToggleList(string data, Dictionary<string, bool> dict)
        {
            if (string.IsNullOrEmpty(data)) return;
            var keys = new List<string>(dict.Keys);
            foreach (var k in keys) dict[k] = false;

            string[] parts = data.Split(',');
            foreach (var p in parts)
            {
                string trimmed = p.Trim();
                if (dict.ContainsKey(trimmed)) dict[trimmed] = true;
            }
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);
                using (StreamWriter sw = new StreamWriter(_filePath))
                {
                    // General
                    sw.WriteLine($"Language={Language}");

                    // Loot
                    sw.WriteLine($"Loot_Enabled={Loot_Enabled}");
                    sw.WriteLine($"Loot_Delay={Loot_Delay.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Loot_Debug={Loot_Debug}");
                    sw.WriteLine($"ActiveProfiles={string.Join(",", ActiveProfiles)}");

                    // Fish Bot (NOWE)
                    sw.WriteLine($"Fish_Enabled={Fish_Enabled}");
                    sw.WriteLine($"Fish_AutoPress={Fish_AutoPress}");
                    sw.WriteLine($"Fish_ReactionTime={Fish_ReactionTime.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Fish_ShowESP={Fish_ShowESP}");

                    // Misc
                    sw.WriteLine($"Misc_EternalDay={Misc_EternalDay}");
                    sw.WriteLine($"Misc_NoFog={Misc_NoFog}");
                    sw.WriteLine($"Misc_Fullbright={Misc_Fullbright}");
                    sw.WriteLine($"Misc_BrightPlayer={Misc_BrightPlayer}");
                    sw.WriteLine($"Misc_LightIntensity={Misc_LightIntensity.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Misc_LightRange={Misc_LightRange.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Misc_ZoomHack={Misc_ZoomHack}");
                    sw.WriteLine($"Misc_ZoomLimit={Misc_ZoomLimit.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Misc_CamAngle={Misc_CamAngle.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Misc_ZoomSpeed={Misc_ZoomSpeed.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Misc_Fov={Misc_Fov.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Misc_RenderDistance={Misc_RenderDistance.ToString(CultureInfo.InvariantCulture)}");

                    // ESP Global
                    sw.WriteLine($"Esp_Enabled={Esp_Enabled}");
                    sw.WriteLine($"Esp_Distance={Esp_Distance.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Esp_ShowBoxes={Esp_ShowBoxes}");
                    sw.WriteLine($"Esp_ShowXRay={Esp_ShowXRay}");
                    sw.WriteLine($"Esp_ShowResources={Esp_ShowResources}");
                    sw.WriteLine($"Esp_ShowMobs={Esp_ShowMobs}");

                    // ESP Categories
                    sw.WriteLine($"Esp_Cat_Mining={Esp_Cat_Mining}");
                    sw.WriteLine($"Esp_Cat_Gather={Esp_Cat_Gather}");
                    sw.WriteLine($"Esp_Cat_Lumber={Esp_Cat_Lumber}");
                    sw.WriteLine($"Esp_Cat_Godsend={Esp_Cat_Godsend}");
                    sw.WriteLine($"Esp_Cat_Others={Esp_Cat_Others}");
                    sw.WriteLine($"Esp_Mob_Aggro={Esp_Mob_Aggro}");
                    sw.WriteLine($"Esp_Mob_Retal={Esp_Mob_Retal}");
                    sw.WriteLine($"Esp_Mob_Passive={Esp_Mob_Passive}");

                    // ESP Lists
                    sw.WriteLine($"Esp_List_Mining={Esp_List_Mining}");
                    sw.WriteLine($"Esp_List_Gather={Esp_List_Gather}");
                    sw.WriteLine($"Esp_List_Lumber={Esp_List_Lumber}");
                    sw.WriteLine($"Esp_List_Godsend={Esp_List_Godsend}");

                    // Colors
                    sw.WriteLine($"MobAggressive={ColorToString(Colors.MobAggressive)}");
                    sw.WriteLine($"MobPassive={ColorToString(Colors.MobPassive)}");
                    sw.WriteLine($"MobFleeing={ColorToString(Colors.MobFleeing)}");
                    sw.WriteLine($"ResLumber={ColorToString(Colors.ResLumber)}");
                    sw.WriteLine($"ResMining={ColorToString(Colors.ResMining)}");
                    sw.WriteLine($"ResGather={ColorToString(Colors.ResGather)}");

                    // Profiles data
                    foreach (var kvp in LootProfiles)
                    {
                        string items = string.Join(";", kvp.Value.Where(x => !string.IsNullOrEmpty(x)));
                        sw.WriteLine($"Profile:{kvp.Key}={items}");
                    }
                }
            }
            catch (Exception ex) { Debug.LogError($"[Config] Save Error: {ex.Message}"); }
        }

        public static void Load()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                string[] lines = File.ReadAllLines(_filePath);
                bool profilesLoaded = false;

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                    string[] parts = line.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();

                    // --- PARSING ---
                    if (key == "Language") Language = val;

                    // Loot
                    else if (key == "Loot_Enabled") bool.TryParse(val, out Loot_Enabled);
                    else if (key == "Loot_Delay") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Loot_Delay);
                    else if (key == "Loot_Debug") bool.TryParse(val, out Loot_Debug);
                    else if (key == "ActiveProfiles")
                    {
                        ActiveProfiles.Clear();
                        foreach (var p in val.Split(',')) if (!string.IsNullOrEmpty(p)) ActiveProfiles.Add(p);
                    }

                    // Fish Bot (NOWE)
                    else if (key == "Fish_Enabled") bool.TryParse(val, out Fish_Enabled);
                    else if (key == "Fish_AutoPress") bool.TryParse(val, out Fish_AutoPress);
                    else if (key == "Fish_ReactionTime") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Fish_ReactionTime);
                    else if (key == "Fish_ShowESP") bool.TryParse(val, out Fish_ShowESP);

                    // Misc
                    else if (key == "Misc_EternalDay") bool.TryParse(val, out Misc_EternalDay);
                    else if (key == "Misc_NoFog") bool.TryParse(val, out Misc_NoFog);
                    else if (key == "Misc_Fullbright") bool.TryParse(val, out Misc_Fullbright);
                    else if (key == "Misc_BrightPlayer") bool.TryParse(val, out Misc_BrightPlayer);
                    else if (key == "Misc_LightIntensity") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Misc_LightIntensity);
                    else if (key == "Misc_LightRange") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Misc_LightRange);
                    else if (key == "Misc_ZoomHack") bool.TryParse(val, out Misc_ZoomHack);
                    else if (key == "Misc_ZoomLimit") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Misc_ZoomLimit);
                    else if (key == "Misc_CamAngle") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Misc_CamAngle);
                    else if (key == "Misc_ZoomSpeed") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Misc_ZoomSpeed);
                    else if (key == "Misc_Fov") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Misc_Fov);
                    else if (key == "Misc_RenderDistance") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Misc_RenderDistance);

                    // ESP
                    else if (key == "Esp_Enabled") bool.TryParse(val, out Esp_Enabled);
                    else if (key == "Esp_Distance") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Esp_Distance);
                    else if (key == "Esp_ShowBoxes") bool.TryParse(val, out Esp_ShowBoxes);
                    else if (key == "Esp_ShowXRay") bool.TryParse(val, out Esp_ShowXRay);
                    else if (key == "Esp_ShowResources") bool.TryParse(val, out Esp_ShowResources);
                    else if (key == "Esp_ShowMobs") bool.TryParse(val, out Esp_ShowMobs);

                    else if (key == "Esp_Cat_Mining") bool.TryParse(val, out Esp_Cat_Mining);
                    else if (key == "Esp_Cat_Gather") bool.TryParse(val, out Esp_Cat_Gather);
                    else if (key == "Esp_Cat_Lumber") bool.TryParse(val, out Esp_Cat_Lumber);
                    else if (key == "Esp_Cat_Godsend") bool.TryParse(val, out Esp_Cat_Godsend);
                    else if (key == "Esp_Cat_Others") bool.TryParse(val, out Esp_Cat_Others);

                    else if (key == "Esp_Mob_Aggro") bool.TryParse(val, out Esp_Mob_Aggro);
                    else if (key == "Esp_Mob_Retal") bool.TryParse(val, out Esp_Mob_Retal);
                    else if (key == "Esp_Mob_Passive") bool.TryParse(val, out Esp_Mob_Passive);

                    else if (key == "Esp_List_Mining") Esp_List_Mining = val;
                    else if (key == "Esp_List_Gather") Esp_List_Gather = val;
                    else if (key == "Esp_List_Lumber") Esp_List_Lumber = val;
                    else if (key == "Esp_List_Godsend") Esp_List_Godsend = val;

                    // Colors
                    else if (key == "MobAggressive") Colors.MobAggressive = StringToColor(val);
                    else if (key == "MobPassive") Colors.MobPassive = StringToColor(val);
                    else if (key == "MobFleeing") Colors.MobFleeing = StringToColor(val);
                    else if (key == "ResLumber") Colors.ResLumber = StringToColor(val);
                    else if (key == "ResMining") Colors.ResMining = StringToColor(val);
                    else if (key == "ResGather") Colors.ResGather = StringToColor(val);

                    // Profiles
                    else if (key.StartsWith("Profile:"))
                    {
                        if (!profilesLoaded) { LootProfiles.Clear(); profilesLoaded = true; }
                        string profileName = key.Substring(8);
                        List<string> items = new List<string>();
                        if (!string.IsNullOrEmpty(val)) items.AddRange(val.Split(';'));
                        LootProfiles[profileName] = items;
                    }
                }

                if (LootProfiles.Count == 0) LootProfiles["Default"] = new List<string>();
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