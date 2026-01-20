using System;
using System.IO;
using UnityEngine;

namespace WildTerraHook
{
    // Prosta klasa do serializacji JSON
    [Serializable]
    public class ConfigData
    {
        // --- Kolory MOBÓW ---
        public Color MobAggressive = Color.red;
        public Color MobPassive = Color.green;
        public Color MobFleeing = new Color(1f, 0.64f, 0f); // Pomarańczowy

        // --- Kolory SUROWCÓW ---
        public Color ResLumber = new Color(0.6f, 0.4f, 0.2f); // Brązowy
        public Color ResGather = Color.white;
        public Color ResMining = Color.gray;
    }

    public static class ConfigManager
    {
        public static ConfigData Settings = new ConfigData();
        private static string _folderPath;
        private static string _filePath;

        static ConfigManager()
        {
            // %appdata%/WildTerraHook/config.json
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook");
            _filePath = Path.Combine(_folderPath, "config.json");

            Load();
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);
                string json = JsonUtility.ToJson(Settings, true);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WildTerraHook] Błąd zapisu configu: {ex.Message}");
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    Settings = JsonUtility.FromJson<ConfigData>(json);
                }
                else
                {
                    // Zapisz domyślne jeśli nie ma pliku
                    Save();
                }
            }
            catch
            {
                Settings = new ConfigData();
            }
        }
    }
}