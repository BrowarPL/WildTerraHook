using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System;

namespace WildTerraHook
{
    public class DebugConsoleModule
    {
        private struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
        }

        private List<LogEntry> _logs = new List<LogEntry>();
        private Vector2 _scrollPos;

        private const int MAX_LOGS = 500;

        public DebugConsoleModule()
        {
            Application.logMessageReceived += HandleLog;
        }

        public void Shutdown()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (_logs.Count > MAX_LOGS) _logs.RemoveAt(0);

            _logs.Add(new LogEntry
            {
                Message = $"[{DateTime.Now:HH:mm:ss}] {logString}",
                StackTrace = stackTrace,
                Type = type
            });

            if (ConfigManager.Console_AutoScroll) _scrollPos = new Vector2(0, float.MaxValue);
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{Localization.Get("CONSOLE_TITLE")}</b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Localization.Get("CONSOLE_SAVE"), GUILayout.Width(100))) SaveLogsToFile();
            if (GUILayout.Button(Localization.Get("CONSOLE_CLEAR"), GUILayout.Width(60))) _logs.Clear();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            bool newAuto = GUILayout.Toggle(ConfigManager.Console_AutoScroll, Localization.Get("CONSOLE_AUTOSCROLL"));
            if (newAuto != ConfigManager.Console_AutoScroll) { ConfigManager.Console_AutoScroll = newAuto; ConfigManager.Save(); }

            bool newInfo = GUILayout.Toggle(ConfigManager.Console_ShowInfo, Localization.Get("CONSOLE_INFO"));
            if (newInfo != ConfigManager.Console_ShowInfo) { ConfigManager.Console_ShowInfo = newInfo; ConfigManager.Save(); }

            bool newWarn = GUILayout.Toggle(ConfigManager.Console_ShowWarnings, Localization.Get("CONSOLE_WARN"));
            if (newWarn != ConfigManager.Console_ShowWarnings) { ConfigManager.Console_ShowWarnings = newWarn; ConfigManager.Save(); }

            bool newErr = GUILayout.Toggle(ConfigManager.Console_ShowErrors, Localization.Get("CONSOLE_ERROR"));
            if (newErr != ConfigManager.Console_ShowErrors) { ConfigManager.Console_ShowErrors = newErr; ConfigManager.Save(); }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUI.skin.box);

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.richText = true;

            for (int i = 0; i < _logs.Count; i++)
            {
                var log = _logs[i];

                if (log.Type == LogType.Log && !ConfigManager.Console_ShowInfo) continue;
                if (log.Type == LogType.Warning && !ConfigManager.Console_ShowWarnings) continue;
                if ((log.Type == LogType.Error || log.Type == LogType.Exception) && !ConfigManager.Console_ShowErrors) continue;

                style.normal.textColor = GetColor(log.Type);
                GUILayout.Label(log.Message, style);

                if (log.Type == LogType.Exception || log.Type == LogType.Error)
                {
                    GUILayout.Label($"<color=grey><size=10>{log.StackTrace}</size></color>", style);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void SaveLogsToFile()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WildTerraHook", "log.txt");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"--- LOG SAVED AT {DateTime.Now} ---");
                foreach (var log in _logs)
                {
                    sb.AppendLine($"{log.Message}");
                    if (!string.IsNullOrEmpty(log.StackTrace)) sb.AppendLine(log.StackTrace);
                    sb.AppendLine("------------------------------------------------");
                }
                File.WriteAllText(path, sb.ToString());
                Debug.Log($"[Console] Log saved to: {path}");
            }
            catch (Exception ex) { Debug.LogError($"[Console] Failed to save: {ex.Message}"); }
        }

        private Color GetColor(LogType type)
        {
            switch (type)
            {
                case LogType.Error: return Color.red;
                case LogType.Assert: return Color.red;
                case LogType.Warning: return Color.yellow;
                case LogType.Log: return Color.white;
                case LogType.Exception: return Color.magenta;
                default: return Color.white;
            }
        }
    }
}