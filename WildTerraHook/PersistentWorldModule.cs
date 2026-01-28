using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mirror;

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // Listy obiektów
        public List<WorldGhost> MobGhosts = new List<WorldGhost>();
        public Dictionary<Vector3, WorldGhost> ResourceGhosts = new Dictionary<Vector3, WorldGhost>();

        private float _scanInterval = 0.1f;
        private float _cleanupInterval = 1.0f;
        private float _lastScanTime = 0f;
        private float _lastCleanupTime = 0f;

        // Dystans, w jakim duch "rozpoznaje" swojego właściciela po powrocie
        // Zwiększono do 20m, aby złapać moby, które się przesunęły/zrespawnowały kawałek dalej
        private const float REATTACH_DISTANCE = 20.0f;

        private string[] _blackList = { "FX_", "Particle", "LightSource", "Sound", "Audio", "Arrow", "Projectile", "Footstep", "UI" };

        public class WorldGhost
        {
            public GameObject VisualObj; // Nasza kopia wizualna
            public GameObject RealObj;   // Oryginał (może być null, jeśli zniknął)
            public string Name;
            public Vector3 Position;     // Ostatnia znana pozycja
            public bool IsMob;
            public int Hp;
            public int MaxHp;
        }

        public void Update()
        {
            if (!ConfigManager.Persistent_Enabled)
            {
                if (ResourceGhosts.Count > 0 || MobGhosts.Count > 0) ClearCache();
                return;
            }

            // Częste skanowanie świata (Update referencji)
            if (Time.time - _lastScanTime > _scanInterval)
            {
                RefreshResources();
                RefreshMobs(); // Tu jest główna naprawa ghostingu
                _lastScanTime = Time.time;
            }

            // Rzadsze czyszczenie śmieci (Fail-Safe)
            if (Time.time - _lastCleanupTime > _cleanupInterval)
            {
                RemoveDuplicates();
                _lastCleanupTime = Time.time;
            }

            UpdateVisibilityAndPosition();
        }

        public void ClearCache()
        {
            foreach (var g in ResourceGhosts.Values) if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
            ResourceGhosts.Clear();

            foreach (var g in MobGhosts) if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
            MobGhosts.Clear();
        }

        // --- ZASOBY (Prosta logika, bo stoją w miejscu) ---
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
                    CreateResourceGhost(realObj, pos);
                }
                else
                {
                    var ghost = ResourceGhosts[pos];
                    if (ghost.VisualObj == null) { ResourceGhosts.Remove(pos); continue; }

                    // Jeśli w tym samym miejscu zmienił się obiekt (np. Drzewo -> Pniak)
                    string cleanReal = realObj.name.Replace("(Clone)", "").Trim();
                    string cleanGhost = ghost.Name.Replace("(Clone)", "").Trim();

                    if (cleanGhost != cleanReal)
                    {
                        UnityEngine.Object.Destroy(ghost.VisualObj);
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
            GameObject ghostGo = new GameObject($"[G_RES] {original.name}");
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

        // --- MOBY (Logika Snajperska v2 - Reattach) ---
        private void RefreshMobs()
        {
            var currentMobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();

            foreach (var mob in currentMobs)
            {
                if (mob == null || mob.health <= 0) continue;

                // Szukamy, czy ten mob jest już śledzony (po GameObject)
                var trackedGhost = MobGhosts.FirstOrDefault(g => g.RealObj == mob.gameObject);

                if (trackedGhost != null)
                {
                    // 1. Już go mamy - aktualizujemy HP
                    trackedGhost.Hp = mob.health;
                    trackedGhost.MaxHp = mob.healthMax;
                }
                else
                {
                    // 2. To "nowy" obiekt dla Unity. Sprawdźmy, czy mamy "wolnego" ducha w pobliżu.
                    // To jest klucz do eliminacji ghostingu przy powrocie do strefy.
                    var orphanGhost = MobGhosts.FirstOrDefault(g =>
                        (g.RealObj == null || !g.RealObj.activeInHierarchy) && // Duch nie ma aktywnego właściciela
                        g.Name == mob.name && // Ta sama nazwa
                        Vector3.Distance(g.Position, mob.transform.position) < REATTACH_DISTANCE // Jest blisko
                    );

                    if (orphanGhost != null)
                    {
                        // ZNALEZIONO STAREGO DUCHA! Podpinamy go pod nowego moba.
                        orphanGhost.RealObj = mob.gameObject;
                        orphanGhost.Hp = mob.health;
                        orphanGhost.MaxHp = mob.healthMax;

                        // Przesuwamy ducha na pozycję moba
                        if (orphanGhost.VisualObj)
                        {
                            orphanGhost.VisualObj.transform.position = mob.transform.position;
                            orphanGhost.VisualObj.transform.rotation = mob.transform.rotation;
                        }
                    }
                    else
                    {
                        // Nie znaleziono pasującego ducha -> dopiero teraz tworzymy nowego
                        CreateMobGhost(mob);
                    }
                }
            }
        }

        private void CreateMobGhost(global::WTMob mob)
        {
            GameObject ghostGo = new GameObject($"[G_MOB] {mob.name}");
            ghostGo.transform.position = mob.transform.position;
            ghostGo.transform.rotation = mob.transform.rotation;
            ghostGo.transform.localScale = mob.transform.localScale;

            CopyVisuals(mob.transform, ghostGo.transform);
            ghostGo.SetActive(false);

            WorldGhost wg = new WorldGhost
            {
                VisualObj = ghostGo,
                RealObj = mob.gameObject,
                Name = mob.name,
                Position = mob.transform.position,
                IsMob = true,
                Hp = mob.health,
                MaxHp = mob.healthMax
            };
            MobGhosts.Add(wg);
        }

        // --- ZARZĄDZANIE WIDOCZNOŚCIĄ I POZYCJĄ ---
        private void UpdateVisibilityAndPosition()
        {
            // Zasoby (Proste on/off)
            foreach (var ghost in ResourceGhosts.Values)
            {
                if (!ghost.VisualObj) continue;
                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);
                if (ghost.VisualObj.activeSelf == realIsAlive) ghost.VisualObj.SetActive(!realIsAlive);
            }

            // Moby (Synchronizacja pozycji)
            for (int i = MobGhosts.Count - 1; i >= 0; i--)
            {
                var ghost = MobGhosts[i];
                if (ghost.VisualObj == null) { MobGhosts.RemoveAt(i); continue; }

                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

                if (realIsAlive)
                {
                    // JEŚLI MOB ŻYJE:
                    // 1. Ukryj ducha.
                    if (ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(false);

                    // 2. CRITICAL: Duch musi PODĄŻAĆ za mobem, nawet jak jest ukryty.
                    // Dzięki temu, gdy mob zniknie (Culling), duch będzie idealnie w tym miejscu.
                    ghost.VisualObj.transform.position = ghost.RealObj.transform.position;
                    ghost.VisualObj.transform.rotation = ghost.RealObj.transform.rotation;
                    ghost.Position = ghost.RealObj.transform.position; // Zapisz pozycję w danych
                }
                else
                {
                    // JEŚLI MOB ZNIKNĄŁ:
                    // Pokaż ducha w ostatniej znanej pozycji.
                    if (!ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(true);
                }
            }
        }

        // --- FAIL-SAFE: USUWANIE DUPLIKATÓW ---
        private void RemoveDuplicates()
        {
            // Jeśli mamy więcej niż 1 ducha o tej samej nazwie w promieniu 1 metra -> usuń
            // To naprawia sytuację, gdy mechanizm Reattach zawiedzie.

            HashSet<WorldGhost> toDelete = new HashSet<WorldGhost>();

            for (int i = 0; i < MobGhosts.Count; i++)
            {
                var g1 = MobGhosts[i];
                if (toDelete.Contains(g1)) continue;

                for (int j = i + 1; j < MobGhosts.Count; j++)
                {
                    var g2 = MobGhosts[j];
                    if (toDelete.Contains(g2)) continue;

                    if (g1.Name == g2.Name && Vector3.Distance(g1.Position, g2.Position) < 2.0f)
                    {
                        // Duplikat wykryty! Usuwamy ten, który nie ma RealObj (lub dowolny)
                        if (g1.RealObj == null) toDelete.Add(g1);
                        else toDelete.Add(g2);
                    }
                }
            }

            foreach (var g in toDelete)
            {
                if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
                MobGhosts.Remove(g);
            }
        }

        private void CopyVisuals(Transform source, Transform dest)
        {
            foreach (Transform child in source)
            {
                if (IsBlacklisted(child.name) || child.GetComponent<Light>() || child.GetComponent<ParticleSystem>())
                    continue;

                // Standardowy Mesh
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

                // Skinned Mesh (Moby) -> Konwersja na statyczny Mesh
                SkinnedMeshRenderer smr = child.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    CopyTransform(child, copy.transform);

                    var mf = copy.AddComponent<MeshFilter>();
                    mf.sharedMesh = smr.sharedMesh; // T-Pose mesh (najbezpieczniejszy)

                    var mr = copy.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = smr.sharedMaterials;
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
            GUILayout.Label($"<b>Persistent World (V3)</b>");

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
                if (GUILayout.Button("Force Cleanup Duplicates")) RemoveDuplicates();
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