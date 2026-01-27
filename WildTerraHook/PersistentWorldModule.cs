using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // Konfiguracja
        private float _scanInterval = 0.5f;
        private float _lastScanTime = 0f;
        private float _cleanupDistance = 500f; // Usuwaj duchy dopiero jak odejdziemy BARDZO daleko (lub nigdy)

        // Magazyn Duchów: Pozycja -> Dane Ducha
        private Dictionary<Vector3, WorldGhost> _ghosts = new Dictionary<Vector3, WorldGhost>();

        // Lista ignorowanych słów (żeby nie kopiować graczy, mobów, efektów)
        private string[] _validKeywords = { "Tree", "Rock", "Ore", "Deposit", "Mushroom", "Bush", "Stump", "Log" };

        private struct WorldGhost
        {
            public GameObject VisualObj; // Nasza kopia (tylko wygląd)
            public GameObject RealObj;   // Referencja do oryginału (może być null jeśli zniknął)
            public string Name;
        }

        public void Update()
        {
            if (Time.time - _lastScanTime > _scanInterval)
            {
                RefreshWorld();
                _lastScanTime = Time.time;
            }

            // Zarządzanie widocznością co klatkę (lub rzadziej dla optymalizacji)
            UpdateVisibility();
        }

        private void RefreshWorld()
        {
            // 1. Znajdź wszystkie aktywne obiekty WTObject (te z serwera)
            var currentObjects = Object.FindObjectsOfType<WTObject>();

            foreach (var realObj in currentObjects)
            {
                if (realObj == null) continue;

                // Filtrowanie: tylko surowce, pomijamy graczy i moby
                if (!IsValidTarget(realObj.name)) continue;

                Vector3 pos = RoundVector(realObj.transform.position);

                // 2. Jeśli nie mamy ducha w tym miejscu -> Tworzymy go
                if (!_ghosts.ContainsKey(pos))
                {
                    CreateGhost(realObj, pos);
                }
                else
                {
                    // 3. Jeśli mamy ducha, ale referencja do oryginału wygasła (bo serwer go zespawnował na nowo), odnawiamy link
                    var ghost = _ghosts[pos];
                    if (ghost.RealObj == null)
                    {
                        // Sprawdzamy czy stan się zmienił (np. drzewo -> pień)
                        // Jeśli nazwy są różne, to znaczy że obiekt zmienił stan. Niszczymy starego ducha i robimy nowego.
                        if (ghost.Name != realObj.name)
                        {
                            Object.Destroy(ghost.VisualObj);
                            _ghosts.Remove(pos);
                            CreateGhost(realObj, pos);
                        }
                        else
                        {
                            // Tylko aktualizujemy referencję
                            ghost.RealObj = realObj.gameObject;
                            _ghosts[pos] = ghost;
                        }
                    }
                }
            }
        }

        private void CreateGhost(WTObject original, Vector3 pos)
        {
            // Tworzymy pusty obiekt kontenera
            GameObject ghostGo = new GameObject($"GHOST_{original.name}");
            ghostGo.transform.position = original.transform.position;
            ghostGo.transform.rotation = original.transform.rotation;
            ghostGo.transform.localScale = original.transform.localScale;

            // Kopiujemy hierarchię wizualną (Meshe)
            CopyVisuals(original.transform, ghostGo.transform);

            // Domyślnie ukrywamy ducha, bo oryginał stoi obok
            ghostGo.SetActive(false);

            WorldGhost wg = new WorldGhost
            {
                VisualObj = ghostGo,
                RealObj = original.gameObject,
                Name = original.name
            };

            _ghosts[pos] = wg;
        }

        // Rekurencyjne kopiowanie tylko elementów wizualnych
        private void CopyVisuals(Transform source, Transform destinationParent)
        {
            foreach (Transform child in source)
            {
                // Pomijamy elementy UI, światła, kolidery, partyzany itp.
                if (child.GetComponent<ParticleSystem>()) continue;
                if (child.GetComponent<Light>()) continue;
                if (child.name.Contains("Collider")) continue;

                MeshFilter sourceMF = child.GetComponent<MeshFilter>();
                MeshRenderer sourceMR = child.GetComponent<MeshRenderer>();

                if (sourceMF != null && sourceMR != null)
                {
                    GameObject childGhost = new GameObject(child.name);
                    childGhost.transform.SetParent(destinationParent);
                    childGhost.transform.localPosition = child.localPosition;
                    childGhost.transform.localRotation = child.localRotation;
                    childGhost.transform.localScale = child.localScale;

                    MeshFilter mf = childGhost.AddComponent<MeshFilter>();
                    mf.sharedMesh = sourceMF.sharedMesh; // Używamy sharedMesh, żeby nie duplikować danych w pamięci!

                    MeshRenderer mr = childGhost.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = sourceMR.sharedMaterials; // To samo dla materiałów
                }

                // Rekurencja dla dzieci (np. gałęzie drzewa)
                if (child.childCount > 0)
                {
                    // Jeśli nie skopiowaliśmy tego obiektu (bo nie miał mesha), tworzymy pusty węzeł dla struktury
                    Transform nextDest = destinationParent.Find(child.name);
                    if (nextDest == null)
                    {
                        GameObject emptyNode = new GameObject(child.name);
                        emptyNode.transform.SetParent(destinationParent);
                        emptyNode.transform.localPosition = child.localPosition;
                        emptyNode.transform.localRotation = child.localRotation;
                        emptyNode.transform.localScale = child.localScale;
                        nextDest = emptyNode.transform;
                    }
                    CopyVisuals(child, nextDest);
                }
            }
        }

        private void UpdateVisibility()
        {
            List<Vector3> toRemove = new List<Vector3>();
            Vector3 playerPos = Camera.main ? Camera.main.transform.position : Vector3.zero;

            foreach (var kvp in _ghosts)
            {
                var ghost = kvp.Value;

                if (ghost.VisualObj == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Logika: Jeśli RealObj istnieje (jest w zasięgu sieci), ukryj ducha.
                // Jeśli RealObj jest null (zniszczony przez sieć), pokaż ducha.
                bool realExists = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

                if (realExists)
                {
                    if (ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(false);
                }
                else
                {
                    if (!ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(true);
                }

                // Opcjonalne czyszczenie bardzo dalekich duchów (RAM management)
                if (Vector3.Distance(playerPos, kvp.Key) > _cleanupDistance)
                {
                    Object.Destroy(ghost.VisualObj);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove) _ghosts.Remove(key);
        }

        private bool IsValidTarget(string name)
        {
            // Wykluczamy wszystko co nie jest surowcem
            foreach (var key in _validKeywords)
            {
                if (name.Contains(key)) return true;
            }
            return false;
        }

        // Zaokrąglanie pozycji, aby zniwelować mikro-przesunięcia i dobrze kluczować słownik
        private Vector3 RoundVector(Vector3 v)
        {
            return new Vector3(
                Mathf.Round(v.x * 100f) / 100f,
                Mathf.Round(v.y * 100f) / 100f,
                Mathf.Round(v.z * 100f) / 100f
            );
        }

        public void DrawMenu()
        {
            GUILayout.Label($"Cached Objects: {_ghosts.Count}");
            if (GUILayout.Button("Clear Cache"))
            {
                foreach (var g in _ghosts.Values) Object.Destroy(g.VisualObj);
                _ghosts.Clear();
            }
        }
    }
}