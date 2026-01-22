using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

namespace WildTerraHook
{
    public static class ConfigManager
    {
        // --- USTAWIENIA OKNA ---
        public static float Menu_X = 100;
        public static float Menu_Y = 100;
        public static float Menu_W = 500;
        public static float Menu_H = 400;
        public static int Menu_Tab = 0;
        public static float Menu_Scale = 1.0f;
        public static string Language = "pl"; // Domyślnie PL

        // --- ESP ---
        public static bool Esp_Enabled = true;
        public static float Esp_Distance = 100f;
        public static float Esp_RefreshRate = 60f; // NOWE POLE
        public static bool Esp_ShowResources = true;
        public static bool Esp_ShowMobs = true;
        public static bool Esp_ShowBoxes = false;
        public static bool Esp_ShowXRay = false;

        public static bool Esp_Cat_Mining = true;
        public static bool Esp_Cat_Gather = true;
        public static bool Esp_Cat_Lumber = true;
        public static bool Esp_Cat_Godsend = true;
        public static bool Esp_Cat_Others = false;

        public static bool Esp_Mob_Aggro = true;
        public static bool Esp_Mob_Retal = true;
        public static bool Esp_Mob_Passive = true;

        // Listy ESP (serialized)
        public static string Esp_List_Mining = "";
        public static string Esp_List_Gather = "";
        public static string Esp_List_Lumber = "";
        public static string Esp_List_Godsend = "";

        // --- AUTO LOOT ---
        public static bool Loot_Enabled = false;
        public static bool Loot_Debug = false;
        public static float Loot_Delay = 0.3f;
        public static Dictionary<string, List<string>> LootProfiles = new Dictionary<string, List<string>> { { "Default", new List<string>() } };
        public static HashSet<string> ActiveProfiles = new HashSet<string> { "Default" };

        // --- AUTO DROP ---
        public static bool Drop_Enabled = false;
        public static bool Drop_Debug = false;
        public static float Drop_Delay = 0.5f;
        public static Dictionary<string, List<string>> DropProfiles = new Dictionary<string, List<string>> { { "Default", new List<string>() } };
        public static HashSet<string> ActiveDropProfiles = new HashSet<string> { "Default" };

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
        public static float Misc_RenderDistance = 1000f;

        // --- FISHING ---
        public static bool ColorFish_Enabled = false;
        public static bool ColorFish_ShowESP = true;
        public static float ColorFish_Timeout = 25f;

        // --- FISHING MEMORY ---
        public static bool MemFish_Enabled = false; // Domyślnie wyłączony
        public static bool MemFish_ShowESP = true;
        public static bool MemFish_AutoPress = false;
        public static float MemFish_ReactionTime = 0.25f;

        // --- COLORS ---
        public static class Colors
        {
            public static Color MobAggressive = Color.red;
            public static Color MobPassive = Color.green;
            public static Color MobFleeing = Color.yellow;
            public static Color ResMining = Color.cyan;
            public static Color ResGather = Color.magenta;
            public static Color ResLumber = new Color(0.6f, 0.3f, 0f);
        }

        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook", "config.json");

        public static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Menu_X=" + Menu_X);
                sb.AppendLine("Menu_Y=" + Menu_Y);
                sb.AppendLine("Menu_W=" + Menu_W);
                sb.AppendLine("Menu_H=" + Menu_H);
                sb.AppendLine("Menu_Tab=" + Menu_Tab);
                sb.AppendLine("Menu_Scale=" + Menu_Scale);
                sb.AppendLine("Language=" + Language);

                sb.AppendLine("Esp_Enabled=" + Esp_Enabled);
                sb.AppendLine("Esp_Distance=" + Esp_Distance);
                sb.AppendLine("Esp_RefreshRate=" + Esp_RefreshRate); // SAVE
                sb.AppendLine("Esp_ShowResources=" + Esp_ShowResources);
                sb.AppendLine("Esp_ShowMobs=" + Esp_ShowMobs);
                sb.AppendLine("Esp_ShowBoxes=" + Esp_ShowBoxes);
                sb.AppendLine("Esp_ShowXRay=" + Esp_ShowXRay);

