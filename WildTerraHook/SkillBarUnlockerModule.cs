using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Collections;
using System.Linq;
using Mirror;

namespace WildTerraHook
{
    public class SkillBarUnlockerModule
    {
        private static GameObject _overlayObj;

        public void Update()
        {
            if (ConfigManager.SkillUnlocker_Enabled)
            {
                if (_overlayObj == null)
                {
                    _overlayObj = new GameObject("WTHook_SkillBarOverlay");
                    GameObject.DontDestroyOnLoad(_overlayObj);
                    _overlayObj.AddComponent<SkillBarOverlay>();
                }
            }
            else
            {
                if (_overlayObj != null)
                {
                    GameObject.Destroy(_overlayObj);
                    _overlayObj = null;
                }
            }
        }
    }

    public class SkillBarOverlay : MonoBehaviour
    {
        private Rect _barRect = new Rect(Screen.width / 2 - 250, Screen.height - 230, 100, 115);
        private Rect _selectorRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 250, 400, 500);
        private const int MAX_SLOTS = 20;

        public static string[] SlotSkills = new string[MAX_SLOTS];
        public static KeyCode[] SlotKeys = new KeyCode[MAX_SLOTS];

        private bool _showSelector = false;
        private int _slotToAssign = -1;
        private Vector2 _selectorScroll;
        private int _slotBinding = -1;

        private Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

        void Start()
        {
            LoadData();
        }

        void Update()
        {
            int visibleSlots = Mathf.Clamp(ConfigManager.SkillUnlocker_Slots, 1, MAX_SLOTS);
            if (_slotBinding == -1)
            {
                for (int i = 0; i < visibleSlots; i++)
                {
                    if (SlotKeys[i] != KeyCode.None && Input.GetKeyDown(SlotKeys[i]))
                    {
                        if (!string.IsNullOrEmpty(SlotSkills[i])) SmartCast(SlotSkills[i]);
                    }
                }
            }
        }

        void OnGUI()
        {
            GUI.skin.window.fontSize = 12;
            GUI.skin.button.fontSize = 11;

            int visibleSlots = Mathf.Clamp(ConfigManager.SkillUnlocker_Slots, 1, MAX_SLOTS);
            float slotWidth = 55f;
            float width = (visibleSlots * slotWidth) + 30;
            _barRect.width = width;

            _barRect = GUI.Window(10001, _barRect, DrawBarWindow, $"Virtual Bar ({visibleSlots})");

            if (_showSelector)
            {
                _selectorRect = GUI.Window(10002, _selectorRect, DrawSelectorWindow, $"Wybierz skill (Slot {_slotToAssign + 1})");
                GUI.BringWindowToFront(10002);
            }
        }

