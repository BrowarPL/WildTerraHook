using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System;
using System.Reflection;
using System.Linq;

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
        private string _commandInput = ""; // Pole do wpisywania komend

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

            // --- HEADER ---
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{Localization.Get("CONSOLE_TITLE")}</b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Localization.Get("CONSOLE_SAVE"), GUILayout.Width(100))) SaveLogsToFile();
            if (GUILayout.Button(Localization.Get("CONSOLE_CLEAR"), GUILayout.Width(60))) _logs.Clear();
            GUILayout.EndHorizontal();

            // --- FILTERS ---
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

            // --- LOG WINDOW ---
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

            // --- COMMAND EXECUTOR (NOWOŚĆ) ---
            GUILayout.Space(5);
            GUILayout.Label("<b>Command Executor (WTPlayer):</b>");
            GUILayout.BeginHorizontal();
            _commandInput = GUILayout.TextField(_commandInput);
            if (GUILayout.Button("EXECUTE", GUILayout.Width(80)))
            {
                TryExecuteCommand(_commandInput);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("<size=10><i>Format: MethodName(123, \"text\", true)</i></size>");

            GUILayout.EndVertical();
        }

        private void TryExecuteCommand(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            if (global::Player.localPlayer == null)
            {
                Debug.LogError("[Executor] LocalPlayer is null!");
                return;
            }

            Debug.Log($"[Executor] Invoking: {input}");

            try
            {
                // 1. Parsowanie nazwy i argumentów
                string methodName = input;
                object[] parameters = new object[0];

                int openParen = input.IndexOf('(');
                int closeParen = input.LastIndexOf(')');

                if (openParen > 0 && closeParen > openParen)
                {
                    methodName = input.Substring(0, openParen).Trim();
                    string argsStr = input.Substring(openParen + 1, closeParen - openParen - 1);
                    if (!string.IsNullOrWhiteSpace(argsStr))
                    {
                        parameters = ParseArguments(argsStr);
                    }
                }

                // 2. Wyszukanie metody w WTPlayer
                var player = global::Player.localPlayer;
                var type = player.GetType();

                // Szukamy wszystkich metod o tej nazwie (public i non-public)
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                                  .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                                  .ToList();

                if (methods.Count == 0)
                {
                    Debug.LogError($"[Executor] Method '{methodName}' not found on WTPlayer.");
                    return;
                }

                // 3. Dopasowanie przeciążenia (Overload)
                MethodInfo targetMethod = null;
                object[] convertedParams = null;

                foreach (var method in methods)
                {
                    var paramInfos = method.GetParameters();
                    if (paramInfos.Length == parameters.Length)
                    {
                        try
                        {
                            // Próba konwersji typów
                            convertedParams = new object[parameters.Length];
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                Type targetType = paramInfos[i].ParameterType;

                                // Obsługa Enumów (jeśli parametr jest intem lub stringiem)
                                if (targetType.IsEnum)
                                {
                                    convertedParams[i] = Enum.Parse(targetType, parameters[i].ToString());
                                }
                                else
                                {
                                    convertedParams[i] = Convert.ChangeType(parameters[i], targetType);
                                }
                            }
                            targetMethod = method;
                            break; // Znaleziono pasującą metodę
                        }
                        catch
                        {
                            // Typy nie pasują, szukamy dalej
                            continue;
                        }
                    }
                }

                if (targetMethod != null)
                {
                    // 4. Wywołanie
                    object result = targetMethod.Invoke(player, convertedParams);
                    if (result != null) Debug.Log($"[Executor] Result: {result}");
                    else Debug.Log($"[Executor] Success (Void)");
                }
                else
                {
                    Debug.LogError($"[Executor] No matching overload found for '{methodName}' with {parameters.Length} arguments.");
                }

            }
            catch (Exception ex)
            {
                Debug.LogError($"[Executor] Exception: {ex.Message}");
            }
        }

        private object[] ParseArguments(string argsStr)
        {
            List<object> args = new List<object>();
            // Prosty parser dzielący po przecinkach, uwzględniający stringi w cudzysłowach
            bool inQuote = false;
            StringBuilder currentArg = new StringBuilder();

            for (int i = 0; i < argsStr.Length; i++)
            {
                char c = argsStr[i];
                if (c == '\"')
                {
                    inQuote = !inQuote;
                    continue; // Pomiń sam znak cudzysłowu w wyniku
                }

                if (c == ',' && !inQuote)
                {
                    args.Add(ParseSingleArg(currentArg.ToString()));
                    currentArg.Clear();
                }
                else
                {
                    currentArg.Append(c);
                }
            }
            if (currentArg.Length > 0) args.Add(ParseSingleArg(currentArg.ToString()));

            return args.ToArray();
        }

        private object ParseSingleArg(string raw)
        {
            string clean = raw.Trim();

            // Boolean
            if (bool.TryParse(clean, out bool bVal)) return bVal;

            // Int
            if (int.TryParse(clean, out int iVal)) return iVal;

            // Float (musi mieć f lub kropkę, ale TryParse ogarnie standard)
            if (float.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float fVal)) return fVal;

            // String (jeśli nie jest liczbą/bool, traktujemy jako string)
            return clean;
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