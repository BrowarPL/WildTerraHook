using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // Używamy Listy dla mobów, aby móc szukać po dystansie, a nie tylko po ID
        public List<WorldGhost> MobGhosts = new List<WorldGhost>();

        // Dla surowców Dictionary jest OK (są statyczne)
        public Dictionary<Vector3, WorldGhost> ResourceGhosts = new Dictionary<Vector3, WorldGhost>();

        private float _scanInterval = 0.15f;
        private float _lastScanTime = 0f;

        // Dystans, przy którym uznajemy, że duch i nowy mob to to samo (np. mob przeszedł kawałek)
        private const float MERGE_DISTANCE = 10.0f;

        // Dystans Fail-Safe: Jeśli gracz jest blisko ducha, a nie ma tam moba -> zostaw.
        // Ale jeśli jest mob -> usuń ducha.
        private const float FAILSAFE_CHECK_DIST = 50.0f;

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
            // Time to live dla duchów mobów - jeśli mob nie był widziany przez X czasu, usuń ducha (żeby nie zaśmiecać mapy starymi pozycjami)
            public float LastSeenTime;
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
                CleanupOldGhosts(); // Nowa funkcja czyszcząca
                _lastScanTime = Time.time;
            }

            UpdateVisibilityAndFailSafe();
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
                    CreateResourceGhost(realObj, pos);
                }
                else
                {
                    var ghost = ResourceGhosts[pos];
                    if (ghost.VisualObj == null) { ResourceGhosts.Remove(pos); continue; }

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

        // --- MOBY (DYNAMICZNE + FAIL SAFE) ---
        private void RefreshMobs()
        {
            var currentMobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();

            foreach (var mob in currentMobs)
            {
                if (mob == null || mob.health <= 0) continue;

                string mobName = mob.name;
                Vector3 mobPos = mob.transform.position;

                // 1. Szukamy ducha tego moba w pobliżu
                // Szukamy ducha o tej samej nazwie w promieniu MERGE_DISTANCE
                var existingGhost = MobGhosts.FirstOrDefault(g =>
                    g.Name == mobName &&
                    Vector3.Distance(g.Position, mobPos) < MERGE_DISTANCE
                );

                if (existingGhost != null)
                {
                    // ZNALEZIONO DUCHA -> AKTUALIZUJEMY GO
                    existingGhost.Position = mobPos;
                    existingGhost.Hp = mob.health;
                    existingGhost.MaxHp = mob.healthMax;
                    existingGhost.RealObj = mob.gameObject;
                    existingGhost.LastSeenTime = Time.time;

                    // Przesuwamy ducha na pozycję moba i ukrywamy go (bo mob jest widoczny)
                    if (existingGhost.VisualObj)
                    {
                        existingGhost.VisualObj.transform.position = mobPos;
                        existingGhost.VisualObj.transform.rotation = mob.transform.rotation;
                        if (existingGhost.VisualObj.activeSelf) existingGhost.VisualObj.SetActive(false);
                    }
                }
                else
                {
                    // NIE ZNALEZIONO -> TWORZYMY NOWEGO
                    CreateMobGhost(mob);
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
                Name = mob.name,
                Position = mob.transform.position,
                IsMob = true,
                Hp = mob.health,
                MaxHp = mob.healthMax,
                LastSeenTime = Time.time
            };
            MobGhosts.Add(wg);
        }

        private void UpdateVisibilityAndFailSafe()
        {
            Vector3 playerPos = Vector3.zero;
            if (Camera.main) playerPos = Camera.main.transform.position;
            else if (global::Player.localPlayer) playerPos = global::Player.localPlayer.transform.position;

            // --- ZASOBY ---
            foreach (var ghost in ResourceGhosts.Values)
            {
                if (!ghost.VisualObj) continue;
                bool realAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);
                ghost.VisualObj.SetActive(!realAlive);
            }

            // --- MOBY (FAIL SAFE) ---
            for (int i = MobGhosts.Count - 1; i >= 0; i--)
            {
                var ghost = MobGhosts[i];
                if (ghost.VisualObj == null)
                {
                    MobGhosts.RemoveAt(i);
                    continue;
                }

                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

                if (realIsAlive)
                {
                    // Jeśli oryginał żyje -> ukryj ducha, zaktualizuj pozycję
                    if (ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(false);
                    ghost.VisualObj.transform.position = ghost.RealObj.transform.position;
                    ghost.Position = ghost.RealObj.transform.position;
                }
                else
                {
                    // Oryginał NIE żyje (lub jest poza siecią).

                    // FAIL SAFE: Sprawdźmy, czy gracz jest blisko ducha
                    float distToPlayer = Vector3.Distance(playerPos, ghost.Position);

                    if (distToPlayer < FAILSAFE_CHECK_DIST)
                    {
                        // Jesteśmy blisko miejsca, gdzie powinien być duch.
                        // Jeśli w pobliżu jest JAKIKOLWIEK żywy mob o tej samej nazwie, to znaczy, 
                        // że nasz duch jest błędem (duplikatem) i należy go usunąć.

                        bool duplicateDetected = false;
                        var nearbyMobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
                        foreach (var m in nearbyMobs)
                        {
                            if (m.name == ghost.Name && Vector3.Distance(m.transform.position, ghost.Position) < MERGE_DISTANCE)
                            {
                                duplicateDetected = true;
                                break;
                            }
                        }

                        if (duplicateDetected)
                        {
                            // Wykryto żywego moba obok ducha -> usuwamy ducha
                            UnityEngine.Object.Destroy(ghost.VisualObj);
                            MobGhosts.RemoveAt(i);
                            continue;
                        }
                    }

                    // Jeśli nie ma duplikatu, pokazujemy ducha (ostatnia znana pozycja)
                    if (!ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(true);
                }
            }
        }

        private void CleanupOldGhosts()
        {
            // Opcjonalne: Usuwanie duchów mobów, których nie widzieliśmy od np. 5 minut
            // Moby wędrują, więc duch sprzed 10 minut jest mylący.
            float expireTime = 300f; // 5 minut
            for (int i = MobGhosts.Count - 1; i >= 0; i--)
            {
                if (Time.time - MobGhosts[i].LastSeenTime > expireTime)
                {
                    if (MobGhosts[i].VisualObj) UnityEngine.Object.Destroy(MobGhosts[i].VisualObj);
                    MobGhosts.RemoveAt(i);
                }
            }
        }

        private void CopyVisuals(Transform source, Transform dest)
        {
            foreach (Transform child in source)
            {
                if (IsBlacklisted(child.name) || child.GetComponent<Light>() || child.GetComponent<ParticleSystem>())
                    continue;

                // Kopiujemy MeshFilter (Zasoby)
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

                // Kopiujemy SkinnedMesh (Moby) -> Konwersja na statyczny Mesh
                // To naprawia problem "dziwnych shaderów" i chamsów, bo renderujemy statyczną klatkę
                SkinnedMeshRenderer smr = child.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    CopyTransform(child, copy.transform);

                    // Zamiast SkinnedMeshRenderer, dodajemy zwykły MeshFilter+Renderer
                    // Używamy sharedMesh, który jest w T-Pose (pozycja domyślna).
                    // Nie robimy BakeMesh(), bo to zjada FPS. T-Pose wystarczy do zaznaczenia pozycji.
                    var mf = copy.AddComponent<MeshFilter>();
                    mf.sharedMesh = smr.sharedMesh;

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
            GUILayout.Label($"<b>Persistent World (Fail-Safe Active)</b>");

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
                GUILayout.Label($"Mobs Ghosts: {MobGhosts.Count}");
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