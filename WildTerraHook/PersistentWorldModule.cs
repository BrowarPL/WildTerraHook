using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WildTerraHook
{
    public class PersistentWorldModule
    {
        // Korzystamy teraz z globalnego ustawienia
        // public bool Enabled = true; <- USUNIĘTE

        public Dictionary<Vector3, WorldGhost> Ghosts = new Dictionary<Vector3, WorldGhost>();

        private float _scanInterval = 0.25f;
        private float _lastScanTime = 0f;

        // Czarna lista dla śmieci wizualnych
        private string[] _blackList = { "FX_", "Particle", "LightSource", "Sound", "Audio" };

        public struct WorldGhost
        {
            public GameObject VisualObj;
            public GameObject RealObj;
            public string Name;
            public Vector3 Position;
            public bool IsMob;
            public int Hp;
            public int MaxHp;
        }

        public void Update()
        {
            // Sprawdzamy ustawienie z ConfigManager
            if (!ConfigManager.Persistent_Enabled)
            {
                if (Ghosts.Count > 0) ClearCache();
                return;
            }

            if (Time.time - _lastScanTime > _scanInterval)
            {
                RefreshWorld();
                _lastScanTime = Time.time;
            }

            UpdateVisibility();
        }

        public void ClearCache()
        {
            foreach (var g in Ghosts.Values)
            {
                if (g.VisualObj) UnityEngine.Object.Destroy(g.VisualObj);
            }
            Ghosts.Clear();
        }

        private void RefreshWorld()
        {
            var currentObjects = UnityEngine.Object.FindObjectsOfType<global::WTObject>();
            foreach (var realObj in currentObjects)
            {
                if (realObj == null) continue;
                if (IsBlacklisted(realObj.name)) continue;

                Vector3 pos = RoundVector(realObj.transform.position);
                UpdateOrAddGhost(realObj.gameObject, realObj.name, pos, false, 0, 0);
            }

            var currentMobs = UnityEngine.Object.FindObjectsOfType<global::WTMob>();
            foreach (var mob in currentMobs)
            {
                if (mob == null || mob.health <= 0) continue;
                Vector3 pos = RoundVector(mob.transform.position);
                UpdateOrAddGhost(mob.gameObject, mob.name, pos, true, mob.health, mob.healthMax);
            }
        }

        private void UpdateOrAddGhost(GameObject realGo, string name, Vector3 pos, bool isMob, int hp, int maxHp)
        {
            if (!Ghosts.ContainsKey(pos))
            {
                CreateGhost(realGo, name, pos, isMob, hp, maxHp);
            }
            else
            {
                var ghost = Ghosts[pos];
                string cleanReal = name.Replace("(Clone)", "").Trim();
                string cleanGhost = ghost.Name.Replace("(Clone)", "").Trim();

                if (cleanGhost != cleanReal)
                {
                    if (ghost.VisualObj) UnityEngine.Object.Destroy(ghost.VisualObj);
                    Ghosts.Remove(pos);
                    CreateGhost(realGo, name, pos, isMob, hp, maxHp);
                }
                else
                {
                    ghost.RealObj = realGo;
                    ghost.Hp = hp;
                    Ghosts[pos] = ghost;
                }
            }
        }

        private void CreateGhost(GameObject original, string name, Vector3 pos, bool isMob, int hp, int maxHp)
        {
            GameObject ghostGo = new GameObject($"[CACHE] {name}");
            ghostGo.transform.position = original.transform.position;
            ghostGo.transform.rotation = original.transform.rotation;
            ghostGo.transform.localScale = original.transform.localScale;

            CopyVisuals(original.transform, ghostGo.transform);

            ghostGo.SetActive(false);

            WorldGhost wg = new WorldGhost
            {
                VisualObj = ghostGo,
                RealObj = original,
                Name = name,
                Position = pos,
                IsMob = isMob,
                Hp = hp,
                MaxHp = maxHp
            };

            Ghosts[pos] = wg;
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

                    MeshFilter mf = copy.AddComponent<MeshFilter>();
                    mf.sharedMesh = sourceMF.sharedMesh;

                    MeshRenderer mr = copy.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = sourceMR.sharedMaterials;
                }

                SkinnedMeshRenderer smr = child.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    GameObject copy = new GameObject(child.name);
                    copy.transform.SetParent(dest);
                    CopyTransform(child, copy.transform);

                    MeshFilter mf = copy.AddComponent<MeshFilter>();
                    mf.sharedMesh = smr.sharedMesh;

                    MeshRenderer mr = copy.AddComponent<MeshRenderer>();
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

        private void UpdateVisibility()
        {
            List<Vector3> toRemove = new List<Vector3>();

            foreach (var kvp in Ghosts)
            {
                var ghost = kvp.Value;

                if (ghost.VisualObj == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                bool realIsAlive = (ghost.RealObj != null && ghost.RealObj.activeInHierarchy);

                if (realIsAlive)
                {
                    if (ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(false);
                }
                else
                {
                    if (!ghost.VisualObj.activeSelf) ghost.VisualObj.SetActive(true);
                }
            }

            foreach (var k in toRemove) Ghosts.Remove(k);
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

            // TICK BOX WYŁĄCZENIA - Korzysta z ConfigManager
            bool newEnabled = GUILayout.Toggle(ConfigManager.Persistent_Enabled, " Enable Persistent Cache");
            if (newEnabled != ConfigManager.Persistent_Enabled)
            {
                ConfigManager.Persistent_Enabled = newEnabled;
                ConfigManager.Save(); // Zapis do pliku od razu po zmianie

                if (!ConfigManager.Persistent_Enabled) ClearCache();
            }

            if (ConfigManager.Persistent_Enabled)
            {
                GUILayout.Label($"Cached Objects: {Ghosts.Count}");
                if (GUILayout.Button("Clear Cache Manually")) ClearCache();
            }
            else
            {
                GUILayout.Label("<color=grey>Module Disabled (Saving RAM)</color>");
            }
            GUILayout.EndVertical();
        }
    }
}