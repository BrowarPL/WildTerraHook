using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using System.Reflection;
using System;
using System.Collections;

namespace WildTerraHook
{
    public class AutoActionModule
    {
        private float _nextActionTime = 0f;

        // Czarna lista tylko dla bezpieczeństwa sieciowego (krótki czas)
        private Dictionary<uint, float> _temporaryBlacklist = new Dictionary<uint, float>();

        // Zmienne Scannera
        public bool IsScanning = false;
        private int _scanCurrentId = 0;
        private float _scanTimer = 0f;
        private NetworkIdentity _scanTarget;

        // Cache metod
        private MethodInfo _cmdUseSkill;
        private MethodInfo _cmdAbilitySkillToPoint;
        private MethodInfo _cmdSetActionTarget;
        private MethodInfo _cmdSetTarget;
        private bool _methodsCached = false;

        // Lista do wyświetlania w menu
        public static readonly Dictionary<int, string> KnownActions = new Dictionary<int, string>
        {
                        { 0, "0: HandAttack" },
            { 1, "1: SwordAttack" },
            { 2, "2: TwoHandSwordAttack" },
            { 3, "3: MaceAttack" },
            { 4, "4: AxeAttack" },
            { 5, "5: KnifeAttack" },
            { 6, "6: BowAttack" },
            { 7, "7: CrossbowAttack" },
            { 8, "8: ThrowSpearAttack" },
            { 9, "9: ThrowBombAttack" },
            { 10, "10: ThrowBallAttack" },
            { 11, "11: SlingAttack" },
            { 12, "12: SnareAttack" },
            { 13, "13: BolasAttack" },
            { 14, "14: PickUp" },
            { 15, "15: NpcAction" },
            { 16, "16: ChangeState" },
            { 17, "17: OpenContainer" },
            { 18, "18: EditStructures" },
            { 19, "19: Suicide" },
            { 20, "20: ReportPlayer" },
            { 21, "21: Fishing" },
            { 22, "22: Catching" },
            { 23, "23: LeaveDwelling" },
            { 24, "24: LeaveDominiumArea" },
            { 25, "25: Gathering" },
            { 26, "26: WaterGathering" },
            { 27, "27: Planting" },
            { 28, "28: Shoveling" },
            { 29, "29: Watering" },
            { 30, "30: ItemUsing" },
            { 31, "31: Lumbering" },
            { 32, "32: Mining" },
            { 33, "33: Skinning" },
            { 34, "34: RevivePointSave" },
            { 35, "35: Sprint" },
            { 36, "36: WontRun" },
            { 37, "37: HeadShot" },
            { 38, "38: Tenacity" },
            { 39, "39: HoldLine" },
            { 40, "40: ShieldStrike" },
            { 41, "41: ArrowsBlock" },
            { 42, "42: HeavyShieldStrike" },
            { 43, "43: ShieldPutUp" },
            { 44, "44: ShieldBehind" },
            { 45, "45: AbsoluteProtection" },
            { 46, "46: Provocation" },
            { 47, "47: LetsBleed" },
            { 48, "48: Smithereens" },
            { 49, "49: Backstab" },
            { 50, "50: DeathBlow" },
            { 51, "51: RotatingStun" },
            { 52, "52: WeekSpotBlow" },
            { 53, "53: InTwain" },
            { 54, "54: IdealStance" },
            { 55, "55: IntoTheEye" },
            { 56, "56: BowPenetration" },
            { 57, "57: PetHeal" },
            { 58, "58: PetMasterBuff" },
            { 59, "59: StunWakeUp" },
            { 60, "60: SacrificeHealth" },
            { 61, "61: Purification" },
            { 62, "62: Identify" },
            { 63, "63: GatheringExperience" },
            { 64, "64: LumberjackFuel" },
            { 65, "65: FisheryArt" },
            { 66, "66: PetLoyalty" },
            { 67, "67: IronEconomy" },
            { 68, "68: SteelEconomy" },
            { 69, "69: CarpentryInspiration" },
            { 70, "70: MaterialEconomy" },
            { 71, "71: Plowman" },
            { 72, "72: Feint" },
            { 73, "73: Puncture" },
            { 74, "74: SpinAttack" },
            { 75, "75: CrushingBlow" },
            { 76, "76: HealerToolAttack" },
            { 77, "77: ThrowAxe" },
            { 78, "78: Blessing" },
            { 79, "79: Eating" },
            { 80, "80: MiningEconomy" },
            { 81, "81: MiningTricks" },
            { 82, "82: SkinningExperience" },
            { 83, "83: SkinningRareChance" },
            { 84, "84: LumberingRareChance" },
            { 85, "85: TamingExperiencedTamer" },
            { 86, "86: EssenceExtraction" },
            { 87, "87: PetSkinning" },
            { 88, "88: WitchcraftBattleHunger" },
            { 89, "89: WitchcraftProtection" },
            { 90, "90: WitchcraftBurningRing" },
            { 91, "91: WitchcraftPoisonProjectile" },
            { 92, "92: WitchcraftWeakness" },
            { 93, "93: OnTheRun" },
            { 94, "94: CripplingShot" },
            { 95, "95: BoltBooster" },
            { 96, "96: LeatherworkingInspiration" },
            { 97, "97: JewelcraftingInspiration" }
        };

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.L)) InspectObjectUnderMouse();

            if (IsScanning) { HandleScanning(); return; }

            if (!ConfigManager.AutoAction_Enabled) return;

            CleanupBlacklist();

            if (Time.time >= _nextActionTime)
            {
                ScanAndInteract();
            }
        }

        private void ScanAndInteract()
        {
            var player = global::Player.localPlayer as global::WTPlayer;
            if (player == null) return;
            if (!_methodsCached) CacheMethods(player);

            // 1. Pobierz nazwę wybranego skilla z plecaka gracza
            // To pozwoli nam sprawdzić, czy obiekt "chce" tego skilla.
            string selectedSkillName = GetPlayerSkillName(player, ConfigManager.AutoAction_ID);

            Collider[] hits = Physics.OverlapSphere(player.transform.position, ConfigManager.AutoAction_Range);
            var candidates = new List<NetworkIdentity>();

            foreach (var hit in hits)
            {
                var ni = hit.GetComponentInParent<NetworkIdentity>();

                if (ni == null || ni.netId == 0) continue;
                if (ni.gameObject == player.gameObject) continue;
                if (_temporaryBlacklist.ContainsKey(ni.netId)) continue;

                // --- SMART MATCHING ---
                // Sprawdź, czy obiekt wymaga DOKŁADNIE tego skilla, który mamy wybranego.
                if (!ObjectRequiresSkill(ni.gameObject, selectedSkillName)) continue;

                candidates.Add(ni);
            }

            // Sortuj od najbliższego
            var nearest = candidates
                .OrderBy(ni => Vector3.Distance(player.transform.position, ni.transform.position))
                .FirstOrDefault();

            if (nearest != null)
            {
                Interact(player, nearest, ConfigManager.AutoAction_ID);
            }
        }

        // --- CORE LOGIC: Sprawdzanie czy obiekt "pasuje" do skilla ---
        private bool ObjectRequiresSkill(GameObject obj, string playerSkillName)
        {
            // Jeśli nie udało się pobrać nazwy skilla gracza (np. ID poza zakresem), 
            // fallback do starej metody "czy ma jakiekolwiek akcje"
            if (string.IsNullOrEmpty(playerSkillName)) return HasAnyAction(obj);

            var components = obj.GetComponentsInParent<MonoBehaviour>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                Type type = comp.GetType();

                // Sprawdzamy pole actionSkills (List<ScriptableSkill>)
                FieldInfo field = type.GetField("actionSkills", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    var list = field.GetValue(comp) as IEnumerable;
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            // Wyciągamy nazwę wymaganego skilla z obiektu
                            string requiredName = GetSkillNameFromScriptable(item);

                            // Porównujemy: Czy obiekt wymaga skilla "Agriculture", a my mamy wybrane "Agriculture"?
                            // Używamy Contains, bo czasem nazwy mają suffixy/prefixy
                            if (!string.IsNullOrEmpty(requiredName) &&
                                (requiredName.Contains(playerSkillName) || playerSkillName.Contains(requiredName)))
                            {
                                return true; // PASUJE!
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool HasAnyAction(GameObject obj)
        {
            var components = obj.GetComponentsInParent<MonoBehaviour>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                Type t = comp.GetType();
                FieldInfo f = t.GetField("actionSkills", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    var list = f.GetValue(comp) as IEnumerable;
                    if (list != null && list.GetEnumerator().MoveNext()) return true;
                }
            }
            return false;
        }

        private string GetPlayerSkillName(global::WTPlayer player, int index)
        {
            if (player.skills == null || index < 0 || index >= player.skills.Count) return null;
            var data = player.skills[index].data;
            return data != null ? data.name : null;
        }

        private string GetSkillNameFromScriptable(object scriptableSkill)
        {
            if (scriptableSkill == null) return null;
            try
            {
                // Próba odczytu pola 'name' z ScriptableObject
                PropertyInfo prop = scriptableSkill.GetType().GetProperty("name");
                if (prop != null) return prop.GetValue(scriptableSkill, null) as string;

                var uObj = scriptableSkill as UnityEngine.Object;
                if (uObj != null) return uObj.name;
            }
            catch { }
            return null;
        }

        private void Interact(global::WTPlayer player, NetworkIdentity target, int skillId)
        {
            try
            {
                if (_cmdSetTarget != null) _cmdSetTarget.Invoke(player, new object[] { target });
                if (_cmdSetActionTarget != null) _cmdSetActionTarget.Invoke(player, new object[] { target });

                bool used = false;

                if (_cmdUseSkill != null)
                {
                    _cmdUseSkill.Invoke(player, new object[] { skillId });
                    used = true;
                }

                if (!used && _cmdAbilitySkillToPoint != null)
                {
                    _cmdAbilitySkillToPoint.Invoke(player, new object[] { skillId, target.transform.position });
                    used = true;
                }

                if (used && !IsScanning)
                {
                    Debug.Log($"[AutoAction] Wykonano Skill {skillId} na {target.name}");

                    // Blacklistujemy tylko na chwilę, żeby serwer zdążył przetworzyć.
                    // Jeśli akcja się uda (np. zasadzisz roślinę), to przy następnym skanie
                    // obiekt nie będzie już wymagał skilla "Agriculture", więc Smart Match go odrzuci naturalnie.
                    _temporaryBlacklist[target.netId] = Time.time + 1.0f; // Krótki czas

                    _nextActionTime = Time.time + ConfigManager.AutoAction_Delay;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AutoAction] Błąd Interact: {ex.Message}");
            }
        }

        private void CleanupBlacklist()
        {
            var toRemove = new List<uint>();
            foreach (var kvp in _temporaryBlacklist)
            {
                if (Time.time > kvp.Value) toRemove.Add(kvp.Key);
            }
            foreach (var key in toRemove) _temporaryBlacklist.Remove(key);
        }

        private void CacheMethods(object player)
        {
            var type = player.GetType();
            _cmdUseSkill = type.GetMethod("CmdUseSkill", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _cmdAbilitySkillToPoint = type.GetMethod("CmdAbilitySkillToPoint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _cmdSetActionTarget = type.GetMethod("CmdSetActionTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _cmdSetTarget = type.GetMethod("CmdSetTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _methodsCached = true;
        }

        // --- SCANNER ---
        private void HandleScanning()
        {
            // (Kod skanera bez zmian - służy tylko do diagnostyki)
            if (!_methodsCached) CacheMethods(global::Player.localPlayer);
            if (_scanTarget == null || _scanTarget.netId == 0)
            {
                var player = global::Player.localPlayer as global::WTPlayer;
                if (player == null) return;
                Collider[] hits = Physics.OverlapSphere(player.transform.position, 5.0f);
                float minDist = 999f;
                foreach (var hit in hits)
                {
                    var ni = hit.GetComponentInParent<NetworkIdentity>();
                    if (ni != null && ni.gameObject != player.gameObject)
                    {
                        float d = Vector3.Distance(player.transform.position, ni.transform.position);
                        if (d < minDist) { minDist = d; _scanTarget = ni; }
                    }
                }
                if (_scanTarget == null) { IsScanning = false; return; }
                Debug.LogWarning($"[Scanner] Cel: {_scanTarget.name}. Start 0-100...");
                _scanCurrentId = 0;
            }

            if (Time.time > _scanTimer)
            {
                if (_scanCurrentId > 100) { IsScanning = false; _scanTarget = null; return; }
                var player = global::Player.localPlayer as global::WTPlayer;
                Interact(player, _scanTarget, _scanCurrentId);
                _scanCurrentId++;
                _scanTimer = Time.time + 0.2f;
            }
        }

        private void InspectObjectUnderMouse()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var target = hit.collider.gameObject;
                Debug.LogWarning($"=== INSPEKCJA: {target.name} ===");

                var comps = target.GetComponentsInParent<MonoBehaviour>();
                foreach (var comp in comps)
                {
                    var f = comp.GetType().GetField("actionSkills", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                    if (f != null)
                    {
                        var list = f.GetValue(comp) as IEnumerable;
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                Debug.Log($" > Wymaga Skilla: {GetSkillNameFromScriptable(item)}");
                            }
                        }
                    }
                }
            }
        }
    }
}