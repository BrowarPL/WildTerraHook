using UnityEngine;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class PlayerAnalyzer
    {
        private UnityEngine.Object _targetObject;
        private string[] _targetClasses = { "WTUIFishingActions", "WTPlayer", "GameManager", "global::Player" };
        private int _selectedClassIndex = 0;
        private Vector2 _scrollPos = Vector2.zero;
        private string _searchQuery = "";

        private Dictionary<string, string> _snapshot = new Dictionary<string, string>();
        private HashSet<string> _changedFields = new HashSet<string>();
        private bool _onlyShowChanged = false;

        public void Update()
        {
            if (!Settings.ShowAnalyzer) return;
            if (_targetObject == null) FindTarget();
            if (_targetObject != null) ScanDifferences();
        }

        private void FindTarget()
        {
            string className = _targetClasses[_selectedClassIndex].Replace("global::", "");
            Type t = MainHack.FindType(className);
            if (t == null) return;
            _targetObject = UnityEngine.Object.FindObjectOfType(t);
            if (_targetObject == null) _targetObject = Resources.FindObjectsOfTypeAll(t).FirstOrDefault() as UnityEngine.Object;
            _snapshot.Clear(); _changedFields.Clear();
        }

        private void ScanDifferences()
        {
            var fields = _targetObject.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var f in fields)
            {
                string cur = f.GetValue(_targetObject)?.ToString() ?? "null";
                if (_snapshot.ContainsKey(f.Name) && _snapshot[f.Name] != cur) _changedFields.Add(f.Name);
            }
        }

        public void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SNAPSHOT", GUILayout.Height(25))) TakeSnapshot();
            _onlyShowChanged = GUILayout.Toggle(_onlyShowChanged, "Zmienione");
            GUILayout.EndHorizontal();

            _searchQuery = GUILayout.TextField(_searchQuery);
            int newIdx = GUILayout.SelectionGrid(_selectedClassIndex, _targetClasses, 2);
            if (newIdx != _selectedClassIndex) { _selectedClassIndex = newIdx; _targetObject = null; }

            if (_targetObject != null)
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(400));
                var fields = _targetObject.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (var f in fields)
                {
                    if (_onlyShowChanged && !_changedFields.Contains(f.Name)) continue;
                    if (!string.IsNullOrEmpty(_searchQuery) && !f.Name.ToLower().Contains(_searchQuery.ToLower())) continue;
                    bool ch = _changedFields.Contains(f.Name);
                    GUILayout.Label($"<color={(ch ? "red" : "yellow")}>{f.Name}: {f.GetValue(_targetObject)}</color>");
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private void TakeSnapshot()
        {
            _snapshot.Clear(); _changedFields.Clear();
            var fields = _targetObject.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var f in fields) _snapshot[f.Name] = f.GetValue(_targetObject)?.ToString() ?? "null";
        }
    }
}