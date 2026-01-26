using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WildTerraHook
{
    public static class Localization
    {
        private static Dictionary<string, string> _currentDict = new Dictionary<string, string>();
        private static string _folderPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook");

        public static void Init()
        {
            if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);
            LoadLanguage(ConfigManager.Language);
        }

        public static void LoadLanguage(string langCode)
        {
            _currentDict.Clear();
            string fileName = $"lang_{langCode}.txt";
            string path = Path.Combine(_folderPath, fileName);

            bool loadedFromDisk = false;
            if (File.Exists(path))
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length == 2) _currentDict[parts[0].Trim()] = parts[1].Trim();
                    }
                    loadedFromDisk = true;
                }
                catch { Debug.LogError("[Localization] Read Error."); }
            }

            if (!loadedFromDisk)
            {
                string content = (langCode == "pl") ? GetDefaultPl() : GetDefaultEn();
                try { File.WriteAllText(path, content); } catch { }
                LoadHardcoded(langCode);
            }
            else
            {
                LoadFallback(langCode);
            }
        }

        public static string Get(string key)
        {
            if (_currentDict.ContainsKey(key)) return _currentDict[key];
            return key;
        }

        private static void LoadHardcoded(string lang)
        {
            _currentDict.Clear();
            string data = (lang == "pl") ? GetDefaultPl() : GetDefaultEn();
            ParseData(data);
        }

        private static void LoadFallback(string lang)
        {
            string data = (lang == "pl") ? GetDefaultPl() : GetDefaultEn();
            foreach (var line in data.Split('\n'))
            {
                if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                var parts = line.Split(new[] { '=' }, 2);
                string key = parts[0].Trim();
                // Dodajemy tylko brakujące klucze, ale MENU_TITLE zostanie nadpisane tylko jeśli plik nie istnieje lub usuniemy go ręcznie.
                // W przypadku developmentu możesz usunąć pliki txt z AppData/WildTerraHook.
                if (!_currentDict.ContainsKey(key) && parts.Length == 2)
                {
                    _currentDict[key] = parts[1].Trim();
                }
            }
        }

        private static void ParseData(string data)
        {
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
MENU_TITLE=Wild Terra 2 Hack by BrowaR
MENU_TOGGLE_INFO=Press INSERT to Toggle Menu | DELETE to Hide All

MENU_TAB_ESP=ESP
MENU_TAB_FISH=Fishing
MENU_TAB_LOOT=Auto Loot
MENU_TAB_DROP=Auto Drop
MENU_TAB_MISC=Misc
MENU_TAB_CONSOLE=CONSOLE

MISC_TITLE=Misc Options
MISC_ETERNAL_DAY=Eternal Day (12:00)
MISC_NO_FOG=No Fog (Distance Hack)
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
MISC_UI_HEADER=UI SETTINGS
MISC_UI_SCALE=Scale
MISC_RENDER_DIST=Render Dist
MISC_AUTO_BUTCHER=Auto Butcher

ESP_MAIN_BTN=[ ENABLE / DISABLE ESP ]
ESP_RES_TITLE=RESOURCES
ESP_MOB_TITLE=MOBS
ESP_DIST=Distance
ESP_FPS=Display FPS
ESP_EDIT_COLORS=Edit ESP Colors
ESP_HIDE_COLORS=Hide Colors
ESP_SAVE_COLORS=Save Colors
ESP_BOX=Box ESP
ESP_XRAY=X-Ray Glow

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

FISH_TITLE=Fish Bot (Smart)
FISH_ENABLE=Enable Bot (Cast manually first)
FISH_TIMEOUT=Timeout
FISH_DEBUG_WAIT=WAITING FOR MANUAL CAST...
FISH_DEBUG_RESET=RESET.
FISH_DEBUG_CALIB=Calibrated! Target:
FISH_DEBUG_MANUAL=CAST MANUALLY TO START!
FISH_DEBUG_ATTACK=ATTACK! Interrupting.
FISH_STATE=STATE
FISH_TIMER=Timer
FISH_ATT_ID=Attack ID
FISH_ROD_ID=Rod ID
FISH_TARGET=Target
FISH_NONE=None
FISH_SHOW_ESP=Show ESP (Green)
FISH_COLOR_BOT_TOGGLE=Color Bot (Standard)

MEMFISH_SHOW_ESP=Show ESP (Purple)
MEMFISH_AUTO=Auto Click
MEMFISH_REACTION=Reaction
MEMFISH_STATUS_WAIT_WIN=Waiting for window...
MEMFISH_STATUS_CRIT=CRITICAL: UI Structure mismatch
MEMFISH_STATUS_READY=Ready (Rules loaded)
MEMFISH_STATUS_ERROR=ERROR
MEMFISH_STATUS_WAIT_FISH=Waiting for fish...
MEMFISH_STATUS_EMPTY_RULES=Rules list is empty/null!
MEMFISH_STATUS_NO_RULE=No rule for
MEMFISH_STATUS_INACTIVE=Inactive

LOOT_TITLE=Auto Loot & Profiles
LOOT_ENABLE=Enable Auto Loot
LOOT_DELAY=Delay
LOOT_DEBUG=Debug Mode
LOOT_FILTER=Search Item...
LOOT_BTN_ADD=Add
LOOT_BTN_REMOVE=Remove
LOOT_BTN_REFRESH=Refresh
LOOT_HEADER_WHITE=Current List Items
LOOT_HEADER_ALL=Available Items
LOOT_STATUS=Status
LOOT_PROFILES=Profiles (Lists)
LOOT_NEW_NAME=New List Name
LOOT_CREATE=Create
LOOT_DELETE=Del
LOOT_ACTIVATE=Load
LOOT_DETECTED=--- DETECTED ---
LOOT_EDITING=EDITING
LOOT_SCANNING=Scanning...
LOOT_READY=Ready
LOOT_ERROR=Error
LOOT_WAITING=Waiting for window...

DROP_TITLE=AUTO DROP (Blacklist)
DROP_ENABLE=ENABLE
DROP_DEBUG=Debug Mode
DROP_OVERRIDE=Override Name
DROP_DIAGNOSTICS=DIAGNOSTICS
DROP_DELAY=Delay
DROP_ADD_HEADER=Add to Blacklist
DROP_BTN_ADD=Add
DROP_BTN_FORCE=Force Add
DROP_ACTIVE_LIST=Active List (Dropped)
DROP_EMPTY=(Empty List)
DROP_REMOVE=Remove
DROP_CLEAR_ALL=Clear All
DROP_PROFILES=Profile Manager
DROP_CREATE_PROF=Create Profile

CONSOLE_TITLE=DEBUG CONSOLE
CONSOLE_SAVE=SAVE LOG
CONSOLE_CLEAR=Clear
CONSOLE_AUTOSCROLL=Auto-Scroll
CONSOLE_INFO=Info
CONSOLE_WARN=Warn
CONSOLE_ERROR=Error
".Trim();
        }

        private static string GetDefaultPl()
        {
            return @"
MENU_TITLE=Wild Terra 2 Hack (PL) by BrowaR
MENU_TOGGLE_INFO=Wciśnij INSERT aby ukryć Menu | DELETE aby ukryć Wszystko

MENU_TAB_ESP=ESP
MENU_TAB_FISH=Wędkowanie
MENU_TAB_LOOT=Auto Loot
MENU_TAB_DROP=Auto Drop
MENU_TAB_MISC=Inne
MENU_TAB_CONSOLE=KONSOLA

MISC_TITLE=Różne Opcje (Misc)
MISC_ETERNAL_DAY=Wieczny Dzień (12:00)
MISC_NO_FOG=Brak Mgły (Distance Hack)
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
MISC_UI_HEADER=USTAWIENIA UI
MISC_UI_SCALE=Skala
MISC_RENDER_DIST=Dystans Renderowania
MISC_AUTO_BUTCHER=Auto Rzeźnik (Butcher)

ESP_MAIN_BTN=[ WŁĄCZ / WYŁĄCZ ESP ]
ESP_RES_TITLE=SUROWCE (RESOURCES)
ESP_MOB_TITLE=MOBY (MOBS)
ESP_DIST=Dystans Rysowania
ESP_FPS=Odświeżanie (FPS)
ESP_EDIT_COLORS=Edytuj Kolory ESP
ESP_HIDE_COLORS=Ukryj Edytor Kolorów
ESP_SAVE_COLORS=Zapisz Konfigurację
ESP_BOX=Obramowania (Box)
ESP_XRAY=Podświetlenie (X-Ray)

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

FISH_TITLE=Fish Bot (Inteligentny)
FISH_ENABLE=Włącz Bota (Zarzuć ręcznie)
FISH_TIMEOUT=Limit Czasu (s)
FISH_DEBUG_WAIT=OCZEKIWANIE NA RĘCZNE...
FISH_DEBUG_RESET=RESET.
FISH_DEBUG_CALIB=Skalibrowano! Cel:
FISH_DEBUG_MANUAL=ZARZUĆ RĘCZNIE ABY ROZPOCZĄĆ!
FISH_DEBUG_ATTACK=ATAK! Przerywam.
FISH_STATE=STAN
FISH_TIMER=Licznik
FISH_ATT_ID=Atak ID
FISH_ROD_ID=Wędka ID
FISH_TARGET=Cel
FISH_NONE=Brak
FISH_SHOW_ESP=Pokaż ESP (Zielony)
FISH_COLOR_BOT_TOGGLE=Color Bot (Standardowy)

MEMFISH_SHOW_ESP=Pokaż ESP (Fiolet)
MEMFISH_AUTO=Auto Klikanie
MEMFISH_REACTION=Reakcja
MEMFISH_STATUS_WAIT_WIN=Oczekiwanie na okno...
MEMFISH_STATUS_CRIT=KRYTYCZNE: Błąd struktury UI
MEMFISH_STATUS_READY=Gotowy (Reguły załadowane)
MEMFISH_STATUS_ERROR=BŁĄD
MEMFISH_STATUS_WAIT_FISH=Czekam na rybę...
MEMFISH_STATUS_EMPTY_RULES=Lista reguł jest pusta/null!
MEMFISH_STATUS_NO_RULE=Brak reguły dla
MEMFISH_STATUS_INACTIVE=Nieaktywny

LOOT_TITLE=Auto Loot i Listy
LOOT_ENABLE=Włącz Auto Loot
LOOT_DELAY=Opóźnienie
LOOT_DEBUG=Tryb Debug
LOOT_FILTER=Szukaj...
LOOT_BTN_ADD=Dodaj
LOOT_BTN_REMOVE=Usuń
LOOT_BTN_REFRESH=Odśwież
LOOT_HEADER_WHITE=Zawartość Listy
LOOT_HEADER_ALL=Wszystkie Itemy
LOOT_STATUS=Status
LOOT_PROFILES=Twoje Listy (Profile)
LOOT_NEW_NAME=Nazwa nowej listy
LOOT_CREATE=Stwórz
LOOT_DELETE=Usuń
LOOT_ACTIVATE=Wczytaj
LOOT_DETECTED=--- WYKRYTE ---
LOOT_EDITING=EDYCJA
LOOT_SCANNING=Skanowanie...
LOOT_READY=Gotowe
LOOT_ERROR=Błąd
LOOT_WAITING=Czekam na okno...

DROP_TITLE=AUTO DROP (Blacklist)
DROP_ENABLE=WŁĄCZ
DROP_DEBUG=Tryb Debug
DROP_OVERRIDE=Override Nazwy
DROP_DIAGNOSTICS=DIAGNOSTYKA
DROP_DELAY=Opóźnienie
DROP_ADD_HEADER=Dodaj do Blacklisty
DROP_BTN_ADD=Dodaj
DROP_BTN_FORCE=Wymuś dodanie
DROP_ACTIVE_LIST=Aktywna Lista (Wyrzucane)
DROP_EMPTY=(Lista pusta)
DROP_REMOVE=Usuń
DROP_CLEAR_ALL=Wyczyść Całą Listę
DROP_PROFILES=Zarządzanie Profilami
DROP_CREATE_PROF=Utwórz Profil

CONSOLE_TITLE=KONSOLA DEBUG
CONSOLE_SAVE=ZAPISZ LOG
CONSOLE_CLEAR=Czyść
CONSOLE_AUTOSCROLL=Auto-Scroll
CONSOLE_INFO=Info
CONSOLE_WARN=Ostrz
CONSOLE_ERROR=Błędy
".Trim();
        }
    }
}