using UnityEngine;

namespace WildTerraHook
{
    public class Loader
    {
        private static GameObject _loadObject;

        // Tę metodę musi wywołać Injector!
        public static void Init()
        {
            // Zabezpieczenie przed podwójnym załadowaniem
            if (GameObject.Find("WildTerraHook_Loader") != null) return;

            // 1. Tworzymy pusty obiekt w grze
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