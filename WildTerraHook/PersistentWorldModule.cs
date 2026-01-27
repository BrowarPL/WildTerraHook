using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // Publiczny dostęp dla ESP
        public Dictionary<Vector3, WorldGhost> Ghosts = new Dictionary<Vector3, WorldGhost>();

        private float _scanInterval = 0.2f;
        private float _lastScanTime = 0f;

        // Usunąłem listę _validKeywords - teraz bierzemy wszystko

        // Lista ignorowanych (Czarna Lista) - tu wpisz tylko to, co powoduje błędy lub błędy graficzne
        // Na razie pusta, żeby łapać wszystko jak leci.
        private string[] _blackList = {
            "FX_", "Particle", "LightSource" // Przykładowe śmieci, które mogą nie być potrzebne
        };

        public struct WorldGhost
        {
            public GameObject VisualObj;
            public GameObject RealObj;
            public string Name;
            public Vector3 Position;
        }

        public void Update()
        {
            if (Time.time - _lastScanTime > _scanInterval)
            {
                RefreshWorld();
                _lastScanTime = Time.time;
            }

            UpdateVisibility();
        }

        private void RefreshWorld()
        {
            // Znajdź wszystkie żywe obiekty WTObject w scenie
            var currentObjects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();

            foreach (var realObj in currentObjects)
            {
                if (realObj == null) continue;

                string name = realObj.name;

                // Sprawdzamy tylko czarną listę (śmieci), reszta przechodzi
                if (IsBlacklisted(name)) continue;

                // Pozycja jako unikalny klucz
                Vector3 pos = RoundVector(realObj.transform.position);

                if (!Ghosts.ContainsKey(pos))
                {
                    CreateGhost(realObj, pos);
                }
                else
                {
                    // Obiekt już jest w cache - aktualizujemy referencję do żywego obiektu
                    var ghost = Ghosts[pos];

                    // Jeśli nazwa się zmieniła (np. Drzewo ścięte -> Pień, Drzwi otwarte -> zamknięte), odświeżamy wizualizację
                    // Ignorujemy zmiany typu "(Clone)" w nazwie
                    string cleanRealName = name.Replace("(Clone)", "").Trim();
                    string cleanGhostName = ghost.Name.Replace("(Clone)", "").Trim();

                    if (cleanGhostName != cleanRealName)
                    {
                        if (ghost.VisualObj) UnityEngine.Object.Destroy(ghost.VisualObj);
                        Ghosts.Remove(pos);
                        CreateGhost(realObj, pos);
                    }
                    else
                    {
                        // Tylko aktualizujemy link do oryginału
                        ghost.RealObj = realObj.gameObject;
                        Ghosts[pos] = ghost;
                    }
                }
            }
        }

        private void CreateGhost(global::WTObject original, Vector3 pos)
        {
            // Tworzymy kontener
            GameObject ghostGo = new GameObject($"[CACHE] {original.name}");
            ghostGo.transform.position = original.transform.position;
            ghostGo.transform.rotation = original.transform.rotation;
            ghostGo.transform.localScale = original.transform.localScale;

            // Kopiujemy TYLKO wygląd (siatki i materiały)
            CopyVisuals(original.transform, ghostGo.transform);

            // Domyślnie ukryty, bo oryginał stoi obok (chyba że właśnie znika, wtedy UpdateVisibility go włączy)
            ghostGo.SetActive(false);

            WorldGhost wg = new WorldGhost
            {
                VisualObj = ghostGo,
                RealObj = original.gameObject,
                Name = original.name,
                Position = pos
            };

            Ghosts[pos] = wg;
        }

        private void CopyVisuals(Transform source, Transform dest)
        {
            foreach (Transform child in source)
            {
                // Pomijamy rzeczy, które nie są widoczne lub są logiką gry
                if (child.name.Contains("Collider") || child.name.Contains("Trigger") ||
                    child.GetComponent<Light>() || child.GetComponent<ParticleSystem>() ||
                    child.GetComponent<EnviroAudioSource>())
                    continue;

                MeshFilter sourceMF = child.GetComponent<MeshFilter>();
                MeshRenderer sourceMR = child.GetComponent<MeshRenderer>();

                // Kopiujemy tylko obiekty, które mają wygląd
                if (sourceMF != null && sourceMR != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    copy.transform.localPosition = child.localPosition;
                    copy.transform.localRotation = child.localRotation;
                    copy.transform.localScale = child.localScale;

                    MeshFilter mf = copy.AddComponent<MeshFilter>();
                    mf.sharedMesh = sourceMF.sharedMesh; // Współdzielimy mesh z pamięci gry (oszczędność RAM)

                    MeshRenderer mr = copy.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = sourceMR.sharedMaterials; // Współdzielimy materiały
                }

                // Rekurencja dla dzieci (ważne dla złożonych obiektów jak bazy)
                if (child.childCount > 0)
                {
                    Transform nextDest = dest.Find(child.name);
                    if (nextDest == null)
                    {
                        // Jeśli dziecko nie miało Mesha, ale ma swoje dzieci, tworzymy pusty węzeł, żeby zachować strukturę
                        GameObject empty = new GameObject(child.name);
                        empty.transform.SetParent(dest);
                        empty.transform.localPosition = child.localPosition;
                        empty.transform.localRotation = child.localRotation;
                        empty.transform.localScale = child.localScale;
                        nextDest = empty.transform;
                    }
                    CopyVisuals(child, nextDest);
                }
            }
        }

        private void UpdateVisibility()
        {
            List<Vector3> toRemove = new List<Vector3>();

            foreach (var kvp in Ghosts)
            {
                var ghost = kvp.Value;

                // Jeśli nasz duch został zniszczony (np. przez Clean Cache), usuń z listy
                if (ghost.VisualObj == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Sprawdzamy czy prawdziwy obiekt istnieje i jest aktywny w świecie gry
                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

                if (realIsAlive)
                {
                    // Oryginał jest -> ukryj ducha
                    if (ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(false);
                }
                else
                {
                    // Oryginał zniknął (Culling/Destroy) -> pokaż ducha
                    if (!ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(true);
                }
            }

            foreach (var k in toRemove) Ghosts.Remove(k);
        }

        private bool IsBlacklisted(string name)
        {
            foreach (var key in _blackList)
                if (name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private Vector3 RoundVector(Vector3 v)
        {
            // Zaokrąglamy pozycję do 10cm, żeby wyeliminować błędy float przy ponownym ładowaniu
            return new Vector3(Mathf.Round(v.x * 10f) / 10f, Mathf.Round(v.y * 10f) / 10f, Mathf.Round(v.z * 10f) / 10f);
        }

        public void DrawMenu()
        {
            GUILayout.Label($"<b>Persistent World</b>");
            GUILayout.Label($"Cached Objects: {Ghosts.Count}");

            // Opcja ręcznego czyszczenia, gdyby coś się zbugowało
            if (GUILayout.Button("Clear Cache"))
            {
                foreach (var g in Ghosts.Values) if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
                Ghosts.Clear();
            }
        }
    }
}