                sb.AppendLine("Esp_Cat_Mining=" + Esp_Cat_Mining);
                sb.AppendLine("Esp_Cat_Gather=" + Esp_Cat_Gather);
                sb.AppendLine("Esp_Cat_Lumber=" + Esp_Cat_Lumber);
                sb.AppendLine("Esp_Cat_Godsend=" + Esp_Cat_Godsend);
                sb.AppendLine("Esp_Cat_Others=" + Esp_Cat_Others);

                sb.AppendLine("Esp_Mob_Aggro=" + Esp_Mob_Aggro);
                sb.AppendLine("Esp_Mob_Retal=" + Esp_Mob_Retal);
                sb.AppendLine("Esp_Mob_Passive=" + Esp_Mob_Passive);

                sb.AppendLine("Esp_List_Mining=" + Esp_List_Mining);
                sb.AppendLine("Esp_List_Gather=" + Esp_List_Gather);
                sb.AppendLine("Esp_List_Lumber=" + Esp_List_Lumber);
                sb.AppendLine("Esp_List_Godsend=" + Esp_List_Godsend);

                sb.AppendLine("Loot_Enabled=" + Loot_Enabled);
                sb.AppendLine("Loot_Debug=" + Loot_Debug);
                sb.AppendLine("Loot_Delay=" + Loot_Delay);
                sb.AppendLine("Loot_Active=" + string.Join("|", ActiveProfiles));
                foreach (var kvp in LootProfiles) sb.AppendLine($"Loot_Profile_{kvp.Key}=" + string.Join("|", kvp.Value));

                sb.AppendLine("Drop_Enabled=" + Drop_Enabled);
                sb.AppendLine("Drop_Debug=" + Drop_Debug);
                sb.AppendLine("Drop_Delay=" + Drop_Delay);
                sb.AppendLine("Drop_Active=" + string.Join("|", ActiveDropProfiles));
                foreach (var kvp in DropProfiles) sb.AppendLine($"Drop_Profile_{kvp.Key}=" + string.Join("|", kvp.Value));

                sb.AppendLine("Misc_EternalDay=" + Misc_EternalDay);
                sb.AppendLine("Misc_NoFog=" + Misc_NoFog);
                sb.AppendLine("Misc_Fullbright=" + Misc_Fullbright);
                sb.AppendLine("Misc_BrightPlayer=" + Misc_BrightPlayer);
                sb.AppendLine("Misc_LightIntensity=" + Misc_LightIntensity);
                sb.AppendLine("Misc_LightRange=" + Misc_LightRange);
                sb.AppendLine("Misc_ZoomHack=" + Misc_ZoomHack);
                sb.AppendLine("Misc_ZoomLimit=" + Misc_ZoomLimit);
                sb.AppendLine("Misc_CamAngle=" + Misc_CamAngle);
                sb.AppendLine("Misc_ZoomSpeed=" + Misc_ZoomSpeed);
                sb.AppendLine("Misc_Fov=" + Misc_Fov);
                sb.AppendLine("Misc_RenderDistance=" + Misc_RenderDistance);

                sb.AppendLine("ColorFish_Enabled=" + ColorFish_Enabled);
                sb.AppendLine("ColorFish_ShowESP=" + ColorFish_ShowESP);
                sb.AppendLine("ColorFish_Timeout=" + ColorFish_Timeout);

                sb.AppendLine("MemFish_Enabled=" + MemFish_Enabled);
                sb.AppendLine("MemFish_ShowESP=" + MemFish_ShowESP);
                sb.AppendLine("MemFish_AutoPress=" + MemFish_AutoPress);
                sb.AppendLine("MemFish_ReactionTime=" + MemFish_ReactionTime);

                sb.AppendLine("Col_MobAggro=" + ColorToHex(Colors.MobAggressive));
                sb.AppendLine("Col_MobPassive=" + ColorToHex(Colors.MobPassive));
                sb.AppendLine("Col_MobFlee=" + ColorToHex(Colors.MobFleeing));
                sb.AppendLine("Col_ResMine=" + ColorToHex(Colors.ResMining));
                sb.AppendLine("Col_ResGather=" + ColorToHex(Colors.ResGather));
                sb.AppendLine("Col_ResLumb=" + ColorToHex(Colors.ResLumber));

