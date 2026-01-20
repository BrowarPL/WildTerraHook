using System;
using UnityEngine;
using UnityEngine.AI;

namespace WildTerraHook
{
    public class MiscModule : MonoBehaviour
    {
        // Główny przełącznik funkcji
        public bool NoClipEnabled = false;

        // Cache komponentów
        private NavMeshAgent _agent;
        private Collider _collider;
        private bool _wasAgentEnabled;
        private bool _wasColliderEnabled;

        public void Update()
        {
            // Bezpiecznik - jeśli nie ma gracza, nic nie robimy
            if (Player.localPlayer == null) return;

            if (NoClipEnabled)
            {
                HandleNoClip(Player.localPlayer);
            }
            else
            {
                // Jeśli wyłączyliśmy NoClip, upewnij się, że komponenty gry wróciły do normy
                RestoreState(Player.localPlayer);
            }
        }

        private void HandleNoClip(Player player)
        {
            // 1. Pobieramy komponenty, jeśli ich nie mamy
            if (_agent == null) _agent = player.GetComponent<NavMeshAgent>();
            if (_collider == null) _collider = player.GetComponent<Collider>();

            // 2. Wyłączamy NavMeshAgenta, aby "odkleić" się od ziemi i limitów mapy
            if (_agent != null && _agent.enabled)
            {
                _wasAgentEnabled = true;
                _agent.enabled = false;
            }

            // 3. Wyłączamy kolizje, aby przechodzić przez ściany (opcjonalne, ale przydatne w jaskiniach)
            if (_collider != null && _collider.enabled)
            {
                _wasColliderEnabled = true;
                _collider.enabled = false;
            }

            // 4. Logika poruszania się (Symulacja WSAD + Mysz)
            MovePlayer(player);
        }

        private void MovePlayer(Player player)
        {
            float currentSpeed = 5.0f; // Domyślna prędkość

            // Próbujemy pobrać legalną prędkość z agenta (nawet wyłączonego), aby serwer nie cofał
            if (_agent != null)
            {
                currentSpeed = _agent.speed;
            }

            // Mnożnik dla sprintu (Left Shift) - uważaj, serwer może to wykryć, jeśli przesadzisz
            if (Input.GetKey(KeyCode.LeftShift))
            {
                currentSpeed *= 1.5f;
            }

            Vector3 moveDirection = Vector3.zero;

            // Pobieramy kierunki kamery, aby sterowanie było intuicyjne
            Transform camTransform = Camera.main.transform;
            Vector3 forward = camTransform.forward;
            Vector3 right = camTransform.right;

            // Resetujemy wpływ osi Y na ruch przód/tył, żeby nie "wbijać" się w ziemię patrząc w dół
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // Obsługa klawiszy
            if (Input.GetKey(KeyCode.W)) moveDirection += forward;
            if (Input.GetKey(KeyCode.S)) moveDirection -= forward;
            if (Input.GetKey(KeyCode.D)) moveDirection += right;
            if (Input.GetKey(KeyCode.A)) moveDirection -= right;

            // Obsługa latania góra/dół (Spacja / Ctrl)
            if (Input.GetKey(KeyCode.Space)) moveDirection += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) moveDirection -= Vector3.up;

            // Aplikujemy ruch
            if (moveDirection != Vector3.zero)
            {
                // Używamy transform.position zamiast Move(), aby ominąć fizykę
                player.transform.position += moveDirection.normalized * currentSpeed * Time.deltaTime;
            }
        }

        private void RestoreState(Player player)
        {
            // Przywracamy Agenta tylko jeśli my go wyłączyliśmy i jeśli jest blisko NavMesha
            // (Inaczej gra może wyrzucić błąd, jeśli włączymy go nad przepaścią)
            if (_agent != null && !_agent.enabled && _wasAgentEnabled)
            {
                // Sprawdzamy czy jesteśmy w ogóle na NavMesh'u, żeby nie crashować gry
                NavMeshHit hit;
                if (NavMesh.SamplePosition(player.transform.position, out hit, 2.0f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position); // Bezpieczny powrót na siatkę
                    _agent.enabled = true;
                    _wasAgentEnabled = false;
                }
            }

            if (_collider != null && !_collider.enabled && _wasColliderEnabled)
            {
                _collider.enabled = true;
                _wasColliderEnabled = false;
            }
        }

        internal void DrawMenu()
        {
            throw new NotImplementedException();
        }
    }
}