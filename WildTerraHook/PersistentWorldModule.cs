using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // Rozdzielamy przechowywanie - Surowce są statyczne (klucz=pozycja), Moby dynamiczne (lista)
        public Dictionary<Vector3, WorldGhost> ResourceGhosts = new Dictionary<Vector3, WorldGhost>();
        public List<WorldGhost> MobGhosts = new List<WorldGhost>();

        private float _scanInterval = 0.1f; // Bardzo szybkie skanowanie dla płynności mobów
        private float _lastScanTime = 0f;

        private string[] _blackList = { "FX_", "Particle", "LightSource", "Sound", "Audio", "Arrow", "Projectile" };

        public class WorldGhost // Zmieniono na klasę dla łatwiejszej edycji referencji
        {
            public GameObject VisualObj;
            public GameObject RealObj;
            public string Name;
            public Vector3 Position;
            public bool IsMob;
            public int Hp;
            public int MaxHp;
            public int RealInstanceID; // Do śledzenia unikalności żywych obiektów
        }

        public void Update()
        {
            if (!ConfigManager.Persistent_Enabled)
            {
                if (ResourceGhosts.Count > 0 || MobGhosts.Count > 0) ClearCache();
                return;
            }

            if (Time.time - _lastScanTime > _scanInterval)
            {
                RefreshResources();
                RefreshMobs();
                _lastScanTime = Time.time;
            }

            UpdateVisibility();
        }

        public void ClearCache()
        {
            foreach (var g in ResourceGhosts.Values) if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
            ResourceGhosts.Clear();

            foreach (var g in MobGhosts) if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
            MobGhosts.Clear();
        }

        // --- ZASOBY (STATYCZNE) ---
        private void RefreshResources()
        {
            var currentObjects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
            foreach (var realObj in currentObjects)
            {
                if (realObj == null) continue;
                if (IsBlacklisted(realObj.name)) continue;

                Vector3 pos = RoundVector(realObj.transform.position);

                if (!ResourceGhosts.ContainsKey(pos))
                {
                    // Nowy zasób
                    CreateResourceGhost(realObj, pos);
                }
                else
                {
                    // Istniejący zasób - aktualizuj referencję
                    var ghost = ResourceGhosts[pos];
                    string cleanReal = realObj.name.Replace("(Clone)", "").Trim();
                    string cleanGhost = ghost.Name.Replace("(Clone)", "").Trim();

                    // Jeśli zmienił się typ (np. drzewo -> pień), przerysuj
                    if (cleanGhost != cleanReal)
                    {
                        if (ghost.VisualObj) UnityEngine.Object.Destroy(ghost.VisualObj);
                        ResourceGhosts.Remove(pos);
                        CreateResourceGhost(realObj, pos);
                    }
                    else
                    {
                        ghost.RealObj = realObj.gameObject;
                    }
                }
            }
        }

        private void CreateResourceGhost(global::WTObject original, Vector3 pos)
        {
            GameObject ghostGo = new GameObject($"[CACHE_RES] {original.name}");
            ghostGo.transform.position = original.transform.position;
            ghostGo.transform.rotation = original.transform.rotation;
            ghostGo.transform.localScale = original.transform.localScale;

            CopyVisuals(original.transform, ghostGo.transform);
            ghostGo.SetActive(false);

            WorldGhost wg = new WorldGhost
            {
                VisualObj = ghostGo,
                RealObj = original.gameObject,
                Name = original.name,
                Position = pos,
                IsMob = false
            };
            ResourceGhosts[pos] = wg;
        }

        // --- MOBY (DYNAMICZNE) ---
        private void RefreshMobs()
        {
            var currentMobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();

            foreach (var mob in currentMobs)
            {
                if (mob == null || mob.health <= 0) continue;

                int currentId = mob.gameObject.GetInstanceID();

                // 1. Sprawdź czy już śledzimy tego konkretnego moba (po InstanceID)
                var existingGhost = MobGhosts.FirstOrDefault(g => g.RealInstanceID == currentId);

                if (existingGhost != null)
                {
                    // Aktualizujemy pozycję i HP istniejącego ducha
                    existingGhost.Position = mob.transform.position;
                    existingGhost.Hp = mob.health;
                    existingGhost.MaxHp = mob.healthMax;

                    if (existingGhost.VisualObj)
                    {
                        existingGhost.VisualObj.transform.position = mob.transform.position;
                        existingGhost.VisualObj.transform.rotation = mob.transform.rotation;
                    }
                    existingGhost.RealObj = mob.gameObject; // Odśwież referencję
                }
                else
                {
                    // 2. To nowy obiekt (dla silnika Unity). Sprawdźmy czy mamy "osieroconego" ducha w pobliżu
                    // który pasuje nazwą (np. gracz wrócił w to miejsce i serwer zespawnował moba na nowo)
                    var orphan = MobGhosts.FirstOrDefault(g =>
                        g.RealObj == null && // Duch bez właściciela
                        Vector3.Distance(g.Position, mob.transform.position) < 5.0f && // Blisko (5m tolerancji)
                        g.Name == mob.name // Ta sama nazwa
                    );

                    if (orphan != null)
                    {
                        // Znaleziono ducha! Podpinamy go pod nowego moba
                        orphan.RealObj = mob.gameObject;
                        orphan.RealInstanceID = currentId;
                        orphan.Hp = mob.health;

                        // Przesuwamy ducha na aktualną pozycję
                        if (orphan.VisualObj)
                        {
                            orphan.VisualObj.transform.position = mob.transform.position;
                            orphan.VisualObj.transform.rotation = mob.transform.rotation;
                        }
                    }
                    else
                    {
                        // 3. Nie znaleziono ani ID, ani pasującego ducha. Tworzymy nowego.
                        CreateMobGhost(mob);
                    }
                }
            }
        }

        private void CreateMobGhost(global::WTMob mob)
        {
            GameObject ghostGo = new GameObject($"[CACHE_MOB] {mob.name}");
            ghostGo.transform.position = mob.transform.position;
            ghostGo.transform.rotation = mob.transform.rotation;
            ghostGo.transform.localScale = mob.transform.localScale;

            CopyVisuals(mob.transform, ghostGo.transform);
            ghostGo.SetActive(false);

            WorldGhost wg = new WorldGhost
            {
                VisualObj = ghostGo,
                RealObj = mob.gameObject,
                RealInstanceID = mob.gameObject.GetInstanceID(),
                Name = mob.name,
                Position = mob.transform.position,
                IsMob = true,
                Hp = mob.health,
                MaxHp = mob.healthMax
            };
            MobGhosts.Add(wg);
        }

        // --- WSPÓLNE ---

        private void CopyVisuals(Transform source, Transform dest)
        {
            foreach (Transform child in source)
            {
                if (IsBlacklisted(child.name) || child.GetComponent<Light>() || child.GetComponent<ParticleSystem>())
                    continue;

                // MeshFilter
                MeshFilter sourceMF = child.GetComponent<MeshFilter>();
                MeshRenderer sourceMR = child.GetComponent<MeshRenderer>();
                if (sourceMF != null && sourceMR != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    CopyTransform(child, copy.transform);
                    var mf = copy.AddComponent<MeshFilter>(); mf.sharedMesh = sourceMF.sharedMesh;
                    var mr = copy.AddComponent<MeshRenderer>(); mr.sharedMaterials = sourceMR.sharedMaterials;
                }

                // SkinnedMesh (Moby) -> Zamiana na statyczny Mesh dla ducha
                SkinnedMeshRenderer smr = child.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    CopyTransform(child, copy.transform);
                    var mf = copy.AddComponent<MeshFilter>(); mf.sharedMesh = smr.sharedMesh;
                    var mr = copy.AddComponent<MeshRenderer>(); mr.sharedMaterials = smr.sharedMaterials;
                }

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
            // Zasoby
            List<Vector3> resToRemove = new List<Vector3>();
            foreach (var kvp in ResourceGhosts)
            {
                var ghost = kvp.Value;
                if (ghost.VisualObj == null) { resToRemove.Add(kvp.Key); continue; }

                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);
                ghost.VisualObj.SetActive(!realIsAlive);
            }
            foreach (var k in resToRemove) ResourceGhosts.Remove(k);

            // Moby
            for (int i = MobGhosts.Count - 1; i >= 0; i--)
            {
                var ghost = MobGhosts[i];
                if (ghost.VisualObj == null) { MobGhosts.RemoveAt(i); continue; }

                // Jeśli referencja jest null (Destroyed) lub nieaktywna (Culling/Disable) -> Pokaż Ducha
                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

                ghost.VisualObj.SetActive(!realIsAlive);

                // Jeśli duch jest aktywny (brak oryginału), upewnij się, że ma pozycję ostatnio widzianą
                if (!realIsAlive)
                {
                    ghost.VisualObj.transform.position = ghost.Position;
                }
            }
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
            GUILayout.Label($"<b>Persistent World</b>");

            bool newEnabled = GUILayout.Toggle(ConfigManager.Persistent_Enabled, " Enable Persistent Cache");
            if (newEnabled != ConfigManager.Persistent_Enabled)
            {
                ConfigManager.Persistent_Enabled = newEnabled;
                ConfigManager.Save();
                if (!ConfigManager.Persistent_Enabled) ClearCache();
            }

            if (ConfigManager.Persistent_Enabled)
            {
                GUILayout.Label($"Resources: {ResourceGhosts.Count}");
                GUILayout.Label($"Mobs: {MobGhosts.Count}");
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