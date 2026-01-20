using System;
using UnityEngine;
using UnityEngine.AI;

namespace WildTerraHook
{
    public class MiscModule
    {
        // --- USTAWIENIA ---
        public bool NoClipEnabled = false;
        public bool EternalDayEnabled = false; // Przywróciłem zmienną, aby można było ją dodać do menu

        // --- CACHE ---
        private NavMeshAgent _agent;
        private Collider _collider;
        private bool _wasAgentEnabled;
        private bool _wasColliderEnabled;

        // Przechowujemy gracza, żeby nie szukać go co klatkę
        private global::Player _cachedPlayer;

        // Ta metoda jest wywoływana przez MainHack.cs (wiersz 54)
        public void Update()
        {
            // 1. Znalezienie gracza (jeśli zgubiony lub null)
            if (_cachedPlayer == null)
            {
                // Próbujemy pobrać z oficjalnego statica (jeśli istnieje)
                try
                {
                    if (global::Player.localPlayer != null)
                        _cachedPlayer = global::Player.localPlayer;
                }
                catch { }

                // Fallback: Szukamy na scenie jeśli static zawiódł
                if (_cachedPlayer == null)
                {
                    var foundPlayers = UnityEngine.Object.FindObjectsOfType<global::Player>();
                    if (foundPlayers != null && foundPlayers.Length > 0)
                    {
                        // Zakładamy, że pierwszy to nasz (w single player/simple multi to zadziała)
                        _cachedPlayer = foundPlayers[0];
                    }
                }
            }

            // Jeśli nadal nie mamy gracza, przerywamy
            if (_cachedPlayer == null) return;

            // 2. Obsługa NoClip
            if (NoClipEnabled)
            {
                HandleNoClip(_cachedPlayer);
            }
            else
            {
                RestoreState(_cachedPlayer);
            }

            // 3. Obsługa Eternal Day (Opcjonalnie - prosty przykład, jeśli gra używa EnviroSky)
            if (EternalDayEnabled)
            {
                // To jest placeholder. Jeśli gra używa np. EnviroSky, można odkomentować:
                /*
                var sky = UnityEngine.Object.FindObjectOfType<EnviroSky>();
                if (sky != null) sky.GameTime.Hours = 12f;
                */
            }
        }

        // Ta metoda jest wywoływana przez MainHack.cs (wiersz 94)
        public void DrawMenu()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Misc Options</b>");

            NoClipEnabled = GUILayout.Toggle(NoClipEnabled, "No Clip (Latanie / Przechodzenie)");
            EternalDayEnabled = GUILayout.Toggle(EternalDayEnabled, "Eternal Day (Wymuś Dzień)");

            GUILayout.EndVertical();
        }

        // --- LOGIKA NOCLIP ---

        private void HandleNoClip(global::Player player)
        {
            // Pobieranie komponentów (lenistwo - tylko raz)
            if (_agent == null) _agent = player.GetComponent<NavMeshAgent>();
            if (_collider == null) _collider = player.GetComponent<Collider>();

            // Wyłączanie NavMeshAgent (kluczowe dla gór i wody)
            if (_agent != null && _agent.enabled)
            {
                _wasAgentEnabled = true;
                _agent.enabled = false;
            }

            // Wyłączanie kolizji (opcjonalne, pozwala przechodzić przez ściany)
            if (_collider != null && _collider.enabled)
            {
                _wasColliderEnabled = true;
                _collider.enabled = false;
            }

            MovePlayer(player);
        }

        private void MovePlayer(global::Player player)
        {
            float currentSpeed = 6.0f; // Bazowa prędkość

            // Pobieramy legalną prędkość z agenta, jeśli istnieje, aby serwer nie cofał
            if (_agent != null)
            {
                currentSpeed = _agent.speed;
            }

            // Sprint
            if (Input.GetKey(KeyCode.LeftShift))
            {
                currentSpeed *= 2.0f;
            }

            Vector3 moveDirection = Vector3.zero;

            // Pobieramy kamerę, aby sterowanie było zgodne z widokiem
            Transform camTransform = Camera.main != null ? Camera.main.transform : null;

            if (camTransform != null)
            {
                Vector3 forward = camTransform.forward;
                Vector3 right = camTransform.right;

                // Ignorujemy pochylenie kamery dla ruchu WSAD
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();

                if (Input.GetKey(KeyCode.W)) moveDirection += forward;
                if (Input.GetKey(KeyCode.S)) moveDirection -= forward;
                if (Input.GetKey(KeyCode.D)) moveDirection += right;
                if (Input.GetKey(KeyCode.A)) moveDirection -= right;
            }
            else
            {
                // Fallback: ruch względem świata
                if (Input.GetKey(KeyCode.W)) moveDirection += Vector3.forward;
                if (Input.GetKey(KeyCode.S)) moveDirection -= Vector3.forward;
                if (Input.GetKey(KeyCode.D)) moveDirection += Vector3.right;
                if (Input.GetKey(KeyCode.A)) moveDirection -= Vector3.left;
            }

            // Latanie góra/dół (Spacja / Ctrl)
            if (Input.GetKey(KeyCode.Space)) moveDirection += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) moveDirection -= Vector3.up;

            // Aplikowanie ruchu bezpośrednio do Transform (omijanie fizyki)
            if (moveDirection != Vector3.zero)
            {
                player.transform.position += moveDirection.normalized * currentSpeed * Time.deltaTime;
            }
        }

        private void RestoreState(global::Player player)
        {
            // Przywracamy Agenta tylko jeśli my go wyłączyliśmy
            if (_agent != null && !_agent.enabled && _wasAgentEnabled)
            {
                NavMeshHit hit;
                // Szukamy bezpiecznego miejsca na ziemi w promieniu 10 jednostek
                if (NavMesh.SamplePosition(player.transform.position, out hit, 10.0f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position); // Teleport na siatkę
                    _agent.enabled = true;
                    _wasAgentEnabled = false;
                }
            }

            // Przywracamy kolizje
            if (_collider != null && !_collider.enabled && _wasColliderEnabled)
            {
                _collider.enabled = true;
                _wasColliderEnabled = false;
            }
        }
    }
}