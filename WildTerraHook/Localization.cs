using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WildTerraHook
{
    public static class Localization
    {
        private static Dictionary<string, string> _currentDict = new Dictionary<string, string>();

        // Ścieżki
        private static string _folderPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook");

        public static void Init()
        {
            EnsureDefaultFiles();
            LoadLanguage(ConfigManager.Language);
        }

        public static void LoadLanguage(string langCode)
        {
            _currentDict.Clear();
            string fileName = $"lang_{langCode}.txt";
            string path = Path.Combine(_folderPath, fileName);

            if (File.Exists(path))
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            _currentDict[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
                catch { Debug.LogError("Błąd wczytywania języka"); }
            }
            else
            {
                // Fallback do twardych danych jeśli plik usunięto
                LoadHardcoded(langCode);
            }
        }

        public static string Get(string key)
        {
            if (_currentDict.ContainsKey(key)) return _currentDict[key];
            return key; // Zwróć klucz jeśli brakuje tłumaczenia
        }

        private static void EnsureDefaultFiles()
        {
            if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);

            string plPath = Path.Combine(_folderPath, "lang_pl.txt");
            string enPath = Path.Combine(_folderPath, "lang_en.txt");

            if (!File.Exists(enPath)) File.WriteAllText(enPath, GetDefaultEn());
            if (!File.Exists(plPath)) File.WriteAllText(plPath, GetDefaultPl());
        }

        // --- DANE TŁUMACZEŃ ---

        private static void LoadHardcoded(string lang)
        {
            string data = (lang == "pl") ? GetDefaultPl() : GetDefaultEn();
            foreach (var line in data.Split('\n'))
            {
                if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2) _currentDict[parts[0].Trim()] = parts[1].Trim();
            }
        }

        private static string GetDefaultEn()
        {
            return @"
MENU_TITLE=Wild Terra 2 Hack
MENU_TOGGLE_INFO=Press INSERT to Toggle Menu | DELETE to Hide All

MISC_TITLE=Misc Options
MISC_ETERNAL_DAY=Eternal Day (12:00)
MISC_NO_FOG=No Fog
MISC_FULLBRIGHT=Fullbright (No Shadows)
MISC_BRIGHT_PLAYER=Bright Player (Flashlight)
MISC_LIGHT_INT=Intensity
MISC_LIGHT_RNG=Range
MISC_ZOOM_TITLE=Zoom Hack (Unlock)
MISC_ZOOM_LIMIT=Zoom Limit
MISC_CAM_ANGLE=Cam Angle (Vert)
MISC_ZOOM_SENS=Sensitivity
MISC_FOV=Camera FOV
MISC_RESET=Reset Defaults
MISC_LANG_SEL=Language / Język

ESP_MAIN_BTN=[ ENABLE / DISABLE ESP ]
ESP_RES_TITLE=RESOURCES
ESP_MOB_TITLE=MOBS
ESP_DIST=Distance
ESP_EDIT_COLORS=Edit ESP Colors
ESP_HIDE_COLORS=Hide Colors
ESP_SAVE_COLORS=Save Colors

ESP_CAT_MINING=Mining
ESP_CAT_GATHER=Gathering
ESP_CAT_LUMBER=Lumberjacking
ESP_CAT_GODSEND=Godsend (Chests)
ESP_CAT_OTHERS=Others

ESP_MOB_AGGRO=Aggressive (Boss/LargeFox)
ESP_MOB_RETAL=Retaliating (Fox/Horse)
ESP_MOB_PASSIVE=Passive (Deer/Hare)

COLOR_MOB_AGGRO=Aggressive Mobs
COLOR_MOB_PASSIVE=Passive Mobs
COLOR_MOB_FLEE=Fleeing/Retal Mobs
COLOR_RES_MINE=Mining Nodes
COLOR_RES_GATHER=Gatherables
COLOR_RES_LUMB=Trees
".Trim();
        }

        private static string GetDefaultPl()
        {
            return @"
MENU_TITLE=Wild Terra 2 Hack (PL)
MENU_TOGGLE_INFO=Wciśnij INSERT aby ukryć Menu | DELETE aby ukryć Wszystko

MISC_TITLE=Różne Opcje (Misc)
MISC_ETERNAL_DAY=Wieczny Dzień (12:00)
MISC_NO_FOG=Brak Mgły (No Fog)
MISC_FULLBRIGHT=Fullbright (Brak Cieni)
MISC_BRIGHT_PLAYER=Latarka Gracza
MISC_LIGHT_INT=Moc Światła
MISC_LIGHT_RNG=Zasięg
MISC_ZOOM_TITLE=Zoom Hack (Odblokuj Kamerę)
MISC_ZOOM_LIMIT=Limit Oddalenia
MISC_CAM_ANGLE=Kąt Patrzenia (Pion)
MISC_ZOOM_SENS=Czułość Zoomu
MISC_FOV=Kąt Widzenia (FOV)
MISC_RESET=Przywróć Domyślne
MISC_LANG_SEL=Język / Language

ESP_MAIN_BTN=[ WŁĄCZ / WYŁĄCZ ESP ]
ESP_RES_TITLE=SUROWCE (RESOURCES)
ESP_MOB_TITLE=MOBY (MOBS)
ESP_DIST=Dystans Rysowania
ESP_EDIT_COLORS=Edytuj Kolory ESP
ESP_HIDE_COLORS=Ukryj Edytor Kolorów
ESP_SAVE_COLORS=Zapisz Konfigurację

ESP_CAT_MINING=Górnictwo (Mining)
ESP_CAT_GATHER=Zbieractwo (Gathering)
ESP_CAT_LUMBER=Drwalnictwo (Lumber)
ESP_CAT_GODSEND=Skarby (Godsend)
ESP_CAT_OTHERS=Inne (Others)

ESP_MOB_AGGRO=Agresywne (Boss/LargeFox)
ESP_MOB_RETAL=Oddające (Lis/Koń)
ESP_MOB_PASSIVE=Pasywne (Jeleń/Zając)

COLOR_MOB_AGGRO=Kolor: Agresywne
COLOR_MOB_PASSIVE=Kolor: Pasywne
COLOR_MOB_FLEE=Kolor: Oddające
COLOR_RES_MINE=Kolor: Skały/Rudy
COLOR_RES_GATHER=Kolor: Zbieractwo
COLOR_RES_LUMB=Kolor: Drzewa
".Trim();
        }
    }
}