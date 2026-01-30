using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using UnityEngine;
using System.Text;

namespace WildTerraHook
{
    public static class ConfigManager
    {
        private static string _folderPath;
        private static string _filePath;

        public static string Language = "en";

        // --- COLORS ---
        public static class Colors
        {
            public static Color MobAggressive = Color.red;
            public static Color MobPassive = Color.green;
            public static Color MobFleeing = new Color(1f, 0.64f, 0f);
            public static Color ResLumber = new Color(0.6f, 0.4f, 0.2f);
            public static Color ResMining = Color.gray;
            public static Color ResGather = Color.white;
            public static Color ResGodsend = Color.blue;
            public static Color ResDungeon = Color.magenta;
        }

        // --- PERSISTENT WORLD ---
        public static bool Persistent_Enabled = true;
        public static float Persistent_CleanupRange = 20.0f;

        // --- COMBAT ---
        public static bool Combat_NoCooldown = false;
        public static bool Combat_FastAttack = false;
        public static float Combat_AttackSpeed = 1.5f;

        // --- AUTO HEAL ---
        public static bool Heal_Enabled = false;
        public static string Heal_ItemName = "Linen bandage";
        public static int Heal_Percent = 60;
        public static bool Heal_CombatOnly = true;
        public static float Heal_Cooldown = 20.0f;

        // --- QUICK STACK (Nowe) ---
        public static bool QuickStack_Enabled = true;
        public static float QuickStack_Delay = 0.2f;

        // --- AUTO LOOT ---
        public static Dictionary<string, List<string>> LootProfiles = new Dictionary<string, List<string>>();
        public static HashSet<string> ActiveProfiles = new HashSet<string>();
        public static bool Loot_Enabled = false;
        public static float Loot_Delay = 0.2f;
        public static bool Loot_Debug = false;

        // --- AUTO DROP ---
        public static Dictionary<string, List<string>> DropProfiles = new Dictionary<string, List<string>>();
        public static HashSet<string> ActiveDropProfiles = new HashSet<string>();
        public static bool Drop_Enabled = false;
        public static float Drop_Delay = 0.5f;
        public static bool Drop_Debug = false;
        public static string Drop_OverrideMethod = "";

        // --- BOTS ---
        public static bool ColorFish_Enabled = false;
        public static bool ColorFish_AutoPress = false;
        public static float ColorFish_ReactionTime = 0.3f;
        public static float ColorFish_Timeout = 25.0f;
        public static bool ColorFish_ShowESP = true;

        public static bool MemFish_Enabled = false;
        public static bool MemFish_AutoPress = false;
        public static float MemFish_ReactionTime = 0.3f;
        public static bool MemFish_ShowESP = true;

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
        public static bool Misc_AutoButcher = false;

        // --- ESP ---
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
        public static bool Esp_Cat_Dungeons = false;
        public static bool Esp_Cat_Others = false;
        public static bool Esp_Mob_Aggro = false;
        public static bool Esp_Mob_Retal = false;
        public static bool Esp_Mob_Passive = false;

        public static string Esp_List_Mining = "";
        public static string Esp_List_Gather = "";
        public static string Esp_List_Lumber = "";
        public static string Esp_List_Godsend = "";
        public static string Esp_List_Dungeons = "";

        // --- CONSOLE ---
        public static bool Console_AutoScroll = true;
        public static bool Console_ShowInfo = true;
        public static bool Console_ShowWarnings = true;
        public static bool Console_ShowErrors = true;

        // --- MENU & SIZES ---
        public static float Menu_Scale = 1.0f;
        public static float Menu_X = 20f;
        public static float Menu_Y = 20f;
        public static float Menu_W = 350f;
        public static float Menu_H = 300f;
        public static int Menu_Tab = 0;

        // Tablice rozmiarów (0-9)
        public static float[] TabWidths = new float[10];
        public static float[] TabHeights = new float[10];

        static ConfigManager()
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook");
            _filePath = Path.Combine(_folderPath, "config.txt");

            // Init defaults
            for (int i = 0; i < 10; i++) { TabWidths[i] = 400f; TabHeights[i] = 300f; }

