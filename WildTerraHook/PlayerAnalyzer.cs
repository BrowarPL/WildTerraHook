using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace WildTerraHook
{
    public class PlayerAnalyzer
    {
        private Vector2 _scrollPos;
        private string _searchString = "";
        private float _updateTimer = 0f;

        // Cache danych gracza
        private string _playerStats = "Brak danych...";

        public void Update()
        {
            // Aktualizacja co 1 sekundę
            if (Time.time > _updateTimer)
            {
                AnalyzeLocalPlayer();
                _updateTimer = Time.time + 1.0f;
            }
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Player Analyzer</b>");

            _searchString = GUILayout.TextField(_searchString);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(300));
            GUILayout.TextArea(_playerStats);
            GUILayout.EndScrollView();

            if (GUILayout.Button("Odśwież"))
            {
                AnalyzeLocalPlayer();
            }

            GUILayout.EndVertical();
        }

        private void AnalyzeLocalPlayer()
        {
            try
            {
                if (global::Player.localPlayer == null)
                {
                    _playerStats = "Player.localPlayer is null";
                    return;
                }

                var p = global::Player.localPlayer;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                sb.AppendLine($"Name: {p.name}");
                sb.AppendLine($"Position: {p.transform.position}");

                // Próba pobrania zdrowia/staminy przez Reflection (uniwersalne)
                AppendField(sb, p, "health");
                AppendField(sb, p, "maxHealth");
                AppendField(sb, p, "stamina");
                AppendField(sb, p, "maxStamina");
                AppendField(sb, p, "speed");

                _playerStats = sb.ToString();
            }
            catch (Exception ex)
            {
                _playerStats = $"Error: {ex.Message}";
            }
        }

        private void AppendField(System.Text.StringBuilder sb, object obj, string fieldName)
        {
            try
            {
                var field = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    sb.AppendLine($"{fieldName}: {field.GetValue(obj)}");
                }
            }
            catch { }
        }

        // Helper do szukania typów (zamiast MainHack.FindType)
        public static Type GetTypeByName(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == name)
                        return type;
                }
            }
            return null;
        }
    }
}