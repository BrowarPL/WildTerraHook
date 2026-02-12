using UnityEngine;
using UnityEngine.AI;
using Mirror;
using System.Reflection;

namespace WildTerraHook
{
    public class FastAttackModule
    {
        private const float BASE_MOVE_SPEED = 4.5f;

        private NavMeshAgent _cachedAgent;
        private Animator _cachedAnimator;
        private FieldInfo _stateField;
        private FieldInfo _useSkillWhenCloserField;

        private bool _wasSpeedHackActive = false;

        public void Update()
        {
            if (!ConfigManager.FastAttack_Enabled)
            {
                ResetAnimator();
                return;
            }

            var player = Player.localPlayer as WTPlayer;
            if (player == null || player.skills == null) return;

            // Inicjalizacja Reflection
            if (_stateField == null)
                _stateField = typeof(Entity).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (_useSkillWhenCloserField == null)
                _useSkillWhenCloserField = typeof(Player).GetField("useSkillWhenCloser", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (_cachedAgent == null) _cachedAgent = player.GetComponent<NavMeshAgent>();
            if (_cachedAnimator == null) _cachedAnimator = player.GetComponentInChildren<Animator>();

            bool isAnySkillActive = false;

            // Iteracja przez skille
            for (int i = 0; i < player.skills.Count; i++)
            {
                Skill skill = player.skills[i];

                // Czy skill trwa (wg czasu sieciowego)?
                if (NetworkTime.time < skill.castTimeEnd)
                {
                    var data = skill.data;
                    if (data == null) continue;
                    if (data.baseCastTime <= 0.1f) continue;

                    isAnySkillActive = true;

                    // --- 1. STATE HACK (FORCE IDLE) ---
                    // To jest kluczowa zmiana. Wymuszamy stan IDLE w KAŻDEJ klatce trwania skilla.
                    // Gra myśli, że stoisz i nic nie robisz, więc zdejmuje wszelkie blokady inputu.
                    // Efekt uboczny: Pasek castowania może zniknąć, bo UI wymaga stanu "CASTING".
                    string currentState = (string)_stateField.GetValue(player);
                    if (currentState != "IDLE" && currentState != "DEAD")
                    {
                        _stateField.SetValue(player, "IDLE");

                        // Dodatkowo czyścimy kolejkę akcji i odblokowujemy agenta od razu
                        if (_useSkillWhenCloserField != null) _useSkillWhenCloserField.SetValue(player, -1);
                        if (_cachedAgent != null && _cachedAgent.isOnNavMesh) _cachedAgent.isStopped = false;
                        player.nextRiskyActionTime = 0;
                    }

                    // --- 2. PRZYSPIESZENIE (TIME COMPRESSION) ---
                    if (ConfigManager.FastAttack_CastSpeed > 0)
                    {
                        double timeSteal = Time.deltaTime * ConfigManager.FastAttack_CastSpeed;
                        skill.castTimeEnd -= timeSteal;

                        // Zapis zmian w pamięci
                        try { player.skills[i] = skill; } catch { }

                        // Wizualne przyspieszenie animacji
                        if (_cachedAnimator != null)
                        {
                            _cachedAnimator.speed = 1.0f + ConfigManager.FastAttack_CastSpeed;
                            _wasSpeedHackActive = true;
                        }
                    }

                    // --- 3. RUCH (SLIDE) ---
                    // Zostawiamy nasz Slide, bo jest pewniejszy niż standardowe chodzenie przy desynchronizacji
                    HandleManualMovement(player);

                    // --- 4. CUTOFF (FINISHER) ---
                    // Nadal potrzebujemy cutoff, żeby wyzerować timer na samym końcu
                    double remaining = skill.castTimeEnd - NetworkTime.time;
                    float progress = 1.0f - (float)(remaining / data.baseCastTime);

                    if (progress >= ConfigManager.FastAttack_Cutoff)
                    {
                        skill.castTimeEnd = 0;
                        try { player.skills[i] = skill; } catch { }
                    }
                }
            }

            // RESETOWANIE STANU GDY NIC NIE ROBIMY
            if (!isAnySkillActive)
            {
                ResetAnimator();

                if (ConfigManager.FastAttack_AlwaysMove)
                {
                    HandleManualMovement(player);
                }
            }
        }

        private void ResetAnimator()
        {
            if (_wasSpeedHackActive && _cachedAnimator != null)
            {
                _cachedAnimator.speed = 1.0f;
                _wasSpeedHackActive = false;
            }
        }

        private void HandleManualMovement(WTPlayer player)
        {
            Vector3 inputDir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) inputDir.z += 1;
            if (Input.GetKey(KeyCode.S)) inputDir.z -= 1;
            if (Input.GetKey(KeyCode.D)) inputDir.x += 1;
            if (Input.GetKey(KeyCode.A)) inputDir.x -= 1;

            if (inputDir == Vector3.zero) return;

            if (Camera.main == null) return;
            Transform cam = Camera.main.transform;
            Vector3 forward = cam.forward;
            Vector3 right = cam.right;
            forward.y = 0; right.y = 0;
            forward.Normalize(); right.Normalize();

            Vector3 moveDir = (forward * inputDir.z + right * inputDir.x).normalized;

            float finalSpeed = BASE_MOVE_SPEED * ConfigManager.FastAttack_MoveSpeed;
            if (finalSpeed <= 0.1f) return;

            Vector3 targetPos = player.transform.position + (moveDir * finalSpeed * Time.deltaTime);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 1.2f, NavMesh.AllAreas))
            {
                player.transform.position = hit.position;
            }
            else
            {
                player.transform.position = targetPos;
            }

            if (moveDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation, targetRot, 25f * Time.deltaTime);
            }
        }
    }
}