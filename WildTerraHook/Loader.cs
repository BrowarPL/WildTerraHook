using UnityEngine;

namespace WildTerraHook
{
    public class Loader
    {
        private static GameObject _loadObject;

        // Tę metodę wywoła Twój injector
        public static void Init()
        {
            // Sprawdź, czy hack już nie działa, aby nie stworzyć duplikatów
            if (GameObject.Find("WildTerraHook_Loader") != null) return;

            // Tworzymy pusty obiekt w świecie gry
            _loadObject = new GameObject();
            _loadObject.name = "WildTerraHook_Loader";

            // Dodajemy do niego nasz główny skrypt MainHack
            _loadObject.AddComponent<MainHack>();

            // Mówimy Unity, aby nie niszczyło tego obiektu przy zmianie sceny (ładowaniu mapy)
            UnityEngine.Object.DontDestroyOnLoad(_loadObject);
        }

        public static void Unload()
        {
            if (_loadObject != null)
            {
                UnityEngine.Object.Destroy(_loadObject);
            }
        }
    }
}