                File.WriteAllText(ConfigPath, sb.ToString());
            }
            catch { }
        }

        public static void Load()
        {
            if (!File.Exists(ConfigPath)) return;
            try
            {
                var lines = File.ReadAllLines(ConfigPath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                    var parts = line.Split(new[] { '=' }, 2);
                    string k = parts[0].Trim();
                    string v = parts[1].Trim();

                    if (k == "Menu_X") float.TryParse(v, out Menu_X);
                    else if (k == "Menu_Y") float.TryParse(v, out Menu_Y);
                    else if (k == "Menu_W") float.TryParse(v, out Menu_W);
                    else if (k == "Menu_H") float.TryParse(v, out Menu_H);
                    else if (k == "Menu_Tab") int.TryParse(v, out Menu_Tab);
                    else if (k == "Menu_Scale") float.TryParse(v, out Menu_Scale);
                    else if (k == "Language") Language = v;

                    else if (k == "Esp_Enabled") bool.TryParse(v, out Esp_Enabled);
                    else if (k == "Esp_Distance") float.TryParse(v, out Esp_Distance);
                    else if (k == "Esp_RefreshRate") float.TryParse(v, out Esp_RefreshRate); // LOAD
                    else if (k == "Esp_ShowResources") bool.TryParse(v, out Esp_ShowResources);
                    else if (k == "Esp_ShowMobs") bool.TryParse(v, out Esp_ShowMobs);
                    else if (k == "Esp_ShowBoxes") bool.TryParse(v, out Esp_ShowBoxes);
                    else if (k == "Esp_ShowXRay") bool.TryParse(v, out Esp_ShowXRay);

                    else if (k == "Esp_Cat_Mining") bool.TryParse(v, out Esp_Cat_Mining);
                    else if (k == "Esp_Cat_Gather") bool.TryParse(v, out Esp_Cat_Gather);
                    else if (k == "Esp_Cat_Lumber") bool.TryParse(v, out Esp_Cat_Lumber);
                    else if (k == "Esp_Cat_Godsend") bool.TryParse(v, out Esp_Cat_Godsend);
                    else if (k == "Esp_Cat_Others") bool.TryParse(v, out Esp_Cat_Others);

                    else if (k == "Esp_Mob_Aggro") bool.TryParse(v, out Esp_Mob_Aggro);
                    else if (k == "Esp_Mob_Retal") bool.TryParse(v, out Esp_Mob_Retal);
                    else if (k == "Esp_Mob_Passive") bool.TryParse(v, out Esp_Mob_Passive);

                    else if (k == "Esp_List_Mining") Esp_List_Mining = v;
                    else if (k == "Esp_List_Gather") Esp_List_Gather = v;
                    else if (k == "Esp_List_Lumber") Esp_List_Lumber = v;
                    else if (k == "Esp_List_Godsend") Esp_List_Godsend = v;

                    else if (k == "Loot_Enabled") bool.TryParse(v, out Loot_Enabled);
                    else if (k == "Loot_Debug") bool.TryParse(v, out Loot_Debug);
                    else if (k == "Loot_Delay") float.TryParse(v, out Loot_Delay);
                    else if (k == "Loot_Active") { ActiveProfiles.Clear(); foreach (var p in v.Split('|')) if (!string.IsNullOrEmpty(p)) ActiveProfiles.Add(p); }
                    else if (k.StartsWith("Loot_Profile_"))
                    {
                        string pName = k.Replace("Loot_Profile_", "");
                        var list = new List<string>();
                        foreach (var i in v.Split('|')) if (!string.IsNullOrEmpty(i)) list.Add(i);
                        LootProfiles[pName] = list;
                    }

                    else if (k == "Drop_Enabled") bool.TryParse(v, out Drop_Enabled);
                    else if (k == "Drop_Debug") bool.TryParse(v, out Drop_Debug);
                    else if (k == "Drop_Delay") float.TryParse(v, out Drop_Delay);
                    else if (k == "Drop_Active") { ActiveDropProfiles.Clear(); foreach (var p in v.Split('|')) if (!string.IsNullOrEmpty(p)) ActiveDropProfiles.Add(p); }
                    else if (k.StartsWith("Drop_Profile_"))
                    {
                        string pName = k.Replace("Drop_Profile_", "");
                        var list = new List<string>();
                        foreach (var i in v.Split('|')) if (!string.IsNullOrEmpty(i)) list.Add(i);
                        DropProfiles[pName] = list;
                    }

                    else if (k == "Misc_EternalDay") bool.TryParse(v, out Misc_EternalDay);
                    else if (k == "Misc_NoFog") bool.TryParse(v, out Misc_NoFog);
                    else if (k == "Misc_Fullbright") bool.TryParse(v, out Misc_Fullbright);
                    else if (k == "Misc_BrightPlayer") bool.TryParse(v, out Misc_BrightPlayer);
                    else if (k == "Misc_LightIntensity") float.TryParse(v, out Misc_LightIntensity);
                    else if (k == "Misc_LightRange") float.TryParse(v, out Misc_LightRange);
                    else if (k == "Misc_ZoomHack") bool.TryParse(v, out Misc_ZoomHack);
                    else if (k == "Misc_ZoomLimit") float.TryParse(v, out Misc_ZoomLimit);
                    else if (k == "Misc_CamAngle") float.TryParse(v, out Misc_CamAngle);
                    else if (k == "Misc_ZoomSpeed") float.TryParse(v, out Misc_ZoomSpeed);
                    else if (k == "Misc_Fov") float.TryParse(v, out Misc_Fov);
                    else if (k == "Misc_RenderDistance") float.TryParse(v, out Misc_RenderDistance);

                    else if (k == "ColorFish_Enabled") bool.TryParse(v, out ColorFish_Enabled);
                    else if (k == "ColorFish_ShowESP") bool.TryParse(v, out ColorFish_ShowESP);
                    else if (k == "ColorFish_Timeout") float.TryParse(v, out ColorFish_Timeout);

                    else if (k == "MemFish_Enabled") bool.TryParse(v, out MemFish_Enabled);
                    else if (k == "MemFish_ShowESP") bool.TryParse(v, out MemFish_ShowESP);
                    else if (k == "MemFish_AutoPress") bool.TryParse(v, out MemFish_AutoPress);
                    else if (k == "MemFish_ReactionTime") float.TryParse(v, out MemFish_ReactionTime);

                    else if (k == "Col_MobAggro") Colors.MobAggressive = HexToColor(v);
                    else if (k == "Col_MobPassive") Colors.MobPassive = HexToColor(v);
                    else if (k == "Col_MobFlee") Colors.MobFleeing = HexToColor(v);
                    else if (k == "Col_ResMine") Colors.ResMining = HexToColor(v);
                    else if (k == "Col_ResGather") Colors.ResGather = HexToColor(v);
                    else if (k == "Col_ResLumb") Colors.ResLumber = HexToColor(v);
                }
            }
            catch { }
        }

        public static List<string> GetCombinedActiveList()
        {
            List<string> combined = new List<string>();
            foreach (var profName in ActiveProfiles)
            {
                if (LootProfiles.ContainsKey(profName)) combined.AddRange(LootProfiles[profName]);
            }
            return combined;
        }

        public static List<string> GetCombinedActiveDropList()
        {
            List<string> combined = new List<string>();
            foreach (var profName in ActiveDropProfiles)
            {
                if (DropProfiles.ContainsKey(profName)) combined.AddRange(DropProfiles[profName]);
            }
            return combined;
        }

        public static string SerializeToggleList(Dictionary<string, bool> dict)
        {
            List<string> active = new List<string>();
            foreach (var kvp in dict) if (kvp.Value) active.Add(kvp.Key);
            return string.Join("|", active);
        }

        public static void DeserializeToggleList(string data, Dictionary<string, bool> dict)
        {
            if (string.IsNullOrEmpty(data)) return;
            var parts = data.Split('|');
            var keys = new List<string>(dict.Keys);
            foreach (var key in keys) dict[key] = false;
            foreach (var p in parts) if (dict.ContainsKey(p)) dict[p] = true;
        }

        private static string ColorToHex(Color c)
        {
            return ColorUtility.ToHtmlStringRGBA(c);
        }

        private static Color HexToColor(string hex)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString("#" + hex, out c)) return c;
            return Color.white;
        }
    }
}