using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mirror;

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // TYLKO ZASOBY (Statyczne)
        public Dictionary<Vector3, WorldGhost> ResourceGhosts = new Dictionary<Vector3, WorldGhost>();

        private float _scanInterval = 0.25f;
        private float _lastScanTime = 0f;

        // Czarna lista śmieci (efekty, dźwięki)
        private string[] _blackList = { "FX_", "Particle", "LightSource", "Sound", "Audio", "Arrow", "Projectile", "Footstep", "UI", "Canvas" };

        public class WorldGhost
        {
            public GameObject VisualObj;
            public GameObject RealObj;
            public string Name;
            public Vector3 Position;
        }

        public void Update()
        {
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
            foreach (var g in ResourceGhosts.Values)
            {
                if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
            }
            ResourceGhosts.Clear();
        }

        private void RefreshResources()
        {
            Vector3 playerPos = Vector3.zero;
            if (global::Player.localPlayer != null) playerPos = global::Player.localPlayer.transform.position;
            else if (Camera.main != null) playerPos = Camera.main.transform.position;

            // Szukamy tylko obiektów statycznych
            var currentObjects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
            foreach (var realObj in currentObjects)
            {
                if (realObj == null) continue;

                // --- NAPRAWIONO: USUNIĘTO BŁĘDNY WARUNEK is WTMob ---
                // Ponieważ WTObject nie jest klasą bazową WTMob, ten kod był zbędny i powodował warning.
                // Teraz po prostu ignorujemy to filtrowanie, bo FindObjectsOfType<WTObject> 
                // i tak nie powinno zwrócić mobów (jeśli struktura gry jest poprawna).
                // Jeśli jednak moby są zwracane jako WTObject, dodaj filtr po nazwie lub komponencie:
                if (realObj.GetComponent<global::WTMob>() != null) continue; // Bezpieczny filtr
                // -------------------------

                if (IsBlacklisted(realObj.name)) continue;

                Vector3 pos = RoundVector(realObj.transform.position);

                if (!ResourceGhosts.ContainsKey(pos))
                {
                    CreateResourceGhost(realObj, pos);
                }
                else
                {
                    var ghost = ResourceGhosts[pos];

                    // --- CLEANUP LOGIC: CZYSZCZENIE ZEBRANYCH ---
                    // Jeśli duch istnieje, ale realny obiekt zniknął (jest null)
                    if (ghost.RealObj == null)
                    {
                        float dist = Vector3.Distance(playerPos, ghost.Position);
                        // Jeśli jesteśmy BLISKO (np. < 20m) i obiektu nie ma -> Został zebrany -> Usuń ducha
                        if (dist < ConfigManager.Persistent_CleanupRange)
                        {
                            if (ghost.VisualObj) UnityEngine.Object.Destroy(ghost.VisualObj);
                            ResourceGhosts.Remove(pos);
                            continue;
                        }
                        // Jeśli jesteśmy DALEKO -> Zostaw ducha (Culling)
                    }

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

            // Dodatkowe czyszczenie dla duchów, których RealObj zniknął (a pętla wyżej ich nie złapała)
            List<Vector3> toClean = new List<Vector3>();
            foreach (var kvp in ResourceGhosts)
            {
                var ghost = kvp.Value;
                if (ghost.RealObj == null) // Obiekt zniknął z gry
                {
                    float dist = Vector3.Distance(playerPos, ghost.Position);
                    if (dist < ConfigManager.Persistent_CleanupRange)
                    {
                        if (ghost.VisualObj) UnityEngine.Object.Destroy(ghost.VisualObj);
                        toClean.Add(kvp.Key);
                    }
                }
            }
            foreach (var k in toClean) ResourceGhosts.Remove(k);
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

                MeshFilter sourceMF = child.GetComponent<MeshFilter>();
                MeshRenderer sourceMR = child.GetComponent<MeshRenderer>();

                if (sourceMF != null && sourceMR != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    CopyTransform(child, copy.transform);

                    var mf = copy.AddComponent<MeshFilter>();
                    mf.sharedMesh = sourceMF.sharedMesh;

                    var mr = copy.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = sourceMR.sharedMaterials;
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
            List<Vector3> toRemove = new List<Vector3>();

            foreach (var kvp in ResourceGhosts)
            {
                var ghost = kvp.Value;
                if (ghost.VisualObj == null) { toRemove.Add(kvp.Key); continue; }

                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

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
            GUILayout.Label($"<b>{Localization.Get("PERSISTENT_TITLE")}</b>");

            bool newEnabled = GUILayout.Toggle(ConfigManager.Persistent_Enabled, " " + Localization.Get("PERSISTENT_ENABLE"));
            if (newEnabled != ConfigManager.Persistent_Enabled)
            {
                ConfigManager.Persistent_Enabled = newEnabled;
                ConfigManager.Save();
                if (!ConfigManager.Persistent_Enabled) ClearCache();
            }

            if (ConfigManager.Persistent_Enabled)
            {
                GUILayout.Label($"{Localization.Get("PERSISTENT_COUNT")}: {ResourceGhosts.Count}");

                // --- SUWAK ZASIĘGU CZYSZCZENIA ---
                GUILayout.Space(5);
                GUILayout.Label($"{Localization.Get("PERSISTENT_CLEANUP")}: {ConfigManager.Persistent_CleanupRange:F0}m");
                float newRange = GUILayout.HorizontalSlider(ConfigManager.Persistent_CleanupRange, 5.0f, 50.0f);
                if (Mathf.Abs(newRange - ConfigManager.Persistent_CleanupRange) > 1.0f)
                {
                    ConfigManager.Persistent_CleanupRange = newRange;
                    ConfigManager.Save();
                }
                // ---------------------------------

                if (GUILayout.Button(Localization.Get("PERSISTENT_CLEAR"))) ClearCache();
            }
            else
            {
                GUILayout.Label($"<color=grey>{Localization.Get("PERSISTENT_DISABLED")}</color>");
            }
            GUILayout.EndVertical();
        }
    }
}