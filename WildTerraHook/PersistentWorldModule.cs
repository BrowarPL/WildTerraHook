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
            // Szukamy tylko obiektów statycznych
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

                    // Jeśli obiekt zmienił stan (np. Drzewo -> Pniak), odświeżamy
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

                // Kopiujemy MeshFilter (tylko statyczne, żadnych SkinnedMesh)
                MeshFilter sourceMF = child.GetComponent<MeshFilter>();
                MeshRenderer sourceMR = child.GetComponent<MeshRenderer>();

                if (sourceMF != null && sourceMR != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    CopyTransform(child, copy.transform);

                    var mf = copy.AddComponent<MeshFilter>();
                    mf.sharedMesh = sourceMF.sharedMesh; // Współdzielimy mesh z gry

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
            // Tłumaczenie tytułu
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
                // Tłumaczenie licznika i przycisku
                GUILayout.Label($"{Localization.Get("PERSISTENT_COUNT")}: {ResourceGhosts.Count}");
                if (GUILayout.Button(Localization.Get("PERSISTENT_CLEAR"))) ClearCache();
            }
            else
            {
                // Tłumaczenie statusu wyłączenia
                GUILayout.Label($"<color=grey>{Localization.Get("PERSISTENT_DISABLED")}</color>");
            }
            GUILayout.EndVertical();
        }
    }
}