            if (LootProfiles.Count == 0) { LootProfiles["Default"] = new List<string>(); ActiveProfiles.Add("Default"); }
            if (DropProfiles.Count == 0) { DropProfiles["Default"] = new List<string>(); ActiveDropProfiles.Add("Default"); }

            Load();
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
                    sw.WriteLine($"Language={Language}");
                    sw.WriteLine($"Menu_Scale={Menu_Scale.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Menu_Tab={Menu_Tab}");
                    sw.WriteLine($"Menu_Rect={Menu_X.ToString(CultureInfo.InvariantCulture)},{Menu_Y.ToString(CultureInfo.InvariantCulture)},{Menu_W.ToString(CultureInfo.InvariantCulture)},{Menu_H.ToString(CultureInfo.InvariantCulture)}");

                    // Tab Sizes
                    string wStr = string.Join(",", TabWidths.Select(f => f.ToString(CultureInfo.InvariantCulture)));
                    string hStr = string.Join(",", TabHeights.Select(f => f.ToString(CultureInfo.InvariantCulture)));
                    sw.WriteLine($"TabWidths={wStr}");
                    sw.WriteLine($"TabHeights={hStr}");

                    // Persistent
                    sw.WriteLine($"Persistent_Enabled={Persistent_Enabled}");
                    sw.WriteLine($"Persistent_CleanupRange={Persistent_CleanupRange.ToString(CultureInfo.InvariantCulture)}");

                    // Combat
                    sw.WriteLine($"Combat_NoCooldown={Combat_NoCooldown}");
                    sw.WriteLine($"Combat_FastAttack={Combat_FastAttack}");
                    sw.WriteLine($"Combat_AttackSpeed={Combat_AttackSpeed.ToString(CultureInfo.InvariantCulture)}");

                    // Heal
                    sw.WriteLine($"Heal_Enabled={Heal_Enabled}");
                    sw.WriteLine($"Heal_ItemName={Heal_ItemName}");
                    sw.WriteLine($"Heal_Percent={Heal_Percent}");
                    sw.WriteLine($"Heal_CombatOnly={Heal_CombatOnly}");
                    sw.WriteLine($"Heal_Cooldown={Heal_Cooldown.ToString(CultureInfo.InvariantCulture)}");

                    // Quick Stack
                    sw.WriteLine($"QuickStack_Enabled={QuickStack_Enabled}");
                    sw.WriteLine($"QuickStack_Delay={QuickStack_Delay.ToString(CultureInfo.InvariantCulture)}");

                    // Loot
                    sw.WriteLine($"Loot_Enabled={Loot_Enabled}");
                    sw.WriteLine($"Loot_Delay={Loot_Delay.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Loot_Debug={Loot_Debug}");
                    sw.WriteLine($"ActiveProfiles={string.Join(",", ActiveProfiles)}");

                    // Drop
                    sw.WriteLine($"Drop_Enabled={Drop_Enabled}");
                    sw.WriteLine($"Drop_Delay={Drop_Delay.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Drop_Debug={Drop_Debug}");
                    sw.WriteLine($"Drop_OverrideMethod={Drop_OverrideMethod}");
                    sw.WriteLine($"ActiveDropProfiles={string.Join(",", ActiveDropProfiles)}");

