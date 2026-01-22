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
        private bool _autoScroll = true;
        private bool _showErrors = true;
        private bool _showLogs = true;
        private bool _showWarnings = true;

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

            if (_autoScroll) _scrollPos = new Vector2(0, float.MaxValue);
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
            _autoScroll = GUILayout.Toggle(_autoScroll, Localization.Get("CONSOLE_AUTOSCROLL"));
            _showLogs = GUILayout.Toggle(_showLogs, Localization.Get("CONSOLE_INFO"));
            _showWarnings = GUILayout.Toggle(_showWarnings, Localization.Get("CONSOLE_WARN"));
            _showErrors = GUILayout.Toggle(_showErrors, Localization.Get("CONSOLE_ERROR"));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUI.skin.box);

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.richText = true;

            for (int i = 0; i < _logs.Count; i++)
            {
                var log = _logs[i];

                if (log.Type == LogType.Log && !_showLogs) continue;
                if (log.Type == LogType.Warning && !_showWarnings) continue;
                if ((log.Type == LogType.Error || log.Type == LogType.Exception) && !_showErrors) continue;

                string color = "white";
                if (log.Type == LogType.Warning) color = "yellow";
                else if (log.Type == LogType.Error || log.Type == LogType.Exception) color = "red";

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