        void DrawBarWindow(int id)
        {
            HandleBindingEvent();
            int visibleSlots = Mathf.Clamp(ConfigManager.SkillUnlocker_Slots, 1, MAX_SLOTS);

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            for (int i = 0; i < visibleSlots; i++)
            {
                GUILayout.BeginVertical(GUILayout.Width(50));

                // X
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("X", GUILayout.Height(15))) { SlotSkills[i] = ""; SaveData(); }
                GUI.backgroundColor = Color.white;

                // SKILL
                string skillName = SlotSkills[i];
                bool hasSkill = !string.IsNullOrEmpty(skillName);
                if (_slotBinding == i) GUI.backgroundColor = Color.yellow;
                else GUI.backgroundColor = hasSkill ? Color.white : new Color(0.8f, 0.8f, 0.8f);

                if (GUILayout.Button("", GUILayout.Height(40)))
                {
                    if (!hasSkill) OpenSelector(i);
                    else SmartCast(skillName);
                }
                Rect btnRect = GUILayoutUtility.GetLastRect();
                if (hasSkill)
                {
                    Sprite icon = GetSkillSprite(skillName);
                    if (icon != null) DrawSprite(btnRect, icon);
                    else GUI.Label(btnRect, skillName.Substring(0, Math.Min(3, skillName.Length)), CenteredStyle());
                }
                else { GUI.Label(btnRect, "+", CenteredStyle()); }
                GUI.backgroundColor = Color.white;

                // HOTKEY
                string keyName = (_slotBinding == i) ? "..." : SlotKeys[i].ToString().Replace("Alpha", "");
                if (SlotKeys[i] == KeyCode.None && _slotBinding != i) keyName = "";
                if (GUILayout.Button(keyName, GUILayout.Height(18))) { _slotBinding = i; }

                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        // --- SMART CAST (FINAL VERSION) ---
        void SmartCast(string skillName)
        {
            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;

            // 1. ZNAJDŹ SKILL (Index)
            int skillIndex = -1;
            object skillObj = null; // Struktura Skill
            for (int i = 0; i < player.skills.Count; i++)
            {
                if (player.skills[i].data != null && player.skills[i].data.name == skillName)
                {
                    skillIndex = i;
                    skillObj = player.skills[i];
                    break;
                }
            }
            if (skillIndex == -1) return;

            // 2. ZNAJDŹ CEL (Target)
            // Używamy ulepszonej metody szukania celu w hierarchii klas
            NetworkIdentity target = GetTargetRobust(player);

            // Jeśli brak manualnego celu, szukaj surowca
            if (target == null)
            {
                target = FindResourceForSkill(player, skillName);
            }

            // 3. WYKONAJ ATAK
            if (target != null)
            {
                Vector3 centerPoint = GetCenterPoint(target);

                // A. Synchronizacja Celu
                SetTarget(player, target);

                // B. Obrót w stronę celu (żeby Pointed Skill poleciał dobrze)
                Vector3 lookPos = centerPoint;
                lookPos.y = player.transform.position.y;
                player.transform.LookAt(lookPos);

                // C. Sprawdź typ skilla (IsPointedSkill)
                // W WTPlayer.cs logika jest taka: jeśli Pointed -> AbilitySkillToPoint, w przeciwnym razie UseSkill
                if (IsPointedSkill(skillObj))
                {
                    CmdAbilitySkillToPoint(player, skillIndex, centerPoint);
                }
                else
                {
                    // Skille namierzane (Targeted) ignorują punkt i lecą w zaznaczony cel
                    CmdUseSkill(player, skillIndex);
                }
            }
            else
            {
                // Brak celu -> Skill na siebie lub w powietrze
                CmdUseSkill(player, skillIndex);
            }
        }

        // --- PANCERNY GET TARGET ---
        NetworkIdentity GetTargetRobust(global::WTPlayer player)
        {
            // Przeszukujemy całą hierarchię klas (WTPlayer -> Player -> Entity -> NetworkBehaviour)
            // Szukamy zarówno public/private PÓL jak i WŁAŚCIWOŚCI o nazwie "target"
            Type type = player.GetType();
            while (type != null && type != typeof(object))
            {
                // 1. Sprawdź Pola
                FieldInfo f = type.GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null)
                {
                    var val = f.GetValue(player) as NetworkBehaviour;
                    if (val != null) return val.netIdentity;
                }

                // 2. Sprawdź Właściwości (Properties)
                PropertyInfo p = type.GetProperty("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (p != null && p.CanRead)
                {
                    var val = p.GetValue(player, null) as NetworkBehaviour;
                    if (val != null) return val.netIdentity;
                }

                type = type.BaseType;
            }
            return null;
        }

        // --- REFLECTION HELPERS ---

        bool IsPointedSkill(object skillObj)
        {
            // Próbujemy wywołać skill.IsPointedSkill()
            // Metoda może być w strukturze Skill lub jako Extension Method.
            // Tutaj zakładamy metodę instancji (jak w WTPlayer.cs)
            try
            {
                MethodInfo m = skillObj.GetType().GetMethod("IsPointedSkill", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null) return (bool)m.Invoke(skillObj, null);

                // Alternatywa: Sprawdź typ ScriptableSkill (data)
                // WTAbilitySkill zazwyczaj jest Pointed, chyba że to TargetSkill
                var dataProp = skillObj.GetType().GetProperty("data");
                if (dataProp != null)
                {
                    var data = dataProp.GetValue(skillObj, null);
                    if (data != null)
                    {
                        // Jeśli to WTAbilityTargetSkill -> False (Targeted)
                        if (data.GetType().Name.Contains("TargetSkill")) return false;
                        // Jeśli to WTAbilitySkill (ale nie Target) -> True (Pointed)
                        if (data.GetType().Name.Contains("AbilitySkill")) return true;
                    }
                }
            }
            catch { }
            return false; // Domyślnie False (bezpieczniej użyć CmdUseSkill)
        }

        void CmdAbilitySkillToPoint(global::WTPlayer player, int index, Vector3 point)
        {
            var m = player.GetType().GetMethod("CmdAbilitySkillToPoint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null) m.Invoke(player, new object[] { index, point });
        }

        void CmdUseSkill(global::WTPlayer player, int index)
        {
            var m = player.GetType().GetMethod("CmdUseSkill", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null) m.Invoke(player, new object[] { index });
        }

        void SetTarget(global::WTPlayer player, NetworkIdentity target)
        {
            var m = player.GetType().GetMethod("CmdSetTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null) m.Invoke(player, new object[] { target });

            // Często potrzebne dla interakcji z NPC/Surowcami
            var m2 = player.GetType().GetMethod("CmdSetActionTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m2 != null) m2.Invoke(player, new object[] { target });
        }

        NetworkIdentity FindResourceForSkill(global::WTPlayer player, string skillName)
        {
            Collider[] hits = Physics.OverlapSphere(player.transform.position, 6.0f);
            NetworkIdentity closest = null;
            float closestDist = 999f;
            foreach (var hit in hits)
            {
                var ni = hit.GetComponentInParent<NetworkIdentity>();
                if (ni == null || ni.gameObject == player.gameObject) continue;
                if (ObjectRequiresSkill(ni.gameObject, skillName))
                {
                    float d = Vector3.Distance(player.transform.position, ni.transform.position);
                    if (d < closestDist) { closestDist = d; closest = ni; }
                }
            }
            return closest;
        }

        bool ObjectRequiresSkill(GameObject obj, string playerSkillName)
        {
            var components = obj.GetComponentsInParent<MonoBehaviour>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                Type type = comp.GetType();
                FieldInfo field = type.GetField("actionSkills", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    var list = field.GetValue(comp) as IEnumerable;
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            string requiredName = GetNameFromScriptable(item);
                            if (!string.IsNullOrEmpty(requiredName) &&
                                (requiredName.IndexOf(playerSkillName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 playerSkillName.IndexOf(requiredName, StringComparison.OrdinalIgnoreCase) >= 0))
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        Vector3 GetCenterPoint(NetworkIdentity target)
        {
            var col = target.GetComponent<Collider>();
            if (col != null) return col.bounds.center;
            return target.transform.position + Vector3.up;
        }

        // --- UI DRAWING & HELPERS ---
        void DrawSelectorWindow(int id)
        {
            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) { _showSelector = false; return; }

            GUILayout.BeginVertical();
            _selectorScroll = GUILayout.BeginScrollView(_selectorScroll);
            if (player.skills != null)
            {
                foreach (var skill in player.skills)
                {
                    if (skill.data != null)
                    {
                        GUILayout.BeginHorizontal("box");
                        Sprite sp = GetSpriteFromScriptable(skill.data);
                        Rect iconRect = GUILayoutUtility.GetRect(35, 35, GUILayout.Width(35), GUILayout.Height(35));
                        if (sp != null) DrawSprite(iconRect, sp);
                        if (GUILayout.Button(skill.data.name, GUILayout.Height(35)))
                        {
                            SlotSkills[_slotToAssign] = skill.data.name;
                            SaveData();
                            _showSelector = false;
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("ANULUJ", GUILayout.Height(30))) _showSelector = false;
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            Rect texCoords = new Rect(sprite.rect.x / sprite.texture.width, sprite.rect.y / sprite.texture.height, sprite.rect.width / sprite.texture.width, sprite.rect.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, texCoords);
        }

        private Sprite GetSkillSprite(string name)
        {
            if (_iconCache.ContainsKey(name)) return _iconCache[name];
            var p = global::Player.localPlayer as global::WTPlayer;
            if (p != null && p.skills != null)
            {
                var skill = p.skills.FirstOrDefault(x => x.data != null && x.data.name == name);
                if (skill.data != null)
                {
                    Sprite s = GetSpriteFromScriptable(skill.data);
                    if (s != null) { _iconCache[name] = s; return s; }
                }
            }
            return null;
        }

        private Sprite GetSpriteFromScriptable(ScriptableObject data)
        {
            try
            {
                FieldInfo f = data.GetType().GetField("image", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) f = data.GetType().GetField("icon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f.GetValue(data) as Sprite;
            }
            catch { }
            return null;
        }

        string GetNameFromScriptable(object obj)
        {
            if (obj == null) return null;
            var uObj = obj as UnityEngine.Object;
            if (uObj != null) return uObj.name;
            return null;
        }

        private void HandleBindingEvent()
        {
            if (_slotBinding != -1)
            {
                Event e = Event.current;
                if (e.isKey && e.keyCode != KeyCode.None)
                {
                    SlotKeys[_slotBinding] = e.keyCode;
                    _slotBinding = -1;
                    SaveData();
                    e.Use();
                }
                else if (e.isMouse && e.type == EventType.MouseDown) _slotBinding = -1;
            }
        }

        void OpenSelector(int slotIndex) { _slotToAssign = slotIndex; _showSelector = true; _selectorScroll = Vector2.zero; }

        void SaveData()
        {
            string sSkills = string.Join(";", SlotSkills);
            PlayerPrefs.SetString("WT_VBar_Skills", sSkills);
            string[] keysAsInts = SlotKeys.Select(k => ((int)k).ToString()).ToArray();
            string sKeys = string.Join(";", keysAsInts);
            PlayerPrefs.SetString("WT_VBar_Keys", sKeys);
            PlayerPrefs.Save();
        }

        void LoadData()
        {
            string sSkills = PlayerPrefs.GetString("WT_VBar_Skills", "");
            if (!string.IsNullOrEmpty(sSkills))
            {
                var parts = sSkills.Split(';');
                for (int i = 0; i < Math.Min(parts.Length, MAX_SLOTS); i++) SlotSkills[i] = parts[i];
            }
            string sKeys = PlayerPrefs.GetString("WT_VBar_Keys", "");
            if (!string.IsNullOrEmpty(sKeys))
            {
                var parts = sKeys.Split(';');
                for (int i = 0; i < Math.Min(parts.Length, MAX_SLOTS); i++)
                {
                    if (int.TryParse(parts[i], out int k)) SlotKeys[i] = (KeyCode)k;
                }
            }
        }

        private GUIStyle CenteredStyle()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.alignment = TextAnchor.MiddleCenter;
            s.normal.textColor = Color.black;
            return s;
        }
    }
}