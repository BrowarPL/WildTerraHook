using UnityEngine;

namespace WildTerraHook
{
    public class Loader
    {
        // Stała nazwa obiektu w scenie - klucz do znalezienia starej wersji
        private const string GAMEOBJECT_NAME = "WildTerraHook_Loader";

        // Tę metodę wywołuje Injector
        public static void Init()
        {
            // KROK 1: BRUTE FORCE CLEANUP
            // Szukamy obiektu po nazwie. To znajdzie obiekt stworzony przez POPRZEDNIĄ wersję DLL.
            GameObject existing = GameObject.Find(GAMEOBJECT_NAME);
            while (existing != null)
            {
                // Niszczymy natychmiast
                UnityEngine.Object.DestroyImmediate(existing);
                // Szukamy, czy są jakieś duplikaty
                existing = GameObject.Find(GAMEOBJECT_NAME);
            }

            // KROK 2: FRESH START
            // Tworzymy nowy obiekt
            GameObject loadObject = new GameObject();
            loadObject.name = GAMEOBJECT_NAME;

            // Dodajemy główny komponent
            loadObject.AddComponent<MainHack>();

            // Zapobiegamy niszczeniu przy zmianie sceny
            UnityEngine.Object.DontDestroyOnLoad(loadObject);
        }

        // Metoda do ręcznego wywalenia hacka (np. przez klawisz DELETE)
        public static void Unload()
        {
            GameObject existing = GameObject.Find(GAMEOBJECT_NAME);
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing);
            }
        }
    }
}