                    // Bots
                    sw.WriteLine($"ColorFish_Enabled={ColorFish_Enabled}");
                    sw.WriteLine($"ColorFish_AutoPress={ColorFish_AutoPress}");
                    sw.WriteLine($"ColorFish_ReactionTime={ColorFish_ReactionTime.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"ColorFish_Timeout={ColorFish_Timeout.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"ColorFish_ShowESP={ColorFish_ShowESP}");

                    sw.WriteLine($"MemFish_Enabled={MemFish_Enabled}");
                    sw.WriteLine($"MemFish_AutoPress={MemFish_AutoPress}");
                    sw.WriteLine($"MemFish_ReactionTime={MemFish_ReactionTime.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"MemFish_ShowESP={MemFish_ShowESP}");

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
                    sw.WriteLine($"Misc_AutoButcher={Misc_AutoButcher}");

                    // ESP
                    sw.WriteLine($"Esp_Enabled={Esp_Enabled}");
                    sw.WriteLine($"Esp_Distance={Esp_Distance.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"Esp_ShowBoxes={Esp_ShowBoxes}");
                    sw.WriteLine($"Esp_ShowXRay={Esp_ShowXRay}");
                    sw.WriteLine($"Esp_ShowResources={Esp_ShowResources}");
                    sw.WriteLine($"Esp_ShowMobs={Esp_ShowMobs}");
                    sw.WriteLine($"Esp_Cat_Mining={Esp_Cat_Mining}");
                    sw.WriteLine($"Esp_Cat_Gather={Esp_Cat_Gather}");
                    sw.WriteLine($"Esp_Cat_Lumber={Esp_Cat_Lumber}");
                    sw.WriteLine($"Esp_Cat_Godsend={Esp_Cat_Godsend}");
                    sw.WriteLine($"Esp_Cat_Dungeons={Esp_Cat_Dungeons}");
                    sw.WriteLine($"Esp_Cat_Others={Esp_Cat_Others}");
                    sw.WriteLine($"Esp_Mob_Aggro={Esp_Mob_Aggro}");
                    sw.WriteLine($"Esp_Mob_Retal={Esp_Mob_Retal}");
                    sw.WriteLine($"Esp_Mob_Passive={Esp_Mob_Passive}");
                    sw.WriteLine($"Esp_List_Mining={Esp_List_Mining}");
                    sw.WriteLine($"Esp_List_Gather={Esp_List_Gather}");
                    sw.WriteLine($"Esp_List_Lumber={Esp_List_Lumber}");
                    sw.WriteLine($"Esp_List_Godsend={Esp_List_Godsend}");
                    sw.WriteLine($"Esp_List_Dungeons={Esp_List_Dungeons}");

                    sw.WriteLine($"MobAggressive={ColorToString(Colors.MobAggressive)}");
                    sw.WriteLine($"MobPassive={ColorToString(Colors.MobPassive)}");
                    sw.WriteLine($"MobFleeing={ColorToString(Colors.MobFleeing)}");
                    sw.WriteLine($"ResLumber={ColorToString(Colors.ResLumber)}");
                    sw.WriteLine($"ResMining={ColorToString(Colors.ResMining)}");
                    sw.WriteLine($"ResGather={ColorToString(Colors.ResGather)}");
                    sw.WriteLine($"ResGodsend={ColorToString(Colors.ResGodsend)}");
                    sw.WriteLine($"ResDungeon={ColorToString(Colors.ResDungeon)}");

                    sw.WriteLine($"Console_AutoScroll={Console_AutoScroll}");
                    sw.WriteLine($"Console_ShowInfo={Console_ShowInfo}");
                    sw.WriteLine($"Console_ShowWarnings={Console_ShowWarnings}");
                    sw.WriteLine($"Console_ShowErrors={Console_ShowErrors}");

                    foreach (var kvp in LootProfiles)
                    {
                        string items = string.Join(";", kvp.Value.Where(x => !string.IsNullOrEmpty(x)));
                        sw.WriteLine($"Profile:{kvp.Key}={items}");
                    }
                    foreach (var kvp in DropProfiles)
                    {
                        string items = string.Join(";", kvp.Value.Where(x => !string.IsNullOrEmpty(x)));
                        sw.WriteLine($"DropProfile:{kvp.Key}={items}");
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
                bool dropProfilesLoaded = false;

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                    string[] parts = line.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();

                    if (key == "Language") Language = val;
                    else if (key == "Persistent_Enabled") bool.TryParse(val, out Persistent_Enabled);
                    else if (key == "Persistent_CleanupRange") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Persistent_CleanupRange);

                    // Tab Sizes
                    else if (key == "TabWidths")
                    {
                        string[] split = val.Split(',');
                        for (int i = 0; i < split.Length && i < 10; i++)
                            float.TryParse(split[i], NumberStyles.Any, CultureInfo.InvariantCulture, out TabWidths[i]);
                    }
                    else if (key == "TabHeights")
                    {
                        string[] split = val.Split(',');
                        for (int i = 0; i < split.Length && i < 10; i++)
                            float.TryParse(split[i], NumberStyles.Any, CultureInfo.InvariantCulture, out TabHeights[i]);
                    }

                    // Combat
                    else if (key == "Combat_NoCooldown") bool.TryParse(val, out Combat_NoCooldown);
                    else if (key == "Combat_FastAttack") bool.TryParse(val, out Combat_FastAttack);
                    else if (key == "Combat_AttackSpeed") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Combat_AttackSpeed);

                    // Heal
                    else if (key == "Heal_Enabled") bool.TryParse(val, out Heal_Enabled);
                    else if (key == "Heal_ItemName") Heal_ItemName = val;
                    else if (key == "Heal_Percent") int.TryParse(val, out Heal_Percent);
                    else if (key == "Heal_CombatOnly") bool.TryParse(val, out Heal_CombatOnly);
                    else if (key == "Heal_Cooldown") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Heal_Cooldown);

                    // Quick Stack
                    else if (key == "QuickStack_Enabled") bool.TryParse(val, out QuickStack_Enabled);
                    else if (key == "QuickStack_Delay") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out QuickStack_Delay);

