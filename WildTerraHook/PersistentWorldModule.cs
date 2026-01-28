using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mirror; // Konieczne do obsługi NetworkIdentity

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // Zasoby są statyczne -> klucz to Pozycja (Vector3)
        public Dictionary<Vector3, WorldGhost> ResourceGhosts = new Dictionary<Vector3, WorldGhost>();

        // Moby są dynamiczne -> klucz to Network ID (uint)
        public Dictionary<uint, WorldGhost> MobGhosts = new Dictionary<uint, WorldGhost>();

        private float _scanInterval = 0.1f;
        private float _lastScanTime = 0f;

        private string[] _blackList = { "FX_", "Particle", "LightSource", "Sound", "Audio", "Arrow", "Projectile", "Footstep" };

        public class WorldGhost
        {
            public GameObject VisualObj;
            public GameObject RealObj;
            public string Name;
            public Vector3 Position;
            public bool IsMob;
            public int Hp;
            public int MaxHp;
            public uint NetId; // ID sieciowe dla pewności
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

            foreach (var g in MobGhosts.Values) if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
            MobGhosts.Clear();
        }

        // --- ZASOBY (Drzewa, Skały - stoją w miejscu) ---
        private void RefreshResources()
        {
            var currentObjects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
            foreach (var realObj in currentObjects)
            {
                if (realObj == null) continue;
                if (IsBlacklisted(realObj.name)) continue;

                // Używamy pozycji, bo surowce się nie ruszają.
                Vector3 pos = RoundVector(realObj.transform.position);

                if (!ResourceGhosts.ContainsKey(pos))
                {
                    CreateResourceGhost(realObj, pos);
                }
                else
                {
                    var ghost = ResourceGhosts[pos];
                    string cleanReal = realObj.name.Replace("(Clone)", "").Trim();
                    string cleanGhost = ghost.Name.Replace("(Clone)", "").Trim();

                    // Jeśli zmienił się typ obiektu w tym samym miejscu (np. Drzewo -> Pniak)
                    if (cleanGhost != cleanReal)
                    {
                        if (ghost.VisualObj) UnityEngine.Object.Destroy(ghost.VisualObj);
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

        // --- MOBY (Ruszają się - używamy NetworkIdentity) ---
        private void RefreshMobs()
        {
            var currentMobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();

            foreach (var mob in currentMobs)
            {
                if (mob == null || mob.health <= 0) continue;

                // Pobieramy NetworkIdentity - to jest unikalny ID z serwera
                var netIdentity = mob.GetComponent<NetworkIdentity>();
                if (netIdentity == null) continue; // Jeśli nie ma netId, to pewnie jakiś efekt wizualny, pomijamy

                uint netId = netIdentity.netId;

                if (MobGhosts.ContainsKey(netId))
                {
                    // AKTUALIZACJA ISTNIEJĄCEGO DUCHA
                    var ghost = MobGhosts[netId];

                    ghost.Position = mob.transform.position;
                    ghost.Hp = mob.health;
                    ghost.MaxHp = mob.healthMax;
                    ghost.RealObj = mob.gameObject;

                    // Przesuwamy ducha tam gdzie jest mob (żeby był gotowy, jak mob zniknie)
                    if (ghost.VisualObj)
                    {
                        ghost.VisualObj.transform.position = mob.transform.position;
                        ghost.VisualObj.transform.rotation = mob.transform.rotation;
                        // Ukrywamy ducha, bo widzimy oryginał
                        if (ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(false);
                    }
                }
                else
                {
                    // TWORZENIE NOWEGO DUCHA
                    CreateMobGhost(mob, netId);
                }
            }
        }

        private void CreateMobGhost(global::WTMob mob, uint netId)
        {
            GameObject ghostGo = new GameObject($"[CACHE_MOB] {mob.name} [{netId}]");
            ghostGo.transform.position = mob.transform.position;
            ghostGo.transform.rotation = mob.transform.rotation;
            ghostGo.transform.localScale = mob.transform.localScale;

            CopyVisuals(mob.transform, ghostGo.transform);
            ghostGo.SetActive(false); // Ukryty na start

            WorldGhost wg = new WorldGhost
            {
                VisualObj = ghostGo,
                RealObj = mob.gameObject,
                NetId = netId,
                Name = mob.name,
                Position = mob.transform.position,
                IsMob = true,
                Hp = mob.health,
                MaxHp = mob.healthMax
            };
            MobGhosts[netId] = wg;
        }

        // --- WSPÓLNE METODY ---

        private void CopyVisuals(Transform source, Transform dest)
        {
            foreach (Transform child in source)
            {
                if (IsBlacklisted(child.name) || child.GetComponent<Light>() || child.GetComponent<ParticleSystem>())
                    continue;

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
                if (ghost.VisualObj.activeSelf == realIsAlive)
                    ghost.VisualObj.SetActive(!realIsAlive);
            }
            foreach (var k in resToRemove) ResourceGhosts.Remove(k);

            // Moby (Iterujemy po słowniku ID)
            List<uint> mobsToRemove = new List<uint>();
            foreach (var kvp in MobGhosts)
            {
                var ghost = kvp.Value;
                if (ghost.VisualObj == null) { mobsToRemove.Add(kvp.Key); continue; }

                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

                if (realIsAlive)
                {
                    // Oryginał jest -> ukryj ducha, zaktualizuj pozycję ducha do oryginału
                    if (ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(false);
                    ghost.VisualObj.transform.position = ghost.RealObj.transform.position;
                    ghost.VisualObj.transform.rotation = ghost.RealObj.transform.rotation;
                    ghost.Position = ghost.RealObj.transform.position; // Aktualizuj zapamiętaną pozycję
                }
                else
                {
                    // Oryginał zniknął -> pokaż ducha w ostatniej znanej pozycji
                    if (!ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(true);
                }
            }
            foreach (var k in mobsToRemove) MobGhosts.Remove(k);
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