using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using EasyBuildSystem.Runtimes.Internal.Builder;
using EasyBuildSystem.Runtimes.Internal.Part;

namespace WildTerraHook
{
    public class BuildingModule
    {
        private bool _init = false;

        private MonoBehaviour _builderInstance;
        private Type _builderType;

        // Zmienne do manipulacji (Pola i Właściwości)
        private FieldInfo _freePlacementField;
        private PropertyInfo _freePlacementProp;

        private FieldInfo _allowPlacementField;
        private PropertyInfo _allowPlacementProp;

        private FieldInfo _layerMaskField;

        // Cache
        private LayerMask _originalLayerMask;
        private bool _hasOriginalMask = false;

        // Używamy LateUpdate, aby nadpisać logikę gry PO jej wykonaniu
        public void Update()
        {
            if (!ConfigManager.Building_Enabled) return;

            // Szukamy buildera co jakiś czas, jeśli go nie ma
            if (_builderInstance == null && Time.frameCount % 60 == 0)
            {
                FindBuilder();
            }

            if (_builderInstance != null)
            {
                ApplyBuildingHacks();
            }
        }

        private void FindBuilder()
        {
            // Szukamy aktywnego buildera w scenie
            if (_builderInstance == null)
            {
                _builderInstance = UnityEngine.Object.FindObjectOfType<BuilderBehaviour>();

                if (_builderInstance != null)
                {
                    _builderType = _builderInstance.GetType();
                    InitReflections();
                }
            }
        }

        private void InitReflections()
        {
            if (_init) return;
            if (_builderType == null) return;

            try
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

                // 1. Szukamy FreePlacement (Tryb bez kolizji)
                _freePlacementField = _builderType.GetField("FreePlacement", flags) ?? _builderType.GetField("freePlacement", flags);
                _freePlacementProp = _builderType.GetProperty("FreePlacement", flags) ?? _builderType.GetProperty("freePlacement", flags);

                // 2. Szukamy AllowPlacement (Wynik sprawdzenia)
                // Często gra ustawia to na false, gdy jest kolizja. My to wymusimy na true.
                _allowPlacementField = _builderType.GetField("AllowPlacement", flags) ?? _builderType.GetField("allowPlacement", flags);
                _allowPlacementProp = _builderType.GetProperty("AllowPlacement", flags) ?? _builderType.GetProperty("allowPlacement", flags);

                // 3. Maska Kolizji
                _layerMaskField = _builderType.GetField("freePlacementLayers", flags)
                               ?? _builderType.GetField("collisionLayer", flags)
                               ?? _builderType.GetField("layerMask", flags);

                Debug.Log($"[Building] Init. Free: {(_freePlacementField != null || _freePlacementProp != null)}, Allow: {(_allowPlacementField != null || _allowPlacementProp != null)}");
                _init = true;
            }
            catch (Exception e) { Debug.LogError($"[Building] Init Error: {e.Message}"); }
        }

        private void ApplyBuildingHacks()
        {
            if (_builderInstance == null) return;

            try
            {
                // A. Wymuszamy tryb FreePlacement (ignorowanie zasad)
                if (_freePlacementField != null) _freePlacementField.SetValue(_builderInstance, true);
                if (_freePlacementProp != null && _freePlacementProp.CanWrite) _freePlacementProp.SetValue(_builderInstance, true, null);

                // B. Wymuszamy AllowPlacement (nawet jak duch jest czerwony, to pozwoli kliknąć)
                if (_allowPlacementField != null) _allowPlacementField.SetValue(_builderInstance, true);
                if (_allowPlacementProp != null && _allowPlacementProp.CanWrite) _allowPlacementProp.SetValue(_builderInstance, true, null);

                // C. Zerujemy maskę kolizji (tylko Field)
                if (_layerMaskField != null)
                {
                    LayerMask current = (LayerMask)_layerMaskField.GetValue(_builderInstance);
                    if (!_hasOriginalMask)
                    {
                        _originalLayerMask = current;
                        _hasOriginalMask = true;
                    }
                    if (current.value != 0)
                    {
                        _layerMaskField.SetValue(_builderInstance, new LayerMask { value = 0 });
                    }
                }
            }
            catch { }
        }

        public void DrawMenu()
        {
            GUILayout.Label("<b>Building Hacks (LateUpdate)</b>");
            bool en = GUILayout.Toggle(ConfigManager.Building_Enabled, "Build Anywhere (Force)");
            if (en != ConfigManager.Building_Enabled)
            {
                ConfigManager.Building_Enabled = en;
                ConfigManager.Save();

                // Reset przy wyłączaniu
                if (!en && _builderInstance != null && _hasOriginalMask && _layerMaskField != null)
                {
                    _layerMaskField.SetValue(_builderInstance, _originalLayerMask);
                }
            }

            GUILayout.Space(5);
            if (GUILayout.Button("DUMP FULL BUILDER INFO"))
            {
                DumpBuilderVars();
            }
            GUILayout.Label("<size=10>Jeśli nie działa: Wejdź w tryb budowania i kliknij DUMP.\nSprawdź w logu nazwy zmiennych.</size>");
        }

        private void DumpBuilderVars()
        {
            if (_builderInstance == null) FindBuilder();
            if (_builderInstance == null)
            {
                Debug.LogError("[Building] Nie znaleziono aktywnego Buildera! (Musisz trzymać młotek/ducha)");
                return;
            }

            Debug.LogWarning("=== BUILDER DUMP START ===");
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            // 1. Pola (Fields)
            foreach (var f in _builderType.GetFields(flags))
            {
                // Filtrujemy typy proste dla czytelności
                if (f.FieldType == typeof(bool) || f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(LayerMask))
                {
                    object val = f.GetValue(_builderInstance);
                    if (f.FieldType == typeof(LayerMask)) val = ((LayerMask)val).value;
                    Debug.Log($"[FIELD] {f.Name} ({f.FieldType.Name}) = {val}");
                }
            }

            // 2. Właściwości (Properties) - TEGO BRAKOWAŁO WCZEŚNIEJ
            foreach (var p in _builderType.GetProperties(flags))
            {
                if (p.PropertyType == typeof(bool) || p.PropertyType == typeof(float) || p.PropertyType == typeof(LayerMask))
                {
                    try
                    {
                        object val = p.GetValue(_builderInstance, null);
                        Debug.Log($"[PROP]  {p.Name} ({p.PropertyType.Name}) = {val}");
                    }
                    catch { } // Niektóre property mogą rzucać błędy przy get
                }
            }
            Debug.LogWarning("=== BUILDER DUMP END ===");
        }
    }
}