                    // Loot
                    else if (key == "Loot_Enabled") bool.TryParse(val, out Loot_Enabled);
                    else if (key == "Loot_Delay") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Loot_Delay);
                    else if (key == "Loot_Debug") bool.TryParse(val, out Loot_Debug);
                    else if (key == "ActiveProfiles")
                    {
                        ActiveProfiles.Clear();
                        foreach (var p in val.Split(',')) if (!string.IsNullOrEmpty(p)) ActiveProfiles.Add(p);
                    }

                    // Drop
                    else if (key == "Drop_Enabled") bool.TryParse(val, out Drop_Enabled);
                    else if (key == "Drop_Delay") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Drop_Delay);
                    else if (key == "Drop_Debug") bool.TryParse(val, out Drop_Debug);
                    else if (key == "Drop_OverrideMethod") Drop_OverrideMethod = val;
                    else if (key == "ActiveDropProfiles")
                    {
                        ActiveDropProfiles.Clear();
                        foreach (var p in val.Split(',')) if (!string.IsNullOrEmpty(p)) ActiveDropProfiles.Add(p);
                    }

                    // Bots
                    else if (key == "ColorFish_Enabled") bool.TryParse(val, out ColorFish_Enabled);
                    else if (key == "ColorFish_AutoPress") bool.TryParse(val, out ColorFish_AutoPress);
                    else if (key == "ColorFish_ReactionTime") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out ColorFish_ReactionTime);
                    else if (key == "ColorFish_Timeout") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out ColorFish_Timeout);
                    else if (key == "ColorFish_ShowESP") bool.TryParse(val, out ColorFish_ShowESP);
                    else if (key == "MemFish_Enabled") bool.TryParse(val, out MemFish_Enabled);
                    else if (key == "MemFish_AutoPress") bool.TryParse(val, out MemFish_AutoPress);
                    else if (key == "MemFish_ReactionTime") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out MemFish_ReactionTime);
                    else if (key == "MemFish_ShowESP") bool.TryParse(val, out MemFish_ShowESP);

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
                    else if (key == "Misc_AutoButcher") bool.TryParse(val, out Misc_AutoButcher);

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
                    else if (key == "Esp_Cat_Dungeons") bool.TryParse(val, out Esp_Cat_Dungeons);
                    else if (key == "Esp_Cat_Others") bool.TryParse(val, out Esp_Cat_Others);
                    else if (key == "Esp_Mob_Aggro") bool.TryParse(val, out Esp_Mob_Aggro);
                    else if (key == "Esp_Mob_Retal") bool.TryParse(val, out Esp_Mob_Retal);
                    else if (key == "Esp_Mob_Passive") bool.TryParse(val, out Esp_Mob_Passive);
                    else if (key == "Esp_List_Mining") Esp_List_Mining = val;
                    else if (key == "Esp_List_Gather") Esp_List_Gather = val;
                    else if (key == "Esp_List_Lumber") Esp_List_Lumber = val;
                    else if (key == "Esp_List_Godsend") Esp_List_Godsend = val;
                    else if (key == "Esp_List_Dungeons") Esp_List_Dungeons = val;
                    else if (key == "MobAggressive") Colors.MobAggressive = StringToColor(val);
                    else if (key == "MobPassive") Colors.MobPassive = StringToColor(val);
                    else if (key == "MobFleeing") Colors.MobFleeing = StringToColor(val);
                    else if (key == "ResLumber") Colors.ResLumber = StringToColor(val);
                    else if (key == "ResMining") Colors.ResMining = StringToColor(val);
                    else if (key == "ResGather") Colors.ResGather = StringToColor(val);
                    else if (key == "ResGodsend") Colors.ResGodsend = StringToColor(val);
                    else if (key == "ResDungeon") Colors.ResDungeon = StringToColor(val);
                    else if (key == "Console_AutoScroll") bool.TryParse(val, out Console_AutoScroll);
                    else if (key == "Console_ShowInfo") bool.TryParse(val, out Console_ShowInfo);
                    else if (key == "Console_ShowWarnings") bool.TryParse(val, out Console_ShowWarnings);
                    else if (key == "Console_ShowErrors") bool.TryParse(val, out Console_ShowErrors);

                    // Other
                    else if (key == "Menu_Scale") float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out Menu_Scale);
                    else if (key == "Menu_Tab") int.TryParse(val, out Menu_Tab);
                    else if (key == "Menu_Rect")
                    {
                        string[] r = val.Split(',');
                        if (r.Length == 4)
                        {
                            float.TryParse(r[0], NumberStyles.Any, CultureInfo.InvariantCulture, out Menu_X);
                            float.TryParse(r[1], NumberStyles.Any, CultureInfo.InvariantCulture, out Menu_Y);
                            float.TryParse(r[2], NumberStyles.Any, CultureInfo.InvariantCulture, out Menu_W);
                            float.TryParse(r[3], NumberStyles.Any, CultureInfo.InvariantCulture, out Menu_H);
                        }
                    }
                    else if (key.StartsWith("Profile:"))
                    {
                        if (!profilesLoaded) { LootProfiles.Clear(); profilesLoaded = true; }
                        string profileName = key.Substring(8);
                        List<string> items = new List<string>();
                        if (!string.IsNullOrEmpty(val)) items.AddRange(val.Split(';'));
                        LootProfiles[profileName] = items;
                    }
                    else if (key.StartsWith("DropProfile:"))
                    {
                        if (!dropProfilesLoaded) { DropProfiles.Clear(); dropProfilesLoaded = true; }
                        string profileName = key.Substring(12);
                        List<string> items = new List<string>();
                        if (!string.IsNullOrEmpty(val)) items.AddRange(val.Split(';'));
                        DropProfiles[profileName] = items;
                    }
                }

                if (LootProfiles.Count == 0) { LootProfiles["Default"] = new List<string>(); ActiveProfiles.Add("Default"); }
                if (DropProfiles.Count == 0) { DropProfiles["Default"] = new List<string>(); ActiveDropProfiles.Add("Default"); }
            }
            catch { }
        }

        private static string ColorToString(Color c) { return $"{c.r.ToString(CultureInfo.InvariantCulture)},{c.g.ToString(CultureInfo.InvariantCulture)},{c.b.ToString(CultureInfo.InvariantCulture)},{c.a.ToString(CultureInfo.InvariantCulture)}"; }
        private static Color StringToColor(string s) { try { string[] split = s.Split(','); if (split.Length >= 3) { float r = float.Parse(split[0], CultureInfo.InvariantCulture); float g = float.Parse(split[1], CultureInfo.InvariantCulture); float b = float.Parse(split[2], CultureInfo.InvariantCulture); float a = split.Length > 3 ? float.Parse(split[3], CultureInfo.InvariantCulture) : 1f; return new Color(r, g, b, a); } } catch { } return Color.white; }
        public static List<string> GetCombinedActiveList() { HashSet<string> combined = new HashSet<string>(); foreach (var profName in ActiveProfiles) { if (LootProfiles.ContainsKey(profName)) foreach (var item in LootProfiles[profName]) combined.Add(item); } return combined.ToList(); }
        public static List<string> GetCombinedActiveDropList() { HashSet<string> combined = new HashSet<string>(); foreach (var profName in ActiveDropProfiles) { if (DropProfiles.ContainsKey(profName)) foreach (var item in DropProfiles[profName]) combined.Add(item); } return combined.ToList(); }
    }
}