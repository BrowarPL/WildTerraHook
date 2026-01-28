using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mirror;

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // Tylko zasoby i obiekty statyczne
        public Dictionary<Vector3, WorldGhost> ResourceGhosts = new Dictionary<Vector3, WorldGhost>();

        private float _scanInterval = 0.25f;
        private float _lastScanTime = 0f;

        // Filtrowanie śmieci
        private string[] _blackList = { "FX_", "Particle", "LightSource", "Sound", "Audio", "Arrow", "Projectile", "Footstep", "UI" };

        public class WorldGhost
        {
            public GameObject VisualObj; // Kopia wizualna
            public GameObject RealObj;   // Oryginał
            public string Name;
            public Vector3 Position;
        }

        public void Update()
        {
            // Globalny wyłącznik z ConfigManager
            if (!ConfigManager.Persistent_Enabled)
            {
                if (ResourceGhosts.Count > 0) ClearCache();
                return;
            }

            if (Time.time - _lastScanTime > _scanInterval)
            {
                RefreshResources();
                _lastScanTime = Time.time;
            }

            UpdateVisibility();
        }

        public void ClearCache()
        {
            foreach (var g in ResourceGhosts.Values) if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
            ResourceGhosts.Clear();
        }

        // --- ZASOBY (STATYCZNE) ---
        private void RefreshResources()
        {
            var currentObjects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
            foreach (var realObj in currentObjects)
            {
                if (realObj == null) continue;
                if (IsBlacklisted(realObj.name)) continue;

                // Dla obiektów statycznych pozycja jest najlepszym kluczem
                Vector3 pos = RoundVector(realObj.transform.position);

                if (!ResourceGhosts.ContainsKey(pos))
                {
                    CreateResourceGhost(realObj, pos);
                }
                else
                {
                    var ghost = ResourceGhosts[pos];
                    if (ghost.VisualObj == null) { ResourceGhosts.Remove(pos); continue; }

                    // Obsługa zmiany stanu (np. Drzewo -> Pniak)
                    string cleanReal = realObj.name.Replace("(Clone)", "").Trim();
                    string cleanGhost = ghost.Name.Replace("(Clone)", "").Trim();

                    if (cleanGhost != cleanReal)
                    {
                        // Nazwa się zmieniła, przerysowujemy ducha
                        UnityEngine.Object.Destroy(ghost.VisualObj);
                        ResourceGhosts.Remove(pos);
                        CreateResourceGhost(realObj, pos);
                    }
                    else
                    {
                        // Aktualizujemy referencję do żywego obiektu
                        ghost.RealObj = realObj.gameObject;
                    }
                }
            }
        }

        private void CreateResourceGhost(global::WTObject original, Vector3 pos)
        {
            GameObject ghostGo = new GameObject($"[G_RES] {original.name}");
            ghostGo.transform.position = original.transform.position;
            ghostGo.transform.rotation = original.transform.rotation;
            ghostGo.transform.localScale = original.transform.localScale;

            // Kopiujemy tylko statyczne meshe
            CopyVisuals(original.transform, ghostGo.transform);

            // Domyślnie ukryty, bo oryginał jest obok
            ghostGo.SetActive(false);

            WorldGhost wg = new WorldGhost
            {
                VisualObj = ghostGo,
                RealObj = original.gameObject,
                Name = original.name,
                Position = pos
            };
            ResourceGhosts[pos] = wg;
        }

        private void CopyVisuals(Transform source, Transform dest)
        {
            foreach (Transform child in source)
            {
                if (IsBlacklisted(child.name) || child.GetComponent<Light>() || child.GetComponent<ParticleSystem>())
                    continue;

                // Kopiujemy MeshFilter (Zasoby/Budynki)
                MeshFilter sourceMF = child.GetComponent<MeshFilter>();
                MeshRenderer sourceMR = child.GetComponent<MeshRenderer>();

                if (sourceMF != null && sourceMR != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    CopyTransform(child, copy.transform);

                    var mf = copy.AddComponent<MeshFilter>();
                    mf.sharedMesh = sourceMF.sharedMesh; // Oszczędność RAM

                    var mr = copy.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = sourceMR.sharedMaterials;
                }

                // Usunięto obsługę SkinnedMeshRenderer (Moby), aby nie powodować błędów

                if (child.childCount > 0)
                {
                    Transform nextDest = dest.Find(child.name);
                    if (nextDest == null)
                    {
                        GameObject empty = new GameObject(child.name);
                        empty.transform.SetParent(dest);
                        CopyTransform(child, empty.transform);
                        nextDest = empty.transform;
                    }
                    CopyVisuals(child, nextDest);
                }
            }
        }

        private void CopyTransform(Transform source, Transform dest)
        {
            dest.localPosition = source.localPosition;
            dest.localRotation = source.localRotation;
            dest.localScale = source.localScale;
        }

        private void UpdateVisibility()
        {
            List<Vector3> toRemove = new List<Vector3>();

            foreach (var kvp in ResourceGhosts)
            {
                var ghost = kvp.Value;
                if (ghost.VisualObj == null) { toRemove.Add(kvp.Key); continue; }

                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

                // Prosta logika: Jest oryginał -> ukryj ducha. Nie ma -> pokaż ducha.
                if (ghost.VisualObj.activeSelf == realIsAlive)
                    ghost.VisualObj.SetActive(!realIsAlive);
            }

            foreach (var k in toRemove) ResourceGhosts.Remove(k);
        }

        private bool IsBlacklisted(string name)
        {
            foreach (var key in _blackList)
                if (name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.Contains("Collider") || name.Contains("Trigger")) return true;
            return false;
        }

        private Vector3 RoundVector(Vector3 v)
        {
            return new Vector3(Mathf.Round(v.x * 10f) / 10f, Mathf.Round(v.y * 10f) / 10f, Mathf.Round(v.z * 10f) / 10f);
        }

        public void DrawMenu()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>Persistent World (Resources Only)</b>");

            bool newEnabled = GUILayout.Toggle(ConfigManager.Persistent_Enabled, " Enable Persistent Cache");
            if (newEnabled != ConfigManager.Persistent_Enabled)
            {
                ConfigManager.Persistent_Enabled = newEnabled;
                ConfigManager.Save();
                if (!ConfigManager.Persistent_Enabled) ClearCache();
            }

            if (ConfigManager.Persistent_Enabled)
            {
                GUILayout.Label($"Cached Objects: {ResourceGhosts.Count}");
                if (GUILayout.Button("Clear Cache")) ClearCache();
            }
            else
            {
                GUILayout.Label("<color=grey>Module Disabled</color>");
            }
            GUILayout.EndVertical();
        }
    }
}