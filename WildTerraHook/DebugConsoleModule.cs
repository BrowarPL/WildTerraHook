using UnityEngine;
using System.Collections.Generic;
using System.Text;

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

        // Ograniczenie pamięci
        private const int MAX_LOGS = 300;

        public DebugConsoleModule()
        {
            // Rejestracja callbacku systemowego Unity
            Application.logMessageReceived += HandleLog;
        }

        public void Shutdown()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (_logs.Count > MAX_LOGS)
            {
                _logs.RemoveAt(0);
            }

            _logs.Add(new LogEntry
            {
                Message = $"[{System.DateTime.Now:HH:mm:ss}] {logString}",
                StackTrace = stackTrace,
                Type = type
            });

            if (_autoScroll)
            {
                _scrollPos = new Vector2(0, float.MaxValue);
            }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");

            // Pasek narzędzi konsoli
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>DEBUG CONSOLE</b>");
            GUILayout.FlexibleSpace();
            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-Scroll");
            if (GUILayout.Button("Clear", GUILayout.Width(60))) _logs.Clear();
            GUILayout.EndHorizontal();

            // Filtry
            GUILayout.BeginHorizontal();
            _showLogs = GUILayout.Toggle(_showLogs, "Info");
            _showWarnings = GUILayout.Toggle(_showWarnings, "Warn");
            _showErrors = GUILayout.Toggle(_showErrors, "Error");
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Obszar logów
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUI.skin.box);

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.richText = true;

            for (int i = 0; i < _logs.Count; i++)
            {
                var log = _logs[i];

                // Filtrowanie
                if (log.Type == LogType.Log && !_showLogs) continue;
                if (log.Type == LogType.Warning && !_showWarnings) continue;
                if ((log.Type == LogType.Error || log.Type == LogType.Exception) && !_showErrors) continue;

                // Kolorowanie
                string color = "white";
                if (log.Type == LogType.Warning) color = "yellow";
                else if (log.Type == LogType.Error || log.Type == LogType.Exception) color = "red";

                // Rysowanie
                style.normal.textColor = GetColor(log.Type);
                GUILayout.Label(log.Message, style);

                // Stack trace dla błędów
                if (log.Type == LogType.Exception || log.Type == LogType.Error)
                {
                    GUILayout.Label($"<color=grey><size=10>{log.StackTrace}</size></color>", style);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
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