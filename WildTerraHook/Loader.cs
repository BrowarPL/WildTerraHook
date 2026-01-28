using UnityEngine;

namespace WildTerraHook
{
    public class Loader
    {
        private static GameObject _loadObject;

        // Tę metodę musi wywołać Injector!
        public static void Init()
        {
            // Zabezpieczenie i obsługa RELOADU
            // Sprawdzamy, czy w scenie istnieje już załadowany hack
            GameObject existing = GameObject.Find("WildTerraHook_Loader");
            if (existing != null)
            {
                // Zmieniamy nazwę, aby nie kolidowała z nowym obiektem
                existing.name = "WildTerraHook_Loader_Old";
                // Niszczymy stary obiekt - to wywoła OnDestroy w MainHack i posprząta moduły
                UnityEngine.Object.Destroy(existing);
            }

            // 1. Tworzymy nowy pusty obiekt w grze
            _loadObject = new GameObject();
            _loadObject.name = "WildTerraHook_Loader";

            // 2. Dodajemy do niego nasz główny skrypt (MainHack)
            _loadObject.AddComponent<MainHack>();

            // 3. Zapobiegamy niszczeniu obiektu przy zmianie mapy
            UnityEngine.Object.DontDestroyOnLoad(_loadObject);
        }

        public static void Unload()
        {
            if (_loadObject != null)
            {
                UnityEngine.Object.Destroy(_loadObject);
                _loadObject = null;
            }
        }
    }
}